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

## T-PERF04 — `TryGetComponent` в четырёх триггер-колбэках (коммит `a2bc7c06`)

Правка: `ShipController.OnTriggerEnter/OnTriggerExit` и `ShipCargoConsole.OnTriggerEnter/OnTriggerExit`
проверяют игрока через `private static bool IsPlayerCollider(Collider other)
=> other.CompareTag("Player") || other.TryGetComponent(out CharacterController _);` — вместо
`other.GetComponent<CharacterController>() != null`. Семантика 1:1 (короткое замыкание по тегу
сохранено, проверка идёт по тому же GameObject), но `TryGetComponent` на отсутствующем
компоненте не строит null-сообщение. Рабочий документ — `02_TRIGGER_CALLBACKS_FIX.md`.

Проверка: `check_compile_errors` — 0 errors (первый вызов упал на domain reload, повтор чистый).
Play Mode / новый захват — NOT RUN, за пользователем; протокол — в `02_TRIGGER_CALLBACKS_FIX.md`.

Ожидание на следующем замере: `GetComponentNullErrorMessage` пропадает из-под этих четырёх
колбэков (было 65.7 MB/сессию = 50% всего GC), итоговый GC сессии ~131 MB → ~65 MB,
GC в среднем 376.5 → ~190 KB/кадр. `Physics.SendTriggerEvents` (27.8 ms/кадр) и `PlayerLoop`
почти не изменятся — это объём событий и CPU, а не мусор.

## Замер 4 — `ProjectC_client_2026-11-22_11-31-27.data` (после T-PERF04)

488 кадров: f0 — старт Play (6 115 ms), f487 — стоп (26 895 ms), сравниваются 1..486.
Код — с правкой T-PERF04 (маркеры: `Physics.OverlapSphere` под `OuterCommZone.Update` — 0 кадров,
`OverlapSphereNonAlloc` под `MarketZone.Update` — 148 кадров). Запись callstack'ов выключена
(`Profiler.Callstack` — 0 сэмплов), т.е. CPU-числа сопоставимы с A/B из замера 2.

| Метрика | C (до T-PERF04, 348 кадров) | D (после, 486 кадров) |
|---|---|---|
| всего GC за сессию | 131 024 KB | 98 622 KB |
| GC в среднем на кадр | 376.5 KB | **202.9 KB (−46%)** |
| GC медиана / p90 | 85.8 / 195.3 | 99.3 / 195.4 |
| `GetComponentNullErrorMessage` | 202.9 KB/кадр, 111 853 вызова | **31.25 KB/кадр, 23 047 (−85%)** |
| GC четырёх триггер-колбэков | 65 703 KB | **0 KB** |
| вызовов в этих четырёх | 99 021 | 102 925 (+3.9%) |
| `PlayerLoop` медиана | 67.5 ms | 68.3 ms |
| `Physics.SendTriggerEvents` мед/сред | 19.2 / 27.8 ms | 20.2 / 27.57 ms |
| `NetworkPlayer.Update` сред | 4.35 ms | 6.12 ms |
| зоны / рынки GC | 0.15 / 0.30 KB | 0.14 / 0.24 KB |
| `ShipDeckNav.LateUpdate` GC | 25.7 KB | 34.2 KB |
| `LogStringToConsole` GC | 44.3 KB | 61.3 KB |

Вердикты по D:
1. Правка подтверждена точно: `ShipController.OnTriggerEnter/OnTriggerExit` и
   `ShipCargoConsole.OnTriggerEnter/OnTriggerExit` — **0 KB GC** при 102 925 вызовах
   (было 65 703 KB на 99 021). Работы стало на 3.9% больше, мусора не стало вовсе.
2. Остаточные 15 187 KB null-сообщений (23 047) — другого происхождения: `GlobalMotionWorld.FixedUpdate`
   5 888 KB / 8 932, `GlobalMotionPoseAdapter.LateUpdate` 1 825 / 2 754, `NpcBrain.Update` 1 499 / 2 376,
   `NetworkPlayer.FixedUpdate` 1 496 / 2 233, `GlobalMotionWorld.Update` 1 208 / 1 832,
   `NetworkTickSystem.Tick` 946 / 1 412 — тот же класс бага в FO-слое.
3. CPU не сдвинулся (как и прогнозировалось): `PlayerLoop` 67.5 → 68.3 ms, `SendTriggerEvents`
   27.8 → 27.57 ms. Правка была про мусор, а не про время кадра.
4. Новый расклад GC по источникам — `LogStringToConsole` 61.3 KB/кадр (28 MB за сессию, 30% всего GC,
   1 076 записей, ~26 KB на `Application.CallLogCallback`; 661 запись со `ExtractStackTrace`),
   `CoroutinesDelayedCalls` 47.0 (из них `GlobalMotionPilotRuntime.PrepareAndStartHost` 29.27 KB/кадр
   при **4 вызовах** — то есть спавн-пики, а не ровная цена), `ShipDeckNav.LateUpdate` 34.2
   (9 720 вызовов = 20 палуб × кадр), `NpcShipWorld.FixedUpdate` 19.0, `GlobalMotionWorld.FixedUpdate` 14.1.
