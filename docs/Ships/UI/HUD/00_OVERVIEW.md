# Ship HUD — Overview & Design Notes

**Сессия планирования:** 2026-06-17
**Статус:** Дизайн согласован, **roadmap к коду готов**. Код не пишем в этой сессии.
**Рабочий каталог документации:** `docs/Ships/UI/HUD/`
**Планируемый каталог кода (когда дойдёт до кодинга):** `Assets/_Project/Scripts/Ship/UI/`
**Планируемый каталог ассетов:** `Assets/_Project/Ship/UI/Resources/UI/`

---

## 1. Назначение

Замена `ShipDebugHUD` (IMGUI, F3) и `Mezi yStatusHUD_Legacy` (IMGUI, F4) — двух отдельных overlay-ов, которые:

1. Показываются всегда когда корабль существует (даже если игрок не пилот).
2. Не интегрированы с входом/выходом из `PilotSeatController`.
3. Используют IMGUI (запрещено для player-facing UI по правилам проекта).

**Новый HUD** — единый лёгкий UI Toolkit overlay, который:
- появляется **только когда `NetworkPlayer` сидит в `PilotSeatController`** (конкретно: `_inShip == true` И `_currentShip != null`);
- исчезает мгновенно при выходе;
- читает данные из `ShipController` (скорость, углы, angular velocity) + `ShipModuleManager` (модули) + `Mezi yModuleActivator` (состояние) + `WindManager` (ветер) + `AltitudeCorridorSystem` (коридор высот);
- НЕ блокирует ввод (`pickingMode = Ignore`), НЕ разблокирует курсор (это не модалка).

---

## 2. Анализ существующей системы (что уже есть)

### 2.1 Источники данных — финальная таблица

Все данные для HUD **уже есть** в существующих компонентах. НЕ создаём новых типов, не дублируем состояние.

| Данные | Источник | Доступ | Что делаем |
|---|---|---|---|
| Скорость (м/с) | `ShipController.CurrentSpeed` | `public` (`ShipController.cs:908`) | читаем |
| Макс. скорость (per-class) | `ShipController.maxSpeed` | `private` поле, `ApplyShipClass()` ставит | **S-HUD-01:** `public float MaxSpeed => maxSpeed;` |
| Тангаж ° | `ShipController.GetNormalizedPitch()` | `private` (`:696`) | **S-HUD-01:** `public float PitchAngleDegrees` |
| Крен ° | `ShipController.GetNormalizedRoll()` | `private` (`:706`) | **S-HUD-01:** `public float RollAngleDegrees` |
| Рыскание ° | `transform.eulerAngles.y` | нет | **S-HUD-01:** `public float YawAngleDegrees` |
| Vertical speed (м/с) | `ShipController._rb.linearVelocity.y` | `private _rb` | **S-HUD-01:** `public float VerticalSpeed` |
| Угловая скорость (Vector3, rad/s) | `ShipController._rb.angularVelocity` | `private _rb` | **S-HUD-01:** `public Vector3 AngularVelocity` (для компаса ветра) |
| Активный коридор | `ShipController._activeCorridor` | `private` (`:165`) | **S-HUD-01:** `public AltitudeCorridorData ActiveCorridor` |
| Altitude status | `ShipController._currentAltitudeStatus` | `private` (`:159`) | **S-HUD-01:** `public AltitudeStatus CurrentAltitudeStatus` |
| Статус модулей | `ShipController.ShipModuleManager.slots` | `public` (`ShipModuleManager.cs:16`) | читаем |
| Состояние мезиевых | `ShipController.Mezi yModuleActivator.GetState(id)` | `public` (`Mezi yModuleActivator.cs:305`) | читаем |
| Корневой transform | `ShipController.ShipRoot` | `public` (`:1285`) | читаем |
| Forward носа | `ShipController.transform.forward` | `public` Unity | читаем для компаса ветра |
| Класс корабля | `ShipController.ShipFlightClass` (enum) | `public` (`:12`) | читаем |
| **Ветер (направление, м/с)** | `WindManager.Instance.CurrentWindDirection / CurrentWindSpeed` | `public` singleton (`WindManager.cs:15-16`) | читаем; для компаса считаем `SignedAngle(ship.forward, windDir, up)` |
| **Активный коридор** | `AltitudeCorridorSystem.Instance.GetActiveCorridor(pos)` | `public` singleton (`AltitudeCorridorSystem.cs:86`) | читаем, в т.ч. `displayName`, `minAltitude`, `maxAltitude` |

