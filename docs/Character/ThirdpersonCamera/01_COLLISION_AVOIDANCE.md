# Collision Avoidance — Deep Dive

> **Файл:** `01_COLLISION_AVOIDANCE.md`  
> **Цель:** Детальный технический дизайн системы защиты камеры от проваливания в геометрию  
> **Статус:** Research — готов к реализации  

---

## 1. Почему SphereCast, а не Raycast

### Проблема
Простой `Raycast` от target к камере — тонкий луч. Если в стене есть щель >0.01 units, луч пройдёт сквозь неё. Камера окажется за стеной.

### SphereCast решение
`Physics.SphereCast(origin, radius, direction, out hit, maxDistance, layerMask)` — толстая сфера.

**Типичный радиус:** 0.3-0.5m  
**Почему именно такой:**
- `< 0.2m` — камера проскакивает в мелкие щели (проваливается в mesh collider детали)
- `> 0.6m` — камера не подходит близко к острым углам (ощущение «отталкивания» от пустоты)
- `0.4m` — золотая середина (используется в God of War, Horizon)

### Альтернатива: несколько RayCast-лучей
Например, 5 лучей веером.  
**Минусы:** более затратно, сложнее edge cases, может найти препятствие там где камера не пройдёт.

**Решение:** один SphereCast — достаточно для нашего случая.

---

## 2. LayerMask — правильная настройка

Это самый частый источник багов. Неправильный LayerMask → камера либо проходит сквозь всё, либо упирается в невидимые триггеры.

### Рекомендуемый LayerMask для коллизий

```csharp
[Header("Collision Avoidance")]
[SerializeField] private LayerMask _collisionMask = -1;  // Default: Everything
```

**Включать:**
- `Default` — стандартная геометрия (здания, terrain, скалы)
- `Terrain` — ландшафт (если используется)
- `Static World` — если есть отдельный слой для статики мира
- `Interactable` — объекты, которые можно подобрать (должны блокировать камеру? — обычно нет)

**Исключать:**
- `Player` / `Character` — иначе SphereCast цепляется за самого персонажа
- `Ignore Raycast` — стандартное исключение
- `Trigger` — триггеры не должны блокировать камеру
- `UI` — очевидно
- `Water` — вода не блокирует камеру
- `TransparentFX` — если используется

**Особенность Project C:** в сцене могут быть `CloudLayer`, `WindZone`, `AltitiudeCorridor` — эти объекты НЕ должны блокировать камеру. Убедиться что они на отдельном слое или добавить их имена в исключение.

### Рекомендация
Настроить через Inspector в префабе камеры, с запасом:

```csharp
// Значение по умолчанию: всё кроме Player, IgnoreRaycast, Trigger, UI
_collisionMask = ~(1 << LayerMask.NameToLayer("Player") 
                 | 1 << LayerMask.NameToLayer("Ignore Raycast") 
                 | 1 << LayerMask.NameToLayer("Trigger")
                 | 1 << LayerMask.NameToLayer("UI"));
```

---

## 3. Алгоритм работы (пошагово)

```
Каждый кадр в LateUpdate():

1. ВРАЩЕНИЕ (yaw/pitch) — как сейчас
   _yaw += input.x * sensitivity
   _pitch -= input.y * sensitivity
   _pitch = Clamp(_pitch, -80, +80)

2. РАСЧЁТ ЖЕЛАЕМОЙ ПОЗИЦИИ
   orbitDir = SphericalToCartesian(_yaw, _pitch)
   desiredPos = targetPos + orbitDir * _currentDistance + up * _currentHeight

3. COLLISION AVOIDANCE
   direction = (desiredPos - targetPos).normalized
   maxDist = Vector3.Distance(targetPos, desiredPos)
   
   // SphereCast от target к desiredPos
   bool hit = Physics.SphereCast(
       targetPos + up * _lookAtHeight,
       _sphereRadius,
       direction,
       out RaycastHit hitInfo,
       maxDist,
       _collisionMask
   )

   if (hit):
       // Камера на точке столкновения + отступ
       cameraTargetPos = hitInfo.point + hitInfo.normal * (_sphereRadius + _wallOffset)
   else:
       // Камера на желаемой позиции
       cameraTargetPos = desiredPos

4. SMOOTH DAMP
   transform.position = Vector3.SmoothDamp(
       transform.position,
       cameraTargetPos,
       ref _positionVelocity,
       _smoothTime
   )

5. LOOK AT
   transform.LookAt(targetPos + up * _dynamicLookAtHeight)

6. (Опционально) OCCLUSION CHECK
   // Проверка: не перекрыт ли обзор на персонажа
   // См. 02_OCCLUSION_FADE.md
```

