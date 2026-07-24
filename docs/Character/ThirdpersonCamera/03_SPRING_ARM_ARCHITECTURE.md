# Spring Arm Camera — Архитектура и план реализации

> **Файл:** `03_SPRING_ARM_ARCHITECTURE.md`  
> **Цель:** Полный технический дизайн нового компонента `SpringArmCamera`, заменяющего `ThirdPersonCamera`  
> **Статус:** Research — готов к реализации  

---

## 1. Что меняется

```
ThirdPersonCamera (307 строк)  →  SpringArmCamera (~450 строк)
├── Базовый orbit              ├── Orbit (как было)
├── Нет smoothing              ├── SmoothDamp position
├── Нет коллизий               ├── SphereCast collision avoidance
├── Нет адаптации              ├── Adaptive distance
├── Нет лага                   ├── Camera lag (XZ/Y раздельный)
├── Нет occlusion              ├── Occlusion detection (data для Renderer Feature)
├── Нет режимов                ├── Walk↔Ship с плавным переходом
└── Нет recovery               └── Wall recovery (fast catch-up)
```

### 1.1 API-контракт — НЕ меняется

Все публичные члены `ThirdPersonCamera` переходят в `SpringArmCamera` **без изменений сигнатуры**:

```csharp
// Сохраняем (прежние имена)
public Vector3 CameraForward { get; }
public Vector3 CameraRight { get; }
public void SetTarget(Transform newTarget);
public void SetTargetMode(Transform newTarget, bool isShip);
public void SetShipMode(bool isShip);
public void InitializeCamera();

// Добавляем
public Camera CameraComponent { get; }  // для ShipObservationCamera
public Transform TargetTransform { get; }  // для других подсистем
```

### 1.2 Что меняется в других файлах

| Файл | Что менять | Сложность |
|------|-----------|-----------|
| `ThirdPersonCamera.prefab` | Заменить компонент `ThirdPersonCamera` → `SpringArmCamera` | 🟢 Low |
| `NetworkPlayer.cs` | Поменять тип поля `_myCamera` и `cameraPrefab` | 🟢 Low |
| `PlayerController.cs` | Поменять тип поля `cameraController` | 🟢 Low |
| `PlayerStateMachine.cs` | Поменять тип поля `cameraController` | 🟢 Low |
| `RepairManagerWindow.cs` | Поменять `FindAnyObjectByType<ThirdPersonCamera>()` | 🟢 Low |
| `FloatingOriginMP.cs` | НЕ МЕНЯТЬ (ищет по имени, не по типу) | 🟢 None |
| `Billboard.cs` | НЕ МЕНЯТЬ (использует Transform) | 🟢 None |

---

## 2. Полная архитектура SpringArmCamera

### 2.1 Компоновка (LateUpdate pipeline)

```
SpringArmCamera.LateUpdate()
│
├── 1. ReadInput()
│      _lookInput = Read mouse delta
│      _yaw += delta.x * sensitivity
│      _pitch -= delta.y * sensitivity * invert
│
├── 2. UpdateModeTransition()
│      SmoothDamp current distance/height → target
│      (Walk vs Ship mode blending)
│
├── 3. UpdateLag()
│      _lagTargetPos follows target with delay (XZ != Y)
│
├── 4. ComputeDesiredPosition()
│      orbitDir = SphericalToCartesian(_yaw, _pitch)
│      desiredPos = _lagTargetPos + orbitDir * _currentDist + up * _currentHeight
│
├── 5. ResolveCollision()
│      SphereCast from _lagTargetPos → desiredPos
│      if (hit) cameraTargetPos = hit.point + normal * offset
│      else     cameraTargetPos = desiredPos
│
├── 6. UpdateAdaptiveDistance()
│      if persistently compressed → slowly reduce _currentDistance
│
├── 7. SmoothPosition()
│      if (compressed > recoveryRatio) → fast recovery
│      else → normal SmoothDamp
│      transform.position = SmoothDamp(...)
│
├── 8. LookAt()
│      transform.LookAt(_lagTargetPos + up * _currentLookAtHeight)
│
├── 9. CheckOcclusion()
│      Raycast from camera → target
│      if occluded → update dither amount for Renderer Feature
│
└── 10. UpdateAutoCenter() [опционально]
       Smoothly rotate yaw behind player when moving forward
```

