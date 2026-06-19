// =====================================================================================
// InventoryServer.cs — server-authoritative RPC hub для инвентаря (Project C: The Clouds)
// =====================================================================================
// Документация:
//   • docs/dev/INVENTORY_V2_REFACTOR.md — Phase 2 (Network hub)
//
// Назначение: NetworkBehaviour, размещается на [InventoryServer] GameObject в BootstrapScene
// (рядом с [ContractServer] и [MarketServer]). Сервер — single source of truth для инвентаря.
// Клиенты шлют RPC сюда, сервер валидирует, мутирует InventoryWorld, шлёт snapshot+result обратно.
//
// Паттерн скопирован с ContractServer (ProjectC.Trade.Network).
//   • [Rpc(SendTo.Server, RequireOwnership = true)] — все мутации
//   • [Rpc(SendTo.Owner)] через NetworkPlayer — delivery snapshot/result
//   • Rate limit per-client (защита от спама RPC)
//
// MIGRATION NOTE: этот файл ЗАМЕНЯЕТ старый Assets/_Project/Scripts/Core/NetworkInventory.cs.
// Старый остаётся жить ещё 1-2 сессии как safety net (parallel-stack pattern, см.
// unity-v2-subsystem-migration §3). Финальное удаление — в cleanup-сессии.
//
// LEGACY CALLERS (которые надо адаптировать в Phase 2):
//   • Assets/_Project/Scripts/World/Chest/NetworkChestContainer.cs:224
//     — `playerObject.GetComponent<NetworkInventory>()` → `playerObject.GetComponent<InventoryServer>()`
//   • Assets/_Project/Scripts/Core/NetworkInventory.cs:202-228
//     — статические RegisterItem/GetItemId → переехали в InventoryWorld
// =====================================================================================

using System;
using System.Collections.Generic;
using ProjectC.Items.Client;
using ProjectC.Items.Dto;
using Unity.Netcode;
using UnityEngine;

namespace ProjectC.Items.Network
{
    [DisallowMultipleComponent]
    public class InventoryServer : NetworkBehaviour
    {
        public static InventoryServer Instance { get; private set; }

        [Header("Settings")]
        [SerializeField] private int maxSlots = 32;
        [SerializeField] private int maxOpsPerMinute = 60;   // rate limit per-client

        [Header("Drop (Phase 10)")]
        [Tooltip("Prefab PickupItem для server-spawn при drop. Должен иметь NetworkObject + PickupItem компоненты. Регистрируется в NetworkManager.NetworkConfig.Prefabs.")]
        [SerializeField] private GameObject _dropPickupPrefab;

        // ============================================================
        // Server-side state
        // ============================================================

        // Per-client rate-limit timestamps (для RPC spam protection)
        private readonly Dictionary<ulong, List<float>> _opTimestamps = new Dictionary<ulong, List<float>>();

        // Локальный кэш ItemData (на клиенте нужен для UI — отображать icon, name)
        // На сервере дублирует InventoryWorld._itemDatabase.
        private readonly Dictionary<int, ItemData> _itemCache = new Dictionary<int, ItemData>();

        // ============================================================
        // Public read-only API (для клиентского UI через InventoryClientState)
        // ============================================================

        /// <summary>Получить ItemData из локального кэша (для UI: icon, name, maxStack).</summary>
        public ItemData GetCachedDefinition(int itemId)
        {
            if (_itemCache.TryGetValue(itemId, out var d)) return d;
            // Fallback: запросим у World (на случай если кэш пуст)
            return InventoryWorld.Instance?.GetItemDefinition(itemId);
        }

        // ============================================================
        // Server APIs (legacy — вызывается из NetworkChestContainer, и т.д.)
        // ============================================================

        /// <summary>
        /// Добавить предмет напрямую на сервере. Используется NetworkChestContainer после открытия сундука.
        /// </summary>
        public bool AddItem(ulong clientId, int itemId, ItemType itemType)
        {
            if (!IsServer) return false;
            if (InventoryWorld.Instance == null) return false;

            var result = InventoryWorld.Instance.AddItemDirect(clientId, itemId, itemType);
            if (result.IsSuccess)
            {
                // Отправим snapshot клиенту
                SendSnapshot(clientId, null);
            }
            return result.IsSuccess;
        }

