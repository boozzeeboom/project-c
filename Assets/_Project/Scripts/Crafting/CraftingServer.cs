// CraftingServer.cs (T-C03) - server-side RPC hub. Pattern: GatheringServer T-G03 + MarketServer.
// Owns: CraftingWorld init/shutdown, CraftingTimeService subscription, recipe registry, rate limit.
// Scene-placed in BootstrapScene (T-C07). Singleton, server-only.
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using ProjectC.Player;

namespace ProjectC.Crafting
{
    [RequireComponent(typeof(NetworkObject))]
    public class CraftingServer : NetworkBehaviour
    {
        public static CraftingServer Instance { get; private set; }

        [Header("Recipes")]
        [Tooltip("Базовые рецепты, зарегистрированные при OnNetworkSpawn. RecipeData -> компактный int id.")]
        [SerializeField] private List<RecipeData> baseRecipes = new List<RecipeData>();

        [Header("Rate Limit")]
        [Tooltip("Максимум крафт-операций от одного клиента в минуту. 0 = без лимита.")]
        [SerializeField] private int _maxOpsPerMinute = 30;

        [Header("Debug")]
        [SerializeField] private bool _debugMode = true;

        // server-only
        private CraftingTimeService _timeService;
        private Dictionary<ulong, List<float>> _opTimestamps = new Dictionary<ulong, List<float>>();

        // ==========================================================
        // NetworkBehaviour lifecycle
        // ==========================================================
        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            Debug.Log($"[CraftingServer] OnNetworkSpawn: Instance set={(Instance==null)}, IsServer={IsServer}");
            if (Instance == null) Instance = this;
            if (!IsServer) { enabled = false; return; }

            // 1. World init
            CraftingWorld.CreateAndInitialize();
            foreach (var r in baseRecipes)
            {
                if (r != null) CraftingWorld.RegisterRecipe(r);
            }

            // 2. TimeService
            _timeService = CraftingTimeService.Instance;
            if (_timeService == null)
            {
                var go = new GameObject("[CraftingTimeService]");
                _timeService = go.AddComponent<CraftingTimeService>();
            }
            _timeService.OnServerStarted();
            _timeService.onCraftingTick.AddListener(OnCraftingTick);

            Debug.Log($"[CraftingServer] Server init done: recipes={baseRecipes?.Count ?? 0}, rateLimit={_maxOpsPerMinute}/min, timeService={(CraftingTimeService.Instance!=null)}");
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();
            if (IsServer)
            {
                if (_timeService != null) _timeService.onCraftingTick.RemoveListener(OnCraftingTick);
                CraftingWorld.Shutdown();
                _opTimestamps.Clear();
            }
            if (Instance == this) Instance = null;
        }

        // ==========================================================
        // Tick (CraftingTimeService -> CraftingWorld)
        // ==========================================================
        private void OnCraftingTick(float dt)
        {
            // Server-time = NM.ServerTime.Time, единые часы с CraftingJob.StartTime
            float serverTime = NetworkManager != null ? (float)NetworkManager.ServerTime.Time : Time.realtimeSinceStartup;
            CraftingWorld.OnTick(serverTime);

            // Push прогресс всем активным подписчикам (своим job'ам)
            // В T-C05 будем хранить mapping clientId -> stationNetId для таргетированной отправки.
            // Пока MVP: отправляем snapshot ТОЛЬКО по запросу клиента (SubscribeStation).
        }

        // ==========================================================
        // RPCs (Client -> Server)
        // ==========================================================

        /// <summary>Клиент подписывается на snapshot станции. Сервер шлёт текущее состояние + далее deltas через TargetRPC.</summary>
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void SubscribeStationRpc(ulong stationNetId, RpcParams rpcParams = default)
        {
            ulong clientId = rpcParams.Receive.SenderClientId;
            Debug.Log($"[CraftingServer] SubscribeStationRpc: client={clientId} station={stationNetId}");
            if (!CheckRateLimit(clientId)) { SendResultToClient(clientId, CraftingResultDto.Denied(CraftingResultCode.RateLimited, "Слишком частые операции")); return; }

            var station = CraftingWorld.GetStationRaw(stationNetId);
            if (station == null)
            {
                // Race fix: станция ещё не зарегистрирована (ScenePlacedObjectSpawner спавнит с задержкой).
                // Отправляем пустой snapshot (state=Empty) — клиент не зависнет в timeout, UI обновится.
                Debug.LogWarning($"[CraftingServer] Subscribe: station {stationNetId} not in CraftingWorld yet (race). Sending empty snapshot.");
                var emptySnap = new CraftingSnapshotDto { stationNetId = stationNetId, jobState = (byte)CraftingJobState.Empty, activeRecipeId = -1 };
                SendSnapshotToClient(clientId, emptySnap);
                return;
            }

            // Distance check
            if (!CheckDistance(clientId, station)) { SendResultToClient(clientId, CraftingResultDto.Denied(CraftingResultCode.NotFound, "Слишком далеко от станции", stationNetId)); return; }

            // Шлём snapshot
            var snap = BuildSnapshot(stationNetId);
            SendSnapshotToClient(clientId, snap);
            Debug.Log($"[CraftingServer] Subscribe: client={clientId} station={stationNetId} state={snap.jobState}");
        }

