# Разбор захвата `ProjectC_client_2026-11-22_10-39-14.data`

Дата: 2026-11-22. Тикет: `T-PERF03`. База: `T-PERF02` (`../25092026/`).
Сцена захвата: `BootstrapScene` (WorldScene_0_0 подгружен), Play Mode в редакторе.

## 0. Каркас захвата

Захват снят в Play Mode внутри редактора, поэтому в дереве кадров есть `EditorLoop`
(838–22 950 ms) — это оверхед редактора, в билде его нет. Read-only числа — только
`PlayerLoop`/`UpdateScene` и `GC`-метки внутри них.

Разбор: Unity MCP (`get_worst_cpu_frames` / `get_worst_gc_frames`) по захвату + прямой
замер сцены в редакторе (`FindObjectsByType`, `Physics.OverlapSphere` по фактическим зонам).

| Кадр | Main Thread | EditorLoop | PlayerLoop | Что в PlayerLoop |
|---|---|---|---|---|
| 333 | 23 256.71 | 22 950.08 | 279.65 | `UpdateScene` 279.65: `ScriptRunBehaviourUpdate` 93.78 + `UIElementsRepaintPanels` 22.08 |
| 0 | 6 981.76 | 241.34 | 6 224.20 | `PhysicsFixedUpdate` 5 937.94 (`PublishResultsStage` 5 899.09) |
| 75 | 1 010.24 | 29.89 | 951.22 | `ScriptRunBehaviourLateUpdate` 799.04 (`ShipDeckNav` 87.05 + 62.28) |
| 6 | 1 000.15 | 665.81 | 313.03 | `ScriptRunBehaviourUpdate` 68.66, `PhysicsFixedUpdate` 16.83 |
| 332 | 957.87 | 838.61 | 94.80 | `ScriptRunBehaviourUpdate` 16.50 (`NetworkPlayer` 11.87), `PhysicsFixedUpdate` 8.06 |
| 41/45/76 | — | — | — | геймплейные кадры: доминируют `Physics.SendTriggerEvents` + `NetworkPlayer.Update` |

Аллокации по кадрам (Main Thread, КБ): 0 → 42 787.64, 45 → 25 944.37, 76 → 23 876.83,
41 → 20 409.65.

### Масштаб сцены (замер в редакторе на момент разбора)

| Метрика | Значение |
|---|---|
| `Collider` (активных) | 21 842 |
| `DoorController` | 2 800 |
| `NpcController` | 8 |
| `OuterCommZone` | 9 |
| `MarketZone` | 12 |
| `ShipDeckNav` | 20 |
| `ShipCargoConsole` | 23 |
| `UIDocument` (activeInHierarchy) | 20 |
| Камеры/канвасы | 2 Canvas (1 активен) |

## 1. P0-1. `Physics.SendTriggerEvents` — 5.9 с в кадре 0 и 20–30 МБ мусора на кадр

Кадр 0: `PhysicsFixedUpdate` 5 937.94 → `Physics.PublishResultsStage` 5 899.09 →
`Physics.SendEvents` 5 899.00 → **`Physics.SendTriggerEvents` 5 898.80**.
Внутри этого узла из C#-кода — только `OuterCommZone.OnTriggerEnter` 4.98 + 4.91 ms.
То есть 5.89 с стоят не в наших обработчиках, а в самой публикации триггер-событий.

Аллокации (одинаковая картина на трёх геймплейных кадрах, где почти весь мусор кадра
приходится на этот узел, а вложенные вызовы дают ~1.7 КБ):

| Кадр | `Physics.SendTriggerEvents` | Main Thread total |
|---|---|---|
| 0 | 30 180.16 КБ | 42 787.64 КБ |
| 41 | 20 024.64 КБ | 20 409.65 КБ |
| 45 | 25 679.56 КБ | 25 944.37 КБ |
| 76 | 23 104.52 КБ | 23 876.83 КБ |

Единственная геометрия в проекте, дающая пары с тысячами коллайдеров, — коммуникационные
зоны: `OuterCommZone.Awake()` ставит `_sphere.radius = commRange`, и в сцене **7 зон имеют
trigger-сферу радиусом 500–1000 м** при 21 842 коллайдерах в мире:

