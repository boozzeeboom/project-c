// =====================================================================================
// InventoryClientState.cs — клиентская проекция инвентаря (Project C: The Clouds)
// =====================================================================================
// Документация:
//   • docs/dev/INVENTORY_V2_REFACTOR.md — Phase 1 (Client projection)
//
// Назначение: singleton-проекция server-state инвентаря на клиентский процесс.
// Один инстанс на клиента (НЕ NetworkBehaviour). Получает snapshot'ы и результаты
// от InventoryServer через NetworkPlayer.ReceiveInventorySnapshotTargetRpc.
//
// UI читает ИСКЛЮЧИТЕЛЬНО из этого класса. Сервер — single source of truth, этот
// класс — projection layer.
//
// Создание: auto-spawn в NetworkManagerController.Awake (FIX C2-паттерн).
//
// Паттерн скопирован с ContractClientState (ProjectC.Trade.Client).
// =====================================================================================

using System;
using System.Collections.Generic;
using ProjectC.Items.Dto;
using UnityEngine;

namespace ProjectC.Items.Client
{
    public class InventoryClientState : MonoBehaviour
    {
        public static InventoryClientState Instance { get; private set; }

        [Header("Lifecycle")]
        [Tooltip("Не уничтожать при загрузке сцены (клиент переживает стриминг)")]
        [SerializeField] private bool dontDestroyOnLoad = true;

        // ============================================================
        // State
        // ============================================================

        public InventorySnapshotDto? CurrentSnapshot { get; private set; }
        public InventoryResultDto? LastResult { get; private set; }

        public string CurrentLocationId =>
            CurrentSnapshot.HasValue ? CurrentSnapshot.Value.locationId : null;

        // ============================================================
        // Events (UI подписывается на эти)
        // ============================================================

        /// <summary>Дёргается при получении нового snapshot с сервера. UI обновляется.</summary>
        public event Action<InventorySnapshotDto> OnSnapshotUpdated;

        /// <summary>Дёргается при получении результата операции (Pickup/Drop/Move/Use).</summary>
        public event Action<InventoryResultDto> OnInventoryResult;

        // ============================================================
        // Lifecycle
        // ============================================================

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                if (dontDestroyOnLoad) DontDestroyOnLoad(gameObject);
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

        // ============================================================
        // Server → Client delivery (вызывается из NetworkPlayer.ReceiveInventory*TargetRpc)
        // ============================================================

        public void OnSnapshotReceived(InventorySnapshotDto snapshot)
        {
            CurrentSnapshot = snapshot;
            try
            {
                int handlerCount = OnSnapshotUpdated?.GetInvocationList().Length ?? 0;
                Debug.Log($"[InventoryClientState] OnSnapshotReceived: items={(snapshot.items!=null?snapshot.items.Length:0)}, handlers={handlerCount}");
                OnSnapshotUpdated?.Invoke(snapshot);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[InventoryClientState] OnSnapshotUpdated handler threw: {ex}");
            }
        }

        public void OnResultReceived(InventoryResultDto result)
        {
            LastResult = result;
            try
            {
                // T-Gxx: per-operation callback (PickupItem) BEFORE global event
                // чтобы избежать cross-talk на OnInventoryResult.
                var cb = _pendingPickupCallback;
                _pendingPickupCallback = null;
                cb?.Invoke(result);

                OnInventoryResult?.Invoke(result);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[InventoryClientState] OnInventoryResult handler threw: {ex}");
            }
        }

        // ============================================================
        // CONVENIENCE API — UI и NetworkPlayer вызывают ЭТИ методы
        // ============================================================

        /// <summary>Попросить сервер выполнить pickup предмета (вызывается из PickupItem.Collect()).
        /// T-KEY-05: instanceId=0 для обычных предметов.</summary>
        public void RequestPickup(int itemId, ItemType itemType, Vector3 worldPos)
        {
            if (ProjectC.Items.Network.InventoryServer.Instance == null)
            {
                Debug.LogWarning("[InventoryClientState] RequestPickup: InventoryServer.Instance is NULL (network not started?)");
                return;
            }
            ProjectC.Items.Network.InventoryServer.Instance.RequestPickupRpc(itemId, (byte)itemType, 0, worldPos);
        }

        /// <summary>T-KEY-05: overload с instanceId (для Key-предметов).</summary>
        public void RequestPickup(int itemId, ItemType itemType, int instanceId, Vector3 worldPos)
        {
            if (ProjectC.Items.Network.InventoryServer.Instance == null) return;
            ProjectC.Items.Network.InventoryServer.Instance.RequestPickupRpc(itemId, (byte)itemType, instanceId, worldPos);
        }

        /// <summary>
        /// T-Gxx: per-operation callback для PickupItem. Предотвращает cross-talk
        /// когда несколько PickupItem подписаны на глобальный OnInventoryResult.
        /// </summary>
        private Action<InventoryResultDto> _pendingPickupCallback;

        /// <summary>
        /// T-Gxx/T-KEY-05: per-operation callback для PickupItem с поддержкой instanceId.
        /// Вызывается из PickupItem.Collect() вместо подписки на глобальное событие OnInventoryResult.
        /// </summary>
        public void RequestPickup(int itemId, ItemType itemType, int instanceId, Vector3 worldPos,
            Action<InventoryResultDto> onResult)
        {
            if (_pendingPickupCallback != null)
            {
                Debug.LogWarning("[InventoryClientState] RequestPickup: предыдущий pickup ещё ожидает ответа. Перезаписываем.");
            }
            _pendingPickupCallback = onResult;
            RequestPickup(itemId, itemType, instanceId, worldPos);
        }

