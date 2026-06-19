# T-KEY-08: MyShipsTab UI — план реализации

> Интеграция UI вкладки "ship" в CharacterWindow.  
> Дата: 2026-06-19 | Версия: v12 (план) | Тикет: T-KEY-08

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
| `InventoryData.GetKeyInstanceIds()` | Параллельный список `instanceId` (стабильные между сессиями? — нет, эфемерные. См. ниже) | Клиент |
| `KeyRodInstanceWorld.GetInstancesForPlayer(clientId)` | Список `instanceId` ключей, которыми владеет игрок | Server-only |
| `KeyRodInstanceWorld.GetInstance(instanceId)` | `KeyRodInstance` (itemId, registeredShipId, ownerPlayerId, state) | Server-only |
| `ShipTelemetryClientState.MyShips` | Список `ShipTelemetryState` для кораблей владельца | Клиент |

### 2.2 Проблема: instanceId эфемерный, KeyRodInstanceWorld — server-only

После рестарта persistence `instanceId` переназначается. На клиенте `KeyRodInstanceWorld` НЕ инициализирован. Чтобы получить список "моих кораблей" на клиенте, нужен **путь через InventoryData + scene-placed `KeyRodInstanceBinding`** (как мы делали в `InventoryTab.ResolveKeyItemDisplayName`).

### 2.3 Решение — Client-side resolution

Аналогично `ResolveKeyItemDisplayName`:

```csharp
// В новом MyShipsTab:
List<ShipTelemetryState> myShips = new();

// 1. Получить все Key-слоты из инвентаря
foreach (var kv in inventoryData.GetAllKeySlots()) {
    // 2. Найти KeyRodInstanceBinding по itemId в сцене (стабильный scene-placed)
    // 3. Получить _ship → ShipController
    // 4. Взять _telemetryState.Value
    myShips.Add(ship.TelemetryState);
}
```

**Преимущества**: работает между сессиями (стабильно).

### 2.4 UI структура (UXML)

```xml
<section name="ship-section" class="ship-section">
  <!-- Header: dropdown выбора корабля -->
  <DropdownField name="ship-selector"
                 label="Корабли"
                 class="ship-selector" />

  <!-- Body: информация о выбранном корабле -->
  <VisualElement name="ship-info" class="ship-info">

    <!-- Карточка 1: общее -->
    <Label class="ship-info-name" name="ship-info-name" />
    <Label class="ship-info-class" name="ship-info-class" />
    <Label class="ship-info-key-id" name="ship-info-key-id" />

    <!-- Карточка 2: топливо -->
    <ProgressBar name="ship-fuel-bar" />
    <Label name="ship-fuel-text" />

    <!-- Карточка 3: груз -->
    <ProgressBar name="ship-cargo-bar" />
    <Label name="ship-cargo-text" />

    <!-- Карточка 4: модули -->
    <Label class="ship-info-header" text="Установленные модули:" />
    <ListView name="ship-modules-list" />

    <!-- Карточка 5: позиция (для отладки) -->
    <Label name="ship-info-position" />

    <!-- Card 6: состояние -->
    <Label name="ship-info-state" />
  </VisualElement>
</section>
```

### 2.5 CSS classes

```css
.ship-section { padding: 8px; }
.ship-selector { margin-bottom: 12px; }
.ship-info { padding: 12px; background: rgba(0,0,0,0.1); border-radius: 4px; }
.ship-info-name { font-size: 18px; -unity-font-style: bold; margin-bottom: 4px; }
.ship-info-class { font-size: 14px; color: rgb(180,180,180); margin-bottom: 12px; }
.ship-info-header { font-size: 14px; -unity-font-style: bold; margin-top: 12px; margin-bottom: 4px; }
```

---

## §3. Реализация — файл за файлом

### Шаг 1: NEW — `Assets/_Project/Scripts/UI/Client/CharacterWindow/MyShipsTab.cs`

```csharp
public class MyShipsTab
{
    // Ссылки на UI элементы
    private DropdownField _shipSelector;
    private VisualElement _shipInfo;
    private Label _name, _class, _keyId, _fuelText, _cargoText, _position, _state;
    private ProgressBar _fuelBar, _cargoBar;
    private ListView _modulesList;

    // Текущий выбранный корабль
    private ulong _currentShipNetId;

    // Метод вызывается CharacterWindow при SwitchTab("ship")
    public void Build(VisualElement shipSection) { /* attach to UXML */ }

    // Подписка на telemetry updates
    public void Subscribe(ShipTelemetryClientState state) { /* OnShipStateChanged */ }
    public void Unsubscribe() { /* unsubscribe */ }

    // Когда инвентарь изменился (key picked up / dropped)
    public void RefreshShipList() {
        // 1. Получить все Key-слоты
        // 2. Для каждого — найти KeyRodInstanceBinding в сцене по itemId
        // 3. Получить ShipController.TelemetryState
        // 4. Обновить _shipSelector choices
        // 5. Если ничего не выбрано → выбрать первый
        // 6. Обновить info panel
    }

    private void OnShipSelectorChanged(ChangeEvent<string> evt) {
        // Парсим выбор, обновляем _currentShipNetId
        // Обновить info panel из telemetry state
    }

    private void OnTelemetryChanged(ulong shipNetId, ShipTelemetryState state) {
        // Если это наш корабль — обновить UI
    }
}
```

### Шаг 2: PATCH — `CharacterWindow.cs`

- + `MyShipsTab _myShipsTab` поле
- + `BuildMyShipsTab()` вызывается из `EnsureBuilt()`
- В `SwitchTab("ship")` → `_myShipsTab.RefreshShipList()` + `Subscribe()`
- В `SwitchTab(<other>)` → `_myShipsTab.Unsubscribe()`
- В `OnDestroy` / `OnDisable` → `_myShipsTab.Unsubscribe()`

