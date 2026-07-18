# Итерации реализации — Engine Visual System

## Итерация от 2026-07-21 (fix 5)

**Задача:** T-ENG02 / T-SHIP-SHAKE — NPC Fallback: визуалы кораблей (EngineThrusterVisual + ShipPartShake) работают на NPC-автопилоте
**Коммит:** `be85fba` — T-ENG02/T-SHIP-SHAKE fix 5: NPC Fallback

**Симптомы:**
- NPC-корабли (NpcShipController) летают по маршруту, но визуалы молчат:
  - Лопасти двигателей не вращаются
  - ShipPartShake не вибрирует
  - Отклонения двигателей по yaw нет
- Игрок, севший в NPC-корабль, видит анимации (ShipInputReader включается)
- Игрок в своём корабле видит анимации (клавиатурный ввод работает)

**Корневая причина:**
NPC-движение идёт через `NpcShipController.NavTick()` → прямые `rb.linearVelocity` / `rb.MoveRotation`, полностью минуя `ShipInputReader` и силовой конвейер `ShipController.FixedUpdate`. Fix 4 отключил `ShipInputReader` для кораблей без пилота (правильно). Но визуалы читали ТОЛЬКО из `ShipInputReader` → `_currentThrust = 0` для NPC.

**Исправление — NPC Fallback в визуальных скриптах:**

Когда `ShipInputReader` отключён (`!isActiveAndEnabled`), визуалы выводят thrust/yaw из `Rigidbody`:

| Скрипт | Что выводится | Из чего | Новое поле |
|---|---|---|---|
| `ShipPartShake` | `targetThrust` | `Clamp01(linearVelocity.magnitude / _maxReferenceSpeed)` | `_maxReferenceSpeed = 10 м/с` |
| `EngineThrusterVisual` | `thrustNorm` | Аналогично | `_maxReferenceSpeed = 10 м/с` |
| `EngineThrusterVisual` | `yawNorm` | `Clamp(angularVelocity.y / _maxRefYawRate)` | `_maxRefYawRate = 45°/с` |

**Архитектура источников ввода (defence in depth):**

```
Общий gate: _shipController.enabled && _shipController.IsEngineRunning
  ├── _inputReader.isActiveAndEnabled? → клавиатурный ввод (игрок за штурвалом)
  │     └── ShipInputReader.CurrentThrust / CurrentYaw (мгновенный)
  └── else → Rigidbody fallback (NPC-автопилот, нет пилота)
        ├── thrustNorm = speed / _maxReferenceSpeed
        └── yawNorm   = angularVelocity.y / _maxRefYawRate
```

**Изменённые файлы:**
- `Assets/_Project/Scripts/Ship/ShipPartShake.cs` — `_maxReferenceSpeed`, `_rbody`, fallback-логика
- `Assets/_Project/Scripts/Ship/Engine/EngineThrusterVisual.cs` — `_maxReferenceSpeed`, `_maxRefYawRate`, `_rbody`, fallback-логика
- `docs/Ships/customisation/SHIP_PART_SHAKE.md`
- `docs/Ships/customisation/ITERATIONS.md`

**Что НЕ сломано:**
- ✅ `ShipInputReader.Awake() → enabled = false` (fix 4) — без изменений
- ✅ `ShipInputReader.OnDisable()` сброс в 0 (fix 2) — без изменений
- ✅ `_shipController.enabled` gate (fix 3) — без изменений
- ✅ `IsEngineRunning` gate — без изменений
- ✅ Игрок за штурвалом — клавиатурный ввод приоритетнее Rigidbody (нет инерции)

**Проверки:**
- 0 ошибок компиляции ✅
- `ShipController.cs` без изменений ✅
- `ShipInputReader.cs` без изменений ✅
- `NpcShipController.cs` без изменений ✅

---

## Итерация от 2026-07-21 (fix 4)

**Задача:** T-SHIP-SHAKE — баг: визуалы кораблей трясутся в пешем режиме (без посадки)
**Коммит:** `5647ec0` — T-SHIP-SHAKE fix 4: ShipInputReader.Awake gate — визуалы кораблей не читают W/S без пилота

