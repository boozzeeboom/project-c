# Ship HUD — Implementation & Design Notes

**Дата:** 2026-06-17  
**Статус:** ✅ **Реализовано (V1)** — 5 колонок, 5 колонок с контентом + топливо под скоростью  
**Файл контроллера:** `Assets/_Project/Scripts/Ship/UI/ShipHudController.cs` (~980 LOC)  
**Ассеты:** `Assets/_Project/Ship/UI/Resources/UI/`  
**Scene:** `[ShipHudPanel]` in `BootstrapScene.unity`

---

## 1. Назначение

Замена `ShipDebugHUD` (IMGUI, F3) и `Mezi yStatusHUD_Legacy` (IMGUI, F4).  
Единый UI Toolkit overlay (runtime-constructed VisualElement, без UXML/USS).

- Появляется **только** когда `NetworkPlayer.IsInShip == true` (сидит в PilotSeat)
- Исчезает мгновенно при выходе
- НЕ блокирует ввод (`pickingMode = Ignore`/`Position`)
- НЕ разблокирует курсор
- Client-side только, без RPC

---

## 2. Текущая архитектура

### 2.1 Компоненты

| Компонент | Где | Роль |
|---|---|---|
| `[ShipHudPanel]` (GameObject) | `BootstrapScene.unity`, root | Контейнер: UIDocument + ShipHudController |
| `ShipHudController` (MonoBehaviour) | `Assets/_Project/Scripts/Ship/UI/ShipHudController.cs` | `TryBuild()` → дерево, `Update()` → опрос `LocalPlayer`, `Refresh()` → колонки |
| `ShipHudPanelSettings` (PanelSettings) | `Assets/_Project/Ship/UI/Resources/UI/ShipHudPanelSettings.asset` | themeUss=UnityDefaultRuntimeTheme, sortingOrder=50 |
| `ShipHudPanel.uxml` (stub) | `Assets/_Project/Ship/UI/Resources/UI/ShipHudPanel.uxml` | Пустой UXML для YAML-целостности (не используется) |

### 2.2 Источники данных

Все данные — `public` геттеры, additive only. Никаких изменений существующей логики.

| Данные | Свойство `ShipController` | Добавлено в |
|---|---|---|
| СКОРОСТЬ (forward, км/ч) | `ForwardSpeedMps` × 3.6 | S-HUD-01 |
| MAX SPEED (км/ч) | `MaxSpeed` × 3.6 | S-HUD-01 |
| LIFT (вертикальная, м/с) | `VerticalSpeed` | S-HUD-01 |
| TURN (angularVelocity.y → deg/s) | `AngularVelocity.y * Rad2Deg` | S-HUD-01 |
| PITCH (°) | `PitchAngleDegrees` | S-HUD-01 |
| BANK (°) | `RollAngleDegrees` | S-HUD-01 |
| MODULES (список слотов) | `ShipModuleManager.slots` | S-HUD-03d |
| MEZIY STATE (цвет/перегрев) | `MeziyModuleActivator.GetState(id)` | S-HUD-03d |
| WIND скорость + направление | `WindManager.Instance.CurrentWindSpeed/Direction` | singleton |
| ALTITUDE (высота + коридор) | `ActiveCorridor` + `CurrentAltitudeStatus` | S-HUD-01 |
| FUEL | `FuelSystem.CurrentFuel/MaxFuel/FuelPercent/isRefueling` | S-HUD-03b-v2 |
| REFUEL rate | `FuelSystem.AtmosphericRefuelRate` | ShipFuelSystem |

### 2.3 Цикл обновления

```csharp
ShipHudController.Update() каждый кадр:
1. TryBuild() если _built == false (5-step guards: doc/rootVE/panelSettings)
2. EnsureLocalPlayer() — ищет NetworkPlayer по SpawnManager.PlayerObjects + IsOwner
3. SetVisible(lp.IsInShip && lp.CurrentShip != null) — toggle display
4. Refresh(ship) — 4 колонки:
   - UpdateSpeedColumn(ship)      → К3 + топливо
   - UpdateFlightColumn(ship)     → К2
   - UpdateModulesColumn(ship)    → К1
   - UpdateEnvColumn(ship)        → К4
   - К5 (Dispatch) — статичен
```

---

## 3. Layout 5 колонок

### 3.1 Схема (63px высота, ~1060px общая ширина)

