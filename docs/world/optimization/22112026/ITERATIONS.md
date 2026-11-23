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

## Замер I — `ProjectC_client_2026-11-22_13-47-22.data` + `console.txt` (Allocation Call Stacks ON)

299 кадров, файл 700 MB — первый замер со **стеками аллокаций**, т.е. со строкой, а не только узлом дерева.
Консоль: 548 записей (H: 582) — `Log` 461, `Warning` 87 (H: 122), `Error`/`Exception` **0**;
`Failed to create agent` **18** (H: 56), managed `Location` только у двух (`ShipCrewSpawner.cs:154`,
`NpcBrain.cs:915`), остальные 16 — нативные без стека; `PrimumCrowdNpc` в консоли **отсутствует**
(в H — 38 × `PrimumCrowdNpc.cs:410`) ⇒ этап 4 по предупреждениям подтверждён. Строк пробы `[T-PERF06/4]` —
**52** (в H было 38): парковки есть; возврат агентов в движение проба не логирует и прогоном не подтверждён.
Предупреждения: 34 `EngineThrusterVisual.cs:185`, 23 `ShipCargoVisual.cs:211`, 2 `DoorVisibilityBudget.cs:256`,
по одной в девяти местах.

Профайлер: GC/кадр — min 36.6, медиана **77.6** (H: 67.7), p90 165.3; `PlayerLoop` медиана 163.12 ms;
пики f0 12 719.9, f50 6 577.3, f298 4 740.3 (стоп).

**Семантика байт (важно):** `HierarchyFrameDataView.columnGcMemory` — **байты**, поэтому «узел 47088 против
46 KB у листьев» — ошибка формата, а не агрегации: 47 088 B = 45.98 KB. Сумма всех 1 132 `GC.Alloc` кадра
f150 = 80 779 B = 78.9 KB = ровно `rootGcKB` у `PlayerLoop`. Одинаковые байты у f150/f151 — детерминизм
(те же NPC), а не баг. `RawFrameDataView.ResolveMethodInfo` в загруженном профиле не резолвит **ничего**
(0/300); работает только `HierarchyFrameDataView.ResolveItemMergedSampleCallstack` у листьев `GC.Alloc`
(callstacks пишутся только для аллокаций).

f150 (78.9 KB): `NpcShipWorld.FixedUpdate` → `NavTick` — **46.0 KB / 639** аллокаций, и все 639 дали ровно
три строки:

| Строка | Аллокаций | KB | Механизм |
|---|---|---|---|
| `NpcShipZoneRegistry.cs:97` | 350 | 26.4 | `new List(16)` в каждом `QueryNearby` ← `NpcProximityZone.cs:230` ← `NpcShipController.cs:842` |
| `MarketConfigCollector.cs:20` | 229 | 16.9 | `Trim().ToUpperInvariant()` в каждом `GetByLocation` ← `ResolveCommZone:1620` ← `TickCruise:1190` ← `NavTick:877` |
| `NpcProximityZone.cs:285` | 60 | 2.7 | `foreach` по `NpcBuildZoneRegistry.All` (`IReadOnlyList`) боксит энумератор ≈46 B |

Промахи `GetComponent` — 14 выз. / 9.39 KB (этап 5 закрыл только часть; `NpcBrain.Update` и
`CurrentNetworkAncestor()` больше не промахиваются): `GlobalMotionWorld.FixedUpdate` 9 + `Update` 1 — всё тот же
`GlobalMotionWorld.cs:205` (`GetComponent<NetworkObject>` в `ReactivateFromCurrentPose`);
`GlobalMotionPoseAdapter.LateUpdate` 3 — `GlobalMotionReplicator.cs:353` (`GetComponent<NetworkTransform>`
в `HasCompetingWriter`); `OuterCommZone.Update` 1 — `OuterCommZone.cs:219` (`GetComponent<NetworkPlayerSpawner>`).

## T-PERF06/6 — аллокации в `NavTick` и остаток промахов

`NpcShipZoneRegistry.QueryNearby(pos, radius, buffer)` — реестр больше не создаёт `List`, а заполняет буфер
вызывающей стороны (реестр server-only, вызовы в одном потоке); свои буферы у `NpcProximityZone`,
`NpcShipController.ComputeScatterDir`, `NpcShipTrafficManager.IsDepartureClear`.
`DockingZoneRegistry.GetByLocation` — прямой lookup по исходному `locationId` (нормализуется на регистрации),
`Trim/ToUpperInvariant` только при промахе. `NpcProximityZone.FindClosestBuildConflict` — индексный обход
вместо `foreach`. `GlobalMotionWorld.cs:205`, `GlobalMotionReplicator.HasCompetingWriter`,
`OuterCommZone.cs:219` — `TryGetComponent`.

compile — 0 errors; Play Mode — **NOT RUN, за пользователем**. Ожидание: 78.9 KB/кадр → **~2–3 KB**
(снято 26.4 + 16.9 + 2.7 + 6.86 + 2.06 + 0.70 KB).

## T-PERF06 — замер J (`ProjectC_client_2026-11-22_14-22-32.data` + `console.txt`)

309 кадров, файл 615 727 816 B, call stacks ON. Консоль: 596 записей (511 `Log`, 85 `Warning`, 0 `Error`),
`Failed to create agent` 18 (как в замере I). GC сессии 43.7 MB; устойчивый участок 41..304 (264 кадра) —
avg **31.6**, медиана 19.2, p90 52.9 KB/кадр (I: медиана 77.6). Пики вне него — f31 11 032 + f32 9 510 KB
(загрузка хоста) и f308 4 681 KB (останов).

