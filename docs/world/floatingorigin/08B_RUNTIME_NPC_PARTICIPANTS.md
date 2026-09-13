# T-FO08B — Рантайм-NPC как участники сдвига (потеря городских NPC после F8)

Date: 2026-09-13. Жалоба: NPC, гулявшие по городу, пропадают после F8
(лог `ф8_17.txt`), несмотря на T-FO08A (сдвиг `_spawnPoint` уже был).

## Диагноз по ф8_17 (точечно)

- Цепочка транзакции полная: `Requested→Validated→NetworkPublished→
  RespawnShifted→CameraShifted→DecksNotified→CarryCachesShifted(pickups=27,
  npcs=36)→ParticlesCleared→BroadcastShifted→WorldFrameShifted→Completed`.
  Ошибок 0.
- Новых ошибок NPC/агентов после `Completed` нет: все `Failed to create
  agent` — досдвиговый legacy из `ShipDeckNav` (строки < 1882, `Completed`
  на 5627).
- Зато сразу после `Completed` (строки 5632+) — серия `Failed to create
  agent because it is not close enough to the NavMesh` из
  `NavMeshSurface.cs:221` (`AddNavMeshData`): навмеш-поверхности города,
  лежащие под сдвинутыми корнями WorldScene, перерегистрировались
  в новых координатах, а чьи-то агенты остались далеко.
- Причина: `NpcSpawner` инстанцирует NPC в активную сцену (сам спавнер —
  scene-placed корень BootstrapScene), т.е. НЕ под корни WorldScene.
  Участники slice — только корни WorldScene + игроки. Тела городских NPC
  после F8 остались в старых координатах (~56 км от уехавших города
  и навмеша) — визуально «пропали». `CarryCachesShifted(npcs=36)` их тоже
  не покрывал: обход идёт только по поддеревьям `_participants`,
  поэтому и сдвиг `_spawnPoint` из 08A до них не доходил.

## Решение (3 правки, best-effort, флаг не нужен — участники обратимы)

1. `GlobalMotionControlledRebaseSlice.CollectParticipants`: все
   `NpcBrain` сцены, не покрытые зарегистрированными корнями
   (`IsContainedByRegisteredRoot` отсекает палубных — они едут
   с кораблём, двойного сдвига нет), регистрируются как
   `NPC_RUNTIME/<name>` kind `NetworkGameplayRoot`. Едут штатным путём
   (`position += T` + `PublishNetworkTeleport`), rollback обратный
   автоматически.
2. `NpcBrain.ApplyRebaseTranslation`: после сдвига тела — `_agent.Warp`
   (внутренняя позиция агента stale, иначе утащит NPC назад; гвард
   `enabled && isOnNavMesh`, off-mesh палубные пропускаются) + проброс
   сдвига в social.
3. `NpcSocialBrain.ApplyRebaseTranslation` (новый): сдвиг печёного массива
   `patrolWaypoints` (позиции запечены спавнером на момент спавна).
   Live-маркеры `patrolWaypointMarkers` читаются по месту и не трогаются.

Попутно: `using System;` в NpcBrain дал конфликт `Random`
(`CS0104`) — откачен, catch через `System.Exception`.

## Проверка

1. Compile: `refresh_unity` (force + compile) — PASS; `read_console` —
   0 errors, 0 CS (после правки конфликта).
2. Play Mode (user): городские NPC → F8 → на месте, гуляют дальше;
   в логе участники `NPC_RUNTIME/*`, `CarryCachesShifted(npcs=...)`
   вырос, серии `not close enough` после `Completed` нет.
