# 01 — AUDIT: факты проекта (измерено 2026-11-21)

Всё ниже — измеренные/прочитанные данные, а не предположения. Где факт взят из прошлой сессии,
указан источник (`_analysis/*` / memory) и он помечен как «ранее измерено».

## 1. Навмеш

| Что | Значение | Источник |
|---|---|---|
| Поверхность | `NavMeshSurface` **на `WorldRoot_0_0`** (`CollectObjects=Children`, `useGeometry=PhysicsColliders`, `layerMask` = только слой `PRIMUM` (8), `agentTypeID=0`, `overrideVoxelSize`, voxelSize 0.3, `minRegionArea=2`) | memory `primum-npc-navmesh-surface` (20.11) |
| Ассет | `Assets/_Project/Scenes/World/WorldScene_0_0/NavMesh-WorldRoot_0_0.asset`, ~1.5 МБ, 22 156 трейсов, 996 383 м² | там же |
| Агент-тип | единственный: id 0 `Humanoid`, r 0.5, h 2.0, slope 45°, climb 0.75 | `NavMeshAreas` проекта + memory |
| Двери в печи | `PRIMUM_DOORS` (слой 10) **исключены** из маски: с ними проходимость дверных проёмов падает 78 % → 32 % | memory (замер) |
| Шов запад/восток | две несвязные сети: запад x<39 782 (629 783 м²) и восток (249 243 м², `40_HUB_WAREHOUSE`); перепад 6.4 м, горизонтально 0.3 м | memory (замер) |
| Сцена | `NavMeshSurface` в YAML присутствует (`m_Script` 1 шт.), других FO/visibility компонентов нет | измерено: YAML-скан |

**Следствие:** город — потенциально две сети. NPC, заспавненные на западе, никогда не дойдут до востока.
Пилот работает на той сети, где стоят их дома; это надо считать нормой, а не багом.

## 2. Дома (якоря «≤4 NPC на дом»)

Измерено по YAML `WorldScene_0_0.unity`:

| Метрика | Значение |
|---|---|
| Компонентов `ProjectC.World.HouseMarker` | **534** |
| Уникальных `houseId` | **362** (маркеров больше, чем домов: у дома несколько частей) |
| Уникальных `chunkId` (район/кластер) | **21** (`PRIMUM_HOUSES_S3_TRD-ES_P1_37H`, `PRIMUM_HOUSES_S8_GW_P1_30H`, …) |
| `hasMapPoint: 1` | 534 (у всех маркеров есть точка) |
| Диапазон `mapPoint` | X 39 403…40 119, Z 39 801…40 314, Y 2 480…2 494 |

`HouseMarker` (`Assets/_Project/Scripts/World/HouseMarker.cs`) содержит `houseId`, `number`, `chunkId`,
`city`, `mapPoint` (+`hasMapPoint`) и `MapPosition => transform.position`.

**Ёмкость якорей:** 362 дома × 4 = **1 448 резидентов максимум**. Значит:
- T1=100, T2=300, T3=1000 — все NPC могут быть «резидентами»;
- T4=10000 — резидентов максимум 1 448, остальные ~8 550 обязаны быть уличной толпой **без дома**
  (иначе правило «≤4 на дом» нарушается). Это заложено в архитектуру (см. `02_ARCHITECTURE.md §4`).

`HouseMarker` — единственный готовый источник «домов»; `HouseDiscovery` использует их же
(`FindObjectsByType<HouseMarker>()`), т.е. формат уже рабочий.

## 3. Двери

| Что | Факт |
|---|---|
| `ProjectC.Ship.DoorController` в сцене города | **0** вхождений в YAML |
| `PM_DOOR_*` префабы в сцене | **0** вхождений |
| Реальные двери города | меши-части внутри префабов домов: `PRIMUM_HOUSES_..._M_DOORPARTS_M_PC_Wood_Red`, `..._M_PC_Bronze`, `..._M_PC_Steel_v2`, `..._M_PC_Cobble`, `PRIMUM_ADMIN_..._M_PC_Bronze` и т.п. (34 уникальных имени) |
| Отдельный корень | `PRIMUM_DOORS` существует как объект (1 объект), `Primum_farms_DOORS` — тоже |
| API `DoorController` | `Toggle()`, `Open()`, `Close()`, `SetOpenInstant(bool)`, `IsOpen`, `IsFullyOpen`, `OpenProgress` — **но в городе его нет ни на одной двери** |

**Следствие для T6:** «открыть дверь» в городе = либо навесить `DoorController` + `NavMeshObstacle(carve)`
на найденную дверную часть внутри инстанса дома, либо крутить саму дверную часть скриптом.
Никакого готового API сейчас нет — это отдельная работа, а не «включить флаг».