Вердикт по этапу 6: `NpcShipWorld.FixedUpdate` — **0 B** (было 46.0 KB), `FixedUpdate` целиком 4.8 KB
(`ContractServer` 3.3 + `GlobalMotionWorld` 1.4). Три правки сработали.

Остаток мусора переехал на сторону Update. f150 (53.3 KB): `AdditionalLightIntensityController.Update`
**37.4 KB** — рескан `RefreshLights` каждые 2 с, `FindObjectsByType<Light>` = 2 × 18.7 KB
на ~2 230 источников (стек `RefreshLights:687` ← `Update:613` совпал с арифметикой). f200 (138.3 KB):
`ShipPositionServer.Update` **115.4 KB** — тик сейва раз в 5 с: `LogStringToConsole` 64.4 (три `Debug.Log`
на тик), `GC.Alloc` 48.5 (тело сейва + `JsonUtility.ToJson` + `WriteAllText`),
`GetComponentNullErrorMessage` 2.5; `HouseDiscovery.Update` 8.4 (`FindObjectsByType<HouseMarker>` каждые 0.5 с).

Поправка к методике: суммы по узлам ( `GetItemColumnData(id, columnGcMemory)` ) надёжны, а привязка
sample → callstack в загруженном профиле смешивает ветки кадра — в узле `ShipPositionServer.Update/GC.Alloc`
первые samples приходят сразу из `GetByLocation`, `QueryNearby`, `ToUpperInvariant` и `SaveCurrentState:437`.
Адрес принимается только при совпадении стека **и** арифметики по размеру узла.

Проба `[T-PERF06/4]`: 84 строки, все `primum_crowd_*`, `sample4m=False`, `enabled=False`, `onAfter=False` —
один сдвиг мира запарковал всю толпу. `enabled`/`onAfter` читаются после `ParkAgent`, поэтому у выключенного
агента `isOnNavMesh` = **false** и ветка восстановления в `Tick` достижима. Проба удалена.

## T-PERF06/7 — мусор на стороне Update

`JsonShipPositionRepository` — успешный сейв логируется один раз за сессию (`LogSaveOnce`, `VerboseLog`
возвращает прежний шум); из `SaveCurrentState` убрана дублирующая строка `Saved: N ships`;
`TryGetComponent` вместо `GetComponent` для `Rigidbody`/`NpcShipController`; список паромов и имена сцен
кэшируются (`GetCachedParoms` 300 кадров, `SceneNameCached` по `SceneHandle`).
`AdditionalLightIntensityController` — рескан по смене состава сцен (`sceneCount` + `OnSceneLoaded`),
страховочный таймер не чаще 30 с. `HouseDiscovery` — кэш `HouseMarker[]` 300 кадров + `HashSet<string>`
открытых домов. `PrimumCrowdNpc` — проба `[T-PERF06/4]` удалена.

compile — 0 errors; Play Mode — **NOT RUN, за пользователем**. Ожидание: 31.6 → **~10 KB/кадр и ниже**.


## T-PERF07 — CPU зон связи и рынка (`ProjectC_client_2026-11-22_14-39-13.data`)

289 кадров, call stacks ON. Агрегация всех кадров main thread: `Physics.SendTriggerEvents` — 82,83 мс total /
61,50 мс self на кадр; `OuterCommZone.OnTriggerEnter` — 5 541 вызов, 18,38 / 17,18 мс;
`MarketZone.Update` — 4,56 / 3,97 мс. Подробности и следующие кандидаты — `05_T_PERF07_ZONE_CPU.md`.

`OuterCommZone`: SphereCollider выключен — прежний polling по `SpawnedObjects` и клиентский polling локального
игрока покрывают детект без событий от городской геометрии. `MarketZone`: два `OverlapSphereNonAlloc` заменены
одним `PollActorsInRadius` по `SpawnedObjects`; радиусы и debounce сохранены.

compile + domain reload — завершены, ошибок C# нет; `git diff --check` — чисто. Play Mode — **NOT RUN,
за пользователем**. Ожидание: исчезновение `OuterCommZone.OnTriggerEnter`, существенное снижение
`Physics.SendTriggerEvents` и `MarketZone.Update`; фактические цифры — после нового захвата.


## T-PERF07 — подтверждение замером `ProjectC_client_2026-11-22_15-10-49.data`

267 кадров, 1 771 маркер, 580 MB. `OuterCommZone.OnTriggerEnter` исчез полностью (до: 5 541 вызов,
18,38 / 17,18 мс total/self на кадр). `Physics.SendTriggerEvents`: 82,83 / 61,50 → **53,44 / 50,63** мс;
`MarketZone.Update`: 4,56 / 3,97 → **3,89 / 3,69** мс. Отключение триггера связи подтверждено;
native trigger-dispatch остаётся главным CPU-участком. `console.txt` не обновился (экспорт 14:39:42),
поэтому не использовался как консоль нового прогона. Подробности — `05_T_PERF07_ZONE_CPU.md`.

## T-PERF08 — interaction-триггеры кораблей

На steady-state участке 30..250 нового захвата (221 кадр): `Physics.SendTriggerEvents` — 38,20 / 37,88 мс
total/self на кадр; `ShipCargoConsole.OnTriggerEnter/Exit` — 2 082 + 10 786 вызовов;
`ShipController.OnTriggerEnter/Exit` — 4 050 + 15 616. Managed self-time мал, поэтому сокращается само число событий.

