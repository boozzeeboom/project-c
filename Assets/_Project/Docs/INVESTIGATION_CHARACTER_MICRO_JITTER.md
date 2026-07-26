# INVESTIGATION: Микротряска персонажа при standing

**Дата:** 2026-07  
**Статус:** v3 — CharacterController keep-grounded micro-bounce (требует playtest)

---

## Симптом

Персонаж испытывает микротряску (micro-jitter) когда стоит на месте в пешем режиме. NPC тоже трясутся. Предметы с анимацией (крутятся/плавают) — НЕ трясутся. Корабль при пилотировании — НЕ трясётся.

## Диагноз

### v1 (ошибочный): Moving-platform carry → ОТКЛОНЁН

Первая гипотеза — `ApplyPlatformCarry()` ловит статичную геометрию как «платформу». Частично верно (фильтрация sleeping Rigidbody полезна), но не корневая причина: NPC используют отдельный `PlatformRideHelper`, а тряска есть у обоих.

### v2 (корневая причина): NetworkTransform.Interpolate конфликтует с прямым движением

**Общие компоненты** для игрока и NPC, которых нет у предметов и корабля:

| Компонент | Игрок | NPC | Предмет | Корабль |
|---|---|---|---|---|
| `CharacterController` | ✅ | ✅ | ❌ | ❌ |
| `NetworkTransform` `Interpolate=true` | ✅ | ✅ | ❌ | ✅ |
| Тип движения | `CC.Move()` (прямая запись) | `NavMeshAgent` (прямая запись) | Transform-анимация | `Rigidbody` (физика) |

**Механизм конфликта:**

1. Игрок: `CharacterController.Move()` в `Update()` двигает `transform.position`
2. NPC: `NavMeshAgent.nextPosition` двигает `transform.position`
3. `NetworkTransform` с `Interpolate=true` видит расхождение между серверным состоянием и текущим трансформом → интерполирует обратно

На **хосте** (IsServer && IsClient) это создаёт замкнутый цикл:
```
CC.Move() → position=X → NT.Interpolate() → position=X±ε → CC.Move() → ...
```

4. **Предметы** не трясутся — у них нет `NetworkTransform`
5. **Корабль** не трясётся — `Rigidbody` двигает трансформ через физический движок, который не конфликтует с `NetworkTransform` (плюс `RigidbodyInterpolation.Interpolate` сглаживает)

6. Комментарий в `NetworkPlayer.cs:238-240` прямо говорит: *«NetworkTransform.Interpolate отключаются ВРУЧНУЮ в Unity Editor на префабе»* — но на префабе `Interpolate=true`. Это баг конфигурации, существовавший с момента создания.

## Исправление v2

### 1. `NetworkPlayer.OnNetworkSpawn()` — отключение Interpolate для owner

```csharp
if (IsOwner)
{
    var nt = GetComponent<NetworkTransform>();
    if (nt != null) nt.Interpolate = false;
}
```

Owner двигает себя через `CharacterController.Move` — интерполяция не нужна и вредна.

### 2. `NpcBrain.OnNetworkSpawn()` — отключение Interpolate на хосте

```csharp
if (IsServer && IsClient)
{
    var nt = GetComponent<NetworkTransform>();
    if (nt != null) nt.Interpolate = false;
}
```

На хосте `NavMeshAgent` пишет позицию напрямую — `NetworkTransform` не должен интерполировать.

### 3. (v1, оставлено) Moving-platform carry — фильтрация sleeping Rigidbody + delta threshold

Оставлено как защитный слой: `DetectGroundPlatform()` не считает спящие Rigidbody платформами, `ApplyPlatformCarry()` игнорирует дельты < 0.5 мм.

---

## Стратегия отката

```bash
git revert <commit-hash>
```

Возвращает все изменения.

## v3 (2026-07): CharacterController keep-grounded micro-bounce

**Симптом:** тряска осталась после v2, усиливается при приближении камеры.

**Корневая причина:** `_velocity.y = -2f` (keep-grounded) каждый кадр толкает CC в землю на ~3.7 см. CharacterController разрешает penetration → выталкивает вверх → gravity тянет вниз → цикл. Даёт вертикальный micro-bounce `transform.position`.

Почему «при приближении усиливается»: вертикальное смещение transform.position даёт больший угловой сдвиг когда камера близко (малый `_currentDistance`).

**Дополнительный фактор:** `CharacterController.MinMoveDistance = 0.001` (1 мм) — разрешает движение на любую микро-пенетрацию, не фильтрует шум.

### Исправление v3 (T-JITTER02 + T-CAM05)

**1. `NetworkPlayer.ProcessMovement()` — смягчение keep-grounded:**
```
- _velocity.y = -2f;  // было: агрессивный прижим к земле
+ _velocity.y = -0.5f; // стало: достаточно для slope-stick, без micro-bounce
```

**2. `PlayerController.HandleMovement()` — аналогично (легаси):**
```
- _velocity.y = -2f;
+ _velocity.y = -0.5f;
```

**3. `NetworkPlayer.prefab` — CharacterController.MinMoveDistance:**
```
- m_MinMoveDistance: 0.001
+ m_MinMoveDistance: 0.005  // 5 мм — фильтрует micro-penetration шум
```

**4. `SpringArmCamera` (T-CAM05):**
- `ReadInput()`: dead-zone 0.01 на мышиный дельта
- `SmoothPosition()`: `Vector3.SmoothDamp` → `Vector3.Lerp` (exp decay) — убирает резонанс в каскаде UpdateLag+SmoothDamp

## Альтернативные гипотезы (если тряска осталась)

- **Animation clip:** idle-анимация содержит микро-движения root bone (Kevin Iglesias Mixamo). Отключить Animator для проверки. (`applyRootMotion=0` на префабе — маловероятно)
- **Камера SpringArmCamera:** до v3 был dual-smoothing resonance (исправлено в T-CAM05)
