# T-CARGO-UI-01: детальный список груза в MyShipsTab

> **Тикет:** T-CARGO-UI-01
> **Дата:** 2026-07-02
> **Автор:** Mavis
> **Статус:** ✅ **СДЕЛАНО** (2026-07-02, ~2.5 ч)
> **Зависит от:** `docs/Ships/cargo_system/CARGO_REMAINING_WORK_2026-07-02.md` §2.1
> **Блокирует:** T-CARGO-UI-02 (cargo manager) — без деталей груза нечего показывать
> **Коммиты:** [юзер коммитит сам — список файлов ниже]

---

## 1. Проблема

`MyShipsTab` в `CharacterWindow` (таб «Корабль») показывает:

```
🚀 Корабль #3 (Light)
Класс: Light
🔑 Key itemId=2010, instanceId=1
Топливо
[████░░░░] 40%
Топливо: 40.0% (100 max)
Груз
[██░░░░░░] 2/4
Груз: 2/4
...
```

Видно **progress bar** (`cargoUsed / cargoMax`) — но **самого списка items нет**. Игрок знает «в трюме 2 шт», но не знает **что именно**. Доп. проблемы:

1. **`cargoMax` = 0** в `ShipTelemetryState` — баг в `ShipController.UpdateTelemetryState` (строка 712-720: `cargoMax` инициализируется `0` и не пересчитывается через `GetEffectiveCargoLimits()`).
2. **`cargoUsed` = `cargo.Items.Count`** — это **число уникальных типов**, не суммарное число. Если в трюме 5 разных канистр — покажет `cargoUsed=5`. Не сходится с UI-ожиданием «x slots used».
3. **Cargo читается ТОЛЬКО из `MarketClientState.CurrentShipCargos`** (если есть) — а `CurrentShipCargos` обновляется **только когда игрок в зоне рынка**. Вне зоны — UI показывает stale или `—`.

---

## 2. Решение (high level)

**Push-подход через `ShipTelemetryState`** (5 Hz синхронизация, уже работает для fuel/position).

### 2.1 Расширить `ShipTelemetryState`

Добавить **массив cargo detail** в существующий `INetworkSerializable` payload:

```csharp
public struct ShipTelemetryState : INetworkSerializable, IEquatable<ShipTelemetryState>
{
    // ... существующие поля ...
    public int   cargoUsed;        // остаётся (используется MyShipsTab)
    public int   cargoMax;         // остаётся, но теперь считается правильно

    // T-CARGO-UI-01 NEW: детальный список items
    public CargoDetailDto[] cargoDetail; // null/empty = пустой трюм
}

public struct CargoDetailDto : INetworkSerializable
{
    public string itemId;          // "mesium_canister_v01"
    public string displayName;     // "Мезиум (канистра)" — резолвится на сервере
    public int    quantity;        // 5
    public float  unitWeight;      // 100 kg (для показа "5 × 100 кг = 500 кг")
    public bool   isDangerous;     // иконка-индикатор
    public bool   isFragile;
}
```

**Что НЕ включаем** (bandwidth): icon (Sprite), description. UI получит через `TradeItemDefinition` lookup по `itemId` (client-side `Resources.Load<TradeItemDefinition>`).

### 2.2 Серверная сторона — фикс в `ShipController.UpdateTelemetryState`

```csharp
// было (баг): cargoMax = 0 всегда
int cargoUsed = 0;
int cargoMax = 0;

// станет:
int cargoUsedSlots = 0;     // sum of (slots per item * qty)
int cargoMaxSlots = 0;
CargoDetailDto[] cargoDetail = Array.Empty<CargoDetailDto>();

if (TradeWorld.Instance != null)
{
    var cargo = TradeWorld.Instance.GetOrLoadCargo(NetworkObjectId, _resolvedCargoClass);
    if (cargo != null)
    {
        cargoMaxSlots = ShipCargoRegistry.GetEffectiveLimits(NetworkObjectId)?.maxSlots
                       ?? ShipClassLimits.Get(_resolvedCargoClass).maxSlots;
        cargoUsedSlots = cargo.ComputeTotalSlots(TradeWorld.Instance.Resolver);

        // Cargo detail (NEW)
        var items = cargo.Items;
        if (items != null && items.Count > 0)
        {
            var resolver = TradeWorld.Instance.Resolver;
            cargoDetail = new CargoDetailDto[items.Count];
            for (int i = 0; i < items.Count; i++)
            {
                var e = items[i];
                var def = resolver.GetDefinition(e.itemId); // safe lookup
                cargoDetail[i] = new CargoDetailDto
                {
                    itemId      = e.itemId,
                    displayName = def != null ? def.displayName : e.itemId,
                    quantity    = e.quantity,
                    unitWeight  = def != null ? def.weight : 0f,
                    isDangerous = def != null && def.isDangerous,
                    isFragile   = def != null && def.isFragile,
                };
            }
        }
    }
}
```

