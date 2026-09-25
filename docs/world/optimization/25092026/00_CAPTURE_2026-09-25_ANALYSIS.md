# Разбор захвата `ProjectC_client_2026-09-25_18-09-59.data`

Дата: 2026-09-25. Тикет: `T-PERF02`. Захват снят после `T-NS-PERF01` (логи приглушены).

## 0. Каркас захвата

| Значение | Число |
|---|---|
| Кадров | 595 (0…594) |
| PlayerLoop | median 18.53 ms, p90 61.4, p99 138.3, max 166.6 |
| EditorLoop | сумма 19 262 ms (avg 32.4 ms/кадр); 135/595 кадров EditorLoop > PlayerLoop |
| RenderPlayModeViewCameras | 2 501 ms (4.20 ms/кадр) — рендер вьюпорта редактора |
| Profiler.FlushCounters | 2 473 ms (4.12 ms/кадр) — оверхед самого профайлера |
| Мусор | 59.0 MB на весь PlayerLoop = 101.5 KB/кадр; `GarbageCollector.CollectIncremental` в 595/595 кадрах |

Масштаб сцены (кадр 300): Static Colliders 8 939, Game Objects 40 092, Scene Objects 129 795,
Dynamic Bodies 19, Overlaps 340, GC Used ≈ 968 MB, Render Textures 65 / 272 MB,
GC Allocated In Frame 54 037 B.

Способ чтения: файл бинарный, разобран через Unity MCP —
`ProfilerDriver.LoadProfile` + `GetHierarchyFrameDataView` (main thread), агрегация
self-time по каждому 5-му кадру + поиск спайк-кадров с детьми.

## 1. Главная причина: `OuterCommZone.Update` + `MarketZone.Update`

Присутствуют во всех 595 кадрах, но дорого стоят только в ~105 из них — и в этих же
кадрах появляется `Physics.OverlapSphere` (совпадение 1:1). Скан зоны запускается
не каждый кадр, а примерно на 18% кадров (полл 0.25 с), и вся его цена оплачивается
одним кадром.

| Скрипт | Сумма | Кадров >10 ms | >30 ms | >50 ms |
|---|---|---|---|---|
| `OuterCommZone.Update` | 3 528 ms | 105 | 43 | 10 |
| `MarketZone.Update` | 1 786 ms | 98 | 7 | — |

Худшие кадры (OC | MZ | PlayerLoop):
f104 95/22/156 · f131 99/34/167 · f143 50/45/124 · f155 54/21/105 ·
f170 80/24/147 · f225 97/45/164 · f257 57/20/138 · f286 52/26/125 ·
f322 61/32/111 · f343 59/43/127 · f420 82/14/124.

На кадре 131 `ScriptRunBehaviourUpdate` = 138.16 ms из 166.57 ms PlayerLoop,
внутри него OuterCommZone 99.18 + MarketZone 33.76 = 132.9 ms, т.е. 80% всего кадра.

Цена сидит в одном экземпляре. На кадре 131 из 10 экземпляров OuterCommZone:

| Объект | self | GC |
|---|---|---|
| Ферма Примума 0_1 | 82.93 ms | 136.6 KB |
| DockStation_TestZone | 1.86 ms | 0.3 KB |
| Ферма Примума 0_4 | 0.31 ms | 0.3 KB |
| Средняя 0_0 | 0.24 ms | 1.4 KB |
| DockStation_Premium | 0.14 ms | 0.9 KB |

У MarketZone (12 экземпляров): `Npc_peacfull_market_zone` 25.97 ms / 69.0 KB,
второй `Npc_peacfull_market_zone` 1.15 ms, `MarketZone_Premium` 0.14 ms.
На f225 у MarketZone self 42.11 ms при OverlapSphere всего 2.47 ms —
цена почти целиком в C#-теле Update.

Физический запрос — не причина: на f131 OverlapSphere = 13.45 ms (20 вызовов)
из 99.18 ms. Стоимость запросов асимметрична: 3 из них 5.05 / 5.40 / 6.49 ms,
остальные 0.01–0.18 ms. Дальше идёт C#-обход результатов: GC 136.6 KB на ферме
против 0.7–0.9 KB на спокойных зонах. `FindObjectsOfType` вызывается из тех же
мест — 53 кадра у OuterCommZone, 54 у MarketZone. Итого два скрипта дают
14.49 + 7.52 = 22 MB мусора за захват.