```
┌────────────────────────────────────────────────────────────────────────────────────────────┐
│ top:8px                                                                                     │
│ ┌────────────┬──────────────┬────────────────┬──────────────┬──────────────┐               │
│ │ MODULES    │ FLIGHT       │    SPEED       │    ENV       │  DISPATCH    │               │
│ │            │              │    87 км/ч     │              │              │               │
│ │ ● PITCH    │ LIFT +1.2   │  ━━━━━━━━━━    │ 12.4 м/с  ◐  │ DISPATCHER---│               │
│ │ ● ROLL     │ TURN  +30°/с│  MAX 144 км/ч  │   ↗          │ REGION    ---│               │
│ │ ● YAW      │ PITCH +12°  │  ━━━━━━━━━━    │  ━━          │ CORRIDOR  ---│               │
│ │ ● THRUST   │ BANK    -5°  │  FUEL 45/100   │ 2 538 м     │              │               │
│ │            │              │  ◉ REFUEL+2.0/s│   Global    │              │               │
│ └────────────┴──────────────┴────────────────┴──────────────┴──────────────┘               │
│   240px          200px            180px           220px           200px   (auto-shrink)      │
└────────────────────────────────────────────────────────────────────────────────────────────┘
```

### 3.2 К1 — Modules (240px, flex-shrink:1)

**Заголовок:** `MODULES` (8px, opacity 0.6)

**Строки модулей (до 4 видимых, остальные `+N more`):**
- Кружок 8×8px (border-radius 4) + имя 9px + процент перегрева 7px
- 🟢 зелёный = установлен, пассивный
- 🟠 оранжевый = активный мезий выхлоп
- 🔴 красный = перегрев / cooldown
- ⚪ серый = установлен, не мезиевый

**Статус:** Реализован ✅. Ждёт настройки модулей в сцене для теста.

### 3.3 К2 — Flight (200px, flex-shrink:0)

**Заголовок:** `FLIGHT` (8px, opacity 0.6)

4 строки (13px каждая), каждая с label + value + center-zero bar:

| Строка | Данные | Диапазон | Ед. |
|---|---|---|---|
| LIFT | `ship.VerticalSpeed` | ±10 м/с | м/с |
| TURN | `ship.AngularVelocity.y * Rad2Deg` | ±180 °/с | °/с |
| PITCH | `ship.PitchAngleDegrees` | ±20° | ° |
| BANK | `ship.RollAngleDegrees` | ±90° | ° |

**Center-zero bar:** `flex-row`, 3px высоты, 3 сегмента — negFill (красный, −50..0%), center (2px, opacity 0.35), posFill (зелёный, 0..+50%).

### 3.4 К3 — Speed + Fuel (180px, flex-shrink:0)

**Центральная колонка — самая важная.**

```
SPEED          ← font 18, bold
 87 км/ч
━━━━━━━━━━     ← speed bar (3 уровня: зелёный < 50%, жёлтый < 80%, красный ≥ 80%)
MAX 144 км/ч   ← font 8, opacity 0.5
━━━━━━━━━━     ← fuel bar (3 уровня: зелёный > 40%, жёлтый > 20%, красный ≤ 20%)
FUEL 45/100    ← font 8
◉ REFUEL +2.0/s ← только при isRefueling (L зажата)
```

**Speed:** только forward component (W/S). `Mathf.Abs(ship.ForwardSpeedMps) * 3.6f`. E/Q не влияют.

**Fuel bar:** высота 3px, цвет по проценту.

**REFUEL indicator:** зелёная точка 6px + текст `REFUEL +{rate:F1}/s`. Показывается только когда `fs.isRefueling == true` (клавиша L зажата, корабль неподвижен).

### 3.5 К4 — Environment (220px, flex-shrink:1)

**Заголовок:** `ENV` (8px, opacity 0.6)

**WIND** (24px row):
- Левая: `12.4 м/с` (font 12) + стрелка направления (↑↗→↘↓)
- Правая: мини-компас 28×28px (Painter2D `generateVisualContent`): тёмный круг, красная стрелка от центра к краю по углу `SignedAngle(ship.forward, windDir, up)`

**ALT** (24px row):
- Левая: `2 538 м` (font 12) + имя коридора (font 9, `ActiveCorridor.displayName -> "---"`)
- Правая: вертикальный progress bar 6×22px: заполнение от `corridor.minAltitude` (дно) до `corridor.maxAltitude` (верх). Цвет по `CurrentAltitudeStatus`: Safe→зелёный, Warning→жёлтый, Danger→красный.

### 3.6 К5 — Dispatch (200px, flex-shrink:1)

**Заголовок:** `DISPATCH` (8px, opacity 0.6)

3 статичные строки-заглушки (font 9, opacity 0.4):
```
DISPATCHER  ---
REGION      ---
CORRIDOR    ---
```

**Ждёт подключения** систем регионов/диспетчера/связи.

---

## 4. Ключевые паттерны реализации

### 4.1 Runtime-constructed VisualElement

