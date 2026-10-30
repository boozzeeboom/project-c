# D. Floating Origin, стриминг и масштаб мира: жёсткие ограничения на culling/visibility

**Дата:** 2026-10-30 · **Автор:** сабагент `fo-world` (тема D) · **Статус:** final · **Область:** ограничения FO, стриминга, масштаба мира 80×80 км, многокамерности, слоёв, «не сломать состояние» и системных требований на любую систему видимости. Только чтение кода/доков. Unity **6000.5.2f1**, URP **17.5.0**.

> `[VERIFIED]` — проверено кодом/файлом/расчётом; `[LIKELY]` — сильные косвенные данные; `[UNVERIFIED]` — нужно
> измерение. Числа без пометки — `[VERIFIED]` по указанному источнику.

---

## 0. TL;DR для Lead

1. Ребейз — не сдвиг origin-константы, а `Transform.position += T` корней `WorldScene_0_0` + `Physics.SyncTransforms()`
 + O(N)-обходы иерархии; данные видимости в мировых координатах после него врут на `T`
 (`GlobalMotionControlledRebaseSlice.cs:265–418, 1345, 326`).
2. Живой конфиг (`BootstrapScene.unity:17054–17084`): сцена-участник — `WorldScene_0_0` (**одна**), порог **256 м**,
 квант **256**, куб **±100 000 м**, авто вкл, опрос **5 с**, кулдаун **35 с**, F8/F9. Двигаются корни **одной** сцены
 (+игрок/удалённые/NPC вне корней): вторая мировая сцена не поедет → мир разорвётся.
3. Первый авто-ребейз после спавна: игрок у (39 460, 2 475, 40 000) ⇒ **|T| ≈ 56.4 км** (= 56 571 u `Respawn_Default`,
 `06M…:38`) — authored-кэш видимости умирает на 5-й секунде. Дальше **|T| ≈ 256–512 м** пешком (5/10 м/с) и
 **≈1.0–1.5 км** на корабле (max 40 м/с) при кулдауне 35 с: сдвиг сравним с габаритом города (1 466×1 059 м). `[расчёт]`
4. Видимость — **frame-local** от Transform либо в локальных координатах ячейки/корня; world-space bake запрещён.
 Публичного хука «ребейз завершён» нет: есть пустая фаза `TryRebuild` и статические `CumulativeRebaseOffset/FrameGeneration`.
5. Ребейз дорог (`SyncTransforms` по ~17 172 коллайдерам, 4–6 обходов ~19 754 узлов, `FindObjectsByType`); culling —
 O(видимых); мс — `[UNVERIFIED]`.
6. Стриминг формально есть, контента нет: `WorldScene_0_0` — 59.96 МБ, остальные 23 — по ~37 КБ / 12 GameObject.
 `WorldStreamingManager` (2/3, чанки 2 км) в сцене отсутствует (мёртвый прототип); пилот выводит `ClientSceneLoader`
 и грузит одну сцену (лишняя ⇒ `scene_count_mismatch`).
7. Наполненность: `WorldRoot_0_0` — **7 152 рендерера / 6.69 млн тр. / 19 754 узла**; город 1 207 (чанк 22–104), двери
 4 949 (2 363 × 2), фермы 142 (`ROADMAP.md:70–76`).
8. **Двери — главный объект culling**: 84 % draw calls (17 766 из 21 118) в shadow 250/1; без дверей 21 118 → 3 352.
9. Туман (0.0002–0.001 × fogScale) + `DistantFocus` (FarEnd 4 000 м) убивают детали дальше ~4 км; дальние города
 32–51 км при Medium невидимы.
10. Мир рендерит одна камера-риг, но тег `MainCamera` на двух камерах ⇒ `Camera.main` — мина (`DetailDistanceCuller.cs:87`;
 `PrimumLightPool` обходит вручную, `:159–219`). `AtmosphereCamera` — меню, far **10 м** (не 120 000).
11. Слои: заняты 6/8/9/31; **свободно 21 (10–30)** — на «слой = тип контента» хватает, на «слой = ячейка» нет.
 `QualitySettings.shadowDistance` в URP **не работает** (URP читает ассет: 1000 / 2 каскада) — рычаг `shadowCastingMode`.
12. Бюджет **16.67 мс** (60 FPS, GTX 1060 / RTX 3050), на culling **2 мс** (`Landscape_TechnicalDesign.md:809–822`).

---

## 1. Вопросы и метод