**Изменения в `ShipController.cs` — additive only, ~10 строк в секции public properties:**
```csharp
// S-HUD-01. Expose for HUD (no logic — just getters over existing state)
public float MaxSpeed => maxSpeed;
public float PitchAngleDegrees { get { float a = transform.eulerAngles.x; if (a > 180f) a -= 360f; return a; } }
public float RollAngleDegrees  { get { float a = transform.eulerAngles.z; if (a > 180f) a -= 360f; return a; } }
public float YawAngleDegrees   => transform.eulerAngles.y;
public float VerticalSpeed     => _rb != null ? _rb.linearVelocity.y : 0f;
public Vector3 AngularVelocity => _rb != null ? _rb.angularVelocity : Vector3.zero;
public AltitudeCorridorData ActiveCorridor => _activeCorridor;
public AltitudeStatus CurrentAltitudeStatus => _currentAltitudeStatus;
```

### 2.2 Точка события «сел / вышел за штурвал»

Самое чистое место — подписка на **существующее** поле `NetworkPlayer._inShip` (public, `NetworkPlayer.cs:123`). Выставляется в `SubmitSwitchModeRpc` (строки 528/552), которое вызывается:
- локально при нажатии F;
- по сети `SendTo.Everyone` RPC → срабатывает у всех клиентов.

**Стратегия:** `ShipHudController` **сам** опрашивает `LocalPlayer.NetworkPlayer.IsInShip` каждый кадр в `Update()`. Дешевле, чем новый event, не требует рефакторинга существующего кода, нет race condition с RPC.

### 2.3 Multi-crew

Phase 1 — `PilotSeatController.PilotSeatType` имеет только `Pilot`. Phase 4+ появятся `Gunner/Engineer/Navigator`. Пока HUD показывается при `IsInShip == true` без фильтра по типу (потому что в Phase 1 другого типа нет). В Phase 4 — добавим условие `lp.CurrentShip.GetNearestPilotSeat(seatPosition).SeatType == PilotSeatType.Pilot`. Тривиальная правка.

### 2.4 Референсные UI-паттерны

| Паттерн | Пример | Подходит? |
|---|---|---|
| 5-step UXML+USS окно | `MarketWindow`, `CharacterWindow`, `DialogWindow`, `CraftingWindow` | **НЕТ** — модальные, с вводом, тяжёлые |
| **Runtime-constructed VisualElement** | `ShipKeyToast`, `QuestToast`, `GatheringToastController`, `MetaRequirementToast` | **ДА — основной референс** |
| IMGUI (legacy, удаляем) | `ShipDebugHUD`, `Mezi yStatusHUD_Legacy` | **НЕТ** |

**Решено:** runtime-constructed VisualElement (как `ShipKeyToast.cs`). 0 файлов `.uxml`/`.uss`, всё в коде.

### 2.5 Что НЕ трогаем

- ❌ `UIManager` (модальный panel-stack, не overlay) — наш HUD не регистрируем
- ❌ `MarketWindow`/`CharacterWindow`/etc.
- ❌ `ShipDebugHUD` / `Mezi yStatusHUD_Legacy` — **deprecated, но НЕ удаляем** в Phase 1 (`_showLegacyMeziyHud = false` уже отключает; см. `ShipController.cs:115`)
- ❌ `NetworkPlayer`, `PilotSeatController`, `Mezi yModuleActivator`, `ShipModuleManager`, `WindManager`, `AltitudeCorridorSystem` — **без изменений**, только читаем
- ❌ `BootstrapScene` менеджеры/спавнеры — без изменений
- ❌ Network-replication — HUD чисто client-side
- ❌ `.meta`/`.asmdef` (Unity сам)

