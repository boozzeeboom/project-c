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

## Реприоритизация (по данным Замера 2)

Логи (бывшая Фаза 5) и null-GetComponent (бывшая Фаза 3) подняты вверх: это 30% мусора
и правки с низким риском (guards + кэш). Порядок: Фаза 5/3 → Фаза 2 (нужны данные сцены
от пользователя: слои/коллайдеры в радиусе фермы) → Фаза 4 (ветер) → Фаза 6 → Фаза 7 (старт хоста).
