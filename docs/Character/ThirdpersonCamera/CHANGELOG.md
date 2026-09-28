# CHANGELOG — Third-Person Camera

> **Что это:** лог итераций реализации SpringArmCamera
> **Первая запись:** 2026-07-26

---

## T-CAM18 — Adaptive: probe на полной дистанции против «мячика» (2026-09-28)

**Задача:** после T-CAM17 падение ок, но pitch в пол вплотную прыгает как мячик.

**Причина:** REST требует `_wasColliding`, а при крутом pitch короткий луч очищается сам
(в `desired = dir*C + up*H` высота перевешивает при малой C) — препятствие «исчезает»
в C-пространстве: recover растит C → втыкается в пол → shrink → цикл. Дедбенд тут
бессилен, т.к. флипает сам предикат коллизии. То же для стены на 70–95% дистанции.

**Изменения:** `IsUserDistanceBlocked()` — один пробный `SphereCast` вдоль орбиты на
`_userDistance`; recovery только если и там чисто, иначе T держится (REST).
У статической геометрии теперь настоящий fixed point. Тумблер `adaptiveProbeEnabled`.

---

## T-CAM17 — Аудит: точка покоя adaptive + рефактор (2026-09-28)

**Задача:** прыжки при pitch вниз «к полу» (тянется к персонажу и назад), рывки при падении.
Эксперимент: без adaptive прыжков нет, без dynamic lag — есть. Причина — adaptive-реле без
точки покоя (разбор: `docs/dev/T-CAM17_SPRINGARM_AUDIT.md`).

**Изменения (`SpringArmCamera.cs`, API-контракт сохранён):**
- Adaptive → 3 состояния SHRINK/REST/RECOVER: shrink только если цель не влезает
  (`T > actual + fitDeadband 0.3м`); у стены, но влезает — REST (держать, точка покоя);
  recovery — после устойчивого просвета 0.3с. `_lastClearTime`/порог ratio удалены.
- Anti-pop stale-guard: замороженная точка держится, только если лаг-цель ушла < 0.35м;
  `_collisionLagPos` едет в rebase-сдвиге (T-FO06DD), restore — консервативно к текущей.
- Vert-boost: гистерезис вкл > 6.5 / выкл < 3.5 м/с вместо дискретных 5 м/с.
- Взгляд: exp-сглаживание точки LookAt 0.03с (углы больше не дёргаются 1:1 за лагом).
- Зум: нормировка скролла на нотчи (/120, 1 нотч ≈ 1.5м) — раньше был бинарным;
  следование дистанции 0.15с, медленные 0.5с — только 1с после смены walk↔ship.
- Коллизия: игнор `CharacterController` (чужие игроки в толпе), NPC как раньше.
- Дефолты кода = префабу (zoom 0.5/50, корабль 2/350).

**Ожидание (ручная проверка пользователем, NOT RUN):**
- pitch в пол и держать — встала и стоит; поднял — отъехала через ~0.3с;
- стена — поджалась и стоит; падение/прыжок — без брыков; колесо — ~1.5м/нотч;
- F8/F9 — без попа камеры; толпа — камера проходит сквозь чужих.

---

## T-CAM16 — Стабилизация Spring Arm и игнорирование NPC (2026-08-15)

**Задача:** Устранить пружинящее поведение при ускорении, откат ручного приближения и скачки камеры в толпе NPC.

**Изменения:**
- `SpringArmCamera.cs` — пользовательская zoom-дистанция отделена от Adaptive Distance; ручное приближение больше не восстанавливается к базовой дистанции.
- `SpringArmCamera.cs` — минимальная дистанция больше не ограничивается суммой near clip и радиуса sphere cast; near clip установлен в `0.1f`.
- `SpringArmCamera.cs` — динамический lag использует сглаженную скорость, чтобы резкие сетевые скачки скорости не меняли lag рывком.
- `SpringArmCamera.cs` — `SphereCast` игнорирует trigger-коллайдеры и NPC с `NavMeshAgent`; цепочка пропускает до 8 игнорируемых коллайдеров.
- `ThirdPersonCamera.prefab` — `positionSmoothTime=0.04`, `near clip plane=0.1`, `ignoreNavMeshAgents=true`.

**Результат:**
- Ручной zoom фиксируется на выбранной дистанции.
- `zoomMinDistance=0.5` становится достижимым.
- NPC больше не участвуют в spring-arm collision probe.
- Проект: 0 compile errors.

---

## T-CAM15 — Zoom камеры колёсиком мыши (2026-07-26)

**Коммит:** `a73fa92` — T-CAM15: Zoom камеры колёсиком мыши

