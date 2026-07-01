# 09 — Moving-Platform Character Physics (чтобы не сдувало с движущихся кораблей)

**Статус:** реализовано в `NetworkPlayer` (пеший режим игрока). Для NPC-экипажа — см.
`docs/Character/Skills/real-time-combat/npc-enemy/01_CREW_ON_MOVING_SHIP.md`.
**Связано:** `07_SHIP_PROXIMITY_AVOIDANCE.md`, `08_CONTROL_AUTHORITY_AND_PHYSICS.md`, `M2_FSM_DIAGNOSIS.md`.

## 1. Проблема

Персонажа, стоящего/идущего по палубе движущегося корабля, «сдувает» назад, когда
корабль летит или поворачивает.

**Причина (evidence):**
- Пеший игрок движется через `CharacterController.Move` в
  `NetworkPlayer.ProcessMovement` (`Assets/_Project/Scripts/Player/NetworkPlayer.cs`).
  `CharacterController` **не наследует** движение платформы под ногами.
- NPC-корабль двигается **прямой записью** `rb.linearVelocity` + `rb.MoveRotation` в
  `NpcShipController.NavTick` (`Stations/NpcShipController.cs`): `TickCruise` до
  `CruiseSpeed=12 м/с`, `TickYaw`/`TickCruise` до `MaxYawRate=45°/с`. Палуба уезжает
  из-под ног → игрок остаётся на месте в мире → соскальзывает/падает.
- **Пилоты не страдают:** при посадке (`SubmitSwitchModeRpc`) игрок парентится к
  `ShipController.ShipRoot` и `_controller.enabled=false`. Баг только у **стоящих на
  палубе не-пилотов**.

## 2. Почему transform-delta, а не velocity

Движение игрока считается на **owner-клиенте** (`ProcessMovement` вызывается только при
`IsOwner`). Корабль реплицируется server-authoritative NetworkTransform — на клиенте его
`Rigidbody.linearVelocity` **недоступен/не синхронизирован**. Поэтому carry считается по
**дельте `Transform` платформы между кадрами**, а не по её velocity. Это устойчиво к
интерполяции NetworkTransform и не зависит от того, как именно платформа движется
(прямой velocity, MoveRotation, кинематика, анимация).

## 3. Дизайн

В пешем режиме (`_inShip==false`, owner, `_controller.enabled`) каждый кадр:

1. **Detect** — `Physics.SphereCast` вниз из центра капсулы по `_platformMask`.
   Корень движения платформы = `hit.collider.attachedRigidbody?.transform ??
   hit.collider.transform`. Поддерживает **любые** движущиеся платформы (палубы
   кораблей, лифты, кинематические платформы), не только `ShipController`.
2. **Carry (позиция + yaw)** — если платформа та же, что в прошлом кадре:
   - `deltaPos = platform.position - _platformLastPos` (следование за палубой, включая
     вертикаль — держит на палубе при взлёте/снижении).
   - `deltaYaw = DeltaAngle(prevYaw, curYaw)` только вокруг **мировой оси Y**: игрока
     доворачиваем на `deltaYaw` и добавляем **орбитальное смещение** вокруг
     `platform.position`, чтобы при повороте корабля не соскальзывал вбок.
   - **Pitch/roll платформы игнорируются** — переносим только `position` и вращаем только
     вокруг `Vector3.up`, поэтому крен/тангаж корабля не опрокидывает и не смещает игрока.
   - Дельта складывается в поле `_platformDelta` и применяется в **едином** финальном
     `CharacterController.Move` (см. §3a).
3. **Hysteresis** — если probe пусто `_platformMissFramesToClear` кадров подряд, считаем,
   что сошли с платформы, и сбрасываем кэш. При смене платформы кэш переинициализируется
   без рывка (кадр «привязки» carry не применяет).

## 3a. Фикс вертикального джиттера (персонаж «подпрыгивал»)

**Симптом:** стоя на летящей палубе персонаж постоянно «падал/приземлялся».