`ShipCargoConsole` регистрируется в `InteractableManager` через `OnEnable/OnDisable`, а его SphereCollider выключен:
поиск и взаимодействие уже используют прежний `pickupRange = 3 м`. `DockingPadTriggerBox` отсекает коллайдеры без
`attachedRigidbody` до поиска `ShipController`; `Rigidbody` у корабля обязателен, остальная логика не изменена.

compile + domain reload — завершены, ошибок C# нет. Play Mode — **NOT RUN, за пользователем**. Критерии следующего
замера и ручные проверки — `06_T_PERF08_INTERACTION_TRIGGERS.md`.


## T-PERF08 — подтверждение замером `ProjectC_client_2026-11-22_17-05-19.data`

605 кадров; steady-state 30..599 = 570 кадров. `Physics.SendTriggerEvents` total/self: 38,20 / 37,88 →
**0,189 / 0,068 мс/кадр**. `ShipCargoConsole.OnTriggerEnter/Exit`: 2 082 + 10 786 → **0 + 0**.
`ShipController.OnTriggerEnter/Exit`: 4 050 + 15 616 → **71 + 125**. Interaction trigger-лавина устранена,
подробности — `06_T_PERF08_INTERACTION_TRIGGERS.md`.

Следующий managed self-кандидат — `Player.NetworkPlayer.Update` (5,32 мс/кадр), затем
`NpcShipWorld.FixedUpdate` (1,69) и `WindManager.FixedUpdate` (1,08). Построчной CPU-атрибуции внутри
`NetworkPlayer.Update` нет; рискованных правок ввода, движения или троттлинга по этой агрегации не делалось.
`console.txt` остался старым экспортом 14:39:42. Play Mode агентом — **NOT RUN**.


## T-PERF09 — точечная атрибуция `NetworkPlayer.Update`

После подтверждения T-PERF08 главный managed self-кандидат — `Player.NetworkPlayer.Update` (5,32 мс/кадр
на 570 steady-кадрах), но его внутренние секции в захвате не разделены. В `NetworkPlayer.cs` добавлены девять
постоянных `ProfilerMarker`: Held/JustPressed ввод, InteractionHint, ChunkTracker, Movement целиком и вложенные
PlatformCarry/Animator/Wind/ControllerMove. Игровая логика не изменена, Deep Profile не требуется.

compile + domain reload — завершены, ошибок C# нет; `git diff --check` — чисто. Play Mode — **NOT RUN**.
Протокол: 30–60 с после загрузки, стояние/ходьба/бег + посадка и полёт, Deep Profile OFF,
Allocation Call Stacks OFF. Подробности — `07_T_PERF09_PLAYER_UPDATE_PROFILING.md`.


## T-PERF09 — результат атрибуции и broadphase дверей (`ProjectC_client_2026-11-22_18-35-43.data`)

495 кадров; steady-state 30..489 = 460 кадров. `Player.NetworkPlayer.Update` — 5,105 / 3,023 мс
total/self на кадр. `NetworkPlayer.InteractionHint` — 1,742 / 1,650 мс/кадр, 136 вызовов,
5,893 мс/вызов. Движение целиком — 0,154 / 0,0176 мс/кадр; ввод и ChunkTracker — ещё меньше.
Горячий участок локализован: `FindNearestDoor` читает `Collider.bounds` примерно у 2 800 дверей
на каждый resolver. Подробности — `07_T_PERF09_PLAYER_UPDATE_PROFILING.md`.

В `DoorController` добавлен ленивый консервативный радиус сферы для `BoxCollider`; для других типов
коллайдеров broadphase отключён через `PositiveInfinity`. `FindNearestDoor` отсекает заведомо дальние
двери до `DistanceToInteraction`, точный расчёт и выбор ближайшей двери не изменены.

compile + domain reload — завершены, новых ошибок C# нет; `git diff --check` — чисто. Play Mode,
тесты и Build агентом — **NOT RUN**. `Profiler.FlushMemoryCounters` (8,536 мс/кадр) и
`Profiler.Callstack` (2,159 мс/кадр) в захвате остались, поэтому снижение profiling overhead
не заявляется. Величина выигрыша будет определена следующим steady-state захватом.


## T-PERF09 — подтверждение broadphase и реестр NPC (`ProjectC_client_2026-11-22_19-16-00.data`)

434 кадра; steady-state 30..428 = 399 кадров. `Player.NetworkPlayer.Update` total/self:
5,105 / 3,023 → **4,270 / 2,784 мс/кадр**. `NetworkPlayer.InteractionHint`: 1,742 / 1,650 →
**1,189 / 1,160 мс/кадр**; на вызов 5,893 → **5,101 мс (−13,4 %)**. Broadphase подтверждён,
но resolver остаётся основным внутренним участком. Подробности — `07_T_PERF09_PLAYER_UPDATE_PROFILING.md`.

`NpcController.FindNearest` теперь использует существующий OnEnable/OnDisable-реестр с прежним
trigger-or-distance правилом. Подсказка и фактическое E-взаимодействие в `NetworkPlayer` переведены
на общий поиск, `FindObjectsByType<NpcController>` из обоих путей убран. Порядок взаимодействий не изменён.

compile + domain reload — завершены, ошибок C# нет; `git diff --check` — чисто. Play Mode, тесты
и Build агентом — **NOT RUN**. Величина дополнительного выигрыша будет определена следующим захватом.


## T-PERF09 — подтверждение реестра NPC и реестр сундуков (`ProjectC_client_2026-11-22_19-37-55.data`)

