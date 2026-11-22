# План исправлений перфоманса (захват 2026-11-22) — `T-PERF03`

База: `00_CAPTURE_2026-11-22_ANALYSIS.md`. Принцип тот же, что в `25092026`: маленький
дифф → проверка компиляции (Console 0 errors) → отдельный коммит → запись в `ITERATIONS.md`.
Play Mode и скриншоты агент не запускает: runtime-проверка за пользователем (в доках — NOT RUN).

Порядок — по измеренной цене, каждая фаза независима (можно остановиться на любой).

## Статус (2026-11-22)

| Фаза | Статус | Коммит |
|---|---|---|
| 1 — триггерный объём | код готов, compile 0 | `b2b1e7b9` |
| 2 — полл зон связи | код готов, compile 0 | `d2616aa2` |
| 3 — полл рынков | код готов, compile 0 | `2fb38061` |
| 4 — подсказка в `Update` | код готов, compile 0 | `c893fb4a` |
| 5 — кап регистрации палуб | код готов, compile 0 | `6ddb25f1` |
| 6 — `verboseLogging` столкновений | **применено локально, в git не попадает** | — |
| 7 — журнал | готово | этот коммит |

Фаза 6 не попадает в коммит: `Assets/Resources/` целиком в `.gitignore` (строка 51),
огибающих правил нет — файл `Assets/Resources/ShipCollisionDamage.asset` в git никогда
не лежал (`git log -- <path>` пуст). Правка применена на диске через `SerializedObject`,
на чистом клоне ассета нет и вступают в силу дефолты кода (`verboseLogging = false`).

## Фаза 1 — Триггерный объём коммуникационных зон (P0-1) — риск средний

Проблема: `OuterCommZone.Awake()` ставит `_sphere.radius = commRange`, из-за чего 7 зон
держат trigger-сферу 500–1000 м в мире с 21 842 коллайдерами. `Physics.SendTriggerEvents`
в захвате: 5.9 с (кадр 0), 20–30 МБ аллокаций на кадр — при том что наши обработчики дают
~10 ms и ~1.7 КБ.

Файлы: `Assets/_Project/Scripts/Docking/Zones/OuterCommZone.cs`.

Правка:
1. Новое поле `[SerializeField] float triggerRadius = 60` (тултип с цифрами замера);
   `_sphere.radius = triggerRadius` вместо `commRange`.
2. Полл по всей дистанции связи не меняется — он уже источник истины с debounce
   (`MISS_THRESHOLD = 3`), и `PollLocalPlayerZone()` тоже считает дистанцию сам.

Почему не «выключить триггер совсем»: мгновенная реакция на вход в зону нужна для
T-клавиши и подсказок; 60 м — компромисс (60 м на крейсерской проходятся за ~1 с).
Цена: до `pollInterval` (250 мс) задержки на границе зоны.

Данные сцены не правятся (сцена не трогается): радиус приходит из кода в `Awake`.
Проверка: Compile 0 errors; пользователь — новый захват, `Physics.SendTriggerEvents`
должен упасть с секунд/мегабайт до шума, T-клавиша и посадка работают.

## Фаза 2 — Полл коммуникационных зон (P0-3) — риск средний

Проблема: `PollPlayersInRange()` и `PollShipsInRange()` — два `Physics.OverlapSphere`
с одинаковыми аргументами на тик, каждый аллоцирует `Collider[]` (41.38 КБ на зоне
STN-PRM-001, 5 304 попадания). Маска `-1`, слоя игрока/корабля в проекте нет.

Файлы: `Assets/_Project/Scripts/Docking/Zones/OuterCommZone.cs`.

Правка: вместо физического запроса — проход по заспавненным объектам
(`NetworkManager.Singleton.SpawnManager.SpawnedObjects`, этим словарём уже пользуются
`DockingServer`, `DockingWorld`, `MetaRequirementRegistry`) и сравнение `sqrMagnitude`
с `commRange²`. Один проход наполняет два набора (`_pollPlayersFound`, `_pollShipsFound`),
дальше — существующая логика add/debounce/remove без изменений.

Почему так, а не `OverlapSphereNonAlloc`: NonAlloc с буфером на 64–256 слотов при 5 304
попаданиях и маске `-1` молча обрезает результат и может потерять игрока. Проход по
20 кораблям + игрокам корректен по построению и не зависит от физики.

Риск: `NetworkPlayer` и `ShipController` — сетевые поведения, они обязаны лежать на корне
`NetworkObject`, поэтому `GetComponent` с корня их находит (старый код искал
`GetComponentInParent` от коллайдера).
Проверка: Compile 0 errors; пользователь — T-key/CommPanel, серверный `IsPlayerInRange`,
докинг и маркет-детект работают как раньше; GC зон → ~0.

## Фаза 3 — Полл рыночных зон (P0-3, продолжение) — риск минимальный

Проблема: те же `Physics.OverlapSphere` в `PollPlayersInRadius` / `PollShipsInRadius`,
12 зон × 2 × 4 Гц = 96 массивов/с. Радиусы малые (5–30 м), CPU дешёвый — цель только GC.

Файлы: `Assets/_Project/Trade/Scripts/Network/MarketZone.cs`.

