# T-KEY-08: MyShipsTab UI — план реализации

> Интеграция UI вкладки "ship" в CharacterWindow.  
> Дата: 2026-06-19 | Версия: v12 | Тикет: T-KEY-08 | Статус: ✅ MVP-1..4 завершены

---

## §1. Цель

Игрок открывает **P** (CharacterWindow) → вкладка **"КОРАБЛЬ"** → видит выпадающий список кораблей из ключей в инвентаре → выбирает корабль → видит его актуальное состояние (топливо, груз, модули, position, etc).

---

## §2. Архитектура

### 2.1 Данные — что уже доступно

Из T-KEY-07 у нас есть:

| Источник | Что даёт | Где живёт |
|---|---|---|
| `InventoryData.GetIdsForType(Key)` | Список itemId всех Key-предметов в инвентаре | Клиент |
| `KeyRodInstanceWorld.GetInstancesForPlayer(clientId)` | Список `instanceId` ключей, которыми владеет игрок | Server-only |
| `KeyRodInstanceWorld.GetInstance(instanceId)` | `KeyRodInstance` (itemId, registeredShipId, ownerPlayerId, state) | Server-only |
| `ShipTelemetryClientState.MyShips` | Список `ShipTelemetryState` для кораблей владельца | Клиент |
| `ItemRegistry` | Стабильный itemId ↔ ItemData маппинг | Static asset |

### 2.2 Проблема: instanceId эфемерный, KeyRodInstanceWorld — server-only

После рестарта persistence `instanceId` переназначается. На клиенте `KeyRodInstanceWorld` НЕ инициализирован. Чтобы получить список "моих кораблей" на клиенте, нужен путь через scene-placed `KeyRodInstanceBinding` (стабильный между сессиями).

### 2.3 Решение — 3 уровня fallback

```csharp
// MyShipsTab.RefreshShipList:
HashSet<int> ownedKeyItemIds = new();

// Priority 1: серверные данные напрямую (Host)
var data = invWorld.GetOrCreate(myId).GetIdsForType(Key);

// Priority 2: KeyRodInstanceWorld.GetInstancesForPlayer
foreach (var iid in KeyRodInstanceWorld.GetInstancesForPlayer(myId))
    ownedKeyItemIds.Add(KeyRodInstanceWorld.GetInstance(iid).itemId);

// Priority 3: snapshot клиента (чистый client)
foreach (var it in invState.CurrentSnapshot.Value.items)
    if (it.type == Key) ownedKeyItemIds.Add(it.itemId);

// Iterate scene-placed KeyRodInstanceBinding, проверяя ownedKeyItemIds.Contains(targetId)
```

### 2.4 UI структура (UXML)

```xml
<section name="ship-section" class="list-section">
 <Label text="Мои корабли" class="section-title" />
 <DropdownField name="ship-selector" label="Корабль" class="ship-selector" />
 <Label name="ship-empty-label" text="Нет доступных кораблей. Найдите ключ в мире." class="ship-empty" />
 <VisualElement name="ship-info" class="ship-info">
  <Label name="ship-info-name" text="—" class="ship-info-name" />
  <Label name="ship-info-class" text="—" class="ship-info-class" />
  <Label name="ship-info-key-id" text="—" class="ship-info-key-id" />
  <Label text="Топливо" class="ship-info-header" />
  <ProgressBar name="ship-fuel-bar" value="0" low-value="0" high-value="100" class="ship-fuel-bar" />
  <Label name="ship-fuel-text" text="—" class="ship-info-row" />
  <Label text="Груз" class="ship-info-header" />
  <ProgressBar name="ship-cargo-bar" value="0" low-value="0" high-value="100" class="ship-cargo-bar" />
  <Label name="ship-cargo-text" text="—" class="ship-info-row" />
  <Label text="Установленные модули" class="ship-info-header" />
  <ScrollView name="ship-modules-scroll" class="ship-modules-scroll">
   <VisualElement name="ship-modules-container" class="ship-modules-container" />
  </ScrollView>
  <Label text="Позиция" class="ship-info-header" />
  <Label name="ship-info-position" text="—" class="ship-info-row" />
  <Label text="Состояние" class="ship-info-header" />
  <Label name="ship-info-state" text="—" class="ship-info-row" />
 </VisualElement>
</section>
```