### Шаг 3: PATCH — `CharacterWindow.uxml`

Добавить `<section name="ship-section">` сразу после открытия `<div name="tab-content-ship">`:

```xml
<div id="tab-content-ship" name="tab-content-ship" class="tab-content" style="display: none;">
    <section name="ship-section" class="ship-section">
        <DropdownField name="ship-selector" label="Корабли" class="ship-selector" />
        <VisualElement name="ship-info" class="ship-info">
            <!-- карточки как в §2.4 -->
        </VisualElement>
    </section>
</div>
```

### Шаг 4: PATCH — `CharacterWindow.uss`

+ `.ship-section`, `.ship-selector`, `.ship-info`, `.ship-info-name`, etc. (см. §2.5).

### Шаг 5: PATCH — `InventoryClientState.cs` (опционально)

+ `event Action OnInventoryUpdated` — стреляет при AddItem/RemoveItem для Key. `MyShipsTab` подписывается на это событие и перезагружает список.

Альтернатива: перезагружать список при открытии вкладки (тогда event не нужен).

---

## §4. Подводные камни

### 4.1 Стабильность между сессиями

`InstanceId` эфемерный. После рестарта:
- `InventoryData.keySlots[i].instanceId` — старый
- `ShipController._telemetryState.keyInstanceId` — старый (но `ShipController.OnNetworkSpawn` перезаписывает)

Решение: **не использовать `instanceId` для matching** на клиенте. Использовать `itemId` (стабильный) + scene-placed `KeyRodInstanceBinding` (стабильный).

### 4.2 KeyRodInstanceBinding инициализируется после InventoryServer

Если игрок открывает вкладку до того как сервер создал `KeyRodInstance` (OnNetworkSpawn race) — `KeyRodInstanceWorld.GetInstance` вернёт null.

Решение: `RefreshShipList()` итерирует scene-placed bindings напрямую, не через `KeyRodInstanceWorld`. См. `ResolveKeyItemDisplayName` Priority 3 — тот же подход.

### 4.3 ServerCargoPenalty и ShipModuleManager — где?

Поля `cargoUsed/cargoMax/moduleCount` уже в `ShipTelemetryState`. Они обновляются в `ShipController.UpdateTelemetryState()` (T-KEY-07) при `IsServer`. На клиенте они доступны через `ShipController.TelemetryState`.

### 4.4 Когда P открыт, а корабль далеко

`ShipTelemetryClientState` агрегирует ВСЕ корабли клиента, независимо от дистанции. NetworkVariable синхронизируется NGO глобально.

### 4.5 Что показывать если у игрока нет ключей?

`ship-selector` пустой → показать placeholder "Нет доступных кораблей. Найдите ключ в мире."

### 4.6 Что показывать если корабль уничтожен?

`ShipTelemetryState.state` (byte). `KeyRodInstanceState.Destroyed` ≠ `ShipState.Destroyed` — это разные enum'ы. Показывать "Уничтожен" если `state == (byte)ShipState.Destroyed`.

---

## §5. Тест-план

| Шаг | Ожидание |
|---|---|
| 1. Play Host (без ключей) | Вкладка "КОРАБЛЬ" → placeholder "Нет доступных кораблей" |
| 2. **E** на [KeyRod_ShipLight] → открыть P | Выпадающий список: `🚀 Pushka` (1 item) |
| 3. Выбрать Pushka | Info: `Pushka (Light)`, fuel bar, cargo bar, modules list |
| 4. Открыть P, переключиться на другой таб, обратно | Список сохранился, выбор сохранился |
| 5. Exit Play → Play снова | При открытии P: список всё ещё `🚀 Pushka` |
| 6. Подобрать второй ключ (heavy) | Список: `🚀 Pushka`, `🚀 Hammer` |
| 7. Переключиться между кораблями | Info панель обновляется с правильными данными каждого |

---

## §6. Effort итерации

| Итерация | Что | Файлы | Effort |
|---|---|---|---|
| **MVP-1** | Dropdown + placeholder | UXML + USS + MyShipsTab.cs (stub) | 0.5h |
| **MVP-2** | Resolution через `KeyRodInstanceBinding` (без telemetry) | MyShipsTab.cs | 1h |
| **MVP-3** | Подписка на `ShipTelemetryClientState` + рендер info | MyShipsTab.cs | 1.5h |
| **MVP-4** | Refresh при AddItem/RemoveItem (опционально) | InventoryClientState.cs | 0.5h |
| **MVP-5** | Modules list (читает ShipModuleManager через reflection) | MyShipsTab.cs | 1h |

**Total MVP**: ~4.5h

---

## §7. Что отложено в Phase 2

| Фича | Почему |
|---|---|
| Кнопка "Лететь к кораблю" | Требует waypoint/companion system |
| Кнопка "Открыть грузовой отсек" | Cargo UI — отдельный тикет |
| Trade через вкладку | Это уже в Markets UI |
| Изменение имени корабля | Не было в MVP |
| Детальные модули (иконки, описание) | Phase 2 |

---

## §8. Зависимости (нужно для старта)

| Зависимость | Статус |
|---|---|
| `ShipTelemetryClientState.MyShips` populated | ✅ T-KEY-07 |
| `InventoryData.GetIdsForType(Key)` | ✅ T-KEY-02 |
| `KeyRodInstanceBinding._ship/_keyItemData` scene-placed | ✅ T-KEY-04 + scene setup |
| Пустая вкладка `tab-content-ship` в UXML | ✅ есть |

**Всё готово. Можно начинать MVP-1.**