**Симптомы:**
- В пешем режиме (игрок не садился в корабль) нажатия W/S вызывали:
  - Дребезг `ShipPartShake` на **всех** кораблях в сцене
  - Вращение лопастей + отклонение `EngineThrusterVisual`
- Эффект проявлялся даже если игрок ни разу не нажимал F

**Корневая причина:**
1. `ShipInputReader.Update()` читает `Keyboard.current` напрямую, без проверки наличия пилота
2. Если `ShipInputReader.enabled = true` в префабе — **каждый** корабль на сцене опрашивает W/S с первого кадра
3. `ShipPartShake`/`EngineThrusterVisual` проверяют `_shipController.enabled` (true) и `IsEngineRunning` (true у NPC-кораблей через `NpcShipController.SetEngineRunning(true)`) → визуалы активируются
4. Предыдущий fix 3 (disembark) покрывал только случай **после** выхода из корабля — не покрывал корабли, в которые игрок **никогда** не садился

**Исправление:**
- `ShipInputReader.Awake()` → `enabled = false` (1 строка)
- `NetworkPlayer` и `PlayerStateMachine` включают компонент при посадке (как и раньше)

**Изменённые файлы:**
- `Assets/_Project/Scripts/Player/ShipInputReader.cs`
- `docs/Ships/customisation/SHIP_PART_SHAKE.md`
- `docs/Ships/customisation/ITERATIONS.md`

**Проверки:**
- 0 ошибок компиляции ✅

---

## Итерация от 2026-07-21 (fix 3)

**Задача:** T-ENG02 — баг: визуалы двигателя реагируют на WASD после выхода из корабля (F)
**Коммит:** `abfa9ff` — T-ENG02: фикс визуалов двигателя v2 — правильный путь disembark в NetworkPlayer

**Симптомы:**
- После выхода из корабля (F → пеший режим) нажатия WASD вызывали:
  - A/D → отклонение двигателя (yaw)
  - W/S → вращение лопастей + ShipPartShake
- Корабль физически оставался в воздухе (правильно), но визуалы продолжали реагировать

**Корневая причина:**
1. Реальный disembark идёт через `NetworkPlayer` (не `PlayerStateMachine`)
2. `NetworkPlayer` вызывает `RemovePilot()` → `RemovePilotRpc`, которая НЕ глушит `ShipController.enabled` (by design: idle/NPC должны работать)
3. `ShipInputReader.enabled` остаётся `true` → `Update()` продолжает опрашивать `Keyboard.current` (WASD)
4. `ShipInputReader._currentThrust`/`_currentYaw` обновляются от нажатий игрока в пешем режиме
5. `EngineThrusterVisual` и `ShipPartShake` видят `_shipController.enabled=true` (не отключён) → читают живой ввод → анимируются

**Исправление (3 уровня защиты):**
| Уровень | Файл | Что |
|---|---|---|
| 1 (root cause) | `NetworkPlayer.cs` | Disembark: `inputReader.enabled = false`; Board: `enabled = true` |
| 2 (stale state) | `ShipInputReader.cs` | `OnDisable()`: сброс `_currentThrust`/`_currentYaw`/etc в 0 |
| 3 (defence) | `EngineThrusterVisual.cs` / `ShipPartShake.cs` | Проверка `!_shipController.enabled` в `Update()` |
| 3 (legacy) | `PlayerStateMachine.cs` | Аналогично для офлайн/тестового режима |

**Изменённые файлы:**
- `Assets/_Project/Scripts/Player/NetworkPlayer.cs`
- `Assets/_Project/Scripts/Player/ShipInputReader.cs`
- `Assets/_Project/Scripts/Player/PlayerStateMachine.cs`
- `Assets/_Project/Scripts/Ship/Engine/EngineThrusterVisual.cs`
- `Assets/_Project/Scripts/Ship/ShipPartShake.cs`

