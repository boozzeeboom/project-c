// CraftingClientState.cs (T-C05, полная версия) - client-only singleton.
// Pattern: GatheringClientState (T-G04) + QuestClientState (T-Q11).
// Подписки: NetworkPlayer.ReceiveCraftingResultTargetRpc + ReceiveCraftingSnapshotTargetRpc.
// События для UI: OnCraftingProgress, OnCraftingCompleted, OnCraftingInterrupted,
//                  OnCraftingDenied, OnCraftingCancelled, OnSnapshotUpdated.

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectC.Crafting
{
    public class CraftingClientState : MonoBehaviour
    {
        public static CraftingClientState Instance { get; private set; }

        [SerializeField] private bool _dontDestroyOnLoad = true;

        [Header("Timeout")]
        [Tooltip("Если сервер не прислал snapshot с InProgress/Completed в течение этого времени (сек) — " +
                 "считаем что крафт прерван (сервер завис).")]
        [SerializeField] private float _serverTimeoutSec = 5f;

        // ==========================================================
        // Events (UI / логика подписывается)
        // ==========================================================

        /// <summary>Сервер прислал snapshot с активным InProgress. UI обновляет ProgressBar.</summary>
        public event Action<ulong, float, string> OnCraftingProgress;   // (stationNetId, progress01, resultItemName)

        /// <summary>Крафт завершён (server: state=Completed). itemName — что можно забрать.</summary>
        public event Action<ulong, string> OnCraftingCompleted;          // (stationNetId, resultItemName)

        /// <summary>Крафт прерван (server tick timeout / Cancel RPC / despawn). reason — human-readable.</summary>
        public event Action<ulong, string> OnCraftingInterrupted;        // (stationNetId, reason)

        /// <summary>Сервер отказал в операции (CraftingResultDto.Denied). reason — human-readable.</summary>
        public event Action<ulong, string> OnCraftingDenied;             // (stationNetId, reason)

        /// <summary>Крафт отменён игроком через Cancel RPC.</summary>
        public event Action<ulong> OnCraftingCancelled;                  // (stationNetId)

        /// <summary>Любое изменение snapshot'а. UI (CraftingWindow) перечитывает state/buffer/committed.</summary>
        public event Action<CraftingSnapshotDto> OnSnapshotUpdated;

        // ==========================================================
        // State
        // ==========================================================

        /// <summary>Кеш последнего snapshot'а для каждой подписанной станции. UI читает отсюда при открытии окна.</summary>
        private readonly Dictionary<ulong, CraftingSnapshotDto> _snapshots = new Dictionary<ulong, CraftingSnapshotDto>();

        /// <summary>T3: клиентский кеш рецептов (загружается из Resources один раз).</summary>
        private readonly Dictionary<int, RecipeData> _recipeCache = new Dictionary<int, RecipeData>();
        private bool _recipesLoaded;

        /// <summary>Текущая станция, с которой работает игрок (выбрана через F).</summary>
        public ulong CurrentStationNetId { get; private set; }

        /// <summary>Есть snapshot для этой станции?</summary>
        public bool HasSnapshot(ulong stationNetId) => _snapshots.ContainsKey(stationNetId);

        /// <summary>Получить snapshot. Если нет — возвращает default (state=Empty).</summary>
        public CraftingSnapshotDto GetSnapshot(ulong stationNetId)
        {
            return _snapshots.TryGetValue(stationNetId, out var s) ? s : new CraftingSnapshotDto { stationNetId = stationNetId, jobState = (byte)CraftingJobState.Empty, activeRecipeId = -1 };
        }

        // ==========================================================
        // Lifecycle
        // ==========================================================
        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                if (_dontDestroyOnLoad) DontDestroyOnLoad(gameObject);
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ==========================================================
        // Outgoing: client → server
        // ==========================================================

        public void RequestSubscribe(ulong stationNetId)
        {
            if (CraftingServer.Instance == null)
            {
                Debug.LogWarning("[CraftingClientState] RequestSubscribe: CraftingServer.Instance==null. (сервер ещё не стартовал?)");
                return;
            }
            CurrentStationNetId = stationNetId;
            _serverTimeoutCoroutine = StartCoroutine(ServerTimeoutWatcher(stationNetId));
            CraftingServer.Instance.SubscribeStationRpc(stationNetId);
        }

        public void RequestUnsubscribe(ulong stationNetId)
        {
            StopTimeoutWatcher();
            if (CraftingServer.Instance == null) return;
            CraftingServer.Instance.UnsubscribeStationRpc(stationNetId);
        }

        public void RequestAddIngredient(ulong stationNetId, int itemId, int quantity, CraftingSourceType source = CraftingSourceType.Inventory)
        {
            if (CraftingServer.Instance == null) return;
            CraftingServer.Instance.AddIngredientRpc(stationNetId, itemId, quantity, (byte)source);
        }

        public void RequestStartCraft(ulong stationNetId, int recipeId)
        {
            if (CraftingServer.Instance == null) return;
            // Restart timeout — InProgress должен прийти в течение _serverTimeoutSec
            StopTimeoutWatcher();
            _serverTimeoutCoroutine = StartCoroutine(ServerTimeoutWatcher(stationNetId));
            CraftingServer.Instance.StartCraftRpc(stationNetId, recipeId);
        }

        public void RequestCancelCraft(ulong stationNetId)
        {
            if (CraftingServer.Instance == null) return;
            CraftingServer.Instance.CancelCraftRpc(stationNetId);
        }

        // ==========================================================
        // T3: Client-side recipe & item cache (вместо прямых вызовов CraftingWorld)
        // ==========================================================

        /// <summary>Загрузить рецепты из Resources один раз.</summary>
        private void EnsureRecipesLoaded()
        {
            if (_recipesLoaded) return;
            var all = Resources.LoadAll<RecipeData>("Crafting/Recipes");
            foreach (var r in all)
            {
                if (r == null) continue;
                int recipeId = CraftingWorld.RegisterRecipe(r);
                if (!_recipeCache.ContainsKey(recipeId))
                    _recipeCache[recipeId] = r;
            }
            _recipesLoaded = true;
        }

        /// <summary>Получить RecipeData по id (из клиентского кеша).</summary>
        public RecipeData GetRecipe(int recipeId)
        {
            EnsureRecipesLoaded();
            _recipeCache.TryGetValue(recipeId, out var r);
            return r;
        }

        /// <summary>Получить отображаемое имя рецепта (для UI).</summary>
        public string GetRecipeDisplayName(int recipeId)
        {
            var r = GetRecipe(recipeId);
            return r != null ? r.DisplayName : "?";
        }

        /// <summary>ItemId для предмета (через InventoryWorld — работает и на клиенте).</summary>
        public int GetItemId(ProjectC.Items.ItemData item)
        {
            if (item == null) return -1;
            var inv = ProjectC.Items.InventoryWorld.Instance;
            if (inv == null) return -1;
            return inv.GetOrRegisterItemId(item);
        }

        /// <summary>ItemData по id (через InventoryWorld — работает и на клиенте).</summary>
        public ProjectC.Items.ItemData GetItem(int itemId)
        {
            var inv = ProjectC.Items.InventoryWorld.Instance;
            if (inv == null) return null;
            return inv.GetItemDefinition(itemId);
        }

        // ==========================================================
        // Outgoing (continued)
        // ==========================================================

        public void RequestCollect(ulong stationNetId)
        {
            if (CraftingServer.Instance == null) return;
            CraftingServer.Instance.CollectRpc(stationNetId);
        }

        // ==========================================================
        // Incoming: server → client
        // ==========================================================

        /// <summary>Вызывается из NetworkPlayer.ReceiveCraftingResultTargetRpc.</summary>
        public void OnCraftingResultReceived(CraftingResultDto result)
        {
            if (Debug.isDebugBuild) Debug.Log($"[CraftingClientState] Result received: station={result.stationNetId} code={result.code} msg={result.message}");
            CraftingResultCode code = (CraftingResultCode)result.code;
            switch (code)
            {
                case CraftingResultCode.Ok:
                    // Snapshot придёт отдельно (или уже пришёл) — просто сбрасываем таймаут.
                    StopTimeoutWatcher();
                    break;
                case CraftingResultCode.NotEnoughResources:
                case CraftingResultCode.StationBusy:
                case CraftingResultCode.NotOwner:
                case CraftingResultCode.NotFound:
                case CraftingResultCode.AlreadyStarted:
                case CraftingResultCode.AlreadyCompleted:
                case CraftingResultCode.InvalidArgs:
                case CraftingResultCode.InternalError:
                case CraftingResultCode.MetaReqDenied:
                case CraftingResultCode.RateLimited:
                    StopTimeoutWatcher();
                    try { OnCraftingDenied?.Invoke(result.stationNetId, string.IsNullOrEmpty(result.message) ? "Отказано" : result.message); }
                    catch (Exception ex) { Debug.LogError("[CraftingClientState] OnCraftingDenied handler threw: " + ex); }
                    break;
            }
        }

        /// <summary>Вызывается из NetworkPlayer.ReceiveCraftingSnapshotTargetRpc. Snapshot — authoritative state.</summary>
        public void OnCraftingSnapshotReceived(CraftingSnapshotDto snap)
        {
            if (Debug.isDebugBuild) Debug.Log($"[CraftingClientState] Snapshot received: station={snap.stationNetId} state={snap.jobState} owner={snap.ownerClientId} recipe={snap.activeRecipeId}");
            _snapshots[snap.stationNetId] = snap;

            // FIX T-C07: Любой snapshot от сервера = сервер жив, таймаут сбрасываем
            StopTimeoutWatcher();

            CraftingJobState state = (CraftingJobState)snap.jobState;
            switch (state)
            {
                case CraftingJobState.InProgress:
                    // FIX T-C07: progress server-computed (избегаем clock drift ServerTime vs realtimeSinceStartup)
                    RestartTimeoutWatcher(snap.stationNetId);
                    try { OnCraftingProgress?.Invoke(snap.stationNetId, snap.progress, snap.resultItemName ?? ""); }
                    catch (Exception ex) { Debug.LogError("[CraftingClientState] OnCraftingProgress handler threw: " + ex); }
                    break;

                case CraftingJobState.Completed:
                    StopTimeoutWatcher();
                    try { OnCraftingCompleted?.Invoke(snap.stationNetId, snap.resultItemName ?? "Результат"); }
                    catch (Exception ex) { Debug.LogError("[CraftingClientState] OnCraftingCompleted handler threw: " + ex); }
                    break;

                case CraftingJobState.Empty:
                    // Возможно — Cancel. Сервер присылает snapshot после каждого CancelCraftRpc → state=Buffered/Empty
                    // Проверим: если до этого был InProgress, значит Cancel.
                    if (_snapshots.TryGetValue(snap.stationNetId, out var prev) && prev.jobState == (byte)CraftingJobState.InProgress)
                    {
                        StopTimeoutWatcher();
                        try { OnCraftingCancelled?.Invoke(snap.stationNetId); }
                        catch (Exception ex) { Debug.LogError("[CraftingClientState] OnCraftingCancelled handler threw: " + ex); }
                    }
                    break;

                case CraftingJobState.Buffered:
                    // Owner UI: ингредиенты на станции, можно StartCraft
                    break;
            }

            try { OnSnapshotUpdated?.Invoke(snap); }
            catch (Exception ex) { Debug.LogError("[CraftingClientState] OnSnapshotUpdated handler threw: " + ex); }
        }

        // ==========================================================
        // Server timeout watcher
        // ==========================================================
        private Coroutine _serverTimeoutCoroutine;
        private ulong _timeoutStationNetId;

        private IEnumerator ServerTimeoutWatcher(ulong stationNetId)
        {
            _timeoutStationNetId = stationNetId;
            float elapsed = 0f;
            while (elapsed < _serverTimeoutSec)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            // Таймаут — сервер не прислал snapshot. Считаем прерванным.
            Debug.LogWarning($"[CraftingClientState] Server timeout ({_serverTimeoutSec}s) — крафт на станции {stationNetId} прерван");
            try { OnCraftingInterrupted?.Invoke(stationNetId, "Сервер не отвечает"); }
            catch (Exception ex) { Debug.LogError("[CraftingClientState] OnCraftingInterrupted (timeout) handler threw: " + ex); }
            _serverTimeoutCoroutine = null;
        }

        private void StopTimeoutWatcher()
        {
            if (_serverTimeoutCoroutine != null)
            {
                StopCoroutine(_serverTimeoutCoroutine);
                _serverTimeoutCoroutine = null;
            }
        }

        private void RestartTimeoutWatcher(ulong stationNetId)
        {
            StopTimeoutWatcher();
            _serverTimeoutCoroutine = StartCoroutine(ServerTimeoutWatcher(stationNetId));
        }
    }
}