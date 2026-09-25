# T-PERF02 — Iteration Log

База: `00_CAPTURE_2026-09-25_ANALYSIS.md`. План: `01_FIX_PLAN.md`.

## Фаза 1 — выполнена (код), замер — за пользователем

Файлы: `Assets/_Project/Scripts/Docking/Zones/OuterCommZone.cs`,
`Assets/_Project/Trade/Scripts/Network/MarketZone.cs`.

Что сделано (поведение бит-в-бит, только распределение по кадрам + GC):
1. `_pollTimer = Random.value * pollInterval` в `OnEnable` — 22 зоны больше не поллят
   синхронно в один кадр (пилы f104/f131/f225).
2. Per-instance `_pollFound` (`HashSet<ulong>`) + `_pollToRemove` (`List<ulong>`),
   `.Clear()` вместо `new` каждый полл. Физический запрос (`OverlapSphere`) не тронут.
3. Маркеры `// T-PERF02` в коде.

Верификация: domain reload свежий, Console — 0 errors (только pre-existing CS0618
`FindObjectsSortMode` в несвязанных файлах + benign MCP-нотисы).

Ручная проверка (пользователь, NOT RUN агентом):
1. Снять захват профайлера в том же сценарии (20+ кораблей, те же зоны).
2. Ожидание: пилы >50 ms от зон пропали, сумма `OuterCommZone+MarketZone` упасть в разы,
   GC этих скриптов → ~0. T-key/CommPanel/маркет-детект работают как раньше.
3. Цифры записать сюда.

## Замер 2 — `ProjectC_client_2026-09-25_18-44-11.data` (1279 кадров, разбор пользователя)

Нормализация на кадр (кадров в 2.15× больше, сравниваем per-frame):

| Метрика | Замер 1 (595) | Замер 2 (1279) | Вывод |
|---|---|---|---|
| PlayerLoop median | 18.5 ms | 23.7 ms (p90 71, p99 140) | хуже (но сценарий мог отличаться: EditorLoop 44 s vs 19.3 s) |
| GC всего | 101 KB/кадр | 146 KB/кадр | хуже |
| Зоны CPU | 8.93 ms/кадр | 8.33 ms/кадр | −7%, пики f131/f225 на месте (99/97 ms) |
| Зоны GC | ~37 KB/кадр | ~27 KB/кадр | −27% (буферы Фазы 1 работают) |
| Логи (`LogStringToConsole`) | 2.1 MB | 24.5 MB (×11!) | новый P0 |

Честный вердикт по Фазе 1: переиспользование буферов снизило GC зон, десинхрон
не убрал пилы — доминирует сам `OverlapSphere r=1000 + ~0` и C#-цикл по сотням хитов
(«Ферма Примума 0_1» по-прежнему худшая). Без сужения маски (Фаза 2) дальше не уехать.

Новые факты из Замера 2:
- Логирование взорвалось: `LogStringToConsole` 24.5 MB + `CallLogCallback` 732 ms / 21.4 MB.
  Источники: `StormCellDirector.Update` (69 кадров), `GlobalSceneNativeExecutor.Update` (66),
  `LocalDensityBuffer.Update` (31), `ParomRoute.Update` (29), `NetworkPlayer.Update` (28).
  Вместе с `GetComponentNullErrorMessage` (46 468 вызовов, 29.7 MB) — 54 MB, 30% мусора.
- Спайк старта хоста f706: `EventSystem.Update` 1370 ms → `GlobalMotionPilotRuntime.
  PrepareAndStartHost()` [Coroutine] 1366 ms / 19.1 MB GC (AddComponent, DTO, статические
  конструкторы, Burst JIT, `FindObjectsOfType`) + f707/f595/f628 рядом. → новая Фаза 7.
- `WindManager.FixedUpdate` 3 054 ms, 0 GC; 3 fixed steps на просевшем кадре, 17 на старте.

## Фаза 5a — тишина логов (выполнена, замер — за пользователем)