---

## 3. Целевая архитектура

### 3.1 Компоненты

| Компонент | Тип | Где | Назначение |
|---|---|---|---|
| `[ShipHudPanel]` | GameObject | `BootstrapScene.unity` (root GO, `DontDestroyOnLoad`) | `UIDocument` + `ShipHudController` |
| `ShipHudController` | MonoBehaviour | `Assets/_Project/Scripts/Ship/UI/ShipHudController.cs` | `TryBuild()` root VE, опрос `LocalPlayer` каждый `Update`, переключение видимости |
| `ShipHudPanelSettings` | `PanelSettings.asset` | `Assets/_Project/Ship/UI/Resources/UI/ShipHudPanelSettings.asset` | `themeUss = UnityDefaultRuntimeTheme` (guid `1cad08e114acf014d94b2301632cffa9`), `sortingOrder = 50` (выше тостов, ниже диалогов) |

`ShipHudPanel` отдельный GO, не как `ShipKeyToast` (у того всё в одном), потому что HUD и Toast — разные по поведению: HUD персистирует сцены, Toast — нет.

### 3.2 Иерархия root VisualElement — **5 колонок**

Согласно ответу на вопрос 4: расширяем правую часть. Теперь **5 колонок**, не 4.

```
_root (VisualElement, pickingMode=Position когда виден / Ignore когда скрыт)
  position:Absolute; top:8px; left:0; right:0; height:96px;
  align-items:center; justify-content:center;     // горизонтальное центрирование

  └─ _centerRow (VisualElement, flex-direction:row, height:100%)
     ├─ _colModules       (VisualElement, width:240px)   // К1: модули
     ├─ _colFlight        (VisualElement, width:200px)   // К2: полёт (LIFT/TURN/PITCH/BANK)
     ├─ _colSpeed         (VisualElement, width:180px)   // К3: СКОРОСТЬ (центр)
     ├─ _colEnv           (VisualElement, width:220px)   // К4: WIND + ALTITUDE (компас + верт.бар)
     └─ _colDispatch      (VisualElement, width:200px)   // К5: DISPATCHER/REGION/CORRIDOR (заглушки)
```

**Ширины:** 240+200+180+220+200 = 1040px + margins ~16px → ~1060px. На FullHD 1920 это 55% — ок. На 1280×720 это 83% — **плотно**, но auto-shrink (см. ниже) сожмёт колонки пропорционально.

**Auto-shrink (вопрос 5):** каждая колонка `flex-shrink: 1`. На узких экранах колонки сожмутся, цифры и бары останутся видны. Критические (centerSpeed, leftFlight) — `flex-shrink: 0`, не сжимаем никогда. Внешние (`_colModules`, `_colDispatch`) — `flex-shrink: 1`.

**Высота:** 96px (8% от 1080p). Колонки выровнены по `align-items: stretch` (default в flex-row) — каждая колонка 100% высоты row, внутри — свой вертикальный layout.

### 3.3 Layout колонок (детально)

#### К1: `_colModules` — модули (240px)

Источник: `ship.ShipModuleManager.slots`.

Заголовок «MODULES» (10px uppercase opacity 0.6).

Для каждого слота `slot.isOccupied`:
- **Кружок-индикатор** 14×14 px: `border-radius:7`, цвет зависит от состояния через `Mezi yModuleActivator.GetState(slot.installedModule.moduleId)`:
  - зелёный (`rgb(80,200,120)`) — `state.isPassive && !state.isOnCooldown`
  - оранжевый (`rgb(240,160,60)`) — `state.isActive` (мезий выхлоп активен)
  - красный (`rgb(220,80,80)`) — `state.isOnCooldown` (перегрев)
  - серый (`rgb(120,120,120)`) — модуль установлен, но не мезиевый / нет активатора
- **Короткое имя** (Label) — последний токен `moduleId` после split по `_`. Пример: `MODULE_MEZIY_PITCH` → `PITCH`, `MODULE_ROLL` → `ROLL`, `MODULE_LIFT` → `LIFT`. Если в ID нет `_` — показать весь ID.
- **Мелкий процент** (Label 9px) — `GetOverheatProgress * 100:F0` справа, только если `> 0`.