### 2.5 CSS classes

```css
.ship-selector { margin-bottom: 8px; flex-shrink: 0; }
.ship-empty { font-size: 11px; color: rgb(150,150,150); font-style: italic; }
.ship-info { padding: 8px; background: rgba(30,40,60,0.25); border-radius: 3px; flex-grow: 1; }
.ship-info-name { font-size: 16px; font-style: bold; color: rgb(220,230,240); }
.ship-info-class { font-size: 12px; color: rgb(180,200,220); }
.ship-info-header { font-size: 11px; font-style: bold; color: rgb(180,200,220); }
.ship-fuel-bar, .ship-cargo-bar { height: 14px; margin-bottom: 4px; }
.ship-modules-scroll { max-height: 33%; }
.ship-module-row { flex-direction: row; padding: 2px 4px; }
```

---

## §3. Реализация — файл за файлом

### Шаг 1: NEW — `Assets/_Project/Scripts/UI/Client/CharacterWindow/MyShipsTab.cs`

Класс `MyShipsTab` в namespace `ProjectC.UI.Client`. Методы:
- `BuildUI(CharacterWindow owner, VisualElement root)` — привязка UI элементов
- `OnTabShown()` — подписка telemetry + inventory, RefreshShipList
- `OnTabHidden()` — no-op (не отписываемся, чтобы dropdown обновлялся без переоткрытия)
- `Unsubscribe()` — снимает обе подписки (вызывается из OnDisable)
- `TrySubscribeTelemetry()` — lazy subscribe на `ShipTelemetryClientState.OnShipStateChanged`
- `TrySubscribeInventory()` — lazy subscribe на `InventoryClientState.OnSnapshotUpdated`
- `HandleShipStateChanged(ulong shipNetId)` — обновляет info panel если выбран этот корабль
- `HandleInventorySnapshotUpdated(InventorySnapshotDto snap)` — RefreshShipList
- `RefreshShipList()` — 3-level fallback для ownedKeyItemIds + iterate scene-placed bindings
- `RenderSelectedShip()` — рендер name/class/fuel/cargo/modules/position/state из telemetry
- `RenderModules(ShipController sc)` — reflection-based names
- `HasKeyItemInSnapshot(snap, itemData)` — НЕ используется (legacy после рефакторинга)

### Шаг 2: PATCH — `CharacterWindow.cs`

- + `MyShipsTab _myShipsTab` поле (вместе с `_inventoryTab` после `private InventoryTab _inventoryTab;`)
- В `EnsureBuilt()` (после `_inventoryTab.BuildUI(...)`):
  ```csharp
  _myShipsTab = new MyShipsTab();
  _myShipsTab.BuildUI(this, _root);
  ```
- В `SwitchTab(tab)`:
  ```csharp
  if (isShip) { if (_myShipsTab != null) _myShipsTab.OnTabShown(); }
  ```
- Удалены мусорные `_shipName/_shipState/_shipSpeed/_shipFuel/_shipCargo` поля
- `RefreshShipStats()` — no-op (legacy stub)

### Шаг 3: PATCH — `CharacterWindow.uxml`

Заменён placeholder (`<ListView name="ship-name"/>`) на полноценную структуру (см. §2.4).

### Шаг 4: PATCH — `CharacterWindow.uss`

+ 11 стилей (см. §2.5).

### Шаг 5: AUTO — `ShipController.cs`

```csharp
// T-KEY-08: авто-добавление ShipOwnershipRequirement
if (GetComponent<ProjectC.Ship.Key.ShipOwnershipRequirement>() == null)
    gameObject.AddComponent<ProjectC.Ship.Key.ShipOwnershipRequirement>();
```

### Шаг 6: PATCH — `InventoryServer.cs`

