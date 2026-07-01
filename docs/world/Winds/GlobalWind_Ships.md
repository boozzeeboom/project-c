# Глобальный ветер → Корабли (WindManager × ShipController)

## Что это
Глобальная система ветра `WindManager` теперь влияет не только на облака, но и на
корабли. Ветер действует на `Rigidbody` корабля как физическая сила (снос по
направлению ветра). Все силы **настраиваются в инспекторе** — никакого хардкода.

## Архитектура
- **`WindManager`** (`Assets/_Project/Scripts/Core/WindManager.cs`) — синглтон
  (`ProjectC.Core`), единый источник правды. Поля:
  - `CurrentWindDirection` (нормализованный вектор направления);
  - `CurrentWindSpeed` (м/с, clamp 0..100).
  - Обновляется сервером через `ServerWeatherController` (ClientRpc broadcast).
- **Потребители ветра:** облака (`CloudManager`, `VeilRaymarchBlit`,
  `ServerStormManager`), HUD (`ShipHudController`) и теперь — корабли
  (`ShipController`).
- **Локальные зоны `WindZone`/`WindZoneData`** остаются независимой системой
  (триггер-объёмы, расставляются вручную / генеративно позже). Глобальный ветер
  и зоны **складываются аддитивно**.

## Реализация в ShipController
Файл: `Assets/_Project/Scripts/Player/ShipController.cs`

Метод `ApplyGlobalWind(float dt)` вызывается из `FixedUpdate` (server-only) сразу
после `ApplyWind(dt)` (шаг 9.65). Логика:

1. Если `_globalWindEnabled == false` → сила плавно гасится к нулю, выход.
2. Читается `Core.WindManager.Instance` (null-guard + дроссель-предупреждение).
3. Направление берётся из `CurrentWindDirection`; вертикаль приглушается
   множителем `_globalWindVerticalFactor` (по умолчанию 0 = только горизонт,
   чтобы не конфликтовать с системой коридоров высот), затем ре-нормализуется.
4. Целевая сила (Н) = `dir * CurrentWindSpeed * _globalWindForceScale`.
5. Плавный `Lerp` к целевой силе по `windDecayTime` (без рывков при смене ветра).
6. Итог = `сила * windInfluence * (windExposure + _moduleWindExposureMod)` и
   применяется через `Rigidbody.AddForce(..., ForceMode.Force)`.

Server-only: `FixedUpdate` выходит при `!IsServer`; `NetworkTransform`
реплицирует результат клиентам.

## Настройки в инспекторе (Header «Глобальный Ветер (WindManager)»)
| Поле | Тип | Дефолт | Назначение |
|------|-----|--------|------------|
| `_globalWindEnabled` | bool | `true` | Вкл/выкл влияние глобального ветра на этот корабль. |
| `_globalWindForceScale` | float | `8` | Ньютонов на 1 м/с скорости ветра. Главный «руль» силы сноса. |
| `_globalWindVerticalFactor` | float [0..1] | `0` | Доля вертикальной составляющей (0 = только горизонт; 1 = полный 3D-снос). |

Дополнительно переиспользуются существующие поля (Header «Ветер и Окружающая Среда»):
- `windInfluence` — общий множитель влияния ветра (и зон, и глобального).
- `windExposure` — класс-специфичная экспозиция (Light 1.2 … HeavyII 0.5,
  задаётся в `ApplyShipClass`). Модули добавляют `_moduleWindExposureMod`.
- `windDecayTime` — время сглаживания/затухания силы.

## Формула
```
dir    = normalize(WindManager.CurrentWindDirection with y *= verticalFactor)
target = dir * WindManager.CurrentWindSpeed * globalWindForceScale      // Н
smooth = Lerp(prev, target, dt / windDecayTime)
force  = smooth * windInfluence * (windExposure + moduleWindExposureMod) // Н
Rigidbody.AddForce(force, Force)
```

## Проверка
1. В Play (host/server) изменить ветер через погодный контроллер (или вручную
   выставить `CurrentWindSpeed` / `CurrentWindDirection` в `WindManager`).
2. Корабль без ввода пилота должно плавно сносить по направлению ветра; сила
   растёт со `speed` и `_globalWindForceScale`.
3. Класс: Light (экспозиция 1.2) сносит сильнее, HeavyII (0.5) — слабее.
4. `_globalWindEnabled = false` → глобальный снос прекращается (плавное
   затухание), локальные `WindZone` продолжают работать.
5. Консоль: раз в ~1 c лог `[ShipController:...] GlobalWind dir=... speed=... force=...N`;
   предупреждение, если `WindManager.Instance == null`.

## Тюнинг
- Слишком сильный/слабый снос — крутить `_globalWindForceScale` (глобально для
  инстанса) или `windInfluence`.
- Нужен вертикальный снос (восходящие потоки) — поднять `_globalWindVerticalFactor`.
- Резкие рывки при смене погоды — увеличить `windDecayTime`.
