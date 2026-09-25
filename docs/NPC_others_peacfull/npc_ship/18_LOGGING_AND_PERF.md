# 18 — Логирование NPC-кораблей: что выключено и как вернуть

> **Project C: The Clouds** | Тикет T-NS-PERF01 (коммит `b295e7a3`)
> **Статус:** тихий режим для перф-замеров. Этот документ — единственная точка
> правды по выключателям. Изменения поведения (полёт, доки, FO) — нет,
> только тишина.

---

## 1. Что выключено (4 места)

| # | Что | Где | Как выключено |
|---|---|---|---|
| 1 | CSV-лог + summary в консоль | `PeacefulShip/Core/NpcShipNavLog.cs:21` | `public static bool Enabled = false` |
| 2 | `debugMode` синглтонов | `NpcShipServer.cs`, `NpcShipTrafficManager.cs`, `NpcShipClientState.cs`, `NpcCargoService.cs`, `NpcShipWorld.cs` | дефолт `false` (было `true`, кроме кораблей) |
| 3 | Сериализованный флаг сцены | `BootstrapScene.unity`, `[NpcShipServer]` | `debugMode: 1 → 0` (1 строка) |
| 4 | — | корабли/сейвы | ничего делать не надо: кораблей нет в сценах (только персистентный спавн), префабы уже `debugMode=false`, сейвы флаг не хранят |

## 2. Как вернуть разбор (по шагам, именно в этом порядке)

1. **CSV + summary:** в `NpcShipNavLog.cs` поставить `Enabled = true`,
   дождаться компиляции (`is_compiling == false`), проверить консоль
   (`[NpcShipNavLog] logging to ...` при первом переходе).
2. **Консольные детали:** включить нужные `debugMode`:
   - отдельный корабль — инспектор префаба `Prefabs/Ships/<Имя>` (поле Debug);
   - сервер/координация — `[NpcShipServer]` в Bootstrap (инспектор);
   - рантайм-синглтоны (`TrafficManager`, `World`, `ClientState`, `Cargo`) —
     только кодом (дефолт), сцены их не хранят.
3. Прогон → CSV в `%USERPROFILE%\AppData\LocalLow\DefaultCompany\ProjectC_client\`,
   разбор как раньше (BEAT/TRANS/SUMMARY).
4. После разбора — вернуть `false` (перф-замеры только в тишине).

## 3. Что шумит даже в тихом режиме (сознательно оставлено)

* Разовые строки за сессию: `PeakRegistry built: N peaks + M meshes`,
  `TrafficManager Created`, `NpcShipWorld Created` (только при debug).
* Варнинги о проблемах: нет расписания / `npcInstanceId = 0` / нет
  `ShipController`, `ConfirmTouchdown`-цепочка, displacement пада.
  Это сигналы, не шум — не глушить.

## 4. Для перф-замеров (базовый протокол)

1. Тишина включена (этот документ), 20 кораблей, старт пачкой из одного города.
2. Прогон 5+ мин. Смотреть в Profiler (Server): суммарный `FixedUpdate`,
   `Physics.Raycast/SphereCast` (лидар 9+1 лучей × stagger 0.2 с;
   лучи PlanRoute/графа — раз на leg, не каждый тик).
3. Потребители по убыванию (ожидаемо): физика 20 Rigidbody → лучи лидара →
   остальное пренебрежимо (строковая интерполяция на местах вызовов NavLog
   осталась, но без файла это доли миллисекунд).
4. Если `FixedUpdate` NPC торчит — кандидаты на оптимизацию (по приоритету):
   stagger проб реже, кэш скана на несколько тиков, `wallObstacleMask` уже
   (проверить слои), Broadphase spatial hash уже есть (`NpcShipZoneRegistry`).

## 5. Проверка тишины (после любых правок логов)

* `read_console` (MCP): по фильтру `NpcShip|PeakRegistry|TrafficManager`
  за сессию — только разовые строки из §3, периодики ноль.
* Каталог `%USERPROFILE%\...\ProjectC_client\`: новых `NpcShipNavLog_*.csv` нет.
* `git status`: только относящиеся файлы (сцены без шума при сейве).