        /// <summary>Клиент отписывается от snapshot'ов (например, закрыл окно).</summary>
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void UnsubscribeStationRpc(ulong stationNetId, RpcParams rpcParams = default)
        {
            // T-C05: будем вести Dictionary<clientId, HashSet<ulong>> подписок. Пока no-op.
        }

        /// <summary>Положить ингредиент в buffer станции. Только владелец job'а (или претендент, если Empty).</summary>
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void AddIngredientRpc(ulong stationNetId, int itemId, int quantity, byte source, RpcParams rpcParams = default)
        {
            ulong clientId = rpcParams.Receive.SenderClientId;
            if (!CheckRateLimit(clientId)) { SendResultToClient(clientId, CraftingResultDto.Denied(CraftingResultCode.RateLimited, "Слишком частые операции", stationNetId)); return; }
            if (quantity <= 0 || itemId < 0) { SendResultToClient(clientId, CraftingResultDto.Denied(CraftingResultCode.InvalidArgs, "Некорректные аргументы", stationNetId)); return; }

            var job = CraftingWorld.GetJob(stationNetId);
            if (job == null) { SendResultToClient(clientId, CraftingResultDto.Denied(CraftingResultCode.NotFound, "Станция не найдена", stationNetId)); return; }
            if (job.State == CraftingJobState.InProgress) { SendResultToClient(clientId, CraftingResultDto.Denied(CraftingResultCode.AlreadyStarted, "Крафт уже запущен, нельзя добавлять", stationNetId)); return; }
            if (job.State == CraftingJobState.Completed) { SendResultToClient(clientId, CraftingResultDto.Denied(CraftingResultCode.AlreadyCompleted, "Крафт уже завершён, заберите результат", stationNetId)); return; }

            // Owner: 0 = no owner, можно стать; иначе должен совпадать
            if (job.OwnerClientId != 0 && job.OwnerClientId != clientId) { SendResultToClient(clientId, CraftingResultDto.Denied(CraftingResultCode.NotOwner, "Станция занята другим игроком", stationNetId)); return; }

            // Проверка инвентаря: возьмём из ProjectC.Items.InventoryWorld (T-C05b wire-in)
            // MVP: сервер ВЕРИТ клиенту. Антиабьюз через OnTick ServerSnapshot verify (T-C07 sub-fix).
            // T-C05: вызвать InventoryWorld.TryConsumeFromInventory(clientId, itemId, quantity, out var reason)
            // и вернуть NotEnoughResources если false.

            // Пока — записываем в buffer
            if (job.OwnerClientId == 0) job.OwnerClientId = clientId;
            job.Buffer.Add(new BufferedIngredientDto { itemId = itemId, quantity = quantity, source = source, ownerClientId = clientId });
            job.State = CraftingJobState.Buffered;

            SendResultToClient(clientId, CraftingResultDto.Ok(stationNetId));
            SendSnapshotToClient(clientId, BuildSnapshot(stationNetId));
            if (_debugMode) Debug.Log($"[CraftingServer] AddIngredient: client={clientId} station={stationNetId} itemId={itemId} qty={quantity}");
        }