        /// <summary>
        /// T-Gxx: legacy overload без instanceId (для обратной совместимости).
        /// </summary>
        public void RequestPickup(int itemId, ItemType itemType, Vector3 worldPos,
            Action<InventoryResultDto> onResult)
        {
            if (_pendingPickupCallback != null)
            {
                Debug.LogWarning("[InventoryClientState] RequestPickup: предыдущий pickup ещё ожидает ответа. Перезаписываем.");
            }
            _pendingPickupCallback = onResult;
            RequestPickup(itemId, itemType, 0, worldPos);
        }

        public void RequestDrop(int slotIndex, int quantity, Vector3 worldPos, Vector3 playerPos)
        {
            if (ProjectC.Items.Network.InventoryServer.Instance == null) return;
            ProjectC.Items.Network.InventoryServer.Instance.RequestDropRpc(slotIndex, quantity, worldPos, playerPos);
        }

        public void RequestMove(int fromSlot, int toSlot)
        {
            if (ProjectC.Items.Network.InventoryServer.Instance == null) return;
            ProjectC.Items.Network.InventoryServer.Instance.RequestMoveRpc(fromSlot, toSlot);
        }

        public void RequestUse(int slotIndex)
        {
            if (ProjectC.Items.Network.InventoryServer.Instance == null) return;
            ProjectC.Items.Network.InventoryServer.Instance.RequestUseRpc(slotIndex);
        }

        /// <summary>Запросить полный snapshot (например, после респауна / переподключения).</summary>
        public void RequestRefresh()
        {
            if (ProjectC.Items.Network.InventoryServer.Instance == null) return;
            ProjectC.Items.Network.InventoryServer.Instance.RequestRefreshRpc();
        }

        // ============================================================
        // HELPERS — для UI (TAB-колесо, P-таб CharacterWindow)
        // ============================================================

        /// <summary>Получить все предметы (с quantity > 0) из текущего snapshot'а.</summary>
        public List<InventoryItemDto> GetItems()
        {
            var result = new List<InventoryItemDto>();
            if (!CurrentSnapshot.HasValue || CurrentSnapshot.Value.items == null) return result;
            foreach (var item in CurrentSnapshot.Value.items)
            {
                if (item.quantity > 0) result.Add(item);
            }
            return result;
        }

        /// <summary>Предметы по типу (для TAB-колеса 8 секторов).</summary>
        public List<InventoryItemDto> GetItemsByType(ItemType type)
        {
            var result = new List<InventoryItemDto>();
            if (!CurrentSnapshot.HasValue || CurrentSnapshot.Value.items == null) return result;
            byte typeByte = (byte)type;
            foreach (var item in CurrentSnapshot.Value.items)
            {
                if (item.type == typeByte) result.Add(item);
            }
            return result;
        }

        /// <summary>Количество предметов определённого типа (для TAB-сектор "X3").</summary>
        public int GetCountByType(ItemType type)
        {
            if (!CurrentSnapshot.HasValue || CurrentSnapshot.Value.items == null) return 0;
            byte typeByte = (byte)type;
            int count = 0;
            foreach (var item in CurrentSnapshot.Value.items)
            {
                if (item.type == typeByte) count += item.quantity;
            }
            return count;
        }

        /// <summary>Есть ли хотя бы один предмет этого типа (для подсветки сектора).</summary>
        public bool HasItemsInType(ItemType type) => GetCountByType(type) > 0;

        /// <summary>Общее количество всех предметов.</summary>
        public int GetTotalItemCount()
        {
            if (!CurrentSnapshot.HasValue || CurrentSnapshot.Value.items == null) return 0;
            int count = 0;
            foreach (var item in CurrentSnapshot.Value.items) count += item.quantity;
            return count;
        }

        /// <summary>Получить ItemData (definition) по itemId из локального кэша.
        /// Возвращает null если предмет не зарегистрирован в ItemDatabase.</summary>
        public ItemData GetItemDefinition(int itemId)
        {
            if (ProjectC.Items.Network.InventoryServer.Instance != null)
                return ProjectC.Items.Network.InventoryServer.Instance.GetCachedDefinition(itemId);
            return null;
        }

        // ============================================================
        // LOCALIZATION — InventoryResultCode → строка
        // ============================================================

        public static string LocalizeResultCode(InventoryResultCode code)
        {
            switch (code)
            {
                case InventoryResultCode.Ok:                return "OK";
                case InventoryResultCode.NotInZone:         return "Слишком далеко от предмета";
                case InventoryResultCode.InventoryFull:     return "Инвентарь полон";
                case InventoryResultCode.ItemNotFound:      return "Предмет не найден";
                case InventoryResultCode.NotEnoughQuantity: return "Недостаточно предметов";
                case InventoryResultCode.InvalidSlot:       return "Неверный слот";
                case InventoryResultCode.RateLimited:       return "Слишком много запросов";
                case InventoryResultCode.InternalError:     return "Внутренняя ошибка";
                case InventoryResultCode.NoPermission:      return "Нет прав на операцию";
                case InventoryResultCode.ItemNotOwned:      return "Этого предмета нет в инвентаре";
                case InventoryResultCode.StackOverflow:     return "Стек переполнен";
                default: return code.ToString();
            }
        }
    }
}