### 2.3 Клиентская сторона — `MyShipsTab.RenderSelectedShip`

Добавить в UXML **ScrollView** для списка items под progress bar. В C# — после рендера fuel/cargo bar:

```csharp
// в RenderSelectedShip() после рендера cargoText:
RenderCargoDetail(sc.TelemetryState.cargoDetail);

// новый метод:
private void RenderCargoDetail(CargoDetailDto[] items)
{
    if (_cargoListContainer == null) return;
    _cargoListContainer.Clear();

    if (items == null || items.Length == 0)
    {
        var empty = new Label("Трюм пуст");
        empty.AddToClassList("ship-cargo-empty");
        _cargoListContainer.Add(empty);
        return;
    }

    foreach (var it in items)
    {
        var row = new VisualElement();
        row.AddToClassList("ship-cargo-row");

        // name
        var name = new Label(FormatCargoName(it));
        name.AddToClassList("ship-cargo-name");
        row.Add(name);

        // qty + weight
        var qty = new Label($"×{it.quantity} ({it.quantity * it.unitWeight:F0} кг)");
        qty.AddToClassList("ship-cargo-qty");
        row.Add(qty);

        // danger/fragile icon
        if (it.isDangerous) row.AddToClassList("dangerous");
        if (it.isFragile) row.AddToClassList("fragile");

        _cargoListContainer.Add(row);
    }
}
```

---

## 3. Архитектурные решения

| # | Решение | Обоснование |
|---|---|---|
| **D19** | Push (NetworkVariable с массивом), не pull (RPC) | Уже синхронизируем telemetry 5 Hz — добавляем поле. Pull потребовал бы RPC на каждое открытие таба = +1 round-trip |
| **D20** | `cargoDetail` включаем в `ShipTelemetryState` (5 Hz), а не в отдельный NetworkVariable | Меньше сущностей, синхронизация батчится NGO |
| **D21** | `cargoUsed` = `ComputeTotalSlots` (sum qty*slots), не `Items.Count` | Соответствует GDD-логике (slot = единица ёмкости) + исправляет текущий баг |
| **D22** | `cargoMax` читается через `ShipCargoRegistry.GetEffectiveLimits` (per-instance + модули) | Уже работает для `TryLoadToShip`, не дублируем логику |
| **D23** | `displayName`/`icon`/etc. резолвятся на **сервере** (Unity.Collections.FixedString) | Сервер имеет `TradeItemDefinitionResolver` под рукой, клиент экономит bandwidth + не делает `Resources.Load` на каждое открытие |
| **D24** | Лимит на размер массива `cargoDetail`: до 32 items | Разумный потолок для корабля (Light=4, Medium=10, Heavy=20, HeavyII=30 + модули ~6-12). Hard cap = 32 = 32×~40 bytes = 1.3 KB на snapshot |
| **D25** | Не вводим новый `NetworkVariable<Dictionary>` или RPC | Push расширение существующего payload — минимум новой инфраструктуры |

---

## 4. Что меняется

| Файл | Что |
|---|---|
| `Assets/_Project/Scripts/Ship/Network/ShipTelemetryState.cs` | + `CargoDetailDto` struct, + `cargoDetail` поле, + `Equals/GetHashCode` (для NetworkVariable delta-detection) |
| `Assets/_Project/Scripts/Player/ShipController.cs` | `UpdateTelemetryState()` — фикс `cargoMax`, новый `cargoDetail` |
| `Assets/_Project/UI/Resources/UI/CharacterWindow.uxml` | + ScrollView для cargo items под progress bar |
| `Assets/_Project/UI/Resources/UI/CharacterWindow.uss` | + `.ship-cargo-list`, `.ship-cargo-row`, `.ship-cargo-name`, `.ship-cargo-qty`, `.ship-cargo-empty` стили |
| `Assets/_Project/Scripts/UI/Client/CharacterWindow/MyShipsTab.cs` | + `RenderCargoDetail()` + чтение из `telemetry.cargoDetail` + handle для "dangerous"/"fragile" |

