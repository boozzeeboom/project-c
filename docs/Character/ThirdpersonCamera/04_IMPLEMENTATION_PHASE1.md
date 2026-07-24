# SpringArmCamera — Implementation Report

> **Дата:** 2026-07-26
> **Исполнитель:** Aura (Project C agent)
> **Статус:** ✅ Phase 1-3 Complete (Core + Lag + Occlusion)

---

## Итоговый статус проблем

| # | Проблема | Статус | Phase |
|---|----------|--------|-------|
| P1 | Камера проходит сквозь стены | ✅ SphereCast collision | 1 |
| P2 | Нет сглаживания (рывки) | ✅ SmoothDamp position | 1 |
| P3 | Нет адаптации дистанции | ✅ Adaptive Distance | 2 |
| P4 | Нет occlusion handling | ✅ Dither fade | 3 |
| P5 | Нет инерции/запаздывания | ✅ Camera Lag | 2 |
| P6 | LookAt фиксирован (1.5m) | ✅ Dynamic LookAt | 1 |
| P7 | Переключение walk/ship мгновенное | ✅ SmoothDamp mode transition | 1 |
| P8 | nearClipPlane = 0.5 | ⬜ Future | — |
| P9 | Нет FOV dynamics | ⬜ Future | — |
| P10 | Нет auto-center | ⬜ Future | — |

---

## Текущий LateUpdate pipeline (9 шагов)

```
ReadInput → UpdateModeTransition → UpdateLag → ComputeDesiredPosition
→ ResolveCollision → UpdateAdaptiveDistance → SmoothPosition
→ UpdateLookAt → CheckOcclusion
```

---

## Файлы

| Файл | Действие |
|------|----------|
| `SpringArmCamera.cs` | Новый (~550 строк) — полный SpringArm |
| `OcclusionDither.shader` | Новый — URP Lit + Bayer 8x8 dither |
| `NetworkPlayer.cs` | ThirdPersonCamera → SpringArmCamera |
| `PlayerController.cs` | ThirdPersonCamera → SpringArmCamera |
| `PlayerStateMachine.cs` | ThirdPersonCamera → SpringArmCamera |
| `RepairManagerWindow.cs` | ThirdPersonCamera → SpringArmCamera |
| `ThirdPersonCamera.prefab` | Замена компонента |

### НЕ затронуты (совместимость):
- `FloatingOriginMP.cs` — ищет по имени, не по типу
- `Billboard.cs` — использует Transform
- `ShipObservationCamera.cs` — получает Camera через RepairManagerWindow

---

## Параметры инспектора

| Параметр | Walk | Ship |
|----------|------|------|
| distance | 5 | 18 |
| height | 2 | 6 |
| sphereCastRadius | 0.4 | 0.4 |
| wallOffset | 0.3 | 0.3 |
| positionSmoothTime | 0.12 | 0.12 |
| antiPopTime | 0.2 | 0.2 |
| recoverySpeed | 10 | 10 |
| recoveryRatio | 0.4 | 0.4 |
| lagHorizontalTime | 0.15 | 0.15 |
| lagVerticalTime | 0.05 | 0.05 |
| adaptiveThreshold | 0.7 | 0.7 |
| adaptiveDelay | 0.5 | 0.5 |
| occlusionFadeSpeed | 5 | 5 |
| maxOcclusionCheckDist | 30 | 30 |
| modeSwitchSmoothTime | 0.5 | 0.5 |
| lookAtHeight | 1.5 | 4.0 |

---

## Коммиты

| Phase | Коммит | Описание |
|-------|--------|----------|
| 1 | `f2f3fbd` | T-CAM01: SpringArmCamera core |
| 2 | `b891391` | T-CAM02: Camera Lag + Adaptive Distance |
| 3 | `8e0412d` | T-CAM03: Occlusion Fade |