        /// <summary>Запустить крафт: buffer -> committed, state -> InProgress.</summary>
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void StartCraftRpc(ulong stationNetId, int recipeId, RpcParams rpcParams = default)
        {
            ulong clientId = rpcParams.Receive.SenderClientId;
            if (!CheckRateLimit(clientId)) { SendResultToClient(clientId, CraftingResultDto.Denied(CraftingResultCode.RateLimited, "Слишком частые операции", stationNetId)); return; }

            var job = CraftingWorld.GetJob(stationNetId);
            if (job == null) { SendResultToClient(clientId, CraftingResultDto.Denied(CraftingResultCode.NotFound, "Станция не найдена", stationNetId)); return; }
            if (job.OwnerClientId != clientId) { SendResultToClient(clientId, CraftingResultDto.Denied(CraftingResultCode.NotOwner, "Только владелец может запустить крафт", stationNetId)); return; }
            if (job.State == CraftingJobState.InProgress) { SendResultToClient(clientId, CraftingResultDto.Denied(CraftingResultCode.AlreadyStarted, "Крафт уже идёт", stationNetId)); return; }

            var recipe = CraftingWorld.GetRecipe(recipeId);
            if (recipe == null) { SendResultToClient(clientId, CraftingResultDto.Denied(CraftingResultCode.NotFound, "Рецепт не найден", stationNetId)); return; }

            // TODO T-C05b: проверить что buffer удовлетворяет recipe.Ingredients
            // Пока: переносим buffer -> committed без валидации (anti-abuse в T-C07 sub-fix)

            job.Committed.Clear();
            for (int i = 0; i < job.Buffer.Count; i++) job.Committed.Add(new CommittedIngredientDto { itemId = job.Buffer[i].itemId, quantity = job.Buffer[i].quantity, ownerClientId = clientId });
            job.Buffer.Clear();
            job.RecipeId = recipeId;
            job.StartTime = NetworkManager != null ? (float)NetworkManager.ServerTime.Time : Time.realtimeSinceStartup;
            float speedMult = 1f;
            // CraftingStation (T-C04) expose SpeedMultiplier via reflection / property — пока hardcode
            job.Duration = Mathf.Max(0.5f, recipe.CraftSeconds / Mathf.Max(0.0001f, speedMult));
            job.State = CraftingJobState.InProgress;
            job.ResultItemName = recipe.DisplayName;

            SendResultToClient(clientId, CraftingResultDto.Ok(stationNetId));
            SendSnapshotToClient(clientId, BuildSnapshot(stationNetId));
            if (_debugMode) Debug.Log($"[CraftingServer] StartCraft: client={clientId} station={stationNetId} recipe={recipeId} duration={job.Duration}s");
        }

        /// <summary>Отменить крафт: возврат committed -> buffer, state -> Buffered. Только владелец.</summary>
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void CancelCraftRpc(ulong stationNetId, RpcParams rpcParams = default)
        {
            ulong clientId = rpcParams.Receive.SenderClientId;
            if (!CheckRateLimit(clientId)) return;

            var job = CraftingWorld.GetJob(stationNetId);
            if (job == null) return;
            if (job.OwnerClientId != clientId) { SendResultToClient(clientId, CraftingResultDto.Denied(CraftingResultCode.NotOwner, "Только владелец может отменить", stationNetId)); return; }
            if (job.State != CraftingJobState.InProgress) { SendResultToClient(clientId, CraftingResultDto.Denied(CraftingResultCode.NotStarted, "Нечего отменять", stationNetId)); return; }

            // Возвращаем committed в buffer (для повторного StartCraft)
            job.Buffer.Clear();
            for (int i = 0; i < job.Committed.Count; i++) job.Buffer.Add(new BufferedIngredientDto { itemId = job.Committed[i].itemId, quantity = job.Committed[i].quantity, source = (byte)CraftingSourceType.Inventory, ownerClientId = clientId });
            job.Committed.Clear();
            job.State = CraftingJobState.Buffered;
            job.RecipeId = -1;
            job.StartTime = 0f;
            job.Duration = 0f;
            job.ResultItemName = null;

            // TODO T-C05b: вернуть предметы в инвентарь клиента через InventoryWorld.TryAddToInventory

            SendResultToClient(clientId, CraftingResultDto.Ok(stationNetId));
            SendSnapshotToClient(clientId, BuildSnapshot(stationNetId));
            if (_debugMode) Debug.Log($"[CraftingServer] CancelCraft: client={clientId} station={stationNetId}");
        }