**Проверки:**
- 0 ошибок компиляции ✅
- `ShipController.cs` без изменений ✅
- Два пути disembark покрыты (`NetworkPlayer` — основной, `PlayerStateMachine` — офлайн/тестовый) ✅

---

## Итерация от 2026-07-21

**Задача:** T-SHIP-SHAKE — визуальный дребезг частей корабля при тяге (W/S)
**Коммит:** `49621a8` — T-SHIP-SHAKE: ShipPartShake — визуальный дребезг частей корабля при тяге (W/S)
**Изменения:**
- `Assets/_Project/Scripts/Ship/ShipPartShake.cs` — новый компонент (дребезг визуала через AnimationCurve)
- `docs/Ships/customisation/SHIP_PART_SHAKE.md` — документация

**Что сделано:**
1. `ShipPartShake` — MonoBehaviour, вешается на любой визуал корабля
2. Читает `ShipInputReader.CurrentThrust` (abs) через `ShipRootReference`
3. AnimationCurve для настройки формы колебаний (по умолчанию синусоида)
4. Раздельные амплитуды позиции (Vector3) и вращения (Vector3, градусы)
5. Порог `_thrustThreshold` для фильтрации мёртвой зоны
6. `OnDisable()` сбрасывает трансформ к базовым значениям

**Проверки:**
- 0 ошибок компиляции ✅
- `ShipController.cs` без изменений ✅

---

## Итерация от 2026-07-21 (fix 1)

**Задача:** T-SHIP-SHAKE — баг: плоская кривая → нулевая интенсивность
**Коммит:** `8271a5a` — fix — плоская кривая по умолчанию заменена на синусоиду через EnsureSineCurve()

**Проблема:** `_shakeCurve` инициализировался как `AnimationCurve.EaseInOut(0,0,1,0)` — два keyframe на y=0. `intensity = thrustNorm × curveValue = 0` всегда.

**Решение:**
- Поле по умолчанию → `null`
- `EnsureSineCurve()` проверяет не только `length == 0`, но и амплитуду keyframe'ов: если все значения `|y| < 0.001` → замена на 5-keyframe синусоиду (±1)
- Вызывается в `Start()` (runtime), `OnValidate()` (editor reload), `Reset()` (component reset)

---

## Итерация от 2026-07-21 (fix 2)

**Задача:** T-SHIP-SHAKE — резкий старт/стоп дрожи при нажатии/отпускании W
**Коммит:** `dda0f7e` — сглаживание thrust через SmoothDamp (_smoothTime)

**Проблема:** `thrustNorm` менялся 0↔1 мгновенно → дрожь включалась/выключалась резко, кривая формы волны не помогала.

**Решение:**
- `Mathf.SmoothDamp(_smoothThrust, targetThrust, ref _smoothVelocity, _smoothTime)`
- Новое поле `_smoothTime` (по умолчанию 0.4 сек) — настраиваемая плавность атаки/затухания
- Интенсивность = `_smoothThrust × curveValue` (вместо сырого thrustNorm)

---

## Итерация от 2026-07-14

**Задача:** T-ENG02 — Engine Visual System (этапы 1-2 + настройка сцены)
**Коммит:** `6c6f50c` — T-ENG02: Engine Visual System — этапы 1-2 + настройка сцены
**Изменения:**
- `Assets/_Project/Scripts/Ship/ModuleSlot.cs` — добавлен `SlotType.Engine`
- `Assets/_Project/Scripts/Ship/ShipModule.cs` — добавлен `ModuleType.Engine`
- `Assets/_Project/Scripts/Ship/Engine/EngineThrusterVisual.cs` — новый компонент (вращение лопастей + отклонение по yaw)
- `Assets/_Project/Scripts/Ship/Engine/EngineThrusterVisual.cs.meta`
- `Assets/_Project/Scenes/World/WorldScene_0_0.unity` — ShipRootReference + Slot_Engine_Left/Right
- `docs/Ships/customisation/02_ENGINE_VISUAL_ANALYSIS_AND_PLAN.md` — статус обновлён