Файлы: `Scripts/World/Clouds/StormCellDirector.cs`, `Scripts/World/Clouds/LocalDensityBuffer.cs`,
`Scripts/World/FloatingOrigin/Network/GlobalSceneNativeExecutor.cs`,
`Scenes/BootstrapScene.unity` (2 строки).

Что сделано:
1. `StormCellDirector._logDebug`: дефолт `true` → `false` (лог Shader push раз в секунду).
2. `LocalDensityBuffer._verboseLogging`: дефолт `true` → `false`; варнинг «Splat queue full»
   теперь только в verbose (спамил каждый кадр при переполнении). Поле и `SetVerboseLogging`
   для AdminLogBus не тронуты.
3. `GlobalSceneNativeExecutor`: новый `[SerializeField] _debugLog = false` + guard в начале
   `LogReadiness` (readiness-лог ~13 KB/вызов, раньше без флага).
4. Сцена: `_logDebug: 1→0`, `_verboseLogging: 1→0` (сериализованные значения перебивали дефолты).

Верификация: domain reload свежий, Console — 0 errors.

Ручная проверка (пользователь, NOT RUN агентом):
1. Новый захват: `LogStringToConsole`/`CallLogCallback` должны collapse к шуму;
   шторм/облака/FO-визуально без изменений (логи только телеметрия).
2. Вернуть при нужде: флаги в инспекторе BootstrapScene (StormCellDirector, LocalDensityBuffer,
   GlobalSceneNativeExecutor → _debugLog).

## Замер 3 — `ProjectC_client_2026-09-25_19-19-06.data` (1723 кадра, разбор MCP)

Метод: `ProfilerDriver.LoadProfile` + `GetHierarchyFrameDataView`, топ по всем кадрам
(merged view). Точные итоги по всем 1723 кадрам:

| Метрика | Замер 2 (1279) | Замер 3 (1723) | Вывод |
|---|---|---|---|
| Зоны CPU (OC+MZ) | 8.33 ms/кадр | 6.9 ms/кадр | −17%, но всё ещё #1 |
| Зоны GC | 27.3 KB/кадр | 38.9 KB/кадр (65.5 MB) | хуже (больше объектов в радиусах?) |
| `LogStringToConsole` | 19.1 KB/кадр (24.5 MB) | 17.1 KB/кадр (28.7 MB) | −10% на кадр |
| `CallLogCallback` | 21.4 MB / 732 ms | 24.6 MB / 727 ms | flat |
| `GetComponentNullErrorMessage` | 23.2 KB/кадр (29.7 MB) | 23.1 KB/кадр (38.8 MB) | flat, всё ещё огромно |
| `StormCellDirector` | 69 кадров с логами | 0.00 MB GC / 2.1 ms self | ✅ Фаза 5a сработала |
| `LocalDensityBuffer` | 31 кадр | 0.00 MB GC | ✅ Фаза 5a сработала |
| `GlobalSceneNativeExecutor` | 66 кадров | 0.22 MB (остаток — реальная работа, не логи) | ✅ guard работает |

Родители логов Замера 3 (точные, через предков сэмплов):
`ShipDeckNav.LateUpdate` 18.4 MB × 10 (!), `PrepareAndStartHost` 4.1 MB × 1,
`ShipCrewSpawner.SpawnWhenReady` 1.9 MB × 3, `GlobalMotionControlledRebaseSlice.Update`
1.5 MB × 5, `NavMesh.Internal_CallPreUpdateListeners` 0.9 MB × 3,
`ShipController.CreateKeyInstanceWhenReady` 0.8 MB × 1, `ShipPositionServer.Update`
0.56 MB × 15 (хроника), `ParomRoute.Update` 0.5 MB × 38 (хроника, самое частое),
`NpcBrain.Update` 0.22 MB × 6. `NetworkPlayer.Update` упал до 87 KB × 2.

Важно: собственные логи `ShipDeckNav` уже под `#if UNITY_EDITOR` (прошлая работа T-PERF) —
18.4 MB идут изнутри `NavMesh.AddNavMeshData` при перерегистрации палуб
(эпизодически, при движении кораблей/сдвигах). Чинить вслепую нельзя: рядом контракт
`T-FO06DF` (кулдаун 30 с + сброс при сдвиге). Нужен отдельный разбор.

