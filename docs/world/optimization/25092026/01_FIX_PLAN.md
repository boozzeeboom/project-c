# План исправлений перфоманса (захват 2026-09-25) — `T-PERF02`

База: `00_CAPTURE_2026-09-25_ANALYSIS.md`. Принцип: фазы от безопасного к рискованному,
каждая фаза — маленький дифф → Compile (Console 0 errors) → замер → запись в `ITERATIONS.md`.
Play Mode и скриншоты не запускаются агентом — runtime-проверка пользователем (NOT RUN в доках).

## Фаза 1 — Десинхрон поллов + переиспользование буферов (зоны) — P0, риск минимальный

Проблема: 10 `OuterCommZone` + 12 `MarketZone` стартуют с `_pollTimer = 0` и поллят
синхронно в один кадр (кадры-пилы f104/f131/f225: 80–133 ms только зоны).
Каждый полл аллоцирует `new HashSet<ulong>` + `new List<ulong>` (22 MB мусора за захват).

Файлы:
`Assets/_Project/Scripts/Docking/Zones/OuterCommZone.cs` (`Update:119`, `PollPlayersInRange:220`,
`PollShipsInRange:260`), `Assets/_Project/Trade/Scripts/Network/MarketZone.cs`
(`Update:157`, `PollPlayersInRadius:252`, `PollShipsInRadius:302`).

Правки (поведение бит-в-бит, меняется только распределение по кадрам):
1. Начальный сдвиг таймера: в `OnEnable`/`Awake` `_pollTimer = Random.value * pollInterval`
   (в MarketZone константа `POLL_INTERVAL`). Размазывает 22 полла на ~15 кадров.
2. Переиспользуемые буферы: per-instance `HashSet<ulong> _pollFound` + `List<ulong> _pollToRemove`
   (`.Clear()` вместо `new`). Физический запрос (`OverlapSphere`) не трогаем.
3. Комментарии `// T-PERF02`.

Почему безопасно: интервал, радиусы, debounce-логика, trigger-путь не меняются.
Проверка: Compile 0 errors; пользователь снимает новый захват — пилы >50 ms должны пропасть,
сумма зон упасть в разы; GC зон → ~0.

## Фаза 2 — Сужение запросов зон + аномалия «Ферма Примума 0_1» — P0, риск средний

Проблема: `commRange = 1000м` + `pollLayerMask = ~0` (`OuterCommZone.cs:33,39`);
один экземпляр даёт 82.93 ms / 136.6 KB против 0.1–0.3 ms у остальных — в радиусе
фермы, видимо, сотни коллайдеров. `MarketZone` аналогично (`~0` в `:256,304`).

Шаги:
1. Выяснить, что лежит в радиусе фермы (спросить пользователя / посмотреть сцену):
   какие слои и сколько коллайдеров возвращает километровый оверлап.
2. Сузить `pollLayerMask` в инспекторе до слоёв игроков/кораблей (поле уже есть,
   тултип уже советует — `OuterCommZone.cs:36-39`). Код не меняется, только данные сцен.
3. Если сужения мало — `OverlapSphereNonAlloc` с per-instance буфером + честный fallback
   при переполнении (не молчаливая обрезка: лог-счётчик переполнений).
4. `Npc_peacfull_market_zone` (25.97 ms / 69 KB) — та же проверка.

Почему отдельно от Фазы 1: меняется множество детектируемых коллайдеров —
нужна ручная проверка пользователя (игрок/корабль детектится, T-key/маркет работают).

## Фаза 3 — `GetComponentNullErrorMessage` 12 MB — P1, риск средний

Проблема: 18 656 вызовов (~31/кадр) из 7 точек: `NetworkPlayer.Update/FixedUpdate`,
`SkillAnimationPlayer.Update/LateUpdate`, `GlobalMotionWorld.Update/FixedUpdate`,
`GlobalMotionPoseAdapter.LateUpdate`, `NetworkBehaviourUpdate`.

Шаги:
1. Найти конкретные строки: `GetComponent<T>()` без null-check в `Update`-пути, результат
   которых null каждый кадр (подозреваемые в `NetworkPlayer.cs`: `:647,743,747,785,831`).
2. Закэшировать в `Awake`/`OnEnable` (как `WindManager` кэширует) либо убрать вызов из
   горячего пути. По одному месту за итерацию, Compile после каждого.
3. `GlobalMotionWorld.FixedUpdate` (44.8 KB только null-ошибок на f0) — первый кандидат.

## Фаза 4 — `WindManager` fixed-step спираль — P1, риск средний

Проблема: `WindManager.FixedUpdate:162` → `DetectShipsInZone:289` (`SplineUtility.GetNearestPoint`
на корабль). Просевший кадр → 3+ fixed steps → кадр ещё тяжелее. На кадре 0 — 17 шагов,
14.24 ms одного WindManager.

Шаги:
1. Проверить `_splineZonesPerFrame` / `_splineDetectionStep` — поднять шаг детекции,
   уменьшить зон на fixed step.
2. Рассмотреть перенос детекции из `FixedUpdate` в `Update`-троттлинг (силы и так
   применяются из кэша — `ApplyAllCachedForces` дёшево). Поведение ветра визуально
   не должно измениться — проверка пользователем.
3. `SpeedTreeWindManager.Update` в 595/595 — проверить, можно ли троттлить.

## Фаза 5 — Логи в горячем пути — P1, риск низкий

Проблема: `LogStringToConsole` 2.11 MB, 162 вызова × ~13 KB. Родители:
`NetworkPlayer.Update` (28 кадров), `GlobalSceneNativeExecutor.Update` (28),
`StormCellDirector.Update` (27), `ParomRoute.Update` (13), `LocalDensityBuffer.Update` (13).

Шаги: по образцу `T-PERF01` — `_debugLog` guard / `#if UNITY_EDITOR` на найденные
незащищённые `Debug.Log` в этих файлах. Проверить каждый guard не душит нужный варнинг.

## Фаза 6 — Мелкие циклы — P2

- `NetworkPlayer.Update` → `FindObjectsByType` почти каждый кадр (594/595): кэш/реестр.
- `PickupDeckRide.LateUpdate` → `SphereCast` в 595/595 кадров: троттлинг или событие.
- `Instantiate` ровно 1/кадр во всех 595 кадрах: найти кто (подозрение — пул отсутствует).
- `NpcShipWorld.FixedUpdate` 6.34 MB (1 001 `GC.Alloc` на f0): разобрать после Фаз 1–3.
- `CharacterPanel` 440 KB + `MarketPanel` 286 KB за перерисовку: dirty-check перед Rebuild.

## Порядок и приёмка

Фазы идут строго по порядку 1→6, каждая коммитится отдельно (`T-PERF02: фаза N — ...`).
После каждой фазы: Compile 0 errors + пользовательский замер (захват профайлера
в том же сценарии). Критерий готовности фазы: цифры в `ITERATIONS.md`.
Откат любой фазы — один `git revert`, фазы независимы.