5. Медиана GC выросла (85.8 → 99.3), хотя среднее упало вдвое: до правки null-сообщения шли пачками
   под триггерами, теперь типовой кадр — это уже не они. p90 одинаковый (195.3 → 195.4).


Не сделано сознательно: объём триггеров (см. п.1 «Остаток» в рабочем документе) — требует правки
геометрии триггерных коллайдеров на префабе корабля и `_interactionRadius` консоли, т.е. данных
префаба, а не кода.

## T-PERF05 — null-`GetComponent` в FO-слое и разбор консоли (коммит `81b7ec8d`)

Код: `GlobalMotionPoseAdapter` — `_transport`, `_body`/`_controller`/`_agent` (в `Bind` и
`SupportedStructure`) и `HasCompetingWriter` переведены на `TryGetComponent`. Это ~7.7 MB
null-сообщений за сессию D (`GlobalMotionWorld.FixedUpdate` 5 888 KB / 8 932,
`GlobalMotionWorld.Update` 1 208 / 1 832, `NetworkTickSystem.Tick` 946 / 1 412) = 8% всего GC.
Ожидание: `GetComponentNullErrorMessage` 31.25 → ~15 KB/кадр. compile 0 errors.

Консоль (1 391 запись, сохранённый буфер): 671 строка (48%) — нативное предупреждение
`Failed to create agent because it is not close enough to the NavMesh`, стека нет, из кода не
глушится; пик шума — кадры f27 (119) и f28 (325), где `ShipCrewSpawner.SpawnWhenReady()` ×20.
Наши логи — 5–6 info-строк на корабль при спавне (~400 записей); вариант гашения одним флагом
`ProjectLog.Verbose` предложен, решение за пользователем. Разбор — `03_FO_NULL_COMPONENT_AND_CONSOLE.md`.

Не сделано: 671 нативное предупреждение (варианты A/B в рабочем документе — все требуют
прогона или меняют FO-контракт `CanApplyGlobalBaseline`); гашение наших info-логов (8 файлов);
остаток null-сообщений `NetworkPlayer.FixedUpdate` 1 496 KB, `NpcBrain.Update` 1 499 KB, Skills ~0.9 MB.

## T-PERF05/C — разовая диагностика off-mesh агентов

Файл: `Assets/_Project/Scripts/Debug/NavMeshAgentAudit.cs` (новый, namespace `ProjectC.Diagnostics`).
compile — 0 errors. Play Mode **не запускался**, прогон за пользователем — без него вердикт по B не выносится.

