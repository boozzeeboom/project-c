// =====================================================================================
// InventoryWorld.cs — серверный POCO singleton инвентаря (Project C: The Clouds)
// =====================================================================================
// Документация:
//   • docs/dev/INVENTORY_V2_REFACTOR.md — Phase 1 (Core POCO)
//
// Назначение: бизнес-логика инвентаря. НЕ MonoBehaviour, НЕ NetworkBehaviour.
// Создаётся через InventoryWorld.CreateAndInitialize() в InventoryServer.OnNetworkSpawn.
// Содержит:
//   • ItemDatabase  (Dictionary<int, ItemData>) — id → definition, заполняется из Resources/Items/
//   • Per-player state (Dictionary<ulong, InventoryData>) — id игрока → его инвентарь
//   • Операции TryPickup / TryDrop / TryMove / TryUse (возвращают InventoryResultDto)
//   • BuildSnapshot(clientId, locationId) — проекция в DTO для клиента
//
// Анти-чит:
//   • TryPickup проверяет Vector3.Distance(worldPos, playerPos) <= PICKUP_RANGE_M (5м).
//   • TryDrop проверяет, что предмет действительно в инвентаре клиента (ownership).
//   • rate limit — на сервере (см. InventoryServer.CheckRateLimit).
//
// Persistence:
//   • В текущей версии НЕ персистится между сессиями (TODO: PlayerPrefs/Repository).
//   • Когда будет — добавить IPlayerDataRepository (как Trade).
// =====================================================================================

using System.Collections.Generic;
using ProjectC.Items.Dto;
using UnityEngine;

namespace ProjectC.Items
{
    /// <summary>
    /// Серверный singleton бизнес-логики инвентаря.
    /// Аналог ContractWorld / TradeWorld в v2-архитектуре подсистемы Trade.
    /// </summary>
    public class InventoryWorld
    {
        // ===========================================================
        // Singleton
        // ===========================================================

        public static InventoryWorld Instance { get; private set; }

        public static InventoryWorld CreateAndInitialize()
        {
            if (Instance != null) return Instance;
            Instance = new InventoryWorld();
            Instance.RegisterAllItems();
            Debug.Log($"[InventoryWorld] Created. Items registered: {Instance._itemDatabase.Count}");
            return Instance;
        }

        public static void Shutdown()
        {
            if (Instance == null) return;
            Instance._playerInventories.Clear();
            Instance._itemDatabase.Clear();
            Instance = null;
        }

        // ===========================================================
        // State
        // ===========================================================

        private const int MAX_SLOTS = 32;
        private const int MAX_STACK_DEFAULT = 1;
        private const float PICKUP_RANGE_M = 5f;

        // itemId → definition (ItemData SO из Resources/Items/)
        private readonly Dictionary<int, ItemData> _itemDatabase = new Dictionary<int, ItemData>();

        // clientId → инвентарь (ИСПОЛЬЗУЕМ существующий InventoryData — не дублируем)
        // Pitfall: InventoryData хранит List<int> ids. Для нашего v2 DTO с quantity
        // расширим в Phase 2: добавим List<int> quantities параллельно.
        // Сейчас — каждый id = 1 юнит (как в существующем NetworkInventory).
        private readonly Dictionary<ulong, InventoryData> _playerInventories = new Dictionary<ulong, InventoryData>();

        // ===========================================================
        // Public API — item database
        // ===========================================================

        public void RegisterItem(int id, ItemData def)
        {
            if (def == null) return;
            _itemDatabase[id] = def;
        }

        public ItemData GetItemDefinition(int id)
        {
            return _itemDatabase.TryGetValue(id, out var d) ? d : null;
        }

        public int GetItemCount() => _itemDatabase.Count;

        /// <summary>
        /// Получить itemId для ItemData (если нет в базе — регистрирует автоматически).
        /// Используется в PickupItem.Start() и NetworkChestContainer.RequestOpenChestServerRpc.
        /// </summary>
        public int GetOrRegisterItemId(ItemData item)
        {
            if (item == null) return -1;
            foreach (var kvp in _itemDatabase)
            {
                if (kvp.Value == item) return kvp.Key;
            }
            int newId = _itemDatabase.Count + 1;
            RegisterItem(newId, item);
            Debug.Log($"[InventoryWorld] Авто-регистрация: ID {newId} - {item.itemName}");
            return newId;
        }

        // ===========================================================
        // Public API — per-player inventory
        // ===========================================================

        public InventoryData GetOrCreate(ulong clientId)
        {
            if (!_playerInventories.TryGetValue(clientId, out var data))
            {
                data = new InventoryData(true);
                _playerInventories[clientId] = data;
            }
            return data;
        }

        public bool Has(ulong clientId) => _playerInventories.ContainsKey(clientId);

        // ===========================================================
        // Operations — TryPickup
        // ===========================================================

        public InventoryResultDto TryPickup(
            ulong clientId,
            int itemId,
            ItemType itemType,
            Vector3 worldPos,
            Vector3 playerPos)
        {
            // Валидация: предмет существует
            if (!_itemDatabase.ContainsKey(itemId))
                return Fail(InventoryResultCode.ItemNotFound, $"Предмет ID={itemId} не найден", itemId, -1);

            // Валидация: дистанция (анти-чит)
            float dist = Vector3.Distance(worldPos, playerPos);
            if (dist > PICKUP_RANGE_M)
                return Fail(InventoryResultCode.NotInZone,
                    $"Слишком далеко ({dist:F1}м, порог {PICKUP_RANGE_M:F1}м)", itemId, -1);

            // Валидация: место в инвентаре
            var data = GetOrCreate(clientId);
            if (data.TotalCount >= MAX_SLOTS)
                return Fail(InventoryResultCode.InventoryFull,
                    $"Инвентарь полон ({data.TotalCount}/{MAX_SLOTS})", itemId, -1);

            // OK: добавляем
            data.AddItem(itemType, itemId);
            Debug.Log($"[InventoryWorld] Player {clientId} picked up ID={itemId} ({itemType}). Total: {data.TotalCount}");
            return Ok($"Подобран предмет", itemId, -1);
        }

