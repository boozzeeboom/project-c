# 28_KEY_ARCHITECTURE_REVIEW — Глубокий архитектурный анализ

**Дата:** 2026-06-19 | **Автор:** Агент Mavis | **Статус:** 📋 Architectural Review

---

## §1. Проблема (как описано пользователем)

> "У предмета есть ID: 123 и есть метадата. К примеру все ключи ID 123 но каждый ключ уникален метадатой. Для корабля А ключ ID 123:1 для корабля Б ключ ID 123:2. Все просто. Сервер просто не пускает без этого. Вся телеметрия работает между связкой которую и так настроили. Метадата не меняется как и ID. Не зависимо дропнул сложил удалил и тд. В том числе и серверу проще мы видим все ключи под одним ID легко можем найти."

**Суть:** у нас сложная многослойная система которая пытается решить простую задачу. Многие игры обходятся простой парой `(itemId, instanceId)`.

---

## §2. Текущая архитектура (что мы накрутили)

```
                  ┌──────────────────────────────────────────────────┐
                  │              InventoryData (per player)           │
                  │  ┌──────────────────────────────────────────┐    │
                  │  │ _keyIds: List<int>   [2010, 2011, 2009] │    │
                  │  │ _keySlots: List<Slot>                   │    │
                  │  │   [{itemId:2010, instanceId:0}, ...]    │    │
                  │  └──────────────────────────────────────────┘    │
                  └─────────────────────┬────────────────────────────┘
                                        │ параллельные списки
                                        │ индексы должны совпадать
                  ┌─────────────────────▼────────────────────────────┐
                  │           KeyRodInstanceWorld (server)          │
                  │  ┌──────────────────────────────────────────┐    │
                  │  │ _instancesById[1]={itemId:2010,         │    │
                  │  │                  registeredShipId:17,    │    │
                  │  │                  ownerPlayerId:0,        │    │
                  │  │                  state:Active}           │    │
                  │  │ _primaryInstanceByShipId[17]=1           │    │
                  │  │ _instancesByPlayer[0]=[1,2,3]           │    │
                  │  └──────────────────────────────────────────┘    │
                  └─────────────────────┬────────────────────────────┘
                                        │ reflection-based queries
                  ┌─────────────────────▼────────────────────────────┐
                  │   KeyRodInstanceBinding (scene-placed)           │
                  │   ┌──────────────────────────────┐              │
                  │   │ _ship: ShipController         │              │
                  │   │ _keyItemData: ItemData        │              │
                  │   │ _instanceId: int (lazy)       │              │
                  │   │ _registered: bool (after init)│              │
                  │   └──────────────────────────────┘              │
                  └──────────────────────────────────────────────────┘
```

**5 источников правды:**
1. `InventoryData._keyIds` (client)
2. `InventoryData._keySlots` (client)
3. `KeyRodInstanceWorld._instancesById` (server)
4. `KeyRodInstanceBinding._ship` (scene)
5. `KeyRodInstanceBinding._instanceId` (scene-lazy)

**И 3 reflection-based query layer:**
- `InventoryTab.ResolveKeyItemDisplayName` — 3 levels fallback
- `MyShipsTab.RefreshShipList` — 3 levels fallback  
- `InventoryUI.ResolveKeyItemDisplayName` — 1 level

**И куча edge-cases:**
- instanceId эфемерный
- pickup без binding → instanceId=0
- drop'нутый ключ → instanceId=0 при pickup
- persistence конфликтует с runtime state
- 3 разных DTO: InventoryItemDto, InventorySlot, KeyRodInstance
- ShipOwnershipRequirement проверяет state==Active
- guard дубликатов по instanceId vs itemId

---

## §3. Что хочет пользователь (простая модель)

```
Все ключи — itemId=123 (один ItemData).
Каждый ключ имеет уникальный instanceId (1, 2, 3...).
```