**Вопросы:** транзакция ребейза; запрет static/world-space bake; FO-safe culling и частота сдвигов; стриминг и смена сцен; масштаб и видимость на 1/5/20/80 км; камеры; слои; что нельзя ломать; системные требования; итоговая матрица.

**Метод (read-only):** чтение исходников FO/стриминга (перечень — §7), включая `GlobalMotionControlledRebaseSlice.cs`
(1 402 стр.), `ClientSceneLoader.cs` (1 028 стр.), `GlobalSceneNativeExecutor`, `OriginRebasePlan`, `LocalCoordinateFrame`;
YAML всех 24 сцен без загрузки в редактор; GUID-скан «что реально в графе сцены»; настройки `TagManager/QualitySettings/
GraphicsSettings` и `ProjectC_URP*.asset`; исходники URP 17.5 в `Library/PackageCache`; документы `docs/world/*` и
`docs/world/primum_city/*`.

---

## 2. Находки

### 2.1. Транзакция ребейза

Конфиг (`BootstrapScene.unity:17071–17084`, `[DefaultExecutionOrder(31000)]`): `_worldScenePath = WorldScene_0_0.unity`,
`_includeAllWorldSceneRoots = 1`, `_initialOrigin = (0,0,0)`, `_maxLocalCoordinate = 100000`, `_threshold = 256`,
`_quantum = 256`, F8 = 289, F9 = 290, авто = 1, опрос 5 с, кулдаун 35 с.
Формула (`OriginRebasePlan.TryCreate:31–62`): по оси `s = round(localFocus/256)*256`, `T = −s`; фокус остаётся в ±128 м;
отказ — только если все три оси в пороге (`:41–42`); вне куба — `TryCreateRecover` (`:73–106`).
Фазы (`ExecuteTransaction:265–418`): resolve фокуса (PlayerObject, не камера — `:1221–1238`) → participants
(`:1094–1193`: все **активные** корни сцены, неактивные исключаются) → prepare/capture → `position += T` (`:1345`) →
**`Physics.SyncTransforms()`** (`:326`) → `TryRebuild` (`:1349`, **no-op** — место для пересборки производных данных) →
validate → `NetworkTransform.Teleport` (`:561`) → камера/deathY/платформа (`:599–633`) → палубы (`:1082`) → `Clear()`
частиц/трейлов/линий (`:1047`) → carry/NPC/RespawnManager (`:1006`) → шторма/ветер/коридоры/veil/журнал/NPC-nav
(`:899–1004`) → `_frame = After`, `CumulativeRebaseOffset += T` → commit → broadcast `FO06_REBASE_SHIFT` (`:719`).
F9 — симметричный откат под флагами «что двигали» (`:459–552`). Чего не делает: не двигает другие сцены; не трогает
рендереры/материалы/свет/тени; публичного C#-события завершения **нет** (только `CurrentFrame`, `FrameGeneration`,
`IsTransactionActive`, `RegisteredParticipantCount`, `CumulativeRebaseOffset/Frame`, `ResetCumulativeRebase`).
Легаси FO (`World/Core/FloatingOrigin.cs`, `World/Streaming/FloatingOriginMP.cs`) в игре отсутствует (GUID-скан) — не подписываться на `FloatingOriginMP.OnWorldShifted`.

### 2.2. Почему static-флаги и baked world-space данные запрещены

Транзакция двигает трансформы, а «запечённое в мир» привязано к мировым координатам и после `+= T` остаётся на месте.
Ломается: **Static Batching** (static off, флаги снимаются — `PrimumLampLighting.cs:407,1081`, `DoorPrefabBuilder.cs:544,568`,
`PrimumCollidersBuilder.cs:133+`; `ROADMAP.md:37–39`: «именно так отвалились двери у заказчика»); **GI/лайтмапы/пробы**
(`lights/T-LIGHT12…:55–61`); **Umbra** (`m_OcclusionCullingData: {fileID: 0}`, 6 провальных бейков — `04_…:108–118`);
**любой свой bake в мир** (bounds, spatial hash по мировым ячейкам, списки дальности) — тихо: ошибок нет, «не те» объекты.

### 2.3. Что такое FO-safe culling

| Класс | Что это | FO | Комментарий |
|---|---|---|---|
| **A frame-local** | заново от Transform каждый чек | 🟢 | `DetailDistanceCuller:85–107`, `PrimumLightPool:89–157` («мировых Vector3 не хранит») |
| **B локальные координаты корня/ячейки** | `world = root.TransformPoint(local)` | 🟢 | для чанков/батчей: при `T` меняется только матрица корня |
| **C мировые + пересборка** | BVH/сетка с явной ре-инициализацией | 🟡 | хук **и** на success, **и** на rollback |