### 2.2 Диаграмма потоков данных

```
Input (Mouse delta) ──► _yaw, _pitch
                            │
                            ▼
Target.position ──► UpdateLag() ──► _lagTargetPos
                                        │
                                        ▼
            SphericalToCartesian(_yaw, _pitch)
                                        │
                                        ▼
              _lagTargetPos + dir * _currentDistance + up * _currentHeight
                                        │
                                        ▼
                          ResolveCollision() ←── SphereCast
                                        │
                                        ▼
                              cameraTargetPos
                                        │
                                        ▼
                     SmoothDamp(position → cameraTargetPos)
                                        │
                                        ▼
                           transform.position = result
                                        │
                                        ▼
                            LookAt(_lagTargetPos)
                                        │
                                        ▼
                            CheckOcclusion() ──► Renderer Feature
```

### 2.3 Структура класса

```csharp
namespace ProjectC.Core
{
    public class SpringArmCamera : MonoBehaviour
    {
        // ──── Inspector Sections ────
        
        [Header("Target")]
        [SerializeField] private Transform _target;
        
        [Header("Orbit")]
        [SerializeField] private float _distance = 5f;
        [SerializeField] private float _shipDistance = 18f;
        [SerializeField] private float _height = 2f;
        [SerializeField] private float _shipHeight = 6f;
        [SerializeField] private float _minVerticalAngle = -80f;
        [SerializeField] private float _maxVerticalAngle = 80f;
        
        [Header("Sensitivity")]
        [SerializeField] private float _mouseSensitivity = 3f;
        [SerializeField] private bool _invertY = false;
        
        [Header("Spring Arm")]
        [SerializeField] private float _sphereCastRadius = 0.4f;
        [SerializeField] private LayerMask _collisionMask = ~0;
        [SerializeField] private float _wallOffset = 0.3f;
        
        [Header("Smoothing")]
        [SerializeField] private float _positionSmoothTime = 0.12f;
        [SerializeField] private float _rotationSmoothTime = 0.08f;
        
        [Header("Camera Lag")]
        [SerializeField] private bool _lagEnabled = true;
        [SerializeField] private float _lagHorizontalTime = 0.15f;
        [SerializeField] private float _lagVerticalTime = 0.05f;
        [SerializeField] private bool _dynamicLagEnabled = true;
        
        [Header("Adaptive Distance")]
        [SerializeField] private bool _adaptiveDistanceEnabled = true;
        [SerializeField] private float _adaptiveThreshold = 0.7f;
        [SerializeField] private float _adaptiveDelay = 0.5f;
        [SerializeField] private float _adaptiveSpeed = 3f;
        [SerializeField] private float _adaptiveRecoverySpeed = 2f;
        
        [Header("Wall Recovery")]
        [SerializeField] private float _recoverySpeed = 10f;
        [SerializeField] private float _recoveryRatio = 0.4f;
        
        [Header("Anti-Pop")]
        [SerializeField] private float _antiPopTime = 0.2f;
        
        [Header("Occlusion")]
        [SerializeField] private bool _occlusionEnabled = true;
        [SerializeField] private LayerMask _occlusionMask;
        [SerializeField] private float _maxOcclusionCheckDist = 30f;
        
        [Header("Auto-Center")]
        [SerializeField] private bool _autoCenterEnabled = false;
        [SerializeField] private float _autoCenterSpeed = 90f;
        
        [Header("Mode Transition")]
        [SerializeField] private float _modeSwitchSmoothTime = 0.5f;
        
        [Header("LookAt")]
        [SerializeField] private float _lookAtHeightWalk = 1.5f;
        [SerializeField] private float _lookAtHeightShip = 4f;
        
        // ──── Internal State ────
        private float _yaw, _pitch;
        private float _currentDistance, _currentHeight, _currentLookAtHeight;
        private float _targetDistance, _targetHeight, _targetLookAtHeight;
        private bool _isShip;
        
        private Vector3 _lagTargetPos;
        private Vector3 _positionVelocity, _recoveryVelocity;
        private float _distanceVelocity, _heightVelocity, _lookAtVelocity;
        
        private float _collisionExitTime;
        private bool _wasColliding;
        private float _lastClearTime;
        private float _currentDitherAmount;
        
        private InputAction _lookAction;
        private Vector2 _lookInput;
        private bool _initialized;
        
        private Camera _camera;
        private Material _ditherMaterial;
        
        // ──── Public API ────
        public Camera CameraComponent => _camera;
        public Transform TargetTransform => _target;
        
        public Vector3 CameraForward { get; }
        public Vector3 CameraRight { get; }
        
        public void SetTarget(Transform newTarget);
        public void SetTargetMode(Transform newTarget, bool isShip);
        public void SetShipMode(bool isShip);
        public void InitializeCamera();
        
        // ──── Lifecycle ────
        private void Awake();
        private void OnEnable();
        private void OnDisable();
        private void OnDestroy();
        private void LateUpdate();
        
        // ──── Pipeline Steps ────
        private void ReadInput();
        private void UpdateLag();
        private void UpdateModeTransition();
        private Vector3 ComputeDesiredPosition();
        private Vector3 ResolveCollision(Vector3 desiredPos);
        private void UpdateAdaptiveDistance();
        private void SmoothPosition(Vector3 targetPos);
        private void UpdateLookAt();
        private void CheckOcclusion();
        private void UpdateAutoCenter();
        
        // ──── Helpers ────
        private Vector3 SphericalToCartesian(float yaw, float pitch);
        
        #if UNITY_EDITOR
        private void OnDrawGizmosSelected();
        #endif
    }
}
```