**Кол-во строк:** все занятые слоты, **без лимита** (вопрос 6). Если высота контейнера превышена — обрезаем снизу, показываем «+N more» (расчёт: `slots.Count - visibleCount`). При типичном наборе 4-6 модулей высота 24px × 6 = 144px — не влезает в 96px-окно, поэтому **scrolling не нужен** — обрезаем. Допустимо: 3-4 модуля видны всегда (заголовок 14px + 3 × 24px = 86px), остальные обрезаны.

#### К2: `_colFlight` — полёт (200px, `flex-shrink: 0`)

4 строки по 20px (header 12px + 4 × 20 = 92px):

1. **LIFT** — вертикальная скорость `ship.VerticalSpeed` (м/с). Center-zero bar ±10 м/с.
2. **TURN** — угловая скорость `ship.AngularVelocity.y` (rad/s → deg/s). Center-zero bar ±180 °/s.
3. **PITCH** — `ship.PitchAngleDegrees`. Center-zero bar ±20° (maxPitchAngle, не ±90 — потому что физически больше не бывает).
4. **BANK** — `ship.RollAngleDegrees`. Center-zero bar ±90° (rigidbody может вращаться свободно, стабилизация тяготеет к 0).

Заголовок «FLIGHT» (10px uppercase opacity 0.6).

**Center-zero bar pattern** (вопрос 1, ответ B): как `rep-bar` в CharacterWindow — `flex-direction:row`, высота 6px, контейнер 100% ширины колонки, 3 сегмента: `negFill` (left of center, 0-50%), `center` (2px, opacity 0.5, всегда посередине), `posFill` (50-100%, 0%). Источник шаблона: `project-c-ui-as-tab` §"Center-zero bar pattern".

#### К3: `_colSpeed` — скорость (180px, `flex-shrink: 0`)

- Большая цифра: `SPEED 24.3 м/с` (font-size 26px, bold, центрировано).
- Под ней progress bar 100% ширины колонки, высота 6px.
- Заполнение: `Mathf.Clamp01(ship.CurrentSpeed / ship.MaxSpeed)`.
- Цвет заполнения (3 диапазона):
  - `< 0.5` → зелёный `rgb(80,200,120)`
  - `< 0.8` → жёлтый `rgb(240,200,80)`
  - `≥ 0.8` → красный `rgb(220,80,80)`
- Под bar-ом: `MAX 40 м/с` (10px, opacity 0.5).

**Никаких других данных** в центре (по требованию «посередине — только скорость»).

#### К4: `_colEnv` — окружающая среда (220px)

Источник: `WindManager.Instance` + `AltitudeCorridorSystem.Instance` + `ship.transform.position.y`.

Заголовок «ENV» (10px uppercase opacity 0.6).

Две строки, 40px каждая:

1. **WIND** (40px высота):
   - Левая половина (50%): `12.4 м/с` (Label, 14px) + мелкое `← ↗ →` направление компаса (8px).
   - Правая половина (50%): **мини-компас** — круг 28×28 px, внутри стрелка-угол. Стрелка = ветер, ориентирована по `SignedAngle(ship.forward, windDir, Vector3.up)`. Красная стрелка, чёрный фон, 2px border.

2. **ALT** (40px высота):
   - Левая половина: высота в метрах `2 538 м` (Label, 14px) + имя активного коридора мелким `Global` / `Primus` (8px).
   - Правая половина: **вертикальный progress bar** 6px × 100% высоты строки. Заполнение сверху вниз: позиция между `corridor.minAltitude` (дно) и `corridor.maxAltitude` (верх). Цвет заливки зависит от `ship.CurrentAltitudeStatus`:
     - `Safe` → зелёный
     - `WarningLower/Upper` → жёлтый
     - `DangerLower/Upper` → красный
   - Деления на баре (опционально, фаза 2): тонкие горизонтальные риски на `minAltitude` и `maxAltitude`.