1 313 кадров; steady-state 30..1307 = 1 278 кадров. `NetworkPlayer.InteractionHint` total/self:
1,189 / 1,160 → **0,730 / 0,727 мс/кадр**; на вызов 5,101 → **2,989 мс (−41,4 %)**.
`Player.NetworkPlayer.Update` total 4,270 → **3,922 мс/кадр**, self 2,784 → 2,851. Реестр NPC
подтверждён; основной остаток self — ежекадровый `FindNearestInteractable` со сканом NetworkChestContainer.

`NetworkChestContainer` теперь ведёт реестр активных spawned-сундуков (spawn/OnEnable/OnDisable/despawn).
`FindInRange` сохраняет прежние проверки и per-chest `GetOpenRadius`; `FindNearestInteractable`
использует его вместо `FindObjectsByType`, сохраняя обновление `_nearest*` для UI и F/E-flow.

compile + domain reload — завершены, ошибок C# нет; `git diff --check` — чисто. Play Mode, тесты
и Build агентом — **NOT RUN**. Следующий захват должен подтвердить снижение self-time Update.


## T-PERF09 — подтверждение реестра сундуков (`ProjectC_client_2026-11-22_20-27-55.data`)

781 кадр; steady-state 30..775 = 746 кадров. `Player.NetworkPlayer.Update` total/self:
3,922 / 2,851 → **0,886 / 0,033 мс/кадр**. Устранение ежекадрового FindObjectsByType
NetworkChestContainer подтверждено; `InteractionHint` — 0,681 / 0,671 мс/кадр, движение без
регрессии по агрегированному маркеру. Подробности — `07_T_PERF09_PLAYER_UPDATE_PROFILING.md`.

## T-PERF10 — общий снапшот актёров для зон

На том же steady-state `MarketZone.Update` — 1,443 / 1,382 мс total/self на кадр (8 206 вызовов),
`OuterCommZone.Update` — 1,183 / 1,134 мс (6 714 вызовов). Зоны уже имеют polling 0,25 с, но каждая
на своём тике повторно обходит все `SpawnedObjects`.

В обоих классах добавлен static per-frame снапшот `NetworkPlayer`/`ShipController`: полный обход
выполняется максимум один раз за кадр для каждого типа зоны, а каждая зона затем проверяет только
актуальных актёров. Радиусы, layer mask, debounce, LocalPlayerZone/Station и период polling не изменены.

compile + domain reload — завершены, ошибок C# нет; `git diff --check` — чисто. Play Mode, тесты
и Build агентом — **NOT RUN**. Следующий захват должен подтвердить снижение обоих Update-маркеров.

## T-PERF10 — подтверждение снапшота зон и кэш компонентов NPC (`ProjectC_client_2026-11-22_23-50-05.data`)

1 550 кадров; steady-state 30..1544 = 1 515 кадров. `MarketZone.Update` total/self:
1,443 / 1,382 → **0,258 / 0,251 мс/кадр**; `OuterCommZone.Update`: 1,183 / 1,134 →
**0,235 / 0,228 мс/кадр**. Общий per-frame снапшот подтверждён. Следующий managed-кандидат —
`NpcShipWorld.FixedUpdate` (0,975 / 0,793 мс/кадр).

В `NpcShipController` добавлены ленивые кэши неизменных компонентов корня `ShipController`
и `Rigidbody`; повторные `GetComponent` в `NavTick`, посадке, смене режима, spawn/despawn
и restore используют их. Частота навигации и игровая логика не изменены.

compile + domain reload — завершены, ошибок C# нет; `git diff --check` — чисто. Play Mode, тесты
и Build агентом — **NOT RUN**. Величина выигрыша будет определена следующим захватом.

## T-PERF10 — подтверждение кэша NPC и снапшот позиций зон (`ProjectC_client_2026-11-23_00-19-16.data`)

1 562 кадра; steady-state = 1 527 кадров. `NpcShipWorld.FixedUpdate` total/self:
0,975 / 0,793 → **0,473 / 0,314 мс/кадр**. Кэш `ShipController`/`Rigidbody` подтверждён.

В этом прогоне при большем числе polling-тиков `MarketZone.Update` — 1,974 / 1,898 мс/кадр
(16 797 вызовов), `OuterCommZone.Update` — 1,524 / 1,463 мс/кадр (13 743 вызова).
Общий снапшот устранил повторный обход `SpawnedObjects`, но каждая зона продолжала читать
`transform.position` и другие свойства всех актёров. Снапшоты обоих классов теперь хранят
позиции и идентификаторы; для связи также слои. Эти значения читаются из Unity один раз
на кадр, зоны выполняют только managed-сравнения. Радиусы, маски, debounce и polling не изменены.

compile + domain reload — завершены, ошибок C# нет; `git diff --check` — чисто. Play Mode, тесты
и Build агентом — **NOT RUN**. Величина выигрыша будет определена следующим захватом.

## T-PERF10 — подтверждение позиций зон и broadphase зданий (`ProjectC_client_2026-11-23_00-29-06.data`)

777 кадров; steady-state = 742 кадра. `MarketZone.Update` total/self: 1,974 / 1,898 →
**0,346 / 0,337 мс/кадр**; `OuterCommZone.Update`: 1,524 / 1,463 → **0,337 / 0,329 мс/кадр**.
Кэширование позиций подтверждено. Главный managed self — `NpcShipWorld.FixedUpdate`
(1,656 / 1,076 мс/кадр); внутри свободных режимов каждый NPC на каждом FixedUpdate проверяет
все building-зоны через `Collider.ClosestPoint`.

`NpcProximityZoneBuilds` теперь при скане коллайдеров сохраняет их общий AABB относительно
корня. `IsIntruding` сначала использует консервативный `Bounds.SqrDistance`; точные
`ClosestPoint` выполняются только для близких зданий. AABB корректно переносится при
Floating Origin через текущий `transform.position`; `RefreshColliders` обновляет охват.
Точный порог и выбор конфликта не изменены.

