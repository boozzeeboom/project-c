# T-PERF03 — Iteration Log

База: `00_CAPTURE_2026-11-22_ANALYSIS.md` (захват `ProjectC_client_2026-11-22_10-39-14.data`).
План: `01_FIX_PLAN.md`. Play Mode агентом не запускался: runtime-проверка за пользователем.

## Фаза 1 — триггерный объём зон связи (P0-1) — `b2b1e7b9`

Файлы: `Assets/_Project/Scripts/Docking/Zones/OuterCommZone.cs`.

Что сделано: `Awake()` больше не ставит `_sphere.radius = commRange`; новый
`[SerializeField, Min(1f)] triggerRadius = 60f` — триггер отвечает только за мгновенную
реакцию на вход (`OnTriggerEnter`/`OnTriggerExit`, T-клавиша, подсказки), дальний детект
остаётся за поллом с debounce `MISS_THRESHOLD = 3`. Данные сцены не правились — радиус
приходит из кода.

Верификация: `check_compile_errors` — 0 errors.

Ручная проверка (пользователь, NOT RUN агентом): новый захват → `Physics.SendTriggerEvents`
падает с секунд/20–30 МБ на кадр до шума; T-клавиша, CommPanel и посадка работают.

## Фаза 2 — полл зон связи без физики (P0-3) — `d2616aa2`

Файлы: `Assets/_Project/Scripts/Docking/Zones/OuterCommZone.cs`.

Что сделано: два одинаковых `Physics.OverlapSphere(pos, commRange, -1, Ignore)` за тик
(41.38 КБ на зону `STN-PRM-001`, 5 304 попадания; 7 540 попаданий на все 9 зон) заменены
одним проходом по `NetworkManager.Singleton.SpawnManager.SpawnedObjects` с фильтром по
`pollLayerMask` и сравнением `sqrMagnitude` с `commRange²`. `_pollFound` разделён на
`_pollPlayersFound` / `_pollShipsFound` (`HashSet<ulong>`, `.Clear()`), диффы —
`ApplyPlayerDiff()` / `ApplyShipDiff()` с прежней семантикой debounce.

Почему не `OverlapSphereNonAlloc`: буфер 64–256 слотов при 5 304 попаданиях и маске `-1`
молча обрезает результат и может потерять игрока.

Верификация: `check_compile_errors` — 0 errors (после одного повтора: domain reload).

Ручная проверка (пользователь, NOT RUN): `IsPlayerInRange`, T-клавиша, докинг и
маркет-детект как раньше; GC зон → ~0.

## Фаза 3 — полл рыночных зон через NonAlloc (P0-3) — `2fb38061`

Файлы: `Assets/_Project/Trade/Scripts/Network/MarketZone.cs`.

Что сделано: per-instance `Collider[128]` + `Physics.OverlapSphereNonAlloc` в
`PollPlayersInRadius()` / `PollShipsInRadius()` вместо двух `OverlapSphere` на тик
(12 зон × 2 × 4 Гц = 96 массивов/с). Разные радиусы (`tradeRadius` / `shipDockRadius`)
сохранены, пропуск одного тика закрывает существующий debounce.

Верификация: `check_compile_errors` — 0 errors.

Ручная проверка (пользователь, NOT RUN): открытие маркета и покупка (`RequestBuyRpc`
не отвечает `NotInZone`).

## Фаза 4 — подсказка взаимодействия (P0-2) — `c893fb4a`

Файлы: `Assets/_Project/Scripts/Player/NetworkPlayer.cs` (19 вставок / 2 удаления).

Что сделано:
1. `UpdateInteractionHint()` вызывается не каждый кадр, а по
   `INTERACTION_HINT_INTERVAL = 0.2f` (5 Гц). В захвате резолвер стоил 11.87 ms и ~44 КБ
   на кадр: `FindObjectsByType<DoorController>` (2 800 дверей = 21.94 КБ) +
   `FindObjectsByType<NpcController>` + `GetComponent<Collider>` на каждую дверь.
   `PublishInteractionHint` уже дедуплицирует состояние — визуально частота не видна.
2. Строка `GlobalMotionRuntimeEvidenceProbe.RecordEvent("player", "Update.begin", $"…")`
   строится только при `IsActive`.

Верификация: `check_compile_errors` — 0 errors.

Ручная проверка (пользователь, NOT RUN): подсказки F/E появляются и исчезают без
заметной задержки; `NetworkPlayer.Update` без 44 КБ/кадр.

## Фаза 5 — кап регистрации навмеша палуб (P1-4) — `6ddb25f1`

Файлы: `Assets/_Project/Scripts/Ship/ShipDeckNav.cs` (15 вставок / 1 удаление).