**Что сделано:**
1. Enum'ы `SlotType` и `ModuleType` синхронно расширены значением `Engine` (позиция 3)
2. `EngineThrusterVisual` — клиентский визуальный компонент:
   - Вращает `_propeller` пропорционально thrust (из `ShipInputReader.CurrentThrust`)
   - Отклоняет `transform.localRotation` по Y пропорционально yaw
   - Никаких RPC, никакой модификации Rigidbody
3. `ShipInputReader` уже имел `CurrentThrust`/`CurrentYaw` — этап 3 пропущен
4. `ShipRootReference` добавлен на `Ship_Light_root`
5. `Slot_Engine_Left` и `Slot_Engine_Right` созданы в `WorldScene_0_0`

**Что НЕ сделано (согласно плану):**
- `thrustNormalized` в `ShipTelemetryState` — отдельный тикет после MVP
- SO-модули двигателей — вручную дизайнером
- Multi-crew поддержка анимации — отдельная задача

**Проверки:**
- `BootstrapScene` не тронут ✅
- `ShipController.cs` без изменений ✅
- 0 ошибок компиляции ✅

---

## Итерация от 2026-07-14 (fix)

**Задача:** T-ENG02 — исправление: ShipInputReader + Slot_Engine_Left + постмортем
**Коммит:** `c00f766` — T-ENG02: фикс — ShipInputReader на корабль + Slot_Engine_Left + постмортем
**Изменения:**
- `Assets/_Project/Scenes/World/WorldScene_0_0.unity` — ShipInputReader добавлен, Slot_Engine_Left пересоздан
- `docs/Ships/customisation/T-ENG01_ShipEngineVisual_PostMortem.md` — корневая причина исправлена

**Причины неработоспособности визуала:**
1. `ShipInputReader` отсутствовал на `Ship_Light_root` — `EngineThrusterVisual._inputReader` был null
2. `Slot_Engine_Left` пропал (сцена не сохранилась в прошлый раз)
3. Постмортем T-ENG01 неверно указывал GlobalObjectIdHash как корневую причину — реальная: модуль с множителями 0

---

## Итерация от 2026-07-14 (финал)

**Задача:** T-ENG02 — финальная архитектура: _pivotPoint + _visuals
**Коммит:** `67d008a` — T-ENG02: _pivotPoint + _visuals — два независимых трансформа

**Финальная архитектура EngineThrusterVisual:**
- `_pivotPoint` (RotationAnchor) — пустой маркер, двигается мышкой, задаёт точку вращения
- `_visuals` (EngineVisuals) — контейнер Body + Blade, двигается мышкой, вращается кодом вокруг `_pivotPoint`
- Оба трансформа полностью независимы — дизайнер не трогает дочерние объекты

**Иерархия (Slot_Engine_Right):**
```
Slot_Engine_Right
├── RotationAnchor   ← _pivotPoint (пустой, точка вращения)
└── EngineVisuals    ← _visuals (Cylinder + Cube, вращается)
    ├── Cylinder     ← Body
    └── Cube         ← _propeller (лопасть)
```

**Эволюция pivot-решения (3 итерации):**
1. `_pivotTransform` (Transform) — неудобно: двигаешь = дети едут
2. `_pivotOffset` (Vector3) — неудобно: слепые числа
3. `_pivotPoint` + `_visuals` (два Transform) — ✅ удобно: оба двигаются мышкой независимо

**Все коммиты T-ENG02:**
| Коммит | Описание |
|---|---|
| `6c6f50c` | Этапы 1-2: enum'ы + EngineThrusterVisual |
| `4f2888b` | Документация итерации |
| `c00f766` | Fix: ShipInputReader + Slot_Engine_Left |
| `64df30b` | ITERATIONS.md fix |
| `8bea729` | Fix позиции Slot_Engine_Left |
| `b958c86` | Fix: Cube под Pivot |
| `a25c81a` | _pivotOffset + _visualRoot |
| `403c8b9` | _pivotTransform + _pivotOffset |
| `67d008a` | **Финальная: _pivotPoint + _visuals** |
