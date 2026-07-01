# Глобальный ветер → Корабли и Персонажи (WindManager)

## Что это
Глобальная система ветра `WindManager` влияет на облака, **корабли**
(`ShipController`, физическая сила на Rigidbody) и **персонажей**
(`NetworkPlayer`, влияние на пешее передвижение). Все силы и множители
**настраиваются в инспекторе** — никакого хардкода.

## Архитектура
- **`WindManager`** (`Assets/_Project/Scripts/Core/WindManager.cs`) — синглтон
  (`ProjectC.Core`), единый источник правды:
  - `CurrentWindDirection` (нормализованный вектор);
  - `CurrentWindSpeed` (м/с, clamp 0..100);
  - обновляется сервером через `ServerWeatherController`.
  - **Множители влияния (инспектор, Header «Влияние на геймплей»):**
    - `ShipWindMultiplier` (дефолт 1) — глобальный тюнинг силы ветра на все корабли.
    - `CharacterWindMultiplier` (дефолт 1) — глобальный тюнинг влияния ветра на всех персонажей.
- **Потребители:** облака, HUD, корабли (`ShipController`), персонажи (`NetworkPlayer`).
- **Локальные зоны `WindZone`/`WindZoneData`** — независимая система; на кораблях
  глобальный ветер и зоны **складываются аддитивно**.

## Корабли — ShipController
Файл: `Assets/_Project/Scripts/Player/ShipController.cs`, метод `ApplyGlobalWind(dt)`
(server-only, из `FixedUpdate` после `ApplyWind(dt)`).
```
dir    = normalize(WindManager.CurrentWindDirection with y *= _globalWindVerticalFactor)
target = dir * WindManager.CurrentWindSpeed * _globalWindForceScale * WindManager.ShipWindMultiplier   // Н
smooth = Lerp(prev, target, dt / windDecayTime)
force  = smooth * windInfluence * (windExposure + moduleWindExposureMod)   // Н
Rigidbody.AddForce(force, Force)
```
Инспектор (Header «Глобальный Ветер (WindManager)»): `_globalWindEnabled`,
`_globalWindForceScale` (Н на м/с, дефолт 8), `_globalWindVerticalFactor`
(0 = только горизонт). Плюс `windInfluence`, `windExposure` (класс-специфична),
`windDecayTime`.

## Персонажи — NetworkPlayer
Файл: `Assets/_Project/Scripts/Player/NetworkPlayer.cs`. Влияние ветра встроено в
`ProcessMovement` (owner-only, пеший режим). Никакого отдельного `Move` — всё в
ЕДИНОМ `CharacterController.Move` (Y держит keep-grounded `-2` → без подпрыгивания).

**Инерция порывов:** целевая скорость сноса (`GetGlobalWindTargetVelocity()`)
сглаживается через `Vector3.SmoothDamp` с временем `_windSmoothTime` — порывы
нарастают/затухают плавно.

**Правила применения (по состоянию персонажа):**
| Состояние | Ветер |
|-----------|-------|
| Стоит на земле (нет ввода) | **НЕ применяется** (иначе воспринимается как баг-дрейф) |
| Бежит/идёт по земле | добавляется к локомоции — встречный тормозит, попутный ускоряет |
| В воздухе (прыжок/падение) | **полный снос** |

```
targetWind   = normalize(horizontal(WindManager.CurrentWindDirection)) *
               WindManager.CurrentWindSpeed * _windDriftScale * WindManager.CharacterWindMultiplier   // м/с
_windVelocity = SmoothDamp(_windVelocity, targetWind, _windSmoothTime)   // инерция (считается всегда)

windVel = (!grounded || hasInput) ? _windVelocity : Vector3.zero          // стоя — 0
motion  = locomotion(horizontal) + windVel;  motion.y += gravity/jump
controller.Move(motion * dt)                                              // единый Move
```
> Скорость сглаживается КАЖДЫЙ кадр (в т.ч. стоя), поэтому к моменту прыжка
> `_windVelocity` уже вышла на целевое значение и снос в воздухе начинается сразу.

Инспектор NetworkPlayer (Header «Ветер (WindManager)»):
| Поле | Дефолт | Назначение |
|------|--------|------------|
| `_globalWindEnabled` | `true` | Вкл/выкл влияние ветра на персонажа. |
| `_windDriftScale` | `0.15` | м/с сноса на 1 м/с ветра (до `CharacterWindMultiplier`). |
| `_windSmoothTime` | `0.5` | Время сглаживания порывов (инерция). Больше = мягче. |

Пример: ветер 20 м/с, `_windDriftScale = 0.15`, `CharacterWindMultiplier = 1` →
целевой снос ~3 м/с (walkSpeed = 5, runSpeed = 10 → встречный ветер заметно
замедляет шаг, попутный ускоряет).

## Множители на WindManager (центральный тюнинг)
`ShipWindMultiplier` / `CharacterWindMultiplier` глобально усиливают/ослабляют/
отключают (`0`) влияние ветра на все корабли или всех персонажей. Перемножаются
с per-объектными полями (`_globalWindForceScale` у корабля, `_windDriftScale` у персонажа).

## Проверка
1. В Play (host/server) менять ветер (погодный контроллер или вручную
   `CurrentWindSpeed`/`CurrentWindDirection` в `WindManager`).
2. **Корабль** без ввода сносит по ветру; сила ∝ `speed`, `_globalWindForceScale`,
   `ShipWindMultiplier`. Классы: Light сильнее, HeavyII слабее.
3. **Персонаж стоя** — НЕ сносит (проверить: отпустить управление на открытой местности).
4. **Персонаж на бегу** — против ветра медленнее, по ветру быстрее.
5. **Персонаж в прыжке** — сносит по ветру (полный снос в воздухе).
6. `CharacterWindMultiplier = 0` → персонажей ветром не трогает; `ShipWindMultiplier = 0`
   → корабли реагируют только на зоны.

## Тюнинг
- Общий баланс — `ShipWindMultiplier` / `CharacterWindMultiplier` на WindManager.
- Точечно — `_globalWindForceScale` (корабль) / `_windDriftScale` (персонаж).
- Мягкость порывов у персонажа — `_windSmoothTime`.
- Вертикальный снос корабля — `_globalWindVerticalFactor`; резкость реакции — `windDecayTime`.