Правка: `Physics.OverlapSphereNonAlloc` с per-instance буфером `Collider[128]`. Для 30 м
переполнение практически исключено; если случится — работает существующий debounce
(`MISS_THRESHOLD = 3`), зона не «выкинет» игрока с одного промаха.

Почему не общий проход по `SpawnedObjects` (как в Фазе 2): у рынков два разных радиуса
(`tradeRadius` для игроков, `shipDockRadius` для кораблей), проход всё равно один — но
правка NonAlloc меньше и не меняет семантику детекта. Единый проход — кандидат в следующий этап.

Проверка: Compile 0 errors; пользователь — открытие маркета, покупка (RPC `RequestBuyRpc`
не получает `NotInZone`).

## Фаза 4 — Подсказка взаимодействия в `NetworkPlayer.Update` (P0-2) — риск минимальный

Проблема: `UpdateInteractionHint()` каждый кадр → `FindObjectsByType<DoorController>`
(2 800 дверей = 21.94 КБ) + `FindObjectsByType<NpcController>` + `GetComponent<Collider>`
на каждую дверь. 11.87 ms и 44 КБ на кадр.

Файлы: `Assets/_Project/Scripts/Player/NetworkPlayer.cs`.

Правка:
1. Троттлинг резолвера: `INTERACTION_HINT_INTERVAL = 0.2f` (5 Гц) вместо каждого кадра;
   `PublishInteractionHint` уже дедуплицирует, поэтому визуально ничего не меняется.
2. `GlobalMotionRuntimeEvidenceProbe.RecordEvent(..., $"…")` — под `if (...IsActive)`,
   чтобы строка не строилась при выключенном пробнике.

Проверка: Compile 0 errors; пользователь — подсказки F/E появляются/исчезают без
заметной задержки; `NetworkPlayer.Update` в захвате — без 44 КБ/кадр.

## Фаза 5 — Кап регистрации палуб (P1-4) — риск минимальный

Проблема: `ProcessPendingRegistrations()` сбрасывает статический счётчик в начале каждого
вызова, поэтому кап «1 Add на кадр» держится только внутри одного объекта; 20 кораблей
могут зарегистрироваться пачкой (кадр 75: 87 + 62 ms, внутри `GC.Collect` 22 ms).

Файлы: `Assets/_Project/Scripts/Ship/ShipDeckNav.cs`.

Правка: вместо сброса — метка кадра (`s_registrationsFrame != Time.frameCount`).
Проверка: Compile 0 errors; пользователь — `ShipDeckNav.DumpStats()` (spawn ≈ 20, drift ≈ 0),
экипаж ходит по палубам; в захвате — не более одного `NotifyNavMeshAdded` на кадр.

## Фаза 6 — `verboseLogging` столкновений (P1-5) — риск минимальный

Проблема: `Assets/Resources/ShipCollisionDamage.asset` содержит `verboseLogging: 1`;
`ShipController.OnCollisionEnter` пишет лог на каждое столкновение (55 ms / 32 КБ на кадре 76).

Файлы: `Assets/Resources/ShipCollisionDamage.asset`.

Правка: `verboseLogging: 1 → 0`.
Проверка: Compile не нужен (ассет); пользователь — столкновения без спама в консоли,
урон/протечки груза считаются как раньше (флаг влияет только на логи).

Факт, найденный при выполнении: `Assets/Resources/` в `.gitignore`, поэтому коммита у этой
фазы нет — правка живёт только на машине, где применена. См. «Статус» выше.

## Фаза 7 — журнал этапа — риск нет

Файлы: `docs/world/optimization/22112026/ITERATIONS.md`, `Assets/_Project/Docs/ITERATIONS.md`.

## Что сознательно НЕ делается в этом этапе

| Тема | Причина |
|---|---|
| UIElements 22 ms / 1 МБ | только кадры меню/загрузки (0 и 333), в геймплейных кадрах узла нет |
| `PrimumLightPool.Check` 2.16 ms | значения (`adaptiveMinInterval = 0.1`) сериализованы в сцене и настроены осознанно (T-LIGHT18); нужна отдельная ручная проверка ощущения |
| Слой `Player`/`Ship` + сужение маски | меняет ProjectSettings и всю физику сцены — отдельный этап |
| `GetComponentNullErrorMessage` в триггерах | механизм не подтверждён, цена мала (0.84 КБ/событие) |
| Стартовые one-off (`ALIC.Start`, `SpawnRoutine`, `RestoreCoroutine`) | разовые, не фрейм-тайм |
| `WindManager.FixedUpdate` | в этом захвате не попал в худшие кадры, тема `25092026` Фазы 4 |

## Протокол проверки (пользователь)

1. Перед Play: Console → Clear (иначе сравниваем накопленную историю, урок логов 5/6 из `25092026`).
2. Тот же сценарий, что и в исходном захвате: host-старт в `BootstrapScene`, полёт до
   коммуникационной зоны, вход/выход из зоны, посадка на корабль, столкновение, рынок.
3. Снять захват, сравнить: `Physics.SendTriggerEvents`, `NetworkPlayer.Update`,
   `OuterCommZone.Update`, `MarketZone.Update`, `ShipDeckNav.LateUpdate`.
4. Цифры записать в `ITERATIONS.md`.