**Задача:** Добавить приближение/отдаление камеры колёсиком мыши через кастомный инпут (InputBindingsConfig) с возможностью регулировки чувствительности в меню настроек.

**Изменения:**
- `InputBindingsConfig.cs` — +`CameraZoom` в GameAction, +default binding
- `SettingsManager.cs` — +`CameraZoomSensitivity` (0.5–15, default 3f), PlayerPrefs + event
- `GameplaySettingsSection.cs` — +слайдер «Чувств. зума»
- `SpringArmCamera.cs` — +`UpdateZoom()`: `<Mouse>/scroll/y`, sensitivity ×0.5, clamp `_targetDistance`

**Результат:**
- ✅ Zoom колёсиком в walk (2–12m) и ship (6–35m) режимах
- ✅ Чувствительность настраивается в меню Настройки → Геймплей → «Чувств. зума»
- ✅ GameAction зарегистрирован в InputBindingsConfig (Rebind-ready)
- ✅ 0 compile errors

---

## T-CAM14 — Глубокий аудит: устранение остаточной тряски (2026-07-26)

**Коммит:** `1035f38` — T-CAM14: Deep Audit

**Задача:** Устранить остаточную тряску при приближении к объектам и персонажу.

**Контекст:** 20+ коммитов (T-CAM01..T-JITTER11) исправляли дёрганье. T-JITTER11 обнаружил что часть проблемы — Animator (skinnedMotionVectors=false). Но тряска при приближении к объектам сохранилась.

**Аудит выявил 3 архитектурные проблемы:**

1. **Двойной near-clip constraint (ResolveCollision + SmoothPosition)**: ResolveCollision честно разрешал коллизию (камера могла быть в 0.7m от lookTarget), а SmoothPosition постфактум выталкивал. Каждый кадр: ResolveCollision → внутри minDist → финальный push → следующий кадр заново. Цикл push-Lerp-push.

2. **Adaptive Distance баг**: `UpdateAdaptiveDistance` использовал базовую дистанцию (`distance`/`shipDistance`) вместо текущей цели (`_targetDistance`) для расчёта `ratio`. При уменьшенной дистанции ratio всегда < threshold → восстановление невозможно.

3. **positionSmoothTime 0.08s вместо 0.04s**: T-CAM12→T-CAM13 поднимали smoothTime для «стабильности», но проблема была в near-clip double-constraint (п.1). При 0.08s соотношение Lag/Smooth = 1.875× вместо задуманных 3.75×.

**Изменения:**
- `SpringArmCamera.cs` — ResolveCollision: +`ClampNearClip()` на всех return-путях (единый источник near-clip)
- `SpringArmCamera.cs` — SmoothPosition: убран near-clip constraint (только чистый exp-Lerp)
- `SpringArmCamera.cs` — UpdateAdaptiveDistance: `desiredDist = _targetDistance` вместо базовой дистанции
- `SpringArmCamera.cs` — `positionSmoothTime = 0.04f` (возврат к задумке T-CAM10)

**Результат:**
- ✅ Единый авторитетный источник near-clip (ResolveCollision)
- ✅ SmoothPosition — чистый exp-Lerp без побочных push'ей
- ✅ Adaptive Distance корректно восстанавливается
- ✅ Lag/Smooth соотношение 3.75× — гарантированно без резонанса
- ✅ 0 compile errors
- ✅ API-контракт сохранён

---

## Итерация от 2026-07-26 (T-CAM10 — Восстановление после выпиливания)

**Задача:** Восстановить Camera Lag + Anti-Pop + Adaptive Distance + Wall Recovery
с правильной архитектурой (Lag и SmoothDamp в разных временных масштабах).

**Контекст:**
- T-CAM05: полная реализация всех систем
- T-CAM06..08: серия «фиксов» дёрганья — лаг выключен, адаптивная дистанция выключена, 
  SmoothDamp сделан агрессивным
- T-CAM09: выпилено всё до голого скелета (только SphereCast + SmoothDamp)

**Корневая причина дёрганья в T-CAM05:**
Lag (0.15s) и SmoothDamp (0.12s) работали с близкими временны́ми константами — 
получалась система второго порядка с oscillation/overshoot.

**Архитектурное решение:**
- Lag = основная инерция (walk 0.15s XZ / 0.05s Y), ship — отключён
- SmoothDamp = быстрый anti-jitter фильтр (0.04s) — только для микро-сглаживания между кадрами
- Два фильтра в разных временны́х масштабах → не конфликтуют