**Компас рендерится в C#:** `generateVisualContent` callback на `VisualElement` (стандартный UI Toolkit подход для custom-рисования, ~20 LOC). Источник паттерна: Unity docs по `generateVisualContent`.

#### К5: `_colDispatch` — диспетчерская (200px, `flex-shrink: 1`)

Источник: 3 строки заглушки (per требование «заглушка, не трогаем правую часть»).

Заголовок «DISPATCH» (10px uppercase opacity 0.6).

3 строки по 20px:
1. `DISPATCHER  ---` (10px label + 11px value opacity 0.5)
2. `REGION      ---`
3. `CORRIDOR    ---`

**Заглушки явные**, чтобы игрок видел «тут скоро будет». Когда подсистема появится — заменим `---` на живые значения:
- `DISPATCHER`: имя ближайшего NPC-диспетчера (пока нет — `---`)
- `REGION`: имя региона мира (тоже `---` — нет системы регионов)
- `CORRIDOR`: дублирует К4 ALT — `displayName` активного коридора

**Зачем дублирование CORRIDOR с К4:** К4 показывает «где я по вертикали» (бар), а К5 — «какой это коридор» (имя). Полезно для игрока, не дублирование данных, а разный slice.

### 3.4 Цикл обновления

```csharp
// ShipHudController.Update() каждый кадр
var lp = LocalPlayer;  // кешируем после нахождения
if (lp == null) { SetVisible(false); return; }

bool shouldShow = lp.IsInShip && lp.CurrentShip != null;
if (shouldShow != _wasShown) SetVisible(shouldShow);
if (!shouldShow) return;

Refresh(lp.CurrentShip);

// Refresh — чистая перерисовка данных в уже построенном дереве
private void Refresh(ShipController ship)
{
    UpdateSpeedColumn(ship.CurrentSpeed, ship.MaxSpeed);
    UpdateFlightColumn(ship.VerticalSpeed, ship.AngularVelocity, ship.PitchAngleDegrees, ship.RollAngleDegrees);
    UpdateModulesColumn(ship.ShipModuleManager, ship.Mezi yModuleActivator);
    UpdateEnvColumn(ship.transform.position, ship.ActiveCorridor, ship.CurrentAltitudeStatus, ship.transform.forward);
    // _colDispatch не обновляется — статичные "---"
}
```

**Частота:** `Update()` каждый кадр. ~30 изменений `style.width` + 1 compass repaint (`MarkDirtyRepaint()`) + 4-6 Label text replacements. Дёшево, throttling не нужен.

### 3.5 Показ / скрытие

```csharp
private void SetVisible(bool visible)
{
    _root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    _root.pickingMode = visible ? PickingMode.Position : PickingMode.Ignore;
    _wasShown = visible;
}
```

**Курсор не трогаем** — flight mode, курсор залочен и спрятан. HUD не вызывает `Cursor.lockState = None` (отличие от `MarketWindow.Show`).

### 3.6 Scene placement

В `BootstrapScene.unity` создаём root GameObject `[ShipHudPanel]`:
- `UIDocument` (sortingOrder = 50, panelSettings = ShipHudPanelSettings, visualTreeAsset = null)
- `ShipHudController` (component)

`ShipHudController.Awake()`:
1. Кеш `_doc = GetComponent<UIDocument>();`
2. Если `_doc.panelSettings == null` → `Resources.Load<PanelSettings>("UI/ShipHudPanelSettings")` (auto-fallback как у `ShipKeyToast`)
3. `DontDestroyOnLoad(gameObject)` (только если root)
4. `_built = false`

`ShipHudController.Update()`:
- Если `!_built` → `TryBuild()` (5-step guards по `project-c-ui-toolkit-runtime` §0)
- Опрос `LocalPlayer`, переключение видимости, refresh

`TryBuild()`:
- `if (_doc == null) return;`
- `if (_doc.rootVisualElement == null) return;`
- `if (_doc.panelSettings == null) return;`
- Только после всех guards: построение дерева 5 колонок.

### 3.7 Поиск LocalPlayer

