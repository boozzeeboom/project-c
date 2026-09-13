# T-FO08A — Сдвиг домашней точки NPC (потеря внекорабельных NPC после F8)

Date: 2026-09-13. Первый тикет механик T-FO08 (корабли/остальные механики).

## Жалоба

Все NPC, размещённые вне кораблей, теряются после F8.

## Диагноз (по коду)

`NpcBrain._spawnPoint` ставится при спавне/ baseline (`Start`, строка ~543;
`OnGlobalBaselineApplied`, ~212) и никогда не сдвигается. После F8 тело NPC
уезжает с миром на `+T`, а точка остаётся: `distFromSpawn ≈ 56 км` →
срабатывают leash (`distFromSpawn > leashRange`, ~991), сброс цели (~1002),
`SetDestination(_spawnPoint)` (~1078), flee в точку (~964). NPC уходит
к досдвиговым координатам — «теряется». `NpcSocialBrain` читает
`_brain.SpawnPoint` live — чинится автоматически.

## Решение (1 строка + комментарий)

`NpcBrain.ApplyRebaseTranslation` дополнительно сдвигает `_spawnPoint`.
Обход slice (`ShiftCarryCaches`, оба пути, маркер `npcs=`) уже существует
с T-FO07E — изменений slice не потребовалось.

## Границы

- In-flight `SetDestination` досдвиговой эпохи: агент дойдёт до старой точки
  один раз, затем leash-логика с починенной точкой всё выровняет. Отдельного
  сброса целей нет.
- `_proxyLastPos` (sandbox) — не мировые данные, не трогаем.
- Проверка возможна только сценой с внекорабельными NPC в момент F8.

## Проверка

1. Compile: `refresh_unity` + `read_console` — 0 errors, 0 CS.
2. Play Mode (user): NPC вне кораблей → F8 → стоят/патрулируют рядом,
   `CarryCachesShifted(npcs>0)`, ухода вдаль нет.