compile + domain reload — завершены, ошибок C# нет; `git diff --check` — чисто. Play Mode, тесты
и Build агентом — **NOT RUN**. Величина выигрыша будет определена следующим захватом.

## T-PERF11 — периодический editor-статтер (`ProjectC_client_2026-11-23_17-44-47.data`)

2 000 кадров; steady-state = 1 965 кадров. Во второй половине пики `Main Thread`
повторяются с периодом 9–11 кадров (автокорреляция lag 10 = 0,213). Главный вклад —
нативный `EditorLoop` self: 15,059 мс/кадр в среднем, на отдельных пиках 80–150 мс;
`PlayerLoop` — 29,166 мс/кадр. Пики не совпадают с `GC.Collect` или регулярными логами.
Отдельный разовый пик Floating Origin (frame 1387) не является причиной ритма.

`NpcShipWorld.FixedUpdate` self: 1,076 → **1,043 мс/кадр**; одного AABB зданий
недостаточно для существенного снижения NPC. Зоны остаются на уровне 0,333 + 0,318 мс/кадр.

`NpcProximityZoneBuilds.OnDrawGizmos` переведён на `OnDrawGizmosSelected`: на каждом
SceneView repaint больше не рисуются все коллайдеры и Handles.Label всех зданий.
Отладка выбранного здания сохранена, runtime-логика и serialized `drawGizmos` не изменены.
Это точечное устранение самого тяжёлого project-side кандидата внутри нативного EditorLoop;
причинно подтвердить исчезновение ритма должен следующий захват с прежними Gizmos/SceneView.

compile + domain reload — завершены, ошибок C# нет; `git diff --check` — чисто. Play Mode, тесты
и Build агентом — **NOT RUN**. Повторить тот же маршрут второй половины прогона 30–60 с.


## T-PERF12 — пакетная оптимизация managed hot paths (`ProjectC_client_2026-11-23_18-17-47.data`)

1 236 steady-state кадров. `Main Thread` — 55,821 мс/кадр, `PlayerLoop` — 37,532;
`EditorLoop` self — 12,841. Исчезновение субъективного ритма после T-PERF11 не подтверждено.
Основные managed-кандидаты этого прогона: `NpcShipWorld.FixedUpdate` total/self 1,542 / 1,099,
`PRIMUM/LightPool.Check` self 0,724, `PlayerRespawnTracker.Update` self 0,727,
`ShipShadowBudget.Update` self 0,469, `DoorVisibilityBudget.Update` self 0,371,
`CityShadowBudget.Update` self 0,349 мс/кадр. Периодический анализ также показывает серии
дорогих вызовов RespawnTracker (автокорреляция lag 2 = 0,602); во время падения каждый кадр
выполнялся глобальный поиск ParomRoute.

Пакет изменений без смены игровых порогов:

- `NpcShipController`: ship/ship и ship/building avoidance-проверки выполняются с частотой
  около 10 Гц вместо 50 Гц на каждый FixedUpdate; небольшая фаза от `npcInstanceId` разносит
  корабли по времени. EnterAvoid, cooldown и режимы навигации не изменены.
- `PrimumLightPool`: локальные позиции неподвижных ламп кэшируются относительно `lightsRoot`
  при Rebuild. Позиция и базис камеры преобразуются один раз за Check, включая поиск фокусной
  камеры. Мировые позиции между кадрами не хранятся — Floating Origin сохраняется через корень.
- `ParomRoute`: добавлен OnEnable/OnDisable-реестр активных веток; спасение игрока и
  определение ветки под ногами больше не вызывают `FindObjectsByType`.
- `ShipShadowBudget`: общий периодический снапшот геймплейных камер для всех кораблей;
  маска рендереров по-прежнему применяется отдельно. Пороги сравниваются в квадратах дистанций.
- `CityShadowBudget` и `DoorVisibilityBudget`: убраны `Sqrt` из регулярных проверок;
  городской Entry хранит текущее состояние теней вместо чтения `shadowCastingMode` каждый тик.
  Restore возвращает авторский режим и сбрасывает это состояние.

compile + domain reload — завершены, ошибок C# нет; `git diff --check` — чисто. Play Mode, тесты
и Build агентом — **NOT RUN**. Исключения `Client handler exited` от MCP — технический шум
подключения, не ошибки C#. Величина выигрыша и влияние на ритм определяются следующим захватом.


## T-PERF13 — подтверждение T-PERF12 и пакет editor/visual hot paths (`ProjectC_client_2026-11-23_18-54-08.data`)

1 965 steady-state кадров (510–2509). Подтверждённый эффект T-PERF12 по self-time на кадр
относительно замера 18-17: `NpcShipWorld.FixedUpdate` 1,099 → **0,749 мс (−31,8 %)**,
`PRIMUM/LightPool.Check` 0,724 → **0,225 мс (−68,9 %)**. `PlayerRespawnTracker.Update`
вышел из top-120 self — глобальный поиск паромов больше не является горячим путём.
`ShipShadowBudget.Update` 0,469 → 0,388; `DoorVisibilityBudget.Update` 0,371 → 0,326;
`CityShadowBudget.Update` 0,349 → 0,294 мс/кадр. `PlayerLoop` 37,532 → **33,368 мс/кадр**.