| Зона | `commRange` | radius коллайдера | isTrigger | коллайдеров в радиусе |
|---|---|---|---|---|
| DockStation_Road to Tertius | 1000 | 1000 | true | 4 |
| DockStation_Road to Quartus | 1000 | 1000 | true | 3 |
| DockStation_Road to Secund | 1000 | 1000 | true | 4 |
| DockStation_Primium_farm_0_1 | 1000 | 1000 | true | 189 |
| DockStation_Primium_farm_0_2 | 500 | 500 | true | 338 |
| DockStation_Primium_farm_0_3 | 500 | 500 | true | 439 |
| DockStation_Primium_farm_0_4 | 500 | 500 | true | 489 |
| STN-PRM-001 | 422 | 1 | false | 5 304 |
| DockStation_Farm_0_0 | 1000 | 1 | false | 770 |

Километровый триггерный объём в городе — это сотни тысяч broadphase-пар на шаг физики
(кандидаты, а не только реальные попадания), и Unity 6 публикует их событиями.
Ограничение вывода: причинная связь «размер триггеров → цена `SendTriggerEvents`»
установлена корреляцией (другой такой геометрии в сцене нет), точный внутренний механизм
профайлер не показывает. Проверка — повторный захват после правки (см. `01_FIX_PLAN.md`).

## 2. P0-2. `NetworkPlayer.Update` — 11.87 ms и 44 КБ каждый кадр

Кадр 332: `NetworkPlayer.Update` 11.87 ms; его дети — два `FindObjectsOfType` по 0.13 ms.
Кадры 41/45/76: `NetworkPlayer.Update` 51.60 КБ, ровно две аллокации по **21.94 КБ**.

Источник найден в коде: `Update()` каждый кадр вызывает `UpdateInteractionHint()` →
`ResolveInteractionHint()`, а внутри:

- `HasNearbyDoorForInteraction()` — `FindObjectsByType<DoorController>(...)` + `door.GetComponent<Collider>()`
  на каждую дверь;
- `HasNearbyNpcForInteraction()` — `FindObjectsByType<NpcController>(...)`.

В сцене 2 800 `DoorController` → массив 2 800 × 8 Б = **22 400 Б ≈ 21.94 КБ**. Совпадение
с профайлером точное. Это 60 массивов в секунду ≈ 1.3 МБ/с мусора только на подсказку
взаимодействия, которая нужна раз в несколько сотен миллисекунд.

## 3. P0-3. Поллы зон: две одинаковые `OverlapSphere` на тик, 41 КБ на вызов

Кадр 45: `OuterCommZone.Update` 82.77 КБ — ровно две аллокации по **41.38 КБ**.
Кадр 333: `OuterCommZone.Update` 14.12 ms (`Physics.OverlapSphere` 0.98 + 0.98),
`MarketZone.Update` 5.89 ms.

Код: `OuterCommZone.PollPlayersInRange()` и `PollShipsInRange()` вызывают
`Physics.OverlapSphere(transform.position, commRange, pollLayerMask, Ignore)` с
**идентичными аргументами** — два запроса на зону за тик. У всех 9 зон в сцене
`pollLayerMask = -1` (весь мир) и `pollInterval = 0.25`.

Замер по фактическим зонам сцены (`OverlapSphere(~0)` с их `commRange`):
STN-PRM-001 (422 м) → **5 304 коллайдера = 41.44 КБ** за один вызов; сумма по всем 9 зонам —
7 540 попаданий, полный свип 11.2 ms в редакторе.

Серверный бюджет: 9 зон × 2 вызова × 4 Гц = 72 запроса/с; `MarketZone` (12 зон, `tradeRadius`
5–30 м) добавляет 96 запросов/с. Малые радиусы рынков дёшевы по CPU, но аллокация
`Collider[]` есть на каждом вызове — 96 массивов/с.

Сузить маску нельзя: в проекте нет слоя игрока/корабля (есть только тег `Ship`,
вся геометрия PRIMUM лежит на `Default`) — это ограничение зафиксировано ещё в `T-PERF02`
(Фаза 2 «дальше не уехать без маски»).

## 4. P1-4. `ShipDeckNav`: кап «одна регистрация на кадр» не работает

Кадр 75: **два** `ShipDeckNav.LateUpdate` в одном кадре — 87.05 и 62.28 ms; внутри
`NavMeshManager.NotifyNavMeshAdded` 85.73 → `LogStringToConsole` 34.68 →
`Application.CallLogCallback` с `GC.Collect` **22.09 ms**.