        // ============================================================
        // CLIENT RPCs — RequestXxx
        // ============================================================

        /// <summary>Клиент хочет подобрать предмет в мире. Только Owner.
        /// T-KEY-05: instanceId для Key-предметов (0 для обычных).</summary>
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        public void RequestPickupRpc(int itemId, byte typeByte, int instanceId, Vector3 worldPos, RpcParams rpcParams = default)
        {
            ulong clientId = rpcParams.Receive.SenderClientId;
            if (!CheckRateLimit(clientId)) return;

            // Найти игрока для distance validation
            var nm = NetworkManager.Singleton;
            if (nm == null) { SendResult(clientId, FailResult(InventoryResultCode.InternalError)); return; }
            var playerObj = nm.SpawnManager.GetPlayerNetworkObject(clientId);
            if (playerObj == null) { SendResult(clientId, FailResult(InventoryResultCode.NoPermission)); return; }

            Vector3 playerPos = playerObj.transform.position;
            var result = InventoryWorld.Instance.TryPickup(clientId, itemId, (ItemType)typeByte, worldPos, playerPos);
            if (result.IsSuccess)
                {
                // T-KEY-05: если Key-предмет с instanceId — передаём владение игроку
                if (instanceId > 0 && (ItemType)typeByte == ItemType.Key)
                {
                    // T-KEY-07: обновляем instanceId в слоте инвентаря (чтобы UI знал, какой это корабль)
                    InventoryWorld.Instance.UpdateKeySlotInstanceId(clientId, instanceId);

                    var krwType = System.Type.GetType("ProjectC.Ship.Key.KeyRodInstanceWorld, Assembly-CSharp");
                    if (krwType != null)
                    {
                        var transfer = krwType.GetMethod("TransferInstance",
                            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                        if (transfer != null)
                        {
                            transfer.Invoke(null, new object[] { instanceId,
                                System.UInt64.MaxValue /* OWNER_NONE */, clientId });
                            Debug.Log($"[InventoryServer] Pickup Key: TransferInstance(id={instanceId}, NONE→{clientId})");
                        }
                    }
                }
                SendSnapshot(clientId, null);
            }
            SendResult(clientId, result);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        public void RequestDropRpc(int slotIndex, int quantity, Vector3 worldPos, Vector3 playerPos, RpcParams rpcParams = default)
        {
            ulong clientId = rpcParams.Receive.SenderClientId;
            if (!CheckRateLimit(clientId)) return;

            var result = InventoryWorld.Instance.TryDrop(clientId, slotIndex, quantity, worldPos, playerPos);
            if (result.IsSuccess)
            {
                // Spawn PickupItem в мире (server-side)
                if (_dropPickupPrefab == null)
                {
                    Debug.LogError("[InventoryServer] CRITICAL: dropPickupPrefab не задан! Drop не создаёт PickupItem в мире.");
                }
                else
                {
                    var itemData = InventoryWorld.Instance.GetItemDefinition(result.itemId);
                    if (itemData == null)
                    {
                        Debug.LogError($"[InventoryServer] Drop: itemData for id={result.itemId} not found");
                    }
                    else
                    {
                        var go = Instantiate(_dropPickupPrefab, worldPos, Quaternion.identity);
                        var pickup = go.GetComponent<ProjectC.Items.PickupItem>();
                        if (pickup != null)
                        {
                            // SetItemData (itemData + auto-register itemId на клиенте через GetOrRegisterItemId)
                            pickup.itemData = itemData;
                            pickup.itemId = InventoryWorld.Instance.GetOrRegisterItemId(itemData);
                        }
                        var netObj = go.GetComponent<Unity.Netcode.NetworkObject>();
                        if (netObj != null)
                        {
                            netObj.Spawn(destroyWithScene: true);
                            Debug.Log($"[InventoryServer] Dropped PickupItem at {worldPos}: id={result.itemId} type={itemData.itemType} netObjId={netObj.NetworkObjectId}");
                        }
                        else
                        {
                            Debug.LogError("[InventoryServer] Drop: prefab missing NetworkObject!");
                            Destroy(go);
                        }
                    }
                }
                SendSnapshot(clientId, null);
            }
            SendResult(clientId, result);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        public void RequestMoveRpc(int fromSlot, int toSlot, RpcParams rpcParams = default)
        {
            ulong clientId = rpcParams.Receive.SenderClientId;
            if (!CheckRateLimit(clientId)) return;
            var result = InventoryWorld.Instance.TryMove(clientId, fromSlot, toSlot);
            if (result.IsSuccess) SendSnapshot(clientId, null);
            SendResult(clientId, result);
        }

        // ============================================================
        // T-Q14: TryRemove (server-side helper) + RequestRemoveRpc (client-initiated)
        // ============================================================

        /// <summary>
        /// T-Q14: удалить N штук предмета напрямую на сервере. Используется для quest turn-in
        /// (QuestServer.RequestTurnInQuestRpc → QuestWorld.TryTurnIn → InventoryServer.TryRemove),
        /// dialogue TakeItem (T-Q15: DialogueAction.TakeItem → QuestServer → InventoryServer.TryRemove),
        /// и любого server-side сценария "забрать предмет".
        /// НЕ вызывается на клиенте — защита через IsServer.
        /// </summary>
        public bool TryRemove(ulong clientId, int itemId, ItemType itemType, int count)
        {
            if (!IsServer) return false;
            if (InventoryWorld.Instance == null) return false;
            var result = InventoryWorld.Instance.RemoveItems(clientId, itemId, itemType, count);
            if (result.IsSuccess)
            {
                // Отправим snapshot клиенту
                SendSnapshot(clientId, null);
            }
            return result.IsSuccess;
        }

        /// <summary>
        /// T-Q14: client-initiated removal. На будущее — для dialogue option "Сдать предмет" (T-Q15+).
        /// Сейчас основной path — server-side TryRemove.
        /// </summary>
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        public void RequestRemoveRpc(int itemId, byte typeByte, int count, RpcParams rpcParams = default)
        {
            ulong clientId = rpcParams.Receive.SenderClientId;
            if (!CheckRateLimit(clientId)) return;
            var result = InventoryWorld.Instance.RemoveItems(clientId, itemId, (ItemType)typeByte, count);
            if (result.IsSuccess) SendSnapshot(clientId, null);
            SendResult(clientId, result);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        public void RequestUseRpc(int slotIndex, RpcParams rpcParams = default)
        {
            ulong clientId = rpcParams.Receive.SenderClientId;
            if (!CheckRateLimit(clientId)) return;
            var result = InventoryWorld.Instance.TryUse(clientId, slotIndex);
            SendResult(clientId, result);
        }

        /// <summary>Клиент просит переслать полный snapshot (для refresh / re-spawn).</summary>
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        public void RequestRefreshRpc(RpcParams rpcParams = default)
        {
            ulong clientId = rpcParams.Receive.SenderClientId;
            SendSnapshot(clientId, null);
        }

        // ============================================================
        // DELIVERY — найти NetworkPlayer и отправить TargetRpc
        // ============================================================

        private void SendSnapshot(ulong clientId, string locationId)
        {
            if (!IsServer) return;
            var nm = NetworkManager.Singleton;
            if (nm == null) return;
            var playerObj = nm.SpawnManager.GetPlayerNetworkObject(clientId);
            if (playerObj == null)
            {
                Debug.LogWarning($"[InventoryServer] SendSnapshot: no NetworkObject for client {clientId}");
                return;
            }
            var networkPlayer = playerObj.GetComponent<ProjectC.Player.NetworkPlayer>();
            if (networkPlayer == null)
            {
                Debug.LogWarning($"[InventoryServer] SendSnapshot: no NetworkPlayer component on playerObject for client {clientId}");
                return;
            }
            var snap = InventoryWorld.Instance.BuildSnapshot(clientId, locationId);
            networkPlayer.ReceiveInventorySnapshotTargetRpc(snap);
        }

        /// <summary>T-Q21/M11: public helper для QuestServer (GiveCredits → push refreshed snapshot).</summary>
        public void PushSnapshot(ulong clientId)
        {
            SendSnapshot(clientId, null);
        }

        private void SendResult(ulong clientId, InventoryResultDto result)
        {
            if (!IsServer) return;
            var nm = NetworkManager.Singleton;
            if (nm == null) return;
            var playerObj = nm.SpawnManager.GetPlayerNetworkObject(clientId);
            if (playerObj == null) return;
            var networkPlayer = playerObj.GetComponent<ProjectC.Player.NetworkPlayer>();
            if (networkPlayer == null) return;
            networkPlayer.ReceiveInventoryResultTargetRpc(result);
        }

        // ============================================================
        // Rate limit
        // ============================================================

        private bool CheckRateLimit(ulong clientId)
        {
            if (maxOpsPerMinute <= 0) return true;
            float now = Time.realtimeSinceStartup;
            if (!_opTimestamps.TryGetValue(clientId, out var list))
            {
                list = new List<float>();
                _opTimestamps[clientId] = list;
            }
            // Удаляем записи старше 60 сек
            list.RemoveAll(t => (now - t) > 60f);
            if (list.Count >= maxOpsPerMinute)
            {
                SendResult(clientId, FailResult(InventoryResultCode.RateLimited));
                return false;
            }
            list.Add(now);
            return true;
        }

        private static InventoryResultDto FailResult(InventoryResultCode code)
            => new InventoryResultDto
            {
                code = (byte)code,
                message = InventoryClientState.LocalizeResultCode(code),
                itemId = -1,
                slotIndex = -1,
                newCredits = -1f,
            };

        // ============================================================
        // Lifecycle
        // ============================================================

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (IsServer && InventoryWorld.Instance == null)
            {
                // T-X0: instantiate repository. Default = JsonInventoryRepository (per-file JSON).
                // Альтернатива (test): new InMemoryInventoryRepository() — out of scope T-X0.
                var repository = new ProjectC.Core.JsonInventoryRepository();
                InventoryWorld.CreateAndInitialize(repository);

                // T-KEY-PERSIST: инициализируем KeyRodInstanceWorld с репозиторием
                if (!ProjectC.Ship.Key.KeyRodInstanceWorld.IsInitialized)
                {
                    var krRepo = new ProjectC.Ship.Key.JsonKeyRodInstanceRepository();
                    ProjectC.Ship.Key.KeyRodInstanceWorld.CreateAndInitialize(krRepo);
                    Debug.Log($"[InventoryServer] KeyRodInstanceWorld initialized with JsonKeyRodInstanceRepository");
                }

                // T-E04: применить лимит слотов из инспектора (по умолчанию 1000).
                InventoryWorld.Instance.ConfigureMaxSlots(maxSlots);

                // T-X0: hook client connect → load persisted inventory.
                if (NetworkManager != null)
                {
                    NetworkManager.OnClientConnectedCallback += HandleClientConnectedServer;
                }
            }

            // Кэш ItemData — заполняем из InventoryWorld (для клиентского UI)
            if (InventoryWorld.Instance != null)
            {
                _itemCache.Clear();
                // Доступ к _itemDatabase через рефлексию (приватный) — для MVP ОК.
                // Альтернатива: добавить public Enumerator в InventoryWorld. Phase 2+.
                // Сейчас — сериализуем через Resources:
                var allItems = Resources.LoadAll<ItemData>("Items");
                int id = 1;
                foreach (var item in allItems)
                {
                    if (item != null) _itemCache[id++] = item;
                }
            }

            if (Instance == null) Instance = this;
            Debug.Log($"[InventoryServer] OnNetworkSpawn. IsServer={IsServer}, _itemCache={_itemCache.Count}, repo={InventoryWorld.Instance?.Repository?.GetType().Name ?? "null"}");
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();
            if (NetworkManager != null)
            {
                NetworkManager.OnClientConnectedCallback -= HandleClientConnectedServer;
            }
            if (Instance == this) Instance = null;
            _opTimestamps.Clear();
        }

        /// <summary>
        /// T-X0: load persisted inventory when client connects. Safe если файл
        /// не существует (новый игрок) — LoadPlayer no-op.
        /// </summary>
        private void HandleClientConnectedServer(ulong clientId)
        {
            if (!IsServer) return;
            if (InventoryWorld.Instance == null) return;
            InventoryWorld.Instance.LoadPlayer(clientId);
            if (Debug.isDebugBuild) Debug.Log($"[InventoryServer] HandleClientConnectedServer({clientId}) — inventory loaded");
        }
    }
}
