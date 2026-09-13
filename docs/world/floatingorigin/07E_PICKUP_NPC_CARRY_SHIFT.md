# T-FO07E — Сдвиг кэшей carry пикапов и NPC (пропажа пикапов при F8)

Date: 2026-09-13.

## Жалоба

При сдвиге визуально теряются pickable предметы: исчезают (или остаются
в старых координатах — проверить старые корды невозможно).

## Диагноз (по коду, тот же класс что DG у игрока)

`PickupDeckRide.LateUpdate` (L3 carry): `delta = platform.new − _platformLastPos.old`.
Slice двигает и платформу, и pickup на `+T`, а `_platformLastPos` остаётся
досдвиговым. Первый LateUpdate после F8 прибавляет `≈T` (двойной сдвиг) —
pickup улетает с палубы в пустоту. Свободный режим самовосстанавливается
(`RefreshWorldBase` каждый кадр), страдают только прикреплённые к палубам.

Тот же паттерн у NPC fallback-carry (`NpcBrain._rideLastPos`, server-side).
Parented/proxy пути (видимый экипаж на палубах) кэш лишь перечитывают —
не страдают, но метод добавлен и им (безвредно).

## Решение (скопом, best-effort)

- `PickupDeckRide.ApplyRebaseTranslation` / `NpcBrain.ApplyRebaseTranslation`:
  сдвиг кэша платформы (ротация не меняется).
- Slice (сервер, оба пути, синхронно до кадров): обход поддеревьев участников,
  маркер `CarryCachesShifted(pickups=N;npcs=M)`.
- Второй клиент: carry пикапов считается локально на каждом пире —
  scene-wide сдвиг в `OnRebaseShiftMessage` (счётчик `pickups=` в
  `ClientShiftApplied`); NPC-carry серверный, клиент не трогает.

## Границы

- База бобаинга самовосстанавливается — не трогаем.
- Проверка возможна только сценой с пикапами на палубе в момент F8.

## Проверка

1. Compile: `refresh_unity` + `read_console` — 0 errors, 0 CS.
2. Play Mode (user): пикапы на палубе → F8 → предметы на месте,
   `CarryCachesShifted(pickups>0)`.

## Верификация 2026-09-13 (ф9_14.txt) — PASS

Полный success-путь: `CarryCachesShifted(pickups=27,npcs=36)`,
`ParticlesCleared(n=1)`, ошибок 0. Визуально: предметы перемещаются,
остаются видимыми, подбираются. `InteractableManager` проверен отдельно:
ссылки на объекты + live-дистанции, кэша позиций нет. T-FO07E закрыт полностью.
