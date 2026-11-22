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

## Замер после правок — `ProjectC_client_2026-11-22_11-00-03.data`

Файл содержит ДВЕ сессии в одном буфере профайлера (637 кадров) — это готовое A/B:

| Кадры | Сессия | Границы |
|---|---|---|
| 0..333 | до правок T-PERF03 | f0 — старт Play (6 982 ms), f333 — стоп Play + domain reload (23 257 ms) |
| 334..636 | после правок | f334 — старт Play (6 309 ms), f636 — стоп (22 947 ms) |

Граница проверена по дереву сэмплов, а не по времени:
- A: `OuterCommZone.Update` содержит `Physics.OverlapSphere` в 123 кадрах; `OverlapSphereNonAlloc` в `MarketZone.Update` — ни в одном.
- B: `Physics.OverlapSphere` под `OuterCommZone.Update` — 0 кадров; `Physics.OverlapSphereNonAlloc` под `MarketZone.Update` — 87 кадров.

Метод: `ProfilerDriver.LoadProfile` + `GetHierarchyFrameDataView` (main thread, merge по имени),
агрегация self-time и GC по всем кадрам. Сравниваются кадры 1..332 против 335..635 (старт/стоп исключены).

| Метрика на кадр | A (до) | B (после) | Δ |
|---|---|---|---|
| `PlayerLoop` всего, медиана | 99.0 ms | 78.0 ms | −21% |
| GC на кадр, медиана | 171.1 KB | 101.8 KB | −40% |
| `Physics.SendTriggerEvents` self | 27.6 ms (p90 52.9) | 24.2 ms (p90 64.4) | −12% |
| `NetworkPlayer.Update` self | 10.6 ms (p90 13.9) | 2.8 ms (p90 11.6) | −74% ✅ Ф4 |
| `OuterCommZone.Update` CPU | 7.6 ms avg | 2.6 ms avg | −66% ✅ Ф2 |
| `OuterCommZone.Update` GC | 38.3 KB avg | 0.2 KB avg | −99.5% ✅ Ф2 |
| `MarketZone.Update` GC | 6.9 KB avg | 0.3 KB avg | −96% ✅ Ф3 |
| `GetComponentNullErrorMessage` GC | 37.5 KB med | 30.2 KB med | −19% |
| пик GC `ShipDeckNav.LateUpdate` | 16 263 KB (f75) | 952 KB | волна рассыпана ✅ Ф5 |
| `LogStringToConsole` GC | 92 KB avg | 96 KB avg | без изменений |
| триггер-событий (зоны), вызовов/кадр | 15.6 | 11.6 | −26% |

Вердикты:
1. **Ф2, Ф3, Ф4 подтверждены замером** — именно те метрики, на которые они были нацелены.
2. **Ф5 частично:** пик волны регистрации упал с 16 263 до 952 KB, но суммарный GC `ShipDeckNav`
   тот же (53.5 KB/кадр): 42 регистрации за сессию остались, изменилось только их размазывание
    (причина — дрейф, тема T-PERF02, не этого этапа).
3. **Ф1 (триггерный объём) дала только −12% по `SendTriggerEvents`.** Объём событий задают
   не зоны связи: в сессии B 107 123 триггер-колбэка за 301 кадр (356/кадр), из них 99 214 (93%)
   — `ShipController` + `ShipCargoConsole`, а зоны `OuterCommZone` — только 6 971 (23/кадр).
4. **`Physics.SendTriggerEvents` остался главным пожирателем кадра:** 24.2 ms медиана,
   в 303/303 кадрах > 10 ms, 10 526 ms за сессию (35 ms/кадр). Собственные колбэки при этом
   стоят ~0.3 ms/кадр — значит цена сидит в нативном диспетчере событий (~100 µs на событие).
   Рычаг — сокращать КОЛИЧЕСТВО событий, не их обработку.
5. Побочно: порог троттлинга подсказки сработал в 100/301 кадров — потому что Editor в этом
   захвате идёт ~10 fps (100 ms/кадр) и `INTERACTION_HINT_INTERVAL = 0.2 s` попадает почти
   в каждый второй кадр; при 60 fps это ~8% кадров.

## Остаток после правок — что теперь первым