---

## 3. Подробная реализация шагов LateUpdate

### 3.1 ReadInput

```csharp
private void ReadInput()
{
    if (Cursor.lockState != CursorLockMode.Locked) return;
    
    _lookInput = _lookAction.ReadValue<Vector2>();
    
    float sens = _cachedMouseSensitivity;
    float invert = _cachedInvertY ? -1f : 1f;
    
    _yaw += _lookInput.x * sens;
    _pitch -= _lookInput.y * sens * invert;
    _pitch = Mathf.Clamp(_pitch, _minVerticalAngle, _maxVerticalAngle);
}
```

### 3.2 UpdateLag

```csharp
private void UpdateLag()
{
    if (!_lagEnabled || _target == null)
    {
        _lagTargetPos = _target != null ? _target.position : Vector3.zero;
        return;
    }
    
    Vector3 delta = _target.position - _lagTargetPos;
    
    // Динамический lag: при беге уменьшается
    float lagXZ, lagY;
    if (_dynamicLagEnabled)
    {
        // Упрощённо: используем магнитуду скорости target
        float speed = delta.magnitude / Time.deltaTime;
        float speedFactor = Mathf.InverseLerp(0f, 10f, speed);  // 0-10 m/s
        float dynamicMultiplier = Mathf.Lerp(1f, 0.3f, speedFactor);
        lagXZ = 1f / (_lagHorizontalTime * dynamicMultiplier);
        lagY = 1f / (_lagVerticalTime * dynamicMultiplier);
    }
    else
    {
        lagXZ = 1f / _lagHorizontalTime;
        lagY = 1f / _lagVerticalTime;
    }
    
    _lagTargetPos.x += delta.x * lagXZ * Time.deltaTime;
    _lagTargetPos.z += delta.z * lagXZ * Time.deltaTime;
    _lagTargetPos.y += delta.y * lagY * Time.deltaTime;
}
```

### 3.3 UpdateModeTransition

```csharp
private void UpdateModeTransition()
{
    _currentDistance = Mathf.SmoothDamp(
        _currentDistance, _targetDistance,
        ref _distanceVelocity, _modeSwitchSmoothTime);
    
    _currentHeight = Mathf.SmoothDamp(
        _currentHeight, _targetHeight,
        ref _heightVelocity, _modeSwitchSmoothTime);
    
    _currentLookAtHeight = Mathf.SmoothDamp(
        _currentLookAtHeight, _targetLookAtHeight,
        ref _lookAtVelocity, _modeSwitchSmoothTime);
}
```

### 3.4 ComputeDesiredPosition

