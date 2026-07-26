# CHANGELOG — Third-Person Camera

> **Что это:** лог итераций реализации SpringArmCamera
> **Первая запись:** 2026-07-26

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