Хук: фаза `TryRebuild` (`:1349`, позиции применены, `SyncTransforms` сделан) либо маркеры
`GlobalMotionRuntimeEvidenceProbe` (`Requested/Applied/Rebuilt/Validated/Published/Completed`, `:306–410`), либо (без правок
FO) поллинг `CumulativeRebaseOffset`/`FrameGeneration`. Откат F9 обязан пересобирать так же — он меняет мир на `∓T`.

### 2.4. Частота и размер ребейза

Порог 256 м по любой оси, квант 256, опрос 5 с, кулдаун 35 с (`:163, :550`). Скорости: 5/10 м/с
(`PlayerController.cs:19–22`, `NetworkPlayer.cs:38–40`), корабль max 40 (`ShipController.cs:324`), NPC-корабли 12
(`NpcShipController.cs:90`); фокус — PlayerObject.

| Режим | Скорость | До порога | Интервал | Типичный \|T\| |
|---|---:|---:|---:|---:|
| шаг | 5 м/с | 51 с | ~55 с | 256 м |
| бег | 10 м/с | 25.6 с | **~35–40 с** | 256–512 м |
| корабль | 40 м/с | 6.4 с | **~35–40 с** | **1.0–1.5 км** |
| первый ребейз | — | сразу | ≤5 с после спавна | **≈56.4 км** |

«Корабельный» `T` (1.4 км) больше города по каждой оси ⇒ мировая сетка/хэш меняет и значения ключей, и соседство. Стоимость: `SyncTransforms` по ~17 172 коллайдерам + 4–6 обходов поддерева
`WorldRoot_0_0` (≈19 754 узла: `NetworkTransform`, `PickupDeckRide`+`NpcBrain`, `ParticleSystem`+`TrailRenderer`+
`LineRenderer`) + `FindObjectsByType<NpcShipController>`; мс — `[UNVERIFIED]` (E5). Culling — O(видимых), пересборка лениво.

### 2.5. Стриминг сцен: фактическое состояние

Сетка: `SceneRegistry.asset` → 6×4, путь `Assets/_Project/Scenes/World/{0}{1}_{2}.unity` (`SceneRegistry.cs:15–25`);
`SceneID.SCENE_SIZE = 79999`, `OVERLAP_SIZE = 1600` (`SceneID.cs:17–19`) ⇒ покрытие **479 994 × 319 996 м** при террейне
**80 000 × 80 000 м** (`terrain/README.md:10`) — сетка в 6× больше мира по площади.
Наполнение: `WorldScene_0_0` — 59.96 МБ (4 648 GameObject / 849 MeshRenderer в файле, остальное префаб-инстансы);
остальные 23 — **~37 КБ**, 12 GameObject (`WorldRoot_X_Z`, `SceneLabel`, `Boundaries`/`BoundaryViz`, `GroundPlane`) ⇒
стриминг ничего не выгружает по смыслу.
`ClientSceneLoader` (legacy): аддитивный `LoadSceneAsync` (`:956`), старт 3×3 (`:856`), прелоад/выгрузка по 10 000 м
(`:66, 68, 216–257`), `maxLoadedScenes = 4` с добиванием |dx|+|dz| > 2 (`:70, 272–298`) — противоречия (3×3 = до 9 сцен
против лимита 4; 10 км < половины сцены 40 км); `TryRetireForGlobalPilot` (`:92–111`) выводит его из игры.
Пилот: **одна** сцена (`GlobalMotionPilotRuntime.cs:62–81`), точное совпадение authored-набора (`scene_count_mismatch`,
`GlobalSceneNativeExecutor.cs:333–352`), fail-closed по scene-placed `NetworkObject` (`:207–219`), каталог
`GlobalMotionPilotSceneCatalog.asset` (~134 КБ, ≈152 записи) ⇒ «нарезать город на сцены ради culling» = каталог+digest'ы+
закрытый набор. `ScenePlacedObjectSpawner` спавнит scene-placed NO (`:105–111, 168–213`), но **выключается** в пилот-режиме
(`:116, :170`); чанк-стриминг (`WorldStreamingManager` 2/3, `ChunkSize = 2000`) **в сценах отсутствует**, и
`chunk_system/02_…` доказывает вред 2-км чанков (1 600 на сцену, 38 400 на сетку; 25 чанков = 10×10 км < 10 000 м).

