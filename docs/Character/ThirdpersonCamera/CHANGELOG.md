# CHANGELOG — Third-Person Camera

> **Что это:** лог итераций реализации SpringArmCamera
> **Первая запись:** 2026-07-26

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