Общий статтер не считается устранённым: `Main Thread` 55,821 → 64,707 мс/кадр, а
`EditorLoop` self 12,841 → 25,562. Маршрут и состояние SceneView могли отличаться.
Дополнительные managed-кандидаты: `ShipContrailVfx.Update` self 0,081 (27 524 вызова),
`DockPadVisualMarker.LateUpdate` 0,162 (133 688 вызовов), `ShipPartShake.Update` 0,092
(94 368 вызовов); `UpdateRendererBoundingVolumes` — 0,826 мс/кадр.

Пакет изменений:

- Все оставшиеся тяжёлые/массовые project-side debug-gizmos переведены на
  `OnDrawGizmosSelected`: сплайновые и обычные ветровые зоны, NPC avoidance и NPC interaction,
  здания/рынок/связь/док-пады, SitPoint/CoverPoint, ParomRoute, CompassRose и StormCellDirector.
  В SceneView больше не выполняются постоянные Handles.Label, кольца сплайнов и сотни сфер
  штормовых ячеек для всех объектов. Визуализация выбранного объекта и runtime не изменены.
- `ShipContrailVfx`: scale и параметры графа применяются только при смене scale или OnValidate;
  убраны повторные `HasFloat`/`SetFloat` и `Random.Range` каждый кадр. Движение и Play/Stop не изменены.
- `ShipPartShake`: в базовой позе `localPosition`/`localRotation` записываются один раз при переходе,
  а не каждый кадр. Амплитуды, частота и сглаживание не изменены.
- `DockPadVisualMarker`: базовый цвет кэшируется при смене материала; убран ежекадровый
  `GetPropertyBlock`/`GetColor`. Пульсация и состояния площадок не изменены.

compile + domain reload — завершены, ошибок C# нет; `git diff --check` — чисто. Play Mode, тесты
и Build агентом — **NOT RUN**. Исключения `Client handler exited` от MCP — технический шум
подключения, не ошибки C#. Следующий захват должен проверить EditorLoop, визуальные Update и ритм.


## T-PERF14 — подтверждение T-PERF13 и триггеры док-падов (`ProjectC_client_2026-11-23_19-43-03.data`)

1 405 steady-state кадров. `Main Thread` 64,707 → **46,815 мс/кадр**, `EditorLoop` self
25,562 → **10,278 мс/кадр**, `PlayerLoop` 33,368 → **31,133 мс/кадр**. Выборочные Gizmos
подтверждены существенным снижением EditorLoop. `UpdateRendererBoundingVolumes`
0,826 → 0,426, `TransformChangeSystem` 0,424 → 0,297, `DockPadVisualMarker.LateUpdate`
0,162 → 0,128 мс/кадр. `ShipContrailVfx` и `ShipPartShake` вышли из top-120 self.

Новый главный project-side self-кандидат — `DockingPadTriggerBox.OnTriggerEnter`:
2,573 / **2,479 мс total/self на кадр**, 1 493 вызова. На каждом дочернем коллайдере
корабля выполнялся `GetComponentInParent<ShipController>`. `EngineThrusterVisual.Update`
остался на 0,193 мс/кадр (56 200 вызовов) и продолжал записывать позы после затухания.

Изменения:

- `DockingPadTriggerBox`: Enter/Exit получают `ShipController` непосредственно с
  `other.attachedRigidbody`; компонент гарантирован на этом корне через RequireComponent.
  Поиск локального игрока использует `NetworkManager.Singleton.LocalClient.PlayerObject`
  вместо `FindObjectsByType`. RPC, проверки владельца и флаг IsShipInside не изменены.
- `ShipWindZone`: тот же прямой путь через attachedRigidbody в Enter/Exit вместо трёх
  поисков по объекту, родителям и детям. Регистрация зон и силы ветра не изменены.
- `EngineThrusterVisual`: когда RPM/скорость RPM и отклонение/скорость отклонения полностью
  затухли, повторный ApplyUnitPose пропускается. Первый settled-кадр применяет финальную позу;
  движение, плавное затухание и расчёт входов не изменены.

compile + domain reload — завершены, ошибок C# нет; `git diff --check` — чисто. Play Mode, тесты
и Build агентом — **NOT RUN**. Следующий захват должен повторить пролёт/контакт с док-падами,
чтобы сравнить OnTriggerEnter при сопоставимом числе событий.


## T-PERF15 — подтверждение триггеров и общий снапшот зон (`ProjectC_client_2026-11-23_20-22-34.data`)

1 965 steady-state кадров. `DockingPadTriggerBox.OnTriggerEnter` total/self:
2,573 / 2,479 → **0,0005 / 0,0005 мс/кадр**; hotspot устранён. Средний self на вызов —
примерно 2,33 → 0,013 мс (75 событий вместо 1 493, поэтому сравнение на кадр дополнительно
зависит от маршрута). `Physics.SendTriggerEvents` total 2,705 → 0,084 мс/кадр.
`Main Thread` 46,815 → **45,213**, `PlayerLoop` 31,133 → **27,502 мс/кадр**.
`EngineThrusterVisual.Update` self 0,193 → 0,177; в движении визуалы продолжают обновляться,
поэтому существенный дополнительный выигрыш этого компонента не заявляется.

Следующие managed self: `NpcShipWorld.FixedUpdate` 0,723, `ShipShadowBudget.Update` 0,322,
`MarketZone.Update` 0,275, `OuterCommZone.Update` 0,260 мс/кадр. Зоны уже имели раздельные
per-frame снапшоты и всё равно могли повторно обходить SpawnedObjects в один кадр.

Изменения:

- Добавлен общий `ZoneActorSnapshot` для MarketZone и OuterCommZone: один обход
  SpawnedObjects и одно чтение позиций/ID/слоёв на кадр для обоих типов зон.
  Радиусы, маски, polling 0,25 с, debounce и наборы состояния не изменены.