```csharp
private Vector3 ComputeDesiredPosition()
{
    Vector3 orbitDir = SphericalToCartesian(_yaw, _pitch);
    
    // Все расчёты от _lagTargetPos, не от target.position
    return _lagTargetPos + orbitDir * _currentDistance + Vector3.up * _currentHeight;
}

private Vector3 SphericalToCartesian(float yaw, float pitch)
{
    float yawRad = yaw * Mathf.Deg2Rad;
    float pitchRad = pitch * Mathf.Deg2Rad;
    
    return new Vector3(
        -Mathf.Sin(yawRad) * Mathf.Cos(pitchRad),
        Mathf.Sin(pitchRad),
        -Mathf.Cos(yawRad) * Mathf.Cos(pitchRad)
    );
}
```

### 3.5 ResolveCollision

```csharp
private Vector3 ResolveCollision(Vector3 desiredPos)
{
    Vector3 from = _lagTargetPos + Vector3.up * _currentLookAtHeight;
    Vector3 direction = (desiredPos - from).normalized;
    float maxDist = Vector3.Distance(from, desiredPos);
    
    float currentTime = Time.time;
    bool hit = Physics.SphereCast(
        from, _sphereCastRadius, direction,
        out RaycastHit hitInfo, maxDist, _collisionMask);
    
    if (hit)
    {
        _wasColliding = true;
        _collisionExitTime = currentTime;
        
        // Камера на точке столкновения + отступ
        return hitInfo.point + hitInfo.normal * (_sphereCastRadius + _wallOffset);
    }
    else if (_wasColliding && currentTime - _collisionExitTime < _antiPopTime)
    {
        // Anti-pop гистерезис: остаёмся прижатыми ещё antiPopTime
        _collisionExitTime = currentTime;  // продлеваем
        return transform.position;  // не двигаемся
    }
    else
    {
        _wasColliding = false;
        return desiredPos;
    }
}
```

### 3.6 UpdateAdaptiveDistance

```csharp
private void UpdateAdaptiveDistance()
{
    if (!_adaptiveDistanceEnabled) return;
    
    float actualDist = Vector3.Distance(transform.position, _lagTargetPos);
    float ratio = actualDist / _targetDistance;
    float currentTime = Time.time;
    
    if (ratio < _adaptiveThreshold && _wasColliding)
    {
        // Камера постоянно прижата → уменьшаем дистанцию
        if (currentTime - _lastClearTime > _adaptiveDelay)
        {
            float minDist = Mathf.Max(1f, actualDist - _wallOffset - _sphereCastRadius);
            _targetDistance = Mathf.Lerp(
                _targetDistance, minDist,
                _adaptiveSpeed * Time.deltaTime);
        }
    }
    else
    {
        // Восстанавливаем базовую дистанцию
        float baseDist = _isShip ? _shipDistance : _distance;
        _targetDistance = Mathf.Lerp(
            _targetDistance, baseDist,
            _adaptiveRecoverySpeed * Time.deltaTime);
        
        if (ratio > 0.95f)
            _lastClearTime = currentTime;
    }
}
```

### 3.7 SmoothPosition

```csharp
private void SmoothPosition(Vector3 cameraTargetPos)
{
    float actualDist = Vector3.Distance(cameraTargetPos, _lagTargetPos);
    float desiredDist = _targetDistance;
    float ratio = actualDist / Mathf.Max(desiredDist, 0.1f);
    
    if (ratio < _recoveryRatio)
    {
        // Fast recovery: камера сильно прижата
        float fastSmoothTime = _positionSmoothTime * 0.3f;
        transform.position = Vector3.SmoothDamp(
            transform.position, cameraTargetPos,
            ref _recoveryVelocity, fastSmoothTime,
            _recoverySpeed);  // max speed = 10 m/s
    }
    else
    {
        // Normal smooth
        transform.position = Vector3.SmoothDamp(
            transform.position, cameraTargetPos,
            ref _positionVelocity, _positionSmoothTime);
    }
}
```

### 3.8 UpdateLookAt

```csharp
private void UpdateLookAt()
{
    Vector3 lookTarget = _lagTargetPos + Vector3.up * _currentLookAtHeight;
    transform.LookAt(lookTarget);
}
```

### 3.9 CheckOcclusion