`NetworkManager.Singleton.SpawnManager.PlayerObjects` — `IReadOnlyList<NetworkObject>`. Фильтруем по `IsOwner` (только наш игрок). Кешируем после первого нахождения. Race-handling: если `SpawnManager == null` или нет нашего player — `null` → HUD скрыт.

---

## 4. Полная схема (ASCII)

```
┌─────────────────────────────────────────────────────────────────────────────────────┐
│  top:8px                                                                             │
│ ┌────────────┬────────────┬──────────┬────────────┬────────────┐                  │
│ │ MODULES    │ FLIGHT     │  SPEED   │  ENV       │ DISPATCH   │                  │
│ │ ━●━ PITCH  │ LIFT ━━│━━ 1.2    │ 24.3 м/с   │ ◐ 12 м/с  │ DISPATCHER --- │   │
│ │ ━●━ ROLL   │ TURN ━│━━━ 30°/s │ ━━━━━━━━━━ │   ←       │ REGION     --- │   │
│ │ ━●━ YAW    │ PITCH━━│━━ 12°    │ MAX 40 м/с │           │ CORRIDOR   --- │   │
│ │ ━●━ THRUST │ BANK━━━│━━ -5°    │            │ ━━━━━━━   │                  │   │
│ │ ━●━ LIFT   │           │          │ ALT 2 538м │                  │   │
│ │            │           │          │   Global   │                  │   │
│ │ +2 more    │           │          │ ━━━━━━━   │                  │   │
│ └────────────┴────────────┴──────────┴────────────┴────────────┘                  │
└─────────────────────────────────────────────────────────────────────────────────────┘
   240px         200px        180px        220px         200px   (auto-shrink)
```

---

## 5. Roadmap к коду (готов к старту)

> Все тикеты additive only. Никаких рефакторингов существующего кода, кроме S-HUD-01 (открытие 5 properties).

### Фаза 0: Подготовка (5 мин)

| # | Тикет | Что | Объём | Verify |
|---|---|---|---|---|
| S-HUD-00a | Создать каталог | `Assets/_Project/Scripts/Ship/UI/` + `Assets/_Project/Ship/UI/Resources/UI/` | 0 строк кода | `ls -la` оба каталога существуют |
| S-HUD-00b | Создать `.asmdef`? | **НЕТ** (per AGENTS.md, `Assembly-CSharp` lives there) | — | — |

### Фаза 1: Expose данные (5 мин)

| # | Тикет | Что | Объём | Verify |
|---|---|---|---|---|
| **S-HUD-01** | `ShipController.cs` | Добавить 5 public properties: `MaxSpeed`, `PitchAngleDegrees`, `RollAngleDegrees`, `YawAngleDegrees`, `VerticalSpeed`, `AngularVelocity`, `ActiveCorridor`, `CurrentAltitudeStatus` (см. §2.1) | 10 строк | `refresh_unity` (mode=force, compile=request) → `read_console` (errors=[]) → 0 errors |

### Фаза 2: Ассеты (5 мин)

| # | Тикет | Что | Объём | Verify |
|---|---|---|---|---|
| **S-HUD-02** | `ShipHudPanelSettings.asset` | Создать PanelSettings asset в `Assets/_Project/Ship/UI/Resources/UI/`. Назначить `themeUss = UnityDefaultRuntimeTheme` (guid `1cad08e114acf014d94b2301632cffa9`), `sortingOrder = 50` | 1 .asset | Открыть в Inspector — themeUss не null, sortingOrder=50 |

### Фаза 3: Контроллер (~1 сессия, 1 PR)