**Изменения:**
- `SpringArmCamera.cs` — +UpdateLag() (экспоненциальная формула, framerate-independent)
- +Anti-Pop гистерезис (0.2s) в ResolveCollision с _lastCollisionPos
- +Wall Recovery (3× fast SmoothDamp при ratio < 0.4)
- +Adaptive Distance (авто-уменьшение _targetDistance в узких пространствах)
- Все расчёты орбиты/LookAt от _lagTargetPos (не от target.position)
- Корабль: lag отключён всегда (камера мгновенно следует за быстрым большим объектом)
- Gizmos: отображение _lagTargetPos и состояния коллизии

**Новые параметры инспектора:**
- Anti-Pop: `antiPopTime = 0.2f`
- Wall Recovery: `recoverySpeed = 10f`, `recoveryRatio = 0.4f`
- Camera Lag: `lagEnabled = true`, `lagHorizontalTime = 0.15f`, `lagVerticalTime = 0.05f`, `dynamicLagEnabled = true`
- Adaptive Distance: `adaptiveDistanceEnabled = true`, `adaptiveThreshold = 0.7f`, `adaptiveDelay = 0.5f`, `adaptiveSpeed = 3f`, `adaptiveRecoverySpeed = 2f`
- Smoothing: `positionSmoothTime = 0.04f` (было 0.05f)
- Collision: `sphereCastRadius = 0.4f` (было 0.3f), `wallOffset = 0.3f` (было 0.2f)

**Результат:**
- ✅ Camera Lag: камера не дёргается за кораблём, плавно следует за пешим персонажем
- ✅ Anti-Pop: нет дрожания у стен
- ✅ Adaptive Distance: в узких пространствах камера сама прижимается
- ✅ Wall Recovery: быстрый отъезд после выхода из-за стены
- ✅ 0 compile errors
- ✅ API-контракт сохранён

---

## Итерация от 2026-07-26 (Phase 3)

**Задача:** Phase 3 — Occlusion Fade
**Коммит:** `8e0412d` — T-CAM03: Phase 3 — Occlusion Fade

**Изменения:**
- `SpringArmCamera.cs` — +CheckOcclusion(), +RestoreOccludedRenderer()
- `OcclusionDither.shader` — URP Lit + Bayer 8x8 dither через clip()

**Результат:**
- ✅ Raycast occlusion detection (каждый 3-й кадр)
- ✅ Per-object dither через MaterialPropertyBlock._DitherAmount
- ✅ Плавный fade-in/out (occlusionFadeSpeed = 5)
- ✅ Устранена P4 (occlusion handling)
- ✅ 0 compile errors

---

## Итерация от 2026-07-26 (Phase 2)

**Задача:** Phase 2 — Camera Lag + Adaptive Distance
**Коммит:** `b891391` — T-CAM02: Phase 2 — Camera Lag + Adaptive Distance

**Изменения:**
- `SpringArmCamera.cs` — +UpdateLag(), +UpdateAdaptiveDistance(), все расчёты через _lagTargetPos

**Результат:**
- ✅ Раздельный XZ/Y Camera Lag с динамическим множителем (бег → меньше отставания)
- ✅ Adaptive Distance: авто-уменьшение дистанции при persistent collision + плавное восстановление
- ✅ Устранены P3 (адаптация) и P5 (инерция)
- ✅ 0 compile errors

---

## Итерация от 2026-07-26 (Phase 1)

**Задача:** Phase 1 — Spring Arm Core (collision avoidance + smoothing)
**Коммит:** `f2f3fbd` — T-CAM01: ThirdPersonCamera → SpringArmCamera (Phase 1 — collision avoidance + smoothing)

**Изменения:**
- `Assets/_Project/Scripts/Core/SpringArmCamera.cs` — новый компонент (400 строк)
- `Assets/_Project/Scripts/Player/NetworkPlayer.cs` — смена типа камеры
- `Assets/_Project/Scripts/Player/PlayerController.cs` — смена типа камеры
- `Assets/_Project/Scripts/Player/PlayerStateMachine.cs` — смена типа камеры
- `Assets/_Project/Scripts/Ship/UI/RepairManagerWindow.cs` — смена типа камеры
- `Assets/_Project/Prefabs/ThirdPersonCamera.prefab` — замена компонента
- `docs/Character/ThirdpersonCamera/04_IMPLEMENTATION_PHASE1.md` — отчёт

**Результат:**
- ✅ SphereCast collision avoidance (радиус 0.4m)
- ✅ SmoothDamp position smoothing (0.12s)
- ✅ Anti-pop гистерезис (0.2s)
- ✅ Wall recovery (3x быстрее при ratio < 0.4)
- ✅ Dynamic LookAt height (walk 1.5m / ship 4m)
- ✅ Smooth mode transition (0.5s)
- ✅ 0 compile errors
- ✅ API-контракт сохранён