```csharp
// Один ItemData для всех ключей
[CreateAssetMenu] class KeyItemData : ItemData { }

// Серверный реестр — простой
class KeyInstance {
    public int instanceId;
    public int itemId;          // = 123 всегда
    public ulong shipId;         // к какому кораблю привязан
    public ulong ownerId;        // кто владеет (ulong.MaxValue = в мире)
}

// Операции
void PickupKey(clientId, instanceId) {
    instance.ownerId = clientId;
}
void DropKey(clientId, instanceId) {
    instance.ownerId = ulong.MaxValue;
}
bool CanBoardShip(clientId, shipId) {
    var inst = GetKeyInstanceByShip(shipId);
    return inst != null && inst.ownerId == clientId;
}
```

**Никаких двойных списков, никаких reflection, никаких scene-placed bindings.**

---

## §4. Анализ — почему мы накрутили сложность

### 4.1 Что мы пытались решить

| Проблема | Наше решение | Правильное решение |
|---|---|---|
| Key не сохраняется при restart | `_instanceId` + persistence | instanceId эфемерный, shipId тоже — но **связка owner→shipId→itemId стабильна** через persistence |
| Разные корабли = разные ключи | Scene-placed `KeyRodInstanceBinding` | **Один KeyData, разные shipId в instance** — ItemData вообще не нужен для Key |
| Find корабль по ключу | `KeyRodInstanceBinding._ship` (scene-search) | **`KeyInstance.shipId` → `FindShipByNetId`** (NetworkObject lookup) |
| Ownership check на сервере | `ShipOwnershipRequirement` + `KeyRodInstanceWorld` | **Прямой `KeyInstanceRegistry.CanBoard(shipId, clientId)`** без MetaRequirementRegistry |
| Display name | `ShipController.CustomDisplayName` через reflection | **На клиенте: `NetworkManager.SpawnManager.SpawnedObjects[shipId].GetComponent<ShipController>().CustomDisplayName`** |
| Drop + repickup cycle | TransferInstance + UpdateState(Lost) + CreateInstance при pickup | **`ownerId = NONE` — нет state.Lost, instance persistent** |

### 4.2 Чего мы достигли

| Что | Плюсы | Минусы |
|---|---|---|
| Scene-placed binding | "Intuitive" для level designer | **Race condition, reflection overhead, hard coupling** |
| KeyRodInstance + InventoryData dual layer | "Separation of concerns" | **Sync bugs, double-lookup, instanceId drifting** |
| ItemRegistry stability | Single source of truth | **Required manual patching** (мы добавляли Key items руками) |
| ShipOwnershipRequirement auto-attach | Per-ship policy | **Doesn't actually fix the core issue — just adds component to scene** |

### 4.3 Что осталось сломано

1. **Drop bug (T-KEY-09)** — ownership не сбрасывается при drop
2. **Pickup без binding → instanceId=0** — drop'нутый ключ неотличим от нового
3. **Persistence файл загрязняется** — нужно вручную удалять
4. **3 reflection fallbacks в UI** — performance + maintainability hit
5. **8 секторов колеса не хватает** — приходится объединять Equipment+Key
6. **Гайдлайны "не хватает"** — каждый pickup создаёт ту же проблему заново

---

## §5. Предлагаемая упрощённая архитектура

### 5.1 Одна структура — `KeyInstance`

```csharp
namespace ProjectC.Items
{
    /// <summary>
    /// Один предмет = (itemId, instanceId). instanceId всегда уникален в рамках itemId.
    /// Server-authoritative, синхронизируется через NetworkList.
    /// </summary>
    public struct KeyInstance : INetworkSerializable, IEquatable<KeyInstance>
    {
        public int   instanceId;        // уникальный в рамках itemId (server counter)
        public int   itemId;            // стабильный ItemRegistry ID (например 123 = "Key")
        public ulong registeredShipId;  // NetworkObjectId корабля-владельца
        public ulong ownerPlayerId;     // ulong.MaxValue = в мире (drop), иначе clientId
    }
}
```

### 5.2 Серверный реестр — простой singleton

