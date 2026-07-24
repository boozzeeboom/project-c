# SpringArmCamera — Phase 1 Implementation Report

> **Дата:** 2026-07-26
> **Исполнитель:** Aura (Project C agent)
> **Статус:** ✅ Complete

---

## Что сделано

### 1. Создан `SpringArmCamera.cs`
**Путь:** `Assets/_Project/Scripts/Core/SpringArmCamera.cs` (~400 строк)

Реализованные шаги LateUpdate pipeline:
- ✅ `ReadInput()` — yaw/pitch orbit (как в ThirdPersonCamera)
- ✅ `UpdateModeTransition()` — SmoothDamp для walk↔ship (distance, height, lookAtHeight)
- ✅ `ComputeDesiredPosition()` — сферические координаты
- ✅ `ResolveCollision()` — SphereCast (radius 0.4m) + anti-pop гистерезис (0.2s)
- ✅ `SmoothPosition()` — SmoothDamp + fast wall recovery (3x быстрее при ratio < 0.4)
- ✅ `UpdateLookAt()` — динамическая высота (1.5m walk / 4m ship)
- ✅ `OnDrawGizmosSelected()` — визуализация SphereCast
- ✅ Полный API-контракт: `CameraForward`, `CameraRight`, `CameraComponent`, `SetTarget`, `SetTargetMode`, `SetShipMode`, `InitializeCamera`

**НЕ включено (Phase 2):**
- Camera Lag (инерция)
- Adaptive Distance
- Occlusion Dither
- Auto-Center Behind Player

### 2. Обновлены зависимые файлы

| Файл | Изменения |
|------|-----------|
| `NetworkPlayer.cs` | `ThirdPersonCamera` → `SpringArmCamera` (4 места: поле prefab, поле _myCamera, GetComponent, FindAnyObjectByType) |
| `PlayerController.cs` | `ThirdPersonCamera` → `SpringArmCamera` (2 места: поле cameraController, FindAnyObjectByType) |
| `PlayerStateMachine.cs` | `ThirdPersonCamera` → `SpringArmCamera` (1 место: поле cameraController) |
| `RepairManagerWindow.cs` | `ThirdPersonCamera` → `SpringArmCamera` (1 место: FindAnyObjectByType в CachePlayerCamera) |

### 3. Обновлён префаб
**Путь:** `Assets/_Project/Prefabs/ThirdPersonCamera.prefab`

Компоненты:
- Transform
- Camera (farClipPlane=1000000, nearClipPlane=0.5)
- UniversalAdditionalCameraData
- **SpringArmCamera** (заменил ThirdPersonCamera)

Имя объекта: `ThirdPersonCamera` — **сохранено** (FloatingOriginMP)

### 4. НЕ затронуты (как и планировалось)
- `FloatingOriginMP.cs` — ищет по имени `"ThirdPersonCamera"`, не по типу → работает
- `Billboard.cs` — использует `Transform`, не тип → работает
- `ShipObservationCamera.cs` — получает `Camera` через `RepairManagerWindow.CachePlayerCamera()` → работает

---

## Проверка

- ✅ 0 compile errors
- ✅ Все 4 зависимых файла обновлены
- ✅ Префаб содержит SpringArmCamera вместо ThirdPersonCamera
- ✅ API-контракт сохранён

---

## Что устранено (из 10 проблем)

| # | Проблема | Статус |
|---|----------|--------|
| P1 | Камера проходит сквозь стены | ✅ SphereCast collision avoidance |
| P2 | Нет сглаживания (рывки) | ✅ SmoothDamp position |
| P6 | LookAt фиксирован (1.5m) | ✅ Dynamic LookAt (walk/ship) |
| P7 | Переключение walk/ship мгновенное | ✅ SmoothDamp mode transition |

---

## Параметры инспектора (рекомендуемые)

В префабе выставлены defaults из ресёрча:
- sphereCastRadius = 0.4
- wallOffset = 0.3
- positionSmoothTime = 0.12
- antiPopTime = 0.2
- recoverySpeed = 10
- recoveryRatio = 0.4
- modeSwitchSmoothTime = 0.5
- lookAtHeightWalk = 1.5
- lookAtHeightShip = 4.0

---

## Следующие шаги

**Phase 2** (Camera Lag + Adaptive Distance):
- Camera Lag (раздельный XZ/Y)
- Adaptive Distance (авто-прижимание в помещениях)
- Wall Recovery (уже частично в Phase 1)

**Phase 3** (Occlusion Fade):
- Screen-space dither через URP Renderer Feature
- Или per-object fade (проще, но грязнее)