Guard дубликата по `instanceId` (не по `itemId`):
```csharp
if (instanceId > 0 && type == Key)
{
    if (InventoryWorld.Instance.HasKeyInstance(clientId, instanceId))
    {
        SendResult(clientId, FailResult(InventoryResultCode.ItemNotFound));
        return;
    }
}
```

---

## §4. Подводные камни

### 4.1 Стабильный itemId для каждого предмета

**Критично**: все Key ItemData **должны быть** в `ItemRegistry.asset` с явными ID. Auto-ID через `GetOrRegisterItemId` — fallback для тестов, не production.

Пример: `Key_light_ship=2010`, `Key_medium_ship=2011`, `Key_heavy_ship=2009`.

### 4.2 KeyRodInstanceBinding инициализируется после InventoryServer

Если игрок открывает вкладку до того как сервер создал `KeyRodInstance` (OnNetworkSpawn race) — `_instanceId == 0`.

Решение: `RefreshShipList()` **не использует** `_instanceId` для matching. Использует `itemId` через `ItemRegistry`.

### 4.3 ServerCargoPenalty и ShipModuleManager

Поля `cargoUsed/cargoMax/moduleCount` уже в `ShipTelemetryState`. Они обновляются в `ShipController.UpdateTelemetryState()` (T-KEY-07) при `IsServer`. На клиенте они доступны через `ShipController.TelemetryState`.

### 4.4 Когда P открыт, а корабль далеко

`ShipTelemetryClientState` агрегирует ВСЕ корабли клиента, независимо от дистанции. NetworkVariable синхронизируется NGO глобально.

### 4.5 Что показывать если у игрока нет ключей

`ship-selector` скрыт, `_emptyLabel` показывает "Нет доступных кораблей. Найдите ключ в мире."

### 4.6 RefreshShipList без переоткрытия вкладки

Подписка на `InventoryClientState.OnSnapshotUpdated` → handler вызывает `RefreshShipList`. Работает в реальном времени.

---

## §5. Тест-план

| Шаг | Ожидание |
|---|---|
| 1. Play Host (без ключей) | Console: `[ItemRegistry] Loaded 2011 items`. `[ShipController] Auto-added ShipOwnershipRequirement на Ship_Medium_root` |
| 2. **E** на [KeyRod_ShipLight] → P → КОРАБЛЬ | Dropdown: `🚀 Pushka` (1 item) |
| 3. **E** на [KeyRod_ShipMedium] → P → КОРАБЛЬ (НЕ закрывая!) | Dropdown автоматически обновляется: `🚀 Pushka`, `🚀 Русски_тест` |
| 4. Выбрать Medium | Info: name, class, keyId, fuel bar, cargo bar, modules list, position, state |
| 5. **E** на [KeyRod_ShipHeavy] → P → КОРАБЛЬ | Dropdown: 3 корабля |
| 6. Exit → Play снова → P | Все 3 корабля в списке |
| 7. **F** у Medium без medium ключа (дропнуть) | ❌ Доступ запрещён |

---

## §6. Effort итераций

| Итерация | Что | Файлы | Effort |
|---|---|---|---|
| **MVP-1** | Dropdown + placeholder + UXML + USS | UXML + USS + MyShipsTab.cs (stub) | 0.5h |
| **MVP-2** | Resolution через scene-placed bindings | MyShipsTab.cs | 1h |
| **MVP-3** | Подписка на ShipTelemetryClientState + рендер info | MyShipsTab.cs | 1.5h |
| **MVP-4** | Refresh при AddItem/RemoveItem (OnSnapshotUpdated) | MyShipsTab.cs | 0.5h |
| **Arch-refactor** | ItemRegistry + 3-level fallback + auto-attach + guard | Asset + 3 .cs файла | 1.5h |

**Total**: ~5h

---

## §7. Что отложено в Phase 2