```csharp
private void CheckOcclusion()
{
    if (!_occlusionEnabled || _target == null || _camera == null) return;
    
    // Проверка: виден ли target на экране
    Vector3 viewportPos = _camera.WorldToViewportPoint(_target.position);
    bool onScreen = viewportPos.x > 0f && viewportPos.x < 1f
                 && viewportPos.y > 0f && viewportPos.y < 1f
                 && viewportPos.z > 0f && viewportPos.z < _maxOcclusionCheckDist;
    
    if (!onScreen)
    {
        // Персонаж не на экране — не надо дизерить
        _currentDitherAmount = Mathf.MoveTowards(_currentDitherAmount, 0f, 
            _occlusionFadeSpeed * Time.deltaTime);
        return;
    }
    
    // Raycast от камеры к персонажу
    Vector3 dir = _target.position - transform.position;
    float dist = dir.magnitude;
    
    if (Physics.Raycast(transform.position, dir.normalized, 
                        out RaycastHit hit, dist, _occlusionMask))
    {
        if (hit.transform != _target)
        {
            // Объект между камерой и персонажем
            _currentDitherAmount = Mathf.MoveTowards(
                _currentDitherAmount, 1f, 
                _occlusionFadeSpeed * Time.deltaTime);
            return;
        }
    }
    
    // Чисто
    _currentDitherAmount = Mathf.MoveTowards(
        _currentDitherAmount, 0f, 
        _occlusionFadeSpeed * Time.deltaTime);
}
```

### 3.10 UpdateAutoCenter

```csharp
private void UpdateAutoCenter()
{
    if (!_autoCenterEnabled || _target == null) return;
    
    // Упрощённая проверка: движение вперёд = игрок нажал W
    // Здесь нужно получать input от NetworkPlayer/PlayerController
    if (GetForwardInput() > _autoCenterThreshold)
    {
        float targetYaw = _target.eulerAngles.y;
        float delta = Mathf.DeltaAngle(_yaw, targetYaw);
        
        if (Mathf.Abs(delta) < 120f)
        {
            _yaw += Mathf.Sign(delta) * _autoCenterSpeed * Time.deltaTime;
        }
    }
}
```

---

## 4. Интеграция с существующими системами

### 4.1 NetworkPlayer.SpawnCamera()

```csharp
// Было:
[SerializeField] private ThirdPersonCamera cameraPrefab;
private ThirdPersonCamera _myCamera;

// Стало:
[SerializeField] private SpringArmCamera cameraPrefab;
private SpringArmCamera _myCamera;

// SpawnCamera() остаётся без изменений:
var camObj = Instantiate(cameraPrefab.gameObject);
_myCamera = camObj.GetComponent<SpringArmCamera>();
_myCamera.SetTarget(transform);
_myCamera.InitializeCamera();
```

### 4.2 PlayerController.cs

```csharp
// Было:
[SerializeField] private ThirdPersonCamera cameraController;

// Стало:
[SerializeField] private SpringArmCamera cameraController;

// Всё остальное без изменений — использует CameraForward/CameraRight
```

### 4.3 ShipObservationCamera

```csharp
// Сейчас получает Camera через FindAnyObjectByType<ThirdPersonCamera>().GetComponent<Camera>()
// После замены:
var tpc = FindAnyObjectByType<SpringArmCamera>();
if (tpc != null) playerCam = tpc.CameraComponent;
```

### 4.4 Billboard.cs

```csharp
// Без изменений — Billboard.ActiveCamera = transform (Transform from ThirdPersonCamera)
// После замены всё ещё Transform — трогать не нужно.
```

### 4.5 FloatingOriginMP.cs

```csharp
// Без изменений — ищет по GameObject.Find("ThirdPersonCamera_<id>")
// Имя камеры сохраняется в SpawnCamera().
```

### 4.6 Prefab

**ThirdPersonCamera.prefab**:
- Имя: `ThirdPersonCamera` (корень)
- Компоненты:
  - `Camera` (farClip=1000000, nearClip=0.5)
  - `UniversalAdditionalCameraData`
  - `SpringArmCamera` (заменяет `ThirdPersonCamera`)

---

## 5. Порядок реализации (Phase 1)

### 5.1 Файлы для создания

1. `Assets/_Project/Scripts/Core/SpringArmCamera.cs` — новый компонент (~450 строк)

### 5.2 Файлы для изменения