| Приоритет | Замер (сессия B, 301 кадр) | Источник в коде |
|---|---|---|
| P0 | `GetComponentNullErrorMessage` = 95.4 MB из 194.4 MB всего GC сессии (49%); из них 60.4 MB под четырьмя триггер-колбэками: `ShipController.OnTriggerExit` 22.9 MB / 37 589 вызовов, `OnTriggerEnter` 14.8 MB / 24 355, `ShipCargoConsole.OnTriggerExit` 14.3 MB / 23 063, `OnTriggerEnter` 8.4 MB / 13 551 | `ShipController.cs:600,609` и `ShipCargoConsole.cs:89,97`: `other.GetComponent<CharacterController>() != null` на каждом чужом коллайдере. 1:1 с сообщением (37 589 сообщений на 37 807 вызовов) |
| P0 | 356 триггер-событий на кадр, `Physics.SendTriggerEvents` 35 ms/кадр | объём триггеров корабля/консоли: 20 кораблей летают по миру с 21 842 коллайдерами; радиус консоли — `_interactionRadius` из инспектора, у `ShipController` триггеры в префабе |
| P1 | `PxScene.simulate` 7.5 ms/кадр, `ScScene.updateCCDSinglePassStage3` 1.7, `SwapPhysXBuffersJob` 1.0, `WaitForJobGroupID` 1.4 | 21 842 коллайдера, матрица слоёв/статик |
| P1 | `LogStringToConsole` 96 KB/кадр, из них `NavMeshManager.NotifyNavMeshAdded` 16.7 MB / 519 записей | Unity-логи внутри `NavMesh.AddNavMeshData`; 42 регистрации за сессию (дрейф палуб, тема T-PERF02), только Editor |
| P2 | `DockingPadTriggerBox.OnTriggerEnter` 277 ms / 111 вызовов = 2.5 ms/вызов | `Assets/_Project/Scripts/Docking/Stations/DockingPadTriggerBox.cs` |
| P2 | `NpcShipWorld.FixedUpdate` 1.7 ms/кадр, `WindManager.FixedUpdate` 1.0, `ShipShadowBudget.Update` 0.8, `PRIMUM/LightPool.Check` 0.85, `HouseDiscovery.Update` 0.8 | без правок в этом этапе |
| — | `EditorLoop` 113 ms/кадр, `Profiler.FlushMemoryCounters` 7.3, `Mono.JIT` 3.8 | артефакт профилирования в Editor, в билде нет |

Оговорка про GC-проценты: сумма 194.4 MB/301 кадр включает стартовые пики (f334 — 23.6 MB);
медиана кадра — 102 KB, из них 30 KB в медиане — те же null-сообщения GetComponent.

## Замер 3 — `ProjectC_client_2026-11-22_11-18-18.data` (тот же код, что в сессии B)

350 кадров, одна сессия: f0 — старт Play (6 567 ms), f349 — стоп (22 539 ms);
сравниваются кадры 1..348. Код тот же, что в сессии B предыдущего захвата (проверено деревом:
`Physics.OverlapSphere` под `OuterCommZone.Update` — 0 кадров, `OverlapSphereNonAlloc` под
`MarketZone.Update` — 84 кадра). Значит разница между B и C — это разброс сценария, а не правки.

| Метрика на кадр | A (до) | B (11:00) | C (11:18) |
|---|---|---|---|
| `PlayerLoop` медиана | 99.0 ms | 78.0 ms | 67.5 ms |
| GC медиана | 171.1 KB | 101.8 KB | 85.8 KB |
| `Physics.SendTriggerEvents` | 27.6 / avg 36.1 | 24.2 / avg 35.0 | 19.2 / avg 27.8 |
| `NetworkPlayer.Update` | 10.6 / avg 10.1 | 2.8 / avg 5.2 | — / avg 4.35 |
| `OuterCommZone.Update` GC | 38.3 KB | 0.2 KB | 0.15 KB |
| `MarketZone.Update` GC | 6.9 KB | 0.3 KB | 0.30 KB |
| `ShipDeckNav.LateUpdate` GC | 53.6 KB | 53.5 KB | 25.7 KB |
| `LogStringToConsole` GC | 92 KB | 96 KB | 44.3 KB |
| всего GC за сессию | — | 194.4 MB | 128.0 MB |
| `GetComponentNullErrorMessage` | 37.5 KB med | 30.2 KB med | 202.9 KB avg / 111 853 вызова |

Вердикты по C:
1. Зоны и рынки остаются чистыми: 0.15 и 0.30 KB/кадр. Регрессий нет.
2. `Physics.SendTriggerEvents` — по-прежнему #1 в CPU: 9 680 ms за 348 кадров (27.8 ms/кадр),
   медиана 19.2 ms, p90 41.4. Триггер-колбэков 111 853 — тот же класс, что в B.
3. `GetComponentNullErrorMessage` — **70.6 MB из 131.0 MB всего GC (54%)**, из них
   93% (65.7 MB) под теми же четырьмя колбэками: `ShipController.OnTriggerExit` 24 969 KB /
   37 770, `OnTriggerEnter` 16 080 KB / 24 434, `ShipCargoConsole.OnTriggerExit` 15 542 KB /
   23 188, `OnTriggerEnter` 9 112 KB / 13 629. Второй независимый замер с тем же выводом.
4. Новое: `DockingPadTriggerBox.OnTriggerEnter` — 250 ms / 111 вызовов = **2.26 ms на вызов**
   (в B было 277 ms / 111 — стабильно). Внутри метода только `FindObjectsOfType` (2.2–3.3 ms
   на кадр) и `other.GetComponentInParent<ShipController>()`, т.е. цена сидит в нативном
   `GetComponentInParent` на чужих коллайдерах (`DockingPadTriggerBox.cs:48`).
5. В этом захвате включена запись callstack'ов профайлера (`Profiler.Callstack` 7.4 ms/кадр),
   поэтому абсолютный CPU здесь завышен; на GC и счётчики вызовов это не влияет.
6. `mscorlib.dll!System::Object.wbarrier_conc()` 549 ms (1.58 ms/кадр) — плата за аллокации,
   отдельным пунктом не считается: уйдёт вместе с мусором.

Итог по замерам 2 и 3: из семи фаз T-PERF03 невыжатыми остались две метрики
(`Physics.SendTriggerEvents` и `GetComponentNullErrorMessage`), и обе ведут к одному месту —
триггеры корабля и грузового консоли.