## 4. Модели NPC

Измерено через AssetDatabase:

| Метрика | Значение |
|---|---|
| FBX под `Assets/Generated_Models_Rigged_NPC` | **106** |
| Humanoid-аватар валиден (`avatar.isValid && isHuman`) | **106 / 106** |
| Костей в скелете | 50 (`B-hips`, `B-spine`, …; лицо не ригится) |
| Высота (localBounds скин-меша) | ≈1.909 м, центр по Y −0.23 |
| Ориентация | лицом в **+Z**, scale 1 (конвенция пайплайна ригинга) |
| Аниматор в FBX | есть, `avatar` валиден, `runtimeAnimatorController` пуст |
| Материал | уже `Universal Render Pipeline/Lit` (URP 17.5.0) |
| Текстура | `<Model>_BaseColor.png` рядом с каждым FBX (106/106) |
| Клипов внутри FBX | **0** (A-pose, `importAnimation: 0`) |
| Высота по `Renderer.bounds` (мир, identity) | **1.865 – 2.387 м** |
| Низ меша относительно пивота | **−0.313 … −0.075 м** (пивот внутри голеней, а не в ступнях) |
| Самая высокая точка | **2.074 м** |

Замер высот/пивота — меню `Tools/Project C/NPC/Primum Crowd/0) Audit readiness`. Следствие: спавн модели в точку
навмеша без подъёма утапливает её в мостовую на 7–31 см, а нейм-плейт на фиксированной высоте 2.1 м
оказывается в груди высоких моделей. Оба случая закрыты в `PrimumCrowdNpc`
(`AlignVisualToGround`, `PlaceNameplateAboveHead`).

Анимации берём ретаргетом с humanoid-паков:

- `Assets/Kevin Iglesias/Human Animations/Animations/Female/Idles/HumanF@Idle01.fbx`
- `.../Female/Movement/Walk/HumanF@Walk01_Forward.fbx`
- мужские аналоги `HumanM@Idle01`, `HumanM@Walk01_Forward`

Оба пака — `animationType: 3` (Humanoid), т.е. ретаргет на наши аватары штатный.

## 5. Идентичность и диалог (готовый пайплайн)

| Что | Факт |
|---|---|
| Компонент | `ProjectC.Quests.NpcController` (`Assets/_Project/Quests/NpcController.cs`): `RequireComponent(Collider)`, **принудительно** делает коллайдер триггером, ведёт статический `Registry`, `Find(npcId)`, `IsWithinDistance` |
| Данные | `NpcDefinition` SO: `npcId`, `displayName`, `factionRef`, `defaultDialogTree`, `greetingText`, `interactionRadius` |
| Ввод | `NetworkPlayer.TryInteractNearestNpc()` — `FindObjectsByType<NpcController>()`, дистанция, `QuestServer.RequestTalkToNpcRpc(npcId, null)` |
| Сервер | `QuestServer.RequestTalkToNpcRpc` → `questDatabase.GetNpc(npcId)` → `defaultDialogTree` → **fallback**: `BuildFallbackDialogTree` из `greetingText` (нода-greeting + «До свидания»), если дерева нет |
| Сборка базы | `QuestDatabaseAutoDiscover` (`[InitializeOnLoad]` + меню `Tools/Project C/Quests/Re-scan Quest Database`) рекурсивно сканирует `Assets/_Project/Quests/Data/Npcs` |
| UI | `DialogWindow` (существующий), рисует шаги с сервера |

**Следствие:** «своё имя + свой диалог» на пилоте достигается **без нового UI и без нового диалогового
движка**: генерируем по одному `NpcDefinition` на NPC (`displayName` + `greetingText`), кладём в
`Quests/Data/Npcs/PrimumCrowd/`, база их подхватывает. Никаких `DialogTree` ассетов на 100 NPC не нужно.

## 6. Floating Origin — как он реально устроен