Новые сигналы: `OuterCommZone.OnTriggerEnter` 85 ms + `DockingPadTriggerBox.OnTriggerEnter`
52 ms за захват (триггерный шторм при спавне/телепортах); `PrepareAndStartHost` 302 ms self
(спайк старта хоста жив, Фаза 7 в силе).

Вердикт: Фаза 5a подтверждена (3 источника → 0). Дальше два рычага:
(a) зоны — только Фаза 2 (маска), кодом больше не выжать;
(b) null-GetComponent 38.8 MB (Фаза 3, кодовая, можно начинать) +
`ShipDeckNav`/перерегистрации (нужен разбор) + хроники `ParomRoute`/`ShipPositionServer`.

## Пункт (а): null-GetComponent — поиск источника (2026-09-25, в работе)

Проверено через MCP-профайлер (все кадры Замера 3, Default view):
- Прямые родители ошибок: `GlobalMotionWorld.FixedUpdate` (×4–5/кадр),
  `NetworkPlayer.FixedUpdate` (×1–2), `SkillAnimationPlayer.LateUpdate`,
  `GlobalMotionPoseAdapter.LateUpdate`, `NetworkTickSystem.Tick`, разово `PickupItem.Start`.
  Промежуточных C#-маркеров нет (вызовы инлайновые), callstack'и в захвате не писались,
  метаданных у сэмплов нет (metaCount=0).
- Исключено: missing-скрипты в BootstrapScene (скан: 0), `GetComponent("string")`
  (нет в коде), `SkillAnimationPlayer.Update/LateUpdate` (чисто),
  `SkillInputService.Update` (`TryGetComponent`, кэши; остальные GetComponent —
  event-путь `TryActivate`), `NetworkPlayer.FixedUpdate` (нет GetComponent в теле —
  ошибка из инлайновых `Coordinates.*`/`RecordEvent`), `GlobalMotionWorld` (делегирует
  в `PoseAdapter.ApplyFixedPose/PrepareBaseline`), Editor.log чист.
- Подозрение: внутри `GlobalMotionPoseAdapter` (`TryPlanHierarchy`/`SupportedStructure`/
  `ResolveRole`) или NGO-внутренности тика; либо missing-скрипты в WorldScene_0_0
  (не загружена в редакторе — не проверена).
- Стоп-условие: без текста ошибки из консоли Play-сессии или deep-профиля дальше гадание.

## Лог `оптим_сент_2.txt` (3027 строк, 758 варнингов) — вердикт

- Создания агентов со стеком `NpcBrain:857` (был `:852` до сдвига строк фиксом):
  **20 → 3 (−85%)** — фикс прокси сработал.
- НО «Failed to create agent» в целом: 692 → 675 (−2%). Разбор полных блоков:
  636/675 несут стек `AddNavMeshData ← ShipDeckNav.cs:283`, 39 — пустой стек.
  Это НЕ создания, а реасессмент голодающих агентов внутри каждого Add.
- Главный вывод: **636 `AddNavMeshData` за одну сессию** — лавина перерегистраций
  палуб (каждый Add 6–50 ms + ~1.8 MB логов). Причина лавины неизвестна
  (дрейф >2500 м? FO-сдвиги через синхронный `RegisterAt` в обход round-robin?
  волна спавнов?) — для этого поставлены счётчики ниже.
- Остальной фон: `ShipCargoVisual` пустые префабы ×22, спавн-гонки, Warp ×12 — без изменений.

## Счётчики AddNavMeshData (выполнено, замер — за пользователем)

`ShipDeckNav`: статические `s_addSpawn/s_addDrift/s_addFoRebuild/s_addFoRestore`,
причина проставляется в 4 точках (`OnNetworkSpawn`, дрейф в `LateUpdate`,
`TryRebuild/TryRestoreFloatingOriginSnapshot`), инкремент в `RegisterAt`,
чтение — `ShipDeckNav.DumpStats()` (консоль/MCP, проверено: возвращает нули).
Ноль логов, ноль поведения. Проверка: Console 0 errors.
Протокол: поиграть как обычно → в конце вызвать `DumpStats()` → цифры сюда.
Ожидаемый вывод: какая причина даёт сотни (ставка: drift или fo-rebuild).

