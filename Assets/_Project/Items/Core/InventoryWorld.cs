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
using ProjectC.Core;
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

        /// <summary>
        /// Repository для persistence (T-X0). Может быть null (legacy / no-save mode) —
        /// в этом случае Save/Load no-op.
        /// </summary>
        private IInventoryRepository _repository;

        public static InventoryWorld CreateAndInitialize()
        {
            if (Instance != null) return Instance;
            Instance = new InventoryWorld();
            Instance.RegisterAllItems();
            Debug.Log($"[InventoryWorld] Created (no repository). Items registered: {Instance._itemDatabase.Count}");
            return Instance;
        }

        /// <summary>
        /// T-X0: Create with persistence repository. Repository.Save/Load вызываются
        /// на каждом Add/Remove (fire-and-forget, no debounce — per §H).
        /// </summary>
        public static InventoryWorld CreateAndInitialize(IInventoryRepository repository)
        {
            if (Instance != null)
            {
                // Replace singleton if exists (testing convenience).
                Instance._repository = repository;
                return Instance;
            }
            Instance = new InventoryWorld();
            Instance._repository = repository;
            Instance.RegisterAllItems();
            Debug.Log($"[InventoryWorld] Created with {repository?.GetType().Name ?? "null"}. Items registered: {Instance._itemDatabase.Count}");
            return Instance;
        }

        public static void Shutdown()
        {
            if (Instance == null) return;
            // T-X0: save all dirty players before shutdown.
            if (Instance._repository != null)
            {
                foreach (var kvp in Instance._playerInventories)
                {
                    Instance._repository.Save(kvp.Key, kvp.Value);
                }
            }
            Instance._playerInventories.Clear();
            Instance._itemDatabase.Clear();
            Instance._repository = null;
            Instance = null;
        }

        /// <summary>Public accessor for tests / debug.</summary>
        public IInventoryRepository Repository => _repository;

        /// <summary>
        /// T-X0: Load persisted inventory for player (on connect). Replaces in-memory
        /// state if file exists. Безопасно вызывать несколько раз — идемпотентно.
        /// </summary>
        public void LoadPlayer(ulong clientId)
        {
            if (_repository == null) return;
            var loaded = _repository.Load(clientId);
            if (loaded.TotalCount > 0)
            {
                _playerInventories[clientId] = loaded;
                if (Debug.isDebugBuild) Debug.Log($"[InventoryWorld] Loaded inventory for client {clientId}: {loaded.TotalCount} items");
            }
        }

        /// <summary>Save single player. Public для QuestServer.TurnInQuest flow (T-Q16) и shutdown.</summary>
        public void SavePlayer(ulong clientId)
        {
            if (_repository == null) return;
            if (!_playerInventories.TryGetValue(clientId, out var inv)) return;
            _repository.Save(clientId, inv);
        }

        // ===========================================================
        // State
        // ===========================================================

        // T-E04: default 1000 чтобы обменник (Unpack 100шт за раз) работал.
        // T-E04: сделано конфигурируемым — InventoryServer.maxSlots передаётся
        // через ConfigureMaxSlots() в OnNetworkSpawn (см. InventoryServer.cs).
        private const int DEFAULT_MAX_SLOTS = 1000;
        private int _maxSlots = DEFAULT_MAX_SLOTS;

        /// <summary>Текущий лимит слотов (конфигурируется через InventoryServer.maxSlots в инспекторе).</summary>
        public int MaxSlots => _maxSlots;

        /// <summary>Установить лимит слотов. Вызывается из InventoryServer после CreateAndInitialize().</summary>
        public void ConfigureMaxSlots(int maxSlots)
        {
            _maxSlots = maxSlots > 0 ? maxSlots : DEFAULT_MAX_SLOTS;
            Debug.Log($"[InventoryWorld] MaxSlots = {_maxSlots}");
        }
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

        /// <summary>
        /// Ship Key Subsystem: проверить, есть ли у игрока предмет с указанным itemId
        /// в любом из ItemType-слотов. Серверный single source of truth — клиент НЕ
        /// может подделать наличие ключа, потому что инвентарь хранится на сервере.
        /// Используется из ShipKeyServer для авторизации board'а.
        /// </summary>
        /// <param name="clientId">id игрока</param>
        /// <param name="itemId">id предмета (из InventoryWorld._itemDatabase)</param>
        /// <returns>true если itemId найден хотя бы в одном слоте</returns>
        public bool HasItem(ulong clientId, int itemId)
        {
            if (itemId <= 0) return false;
            if (!_playerInventories.TryGetValue(clientId, out var data)) return false;
            foreach (ItemType type in System.Enum.GetValues(typeof(ItemType)))
            {
                var ids = data.GetIdsForType(type);
                if (ids == null) continue;
                for (int i = 0; i < ids.Count; i++)
                {
                    if (ids[i] == itemId) return true;
                }
            }
            return false;
        }

        // === MetaRequirement extensions (2026-06-06, R2-META-REQ-001) ===
        // Обобщение для системы MetaRequirement: AND/OR/N-из-M логика.
        // Все методы — server-side, используются MetaRequirementRegistry при авторизации.
        // Backward compatible с HasItem (старый код ShipKeyServer продолжает работать).

        /// <summary>True если у игрока ЕСТЬ ВСЕ itemId из списка. Дубликаты игнорируются (HashSet).</summary>
        public bool HasAllItems(ulong clientId, int[] itemIds)
        {
            if (itemIds == null || itemIds.Length == 0) return true; // пустой = trivially satisfied
            if (!_playerInventories.TryGetValue(clientId, out var data)) return false;
            // Соберём уникальные requested id'ы
            var requested = new HashSet<int>();
            for (int i = 0; i < itemIds.Length; i++) requested.Add(itemIds[i]);
            // Пройдём по всем слотам игрока
            foreach (ItemType type in System.Enum.GetValues(typeof(ItemType)))
            {
                var ids = data.GetIdsForType(type);
                if (ids == null) continue;
                for (int i = 0; i < ids.Count; i++)
                {
                    requested.Remove(ids[i]);
                    if (requested.Count == 0) return true;
                }
            }
            return requested.Count == 0;
        }

        /// <summary>True если у игрока ЕСТЬ ХОТЯ БЫ ОДИН itemId из списка. Пустой список → false.</summary>
        public bool HasAnyItem(ulong clientId, int[] itemIds)
        {
            if (itemIds == null || itemIds.Length == 0) return false;
            if (!_playerInventories.TryGetValue(clientId, out var data)) return false;
            foreach (ItemType type in System.Enum.GetValues(typeof(ItemType)))
            {
                var ids = data.GetIdsForType(type);
                if (ids == null) continue;
                for (int i = 0; i < ids.Count; i++)
                {
                    for (int j = 0; j < itemIds.Length; j++)
                    {
                        if (ids[i] == itemIds[j]) return true;
                    }
                }
            }
            return false;
        }

        /// <summary>Сколько штук указанного itemId есть у игрока (по List&lt;int&gt;.Count, MVP без stackable).</summary>
        public int CountOf(ulong clientId, int itemId)
        {
            if (itemId <= 0) return 0;
            if (!_playerInventories.TryGetValue(clientId, out var data)) return 0;
            int n = 0;
            foreach (ItemType type in System.Enum.GetValues(typeof(ItemType)))
            {
                var ids = data.GetIdsForType(type);
                if (ids == null) continue;
                for (int i = 0; i < ids.Count; i++)
                {
                    if (ids[i] == itemId) n++;
                }
            }
            return n;
        }

        /// <summary>Массив itemId, которых НЕТ у игрока. Используется для генерации reason в toast'е.
        /// Дубликаты входного списка → выходной список может быть короче (только уникальные missing).</summary>
        public int[] GetMissingItems(ulong clientId, int[] itemIds)
        {
            if (itemIds == null || itemIds.Length == 0) return System.Array.Empty<int>();
            var missing = new System.Collections.Generic.List<int>();
            var seen = new HashSet<int>();
            for (int i = 0; i < itemIds.Length; i++)
            {
                if (itemIds[i] <= 0) continue;
                if (!seen.Add(itemIds[i])) continue; // уже проверяли
                if (!HasItem(clientId, itemIds[i])) missing.Add(itemIds[i]);
            }
            return missing.ToArray();
        }

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
            if (data.TotalCount >= _maxSlots)
                return Fail(InventoryResultCode.InventoryFull,
                    $"Инвентарь полон ({data.TotalCount}/{_maxSlots})", itemId, -1);

            // OK: добавляем
            data.AddItem(itemType, itemId);
            Debug.Log($"[InventoryWorld] Player {clientId} picked up ID={itemId} ({itemType}). Total: {data.TotalCount}");

            // T-X0: persist + publish event
            SavePlayer(clientId);
            PublishItemAdded(clientId, itemId, itemType, count: 1);

            return Ok($"Подобран предмет", itemId, -1);
        }

        // ============================================================
        // Operations — TryDrop (Phase 10)
        // ============================================================
        // Drop: убрать предмет из инвентаря. PickupItem в мире спавнит InventoryServer
        // (не InventoryWorld — последний не имеет NetworkObject спавна).

        private const float DROP_RANGE_M = 3f;

        public InventoryResultDto TryDrop(ulong clientId, int slotIndex, int quantity, Vector3 worldPos, Vector3 playerPos)
        {
            if (slotIndex < 0 || slotIndex >= _maxSlots)
                return Fail(InventoryResultCode.InvalidSlot, $"Слот {slotIndex} вне диапазона", -1, slotIndex);
            if (quantity <= 0)
                return Fail(InventoryResultCode.NotEnoughQuantity, "Quantity должен быть > 0", -1, slotIndex);

            var data = GetOrCreate(clientId);

            // Convert slotIndex → (itemId, itemType) using snapshot order
            // (тот же порядок что в BuildSnapshot — иначе UI/server разойдутся)
            int currentSlot = 0;
            int foundItemId = -1;
            ItemType foundType = ItemType.Resources;
            List<int> foundList = null;
            int indexInList = -1;
            foreach (ItemType type in System.Enum.GetValues(typeof(ItemType)))
            {
                var ids = data.GetIdsForType(type);
                if (ids == null) continue;
                if (currentSlot + ids.Count <= slotIndex)
                {
                    currentSlot += ids.Count;
                    continue;
                }
                // slotIndex внутри этого списка
                indexInList = slotIndex - currentSlot;
                if (indexInList < 0 || indexInList >= ids.Count) continue;  // safety
                foundItemId = ids[indexInList];
                foundType = type;
                foundList = ids;
                break;
            }
            if (foundItemId < 0 || foundList == null)
                return Fail(InventoryResultCode.InvalidSlot, $"Слот {slotIndex} пуст", -1, slotIndex);

            // Anti-cheat: distance check (3м от player)
            float dist = Vector3.Distance(worldPos, playerPos);
            if (dist > DROP_RANGE_M)
                return Fail(InventoryResultCode.NotInZone,
                    $"Drop: distance {dist:F1}м > {DROP_RANGE_M}м (анти-чит)", foundItemId, slotIndex);

            // Убрать itemId из списка. MVP: всегда 1 quantity.
            // Убираем конкретное вхождение по indexInList (тот, что соответствует slotIndex).
            foundList.RemoveAt(indexInList);

            // Log для verify
            if (foundList.Count == 0)
                Debug.Log($"[InventoryWorld] Player {clientId} dropped last {foundType} ID={foundItemId} at {worldPos}");
            else
                Debug.Log($"[InventoryWorld] Player {clientId} dropped {foundType} ID={foundItemId} at {worldPos} (still has {foundList.Count} of this type)");

            // T-X0: persist + publish event
            SavePlayer(clientId);
            PublishItemRemoved(clientId, foundItemId, foundType, count: 1);

            return Ok($"Dropped {foundType} ID={foundItemId}", foundItemId, slotIndex);
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
            if (data.TotalCount >= _maxSlots)
                return Fail(InventoryResultCode.InventoryFull,
                    $"Инвентарь полон ({data.TotalCount}/{_maxSlots})", itemId, -1);

            data.AddItem(itemType, itemId);

            // T-X0: persist + publish event
            SavePlayer(clientId);
            PublishItemAdded(clientId, itemId, itemType, count: 1);

            return Ok($"+{_itemDatabase[itemId].itemName}", itemId, -1);
        }

        // ============================================================
        // T-Q14: RemoveItems — удалить N штук предмета (для quest turn-in, dialogue TakeItem, etc.)
        // ============================================================

        /// <summary>
        /// T-Q14: удалить N штук предмета itemId (типа itemType) из инвентаря игрока.
        /// Используется для quest turn-in (QuestServer), dialogue TakeItem (T-Q15), и любого
        /// server-side сценария "забрать предмет". НЕ вызывается на клиенте — защита через InventoryServer.IsServer.
        /// </summary>
        /// <returns>Ok если удалено, Fail если itemId не найден или недостаточно count.</returns>
        public InventoryResultDto RemoveItems(ulong clientId, int itemId, ItemType itemType, int count)
        {
            if (count <= 0)
                return Fail(InventoryResultCode.NotEnoughQuantity, $"count={count} должен быть >0", itemId, -1);
            if (!_itemDatabase.ContainsKey(itemId))
                return Fail(InventoryResultCode.ItemNotFound, $"ID={itemId}", itemId, -1);

            var data = GetOrCreate(clientId);
            var ids = data.GetIdsForType(itemType);
            if (ids == null)
                return Fail(InventoryResultCode.ItemNotOwned, $"Нет предметов типа {itemType}", itemId, -1);

            // Считаем сколько раз itemId встречается в списке
            int available = 0;
            for (int i = 0; i < ids.Count; i++)
            {
                if (ids[i] == itemId) available++;
            }
            if (available < count)
                return Fail(InventoryResultCode.NotEnoughQuantity,
                    $"Недостаточно: have={available} need={count}", itemId, -1);

            // Удаляем первые `count` вхождений (для MVP — каждый id = 1 quantity).
            int removed = 0;
            for (int i = ids.Count - 1; i >= 0 && removed < count; i--)
            {
                if (ids[i] == itemId) { ids.RemoveAt(i); removed++; }
            }

            // T-X0: persist + publish event (один event на count, не на каждый item).
            SavePlayer(clientId);
            PublishItemRemoved(clientId, itemId, itemType, count);

            Debug.Log($"[InventoryWorld] Player {clientId} removed {count}x ID={itemId} ({itemType}). Remaining: {ids.Count}");
            return Ok($"-{count}x {_itemDatabase[itemId].itemName}", itemId, -1);
        }

        // ============================================================
        // T-X0: Event publishing helpers
        // ============================================================

        private void PublishItemAdded(ulong clientId, int itemId, ItemType itemType, int count)
        {
            if (_itemDatabase.TryGetValue(itemId, out var def) && def != null)
            {
                // Def has `itemName` but no Trade itemId string. TradeItemId остаётся пустым — T-Q06 cross-link опционален.
                WorldEventBus.Publish(new ItemAddedEvent
                {
                    PlayerId = clientId,
                    TimestampUnix = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    ItemId = itemId,
                    Count = count,
                    TradeItemId = ""  // T-Q06: lookup via ItemData → TradeItemDefinition mapping
                });
            }
        }

        private void PublishItemRemoved(ulong clientId, int itemId, ItemType itemType, int count)
        {
            WorldEventBus.Publish(new ItemRemovedEvent
            {
                PlayerId = clientId,
                TimestampUnix = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                ItemId = itemId,
                Count = count,
                TradeItemId = ""
            });
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
                maxSlots   = _maxSlots,
                credits    = ProjectC.Trade.Core.TradeWorld.Instance?.Repository?.GetCredits(clientId) ?? 0f,
            };
        }

        // ===========================================================
        // Initialization — register all items from Resources
        // ===========================================================

        private void RegisterAllItems()
        {
            // T-Q26: use ItemRegistry as single source of truth (replaces dual registration).
            // Fallback: Resources/Items/ scan if ItemRegistry unavailable (e.g. tests).
            ProjectC.Items.ItemRegistry registry = null;
            if (ProjectC.Items.ItemRegistry.Instance != null)
            {
                registry = ProjectC.Items.ItemRegistry.Instance;
            }
            else
            {
                // Try to load from Resources/ItemRegistry.asset (test convenience).
                registry = Resources.Load<ProjectC.Items.ItemRegistry>("ItemRegistry");
                if (registry != null) ProjectC.Items.ItemRegistry.SetInstance(registry);
            }

            int registered = 0;
            if (registry != null && registry.Count > 0)
            {
                // T-Q26: use explicit ids from registry.
                foreach (var entry in registry.GetEntries())
                {
                    if (entry.item == null) continue;
                    RegisterItem(entry.id, entry.item);
                    registered++;
                }
            }
            else
            {
                // Fallback: Resources/Items/ scan (original behavior).
                var allResources = Resources.LoadAll<ItemData>("Items");
                int id = 1;
                foreach (var item in allResources)
                {
                    if (item == null) continue;
                    RegisterItem(id++, item);
                    registered++;
                }
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
                if (!already) RegisterItem(_itemDatabase.Count + 1, pickup.itemData);
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