| # | Тикет | Что | Объём | Verify |
|---|---|---|---|---|
| **S-HUD-03a** | `ShipHudController.cs` — скелет | Awake/OnEnable, TryBuild (5-step guards), SetVisible, FindLocalPlayer, _wasShown флаг. БЕЗ содержимого колонок. | ~80 LOC | `refresh_unity` → `read_console` 0 errors. Play Mode → HUD создаётся (но пустой) |
| **S-HUD-03b** | `ShipHudController.cs` — К3 (Speed, центр) | Построить `_colSpeed` (180px), большую цифру, bar с 3-уровневой раскраской, MAX текст. Refresh в Update. | ~40 LOC | Play Mode → сел в корабль → видна цифра SPEED, bar растёт с ускорением |
| **S-HUD-03c** | `ShipHudController.cs` — К2 (Flight) | Построить `_colFlight` (200px), 4 строки LIFT/TURN/PITCH/BANK + center-zero bars. Refresh. | ~80 LOC | Play Mode → видны 4 строки, цифры обновляются при манёврах, bar-ы двигаются |
| **S-HUD-03d** | `ShipHudController.cs` — К1 (Modules) | Построить `_colModules` (240px), кружки-индикаторы, имена модулей, +N more. Refresh. | ~60 LOC | Play Mode → видны установленные модули, кружки меняют цвет (при активации мезиевых видно оранжевый) |
| **S-HUD-03e** | `ShipHudController.cs` — К4 (Env: Wind + Altitude) | Построить `_colEnv` (220px), WIND строка (цифра + мини-компас через `generateVisualContent`), ALT строка (высота + имя коридора + вертикальный bar с раскраской по AltitudeStatus). | ~100 LOC | Play Mode → видна высота, меняется при подъёме; при Warning → bar жёлтый; ветер показывает стрелку |
| **S-HUD-03f** | `ShipHudController.cs` — К5 (Dispatch placeholders) | Построить `_colDispatch` (200px), 3 статичные строки «---». | ~20 LOC | Play Mode → видны 3 прочерка |

**Порядок внутри S-HUD-03:** a → b → c → d → e → f. Каждый шаг = `refresh_unity` + `read_console` 0 errors → визуальная проверка в Play Mode (пользователь запускает и подтверждает «видно»).

### Фаза 4: Scene placement (10 мин)

| # | Тикет | Что | Объём | Verify |
|---|---|---|---|---|
| **S-HUD-04** | `BootstrapScene.unity` | Через MCP `manage_gameobject` создать `[ShipHudPanel]` root GO. Через `manage_component` добавить `UIDocument` (sortingOrder=50, panelSettings=ShipHudPanelSettings) + `ShipHudController`. Save scene. | 4 MCP-команды | Play Mode → HUD появляется при входе в PilotSeat, исчезает при выходе |

### Фаза 5: Cleanup (5 мин)

| # | Тикет | Что | Объём | Verify |
|---|---|---|---|---|
| **S-HUD-05** | `ShipController.cs` | Оставить `_showLegacyMeziyHud = false` по умолчанию (уже). Снять legacy `ShipDebugHUD` + `Mezi yStatusHUD_Legacy` с корабля в `WorldScene_0_0` (MCP `manage_component` action=remove). | 2 MCP-команды | Play Mode → F3/F4 ничего не показывают (legacy off), новый HUD работает |

### Фаза 6: Verify (10 мин, ты делаешь)

| # | Тикет | Что | Verify |
|---|---|---|---|
| **S-HUD-06** | Manual | В Play Mode: войти в PilotSeat → HUD появился. Видны 5 колонок. Цифры обновляются. Выйти → HUD исчез мгновенно. F3/F4 legacy не мешают. | Скриншот или подтверждение «хорошо» |

### Общий объём

- **Кода:** ~390 LOC (`ShipHudController.cs`)
- **Изменений в существующем:** 10 строк в `ShipController.cs` (additive)
- **Ассетов:** 1 (`ShipHudPanelSettings.asset`)
- **Scene work:** 1 GO + 1 component + save
- **Сессий:** 1-2 (S-HUD-01..02 за одну, S-HUD-03a..f + 04..06 за вторую)

---

## 6. Сводка по рискам