Что сделано: `ProcessPendingRegistrations()` начинался с `s_registrationsThisFrame = 0;`
и вызывается из `LateUpdate` каждого корабля, поэтому `MAX_REGISTRATIONS_PER_FRAME = 1`
дежался только внутри одного экземпляра — 20 кораблей в одном кадре давали до 20 Add
(кадр 75: два `ShipDeckNav.LateUpdate` 87.05 и 62.28 ms, внутри `NotifyNavMeshAdded`
85.73 ms = `LogStringToConsole` 34.68 + `GC.Collect` 22.09). Заменено на метку кадра:
`s_registrationsFrame != Time.frameCount` → сброс. Контракты T-PERF02 (кулдаун 30 с,
`RestoreDone()`, `NotifyWorldRebased`, `DumpStats`/`ResetStats`) не тронуты.

Верификация: `check_compile_errors` — 0 errors.

Ручная проверка (пользователь, NOT RUN): `ShipDeckNav.DumpStats()` → `spawn ≈ 20,
drift ≈ 0`; экипаж ходит по палубам (первая регистрация волны теперь растянута на ~20
кадров); в новом захвате — не более одного `NotifyNavMeshAdded` на кадр.

## Фаза 6 — `verboseLogging` столкновений (P1-5) — без коммита

Файл: `Assets/Resources/ShipCollisionDamage.asset` (`verboseLogging: 1 → 0`, строка 19).

Что сделано: правка применена на диске через `SerializedObject`/`SaveAssets` (проверено
чтением: `1 → 0`). `ShipController.OnCollisionEnter` (`:648`) и `ShipHull` проверяют флаг
перед логом, поэтому уходит источник 55.03 ms / 32.01 КБ в кадре 76
(`LogStringToConsole`).

**Почему нет коммита:** `/[Aa]ssets/Resources/` целиком в `.gitignore` (строка 51),
огибающих правил нет, и `git log -- Assets/Resources/ShipCollisionDamage.asset` пуст —
файл в git никогда не лежал. Значит правка живёт только на этой машине. На чистом клоне
ассета нет вовсе, `ShipCollisionDamageConfig.Default` создаёт in-memory экземпляр, и там
`verboseLogging = false` по дефолту кода (T-SHIP-FIX12) — то есть тишина достигается и без
ассета. Ассет `Assets/Resources/ShipDamage.asset` отсутствует (fallback с дефолтами).
`Assets/Resources/` из `.gitignore` не выводился — это решение владельца репозитория.

Проверка: compile не затрагивался (только данные ассета).

Ручная проверка (пользователь, NOT RUN): столкновение без спама в консоли; урон и
протечки груза считаются как раньше (флаг влияет только на логи).

## Фаза 7 — журнал этапа (этот коммит)

Файлы: `docs/world/optimization/22112026/ITERATIONS.md` (новый),
`docs/world/optimization/22112026/01_FIX_PLAN.md` (раздел «Статус», уточнение Фаз 5 и 6),
`Assets/_Project/Docs/ITERATIONS.md`.

## Итог по коммитам

`147c4114` (разбор + план) → `b2b1e7b9` (Ф1) → `d2616aa2` (Ф2) → `2fb38061` (Ф3)
→ `c893fb4a` (Ф4) → `6ddb25f1` (Ф5) → Ф6 (локально, git-невидима) → этот коммит (Ф7).

## Протокол замера после правок (пользователь)

1. Перед Play: Console → Clear (иначе сравниваем накопленную историю — урок логов 5/6
   из `25092026`).
2. Тот же сценарий, что в исходном захвате: host-старт в `BootstrapScene`, полёт до
   коммуникационной зоны, вход/выход из зоны, посадка на корабль, столкновение, рынок.
3. Снять захват и сравнить: `Physics.SendTriggerEvents`, `NetworkPlayer.Update`,
   `OuterCommZone.Update`, `MarketZone.Update`, `ShipDeckNav.LateUpdate`.
4. Ожидания: `SendTriggerEvents` — из секунд в шум; `NetworkPlayer.Update` — без 44 КБ/кадр
   и без 11.87 ms; `OuterCommZone.Update` — без `Collider[]` (41.38 КБ); `MarketZone.Update`
   — GC ~0; `ShipDeckNav.LateUpdate` — не более одного Add на кадр.
5. Цифры записать сюда, в раздел «Замер после правок».
6. Попутно проверить поведение, а не только цифры: T-клавиша/CommPanel, докинг,
   маркет-покупка, экипаж на палубах, урон от столкновения.

## Замер после правок

NOT RUN (за пользователем).
