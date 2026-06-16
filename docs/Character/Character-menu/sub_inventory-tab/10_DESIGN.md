# Inventory Sub-System — Design (UXML/USS/Classes)

**Дата:** 2026-06-05
**Зависит от:** `00_OVERVIEW.md`

Этот документ описывает **что нарисовано** (TAB-колесо + P-таб) и какие USS-классы используются.

---

## 1. TAB-колесо (`InventoryUI`)

### 1.1 Файлы
- `Assets/_Project/UI/Resources/UI/InventoryWheel.uxml` — структура
- `Assets/_Project/UI/Resources/UI/InventoryWheel.uss` — стили
- `Assets/_Project/UI/Client/InventoryUI.cs` — контроллер (подписка на ClientState, hover/select, sublist)

### 1.2 UXML — структура

```
.wheel-container                              ← главный контейнер, position: absolute
  ├── .header
  │     ├── #wheel-title    "ИНВЕНТАРЬ"
  │     └── #wheel-hint     "TAB — закрыть • Клик по сектору — список предметов"
  │
  ├── .wheel-area
  │     ├── #wheel          ← круг (380×380), position: relative
  │     │     ├── #sector-0 ... #sector-7   ← 8 секторов (110×110 каждый)
  │     │     ├── #label-0 ... #label-7     ← лейблы внутри секторов
  │     │     └── .wheel-center
  │     │           ├── #center-type-label   "Ресурсы" / "Оборудование" / ...
  │     │           └── #center-count-label  "0", "3", "20"
  │     │
  │     └── #sublist-panel
  │           ├── #sublist-title             "Ресурсы (3)"
  │           └── #sublist                   ListView (предметы выбранного сектора)
  │
  ├── .actions
  │     ├── #use-btn         "ИСПОЛЬЗОВАТЬ"
  │     └── #close-btn       "ЗАКРЫТЬ"
  │
  └── #message-label        "Откройте инвентарь по TAB"
```

### 1.3 USS — сектора по окружности

Радиус: 130 px от центра. Размер сектора: 110×110 px. **8 секторов** расположены через `position: absolute; top/left` (ручной расчёт):

| Sector | Type | top | left |
|---|---|---|---|
| #sector-0 | Resources | 35px | 135px | (верх) |
| #sector-1 | Equipment | 75px | 235px | (верх-право) |
| #sector-2 | Food | 135px | 290px | (право) |
| #sector-3 | Fuel | 235px | 235px | (низ-право) |
| #sector-4 | Antigrav | 270px | 135px | (низ) |
| #sector-5 | Meziy | 235px | 35px | (низ-лево) |
| #sector-6 | Medical | 135px | -20px | (лево) |
| #sector-7 | Tech | 75px | 35px | (верх-лево) |