| Слой | Факт |
|---|---|
| `ProjectC.World.Core.FloatingOrigin` | `public Transform worldRoot`, `threshold=100000`; в `LateUpdate()` при `|camera|>threshold` делает `worldRoot.position -= round(cameraPos)`. **События о сдвиге нет.** |
| Кто его создаёт | `WorldCamera.Awake` (BootstrapScene): `AddComponent<FloatingOrigin>()`, `worldRoot = FindWorldRoot()`, `threshold=100000` |
| `WorldCamera.FindWorldRoot()` | 1) ищет объект **по имени `Mountains`** (любой, с детьми!) → берёт его как корень; 2) иначе любой корневой объект с >5 детьми |
| Что в сценах | В `WorldScene_0_0` **нет** ни `FloatingOrigin`, ни `FloatingOriginMP`, ни `GlobalMotionControlledRebaseSlice`, ни `VisibilityDirector`, ни `WorldCamera` (YAML-скан) |
| Второй слой | `GlobalMotion*` (сотни файлов) — сетевой rebase-пилот: `RebuildParticipants()` = все корни `_worldScenePath`, **отвергает** объекты с `Rigidbody/Joint/CharacterController/NavMeshAgent` (см. `GlobalSceneNativeExecutor`, `GlobalMotionPoseAdapter.SupportedStructure`); в сцене не установлен |
| Контракт координат | `PrimumHlodManifest`: «Координаты — в пространстве `WorldRoot_0_0` (FO-инвариантно): ребаз переносит WorldRoot, а вместе с ним и всё, что под ним» |
| `VisibilityDirector` | сам детектит сдвиг `WorldRoot_0_0` по `position` и пересчитывает смещения (`DetectFrameShift`, `RecomputeOffsets`) — т.е. в проекте уже принят паттерн «следи за позицией корня, а не за событием» |

**Два вывода, обязательных для NPC:**

1. **NPC живут только под `WorldRoot_0_0`.** Тогда любой сдвиг корня (кем бы он ни был вызван) уносит их
   вместе с городом бесплатно; всё, что вне корня, при ребазе «отстаёт».
2. **События сдвига нет** → коллекция NPC сама следит за `worldRoot.position` в `LateUpdate` и на дельте
   делает `NavMeshAgent.ResetPath()` + `Warp(transform.position)` и пере-выдаёт цель из **локальных**
   координат корня. Это же спасает, если FO вообще не сработает: локальные координаты остаются валидными.

Отдельный риск, который надо знать: `FindWorldRoot()` предпочитает имя `Mountains`, а в BootstrapScene есть
`WorldRoot/Mountains`. Если FO возьмёт его, `WorldRoot_0_0` не сдвинется — для NPC это безопасно
(они едины с городом), но «каноничного FO» в пилоте может не быть. Это не наша зона правки.

## 7. Что уже есть в сцене и мешает/помогает

- 4 907 prefab-инстансов; сцена 64.3 МБ.
- `NPC` (1 объект) — контейнер для ручных NPC (`[Mira]`), пустой по факту.
- `PRIMUM` (слой 8) — геометрия города; `PRIMUM_DOORS` — корень дверей.
- `HouseDiscovery`, `VisibilityDirector` в сцене отсутствуют (появятся в рантайме с других сцен).
- `NavMeshAgent` в сцене: только `m_Script`-ссылка (компоненты лежат в префабах), т.е. конфликтов имён нет.

## 8. Что НЕ подтверждено и требует проверки на T1

1. Покрывает ли навмеш **интерьеры** домов (а не только двор/улицу) — проверяется по факту спавна
   (`NavMesh.SamplePosition` внутри дома) и визуально пользователем.
2. Точное соответствие `HouseMarker.mapPoint` двери дома (сейчас трактуем как «точка дома», спавн идёт
   NavMesh-сэмплом в радиусе).
3. Стоимость 100 skinned-мешей + 100 уникальных `URP/Lit` материалов в городе — меряется на T1.

## 9. Готовность сцены к толпе (измерено 2026-11-21, меню `0) Audit readiness`)

| Метрика | Значение |
|---|---|
| `HouseMarker` активных / всего | **534 / 534** (скрытых нет, `FindObjectsInactive.Exclude` безопасен) |
| Уникальных `houseId` | **362** → ёмкость 362 × 4 = **1 448** |
| Центры домов на навмеше (`houseSampleRadius=30`) | **362 / 362**, ВНЕ = **0** → `samplesFailed` на T1 ожидается 0 |
| `NavMeshSurface` в сцене | **21**, у всех заполнен `navMeshData` (один `WorldRoot_0_0`, остальные `DeckNavSurface`) |
| Триангуляция навмеша (edit-mode) | **22 156** трис — запросы `SamplePosition` работают без Play Mode |
| Корень коллекции | `WorldRoot_0_0/PRIMUM_NPC_CROWD`, local = (0,0,0), директор с назначенным конфигом |
| NPC толпы в `QuestDatabase` | **100 / 100** (`GetNpc` находит `primum_crowd_0001` и `0100`) |

Это закрывает пункт 8.1 в части **двор/улица**: навмеш есть рядом с каждым домом в радиусе 30 м.
Глубина интерьера (второй этаж, комнаты) по-прежнему не доказана — это задача T5.