- `ShipShadowBudget`: позиции отфильтрованных камер кэшируются один раз за UpdateShadows
  каждого корабля, вместо чтения Transform для каждого рендерера. Пороги on/off также
  вычисляются один раз. Мировые позиции между тиками не хранятся — Floating Origin сохраняется.

compile + domain reload — завершены, ошибок C# нет; `git diff --check` — чисто. Play Mode, тесты
и Build агентом — **NOT RUN**. Параллельно появившаяся пользовательская правка NetworkPlayer.cs
не изменялась и не входит в пакет. Следующий захват должен проверить оба Update зон и ShipShadowBudget.


## T-PERF16 — остаточные Update зон и кэш камер бюджетов (`ProjectC_client_2026-11-23_21-18-49.data`)

1 325 steady-state кадров. `Main Thread` 45,213 → 57,400 мс/кадр, `PlayerLoop` 27,502 → 38,351,
`EditorLoop` self 13,019. В выборке есть загрузочные и разовые вклады (`Mono.JIT`, запуск Host,
File.Read и GC), поэтому общий FPS нельзя сравнивать как чистый регресс T-PERF15.
`DockingPadTriggerBox.OnTriggerEnter` остаётся на 0,0001 мс/кадр — исправление T-PERF14 стабильно.
Главный managed hotspot: `NpcShipWorld.FixedUpdate` total/self 1,596 / 1,086 мс/кадр
(3 636 вызовов). Для него нужна отдельная атрибуция режимов навигации: снижать частоту всего
FixedUpdate нельзя, поскольку это изменит управление Rigidbody.

Снижение от T-PERF15 в этом маршруте не подтверждено: `MarketZone.Update` total/self
0,282 / 0,275 → 0,439 / 0,426, `OuterCommZone.Update` 0,267 / 0,260 → 0,358 / 0,346;
`ShipShadowBudget.Update` self 0,324 практически без изменений. Остальные регулярные self:
`DoorVisibilityBudget.Update` 0,382, `CityShadowBudget.Update` 0,347 мс/кадр.

Изменения:

- `DoorVisibilityBudget` и `CityShadowBudget`: позиции отфильтрованных камер читаются один раз
  за UpdateShadows и переиспользуются для всех групп/рендереров; квадраты on/off порогов
  вычисляются один раз за тик. Мировые позиции между тиками не хранятся — Floating Origin сохраняется.
- `MarketZone` и `OuterCommZone`: локальный игрок берётся напрямую из
  `NetworkManager.Singleton.LocalClient.PlayerObject`, без TTL-кэша и периодического FindObjectsByType.
  Клиентские проверки игрока и корабля используют sqrMagnitude; квадратный корень остаётся только
  в отладочных сообщениях MarketZone. Polling 0,25 с, debounce, серверные снапшоты и registry не изменены.

compile + domain reload — завершены, ошибок C# нет; `git diff --check` — чисто. Play Mode, тесты
и Build агентом — **NOT RUN**. Исключения `Client handler exited` от MCP — технический шум
stdio-подключений, не ошибки C#. Следующий захват по тому же маршруту должен проверить оба Update
зон и три бюджета теней; NPC навигацию разбирать отдельно по внутренним маркерам/режимам.


## T-PERF17 — подтверждение зон/теней и навигационные зонды NPC (`ProjectC_client_2026-11-23_23-51-45.data`)

1 881 steady-state кадров. `Main Thread` 57,400 → 50,284 мс/кадр, `PlayerLoop` 38,351 → 33,576;
часть общей разницы связана с маршрутом и загрузочными вкладами. T-PERF16 подтверждён по self-time:
`MarketZone.Update` 0,426 → **0,048 мс/кадр**, `OuterCommZone.Update` 0,346 → **0,009**,
`DoorVisibilityBudget.Update` 0,382 → **0,190**, `CityShadowBudget.Update` 0,347 → **0,159**,
`ShipShadowBudget.Update` 0,324 → **0,172**. Изменение маршрута влияет на вызовы, но масштаб
снижения зон подтверждает удаление поиска игрока и лишней работы.

`NpcShipWorld.FixedUpdate` total/self 1,596 / 1,086 → 0,633 / 0,488 мс/кадр — остаётся главным
managed self-кандидатом. `AI.NpcBrain.FixedUpdate` self 0,146 (151 176 вызовов), `Physics.SphereCast`
0,185 (63 881 вызов), `Physics.Raycast` 0,112 (23 541 вызов). Код показал два регулярных неоптимальных
пути: `ResolveCommZone()` выполнял GetComponentInChildren каждый FixedUpdate, а RayClear для мягкого
подлёта к промежуточной точке не имел троттлинга. Свободные пешие NPC также искали палубу через
SphereCast на каждом physics-тике.

Изменения:

- `NpcShipController.TickCruise`: RayClear промежуточной точки выполняется с частотой основного
  лидара (`wallProbeIntervalSec` + фаза NPC), между пробами используется последний результат.
  Смена waypoint сбрасывает пробу; режимы, управление Rigidbody и пороги скорости не изменены.
- `NpcShipController.ResolveCommZone`: ссылка на OuterCommZone кэшируется по locationId маршрута
  и перечитывается при смене маршрута/уничтожении объекта. Мировые координаты не кэшируются.
- `NpcBrain.FixedUpdate`: обнаружение новой платформы — 10 Гц вместо каждого physics-тика.
  Для уже найденной платформы carry, потеря контакта и deck-nav остаются на каждом FixedUpdate.