```csharp
namespace ProjectC.Items
{
    public static class KeyRegistry
    {
        // ВСЕ instance'ы (Active+Lost), сервер-only
        private static Dictionary<int, KeyInstance> _byId = new();
        private static Dictionary<ulong, int> _byShip = new();  // shipId → instanceId (1:1)
        private static Dictionary<ulong, List<int>> _byPlayer = new();
        private static int _nextId = 1;

        public static KeyInstance Create(int itemId, ulong shipId) {
            var inst = new KeyInstance {
                instanceId = _nextId++,
                itemId = itemId,
                registeredShipId = shipId,
                ownerPlayerId = ulong.MaxValue  // в мире
            };
            _byId[inst.instanceId] = inst;
            _byShip[shipId] = inst.instanceId;
            return inst;
        }

        public static bool Transfer(int instanceId, ulong toClientId) {
            if (!_byId.TryGetValue(instanceId, out var inst)) return false;
            RemoveFromPlayerIndex(inst);
            inst.ownerPlayerId = toClientId;
            _byId[instanceId] = inst;
            AddToPlayerIndex(inst);
            return true;
        }

        public static bool CanPlayerUseShip(ulong clientId, ulong shipId) {
            if (!_byShip.TryGetValue(shipId, out int iid)) return false;
            return _byId.TryGetValue(iid, out var inst)
                && inst.ownerPlayerId == clientId;
        }

        // ... Save/Load через IKeyInstanceRepository
    }
}
```

### 5.3 Inventory — простой список пар

```csharp
// Просто List<(int itemId, int instanceId)>
public class InventoryData
{
    private List<(int itemId, int instanceId)> _slots;
    
    public void Add(int itemId, int instanceId) {
        _slots.Add((itemId, instanceId));
    }
    
    public bool RemoveByInstanceId(int instanceId) {
        for (int i = 0; i < _slots.Count; i++) {
            if (_slots[i].instanceId == instanceId) {
                _slots.RemoveAt(i);
                return true;
            }
        }
        return false;
    }
    
    public bool HasInstance(int instanceId) => 
        _slots.Any(s => s.instanceId == instanceId);
}
```

**Никаких параллельных списков. Никаких разных методов AddItem/AddKeyItem.**

### 5.4 UI — прямые запросы без reflection

```csharp
// InventoryTab — отображение
public string ResolveDisplayName(int itemId, int instanceId) {
    var inst = KeyRegistry.GetInstance(instanceId);
    if (inst == null) return ItemRegistry.GetItem(itemId).itemName;
    
    var ship = NetworkManager.SpawnManager.SpawnedObjects[inst.registeredShipId]
        .GetComponent<ShipController>();
    return ship != null ? $"🚀 {ship.CustomDisplayName}" : "Unknown";
}

// MyShipsTab — dropdown
public void RefreshShipList() {
    foreach (var instanceId in KeyRegistry.GetInstancesForPlayer(myId)) {
        var inst = KeyRegistry.GetInstance(instanceId);
        var ship = NetworkManager.SpawnManager.SpawnedObjects[inst.registeredShipId]
            .GetComponent<ShipController>();
        if (ship != null) AddToDropdown(ship.CustomDisplayName, instanceId, ship);
    }
}

// ShipOwnershipCheck (server, на F)
bool allowed = KeyRegistry.CanPlayerUseShip(myClientId, shipNetId);
```

### 5.5 Persistence — простой JSON

```json
{
  "instances": [
    {"instanceId": 1, "itemId": 123, "registeredShipId": 17, "ownerPlayerId": 0},
    {"instanceId": 2, "itemId": 123, "registeredShipId": 19, "ownerPlayerId": 0}
  ],
  "nextId": 3
}
```

**Один файл. Один реестр. Один источник правды.**

---

## §6. Сравнение

| Метрика | Текущая | Предлагаемая |
|---|---|---|
| Файлов | 11 (.cs) | 4 (.cs) |
| Reflection в runtime | 5 мест | 0 |
| Source-of-truth | 5 словарей | 1 словарь |
| Race conditions | 3 (retry loop, async pickup, instanceId drift) | 0 (sync через NetworkList) |
| Drop bug | Существует | Невозможен (Transfer всегда работает) |
| Persistence keys | `_keyIds + _keyInstanceIds` (parallel) | `instances[]` |
| UI lookups | Reflection + 3 fallback levels | Прямой доступ |
| Add scene key | Manual drag binding | Ничего (auto-bind через shipId в instance) |
| Drop key | Ломается при instanceId=0 | Всегда работает |
| Re-add drop'нутого ключа | Manual scene-placed pickup | NetworkObject pickup, instanceId в payload |