### 2.6. Наполненность и доля пустоты

| Корень | Рендереров | Тр. | Заметка |
|---|---:|---:|---|
| `WorldRoot_0_0` (30.10) | **7 152** | 6 692 333 | 19 754 узла, 92 материала |
| `…/PRIMUM` | **1 207** | 5 438 062 | 761 батч + 446; чанк **22…104** (23 чанка) |
| `…/PRIMUM_DOORS` (+ADMIN 266) | **4 949** | 505 656 | 2 363 инстанса × 2; 2 363 `DoorController`; 4 432 `BoxCollider` |
| `…/Primum_farms` (+DOORS) | 142 (+270) | 1 269 389 | 94 — батчи двух ферм |
| `…/RuinsValleys` | 496 | 11 409 | **77.7 × 78.2 км**, тени off, без коллайдеров |
| `…/Secund`/`Tertius`/`Quartus` | по 25 | 7.6–8.8 k | (15 733, 61 731) / (4 428, 2 931) / (76 842, 70 471) |
| `…/Terrain_0_0` | **0** | — | Terrain-компонент (80×80 км, `pixelError 200`, `basemap 20 000`) |

Плюс свет и физика: `PRIMUM_LIGHTS` — 2 087 `Light` (≤96, радиус 140 м); `PRIMUM_COLLIDERS` — 17 172 бокса + 9 мешей.

Город **1 466 × 73 × 1 059 м** (`04_…:87–88`), агрегат рендереров `WorldRoot_0_0` — **78 866 × 5 735 × 70 395 м**
(`06M:27–28`). Интерьеры (1 116 `_INT_`) внутри общих батчей (этап отдельных префабов отменён, `ROADMAP.md:129`) ⇒
отдельно не куллятся. Пустота: сетка 153 600 км², террейн 6 400 км² (**4.2 %**), город 1.56 км² (**0.024 %** террейна,
**0.001 %** сетки) ⇒ выигрыш не в «выгрузить далёкое», а в «не рисовать мелкое вблизи» и «не грузить лишние сцены».

### 2.7. Что видно на 1 / 5 / 20 / 80 км

Три занавеса: (1) far из пресета — Near **30 000**, Medium **60 000**, Far **120 000** (`ViewDistanceApplier.cs:78–84`);
(2) экспоненциальный туман `fogDensity = phase.fogDensity × fogScale` (`DayNightController.cs:512–513, 621–633`), фазы
**0.0002–0.001**, `fogScale` 1.3/1.0/0.6 (`ViewDistanceConfig.cs:34–68`); (3) `DistantFocus` (`FarStart 800 / FarEnd 4000`,
`DistantFocusRenderFeature.cs:35–41`). Итог для Medium: **1 км** — 74 % контраста, резко (полная геометрия: батчи, двери,
корпуса ламп, интерьеры); **5 км** — 22 %, размыто (только силуэты); **20 км** — 0.25 % (крупные контуры); **80 км** —
~0, невидимо (дальний город только импостор при Far).
Ступени ячеек: **50 м** (дом/группа дверей), **250 м** (блок 100–300 м — `02_BLOCKS…:10`), **1 км** (чанк), **4 км**
(граница читаемости DoF), **12–20 км** (фог на Far), **80 км** (край террейна); «8 км» = «дальше только импостор».

### 2.8. Камеры

| Объект | Где | far | near | Роль |
|---|---|---:|---:|---|
| `BootstrapScene/MainCamera` (тег `MainCamera`, `AudioListener`) | authored, (239 997, 3 000, 159 998) | 1 000 → пресет | 0.3 | статичная legacy |
| `ThirdPersonCamera.prefab` (тег **тоже** `MainCamera`) | runtime (`NetworkPlayer.cameraPrefab`) | 1 000 → пресет (`SpringArmCamera.cs:474`) | 0.1 | геймплейный риг |
| `AtmosphereCamera` | runtime, `ProjectCMainMenuAtmosphere.Build()` | **10** | 0.01 | **меню**, маска только слой 9 |

`Camera.main` — не камера игрока: `DetailDistanceCuller.cs:87` это использует и, `[LIKELY]`, куллит руины относительно
камеры в 233 км (баг-репорт). Мир рендерит **одна** камера ⇒ «culling для каждой камеры» = привязка к активному ригу;
`ApplyAll` ставит far всем активным `CameraType.Game` (`:78–84`). Тени: main light, 2 каскада, 2048, split 0.25
(к1 0–250 м, к2 250–1000 м) — отсюда ×2 к draw calls дверей.