---

## 4. Anti-Pop Timer (гистерезис)

### Проблема
Камера у стены — SphereCast постоянно в hit. Игрок чуть шевельнулся — SphereCast чистый. Камера дёргается туда-сюда каждый кадр.

### Решение: таймаут гистерезиса

```csharp
// Состояние
private float _collisionExitTime;  // когда последний раз вышли из коллизии
private bool _wasColliding;

// В collision avoidance
bool isColliding = hit;
float currentTime = Time.time;

if (isColliding)
{
    _wasColliding = true;
    // Немедленно прижимаем — задержки нет
    cameraTargetPos = hitInfo.point + hitInfo.normal * offset;
}
else if (_wasColliding && currentTime - _collisionExitTime < _antiPopTime)
{
    // Остаёмся прижатыми ещё antiPopTime секунд
    cameraTargetPos = currentPos;  // не двигаемся
}
else
{
    _wasColliding = false;
    _collisionExitTime = currentTime;
    cameraTargetPos = desiredPos;
}
```

**Рекомендуемый anti-pop time:** 0.15-0.3s (Elden Ring — 0.2s).

---

## 5. Wall Recovery (Fast Catch-Up)

### Проблема
Камера была прижата к стене, игрок отошёл — камера не успевает отъехать. SmoothDamp слишком медленный.

### Решение: ускоренное восстановление

```csharp
// Если фактическая дистанция << желаемой — быстро восстанавливаемся
float actualDist = Vector3.Distance(transform.position, targetPos);
float desiredDist = _currentDistance;

if (actualDist < desiredDist * 0.4f)  // Камера прижата сильно
{
    // Fast recovery: короткое smoothTime + clamp max speed
    float fastSmoothTime = _smoothTime * 0.3f;  // в 3 раза быстрее
    Vector3 fastTarget = cameraTargetPos;
    transform.position = Vector3.SmoothDamp(
        transform.position,
        fastTarget,
        ref _recoveryVelocity,
        fastSmoothTime,
        _maxRecoverySpeed  // ~10 m/s
    );
}
else
{
    // Normal smooth
    transform.position = Vector3.SmoothDamp(
        transform.position,
        cameraTargetPos,
        ref _positionVelocity,
        _smoothTime
    );
}
```

**Параметры:**
- Порог recovery: `actualDist < desiredDist * 0.4f` (Elden Ring стиль)
- Recovery smoothTime: `_smoothTime * 0.3f` (3x быстрее нормы)
- Max recovery speed: `10 m/s` (чтобы не «выстреливать»)

---

## 6. Camera Lag (инерция за движением)

### Проблема
Камера следует за target мгновенно — нет ощущения веса. Персонаж бежит, камера стоит на месте (относительно него).

### Решение: раздельный lag по осям

```csharp
private Vector3 _lagTargetPos;

void UpdateLag()
{
    Vector3 delta = target.position - _lagTargetPos;
    
    // XZ — медленнее (горизонтальное движение)
    // Y — быстрее (вертикальное — прыжки, подъёмы)
    float lagXZ = 1f / _lagHorizontalTime;  // _lagHorizontalTime ~0.15s
    float lagY = 1f / _lagVerticalTime;      // _lagVerticalTime ~0.05s
    
    _lagTargetPos.x += delta.x * lagXZ * Time.deltaTime;
    _lagTargetPos.z += delta.z * lagXZ * Time.deltaTime;
    _lagTargetPos.y += delta.y * lagY * Time.deltaTime;
}
```