Что делает: само-устанавливается при входе в Play Mode, делает два снимка (8 с и 20 с) и пишет
на каждый одну запись `[PerfNavAudit]` — всего/включённых/на навмеше/**вне навмеша** агентов,
разбивка вне-навмеша по именам (префабам), признак «в иерархии корабля», результат
`NavMesh.SamplePosition` в радиусе 60 м и до 5 примеров координат.

Зачем именно так: 671 сообщение из консоли невидимо профайлеру — оно нативное и без стека;
единственный способ отличить «агент создан вне навмеша один раз» от «агент живёт вне навмеша
и переоценивается на каждом чужом `AddNavMeshData`» — посчитать живые объекты в рантайме.

Уточнение по варианту B из разбора префабов (важно для следующего шага):
- префабов капитанов **20**, не 23 (23 — это число кораблей в мире, оттуда 23× `[ShipController]`);
- все 20 — варианты ОДНОГО базового `Assets/_Project/Prefabs/NPC/[Mira] - DON`T DELETE DEFOULT.prefab`
  (guid `172a127fa0f35124bb11eda19a17b16e`), где и лежит `NavMeshAgent` с `m_Enabled: 1`;
- тот же базовый префаб используется сценовыми квесторами в `WorldScene_0_0.unity` (`[Onboarding alfa]`,
  `Q001_NPC_Lyra__Lyra`) и `Gorgona_Pilot` — им агент нужен, поэтому B — это override `m_Enabled: 0`
  в каждом из 20 файлов вариантов, а НЕ правка базового префаба.

Протокол для пользователя (один прогон): Console → Clear → Play → подождать 25 с → прислать две
строки `[PerfNavAudit]`. Подробности и таблица расшифровки — в `03_FO_NULL_COMPONENT_AND_CONSOLE.md`.

## T-PERF05/B — гашение `NavMeshAgent` у префабов капитанов

Решение принято по аудиту C: на 20-й секунде 14 капитанов стоят вне навмеша на палубах (`onShip=14`),
у них `updatePosition=false` (`NpcBrain.BeginRide`), но `enabled=true` — живые зарегистрированные
агенты и есть источник 671 нативного сообщения. На 8-й секунде капитанов ещё нет (`agents=25`,
`onShip=0`) — они совпадают с пиком логов f27/f28.

Правка: в 20 файлах `Assets/_Project/Prefabs/NPC/Ships/Captains/*.prefab` добавлен override
`m_Enabled: 0` на `NavMeshAgent` базового префаба (fileID `6435950676967608688`). Базовый
`[Mira] - DON`T DELETE DEFOULT.prefab` не тронут: его агент нужен сценовым квесторам
(`Q001_NPC_*`, `[Mira]`, `[Onboarding alfa]`) и `Gorgona_Pilot`. Diff: +4 строки × 20 файлов (80 вставок),
проверка `AssetDatabase` — `NavMeshAgent.enabled == false` у 20 из 20, у базового — `true`.

Безопасность (проверено по коду и ассетам):
- 0 ссылок на guid капитанов в `*.unity`/`*.prefab` (скан 231 файла) — в сцене их нет, только `ShipCrewSpawner`;
- `AddComponent<GlobalMotionPoseAdapter>` в проекте отсутствует, в базовом префабе FO-компонентов
  нет ⇒ капитаны не участники FO, `CanApplyGlobalBaseline` для них не вызывается (опасение варианта A
  сюда не переносится);
- все `_agent.SetDestination/ResetPath/isStopped/Warp/velocity` в `NpcBrain` стоят под `_agent.isOnNavMesh`;
- палубная навигация — через `_proxyAgent` (`EnsureProxy` под `_deckNav.IsReady`).

Проверка compile: 0 errors. Play Mode — NOT RUN, за пользователем: консоль без `Failed to create agent`,
экипаж ходит по палубам, `[PerfNavAudit]` на 20 с без `*_Captain` в `off-mesh by name`.
Оговорка: `NpcBrain.Respawn` включает `_agent.enabled = true`; у капитанов `_respawnEnabled = 0`.

## Замер E — `ProjectC_client_2026-11-22_11-49-36.data` (после T-PERF05, до B)

351 кадр (f0..351), геймплей 1..350. Код — с правкой адаптера T-PERF05, аудит C в проекте
(он логирует дважды и на метрики не влияет).

| Метрика | D (11-31-27, 486 к.) | E (11-49-36, 350 к.) |
|---|---|---|
| GC сессии (PlayerLoop) | 98 622 KB | **77 730 KB** |
| GC в среднем / медиана | 202.9 / 99.3 KB | 222.1 / **67.9 KB** |
| GC boot f1..40 | — | 35 909 KB (avg 898 KB) |
| GC steady f41.. | — | 41 821 KB (avg 134.9 / med 60.7) |
| `GetComponentNullErrorMessage` | 31.25 KB/кадр, 23 047 выз. | **10.7 KB/кадр (3 747 KB / 5 723)** |
| `LogStringToConsole` | 61.3 KB/кадр | 80.6 KB/кадр (28 224 KB / 1 012) |
| в нём `NavMeshManager.NotifyNavMeshAdded` | — | 17 818 KB / **20 вызовов** (≈878 KB/вызов) |
| `CoroutinesDelayedCalls` | 47.0 KB/кадр | 65.0 KB/кадр (22 758 KB / 58 выз.) |
| `Physics.SendTriggerEvents` GC | ~0 | 109 KB за сессию |
| `PlayerLoop` CPU медиана | 68.3 ms | 64.8 ms |
| `SendTriggerEvents` CPU мед/сред | 20.2 / 27.6 ms | 17.75 / 35.4 ms (p90 55.9) |

1. **Правка адаптера подтверждена:** null-сообщения 31.25 → 10.7 KB/кадр (−66%), 23 047 → 5 723 вызова
   (−75%). Прогноз был ~15 KB/кадр — реальность лучше.
2. Сессионный GC 98.6 → 77.7 MB (−21%), медиана кадра 99.3 → 67.9 KB (−32%). Среднее выросло
   (202.9 → 222.1) из-за доли boot-кадров: в E сессия на 136 кадров короче, а те же спавн-пики
   делятся на меньший знаменатель (boot — 898 KB/кадр, установившийся режим — 134.9).
3. `LogStringToConsole` остался главным источником мусора: 28.2 MB (36% сессии), из них 17.8 MB —
   20 вызовов `NotifyNavMeshAdded` по ~878 KB (нативная цена `AddNavMeshData` в Editor) и 17.2 MB
   под `ShipDeckNav.LateUpdate`. В билде этого лога нет; рычаг — дрейф палуб (T-PERF02).
4. `CoroutinesDelayedCalls` 22.8 MB / 58 вызовов — спавн-корутины (`PrepareAndStartHost`), не ровная цена.
5. CPU: `SendTriggerEvents` (17.75 медиана / 35.4 среднее) по-прежнему #1 — это объём триггеров
   корабля и консоли, т.е. данные префаба, а не код.

## Замер F — `ProjectC_client_2026-11-22_11-57-08.data` (после B)

391 геймплейный кадр. B подтверждён по трём осям сразу: консоль `Failed to create agent` **671 → 438** (−35%),
записей в консоли 1 391 → 1 063; нативная цена одной регистрации палубы `NotifyNavMeshAdded`
17 818 KB / 20 вызовов (891 KB/вызов) → **10 295 KB / 20 (515, −42%)**; GC сессии 77 730 → **64 870 KB** (−17%),
медиана кадра 67.9 → **61.0 KB**, установившийся режим (f41+) 134.9 → **97.1 KB/кадр**, boot 898 → 770;
`PlayerLoop` медиана 64.8 → **56.6 ms**; `LogStringToConsole` 28 224 → 21 247 KB / 795;
`GetComponentNullErrorMessage` 10.7 → 9.2 KB/кадр; `CoroutinesDelayedCalls` 22 758 → 20 850 KB
(`PrepareAndStartHost` 15 504). CPU #1 — по-прежнему `SendTriggerEvents` (медиана **15.98** ms, среднее 23.75).

Остаток 438 разобран по `Location` — в этом буфере он у нативных предупреждений **появился** (в буфере D
стека не было), и это дало адрес каждой группы. Записи идут непрерывными блоками, а не ровным фоном:

| Записи (N) | Кол-во | `Location` | Причина |
|---|---|---|---|
| 568–667 | **100** | `World/Npc/Primum/PrimumCrowdNpc.cs:401` | один проход `ApplyWorldRebase` по 100 NPC (тир 100), `Warp(transform.position)` после отвязки агентов пере-регистрацией навмеша |
| 669–684 | 16 | `NavMeshSurface.cs:221` (пакет) | пере-регистрация мирового навмеша (`RemoveData()`+`AddData()`), на ней переоцениваются 16 мировых NPC вне меша |
| 701–1027 | **304** | `Ship/ShipDeckNav.cs:342` | 20 регистраций палуб × ~15 живых off-mesh агентов |
| 65–77, 78–80, 485, 805 | 18 | — / `Debug.bindings.h:39` / `ShipCrewSpawner.cs:154` / `NpcBrain.cs:866` | нативные кадры и спавн экипажа — не наш путь |

Итог по причинам: **320 (73%)** — 16 мировых NPC вне мирового навмеша (расстановка/бейк в сцене,
правит пользователь), **100 (23%)** — `PrimumCrowdNpc.ApplyWorldRebase → Warp` (наш код; правку
поведения толпы без прогона не делал, цена ошибки — застывшие вне навмеша NPC), **18 (4%)** — не наш путь.
Подробности, включая цепочку `NavMeshSurface.UpdateDataIfTransformChanged → RemoveData+AddData` и
рекомендацию по пробному логу — в `03_FO_NULL_COMPONENT_AND_CONSOLE.md`.

Поправка к подписи аудита C: строка `enabled by name` считала **все** живые компоненты (инкремент идёт
до проверки `enabled`), поэтому капитаны были видны в ней и после B — переименована в `all agents by name`,
чтобы следующее чтение не путало выключенные агенты с включёнными. Сам аудит оставлен: он даёт
повторяемый снимок off-mesh агентов и удаляется одной командой после финального вердикта.

## T-PERF05/G — `ProjectLog.Verbose`: гашение своих info-логов

Файл: `Assets/_Project/Scripts/Core/ProjectLog.cs` (новый, `namespace ProjectC.Core`). Один мастер-флаг
`ProjectLog.Verbose` (по умолчанию `false` и в Editor, и в билде; сбрасывается в `SubsystemRegistration` —
при выключенном domain reload статика живёт между сессиями) плюс два helper'а `Info(string)` /
`Info(string, Object)`. В горячих местах — guard, чтобы интерполяция не выполнялась вовсе.

Основание (замер F): `LogStringToConsole` 21 247 KB, из них 10 295 KB — нативная цена регистрации
палуб; остаток **10 952 KB = 17% GC сессии** — наши info-строки, 557 из 1 063 записей консоли, и класс
шума один: 5–6 сообщений на корабль при спавне × 23 корабля.

Под флаг ушли 7 мест / 159 записей (29% всех info-строк): `[ShipController] Some meziy modules NOT found!` (23),
`[JsonKeyRodInstanceRepository] Saved …` (23), `[MeziyModuleActivator] Initialized …` + `Registered passive …`
(23), `[KeyRodInstanceWorld] AutoSave …` (23), `[CombatServer] RecoverExistingEntities …` (32),
`[ShipFuelSystem] Initialized …` (21), `[PilotSeatController] … IsTrigger=true` (14).
НЕ глушится: `Debug.LogWarning`/`LogError` — `[ShipCargoVisual] _boxPrefabs array is empty` и
`[EngineThrusterVisual]` плечо pivot→visuals остаются (это реальные недоработки данных).

Проверка compile: 0 errors. Play Mode / новый захват — **NOT RUN, за пользователем**.
Ожидание: консоль 1 063 → ~904 записи, GC сессии 64 870 → ~62 700 KB (оценка по средней цене
~13.8 KB на log-вызов, а не по измеренной цене именно этих строк — подтверждение замером).

## Замер G — `ProjectC_client_2026-11-22_12-13-57.data` + `console.txt` (после варианта G)

**Консоль: 7 видов погашены полностью.** Записей 878 (F: 1 063, −18%): `Log` 421 (F: 557), `Warning` 457
(F: 506), `Error`/`Exception` 0. Поиск по тексту сообщений — **0 вхождений** ни одного из 7 погашенных видов.
Остаток шума: **391** нативное `Failed to create agent` (86% всех предупреждений, −47 к F), 43 `[CombatServer]`
(прочие строки), 34 `[EngineThrusterVisual]`, 23 `[ShipCargoVisual]` — последние два и есть заявленные
недоработки данных префабов.

**Профайлер: 283 кадра, GC сессии 87 549 KB (F: 64 870, +35%), но установившийся режим не изменился.**
boot (первые 41 кадр) = 54 053 KB = **62% всего мусора сессии** (1 318 KB/кадр против 770 в F);
установившийся режим f41+ — 33 497 KB / 242 кадра, среднее 138.4, **медиана 62.1** (F: 97.1 / медиана 61.0).
`PlayerLoop` медиана 60.4 ms (F: 56.6), среднее 103.2, p90 134.2, максимум 4 466.9 ms (стойл-кадр — им и
раздут средний кадр). Медиана рабочего режима +2% ⇒ G в игре ничего не ухудшил и не улучшил.

**Оценка экономии от G не подтвердилась.** `LogStringToConsole` 21 247 → **20 191 KB (−1 056 KB, −5%)**,
а не −2.2 MB. Причина: число вызовов упало 795 → 745 при 159 снятых строках, т.е. цена в профайлере
агрегируется по кадрам, а не по сообщениям; фактическая цена снятой записи ≈ 6.6 KB, и она неотделима
от нативных предупреждений того же пути. `NotifyNavMeshAdded` не изменился (10 295 → 10 326 KB / 20).
Оговорки: 283 кадра против 391 и удалённый F-захват — перепроверить F тем же экстрактором нельзя.

**Где мусор теперь — установившийся режим (138.4 KB/кадр):**
`LogStringToConsole` 45.7 KB/кадр (391 нативное предупреждение и прочие логи);
`ShipDeckNav.LateUpdate` **42.0 KB/кадр**, целиком `NavMeshManager.NotifyNavMeshAdded` 9 750 KB / **19 вызовов**
= 513 KB на вызов (палуба перерегистрирует навмеш раз в ~13 кадров — та же операция, что даёт 304 из 438
предупреждений в F по `Location`); `NetworkPlayer.Update` **18.1 KB/кадр**; `UIElementsRepaintPanels` 18.7 KB/кадр.

**Три новые находки (все — с адресом в коде):**
1. `NetworkPlayer.Update` = 5 000 KB / 265 вызовов (18.9 KB на вызов; цифра «10 000 KB / 530» из первого прохода
   была двойным счётом Invoke-узла и узла метода). Источник — `FindObjectsByType<DoorController>` в
   `HasNearbyDoorForInteraction` (`NetworkPlayer.cs:1912`): 2 800 дверей = 21.94 KB массив, а троттлинг 5 Гц
   (`INTERACTION_HINT_INTERVAL = 0.2f`, `:1826`) при кадре ~200 ms пропускает каждый кадр (227 вызовов × 21.94 ≈ 4 980 KB).
   Callstack'ов аллокаций в захвате нет — адрес получен из кода и арифметики.
2. UI Toolkit (`PCMM_PanelSettings`, главное меню) — 17 526 KB за сессию (20% GC), но 74% приходится на первые
   41 кадр (постановка меню); в установившемся режиме 18.7 KB/кадр. 18 render tree на кадр, аллокации размазаны
   (1.8 KB на `RenderTree.UpdateVisuals`) — одного горячего места нет.
3. boot — отдельный фронт: `Update.ScriptRunDelayedDynamicFrameRate` 20 465 KB / 40 вызовов (внутри —
   `GlobalMotionPilotRuntime.PrepareAndStartHost` **13 290 KB / 4 вызова = 3 322 KB на вызов**), UI Toolkit 13 009 KB,
   `LogStringToConsole` ~9 125 KB.

Следующие рычаги (не делал, ждут решения): дрейф палуб (`ShipDeckNav`, 30% установившегося мусора + 391
предупреждение), реестр дверей вместо `FindObjectsByType` в резолвере подсказки, старт хоста и постановка меню.
Подробности — `03_FO_NULL_COMPONENT_AND_CONSOLE.md`, раздел «Замер G».

## T-PERF06 — четыре этапа, порядок 3 → 1 → 4 → 2

Замер G дал три адреса, и они разные по риску: мусор без изменения поведения, агенты вне меша (меняет
поведение), проба PRIMUM (диагностика), дрейф палуб (упирается в FO-контракт). Пользователь выбрал
порядок «сначала безрисковое, потом требующее Play-прогона»; отдельный коммит на этап.
План и состояние этапов — `04_T_PERF06_GARBAGE_CLEANUP.md`.

## T-PERF06/3 — мусор в горячих путях без изменения поведения

Файлы: `Assets/_Project/Scripts/Core/InteractableManager.cs`, `Assets/_Project/Scripts/Ship/DoorController.cs`,
`Assets/_Project/Scripts/Ship/PilotSeatController.cs`, `Assets/_Project/Scripts/Player/NetworkPlayer.cs`,
`Assets/_Project/Scripts/Player/PlayerRespawnTracker.cs`,
`Assets/_Project/Scripts/World/FloatingOrigin/Network/GlobalMotionReplicator.cs`,
`Assets/_Project/Scripts/World/FloatingOrigin/Network/GlobalMotionPoseAdapter.cs`.

**Реестр дверей.** `NetworkPlayer` дважды за тик подсказки собирал `FindObjectsByType<DoorController>`
по 2 800 дверям — 21.94 KB на вызов, 227 из 265 вызовов доходили до строки (`INTERACTION_HINT_INTERVAL`
0.2 с при кадре ~200 ms пропускает каждый кадр) = **4 980 KB** из измеренных 5 000. Теперь
`InteractableManager` держит `List<DoorController>` (ёмкость 4096), `DoorController.OnEnable/OnDisable`
регистрирует и снимает дверь (локальный флаг — защита от двойной активации), `FindNearestDoor` обходит
список без аллокаций и вычищает уничтоженные элементы по месту. Расстояние — `DistanceToInteraction`:
до ближайшей точки коллайдера (кэш `InteractionCollider`), fallback — до трансформа; ровно та же формула,
что была в двух старых циклах. Набор дверей = «активна и включена», как и было в
`FindObjectsByType(FindObjectsInactive.Exclude)`; `TryInteractNearestDoor` раньше фильтровал по `activeSelf` —
теперь оба пути согласованы (практической разницы нет, дверей с выключенным `DoorController` в сцене нет).

**Кэш коллайдера посадки.** `FindNearestShip` вызывал `GetComponentInChildren<PilotSeatController>(true)`
и `GetComponentInChildren<Collider>()` на каждый из 20 кораблей на каждый тик; этот overload реализован
через `GetComponentsInChildren` и потому аллоцирует массив. Теперь `PilotSeatController.SeatCollider`
кэшируется в `Awake`, а `InteractableManager` держит `Dictionary<ShipController, ShipBoardingTarget>` с
повтором резолва через 60 кадров, если кресло/коллайдер ещё не готовы. Семантика ветки 1:1: нет коллайдера
кресла — `seat = null`, fallback на первый коллайдер в детях, радиус = переданный `boardDistance`.
Если кресло есть, но его `Awake` ещё не проставил кэш, берётся `seat.GetComponent<Collider>()` (без
аллокаций), иначе кэш зафиксировал бы чужой коллайдер навсегда.

**Payload-строки пробника.** `RecordEvent` сам no-op при выключенном захвате, но строка интерполируется
у вызывающего. Под `IsActive` переведены оставшиеся периодические вызовы: `NetworkPlayer`
`Update.beforeProcessMovement`, `FixedUpdate.begin/end`, `CharacterController.Move.before/after`,
`PlatformCarry`; `PlayerRespawnTracker.Update.begin`; `GlobalMotionReplicator.NetworkTick`;
`GlobalMotionPoseAdapter.SyncTransforms.begin/end` (каждый кадр для актёров без `Rigidbody` — игрок на
`CharacterController`). Событийные вызовы (спавн, baseline, телепорт, rebase, respawn-переходы) не тронуты:
они идут единицами за сессию и служат доказательной базой FO.

**Ожидание:** `NetworkPlayer.Update` 18.9 KB/вызов → ~0 (остаток — `FindObjectsByType<NpcController>` в
`HasNearbyNpcForInteraction`, 25–30 NPC ≈ 0.3 KB); `GlobalMotionPoseAdapter`/`PlayerRespawnTracker`/
`GlobalMotionReplicator` — по строке на кадр или тик. CPU-порядок обхода дверей не изменился (O(N) по
2 800), снят только массив; пространственная разбивка — отдельная задача.

**Проверка:** `check_compile_errors` — 0 errors. Play Mode и новый замер — **NOT RUN, за пользователем**.
Дальше по плану: этап 1 (агенты вне меша), этап 4 (проба PRIMUM), этап 2 (ленивая регистрация палуб).

## T-PERF06/1 — парковка мир-агента вне навмеша

Файл: `Assets/_Project/Scripts/AI/NpcBrain.cs`.

Механизм выбран пользователем — парковать в `NpcBrain`, а не отдельным компонентом и не расстановкой в
сцене. Прецедент уже в проекте: T-PERF02 так же паркует deck-прокси («висящий off-mesh агент реасессится
каждым чужим `AddNavMeshData` с варнингом»), и вариант B ( `m_Enabled: 0` в префабах капитанов)
доказал, что гашение `enabled` снимает это предупреждение.

`MaintainOffMeshAgentPark()` вызывается первым в `Update()` — до серверных гейтов, потому что
предупреждение нативное и шумит на любом пире; внутри троттлинг 1 с:
агент включён и `isOnNavMesh` — ничего; включён и вне меша — `enabled = false` и флаг «наш парк»;
выключен не нами — не трогаем (смерть/контракт/сцена); запаркован нами и `NavMesh.SamplePosition(position,
height * 2)` даёт меш в пределах 0.5 м по горизонтали — `enabled = true`, а если агент не сел на меш —
сразу обратно в парк. Палуба (`_deckNavActive`) и `BrainState.Dead` исключены.

**Контракты FO.** `HasPreparedNavigation()` уже требует `isOnNavMesh` — для off-mesh NPC он и так был
`false`, вердикт потока (`IsGlobalMotionReady`, `ConfirmGlobalNavigationPrepared`) не меняется. Меняется
`CanApplyGlobalBaseline` (`_agent == null || !_agent.enabled` → `true`); для запаркованного неподвижного
NPC это безопасное направление — приём baseline его не двигает. Именно эта строка была причиной
отклонения варианта A; изменение осознанное, по решению владельца.

**Проверка:** `check_compile_errors` — 0 errors. Play Mode — **NOT RUN, за пользователем**. Ожидание:
304 → ~0 (все 16 мировых NPC идут через `NpcBrain`: `Npc_peacfull_market_zone` ×8, `Q001_NPC_*` ×6,
`[Mira]`, `[Onboarding alfa]`). Наблюдать надо две вещи: предупреждения `ShipDeckNav.cs:342` и то, что
легитимные NPC не остались запаркованными (если мировой навмеш появляется позже первого кадра —
1–2 с статики на старте и самостоятельный возврат).

## T-PERF06/4 — проба в PRIMUM-толпе (временная)

Файл: `Assets/_Project/Scripts/World/Npc/Primum/PrimumCrowdNpc.cs` (`ApplyWorldRebase`).

53 предупреждения замера G идут с `Location: PrimumCrowdNpc.cs:401` — это `_agent.Warp(transform.position)`
в ветке «агент был на меше». По профайлеру причину не взять (предупреждение нативное), поэтому ставится
проба: до ветвления снимаются `_agent.isOnNavMesh` и `NavMesh.SamplePosition(transform.position, 4f)`,
после — `onAfter` и `state`. Пишутся только аномалии (пришли вне меша или меша нет в 4 м), успешные
случаи молчат — иначе сама проба замусорит консоль замера. Всё под `#if UNITY_EDITOR`, порядок веток
и `return` не меняются.

**Прогон за пользователем:** Console → Clear → Play → дождаться мира → **F8** (один rebase) → 5–10 с →
прислать строки `[T-PERF06/4]`. После замера проба удаляется отдельным коммитом — по определению
временная. compile — 0 errors; Play Mode — **NOT RUN**.

## T-PERF06/2 — заблокирован контрактом FO (кода нет)

Поправка к первому прочтению G: 42.0 KB/кадр у `ShipDeckNav.LateUpdate` — это **не дрейф**. 19 вызовов
`NotifyNavMeshAdded` — очередь первичной регистрации 20 кораблей при `MAX_REGISTRATIONS_PER_FRAME = 1`
(один на f17, девятнадцать подряд на f78..f96), т.е. 9 750 KB — разовая цена входа в мир
(≈513 KB на регистрацию, Editor-маршаллинг `NavMesh.AddNavMeshData`), а не протечка. Повторная
регистрация требует сдвига корабля на 2 500 м и истёкшего кулдауна 30 с.

Ленивая регистрация невозможна без правки FO-контракта: манифест rebase требует, чтобы каждая палуба
сцены была `IsRegistered && IsNavMeshInstanceValid && IsReady`
(`GlobalMotionRebaseSceneRuntimeManifestSource.cs:126`, иначе `ship_deck_nav_not_ready:<name>`);
то же у `ShipDeckNav.TryCapture*` (`:174`, `:197`), `GlobalMotionShipDeckCombinedTransactionHost` (`:216`)
и lifecycle-контракта (`:83`).

Этап закрыт как «решение требуется», код не менялся. Варианты: (а) оставить как есть;
(б) разрешить в манифесте неиспользуемую палубу (правка контракта + прогон rebase); (в) сначала снять
не-Editor замер цены одной регистрации, потому что 513 KB в значительной части — редакторный маршаллинг.

## Замер H — `ProjectC_client_2026-11-22_12-52-39.data` + `console.txt` (после этапов 3/1/4)

237 кадров (f0..f236). Консоль: 582 записи (G: 878) — `Log` 460, `Warning` 122, `Error`/`Exception` **0**.
`Failed to create agent` **56** (G: 391, F: 671); `ShipDeckNav.cs:342` **304 → 0**; `NavMeshSurface.cs:221` 16 → 0.
Из 56 нативных 40 имеют managed `Location`: 38 — `PrimumCrowdNpc.cs:410`, 1 — `NpcBrain.cs:913` (прокси),
1 — `ShipCrewSpawner.cs:154`; остальные 16 — без `Location`. Предупреждения (122): 38 `PrimumCrowdNpc.cs:410`,
34 `EngineThrusterVisual.cs:185`, 23 `ShipCargoVisual.cs:211`, 2 `DoorVisibilityBudget.cs:256`, по одной в девяти местах.

GC в установившемся режиме: медиана **67.7 KB**/кадр, p90 179.7, min 29.2. Сумма по сессии 72.0 MB, но
46.9 MB из неё — четыре кадра (f0 20.8 MB старт, f16 10.8, f17 8.8, f236 4.6 стоп). Геймплейный f120
(59.1 KB) раскладывается так:

