# INVESTIGATION: Микротряска персонажа при standing

**Дата:** 2026-07  
**Статус:** Исправлено (требует проверки playtest)

---

## Симптом

Персонаж испытывает микротряску (micro-jitter) когда стоит на месте в пешем режиме. Другие объекты рядом не трясутся. Корабль при пилотировании (клавиша F) не трясётся. NPC-модели не трясутся.

## Диагноз

### Корневая причина: `ApplyPlatformCarry()` + `DetectGroundPlatform()`

Система moving-platform carry (`NetworkPlayer.cs`, строки 944–1070) предназначена для переноса персонажа вместе с движущейся палубой корабля. Однако она срабатывала и на статичной земле:

1. **`DetectGroundPlatform()`** — SphereCast вниз через `_platformMask = ~0` (Everything). Находил любой коллайдер с Rigidbody и возвращал его как «движущуюся платформу», даже если Rigidbody спал/стоял на месте.

2. **`ApplyPlatformCarry()`** — отслеживал дельту `transform.position` этой «платформы» каждый кадр и применял через `_controller.Move()`.

3. **Почему возникала микротряска:**
   - Статичная геометрия сцены (полы, платформы доков) часто имеет Rigidbody (для коллизий, триггеров).
   - Даже спящий Rigidbody даёт микроскопические флуктуации `transform.position` из-за:
     - `RigidbodyInterpolation.Interpolate` на корабле (стр. 507 ShipController)
     - Floating-point resolution physics solver
   - Эти флуктуации (<< 1 мм) накапливались в `_platformDelta` и толкали CharacterController.

4. **Почему только персонаж:**
   - NPC используют другой код движения (не `NetworkPlayer`).
   - Корабль при пилотировании (F): `_controller.enabled = false`, персонаж припарентен к кораблю напрямую (`transform.SetParent(_currentShip.ShipRoot, true)`) — платформенный carry не активен.

### Второстепенный фактор

`_onPlatform` + `_isGrounded` flicker (строка 869): если SphereCast нестабильно находил/терял платформу между кадрами, `_onPlatform` мигал → `groundedForMovement` терялся → гравитация тянула вниз → микро-подскок.

## Исправление

Три архитектурных изменения в `NetworkPlayer.cs`:

### 1. `DetectGroundPlatform()` — фильтрация стационарных Rigidbody

- Статичные коллайдеры **без** Rigidbody → `null` (не платформа).
- Спящий Rigidbody (`rb.IsSleeping()`) → `null`.
- Kinematic Rigidbody с нулевой скоростью → `null`.
- Non-kinematic Rigidbody с `velocity.sqrMagnitude < 0.0001` → `null`.

Платформа определяется **только** если Rigidbody реально движется.

### 2. `ApplyPlatformCarry()` — фильтр минимальной дельты

```csharp
if (deltaPos.sqrMagnitude < _platformMinDelta * _platformMinDelta)
{
    _platformLastPos = platform.position;  // обновляем кеш, не накапливаем шум
    _platformLastRot = platform.rotation;
    return;
}
```

`_platformMinDelta = 0.0005f` (0.5 мм) — дельты меньше считаются floating-point шумом.

### 3. Новое поле `_platformMinDelta`

Сериализовано в инспекторе с tooltip-документацией. Можно изменить без правки кода.

## Стратегия отката

Если тряска не пропала — это означает, что причина **НЕ** в moving-platform carry.  
Возможные альтернативные причины (для дальнейшего расследования):

- **Animator root motion:** если `Apply Root Motion` включён на Animator персонажа и idle-анимация имеет микро-смещения root bone.
- **CharacterController.Skin Width:** слишком маленький skin width (←0.01) вызывает micro-penetration resolution.
- **FixedUpdate позиционная коррекция (стр. 816):** `_hasServerPosition` + `positionCorrectionThreshold` — хоть порог и 99999, остаточная логика может вмешиваться.
- **Камера:** `SpringArmCamera.UpdateLag()` имеет dynamic-lag с `positionSmoothTime = 0.08f` — потенциальный источник визуальной тряски (не трансформа).

Откат: `git revert <commit-hash>` этого коммита.

## Верификация

1. Запустить игру, персонаж на земле (не на корабле) — тряска должна исчезнуть.
2. Персонаж на палубе летящего корабля (НЕ за штурвалом) — должен переноситься вместе с палубой без тряски.
3. Персонаж за штурвалом (F) — без изменений (CharacterController отключён).