## Счётчики ответили + фикс парного Add (2026-09-25)

`DumpStats()` вживую: `spawn=20 drift=20 fo-rebuild=0 fo-restore=0` (Add один,
`Assets/_Project/Scripts/Ship/ShipDeckNav.cs:316` — счётчики полны).
Выводы:
1. FO-путь невиновен (нули) — синхронные перестройки не текут.
2. 636 Add в логе — накопления нескольких сессий (консоль не чистилась, статики
   сбрасываются перезагрузкой домена). За сессию: 20 spawn + 20 drift.
3. Каждый корабль регистрируется ДВАЖДЫ: spawn-Add, затем телепорт host-старта
   срывает дрейф-проверку (>2500 м) — второй Add мусорный, т.к. первый и так встал
   на свежую позицию. Причина: `_nextReregistrationTime` стартовал с 0 и штамповался
   только в drift-ветке.
4. Каждый лишний Add = 6–50 ms + ~1.8 MB + реасессмент голодающих агентов (≈1 варнинг).

Фикс (коммит ниже): штамп кулдауна 30 с в успешном `RegisterAt`. Легитимный дрейф
не страдает (2500 м на крейсерской — дольше 40 с), FO-сброс кулдаунов (T-FO06DF)
сохранён. Проверка: Console 0 errors.
Протокол: новая сессия (домен перезагружен компиляцией — счётчики с нуля) →
в конце `DumpStats()` (ожидание: `drift≈0`) + счётчик «Failed to create agent».

## Пункт (а), продолжение: варнинги из `Q:\Project-c_logs\оптим_сент_1.txt` (3107 строк, 67 типов)

Разблокировано логом пользователя (ошибок в консоли нет, только варнинги).
Доминанта: **692× «Failed to create agent because it is not close enough to the NavMesh»**
со стеком через `NavMesh.AddNavMeshData ← ShipDeckNav.cs:283`. Нативный варнинг агента,
приписанный стеку шедшего в тот момент C# — реальный виновник не Add, а создание
агентов вдали от меша. Единственное место создания агентов в проекте —
`NpcBrain.EnsureProxy:852` (`AddComponent<NavMeshAgent>`), и прокси-GO там создавался
в origin (0,0,0, км от палубного меша), а варпался на меш только потом
(`WarpProxyToNpc`). Сопутствующие находки (не чинили): `ShipCargoVisual` пустой
`_boxPrefabs` ×22, `ShipHull`/`ShipOwnershipRequirement`/`ResourceNode`/`MetaRequirement`
spawn-гонки ×19+, `Animator` без параметров `Work`/`WorkVariant`, `PlayerTarget`
«HP init FAILED after 20 retries», `NavMeshAgent.Warp` в `RebaseSlice:363` ×12.

Фикс (коммит ниже): `NpcBrain.EnsureProxy` — позиция прокси в нав-кадре
(`DeckLocalToNav`, та же математика, что в `WarpProxyToNpc`) ДО `AddComponent`.
Поведение сохранено (следом всё равно Warp), убран только спам создания.
Проверка: Console 0 errors; пользователь смотрит счётчик «Failed to create agent»
в следующем захвате (ожидание → ~0) + `LogStringToConsole`/`CallLogCallback` вниз.
  Нужно от пользователя: открыть Console → Clear → Play 10 с → прислать первый красный
  текст (или скрин). Альтернатива: deep-профиль одного прогона.

## Реприоритизация (по данным Замера 2)

Логи (бывшая Фаза 5) и null-GetComponent (бывшая Фаза 3) подняты вверх: это 30% мусора
и правки с низким риском (guards + кэш). Порядок: Фаза 5/3 → Фаза 2 (нужны данные сцены
от пользователя: слои/коллайдеры в радиусе фермы) → Фаза 4 (ветер) → Фаза 6 → Фаза 7 (старт хоста).