        /// <summary>Забрать готовый результат. Только владелец. State -> Empty.</summary>
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void CollectRpc(ulong stationNetId, RpcParams rpcParams = default)
        {
            ulong clientId = rpcParams.Receive.SenderClientId;
            if (!CheckRateLimit(clientId)) return;

            var job = CraftingWorld.GetJob(stationNetId);
            if (job == null) return;
            if (job.OwnerClientId != clientId) { SendResultToClient(clientId, CraftingResultDto.Denied(CraftingResultCode.NotOwner, "Только владелец может забрать", stationNetId)); return; }
            if (job.State != CraftingJobState.Completed) { SendResultToClient(clientId, CraftingResultDto.Denied(CraftingResultCode.NotStarted, "Крафт ещё не завершён", stationNetId)); return; }

            // TODO T-C05b: выдать предмет в инвентарь клиента через InventoryWorld.TryAddToInventory
            // T-C05b hook: var recipe = CraftingWorld.GetRecipe(job.RecipeId);
            //              foreach (var out in recipe.Outputs) InventoryWorld.TryAddToInventory(clientId, out.item, out.quantity)

            job.Reset();
            job.State = CraftingJobState.Empty;

            SendResultToClient(clientId, CraftingResultDto.Ok(stationNetId));
            SendSnapshotToClient(clientId, BuildSnapshot(stationNetId));
            if (_debugMode) Debug.Log($"[CraftingServer] Collect: client={clientId} station={stationNetId}");
        }

        // ==========================================================
        // Senders (Server -> Client TargetRPC)
        // ==========================================================
        private void SendResultToClient(ulong clientId, CraftingResultDto result)
        {
            var netPlayer = FindNetworkPlayer(clientId);
            if (netPlayer == null) { if (_debugMode) Debug.LogWarning($"[CraftingServer] no NetworkPlayer for client {clientId}"); return; }
            netPlayer.ReceiveCraftingResultTargetRpc(result);
        }

        private void SendSnapshotToClient(ulong clientId, CraftingSnapshotDto snap)
        {
            var netPlayer = FindNetworkPlayer(clientId);
            if (netPlayer == null) return;
            netPlayer.ReceiveCraftingSnapshotTargetRpc(snap);
        }

        private NetworkPlayer FindNetworkPlayer(ulong clientId)
        {
            if (NetworkManager == null) return null;
            if (!NetworkManager.ConnectedClients.TryGetValue(clientId, out var cc)) return null;
            return cc.PlayerObject != null ? cc.PlayerObject.GetComponent<NetworkPlayer>() : null;
        }

        // ==========================================================
        // Helpers
        // ==========================================================
        private bool CheckDistance(ulong clientId, MonoBehaviour station)
        {
            var netPlayer = FindNetworkPlayer(clientId);
            if (netPlayer == null) return false;
            // T-C04 exposes InteractRadius — use reflection-safe fallback 4f
            float radius = 4f;
            var mi = station.GetType().GetMethod("GetInteractRadius");
            if (mi != null) { try { radius = (float)mi.Invoke(station, null); } catch { /* keep default */ } }
            float dist = Vector3.Distance(netPlayer.transform.position, station.transform.position);
            return dist <= radius + 0.5f;
        }

        private CraftingSnapshotDto BuildSnapshot(ulong stationNetId)
        {
            var job = CraftingWorld.GetJob(stationNetId);
            if (job == null) return new CraftingSnapshotDto { stationNetId = stationNetId, jobState = (byte)CraftingJobState.Empty, activeRecipeId = -1 };
            return new CraftingSnapshotDto
            {
                stationNetId = stationNetId,
                jobState = (byte)job.State,
                ownerClientId = job.OwnerClientId,
                activeRecipeId = job.RecipeId,
                startTime = job.StartTime,
                duration = job.Duration,
                buffer = job.Buffer.ToArray(),
                committed = job.Committed.ToArray(),
                resultItemName = job.ResultItemName ?? "",
            };
        }

        private bool CheckRateLimit(ulong clientId)
        {
            if (_maxOpsPerMinute <= 0) return true;
            if (!_opTimestamps.TryGetValue(clientId, out var list))
            {
                list = new List<float>(8);
                _opTimestamps[clientId] = list;
            }
            float now = Time.realtimeSinceStartup;
            for (int i = list.Count - 1; i >= 0; i--) if (now - list[i] > 60f) list.RemoveAt(i);
            if (list.Count >= _maxOpsPerMinute)
            {
                if (_debugMode) Debug.LogWarning($"[CraftingServer] Rate limit hit for client {clientId} ({list.Count} ops/min)");
                return false;
            }
            list.Add(now);
            return true;
        }
    }
}