Ограничение измерения: профайлер не пишет метаданные OverlapSphere (metaCount=0),
поэтому «стоимость ∝ числу найденных коллайдеров» — вывод из формы пика
(цена в теле цикла, а не в запросе), а не счётчик.

## 2. Вторая причина: `WindManager.FixedUpdate`

Сумма 1 380 ms self, отсутствует лишь в 3 кадрах; >2 ms в 227 кадрах,
>5 ms в 140 кадрах, на пиках 7.9–9.2 ms за кадр при 3 вызовах FixedUpdate
(≈2.6–3 ms на один fixed step), 0 GC.

Ключевое: FixedUpdate в просевших кадрах выполняется 3 раза, а на кадре 0 —
17 раз (там WindManager один даёт 14.24 ms). Просевший кадр → больше fixed steps →
дороже WindManager → кадр ещё тяжелее (положительная обратная связь).
Плюс `SpeedTreeWindManager.Update` / `UpdateGlobalAndLocalWinds` — в 595/595 кадров.

## 3. Третья причина: постоянный мелкий мусор и логирование (101.5 KB/кадр)

| Источник | GC за захват | Деталь |
|---|---|---|
| `GetComponentNullErrorMessage` | 12.03 MB (20% всего мусора) | 18 656 вызовов (~31/кадр), ~594 кадра, из 7 точек: `NetworkPlayer.Update/FixedUpdate`, `SkillAnimationPlayer.Update/LateUpdate`, `GlobalMotionWorld.Update/FixedUpdate`, `GlobalMotionPoseAdapter.LateUpdate`, `NetworkBehaviourUpdate` |
| `NpcShipWorld.FixedUpdate` | 6.34 MB (11 KB/кадр) | на f0 — 71.6 KB, из них 1 001 `GC.Alloc` внутри одного кадра |
| `GlobalMotionWorld.FixedUpdate` | 4.25 MB (7.3 KB/кадр) | на f0 — 44.8 KB только от `GetComponentNullErrorMessage` (68 вызовов) |
| `NetworkPlayer.Update` | 4.17 MB (7.2 KB/кадр) | |
| `UIElementsRepaintPanels` | 3.67 MB (6.3 KB/кадр) | но per-event: `CharacterPanel` 440 KB + `MarketPanel` 286 KB за перерисовку (f594) |
| `GlobalMotionPoseAdapter.LateUpdate` | 3.40 MB | |
| `LogStringToConsole` | 2.11 MB | 162 вызова × ~13 KB; родители: `NetworkPlayer.Update` (28 кадров), `GlobalSceneNativeExecutor.Update` (28), `StormCellDirector.Update` (27), `ParomRoute.Update` (13), `LocalDensityBuffer.Update` (13), `NpcBrain.Update`, `ShipPositionServer.Update`. На f0 логирование ≈ 97 KB за один кадр |

## 4. Мелкие постоянные циклы

`FindObjectsOfType` — 1 948 вызовов, из них 594 кадра из `Player.NetworkPlayer.Update`
(скан сцены почти каждый кадр) + `AdditionalLightIntensityController`; 141 ms.
`Physics.SphereCast` — 28 153 вызова (47/кадр) из `PickupDeckRide.LateUpdate` (595 кадров),
`NpcBrain.FixedUpdate`, `NetworkPlayer.Update`; 110 ms.
`Instantiate` — ровно 1 вызов в каждом из 595 кадров (подозрительно регулярно — найти кто).

## 5. Что причиной НЕ является

- Физика: `PhysicsFixedUpdate` 2.83 ms/кадр, `PxScene.simulate` 0.98 ms/кадр.
  8 939 статических коллайдеров симулируются за ~3 ms — не узкое место.
- NGO: `NetworkPreUpdate` 0.26 ms/кадр, `NetworkTickSystem.Tick` 1 KB/кадр — сам тик лёгкий;
  его мусор приходит из `GetComponentNullErrorMessage` внутри, а не из сети.
- UI Toolkit: 0.34 ms/кадр внутри PlayerLoop.
- Аниматоры: `Animators.Update` ~0.3 ms/кадр.
- Маршруты кораблей (`NpcShipWorld.FixedUpdate` без мусора): ~0.6 ms/кадр self —
  правка маршрутов невиновна.
- 4 «гигантских» кадра 594/11/346/593 (EditorLoop 8 244/1 122/1 021/747 ms против
  PlayerLoop 160/36/56/54) — редакторные хитчи, не игровая нагрузка.