**ВАЖНО:** Все расчёты позиции камеры (collision, orbit, smooth) ведутся от `_lagTargetPos`, а не от `target.position`.

**Динамический lag (God of War):** при беге/спринте lag уменьшается:

```csharp
float speedFactor = Mathf.InverseLerp(0, _runSpeed, currentSpeed);
float dynamicLag = Mathf.Lerp(_lagHorizontalTime, _lagHorizontalTime * 0.3f, speedFactor);
```

---

## 7. Adaptive Distance (помещения и узкие пространства)

### Проблема
Игрок зашёл в пещеру/комнату. Камера упирается в потолок/стены. Вместо персонажа — текстура в упор.

### Решение: автоматическое уменьшение desiredDistance

```csharp
private float _adaptiveDistance;
private float _lastClearTime;

void UpdateAdaptiveDistance()
{
    float actualDist = Vector3.Distance(transform.position, target.position);
    float ratio = actualDist / _baseDistance;  // насколько прижата камера
    
    if (ratio < _adaptiveThreshold)  // threshold = 0.7 (70% от желаемой дистанции)
    {
        // Камера прижата — ждём adaptiveDelay секунд, потом уменьшаем desired
        if (Time.time - _lastClearTime > _adaptiveDelay)  // delay = 0.5s
        {
            // Плавно уменьшаем базовую дистанцию
            _currentDistance = Mathf.Lerp(
                _currentDistance,
                actualDist - _minWallOffset,  // minWallOffset = 0.5m
                _adaptiveSpeed * Time.deltaTime
            );
        }
    }
    else
    {
        // Пространство чистое — восстанавливаем базовую дистанцию
        _currentDistance = Mathf.Lerp(
            _currentDistance,
            _baseDistance,
            _adaptiveRecoverySpeed * Time.deltaTime  // recoverySpeed = 2x от speed
        );
        
        if (ratio > 0.95f)
            _lastClearTime = Time.time;  // обновляем таймер
    }
}
```

**Параметры:**
- `adaptiveThreshold = 0.7f` — камера прижата до 70% → адаптация
- `adaptiveDelay = 0.5s` — гистерезис (не реагировать на мимолётные коллизии)
- `adaptiveSpeed = 3f` — скорость уменьшения
- `adaptiveRecoverySpeed = 2f` — скорость восстановления (медленнее, чем уменьшение)
- `minWallOffset = 0.5m` — минимальный отступ от стены

---

## 8. Режимы: Walk vs Ship

У нас два принципиально разных таргета:
- **Walk:** персонаж ~1.8m высотой, дистанция 5m, высота камеры 2m
- **Ship:** корабль ~5-10m, дистанция 18m, высота 6m

### Dynamic LookAt Height

Фиксированная `target.position + up * 1.5f` — плохо для корабля (камера смотрит в пустоту).

```csharp
[SerializeField] private float _lookAtHeightWalk = 1.5f;    // пеший — голова персонажа
[SerializeField] private float _lookAtHeightShip = 4f;      // корабль — центр корпуса

private float _currentLookAtHeight;

void UpdateLookAt()
{
    // Плавный переход между режимами
    _currentLookAtHeight = Mathf.SmoothDamp(
        _currentLookAtHeight,
        _isShip ? _lookAtHeightShip : _lookAtHeightWalk,
        ref _lookAtVelocity,
        _modeSwitchSmoothTime
    );
    
    transform.LookAt(target.position + Vector3.up * _currentLookAtHeight);
}
```

### Smooth Mode Transition

Сейчас `SetShipMode()` — мгновенное присваивание.