**Не трогаем:**
- `TradeWorld` — без изменений (POCO работает)
- `MarketClientState` / `MarketWindow` — без изменений (используют свой snapshot, не telemetry)
- `ShipCargoRegistry` — без изменений (уже корректно)

---

## 5. Бандл тикетов (3 тикета)

### T-CARGO-UI-01a: расширение `ShipTelemetryState` + серверный push
- Файлы: `ShipTelemetryState.cs`, `ShipController.cs`
- Что: добавить `CargoDetailDto`, `cargoDetail` поле, фикс `cargoMax`, populate в `UpdateTelemetryState()`
- Тест: `compile OK`, существующий `MyShipsTab` показывает `cargoMax > 0` (если есть модули — то с бонусами)
- Оценка: ~1 ч

### T-CARGO-UI-01b: UXML + USS для cargo list
- Файлы: `CharacterWindow.uxml`, `CharacterWindow.uss`
- Что: ScrollView `ship-cargo-scroll` с container `ship-cargo-container` (по аналогии с `ship-modules-scroll`/`ship-modules-container`)
- Стили: `ship-cargo-row` (flex-row, align-center, padding 2-4px), `ship-cargo-name` (grow, font-size 11px), `ship-cargo-qty` (font-size 10px, color серый), `ship-cargo-empty` (italic, серый), опасный/хрупкий (warning цвет)
- Оценка: ~30 мин

### T-CARGO-UI-01c: `MyShipsTab.RenderCargoDetail`
- Файлы: `MyShipsTab.cs`
- Что: подписка на telemetry (уже есть), вызов `RenderCargoDetail(cargoDetail)` в `RenderSelectedShip` + `HandleShipStateChanged`
- Оценка: ~1 ч

**Суммарно: ~2.5-3 ч** (укладывается в 1 сессию).

---

## 6. Verification (для пользователя)

**Compile:**
- Открыть Unity Editor → Console → 0 errors
- Префабы в `BootstrapScene.unity` / `WorldScene_0_0.unity` не сломались

**Play Mode test recipe:**
1. Open `WorldScene_0_0` → Play → Start Host
2. Зайти в зону рынка `Primium` (центральный кластер, `MarketZone_Primium`)
3. Открыть `MarketWindow` (E у станции) → выбрать корабль → LOAD 3× `mesium_canister`, 1× `antigrav_ingot`
4. Выйти из зоны рынка (отлететь на 50+ м)
5. Открыть `CharacterWindow` (P) → таб «Корабль»
6. **Проверить:**
   - `cargoMax` показывает правильное значение (Light=4, Medium=10, ... + бонусы модулей если есть)
   - `cargoUsed` показывает `4` (= 3×1 + 1×1 slot)
   - **Список items** показывает 2 строки:
     - `Мезиум (канистра) ×3 (300 кг)`
     - `Антиграв (слиток) ×1 (50 кг)` — или с warning-цветом если fragile
7. UNLOAD 1× `mesium_canister` через рынок (вернуться в зону)
8. Без закрытия CharacterWindow — обновить таб (нажать «КОРАБЛЬ» повторно или переключиться и обратно)
9. **Проверить:** список показывает `Мезиум ×2 (200 кг)`, `cargoUsed=3`

**Ожидаемые логи в Console:**
```
[MyShipsTab] OnTabShown
[ShipController] cargo penalty=X.XX (при изменениях)
```

**Edge cases:**
- Пустой трюм → "Трюм пуст" placeholder
- Товар без `TradeItemDefinition` (legacy itemId) → `displayName = itemId`, `unitWeight = 0`, `isDangerous = false`
- Модули установлены → `cargoMax` корректно растёт
- NPC-корабль (нет KeyRodInstance) → `ownerClientId = 0`, но `cargoDetail` всё равно показывается (T-CARGO-NPC-01 — следующий эпик, но API уже готов)

---

## 7. Что НЕ делаем в этом тикете (явно out of scope)