Как `ShipKeyToast.cs`: 0 UXML/USS, всё в C#.  
- `TryBuild()` — 5-step guards (`_doc == null → rootVisualElement == null → panelSettings == null`)
- Все `style.*` в коде (font, color, size, layout)

### 4.2 Показ/скрытие

```csharp
SetVisible(true):
  _root.style.display = DisplayStyle.Flex;
  _root.pickingMode = PickingMode.Position;  // пропускает клики сквозь HUD
SetVisible(false):
  _root.style.display = DisplayStyle.None;
  _root.pickingMode = PickingMode.Ignore;    // не блокирует мир
```

Курсор **не трогаем** — flight mode.

### 4.3 Компас (Painter2D)

```csharp
_windCompass.generateVisualContent += OnCompassGenerateContent;
// В callback:
ctx.painter2D → Arc (круг) → MoveTo/LineTo (стрелка) → Arc (центр)
```

Перерисовка (`MarkDirtyRepaint()`) только при изменении `_lastCompassAngle > 1°`.

### 4.4 Center-zero bar (Flight)

```csharp
// 3 сегмента в flex-row: negFill | center(2px) | posFill
// pct = clamp(value / range, -1, +1)
if (pct >= 0) posFill.width = pct * 50%, negFill.width = 0
else          negFill.width = |pct| * 50%, posFill.width = 0
```

---

## 5. Изменения в существующем коде

| Файл | Изменения |
|---|---|
| `ShipController.cs` | +10 public properties (S-HUD-01 + FuelSystem) |
| `ShipFuelSystem.cs` | +`ConsumptionRate`, `AtmosphericRefuelRate` (public для HUD) |
| `ShipController.cs:1171-1190` | `InitializeDebugHUD()` — больше не создаёт ShipDebugHUD при `_showLegacyMeziyHud == false` |

Все изменения — additive only.

---

## 6. Статус по подсистемам

| Компонент | Статус | Примечание |
|---|---|---|
| Speed (K3) | ✅ | Forward компонента, км/ч, 3-уровневый bar |
| Fuel + Refuel (K3) | ✅ | Bar + label + зелёная точка при L |
| Flight (K2) | ✅ | LIFT/TURN/PITCH/BANK center-zero bars |
| Modules (K1) | ✅ | Кружки-индикаторы, +N more. **Ждёт настройки модулей** |
| Wind (K4) | ✅ | Цифра + текстовая стрелка + Painter2D компас |
| Altitude (K4) | ✅ | Высота + имя коридора + вертикальный bar по AltitudeStatus |
| Dispatch (K5) | ✅ | 3 строки-заглушки. **Ждёт системы регионов** |
| UI Sound / Interaction | ❌ не требуется | HUD read-only |
| Multi-crew (Gunner/Engineer) | ⏭️ Phase 4+ | Фильтр по `PilotSeatType` |

---

## 7. Roadmap — следующий шаг

Приоритет на усмотрение пользователя:

1. **Настройка модулей в сцене** — чтобы протестировать К1 (MODULES) с живыми данными
2. **Система регионов / диспетчера** — чтобы наполнить К5 (DISPATCH)
3. **Косметика** — тюнинг цветов, размеров, анимация появления
4. **Рефакторинг** — вынести `ShipHudController` в отдельные классы колонок (при росте > 1000 LOC)

---

## 8. Файлы ассетов

```
Assets/_Project/Ship/
└── UI/Resources/UI/
    ├── ShipHudPanelSettings.asset     ← PanelSettings (themeUss, sortingOrder=50)
    ├── ShipHudPanel.uxml              ← stub (пустой, для YAML)
    └── (uxml не используется — runtime-constructed)

Assets/_Project/Scripts/Ship/
├── UI/
│   └── ShipHudController.cs          ← ~980 LOC
└── (другие компоненты — ShipModuleManager, MeziyModuleActivator, ShipFuelSystem — без изменений)

Assets/_Project/Scenes/BootstrapScene.unity
  → [ShipHudPanel] (UIDocument + ShipHudController)
```

---

## 9. Известные ограничения V1

- К1 (Modules) не тестировалась — в сцене нет настроенных модулей
- К5 (Dispatch) — заглушка, ждёт подсистем
- Fuel bar показывает общий запас без разбивки по типам (на будущее)
- Compass перерисовывается только при `∆ > 1°` — на неподвижном корабле не обновляется (но ветер не меняется на месте)
- HUD не масштабируется под разрешения экрана кроме авто-shrink (flex-shrink)
- Нет анимации появления/исчезновения (мгновенный toggle display)
- Нет поддержки multi-crew (все колонки показываются любому игроку в любом кресле)