Причина в коде: `ProcessPendingRegistrations()` — `static`, и первым делом делает
`s_registrationsThisFrame = 0;`. Счётчик сбрасывается при каждом вызове, то есть на каждом
`ShipDeckNav.LateUpdate`, поэтому `MAX_REGISTRATIONS_PER_FRAME = 1` действует только внутри
одного объекта. При 20 `ShipDeckNav` в сцене регистрации палуб идут волной (в кадре 75 — две).

## 5. P1-5. `verboseLogging` столкновений включён в ассете

Кадр 76: `ShipController.OnCollisionEnter` 55.03 ms, 32.01 КБ — всё в
`LogStringToConsole`/`CallLogCallback` (плюс второй вызов 22.64 ms).

Код (`ShipController.cs:648-651`) пишет лог под `cfg.verboseLogging`, а сам конфиг —
`Assets/Resources/ShipCollisionDamage.asset`, строка `verboseLogging: 1`. В коде дефолт
`false` (T-SHIP-FIX12), но ассет перебивает дефолт.

## 6. P2. Наблюдения без правки в этом этапе

- **UIElements: 22.08 ms и ~1 МБ** — но только в кадрах 0 и 333 (`PCMM_PanelSettings`
  10.25 ms / 486–4 196 КБ, `EscMenuPanelSettings` 1.23 ms, `CharacterPanelSettings` 450 КБ;
  внутри `ATGTextJob.GenerateText` — реальная раскладка текста). В геймплейных кадрах
  41/45/76 узла `UIElementsRepaintPanels` в дереве нет. Вывод: цена меню/загрузки,
  а не игрового кадра. Правка не планируется; при необходимости — отдельный этап
  (выключать `UIDocument`/root у закрытых окон, но не через `UIDocument.enabled`:
  окна строят дерево кодом и потеряют его при re-enable).
- **`PrimumLightPool.Check` 2.16 ms** (кадр 332) при `checkInterval = 0.25`,
  `adaptiveMinInterval = 0.1` и 2 238 лампах — до 10 полных пересборок набора с двумя
  `List.Sort` в секунду. Значения сериализованы в сцене; менять — отдельным этапом
  с ручной проверкой ощущения (T-LIGHT18 настраивал 0.1 с осознанно).
- **`GetComponentNullErrorMessage` 0.84 КБ на событие** в
  `ShipCargoConsole.OnTriggerEnter/Exit` и `ShipController.OnTriggerEnter/Exit` — это ровно
  те места, где стоит `other.GetComponent<CharacterController>()`. Закрывает часть открытого
  вопроса из `25092026/ITERATIONS.md` («пункт (а): поиск источника»). Механизм (почему
  Unity строит текст ошибки при ненулевом `other`) не подтверждён, поэтому правки нет.
- **Разовые стартовые затраты** (не фрейм-тайм): `AdditionalLightIntensityController.Start`
  14.90 ms (`ALIC.ApplyIntensity` 8.06), `PrimumNpcCrowdDirector.SpawnRoutine` 12.48 ms
  (`Physics.RaycastNonAlloc` 5.57), `ShipPositionServer.RestoreCoroutine` 417.72 КБ.
- `GlobalMotionRuntimeEvidenceProbe.RecordEvent("player", "Update.begin", $"…")` в
  `NetworkPlayer.Update:964` строит интерполированную строку каждый кадр, хотя сам пробник
  (строки 49–53 его файла) предупреждает именно об этом. Входит в правку P0-2.

## 7. Ограничения замера

1. Захват — Play Mode в редакторе: `EditorLoop`, `CallLogCallback → GC.Collect` и
   `ExtractStackTrace` усилены редактором. В билде `SendTriggerEvents` останется, но
   стоимость логов упадёт.
2. Захват содержит 8 «худших» кадров, а не полную покадровую статистику; медианы/перцентили
   по кадрам не считались.
3. Профайлер не пишет метаданные `Physics.OverlapSphere` (metaCount = 0) — размер массива
   на вызов восстановлен замером сцены, а не из захвата.
4. Агент не запускал Play Mode: все runtime-эффекты правок проверяет пользователь.