```csharp
// В SetShipMode() — только задаём цели
public void SetShipMode(bool isShip)
{
    _targetDistance = isShip ? _shipDistance : _walkDistance;
    _targetHeight = isShip ? _shipHeight : _walkHeight;
    _targetLookAtHeight = isShip ? _lookAtHeightShip : _lookAtHeightWalk;
    _isShip = isShip;
}

// В LateUpdate — плавно интерполируем
void UpdateModeTransition()
{
    _currentDistance = Mathf.SmoothDamp(
        _currentDistance,
        _targetDistance,
        ref _distanceVelocity,
        _modeSwitchSmoothTime  // ~0.5s
    );
    
    _currentHeight = Mathf.SmoothDamp(
        _currentHeight,
        _targetHeight,
        ref _heightVelocity,
        _modeSwitchSmoothTime
    );
}
```

---

## 9. Auto-Center Behind Player

### Проблема
Игрок повернул камеру влево, потом нажал W — персонаж бежит в сторону, а не в экран. Неудобно.

### Решение
Когда игрок двигается вперёд, камера плавно доворачивается за спину:

```csharp
[SerializeField] private bool _autoCenterEnabled = true;
[SerializeField] private float _autoCenterSpeed = 90f;  // градусов/сек
[SerializeField] private float _autoCenterThreshold = 0.5f;  // порог ввода

void UpdateAutoCenter()
{
    if (!_autoCenterEnabled || target == null) return;
    
    // Проверяем движение вперёд (через PlayerInputReader или NetworkPlayer)
    float forwardInput = GetForwardInput();  // 0..1
    if (forwardInput > _autoCenterThreshold)
    {
        // Плавно доворачиваем yaw за спину персонажа
        float targetYaw = target.eulerAngles.y;
        float delta = Mathf.DeltaAngle(_yaw, targetYaw);
        
        // Не доворачиваем если угол большой (>120°) — игрок специально смотрит в сторону
        if (Mathf.Abs(delta) < 120f)
        {
            _yaw += Mathf.Sign(delta) * _autoCenterSpeed * Time.deltaTime;
        }
    }
}
```

> **Не для MVP.** Реализовать после Phase 2.

---

## 10. Инспектор — новые поля Spring Arm

```csharp
[Header("Spring Arm")]
[SerializeField] private float _sphereCastRadius = 0.4f;
[SerializeField] private LayerMask _collisionMask = ~0;
[SerializeField] private float _wallOffset = 0.3f;

[Header("Smoothing")]
[SerializeField] private float _positionSmoothTime = 0.12f;
[SerializeField] private float _rotationSmoothTime = 0.08f;

[Header("Camera Lag")]
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

[Header("Auto-Center")]
[SerializeField] private bool _autoCenterEnabled = false;  // OFF for MVP
[SerializeField] private float _autoCenterSpeed = 90f;

[Header("Mode Transition")]
[SerializeField] private float _modeSwitchSmoothTime = 0.5f;
```

---

## 11. Проверка работоспособности

### Что тестировать

| Ситуация | Ожидание |
|----------|----------|
| Камера у стены | Плавно прижимается, не дёргается |
| Выход из-за стены | Плавно отъезжает (fast recovery) |
| Узкий коридор | Adaptive distance уменьшает дистанцию |
| Потолок низкий | Камера прижимается вниз |
| Мгновенное вращение мыши | SmoothDamp не даёт рывков |
| Быстрый бег | Камера отстаёт (lag), но нагоняет |
| Переключение F (walk↔ship) | Плавный переход за 0.5s |
| Острые углы (угол здания) | Камера скользит вдоль, не застревает |
| Край обрыва | Камера НЕ проваливается за край |
| Несколько препятствий подряд | Камера выбирает ближайшее |

### Слои для отладки

Включить визуализацию SphereCast в редакторе:

```csharp
#if UNITY_EDITOR
private void OnDrawGizmosSelected()
{
    if (target == null) return;
    
    // Отображаем SphereCast
    Vector3 origin = target.position + Vector3.up * _currentLookAtHeight;
    Vector3 desiredPos = target.position + orbitDir * _currentDistance + Vector3.up * _currentHeight;
    
    Gizmos.color = Color.yellow;
    Gizmos.DrawWireSphere(desiredPos, _sphereCastRadius);
    
    if (_wasColliding)
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, _sphereCastRadius);
    }
}
#endif
```
