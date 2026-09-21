# Floating Origin — читаемый указатель (2026-09-14)

Хост-одиночка (сервер-хост + 1 клиент): код закрыт, приёмка — за ручными
тестами. Этот файл — точка входа. Детали — по ссылкам, процессный шум —
в `archive_floatingoriginintegration/`.

## Как это работает сейчас (одним абзацем)

Мир сдвигается целиком транзакцией `GlobalMotionControlledRebaseSlice`
(F8 вручную, авто — отлёт >256м от origin, опрос 5с, кулдаун 35с, галка
`Auto Rebase Enabled`; F9 — откат): корни сцен + игроки едут `position += T`
с серверным `NetworkTransform.Teleport` (`NetworkPublished`), локальные
кэши (deathY, камера+collision, платформа, carry пикапов/NPC, шторма, AABB
ветра, коридоры высот, fallback-точки) сдвигаются тем же `T` под флагами,
вторым клиентам шлётся broadcast `FO06_REBASE_SHIFT` (±T), откат возвращает
и книги (`_frame`, кумулятив сейва). Маркер успеха — `runtimeRebase.Completed`,
пилот на месте — `ActorRebound(ok=True)`.

## Читать по ролям

| Нужно | Файл |
|---|---|
| План, статусы §3.1, остаток работ | `00_ARCHITECTURE_AND_PLAN.md` |
| Живой статус приёмки (что PASS/OPEN) | `09A_ACCEPTANCE_MATRIX.md` |
| Закрывающее ревью (все дефекты, вердикт) | `09O_FULL_REVIEW_AND_POLISH.md` |
| Транзакция: контракт + границы + вертикальный слайс | `06N_REBASE_TRANSACTION_CONTRACT.md`, `06Z_REBASE_TRANSACTION_BOUNDARY_CONTRACT.md`, `06CY_CONTROLLED_REBASE_VERTICAL_SLICE.md` |
| Сейвы при сдвиге (кумулятив, restore) | `PERSIST01_REBASE_AWARE_RESTORE.md`, `05A_PLAYER_GLOBAL_PERSISTENCE.md` |
| Фиксы сдвига (что едет с миром) | `06CZ`, `06DA`, `06DB`, `06DC`, `06DD`, `06DF`, `06DG`, `07A`, `07B`, `07C`, `07E`, `07F`, `08A`, `08B`, `08D`, `09G`, `09K`, `09L`, `09M` |
| Авторебейс | `09B_AUTO_REBASE_IMPLEMENTATION.md` |
| Спавн/спасение/посадка | `09D_RESCUE_POINT_VOID.md`, `09F_SPAWN_ANCHOR_FIELD.md`, `09H_BOARD_AFTER_RESTART.md`, `09H_BOARD_FREEZE_FIX.md`, `09J_SPAWN_FRAME_EXTENT.md`, `09N_RESCUE_TO_DEFAULT_SPAWN.md`, `07H_SEATED_PLAYER_INPUT_GATE.md` |
| Корабли: почему rebind в полёте отказывает, что дальше | `08C_SHIP_ADAPTER_DESIGN.md` (включая свёртку 07I/07J) |
| Фундамент (ядро, транспорт, адаптеры, спавн, сцены) | `01_AUDIT_AND_SOURCES.md`, `02_FOUNDATION_IMPLEMENTATION.md`, `03_BOUNDARY_REVIEW.md`, `04A`–`04I`, `05B`–`05E` |
| Пилот T-FO06 (scope, префаб, реестр, сцены, static world) | `06A`–`06M` (кроме архивных) |
| Новый контент на сцену (пайплайн, маркеры, каталог, digest) | `09S_NEW_CONTENT_PIPELINE.md` (отменяет регламент 09R для NO-объектов) |
| Dormant контракты (NetworkBaseline, пассажиры, манифесты) | `06AG`, `06AP`, `06AT`–`06AZ`, `06BC`–`06CW`, `06N`, `06O`, `06P`, `06Q`–`06W`, `06Z_*` |

## Что не закрыто (код не требуется, кроме приёмки)

1. Ручные тесты хоста: far-repeat кулдауна, посадка/выход в момент сдвига,
   шторм, docking/recall/autopilot/торговля, recover-ветка в дикой природе.
2. Контент сцены: `Respawn_Default` висит в воздухе (09D — чинить в редакторе).
3. Снятие `NOT READY` (§4) — отдельным решением после п.1.

## Отложено (не хост-скоп)

- Мультиплеер v2 (топология, late join, owner-rules) — `06DH` dormant.
- Ship-adapter v2 (миграция NT→Replicator) — `08C`.
- Initial-rebase между сессиями, spawn-from-save рантайм-кораблей.

## Архив

`archive_floatingoriginintegration/` — всё процессное без ценности для
понимания текущего кода, история сохранена: серийные capture/evidence-снимки
(06Y/06Z/06U/06X…), микрошаги readiness-цепочки (06AB–06AO, свёртки в
06AG/06AP), superseded (09C отменён 09N; 09I разовый разбор; 09E→09D;
09B_PLAN→09B; 07D/07G→09O; 07I/07J→08C), машинные `evidence_json/` (117 шт.),
`*.csv` census, разовые Editor-аудиты `tools/` (dormant, не удалять).