### 2.9. Слои: бюджет и альтернативы

`TagManager.asset:9–41`: 0 `Default`, 1 `TransparentFX`, 2 `Ignore Raycast`, 3 `—`, 4 `Water`, 5 `UI`, **6 `ShipDeck`**,
7 `—`, **8 `PRIMUM`**, **9 `PCMM_Atmosphere`**, **10–30 `—`**, **31 `ZoneCollider`**. Заняты контентом: 8 (город, двери,
фермы — проверено в `PRIMUM_CITY.prefab` и дверных префабах), 6 (1 префаб корабля), 9 (меню), 31 (только имя;
упоминаний в `Assets/**` нет). **Свободно 21 слот (10–30).** Схема «слой = тип контента» (8 город, 10 двери, 11 интерьеры,
12 фермы, 13 руины, 14 корабли, 15 импосторы, 16 VFX, 17 лампы, 18 террейн) влезает; «слой = ячейка» — нет (23 чанка).
Альтернативы: `Camera.layerCullDistances` (32 float'а, при нарезке слоёв; не влияет на shadow-кастеров; семантика «0»/URP — `[UNVERIFIED]`, E2); `CullingGroup` (CPU-видимость сфер: ячейки, дальние города, LOD-веера; FO-safe при frame-local сферах); `Renderer.forceRenderingOff` (рендер+тени off без `enabled`; `[LIKELY]`, E4); URP Rendering Layers выключены (`m_SupportsLightLayers: 0`) и про свет ⇒ не альтернатива.

### 2.10. Что culling не должен ломать

| Подсистема | Факт | Запрещено |
|---|---|---|
| Физика | коллайдеры — отдельные объекты `PRIMUM_COLLIDERS` (17 172 + 9) | `SetActive(false)`, отключение `Collider` |
| Двери | 2 363 `DoorController`; в префабе 1 скрипт, 2 `MeshRenderer`, 2 `BoxCollider`, нет NO/AudioSource; анимация локальная (`DoorController.cs:9–29`) | трогать иерархию/позу, коллайдер, деактивировать GO |
| Сеть | scene-placed NO (22 корня кораблей, маркеры, зоны, паром); NT телепортируется транзакцией | `SetActive(false)` на NO/NT, перепарентинг |
| Аудио/VFX | `AudioListener` на `MainCamera`; частицы/трейлы `Clear()` на ребейзе (`:1047`) | деактивировать GO со звуком; гасить симуляцию |
| Свет | `PrimumLightPool` крутит **`Light.enabled`** (2 087, ≤96, 140 м) | путать бюджет света с рендером |
| Другие culling-и | `DetailDistanceCuller` пишет `Renderer.enabled` (`:104`), `MeziyThrusterVisual` гасит свой (`:124, 210`) | молча перетирать `enabled` (нужен арбитраж) |

**«renderer off ≠ объект исчез»**: состояние (физика, сеть, двери, свет, аудио, симуляция VFX, culling-логика) остаётся
полным; отключается только отрисовка и, отдельно, тени (`shadowCastingMode`), а не компонент/GO. `OnBecameVisible/Invisible`
в `Assets/_Project/Scripts` не используются (у Aura-модов — `[UNVERIFIED]`), но при внедрении `enabled`/`forceRenderingOff`
это надо перепроверить (E4).

### 2.11. Системные требования (известно / неизвестно)

Известно: **60 FPS на среднем ПК (GTX 1060 / RTX 3050)**, кадр **16.67 мс**, culling **2 мс**, rendering 8 мс
(`Landscape_TechnicalDesign.md:809–822`); пресеты: far 30k/60k/120k, lodBias 0.7/1.0/1.3, pixelError 300/200/50,
basemap 12k/20k/80k, shadowDistance 500/1500/4000, detailCull 4k/8k/12k, fogScale 1.3/1.0/0.6; URP **Forward+**
(`ProjectC_URP_Renderer.asset:189` `m_RenderingMode: 2`, enum — `UniversalRenderer.cs:11–22`), SRP Batcher вкл, GPU Resident
Drawer выкл, HDR off, MSAA 1, main shadows 2048/2/1000; замеры: 26 981–26 996 draw calls (двери вкл), 4 954 (off),
21 118 → 3 352 (shadow 250/1, двери 84 %), FPS 27.8 vs 34.1 (`04_…:50–58`). `QualitySettings`: «Medium», у всех уровней
`customRenderPipeline: {fileID: 0}` ⇒ один URP-ассет; `QualitySettings.shadowDistance` URP игнорирует.
Неизвестно: железо/разрешение/целевой FPS заказчика; длительность ребейза в мс; стоимость CPU-culling; `layerCullDistances` в URP (`[UNVERIFIED]`).

---

## 3. Матрица: контент × механизм × дистанции × гранулярность × FO-оговорки

**F**rustum, **D**istance, **S**hadow-only, **O**cclusion (Umbra недоступен), **L**OD/HLOD/импостор, **ST**reaming,
**C**ullingGroup.

| Класс | Кол-во | Механизм | Дистанции | Гранулярность | FO-оговорки |
|---|---|---|---|---|---|
| Батчи домов | 1 207 р. | F + D + L(HLOD) + L2 | 0–3 км полная; 3–12 HLOD; >12 импостор; >20 туман | **чанк 22–104 р.** / блок 100–300 м | bounds локально (B); никакого static/LODGroup-бейка |
| Двери | 4 949 р. / 2 363 инст. | **S + D** | 0–40 м тени+цвет; 40–80 цвет; >80–300 off | **инстанс** (2 рендерера) | коллайдер и `DoorController` не трогать (A) |
| Интерьеры (1 116) | в батчах | **неотделимы**; только L/HLOD | 0–1 км | батч/чанк | отдельные объекты = только runtime-LOD |
| Лампы/emissive | 2 087 | свет — пул (готово); корпуса — D с батчем | свет 140 м | пул / чанк | `Light.enabled` ≠ `Renderer.enabled` |
| Фермы (+двери/лампы) | 142 (+270) | F + D + L | 0–3 км; 3–12 HLOD | ферма | как город |
| Руины | 496 инст. | **D уже есть** + F | 4/8/12 км по пресету | **хамлет-группа (~60)** | класс A; **исправить `Camera.main`** |
| Корабли | десятки–сотни | F + **S** + L(модульный) | тени 40–150 м | модуль/корень | участник ребейза; без деактивации/перепарентинга |
| Террейн | 1 объект, 0 р. | **никогда**; `pixelError/basemap/far/fog` | всегда | объект | едет с корнем |
| Дальние города | по 25 р. | **L2-импостор** + C | 12–80 км (реально Far) | город (1–2 квада) | camera-facing, **только скаляры** |
| VFX/шторма/veil/небо | ~300 | не куллить рендером; LOD облаков отдельно | — | система/фича | сдвигаются транзакцией |
| Триггеры/зоны, коллайдеры | 17 181 | **никогда** | — | — | выключение = поломка геймплея/физики |

---

## 4. Что это значит для Project C

1. **PRIMUM:** не «запечь окклюзию», а (а) двери (shadow+budget), (б) кольца дальности на **чанк-гранулярности**
 (22–104 рендерера), (в) L1-HLOD 3–12 км и L2-импостор 12–80 км; без static-флагов (классы A/B).
2. **Двери:** `shadowCastingMode = Off` дальше ~40–80 м + рендер off дальше ~150–300 м (или `forceRenderingOff`), **не
 трогая** 4 432 коллайдера и `DoorController` — самый большой и локальный выигрыш.
3. **Корабли:** куллить только рендеры модулей; запрещены деактивация/перепарентинг/выгрузка; палубы и навмеш —
 отдельный контракт (`ShipDeckNav`, кулдаун 30 с, сброс на ребейзе).
4. **FO:** не хранить authored-констант; пересборка на success **и** rollback; «доли мс»; не использовать `Camera.main`;
 уметь `T = 56 км` и `T ≈ 1.4 км`; фокус — PlayerObject (в т.ч. на палубе).
5. **Стриминг/слои:** выгружать нечего; «нарезать мир на сцены» упирается в FO, `scene_count_mismatch`/каталог и куб
 ±100 км против сетки 480×320 км; 21 свободный слой — на типы контента, ячейки — только `CullingGroup`/dist-группы.
6. **Тени/бюджет:** `shadowDistance` — no-op (рычаги: `shadowCastingMode`, URP-ассет); 2 мс ⇒ структура нативная либо очень дешёвая (обход 7 152 рендереров раз в N кадров, гистерезис, кэш групп).

---

## 5. Рекомендованные эксперименты

Play Mode у заказчика; сравнивать на равном `frameCount` (`04_…:39–41`).

| # | Что | Как | Ожидаемо | Kill criteria |
|---|---|---|---|---|
| E1 | базовая линия + чанк-гранулярность | Profiler `Camera.Render`/`Culling`/CPU, 3 точки × 3 пресета; off по чанкам города на 0.5/1/1.5 км | culling ≤2 мс, кадр ≤16.67; линейное падение draw calls | >3 мс или pop-in ⇒ off/2 порога |
| E2 | `layerCullDistances` в URP | слой с X и слой с `0`, объекты на 2X; влияние на тени | «0» = без ограничения `[UNVERIFIED]` | если 0 = «не рисовать» — только явные far |
| E3 | дверной shadow-only | `shadowCastingMode = Off` дальше 50/100/200 м | −60…80 % draw calls | тени деградировали ⇒ поднять порог |
| E4 | `enabled` vs `forceRenderingOff` | руины/двери, проверить `OnBecameVisible`/скрипты/VFX | нет конфликта | конфликт ⇒ реестр владения |
| E5 | стоимость ребейза | Stopwatch вокруг F8 + Profiler, 5 повторов | хитч единицы–десятки мс | >50 мс ⇒ пересборка вне транзакции |
| E6 | кто такая `Camera.main` | лог `Camera.main`/позиция/`allCameras.Length` | статичная в 233 км `[LIKELY]` | если так — починить `DetailDistanceCuller` |
| E7 | толерантность L2 | импостор города/дальних на 4/12/20 км, сравнить кадры | разницы не видно `[LIKELY]` | видно ⇒ поднять детализацию |

FO-регресс: F8 → объект на месте, F9 → возврат, пролёт на корабле 2–3 минуты → видимость не «съезжает».

---

## 6. Риски и открытые вопросы

1. **Одна сцена как контракт:** сценовые схемы culling/стриминга требуют переработки транзакции и каталога пилота.
2. **`T` первого ребейза ≈56 км:** «посчитавший один раз» мировые позиции на старте врёт через 5 секунд — тихо.
3. **Нет публичного события ребейза:** нужна правка FO (участник/фаза) или поллинг `CumulativeRebaseOffset`; легко
 забыть rollback. **Ребейз-хитч не измерен** (E5) — от него зависит допустимость пересборки BVH в транзакции.
4. **`Camera.main`-мина** уже сработала в `DetailDistanceCuller`.
5. **Umbra закрыт**, но физическая окклюзия огромна (из 2 881 двери в фрустуме «видимо» 10, `04_…:97`) ⇒ потенциал
 программной окклюзии/HLOD большой, но нужна своя геометрия. «84 % дверей» — замер из одной точки (E3).
6. **Слои:** «тип контента» съест ~10 из 21; регионы потребуют CullingGroup.
7. **`QualitySettings.shadowDistance` — no-op в URP; `layerCullDistances` семантика «0» — `[UNVERIFIED]` (E2).**
8. **Документация устарела:** `AtmosphereCamera far 120 000` (код: 10 м), `far 1 000 000`, «64 000 трансформов», «живой» `WorldStreamingManager` — числа из §2.

---

## 7. Источники

**Код** (под `Assets/_Project/Scripts/`, кроме отмеченного): `World/FloatingOrigin/Network/GlobalMotionControlledRebaseSlice.cs`
(1–1402); `World/FloatingOrigin/{OriginRebasePlan,LocalCoordinateFrame}.cs`; `World/FloatingOrigin/Network/GlobalSceneNativeExecutor.cs`
(89–352); `World/FloatingOrigin/Pilot/GlobalMotionPilotRuntime.cs` (45–130); `World/Scene/{ClientSceneLoader,SceneID,SceneRegistry,
ScenePlacedObjectSpawner}.cs`; `World/Streaming/{WorldStreamingManager,FloatingOriginMP}.cs`; `World/Core/FloatingOrigin.cs`;
`World/{DetailDistanceCuller,PrimumLightPool,ViewDistanceApplier,ViewDistanceConfig}.cs`; `Core/{SpringArmCamera,WorldCamera,
DayNight/DayNightController}.cs`; `Rendering/DistantFocusRenderFeature.cs`; `Ship/DoorController.cs`;
`Player/{PlayerController,NetworkPlayer,ShipController}.cs`; `Assets/Aura/Mods/ProjectCMainMenu/Scripts/ProjectCMainMenuAtmosphere.cs`;
`Assets/_Project/Settings/{ProjectC_URP,ProjectC_URP_Renderer}.asset`; `ProjectSettings/{TagManager,QualitySettings,
GraphicsSettings}.asset`; `Scenes/BootstrapScene.unity` (17054–17084), `Scenes/World/WorldScene_*.unity`;
`Prefabs/City/{PRIMUM_CITY.prefab,Doors/PM_DOOR_SINGLE_PANEL_B.prefab}`, `Prefabs/ThirdPersonCamera.prefab`,
`Prefabs/FloatingOrigin/GlobalMotionPilotSceneCatalog.asset`; `Library/PackageCache/…universal@748d7458cc62/Runtime/UniversalRenderer.cs:11–22`.

**Документы:** `docs/world/floatingorigin/{README,06M_REBASE_BOUNDARY_CENSUS,09S_NEW_CONTENT_PIPELINE}.md`; `docs/world/chunk_system/{01_CURRENT_STATE,02_DO_WE_NEED_CLIENT_CHUNK_LOADER}.md`; `docs/world/optimization/LOD_VIEW_DISTANCE_DESIGN.md`, `docs/world/terrain/README.md`; `docs/world/Landscape_TechnicalDesign.md` (§5.1, 809–822); `docs/world/distantfocus/README.md`; `docs/world/lights/T-LIGHT12-forwardplus-pilot.md`; `docs/world/primum_city/{ROADMAP,README}.md`, `06_REFERENCE/04_OCCLUSION_CULLING_RESEARCH.md`.\n\n**Веб:** недоступен (нет ключа поиска) ⇒ `layerCullDistances` — `[UNVERIFIED]` (E2).

---

## 8. Приложение: сырые данные

**GUID-скан сцен** (`*.cs.meta` → `Select-String -SimpleMatch` по сцене): `BootstrapScene` — `ScenePlacedObjectSpawner` 1,
`ClientSceneLoader` 1, `GlobalMotionPilotRuntime` 1, `GlobalMotionControlledRebaseSlice` 1; `WorldScene_0_0` — `PrimumLightPool` 1.
Ноль в обеих: `WorldStreamingManager`, `WorldChunkManager`, `ChunkLoader`, `FloatingOriginMP`, legacy `FloatingOrigin`,
`WorldCamera`, `SpringArmCamera`, `DetailDistanceCuller`, `DoorController` (последние три — в префабах; скан видит только
authored-в-сцене). Четыре prefab-источника `BootstrapScene`: `ProjectCMainMenuUI`, `ProjectCMainMenu`, `CloudyNavigatorUI`,
`NetworkPlayer` — компонентов стриминга/чанков нет.
**Заглушка `WorldScene_1_1.unity`** (37 412 байт): 12 GameObject, 1 корневой трансформ — `WorldRoot_1_1`, `SceneLabel_1_1`,
`Boundaries_1_1` (+`East/North/South/West`/`BoundaryViz`), `GroundPlane_1_1`.
**Дверной префаб `PM_DOOR_SINGLE_PANEL_B.prefab`** (10 347 байт): 2 `MeshRenderer` (`Leaf`, `Frame`), `castShadows = 1`,
`receiveShadows = 1`, 2 `BoxCollider`, 2 материала, 1 скрипт — `DoorController`; `NetworkObject`/`AudioSource` нет; слой 8.

---

## Handoff

**Главный вывод:** culling строится как **frame-local/локальные данные + дешёвые дистанционные группы**, а не как
запечённая в мир структура: FO двигает корни одним `position += T` (первый авто-ребейз ≈56 км, дальше 0.26–1.5 км каждые
~35 с), static-флаги и world-space bake запрещены, выгружать из стриминга нечего (23 из 24 сцен пустые).

**Три главных факта:** (1) транзакция двигает только корни `WorldScene_0_0` и не имеет публичного события завершения —
пересборка видимости требует хука (`TryRebuild`) или поллинга `CumulativeRebaseOffset`, и на rollback тоже; (2) двери —
4 949 рендереров, 84 % draw calls в теневом конфиге, 2 363 `DoorController`/4 432 коллайдера, которые нельзя отключать
(только рендер/тени); (3) мир рендерит одна камера-риг, но `Camera.main` — мина (две камеры с тегом `MainCamera`), а
`AtmosphereCamera` — меню с far 10 м.

**Непроверено:** длительность ребейза (E5), `layerCullDistances` (E2), стоимость CPU-culling (E1), `forceRenderingOff` (E4), железо/FPS заказчика.