| Риск | Митигация |
|---|---|
| `ShipController` — public properties могут сломать что-то | Additive only, никаких переименований. 1 цикл `refresh_unity` после. |
| `LocalPlayer` ещё не заспавнен при `Awake` | `Update` каждый кадр, race-handling в `FindLocalPlayer` |
| PanelSettings не загружена → «thin strip» | 5-step guards в `TryBuild` + `Resources.Load` fallback в `Awake` (как `ShipKeyToast`) |
| Auto-shrink ломает label-ы | Использовать `min-width: 60px` на колонках с цифрами (`_colSpeed`, `_colFlight`) — `flex-shrink: 0` для критических |
| Compass `generateVisualContent` производительность | `MarkDirtyRepaint()` только при изменении `_lastCompassAngle > 1°` |
| `OnDestroy` не вызывается на HUD при scene reload | `DontDestroyOnLoad` на root — переживает сцены |
| F3/F4 legacy IMGUI всё ещё рисуется | `_showLegacyMeziyHud = false` уже в `ShipController.cs:115`; `ShipDebugHUD._visible = false` по умолчанию (`ShipDebugHUD.cs:17`) — двойная страховка, в S-HUD-05 снимем компоненты |
| WindManager.Instance == null в сцене без WindManager | `if (WindManager.Instance == null) { /* показать "---" */ }` в Refresh, без краша |
| AltitudeCorridorSystem.Instance == null | Аналогично, fallback `ship.transform.position.y` как «сырая высота», `Safe` статус |

---

## 7. Что в этой сессии (планирование) СДЕЛАНО

✅ Прочитал `docs/Ships/00_COMPOSITE_SHIP_SUMMARY.md`
✅ Прочитал `docs/Ships/analysis-composite-ship.md`
✅ Прочитал `docs/Ships/roadmap-integration.md`
✅ Прочитал `PilotSeatController.cs`, `Mezi yStatusHUD_Legacy.cs`, `ShipDebugHUD.cs`
✅ Прочитал `ShipController.cs` (1325 строк) — нашёл что expose нужно
✅ Прочитал `NetworkPlayer.cs` (1395 строк) — `IsInShip` уже public
✅ Прочитал `Mezi yModuleActivator.cs`, `ShipModuleManager.cs`
✅ Прочитал `UIManager.cs` — отвергнут (panel-stack)
✅ Прочитал `WindManager.cs` — singleton, `CurrentWindDirection/Speed` public
✅ Прочитал `AltitudeCorridorSystem.cs` + `AltitudeCorridorData.cs` — singleton, `GetActiveCorridor`/`GetStatus` public
✅ Загрузил skills: `project-c-bootstrap`, `project-c-ui-as-tab`, `project-c-ui-toolkit-runtime`
✅ Зафиксировал 7 ответов пользователя (включая ключевой — вопрос 4 → 5 колонок вместо 4)
✅ Создал `docs/Ships/UI/HUD/00_OVERVIEW.md`

## 8. Что в этой сессии НЕ СДЕЛАНО

❌ Никакого кода
❌ Никаких правок `ShipController.cs` (S-HUD-01 — следующая сессия)
❌ Никаких сцен-операций в Unity Editor
❌ Никакого git commit

---

## 9. Старт кодинга (по твоей команде)

Рекомендуемый порядок сессий:

**Сессия A (быстрая, 15 мин):**
1. S-HUD-00a (mkdir)
2. S-HUD-01 (`ShipController.cs` +5 properties → `refresh_unity` → `read_console` 0 errors)
3. S-HUD-02 (PanelSettings asset)
4. S-HUD-03a (скелет контроллера, 80 LOC, без колонок)
→ `refresh_unity` + `read_console` 0 errors → ты проверяешь «HUD создаётся при входе в PilotSeat» (но пустой)

**Сессия B (основная, ~1.5 часа):**
5. S-HUD-03b (Speed)
6. S-HUD-03c (Flight)
7. S-HUD-03d (Modules)
8. S-HUD-03e (Env: Wind + Altitude)
9. S-HUD-03f (Dispatch placeholders)
10. S-HUD-04 (scene placement)
11. S-HUD-05 (legacy cleanup)
12. S-HUD-06 (verify в Play Mode)
→ ты говоришь «хорошо, можно отчитаться» → коммитишь сам

**Или одной сессией** (если ритм позволяет): все 12 тикетов за раз.

Жду команду «погнали S-HUD-01» или сначала обсудить roadmap / риски / §6.