| Фича | Почему | Effort |
|---|---|---|
| Детальные модули (иконки, описание, эффекты) | Требует новый UI | 2h |
| Кнопка "Лететь к кораблю" (waypoint) | Требует waypoint/companion system | 3h |
| Cargo UI во вкладке (открытие грузового отсека) | Cargo UI — отдельный тикет | 4h |
| Trade через вкладку | Это уже в Markets UI | 4h |
| Изменение имени корабля через UI | Не было в MVP | 1h |
| HUD telemetry widget для активного корабля | Зависит от UI-проекта | 3h |
| Multi-pilot display | Убран из MVP (Q8) | 4h |

---

## §8. Зависимости

| Зависимость | Статус |
|---|---|
| `ShipTelemetryClientState` populated | ✅ T-KEY-07 |
| `InventoryData.GetIdsForType(Key)` | ✅ T-KEY-02 |
| `KeyRodInstanceBinding._ship/_keyItemData` scene-placed | ✅ T-KEY-04 + scene setup |
| ItemRegistry содержит Key-предметы с стабильными ID | ✅ Ручная правка asset |
| Пустая вкладка `tab-content-ship` в UXML | ✅ есть |

---

## §9. Статус реализации

**MVP-1, MVP-2, MVP-3, MVP-4 завершены 2026-06-19.**

**Файлы**:
- ✅ NEW: `Assets/_Project/Scripts/UI/Client/CharacterWindow/MyShipsTab.cs` (~530 строк)
- ✅ PATCH: `Assets/_Project/UI/Resources/UI/CharacterWindow.uxml` — заменён placeholder на полноценную структуру (dropdown + info panel + modules scroll)
- ✅ PATCH: `Assets/_Project/UI/Resources/UI/CharacterWindow.uss` — добавлены 11 стилей
- ✅ PATCH: `Assets/_Project/Scripts/UI/Client/CharacterWindow.cs` — добавлено поле `_myShipsTab`, удалены мусорные поля, перенаправлен `RefreshShipStats`
- ✅ PATCH: `Assets/_Project/Scripts/Player/ShipController.cs` — auto-attach `ShipOwnershipRequirement` в `Awake()`
- ✅ PATCH: `Assets/_Project/Items/Network/InventoryServer.cs` — guard дубликата по `instanceId`
- ✅ PATCH: `Assets/_Project/Items/Data/ItemRegistry.asset` — добавлены 3 Key entries (id=2009/2010/2011)

**Compile**: 0 errors.

**Что реализовано**:
- Dropdown со списком кораблей из Key-слотов инвентаря
- Info panel с 7 секциями: name, class, keyId, fuel, cargo, modules, position, state
- Subscribe на `ShipTelemetryClientState.OnShipStateChanged` (с throttle eps=0.01)
- Subscribe на `InventoryClientState.OnSnapshotUpdated` — **real-time refresh** dropdown при pickup/drop
- Empty state: "Нет доступных кораблей"
- Modules list: reflection-based имена модулей
- 3-level fallback для ownedKeyItemIds (серверные данные / KeyRodInstanceWorld / snapshot)
- Auto-attach ShipOwnershipRequirement на каждый ShipController
- Guard дубликата по instanceId (не по itemId)

---

## §10. Архитектурный рефакторинг (2026-06-19)

После первых Play Mode тестов выявлено 3 бага:

1. **itemId ключей неопределённый** — ItemRegistry не содержал Key-предметы, `GetOrRegisterItemId` назначал auto-ID 1010 всем
2. **MyShipsTab показывал wrong ship** — `FindKeyRodBindingByItemId` возвращал первый matching, неправильный
3. **Persistence загрязнён** — `KeyRodInstances.json` имел кривые instance'ы

### Фиксы

