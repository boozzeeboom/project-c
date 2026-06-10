// CraftingServer.cs (T-C03) - server-side RPC hub. Pattern: GatheringServer T-G03 + MarketServer.
// Owns: CraftingWorld init/shutdown, CraftingTimeService subscription, recipe registry, rate limit.
// Scene-placed in BootstrapScene (T-C07). Singleton, server-only.
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using ProjectC.Player;
using ProjectC.Items;
using ProjectC.Items.Dto;

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

        // client subscriptions: clientId -> set of stationNetIds (множественные подписки — разные станции)
        private readonly Dictionary<ulong, System.Collections.Generic.HashSet<ulong>> _subscribers = new Dictionary<ulong, System.Collections.Generic.HashSet<ulong>>();

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
                _subscribers.Clear();
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

            // Push прогресс всем подписчикам (каждый 1Гц tick — как GatheringServer.TickActiveGathers)
            if (_subscribers.Count > 0)
            {
                var clientIds = new List<ulong>(_subscribers.Keys);
                for (int ci = 0; ci < clientIds.Count; ci++)
                {
                    ulong clientId = clientIds[ci];
                    if (!_subscribers.TryGetValue(clientId, out var stations)) continue;
                    // Push snapshot для КАЖДОЙ подписанной станции этого клиента
                    foreach (var sn in stations)
                    {
                        var snap = BuildSnapshot(sn);
                        SendSnapshotToClient(clientId, snap);
                    }
                }
            }
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

            // Record subscription (add to set — несколько станций на одного клиента)
            if (!_subscribers.TryGetValue(clientId, out var subSet))
            {
                subSet = new System.Collections.Generic.HashSet<ulong>();
                _subscribers[clientId] = subSet;
            }
            subSet.Add(stationNetId);

            // Шлём snapshot
            var snap = BuildSnapshot(stationNetId);
            SendSnapshotToClient(clientId, snap);
            Debug.Log($"[CraftingServer] Subscribe: client={clientId} station={stationNetId} state={snap.jobState}");
        }

        /// <summary>Клиент отписывается от snapshot'ов одной станции. Не удаляет клиента полностью — другие станции остаются.</summary>
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void UnsubscribeStationRpc(ulong stationNetId, RpcParams rpcParams = default)
        {
            ulong clientId = rpcParams.Receive.SenderClientId;
            if (_subscribers.TryGetValue(clientId, out var subSet))
            {
                subSet.Remove(stationNetId);
                if (subSet.Count == 0) _subscribers.Remove(clientId);
            }
            if (_debugMode) Debug.Log($"[CraftingServer] Unsubscribe: client={clientId} station={stationNetId}");
        }

        // Also remove subscriber when station despawns
        public void RemoveSubscriberForStation(ulong stationNetId)
        {
            // Clean up all subscribers for this station (iterates over all client sets)
            var clientsToRemove = new List<ulong>();
            foreach (var kv in _subscribers)
            {
                kv.Value.Remove(stationNetId);
                if (kv.Value.Count == 0) clientsToRemove.Add(kv.Key);
            }
            foreach (var clientId in clientsToRemove) _subscribers.Remove(clientId);
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

            // T-C07b: списать предметы из инвентаря
            var itemData = CraftingWorld.GetItem(itemId);
            if (itemData == null) { SendResultToClient(clientId, CraftingResultDto.Denied(CraftingResultCode.NotFound, "Предмет не найден", stationNetId)); return; }

            var invWorld = InventoryWorld.Instance;
            if (invWorld == null)
            {
                Debug.LogWarning("[CraftingServer] InventoryWorld.Instance==null — пропускаем проверку инвентаря");
            }
            else
            {
                int invItemId = invWorld.GetOrRegisterItemId(itemData);
                if (invItemId < 0) { SendResultToClient(clientId, CraftingResultDto.Denied(CraftingResultCode.InternalError, "Ошибка ID предмета", stationNetId)); return; }

                var removeResult = invWorld.RemoveItems(clientId, invItemId, itemData.itemType, quantity);
                if (!removeResult.IsSuccess)
                {
                    SendResultToClient(clientId, CraftingResultDto.Denied(CraftingResultCode.NotEnoughResources,
                        "Недостаточно предметов: " + (removeResult.message ?? ""), stationNetId));
                    return;
                }
            }

            if (job.OwnerClientId == 0) job.OwnerClientId = clientId;
            job.Buffer.Add(new BufferedIngredientDto { itemId = itemId, quantity = quantity, source = source, ownerClientId = clientId });
            job.State = CraftingJobState.Buffered;

            SendResultToClient(clientId, CraftingResultDto.Ok(stationNetId));
            SendSnapshotToClient(clientId, BuildSnapshot(stationNetId));
            if (_debugMode) Debug.Log($"[CraftingServer] AddIngredient: client={clientId} station={stationNetId} itemId={itemId} qty={quantity} item={itemData.itemName}");
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

            // FIX T-C07: вызываем станцию, а не мутируем job напрямую.
            // station.ServerStartCraft синхронизирует и _replicatedState (NetworkVariable) и job в CraftingWorld.
            var station = CraftingWorld.GetStationRaw(stationNetId);
            if (station == null) { SendResultToClient(clientId, CraftingResultDto.Denied(CraftingResultCode.NotFound, "Станция не найдена", stationNetId)); return; }

            // Подготовить committed из buffer (передаётся по значению через список)
            var committedItems = new System.Collections.Generic.List<ProjectC.Crafting.CommittedIngredientDto>();
            for (int i = 0; i < job.Buffer.Count; i++)
            {
                committedItems.Add(new ProjectC.Crafting.CommittedIngredientDto {
                    itemId = job.Buffer[i].itemId,
                    quantity = job.Buffer[i].quantity,
                    ownerClientId = clientId
                });
            }

            float startTime = NetworkManager != null ? (float)NetworkManager.ServerTime.Time : Time.realtimeSinceStartup;
            float duration = Mathf.Max(0.5f, recipe.CraftSeconds / 1f); // speedMult=1f hardcode пока

            // Find CraftingStation component (station is MonoBehaviour)
            var cs = station.GetComponent<ProjectC.Crafting.CraftingStation>();
            if (cs != null)
            {
                cs.ServerStartCraft(clientId, recipeId, startTime, duration, committedItems, recipe.DisplayName);
                job.Buffer.Clear(); // committed already set inside ServerStartCraft
            }
            else
            {
                // Fallback: manual mutation (shouldn't happen)
                job.Committed.Clear();
                for (int i = 0; i < job.Buffer.Count; i++)
                    job.Committed.Add(new CommittedIngredientDto { itemId = job.Buffer[i].itemId, quantity = job.Buffer[i].quantity, ownerClientId = clientId });
                job.Buffer.Clear();
                job.RecipeId = recipeId;
                job.StartTime = startTime;
                job.Duration = duration;
                job.State = CraftingJobState.InProgress;
                job.ResultItemName = recipe.DisplayName;
            }

            SendResultToClient(clientId, CraftingResultDto.Ok(stationNetId));
            SendSnapshotToClient(clientId, BuildSnapshot(stationNetId));
            Debug.Log($"[CraftingServer] StartCraft: client={clientId} station={stationNetId} recipe={recipeId} duration={duration}s");
        }

        /// <summary>Отменить крафт: возврат committed -> buffer, state -> Buffered. Только владелец.</summary>
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void CancelCraftRpc(ulong stationNetId, RpcParams rpcParams = default)
        {
            ulong clientId = rpcParams.Receive.SenderClientId;
            if (!CheckRateLimit(clientId)) return;

            var station = CraftingWorld.GetStationRaw(stationNetId);
            if (station == null) return;

            // T-C07b: вернуть предметы в инвентарь ДО ServerCancelCraft
            var job = CraftingWorld.GetJob(stationNetId);
            if (job != null && job.State == CraftingJobState.InProgress)
            {
                var invWorld = InventoryWorld.Instance;
                if (invWorld != null)
                {
                    foreach (var c in job.Committed)
                    {
                        var itemData = CraftingWorld.GetItem(c.itemId);
                        if (itemData == null) continue;
                        int invItemId = invWorld.GetOrRegisterItemId(itemData);
                        if (invItemId < 0) continue;
                        for (int n = 0; n < c.quantity; n++)
                            invWorld.AddItemDirect(clientId, invItemId, itemData.itemType);
                    }
                    Debug.Log($"[CraftingServer] CancelCraft return: client={clientId} station={stationNetId} items={job.Committed.Count}");
                }
            }

            var cs = station.GetComponent<ProjectC.Crafting.CraftingStation>();
            if (cs != null)
            {
                cs.ServerCancelCraft();
            }

            SendResultToClient(clientId, CraftingResultDto.Ok(stationNetId));
            SendSnapshotToClient(clientId, BuildSnapshot(stationNetId));
            Debug.Log($"[CraftingServer] CancelCraft: client={clientId} station={stationNetId}");
        }

        /// <summary>Забрать готовый результат. Только владелец. State -> Empty.</summary>
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void CollectRpc(ulong stationNetId, RpcParams rpcParams = default)
        {
            ulong clientId = rpcParams.Receive.SenderClientId;
            if (!CheckRateLimit(clientId)) return;

            var station = CraftingWorld.GetStationRaw(stationNetId);
            if (station == null) return;

            // T-C07b: выдать результат в инвентарь ДО ServerCollect (ServerCollect очищает job.State)
            var job = CraftingWorld.GetJob(stationNetId);
            if (job != null && job.State == CraftingJobState.Completed)
            {
                var recipe = CraftingWorld.GetRecipe(job.RecipeId);
                if (recipe != null)
                {
                    var invWorld = InventoryWorld.Instance;
                    if (invWorld != null)
                    {
                        foreach (var output in recipe.Outputs)
                        {
                            if (output.item == null) continue;
                            int invItemId = invWorld.GetOrRegisterItemId(output.item);
                            if (invItemId < 0) continue;
                            for (int n = 0; n < output.quantity; n++)
                            {
                                invWorld.AddItemDirect(clientId, invItemId, output.item.itemType);
                            }
                            Debug.Log($"[CraftingServer] Collect grant: client={clientId} item={output.item.itemName} qty={output.quantity}");
                        }
                    }
                    else
                    {
                        Debug.LogWarning("[CraftingServer] InventoryWorld.Instance==null — не выдаём предметы");
                    }
                }
            }

            var cs = station.GetComponent<ProjectC.Crafting.CraftingStation>();
            if (cs != null)
            {
                cs.ServerCollect();
            }

            SendResultToClient(clientId, CraftingResultDto.Ok(stationNetId));
            SendSnapshotToClient(clientId, BuildSnapshot(stationNetId));
            Debug.Log($"[CraftingServer] Collect: client={clientId} station={stationNetId}");
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
            if (netPlayer == null)
            {
                // NetworkPlayer ещё не спавнен для clientId (race condition при свежем подключении).
                // Повторим через 1 кадр — к тому моменту NetworkPlayerSpawner уже заспавнит player object.
                Debug.LogWarning($"[CraftingServer] SendSnapshotToClient: NetworkPlayer for client {clientId} not spawned yet — scheduling retry in 1 frame");
                StartCoroutine(RetrySendSnapshotNextFrame(clientId, snap));
                return;
            }
            Debug.Log($"[CraftingServer] SendSnapshotToClient: station={snap.stationNetId} state={snap.jobState} → client={clientId}");
            netPlayer.ReceiveCraftingSnapshotTargetRpc(snap);
        }

        private System.Collections.IEnumerator RetrySendSnapshotNextFrame(ulong clientId, CraftingSnapshotDto snap)
        {
            yield return null; // 1 кадр
            for (int i = 0; i < 10; i++) // до 10 попыток (примерно 0.16 сек на 60fps)
            {
                var netPlayer = FindNetworkPlayer(clientId);
                if (netPlayer != null)
                {
                    Debug.Log($"[CraftingServer] RetrySendSnapshot: SUCCESS after {i+1} frames → client={clientId}");
                    netPlayer.ReceiveCraftingSnapshotTargetRpc(snap);
                    yield break;
                }
                yield return null;
            }
            Debug.LogError($"[CraftingServer] RetrySendSnapshot: GIVE UP after 10 frames — no NetworkPlayer for client {clientId}");
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

            // Server-computed progress (fixes clock drift between ServerTime.Time and Time.realtimeSinceStartup)
            float progress = 0f;
            if (job.State == CraftingJobState.InProgress && job.Duration > 0f)
            {
                float serverTime = NetworkManager != null ? (float)NetworkManager.ServerTime.Time : Time.realtimeSinceStartup;
                progress = Mathf.Clamp01((serverTime - job.StartTime) / job.Duration);
            }
            else if (job.State == CraftingJobState.Completed)
            {
                progress = 1f;
            }

            return new CraftingSnapshotDto
            {
                stationNetId = stationNetId,
                jobState = (byte)job.State,
                ownerClientId = job.OwnerClientId,
                activeRecipeId = job.RecipeId,
                startTime = job.StartTime,
                duration = job.Duration,
                progress = progress,
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