- ❌ Cargo manager / Exchanger UI на корабле (T-CARGO-UI-02)
- ❌ 3D визуал ящиков на палубе (T-CARGO-VIS-01)
- ❌ NPC-cargo (T-CARGO-NPC-01)
- ❌ Менять `MarketWindow` / `MarketClientState` (там своя логика, работает)
- ❌ Иконки в списке items (только текст + warning-цвет для dangerous/fragile). Иконки — отдельный тикет, если юзер попросит
- ❌ Показ slot/weight деталей в одной строке (только qty × unitWeight, без "X slots used" breakdown)

---

## 8. Связанные документы

- `docs/Ships/cargo_system/CARGO_DIAGNOSIS_2026-06-17.md` — диагноз (T-CARGO-01..06 done)
- `docs/Ships/cargo_system/CARGO_REMAINING_WORK_2026-07-02.md` — сводный план 4 эпиков
- `docs/Ships/Key-subsystem/22_SHIP_TELEMETRY_PLAN.md` — существующая telemetry архитектура
- `docs/Ships/Key-subsystem/26_TKEY08_MYSHIPS_TAB_PLAN.md` — MyShipsTab plan
- `Assets/_Project/Trade/Scripts/TradeItemDefinition.cs` — definition формат
- `Assets/_Project/Scripts/Ship/Network/ShipTelemetryState.cs` — текущий payload
- `Assets/_Project/Scripts/Player/ShipController.cs:678-741` — UpdateTelemetryState (что фиксим)
- `Assets/_Project/Scripts/UI/Client/CharacterWindow/MyShipsTab.cs:289-348` — RenderSelectedShip (что расширяем)

---

## 9. Реализация (2026-07-02, ✅ DONE)

### 9.1 Изменённые файлы

| Файл | Что |
|---|---|
| `Assets/_Project/Scripts/Ship/Network/ShipTelemetryState.cs` | + `CargoDetailDto` struct, + `cargoDetail` поле в `ShipTelemetryState`, расширен `Equals` для delta-detection |
| `Assets/_Project/Scripts/Player/ShipController.cs` | `UpdateTelemetryState()`: фикс `cargoMax` (был всегда 0) — теперь через `ShipCargoRegistry.GetEffectiveLimits`; populate `cargoDetail[]` (cap 32) с резолвом `displayName/weight/dangerous/fragile` через `TradeItemDefinitionResolver.TryGet` |
| `Assets/_Project/UI/Resources/UI/CharacterWindow.uxml` | + ScrollView `ship-cargo-scroll` + container `ship-cargo-container` под progress bar |
| `Assets/_Project/UI/Resources/UI/CharacterWindow.uss` | + стили `.ship-cargo-scroll` / `.ship-cargo-row` / `.ship-cargo-name` / `.ship-cargo-qty` / `.ship-cargo-empty` + `.dangerous` / `.fragile` warning-цвета |
| `Assets/_Project/Scripts/UI/Client/CharacterWindow/MyShipsTab.cs` | + bind `_cargoScroll`/`_cargoContainer`, + `RenderCargoDetail(CargoDetailDto[])`, throttle `ShipTelemetryStateEqualsApprox` учитывает `cargoDetail` |
| `docs/Ships/cargo_system/CARGO_UI_01_DESIGN_2026-07-02.md` | этот документ (статус ✅) |
| `docs/Ships/cargo_system/CARGO_REMAINING_WORK_2026-07-02.md` | отмечен эпик 1 как done |

### 9.2 Архитектурные решения (закреплены)

- **D19:** push (NetworkVariable с массивом), не pull (RPC) — минимизация новой инфраструктуры
- **D20:** `cargoDetail` в `ShipTelemetryState` (5 Hz), не отдельный NetworkVariable
- **D21:** `cargoUsed = ComputeTotalSlots` (sum qty*slots), не `Items.Count` — соответствует GDD-логике + исправляет старый баг
- **D22:** `cargoMax` через `ShipCargoRegistry.GetEffectiveLimits` (per-instance + модули)
- **D23:** `displayName/flags/weight` резолвятся на **сервере** (FixedString64Bytes + byte flags) — клиент экономит bandwidth + не делает Resources.Load
- **D24:** Hard cap = 32 items (Light=4 / Medium=10 / Heavy=20 / HeavyII=30 + ~6-12 module-bonus)
- **D25:** `CargoDetailDto` — `byte flags` (bit0=dangerous, bit1=fragile) вместо двух bool — экономит 1 byte на entry

### 9.3 Verification (компиляция)