> **Важно:** все стили используют `!important` для победы над `UnityDefaultRuntimeTheme` (pitfall #24 из `unity-mcp-orchestrator` skill).

### 1.4 USS-классы — состояния секторов

| Класс | Когда применяется | CSS-эффект |
|---|---|---|
| `.sector` | всегда | базовая рамка, серый фон |
| `.sector-empty` | `count == 0` | тёмный фон, серая рамка |
| `.sector-has-items` | `count > 0` | зелёный фон, светлая зелёная рамка |
| `.sector-hover` | наведение мыши | жёлтый фон, яркая жёлтая рамка, scale 1.1 |
| `.sector-selected` | клик | золотой фон, толстая золотая рамка, scale 1.15 |

> Может быть несколько классов одновременно: `.sector .sector-empty` или `.sector .sector-has-items .sector-selected`.

### 1.5 Sublist (правая панель)

`ListView` (UI Toolkit) с кастомными row-шаблонами:

```
.sublist-row                          (height: 32px, flex-direction: row)
  ├── .sublist-row-icon               (24×24, иконка предмета)
  ├── .sublist-row-name  flex-grow    (имя предмета, e.g. "Железная руда")
  └── .sublist-row-qty   40px         ("×N" если qty > 1)
```

`MakeSublistRow` создаёт структуру, `BindSublistRow` заполняет данными. Клик по row → `_selectedItemIndex` (для `RequestUse` — TODO).

### 1.6 Контроллер (логика)

**`InventoryUI.cs`** — основной класс:
- `Awake` — InputAction "<Keyboard>/tab"
- `OnEnable` — `EnsureBuilt()` + `TrySubscribeToClientState()`
- `OnDisable` — unsubscribe
- `Update` — retry подписки (если ClientState создался позже)
- `EnsureBuilt` — Q<>() element refs, ListView factory, pointer events
- `HandleSnapshotUpdated(snap)` — обновляет sector classes (empty/has-items), обновляет лейблы
- `HandleResultReceived(result)` — feedback в message label (cross-tab, см. pitfall #11)
- `OnSectorClick(idx)` — добавляет `sector-selected`, вызывает `RefreshSublist`
- `RefreshSublist(type)` — ListView.itemsSource = `state.GetItemsByType(type)`, обновляет center label/count
- `Toggle` — Tab handler (visibility + RequestRefresh)
- `SetVisible(bool)` — display, pickingMode, cursor lock/unlock

### 1.7 Input
- **Tab** — Toggle (открыть/закрыть колесо)
- **Esc** — закрыть (через Update в CharacterWindow — нет, в самом InventoryUI не реализовано, TODO Phase 8+)
- **Click на секторе** — select + показать sublist
- **Click на row sublist** — select item

---

## 2. P-таб "Инвентарь" (CharacterWindow)

### 2.1 Файлы
- `Assets/_Project/UI/Resources/UI/CharacterWindow.uxml` — структура (уже существовало до v2)
- `Assets/_Project/UI/Resources/UI/CharacterWindow.uss` — стили
- `Assets/_Project/Scripts/UI/Client/CharacterWindow.cs` — контроллер (патчи Phase 5)

### 2.2 UXML — секция (уже было)

```xml
<ui:VisualElement name="inventory-section" class="list-section" style="display: none;">
    <ui:Label text="Инвентарь" class="section-title" />
    <ui:ListView name="inventory-list" class="item-list" />
</ui:VisualElement>
```

### 2.3 USS-классы (уже было)

| Класс | Что |
|---|---|
| `.inventory-row` | flex-direction: row, height: 30px, padding, hover-эффект |
| `.inventory-icon` | 24×24, иконка |
| `.inventory-name` | flex-grow, имя предмета |
| `.inventory-type` | 110px, тип (rus name) |
| `.inventory-qty` | 40px, "×N" |
| `.inventory-row-empty` | opacity 0.5, italic |

### 2.4 Контроллер (логика Phase 5)

**`CharacterWindow.cs`** — изменения:
- `using ProjectC.Items.Client;` + `using ProjectC.Items.Dto;`
- Подписка `OnSnapshotUpdated` / `OnInventoryResult` в `EnsureBuilt`
- Отписка в `OnDisable` (event-leak prevention)
- `RefreshInventoryCache` — читает `InventoryClientState.CurrentSnapshot`:
  - Группирует items по `itemId` (суммирует quantity)
  - `InventoryListItem { itemId, displayName, type, quantity, icon }`
  - Получает `ItemData def = invState.GetItemDefinition(itemId)` для имени/icon
- `HandleInventorySnapshotUpdated` — обновляет credits в header (cross-tab); если `activeTab == "inventory"` → `RefreshInventoryCache + ApplyInventoryFilters`
- `HandleInventoryResultReceived` — feedback в `_messageLabel` (cross-tab, см. pitfall #11)

### 2.5 Фильтры (уже было до Phase 5)

```
.filters-row (display: flex только при activeTab in [contracts, inventory])
  ├── #filter-source  DropdownField   "Все типы" / "Ресурсы" / "Оборудование" / ...
  ├── #filter-state   DropdownField   (скрыт для inventory)
  └── #filter-search  TextField       (поиск по имени, substring)
```

`ApplyInventoryFilters()`:
- source filter: `ItemTypeNames.GetDisplayName(i.type) == source`
- search filter: `i.displayName.ToLower().Contains(search)`

### 2.6 Input
- **P** — открыть/закрыть CharacterWindow (в `NetworkPlayer.Update`)
- **Esc** — закрыть (в `CharacterWindow.Update`)
- **Click "ИНВЕНТАРЬ"** в табах — `SwitchTab("inventory")` → показывает `inventory-section`, скрывает остальные
- **Click row в ListView** — `_selectedInventoryItem` (для будущих use/drop actions)

---

## 3. Согласованность данных

**Когда обновляется TAB-колесо:**
- При `InventoryClientState.OnSnapshotUpdated` (новый snapshot с сервера)
- Внутри: `HandleSnapshotUpdated` → пересчёт sector'ов + sublist (если выбран)

**Когда обновляется P-таб:**
- Только когда `activeTab == "inventory"` И `OnSnapshotUpdated` сработал
- Если другой таб активен — данные в кэше всё равно обновятся (`RefreshInventoryCache` при следующем SwitchTab), но ListView не пересоздаётся (экономим CPU)
- При возврате на таб "Инвентарь" из другого — `SwitchTab` вызывает `RefreshInventoryCache + ApplyInventoryFilters`

**Race conditions:**
- Если сервер прислал snapshot ДО открытия окна → кэш в `_inventoryCache` уже полный, `RefreshInventoryCache` его правильно отдаст
- Если клиент инициирует `RequestPickup`, а сервер ещё не ответил → UI не блокируется (optimistic в Phase 8+)

---

## 4. USS-классы (полный список)

| Класс | Где | Эффект |
|---|---|---|
| `.wheel-container` | TAB | главный контейнер, position absolute |
| `.wheel-area` | TAB | flex row: wheel + sublist |
| `.wheel` | TAB | 380×380, position relative |
| `.sector` | TAB | базовый sector 110×110 |
| `.sector-N` (0-7) | TAB | позиция через top/left |
| `.sector-empty` | TAB | пустой сектор (тёмный) |
| `.sector-has-items` | TAB | с предметами (зелёный) |
| `.sector-hover` | TAB | hover (жёлтый, scale 1.1) |
| `.sector-selected` | TAB | selected (золотой, scale 1.15) |
| `.sector-label` | TAB | label внутри сектора |
| `.sector-label-N` (0-7) | TAB | позиция label'а |
| `.wheel-center` | TAB | центральный круг |
| `.center-type-label` | TAB | текст типа в центре |
| `.center-count-label` | TAB | счётчик в центре (большой) |
| `.sublist-panel` | TAB | правая панель (flex-grow) |
| `.sublist-title` | TAB | "Ресурсы (3)" |
| `.sublist-list` | TAB | ListView (sublist) |
| `.sublist-row` | TAB | row шаблон (32px) |
| `.sublist-row-icon` | TAB | 24×24 иконка |
| `.sublist-row-name` | TAB | flex-grow имя |
| `.sublist-row-qty` | TAB | 40px "×N" |
| `.character-window` | P | главный контейнер CharacterWindow |
| `.inventory-row` | P | row шаблон в ListView (30px) |
| `.inventory-icon` | P | 24×24 |
| `.inventory-name` | P | flex-grow |
| `.inventory-type` | P | 110px, тип (rus) |
| `.inventory-qty` | P | 40px "×N" |
| `.inventory-row-empty` | P | opacity 0.5 |
| `.message-label` | оба | feedback (успех/ошибка) |

---

## 5. Layout & Sizing

| Параметр | TAB-колесо | P-таб |
|---|---|---|
| Width | 800px (max 90%) | 720px (max 90%) |
| Height | auto (max 90%) | auto (max 92%) |
| Позиция | top 5%, center | top 4%, center |
| Background | rgba(20,25,35,0.92) | rgba(20,25,35,0.95) |
| Border | 2px, rgba(120,150,200,0.7) | 2px, rgba(120,150,200,0.8) |

---

## 6. Performance considerations

- **ListView recycling:** UI Toolkit ListView переиспользует row'ы (не создаёт новый VE на каждый item). Важно для 32-slot инвентаря.
- **Snapshot throttling:** сервер шлёт snapshot только при изменении (NetworkVariable `OnValueChanged`). Не на каждый кадр.
- **Sublist rebuild:** вызывается ТОЛЬКО при `RefreshSublist(type)` — не на каждый snapshot, а только при выборе нового сектора или изменении данных.
- **P-таб кэш:** `_inventoryCache` (List<InventoryListItem>) обновляется в `RefreshInventoryCache` (вызывается при `activeTab == "inventory"` + snapshot). ListView.itemsSource = cache, не аллоцируется заново на каждый snapshot.

---

## 7. Расширения (Phase 8+)

- **Drag-and-drop** в P-таб sublist → перемещение между слотами (требует RequestMove RPC, уже есть stub)
- **Drop в мир** (SpawnPickupItem на сервере) — пока stub
- **Иконки** — спрайты для каждого ItemType (сейчас `icon = null` для тестового датасета)
- **Анимация вспышки** при pickup (transition на `.sector-N` после `OnInventoryResult.IsSuccess`)
- **Quantity > 1** — stackable inventory (требует изменений в `InventoryData`)
- **Cargo limits** — `weightKg` уже есть, нужна логика суммирования
- **Tooltips** — при hover на row в P-таб показывать описание предмета