| Фикс | Файл | Что изменилось |
|---|---|---|
| **Key items в ItemRegistry** | `Assets/_Project/Items/Data/ItemRegistry.asset` | +3 entries: `Key_heavy_ship=2009`, `Key_light_ship=2010`, `Key_medium_ship=2011`. Стабильные ID навсегда. |
| **Persistence сброшен** | `Application.persistentDataPath/KeyRodInstances.json` | Удалён (3 instance с одинаковым itemId=1010). Сервер пересоздал корректные. |
| **MyShipsTab: 3-level fallback** | `MyShipsTab.cs` | `RefreshShipList()` использует 3 источника: (1) серверные данные, (2) `KeyRodInstanceWorld.GetInstancesForPlayer()`, (3) `InventoryClientState.CurrentSnapshot`. Устойчиво к race-condition. |
| **Guard дубликата по instanceId** | `InventoryServer.cs` | `RequestPickupRpc` проверяет `HasKeyInstance(clientId, instanceId)` **до** TryPickup. Раньше — по itemId, что блокировало Medium/Heavy. |
| **Auto-attach ShipOwnershipRequirement** | `ShipController.cs` | `Awake()` → `if (GetComponent<ShipOwnershipRequirement>() == null) AddComponent`. |

### Архитектурный принцип

> **Стабильный ID для каждого предмета.** Все Key-предметы должны быть зарегистрированы в `ItemRegistry.asset` с явными ID. Auto-ID через `GetOrRegisterItemId` — fallback для тестов, не production.

### Тест-план

| Шаг | Ожидание |
|---|---|
| Play Host | Console: `[ItemRegistry] Loaded 2011 items` (включая 3 Key). `[ShipController] Auto-added ShipOwnershipRequirement на Ship_Medium_root` |
| **E** на любом ключе | `[InventoryServer] Pickup Key: TransferInstance(id=N, NONE→0)` + `[MyShipsTab] ownedKeyItemIds: [...]` |
| **P** → КОРАБЛЬ | Dropdown обновляется **в реальном времени** при подборе каждого ключа |
| Подобрать 3 разных ключа | Dropdown: 3 корабля |
| **F** у любого корабля | Доступ разрешён только при наличии ключа |

---

## §11. Что осталось

### ✅ Сделано

| MVP | Что | Статус |
|---|---|---|
| MVP-1 | Dropdown + placeholder | ✅ |
| MVP-2 | Resolution через KeyRodInstanceBinding | ✅ |
| MVP-3 | Подписка на ShipTelemetryClientState + info panel | ✅ |
| MVP-4 | Refresh при AddItem/RemoveItem | ✅ |
| Arch-refactor | ItemRegistry + 3-level fallback + auto-attach + guard | ✅ |

### 🔲 Phase 2 (отложено, не в MVP)

| Тикет | Что | Effort |
|---|---|---|
| T-KEY-08-2 | Детальные модули (иконки, описание, эффекты) | 2h |
| T-KEY-08-3 | Кнопка "Лететь к кораблю" (waypoint) | 3h |
| T-KEY-08-4 | Cargo UI во вкладке (открытие грузового отсека) | 4h |
| T-KEY-08-5 | Изменение имени корабля через UI | 1h |
| T-KEY-08-6 | HUD telemetry widget для активного корабля | 3h |
| T-KEY-08-7 | Trade через вкладку "Корабли" | 4h |

### 🔴 Известные смежные баги (требуют решения)

| Проблема | Описание | Приоритет | Effort |
|---|---|---|---|
| ~~InventoryTab показывает "x2 Pushka"~~ | ~~Если 2 PickupItem используют одно ItemData — инвентарь группирует по itemId.~~ ✅ **FIXED 2026-06-19** | ✅ P1 done | 30min |
| ~~Фильтр "Key" ломается после pickup 3 ключей~~ | ~~Расследовать — возможно `InventoryClientState.OnSnapshotUpdated` стреляет несколько раз, фильтр не успевает пересчитаться~~ | ~~P2~~ ✅ done | — |

### Что дальше

**Самый логичный следующий шаг** — фикс дубликатов в InventoryTab (отображение Key-предметов по `instanceId`). Без этого игрок видит `x2 Pushka` хотя это разные ключи. 30 минут работы, решит оставшуюся UX-проблему.

После этого — **Phase 2 фичи** или **другая подсистема** (Crafting, Markets, Quests и т.п.).

---

*Changelog ведёт агент Mavis. Дата: 2026-06-19*