| Источник | KB/кадр | Доля | Вердикт |
|---|---|---|---|
| `NpcShipWorld.FixedUpdate` → `NavTick` | 26.9 | 45% | остаток: 372 `GC.Alloc`, `Physics.Raycast` ×71 |
| `GetComponentNullErrorMessage` ×18 | 11.7 | 20% | починен этапом 5 |
| `ContractServer.FixedUpdate` | 4.9 | 8% | не в этих этапах |
| `NetworkPlayer.Update` | 3.3 | 6% | этап 3 подтверждён (было 18.9 KB/вызов) |
| URP / PlayMode-view рендер | 5.0 | 8% | — |

`ShipDeckNav.LateUpdate` на f100/f120/f152 — **0 KB** при тех же 20 вызовах: вывод этапа 2 подтверждён
(42 KB/кадр в G — разовая очередь регистрации, а не дрейф). `NpcShipWorld.FixedUpdate` — 372 `GC.Alloc`
и 71 `Physics.Raycast` на 6 вызовов: без Allocation Call Stacks атрибуция внутри `NavTick` невозможна.

В f17 `LogStringToConsole` = 5 464 KB на 236 вызовов (≈23 KB на запись), `CallLogCallback` — 303 ms self:
в редакторе предупреждение дороже самой логики, т.е. гасить надо причину, а не фильтр.