2. `Assets/_Project/Scripts/Player/NetworkPlayer.cs` — смена типа камеры
3. `Assets/_Project/Scripts/Player/PlayerController.cs` — смена типа
4. `Assets/_Project/Scripts/Player/PlayerStateMachine.cs` — смена типа
5. `Assets/_Project/Scripts/Ship/UI/RepairManagerWindow.cs` — смена типа в Find

### 5.3 Префаб

6. `Assets/_Project/Prefabs/ThirdPersonCamera.prefab` — замена компонента

### 5.4 Шаги

```
Step 1: Написать SpringArmCamera.cs (все шаги LateUpdate из §3)
        → Включить: ReadInput, ComputeDesired, ResolveCollision, Smooth, LookAt
        → Исключить (Phase 2): Lag, AdaptiveDistance, Occlusion, AutoCenter
Step 2: Обновить NetworkPlayer.cs (смена типа)
Step 3: Обновить PlayerController.cs (смена типа)
Step 4: Обновить PlayerStateMachine.cs (смена типа)
Step 5: Обновить RepairManagerWindow.cs
Step 6: Заменить компонент в префабе
Step 7: Проверить compile (refresh_unity → read_console)
```

---

## 6. Параметры инспектора (рекомендуемые значения)

| Параметр | Walk | Ship | Примечание |
|----------|------|------|------------|
| distance | 5 | 18 | Базовые дистанции |
| height | 2 | 6 | Высота камеры |
| sphereCastRadius | 0.4 | 0.4 | Единый радиус |
| wallOffset | 0.3 | 0.5 | Корабль — больше отступ |
| positionSmoothTime | 0.12 | 0.2 | Корабль — медленнее |
| lagHorizontalTime | 0.15 | 0.3 | Корабль — больше инерции |
| lagVerticalTime | 0.05 | 0.1 | |
| adaptiveThreshold | 0.7 | 0.5 | |
| adaptiveDelay | 0.5 | 1.0 | |
| recoveryRatio | 0.4 | 0.3 | |
| antiPopTime | 0.2 | 0.3 | |
| lookAtHeight | 1.5 | 4.0 | Голова vs центр корпуса |
| modeSwitchSmoothTime | 0.5 | 0.5 | |

---

## 7. Тестирование

### 7.1 Базовые тесты (после Phase 1)

1. **Compile:** 0 errors in console после замены
2. **Play Mode:** камера работает, вращается мышью
3. **Collision:** камера упирается в стену, не проваливается
4. **Smoothing:** плавное движение, нет рывков
5. **Mode switch (F):** переключение walk↔ship работает

### 7.2 Тесты (после Phase 2)

6. **Lag:** камера отстаёт при беге, нагоняет при остановке
7. **Adaptive:** в пещере камера сама прижимается
8. **Recovery:** после стены быстро отъезжает
9. **Anti-pop:** у стены не дёргается

### 7.3 Тесты (после Phase 3)

10. **Occlusion:** объекты между камерой и персонажем дизерятся
11. **FOV:** изменяется в зависимости от скорости/режима

---

## 8. Известные риски

| Риск | Митигация |
|------|-----------|
| SphereCast спам → CPU | Оптимизация: проверять каждый 2-й кадр в LateUpdate? Нет, LateUpdate один раз. Один SphereCast не критичен. |
| Camera «ныряет» при резком повороте | test с разными _positionSmoothTime |
| FloatingOriginMP не находит камеру после замены компонента | Имя объекта сохраняется — не проблема |
| SmoothDamp overshoot при быстром recovery | Clamp maxSpeed (10 m/s) |
| AdaptiveDistance создаёт «кивание» камеры | Anti-pop таймер решает |

---

## 9. Сводка: что даёт каждая фаза

| Phase | Добавляет | Строк кода | Устраняет |
|-------|-----------|-----------|-----------|
| 1 | SphereCast + SmoothDamp + Dynamic LookAt | ~150 net new | P1, P2, P6 |
| 2 | Camera Lag + Adaptive + Recovery + Anti-pop | ~100 net new | P3, P5 |
| 3 | Occlusion Fade + Auto-Center | ~100 net new | P4 |

**Итого:** ~350 новых строк кода вместо 307 старых. Net +50 строк за все фазы, но с полным функционалом современной TPS камеры.