compile + domain reload — завершены, ошибок C# нет; `git diff --check` — чисто. Play Mode, тесты
и Build агентом — **NOT RUN**. Следующий захват должен проверить NpcShipWorld, SphereCast/Raycast,
AI.NpcBrain.FixedUpdate и сохранение штатного движения NPC по палубам/маршрутам.


## T-PERF18 — атрибуция NPC.Nav, скрытая атмосфера и покой паромов (`ProjectC_client_2026-11-24_00-10-22.data`)

1 604 steady-state кадра. `Main Thread` 50,284 → **37,959 мс/кадр**, `PlayerLoop` 33,576 → **25,111**,
`EditorLoop` self 12,944 → 8,766. Маршрут/загрузочные вклады различаются, поэтому общее снижение
не приписывается только T-PERF17. Подтверждённые эффекты по managed/physics self: `AI.NpcBrain.FixedUpdate`
0,146 → **0,095 мс/кадр**, `Physics.SphereCast` 0,185 → **0,106** (63 881 → 30 168 вызовов).
При этом `NpcShipWorld.FixedUpdate` total/self 0,633 / 0,488 → 0,636 / 0,507: снижения нет,
троттлинг waypoint-RayClear и кэш зоны связи не устранили основной расход. `Physics.Raycast`
0,112 → 0,115 (23 541 → 28 902 вызова), поэтому выигрыш этого участка не заявляется.

Остальные регулярные кандидаты: `ProjectCMainMenuAtmosphere.Update` self 0,132 — скрытая игровая
камера продолжала обновлять материалы/тросы/пыль; `ParomRoute.Update` total 0,430, включая
`Physics.SyncTransforms`, вызывавшийся на каждом кадре даже для стоящей кабинки.

Изменения:

- `NpcShipController.NavTick`: постоянные ProfilerMarker `NPC.Nav.Docked/Lifting/Yawing/Cruising/
  Berthing/Avoiding/AvoidYield/WallFollow/GateApproach/CorridorLeg`. Маркер выбирается по режиму
  на входе и охватывает весь тик; Deep Profile не требуется. Это атрибуция, не заявленная оптимизация.
- `ProjectCMainMenuAtmosphere.Update`: при выключенной atmosphereCamera обновление пропускается.
  При повторном показе камеры обновление продолжается; Build и SetVisible не изменены.
- `ParomRoute.ApplyTrolley`: позиция/поворот записываются только при фактическом изменении,
  глобальный Physics.SyncTransforms вызывается только после такого изменения. Во время движения,
  доворота и Floating Origin (worldPos меняется) синхронизация сохраняется.

compile + domain reload — завершены, ошибок C# нет; `git diff --check` — чисто. Play Mode, тесты
и Build агентом — **NOT RUN**. Следующий захват должен содержать маркеры NPC.Nav.*, а также проверить
ParomRoute, Physics.SyncTransforms и ProjectCMainMenuAtmosphere; стоянка и движение парома желательны.



## T-PERF19 — сплайновый ветер, снапшот NPC.Cruising и общий SyncTransforms (`ProjectC_client_2026-11-24_00-43-27.data`)

1 828 steady-state кадров. `Main Thread` 37,959 → 55,429 мс/кадр, `PlayerLoop` 25,111 → 33,420,
`EditorLoop` self 8,766 → 16,206. Маршрут, активность физики и загрузка редактора различаются,
поэтому общий прирост не относится к регрессу T-PERF18 без контрольного A/B. Скрытая
`ProjectCMainMenuAtmosphere.Update` исчезла из top self — отключение обновления скрытой атмосферы
подтверждено. Паромы в этом захвате двигались: `ParomRoute.Update` total 0,701,
`Physics.SyncTransforms` total 0,751 (10 173 вызова); это не сравнение со стоящими кабинками.

Атрибуция NPC сработала: `NpcShipWorld.FixedUpdate` total 1,154 / self 0,043 мс/кадр;
`NPC.Nav.Cruising` total 0,797 / self 0,589 (65 300 вызовов), `NPC.Nav.WallFollow` total 0,252 /
self 0,222 (11 915 вызовов). `Physics.Raycast` self 0,271 (59 861 вызов). Новый главный managed
self-кандидат — `WindManager.FixedUpdate`: 1,616 мс/кадр, 5 080 вызовов. Следующие регулярные
участки после этого пакета: `NetworkPlayer.InteractionHint` self 0,737 и `NPC.Nav.WallFollow`.

Изменения:

- `WindManager`: кэш кораблей каждой сплайновой зоны переведён с Dictionary на предварительно
  ёмкий List — записи только очищаются, добавляются и последовательно обходятся, хеширование не нужно.
  При применении силы позиция корабля читается максимум один раз и только для Shear/центрирования.
  Round-robin, частота детекции, ближайшие точки сплайнов, направления и величины сил не изменены.
- `NpcShipController.TickCruise`: Time.time и Rigidbody.position кэшируются один раз на тик
  (30 и 23 повторных чтения соответственно убраны). MoveRotation не меняет position синхронно;
  частота управления, зонды, watchdog, скорости и переходы режимов не изменены.
- `ParomRoute`: несколько кабинок делят максимум один глобальный Physics.SyncTransforms за кадр.
  Запись изменившихся позиций/поворотов и синхронизация после движения/ребейза сохранены.

compile + domain reload — завершены, ошибок C# нет; `git diff --check` — чисто. Play Mode, тесты
и Build агентом — **NOT RUN**. Следующий захват 30–60 секунд по тому же маршруту должен проверить
WindManager.FixedUpdate, NPC.Nav.Cruising, число Physics.SyncTransforms и отсутствие рассинхрона паромов.