**Причина:**
1. **Два `_controller.Move` за кадр** (отдельный carry-Move + локомоция) — каждый Move
   переоценивает коллизии, из-за чего `CharacterController.isGrounded` мигал, и по Y шли
   пере-приземления.
2. NPC в `Cruising` держит высоту через `linearVelocity.y` в диапазоне ±2 м/с (`altHold`
   в `TickCruise`) — палуба слегка **колышется по вертикали**, что усиливало дрожание
   `isGrounded` и накопление гравитации.

**Решение:**
1. Carry больше **не** делает свой `Move`. Дельта копится в `_platformDelta` и уходит в
   **один** финальный `Move`: `_controller.Move(motion * dt + _platformDelta)`.
2. Введён флаг `_onPlatform` и `groundedForMovement = _isGrounded || _onPlatform`. Пока
   персонаж на платформе, он считается «на земле»: `_velocity.y` держится на keep-grounded
   (-2) без накопления гравитации → нет подскоков. К этому же флагу привязаны прыжок и
   логика ветрового сноса.

**Известный нюанс:** прыжок на палубе может подсекаться, пока probe ещё «видит» палубу в
пределах `_platformProbeDistance`. Лечится уменьшением `_platformProbeDistance` или паузой
carry-grounded на кадры прыжка (при необходимости).

## 4. Параметры (все `[SerializeField]`, без magic numbers)

| Поле | Назначение |
|------|-----------|
| `_platformCarryEnabled` | вкл/выкл всей механики |
| `_platformMask` | слои, считающиеся движущимися платформами |
| `_platformProbeDistance` | добавочная дальность probe ниже подошвы |
| `_platformProbeRadius` | радиус SphereCast |
| `_carryYaw` | переносить курсовой поворот (yaw). Pitch/roll — никогда |
| `_platformMissFramesToClear` | гистерезис схода с платформы (кадры) |

## 5. Что нельзя ломать

- **НЕ** трогать `NpcShipController.NavTick`, прямой `linearVelocity`/`MoveRotation`,
  `MaxYawRate`, `Docked`-логику (`isKinematic`, dwell, `detectCollisions`).
- **НЕ** трогать силовую модель игрока (`ShipController.FixedUpdate`), NGO/RPC/NetworkTransform.
- Carry работает поверх существующего `CharacterController.Move`, единым аддитивным Move.
- Ветровой снос (`_windVelocity`) остаётся независимым (мировой снос ≠ следование за палубой).

## 6. Крайние случаи

- **Docked NPC:** `isKinematic=true` → `platform.position` не меняется → carry = 0 (нейтрален).
- **Lifting NPC:** `detectCollisions=false` — палубы под ногами может не быть, probe пусто →
  carry не применяется (штатно, персонаж просто не «прилипает» на время взлёта).
- **Пилот:** `_controller.enabled=false` → carry не запускается; при посадке `_currentPlatform`
  сбрасывается, чтобы после выхода не было скачка.
- **Пустой `_platformMask`:** carry выключается, один раз пишем `Debug.LogWarning`.

## 7. Обязательная настройка (Inspector)

На префабе `Player` задать **`_platformMask`** = слой(и) коллайдеров палуб. **Не оставлять
`Everything`** — иначе probe попадёт в собственный коллайдер игрока и не найдёт палубу.
Палубы кораблей держать на отдельном слое, не совпадающем с Player.

## 8. Тест-план (T-NS-MP01)

- `WorldScene_0_0`: встать на палубу `NPC_Ship_HeavyII_01`, дождаться `Lifting → Yawing →
  Cruising`. Ожидание: персонаж едет с палубой, не соскальзывает назад; при повороте
  (`Yawing`) не сносит вбок; при крене/тангаже корабля персонаж не опрокидывается; **вертикаль
  спокойная** (нет «подпрыгивания»).
- Логи `entered/left moving platform` появляются в нужные моменты, без спама.
- Регресс: ходьба по неподвижной земле, посадка пилотом (F), ветровой снос в воздухе — без изменений.