## T-PERF06/4 — сделано: парковка толпы вместо `Warp` в пустоту

Проба дала 38 строк, все формы `onBefore=True enabled=True sample4m=False onAfter=False`: агент приходил
на rebase «на меше», меша в 4 м под пост-ребейз позицией не было, `Warp` отцеплял его и рождал нативное
предупреждение. `pos` = `home` − один и тот же сдвиг (≈39 680 / 2 562 / 39 937): тело на месте, навмеш
переезжает вместе с `WorldRoot_0_0` **не в тот же кадр**. Правка: `TryRefreshAgentOnNavMesh()`
(Sample → Warp на найденный меш, иначе `ParkAgent()`), `ReLandOnNavMesh` — no-op на выключенном агенте,
`Tick` будит `TryRestoreParkedAgent()` (тело садится на меш до `enabled = true`). compile — 0 errors;
Play Mode — **NOT RUN, за пользователем**. Ожидание: 38 предупреждений `PrimumCrowdNpc.cs:410` → 0,
`Failed to create agent` 56 → ~18. Проба остаётся до этого прогона.

## T-PERF06/5 — null-`GetComponent` в кадре: 18 промахов → 0

Замер H показал остаток, которого не было в плане: `GetComponentNullErrorMessage` — 18 вызовов и 11.7 KB
на геймплейный кадр (20% мусора кадра). По узлам: `AI.NpcBrain.Update` 8 × 0.63 KB (lazy-init
`_socialBrain = GetComponent<NpcSocialBrain>()` в `Update` у NPC без компонента — повтор **каждый** кадр),
`GlobalMotionWorld.FixedUpdate → ApplyFixedPose` 6 × 0.67, `GlobalMotionPoseAdapter.LateUpdate` 3 × 0.67,
`GlobalMotionWorld.Update` 1 × 0.67 — все три последних через `CurrentNetworkAncestor()`.

Правки: в `NpcBrain` (`Update`, `ApplyRebaseTranslation`) — `TryGetComponent(out _socialBrain)` (lazy-init
сохранён, промах больше не строит строку); в `GlobalMotionPoseAdapter` — `CurrentNetworkAncestor()` и проверка
`Rigidbody` в `SupportedStructure()` ходят по родителям циклом `TryGetComponent` (`FindComponentInParent<T>`)
вместо `GetComponentInParent`. Кеша нет намеренно: иерархия читается свежей, поведение 1:1.

compile — 0 errors; Play Mode — **NOT RUN, за пользователем**. Ожидание: 18 → 0 вызовов,
−11.7 KB/кадр (f120: 59.1 → ~47.4 KB).
