# CHANGELOG — Third-Person Camera

> **Что это:** лог итераций реализации SpringArmCamera
> **Первая запись:** 2026-07-26

---

## Итерация от 2026-07-26

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