```
refresh_unity mode=force compile=request wait_for_ready=true → success, ready=True
read_console types=error,warning count=30 → 0 наших ошибок/варнингов
filter ShipTelemetryState → 0 entries
filter MyShipsTab → 0 entries
filter ShipController.cs → 0 entries
filter CARGO-UI-01 → 0 entries
```

(Варнинги в Console — legacy obsolete API от других подсистем, не наши.)

### 9.4 Verification (Play Mode, для пользователя)

**Recipe (для проверки):**

1. Open `WorldScene_0_0` → Play → Start Host
2. Зайти в зону рынка `Primium` (центральный кластер, `MarketZone_Primium`)
3. Открыть `MarketWindow` (E у станции) → выбрать корабль → LOAD 3× `mesium_canister`, 1× `antigrav_ingot`
4. Выйти из зоны рынка (отлететь на 50+ м)
5. Открыть `CharacterWindow` (P) → таб «Корабль»
6. **Проверить:**
   - `cargoMax` показывает правильное значение (Light=4, Medium=10, ... + бонусы модулей)
   - `cargoUsed` показывает `4` (= 3×1 + 1×1 slot)
   - **Список items** показывает 2 строки:
     - `Мезиум (канистра) ×3 (300 кг)` (или с ⚠ если dangerous)
     - `Антиграв (слиток) ×1 (50 кг)` (или с ❄ если fragile)
7. UNLOAD 1× `mesium_canister` через рынок (вернуться в зону)
8. Без закрытия CharacterWindow — переключиться на другой таб и обратно на «Корабль»
9. **Проверить:** список показывает `Мезиум ×2 (200 кг)`, `cargoUsed=3`

**Edge cases для проверки:**
- Пустой трюм → ScrollView скрыт, показывается "Трюм пуст"
- Товар без `TradeItemDefinition` → `displayName = itemId`, `unitWeight = 0`
- Модули установлены → `cargoMax` корректно растёт (через `GetEffectiveCargoLimits`)
- Снапшот работает при выходе/входе в зону рынка (telemetry не зависит от зоны)

### 9.5 Pitfalls / lessons learned

1. **Equals должен включать cargoDetail** — иначе NetworkVariable не увидит изменение items и не пошлёт delta (первая версия бага, исправлена в одном patch).
2. **TradeItemDefinitionResolver.TryGet** — это правильный API, не `GetDefinition` (которого нет).
3. **`FixedString64Bytes` limit 61 chars** — displayName обрезается до 60 + NUL. Если имя длиннее — потеряется. Не критично для русского UI сейчас, но для будущих itemId с длинными именами — пересмотреть на 128Bytes.
4. **Throttle (`ShipTelemetryStateEqualsApprox`)** — старый throttle не учитывал `cargoDetail`, поэтому qty-only изменения могли пропускаться. Расширен.
5. **`ShipClassLimits.Get` fallback** — если `ShipCargoRegistry` не содержит корабль (например, NPC-корабль без регистрации), fallback на статический class-limits уже работает в `CargoData.cs:61`. **Но для NPC это потенциальная проблема в T-CARGO-NPC-01** (NPC-корабли могут не быть зарегистрированы) — будет решено в следующем эпике.

### 9.6 Открытые вопросы / Что НЕ сделано

- ❌ Не делал кнопки [ВЗЯТЬ] / [ПОЛОЖИТЬ] в списке items (для T-CARGO-UI-02 — следующий эпик, требует решения Q7)
- ❌ Не показывал иконки предметов (только текст + warning-цвет). Иконки требуют резолва Sprite, вынесу в отдельный тикет если юзер попросит
- ❌ Не выводил детальный breakdown slot/weight (только qty × unitWeight). Если нужен «X/Y slots used, Z/W kg» — отдельный тикет
- ❌ Не выводил `displayName` из Resources.Load на клиенте (сервер пушит готовое имя). Если изменится TradeItemDefinition.displayName, все клиенты увидят обновление после следующего telemetry-tick (5 Hz max)
- ⚠️ Первый раз используется `byte flags` паттерн в NetworkVariable — если NGO 2.11 имеет баги с `byte` сериализацией, увидим в Play Mode

---

## 10. История

| Дата | Сессия | Изменения |
|---|---|---|
| 2026-07-02 | T-CARGO-UI-01 | Диздок → код → verify (compile OK 0 errors). Сервер-push `cargoDetail[]` через `ShipTelemetryState`. UI-рендер в MyShipsTab. Фикс бага `cargoMax=0`. |