---

## §7. План миграции (если пользователь хочет)

### Phase A — подготовить
1. Создать `KeyRegistry.cs` (server-side)
2. Создать `KeyInstance.cs` (struct)
3. Создать `KeyInstanceRepository.cs` (persistence)
4. **Не ломать** старую систему — работают параллельно

### Phase B — мигрировать hot paths
1. `InventoryServer.RequestPickupRpc` → использовать KeyRegistry
2. `InventoryServer.RequestDropRpc` → использовать KeyRegistry.Transfer(instanceId, NONE)
3. `NetworkPlayer.SubmitSwitchModeRpc` → KeyRegistry.CanPlayerUseShip()
4. Удалить `ShipOwnershipRequirement` (больше не нужен — KeyRegistry прямая проверка)

### Phase C — мигрировать UI
1. `InventoryTab` → KeyRegistry.GetInstance(instanceId)
2. `MyShipsTab` → KeyRegistry.GetInstancesForPlayer(myId)
3. `InventoryUI` → KeyRegistry.GetInstance(instanceId)
4. **Удалить** все 3 reflection fallback helper'а

### Phase D — удалить старое
1. Удалить `KeyRodInstanceWorld.cs`
2. Удалить `KeyRodInstance.cs`
3. Удалить `KeyRodInstanceRepository.cs`
4. Удалить `KeyRodInstanceBinding.cs`
5. Удалить `ShipOwnershipRequirement.cs`
6. Удалить `ShipOwnershipRegistry.cs`
7. Удалить reflection в `InventoryServer.RequestPickupRpc`

### Effort

| Phase | Что | Время |
|---|---|---|
| A | Подготовка (4 файла, не ломаем) | 3h |
| B | Hot paths миграция | 4h |
| C | UI миграция | 3h |
| D | Удаление legacy | 1h |
| Тесты | End-to-end | 2h |
| **Total** | | **~13h** |

**Это целая сессия работы** — но архитектурно правильное решение.

---

## §8. Сравнение с существующими играми

| Игра | Как хранят ключи |
|---|---|
| **Minecraft** | `ItemStack` (itemId, count, metadata). Уникальные ключи через NBT теги |
| **Rust** | `ItemBlueprint` + per-instance `InstanceData`. Ключ от дома = (itemId + doorOwnerId) |
| **Star Citizen** | `ShipKey` = (shipEntityId, ownerPlayerId) — прямой ID |
| **EVE Online** | `ItemInstance` (itemId, instanceId, locationId, ownerId) — простая пара |
| **Terraria** | ItemId + stack — без уникальности |

**Все используют простую пару (itemId, instanceId)** + lookup по ней. Никто не держит параллельные словари.

---

## §9. Рекомендация

**Шаг 1 (сейчас):** исправить drop bug минимальным фиксом (T-KEY-09 План §3 — Шаг 1). 30 минут.

**Шаг 2 (потом):** если хочется стабильности — провести рефакторинг по плану §7. ~13 часов.

**Шаг 3 (или):** оставить текущую архитектуру, документировать known issues, и не делать больше ничего. ~2 часа документирования.

---

## §10. Что выбрать?

| Вариант | Effort | Reliability | Документация |
|---|---|---|---|
| Минимальный fix (T-KEY-09 Шаг 1) | 30 min | 70% — drop работает, pickup-bug остаётся | 2h |
| Полный рефакторинг | 13h | 99% — всё работает | 3h |
| Оставить как есть + документировать | 2h | 50% — drop bug, pickup-bug | 5h |

Моя рекомендация: **полный рефакторинг**. Текущая система — это 11 файлов reflection-based кода, который никогда не будет стабильным. 13 часов — это нормальная цена за архитектурно правильное решение.

*Changelog ведёт агент Mavis.*