        // ===========================================================
        // Operations — TryDrop (TODO: world-spawn PickupItem после drop)
        // ===========================================================

        public InventoryResultDto TryDrop(ulong clientId, int slotIndex, int quantity, Vector3 worldPos)
        {
            if (slotIndex < 0 || slotIndex >= MAX_SLOTS)
                return Fail(InventoryResultCode.InvalidSlot, $"Слот {slotIndex} вне диапазона", -1, slotIndex);

            var data = GetOrCreate(clientId);
            // TODO (Phase 2): inventory-by-slot, не flat list. Сейчас — no-op.
            // For MVP: возвращаем NotImplemented.
            return Fail(InventoryResultCode.InternalError,
                "Drop пока не реализован (TODO Phase 2)", -1, slotIndex);
        }

        // ===========================================================
        // Operations — TryMove (TODO)
        // ===========================================================

        public InventoryResultDto TryMove(ulong clientId, int fromSlot, int toSlot)
        {
            return Fail(InventoryResultCode.InternalError, "Move пока не реализован (TODO Phase 2)", -1, -1);
        }

        // ===========================================================
        // Operations — TryUse (TODO — когда появятся use-эффекты)
        // ===========================================================

        public InventoryResultDto TryUse(ulong clientId, int slotIndex)
        {
            return Fail(InventoryResultCode.InternalError, "Use пока не реализован (TODO)", -1, slotIndex);
        }

        // ===========================================================
        // Server-side helpers (для NetworkChestContainer и т.п.)
        // ===========================================================

        /// <summary>
        /// Добавить предмет напрямую на сервере (для сундуков, квестов, и т.д.).
        /// НЕ вызывается на клиенте — защита через InventoryServer.IsServer.
        /// </summary>
        public InventoryResultDto AddItemDirect(ulong clientId, int itemId, ItemType itemType)
        {
            if (!_itemDatabase.ContainsKey(itemId))
                return Fail(InventoryResultCode.ItemNotFound, $"ID={itemId}", itemId, -1);

            var data = GetOrCreate(clientId);
            if (data.TotalCount >= MAX_SLOTS)
                return Fail(InventoryResultCode.InventoryFull,
                    $"Инвентарь полон ({data.TotalCount}/{MAX_SLOTS})", itemId, -1);

            data.AddItem(itemType, itemId);
            return Ok($"+{_itemDatabase[itemId].itemName}", itemId, -1);
        }

        // ===========================================================
        // Snapshot — проекция для клиента
        // ===========================================================

        public InventorySnapshotDto BuildSnapshot(ulong clientId, string locationId)
        {
            var data = GetOrCreate(clientId);
            var items = new List<InventoryItemDto>();
            int slotIndex = 0;

            foreach (ItemType type in System.Enum.GetValues(typeof(ItemType)))
            {
                var ids = data.GetIdsForType(type);
                if (ids == null) continue;
                foreach (int id in ids)
                {
                    items.Add(new InventoryItemDto
                    {
                        itemId    = id,
                        type      = (byte)type,
                        quantity  = 1,                  // MVP: каждый id = 1 unit
                        slotIndex = slotIndex++,
                    });
                }
            }

            return new InventorySnapshotDto
            {
                locationId = locationId,
                items      = items.ToArray(),
                maxSlots   = MAX_SLOTS,
                credits    = 0f,                       // Phase 2: подтянуть из PlayerDataRepository
            };
        }

        // ===========================================================
        // Initialization — register all items from Resources
        // ===========================================================

        private void RegisterAllItems()
        {
            // 1. Resources/Items/ — все ItemData
            var allResources = Resources.LoadAll<ItemData>("Items");
            int id = 1;
            foreach (var item in allResources)
            {
                if (item == null) continue;
                RegisterItem(id++, item);
            }

            // 2. (legacy compat) ItemData из PickupItem на сцене — в случае если
            //    не все SO лежат в Resources/Items/. Подбираем без дубликатов.
            var pickups = Object.FindObjectsByType<PickupItem>(FindObjectsInactive.Include);
            foreach (var pickup in pickups)
            {
                if (pickup == null || pickup.itemData == null) continue;
                // Не дублируем: если уже зарегистрирован (по ссылке) — skip
                bool already = false;
                foreach (var kvp in _itemDatabase)
                {
                    if (kvp.Value == pickup.itemData) { already = true; break; }
                }
                if (!already) RegisterItem(id++, pickup.itemData);
            }
        }

        // ===========================================================
        // Result helpers
        // ===========================================================

        private static InventoryResultDto Ok(string message, int itemId, int slotIndex)
            => new InventoryResultDto { code = (byte)InventoryResultCode.Ok, message = message, itemId = itemId, slotIndex = slotIndex, newCredits = -1f };

        private static InventoryResultDto Fail(InventoryResultCode code, string message, int itemId, int slotIndex)
            => new InventoryResultDto { code = (byte)code, message = message, itemId = itemId, slotIndex = slotIndex, newCredits = -1f };
    }
}
