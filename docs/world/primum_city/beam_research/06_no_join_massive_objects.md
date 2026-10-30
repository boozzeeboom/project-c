# 06. «А что если без склейки?» — массовые независимые объекты, физика, разрушаемость, корабли

> Дата: 2026-10-30. Исследование по запросу пользователя: "а что если без склейки? есть решения? это не фантастика, встречались игры с 100500 объектами лоуполи на которых на каждом был коллайдер и физика — наша задумка изначально была такой. разрушаемость и т.п. могли проработать поверх."

> Режим: research-only, код не менялся. Два сабагента: explore (кодовая база) + general (интернет-ресерч).

## 0. TL;DR

1. **Разрушаемости окружения нет.** Урон/HP существуют только для игроков, NPC и кораблей (`ShipHull` как `IDamageTarget`). Город, пропсы, двери — статика, коллайдеры не разрушаемые. Voxel/fracture/breakable-систем в проекте нет.
2. **Физика города — боксы по классам** (стены/плиты/крыши/марши) из **несклеенной** геометрии. `MeshCollider` на чанк не используется (5.8 млн тр. на кук). `Rigidbody` у города отсутствует (статика). Физика едет с миром (FO).
3. **Корабли — compound-коллайдеры из боксов** (режим `shipMode`), `Rigidbody` + `NetworkObject` + `ShipController`. Склейка `CombineMeshesToCollider` применялась к импортированным схемам (порт), но не к кораблям как стандарт.
4. **Двери — `DoorController` (MonoBehaviour, локальная анимация), BoxCollider на полотне; не разрушаемы.** 2 161+133 инстансов префабов.
5. **DOTS/ECS отсутствует** (нет `com.unity.entities` в `Packages/manifest.json`, нет `using Unity.Entities` в коде).
6. **GPU Resident Drawer выключен** (откат 18.10), BRG Variants = Strip, Static Batching off, Dynamic Batching off. SRP Batcher вкл.
7. **Стриминг — сцены (ClientSceneLoader, 6×4, SCENE_SIZE 79999), чанки legacy не используются.** Floating Origin сдвигает корни сцен целиком.
8. **Бюджет draw calls L0 — ≤ 120; факт 26 981–26 996.** До склейки — 147 715 объектов / 4.49 млн тр.
9. **100k+ объектов с физикой** — домен DOTS/ECS + Unity Physics; PhysX имеет жёсткий лимит ~65k коллайдеров на сцену и не потянет 100k живых rigidbody.
10. **Рендер 100k статических** возможен через GPU Resident Drawer (Forward+, DOTS_INSTANCING_ON шейдеры), но только для однотипных меш+материал.
11. **Разрушаемость:** pre-fractured (Fortnite-стиль) реалистична без DOTS; воксель (Teardown) — большой отдельный проект; runtime boolean (OpenFracture) — дорого.
12. **MMO:** NGO 2.13 даёт только примитивный visibility callback — нужна своя interest-management система поверх spatial hash.
13. **Без DOTS** реалистичный потолок: сотни–тысячи активных объектов с физикой, десятки тысяч статических визуальных через GPU RD.
14. **Вывод:** «без склейки» жизнеспособно для визуальных пропсов и дверей (через GPU-driven + culling), но не для массовой физики. Разрушаемость окружения — отдельный большой проект (pre-fractured или voxel).

---

## 1. Что есть в проекте: разрушаемость

### 1.1 Урон и HP
- `Assets/_Project/Scripts/Combat/Core/IDamageTarget.cs` — интерфейс урона. Реализации: `PlayerTarget`, `NpcTarget`, `ShipHull`.
- `Assets/_Project/Scripts/Ship/Combat/ShipHull.cs` — HP корабля, столкновения через `ShipController.OnCollisionEnter → ApplyCollisionDamage(energy)`. Конфиг `ShipDamageConfig` (SO, `Resources/Ship_hull/`).
- `Assets/_Project/Trade/Scripts/Core/TradeWorld.cs` — `TryDamageCargo` (cargo leaked/damaged при столкновениях).
- `Assets/_Project/Scripts/Combat/Network/CombatServer.cs` — серверный raycast/AOE, `GetComponentInParent<IDamageTarget>()`.

### 1.2 Чего нет
- Нет voxel-систем, fracture, breakable-компонентов, разрушаемых пропсов/стен/дверей.
- Городские коллайдеры (боксы домов, BASE, ADMIN) — **статические**, не `IDamageTarget`, не имеют HP.
- Двери (`DoorController`) — только анимация открытия/закрытия; замки через `MetaRequirement` (заперто/открыто), не разрушение.
- В `docs/world/primum_city/ROADMAP.md` и `04_COLLIDERS/*` разрушаемость окружения не упоминается как план.

**Вывод:** «разрушаемость без склейки» в проекте не планируется. Урон касается только сущностей с `IDamageTarget`. Для реализации разрушаемых стен/пропсов понадобится новая подсистема (отсутствует prior art).

---

## 2. Физика: PhysX, коллайдеры, CharacterController

### 2.1 Коллайдеры города (боксы по классам)
Источник: `docs/world/primum_city/04_COLLIDERS/01_COLLIDERS.md`, `05_HOUSES_BOXES.md`, `04_STATUS_AND_GAPS.md`.

- Сгенерированы **из исходной (несклеенной) геометрии** — склеенные меши непригодны (один меш = квартал, бокс по нему бессмыслен). Скрипт: `_work/colliders_csv.py` / `tools/houses_boxes.py`.
- Классы: `00_TERRAIN`, `05_FLOOR`, `10_WALLS`, `20_ENTRANCE`, `30_PLAZA`, `50_ROOF`, `ADM_SHELL`, `STAIR`, `STEPS`, `SLAB`, `DECK`, `RISER`, `50_CIRCULATION`, `20_PLATFORMS`, `40_ADMIN`. Исключены интерьеры (`40_INTERIOR`), окна, декор, двери (свои коллайдеры).
- **Финальное состояние (22.10):**
  - Дома: **10 355 боксов** (стены 7 715 / плиты 1 652 / крыши 760 / марши 228) в 25 чанках.
  - BASE: **74** (44 DECK + 24 STAIR + 4 LANDING + 2 SURFACE-меша).
  - ADMIN: **382** (WALL 298 / FLOOR 47 / ROOF 9 / STAIR 28).
  - **Итого PRIMUM_COLLIDERS: 10 817 = 10 809 боксов + 8 мешей** (6 `MeshCollider STAIRS` удалены 30.10, остались 2 `SURFACE`).
  - `WorldRoot_0_0` после Э8: **17 181 коллайдер = 17 172 бокса + 9 мешей**.
- Применение: префабы `Assets/_Project/Prefabs/City/Colliders/PRIMUM_COLLIDERS_<чанк>.prefab`, инстансы под `WorldRoot_0_0/PRIMUM_COLLIDERS` (чтобы не раздувать сцену). Трансформы identity, слой `PRIMUM` (8), static-флаги **не ставим** (FO).
- `BoxCollider` на дверях — 2 114 (в старой схеме); в финальной — двери имеют свои боксы.
- Точные `MeshCollider` (non-convex) используются точечно: BASE SURFACE (гранит/брусчатка, 9 172/8 024 тр.), ADMIN-лестницы, внутренние лестницы (7 `PM_STAIRS_*`, 2 640–11 440 тр.).
- `CombineMeshesToCollider.cs` — склейка дочерних `MeshFilter` в один `MeshCollider (convex=false)` на корне; используется для импортированных схем («gorod port»), не для города целиком. `CharacterController` требует одного невыпуклого mesh-коллайдера.
- `BuildSettlementColliders.cs` — универсальный builder боксов для поселений/ферм/блок-аутов; режим `shipMode` (без static, иерархия едет с Rigidbody как compound-коллайдер).
- `StairCollidersTool.cs`, `CollectCollidersTool.cs`, `GenerateSimpleCityColliders.cs`, `PrimumHouseColliders.cs`, `PrimumBaseDeckColliders.cs`, `PrimumMeshRebase.cs` — инструменты генерации.

### 2.2 CharacterController / Player
- `PlayerController` требует одного невыпуклого `MeshCollider` (см. `CombineMeshesToCollider.cs:16`). В городе игрок ходит по боксам (стены/полы/крыши).
- `Physics.SyncTransforms()` вызывается после сборки.

### 2.3 Массовая физика: есть ли планы на per-object?
- Нет. Физика города — статика (боксы, `Rigidbody` нет). Перенос города → боксы едут (локальные center/size). Планов на Rigidbody-пропсы/разрушение нет.
- `BuildSettlementColliders` поддерживает `shipMode` для движущихся объектов (compound-коллайдер из боксов), но это для кораблей/ферм, не для массовых пропсов.

### 2.4 Unity Physics (DOTS) vs PhysX — что говорит интернет
- **PhysX (встроенный в Unity):** жёсткий лимит ~65k коллайдеров на сцену (16-bit index buffer в broadphase). 100k+ живых rigidbody — не потянет (CPU-bound, O(n²) в худшем случае).
- **Unity Physics (DOTS):** поддерживает 100k+ коллайдеров/rigidbody (ECS, Jobs, Burst). Но требует полного перехода на DOTS (SubScenes, EntityCommandBuffer, IJobEntity).
- **Без DOTS:** реалистичный потолок — сотни–тысячи активных rigidbody, десятки тысяч статических коллайдеров (через GPU RD для визуала, но коллайдеры всё равно CPU).
- **Вывод:** массовая физика (100k объектов с коллайдерами) без DOTS невозможна. С DOTS — возможна, но требует миграции (мы на NGO GameObjects).

---

## 3. Корабли: CombineMeshesToCollider, ShipController, Rigidbody, NetworkObject

### 3.1 Архитектура
- `Assets/_Project/Scripts/Player/ShipController.cs` — `[RequireComponent(typeof(Rigidbody))]`, `[RequireComponent(typeof(NetworkObject))]`, `NetworkBehaviour`, `IGlobalMotionActorParticipant`. Scene-placed, спавнится через `ScenePlacedObjectSpawner` (иначе [Rpc] падают с NRE).
- Классы полёта: `ShipFlightClass` (Light/Medium/Heavy/HeavyII). Грузовой класс — `Trade.Core.ShipClass` (маппинг через `ShipClassMappingConfig`).
- `Rigidbody`: `interpolation = Interpolate`, constraints настраиваемые (`shipConstraints`), torque через `ApplyTorque`.
- `ShipHull` (HP) — `IDamageTarget`, server-authoritative.
- `NpcShipController` — scene-placed `NetworkBehaviour` на корне NPC-корабля, паттерн `ShipController`.

### 3.2 Коллайдеры кораблей
- `BuildSettlementColliders` в режиме `shipMode`: холдеры/группы/корень генерации НЕ помечаются static, иерархия `<Root>_Colliders` едет с Rigidbody как compound-коллайдер (боксы + капсулы для круглых баров).
- `ShipPresetCreator.cs` — создаёт префаб корабля: `Rigidbody`, `BoxCollider` на платформе/пилоте/двери/грузе, `SphereCollider` на обменнике, `CapsuleCollider` на цилиндрах. NavMeshSurface на DeckNavSurface.
- `ShipSummaryWindow.cs` — инспектор Rigidbody (mass/drag/angularDrag).
- `CombineMeshesToCollider.cs` — применялся к импортированным схемам (порт); для кораблей предпочтителен compound из боксов (физика предсказуема).

### 3.3 Хранение деталей
- Корабли не «склеены» в один меш по умолчанию — детали остаются отдельными объектами (визуал + коллайдеры). `ShipCargoVisual` — визуализация груза.
- Модули: `docs/Ships/Modul_system/`, `customisation/`. Ключи/мета: `ShipKey`, `MetaRequirement`.
- Доки: `docs/Ships/SHIP_REFACTOR_PLAN_2026-07-21.md`, `ship_collision_analysis_2026-07-17.md`, `damage_subsystem/`.

### 3.4 Массовость кораблей
- В `WorldScene_0_0` historically root `ships` с 3 тестовыми `ShipController` (status не верифицирован, root не найден grep'ом). NPC-корабли — scene-placed.
- Корабли — участники ребейза (`NetworkTransform.Teleport`). Адаптер dormant (`docs/Ships/08C_SHIP_ADAPTER_DESIGN.md`).

---

## 4. Двери: DoorController, префабы, коллайдеры, разрушаемость

### 4.1 Устройство
- `Assets/_Project/Scripts/Ship/DoorController.cs` — `MonoBehaviour`, `[RequireComponent(typeof(Collider))]`. Типы: SlidingSingle/Double, HingedSingle/Double, LiftUp. Анимация в локальных координатах (FO-safe). Toggle() вызывается из `NetworkPlayer`.
- Замки: `MetaRequirement` (заперто/открыто).
- Префабы: `Assets/_Project/Prefabs/...` (DoorPrefabBuilder.cs). 3 одиночных + 1 двустворчатый + ADMIN-варианты. 2 161 одиночных + 110 двустворчатых + 133 ADMIN.
- Коллайдеры: `BoxCollider` на полотне (блокирует), рама — триггер. 4 432 BoxCollider на дверях.
- `DoorController` не меняет `isTrigger` в Awake; полотно твёрдое.

### 4.2 Разрушаемость
- **Не разрушаемы.** Нет HP, нет fracture. Закрытая дверь блокирует проход (бокс полотна). Проблема: при открытии может вдавить капсулу в стену (отмечено в `05_HOUSES_BOXES.md` §8.6 и §11.7) — рекомендация: перевести бокс полотна в кинематический триггер, проём закрывать отдельным объёмным боксом.

### 4.3 Влияние на draw calls
- Двери = 84 % draw calls кадра (17 766 из 21 118 в конфиге B). SRP Batcher не объединяет вызовы; нужен GPU instancing/BRG/Drawer. См. `04_OCCLUSION_CULLING_RESEARCH.md` §3.

---

## 5. Пропсы: RuinsValleys, Secund/Tertius/Quartus, мелкие объекты

### 5.1 Устройство (из `beam_research/01_our_city_optimization.md` §1.6)
- `RuinsValleys`: 496 инстансов, 2 инстансинг-материала, ~5–7 draw calls, тени выкл, коллайдеров нет, `isStatic=false`, разбросаны на 77.7×78.2 км. Визуальный culling целыми хамлетами через `DetailDistanceCuller`.
- `Secund` / `Tertius` / `Quartus`: по 25 рендереров, ~7.6–8.8k тр., центры (15 733, 61 731) / (4 428, 2 931) / (76 842, 70 471). Материалы `URP/Lit` (остаток 511 слотов вне города).
- `Primum_farms`: 142 рендерера (было 11 712 → батчи), 1 269 389 тр.
- `OUTLIERS` ×3: 75 рендереров, выключены (столы, бра с габаритом 90–172 м, ручки).

### 5.2 Инстансинг
- GPU instancing через общие материалы (2 материала для RuinsValleys). `DetailDistanceCuller` использует `Renderer.enabled` (не SetActive) — дешевле, не трогает иерархию при FO-сдвиге.
- Статические флаги не ставим (FO). Static Batching off.

### 5.3 Мелкие объекты
- В городе: 1 116 рендереров `_INT_` (интерьеры, остались в батчах), 172 рендерера вне батчей (OUTLIERS, FBX-двери, эмблемы, No Name).
- Коллайдеры: 324 «блобообразных» стен-бокса, 157 выше 8 м (открытый дефект, не блокирует).

---

## 6. DOTS/ECS в проекте

- **Нет.** `Packages/manifest.json` не содержит `com.unity.entities`, `com.unity.physics`, `com.unity.rendering.hybrid`. Код: `grep Unity.Entities` по `*.cs` — 0 совпадений (только `Unity.Collections`).
- Проект на NGO GameObjects (`NetworkBehaviour`, `NetworkObject`, `NetworkVariable<T>`, `[Rpc(SendTo.X)]`).
- `beam_research` явно: «Не мигрировать на DOTS/Entities — мы на NGO GameObjects».
- `CombineMeshesToCollider` использует `CombineMeshes` (CPU, не DOTS). GPU-инстансинг — через URP GPU Resident Drawer / BRG, но выключен.

---

## 7. GPU instancing / BRG: статус

Источник: `06_REFERENCE/01_INDUSTRY_PRACTICE.md` §7–8, `beam_research/01_our_city_optimization.md` §2.10.

| Настройка | Файл | Значение (текущее, после отката 18.10) |
|---|---|---|
| SRP Batcher | `ProjectC_URP.asset` | вкл (`m_UseSRPBatcher: 1`) |
| GPU Resident Drawer | `ProjectC_URP.asset` | **Disabled** (`m_GPUResidentDrawerMode: 0`) |
| GPU occlusion culling (Drawer) | `ProjectC_URP.asset` | выкл (`m_GPUResidentDrawerEnableOcclusionCullingInCameras: 0`) |
| Rendering Path | `ProjectC_URP_Renderer.asset` | **Forward** (`m_RenderingMode` — в docs фиксировано откат к Forward; ассет показывает 2=Forward+, расхождение требует проверки) |
| BRG Variants | `GraphicsSettings.asset` | **Strip** (`m_BrgStripping`) |
| Static Batching | `ProjectSettings.asset` | off (`m_StaticBatching: 0`) |
| Dynamic Batching | `ProjectSettings.asset` | off (`m_DynamicBatching: 0`) |
| Галочка GPU Instancing в материалах | — | рекомендация Unity: отключить (лишние шейдерные варианты) |

- Причина отката: кастомные шейдеры (`ProjectC/ProceduralSteel` и др.) не поддерживают `DOTS_INSTANCING_ON` → `undeclared identifier 'unity_ObjectToWorld'` → съеденные куски мешей.
- Рецепт возврата (Э5): `#pragma multi_compile _ DOTS_INSTANCING_ON` + `UNITY_DOTS_INSTANCED_PROP` / `unity_ObjectToWorld` из `UnityDOTSInstancing.hlsl` → включить Drawer = Instanced Drawing, GPU occlusion, BRG Variants = Keep All, Forward+.
- В `01_INDUSTRY_PRACTICE.md` §7 описано состояние «включено» (17.10), §8 — откат (18.10). Текущее состояние — откачено.

---

## 8. Стриминг: ClientSceneLoader, WorldStreamingManager

### Действующий
- `Assets/_Project/Scripts/World/Scene/ClientSceneLoader.cs` (1028 строк): singleton + `DontDestroyOnLoad`. Сетка 6×4, `SCENE_SIZE = 79999f`, `preloadDistance = 10000f`, `unloadDistance = 10000f`, `maxLoadedScenes = 4`. Загрузка — `SceneManager.LoadSceneAsync(path, Additive)` (не через `NetworkSceneManager`).
- `ScenePlacedObjectSpawner.cs` — серверный спавн scene-placed `NetworkObject` с `InScenePlacedSourceGlobalObjectIdHash == 0`. Удалять запрещено.
- `ServerSceneManager.cs` — серверный `NetworkBehaviour`, per-client карта `clientId → SceneID`, `NetworkHide/NetworkShow` (зачаток interest management).
- `Assets/_Project/Scripts/World/Scene/SceneRegistry.cs`: `GridColumns = 6`, `GridRows = 4`, `SceneNamePrefix = "WorldScene_"`.

### Legacy (не используются)
- `WorldStreamingManager.cs` (`loadRadius = 2`, `unloadRadius = 3`, `updateInterval = 0.5f`), `WorldChunkManager`, `ChunkLoader`, `ProceduralChunkGenerator`, `PlayerChunkTracker`, `ChunkNetworkSpawner`, `FloatingOriginMP` — ни одна сцена не ссылается. Прототипы лежат кодом. Не воскрешать.
- `FloatingOriginMP.cs`: `threshold = 150000f`, `shiftRounding = 10000f`, режимы Local/ServerSynced/ServerAuthority. Заменён `GlobalMotionControlledRebaseSlice`.

### Floating Origin
- `GlobalMotionControlledRebaseSlice`: F8 вручную, авто при отлёте > 256 м (опрос 5 с, кулдаун 35 с), F9 откат. Корни сцен + игроки едут `position += T` с `NetworkTransform.Teleport`. Маркеры `runtimeRebase.Completed`, `ActorRebound(ok=True)`.
- Контент под корнями `WorldScene_*` едет бесплатно. Скрипты, кэширующие мировые `Vector3`, требуют хуков (`ApplyRebaseTranslation`).

---

## 9. Budgets: draw calls, коллайдеры, объекты

### До склейки
- `ROADMAP.md` §1: исходная точка 17.10 — **147 715 объектов**, 4.49 млн треугольников, 167 969 рендереров в `WorldScene_0_0`, тысячи draw calls.
- `beam_research/01`: до join — 147 715 объектов / 4.49 млн тр. (версия 18 чанков — старые цифры).
- `01_COLLIDERS.md`: 25 000+ исходных объектов в структурных классах → 10 831 бокс.

### После склейки (Э4, 30.10)
- `PRIMUM`: 19 944 → **1 207** рендереров (761 батч по материалу + 446 прочих), слоты 26 566 → **1 806**, тр. 5 438 062.
- `PRIMUM_DOORS`: 11 726 → **4 949** (2 363 инстанса × 2), слоты 12 189.
- ADMIN-двери: 712 → **266** (133 × 2).
- Чанк: 6 … 6 388 → **22 … 104** мешей.
- `WorldRoot_0_0` после Э8: рендереров 24 670 → **18 447**, ламп 2 098 → **2 087**, коллайдеров 18 109 → **17 181** (17 172 бокса + 9 мешей).
- `WorldRoot_0_0` на 30.10 вечер (после ферм): **7 152 рендерера**, 6 692 333 тр., 19 754 узла, 15 403 слотов, 92 материала, `Primum_farms` 11 712 → 142.

### Draw calls
- Бюджет DoD: **≤ 120** (L0).
- Факт: **26 981–26 996** (shadow 1000 / 2 каскада, двери вкл). Зазор ~225×.
- Конфиг B (shadow 250 / 1 каскад): 21 118. Двери выкл: 4 954 (A) / 3 352 (B). Двери = 84 % draw calls (17 766 из 21 118).
- SetPass: 232 (A) / 216 (B). Треугольники: 15.86–16.57 млн (A) / 11.61 млн (B).

### Коллайдеры
- 10 817 в PRIMUM (10 809 боксов + 8 мешей) → после Э8 10 436 (10 355 + 74 + 382 + 6 mesh + 2 surface) — точнее: 10 436 всего в узле (10 355 домов + 74 BASE + 382 ADMIN + 6 legacy mesh + 2 surface? пересчёт: docs дают 10 436).
- `WorldRoot_0_0`: 17 181 (17 172 бокса + 9 мешей).

### Материалы
- После Э3.1: 0 слотов `URP/Lit` в городе+фермах; 53 материала (DoD ≤ 60). До: 444 инстанса / 105 имён, 24 378 слотов (92.1 %) `URP/Lit`.
- Всего `WorldRoot_0_0`: 92 материала, 15 403 слота, 3 202 `URP/Lit` (RuinsValleys 496 + горные группы 15).

### Зернистость (DoD)
- `PRIMUM` ≤ ~1 200 мешей (✅ 1 207), дверь = 2 рендерера (✅), чанк ≤ ~110 (✅ 104). Цель «≤ 600 мешей» отменена (2 363 двери × 2 = 4 726).

---

## 10. Упоминания «массовые объекты», «100k», «разрушаемость», «voxel», «per-object physics»

| Термин | Где | Контекст |
|---|---|---|
| 147 715 объектов | `docs/world/primum_city/ROADMAP.md` §1, `beam_research/01_our_city_optimization.md` §1.2 | Исходная точка до склейки |
| 100k+ дочерних объектов | `CombineMeshesToCollider.cs:14` | Импортированные схемы (порт) |
| Массовые объекты / «не склеивать» | `beam_research/README.md` §0 | Видение пользователя: оставить каждый объект независимым, решать через culling/слои |
| Разрушаемость | `ShipHull.cs`, `Combat/*`, `TradeWorld.TryDamageCargo` | Только корабли/NPC/игроки; окружение не разрушаемо |
| Voxel | нет | Отсутствует полностью |
| Per-object physics | `BuildSettlementColliders shipMode` | Compound-коллайдеры для кораблей/ферм; не массовые пропсы |
| «100 NPC» | `docs/Character/2026-06-26-npc-p2-visual-config-chunk-integration.md` | Rate-limit спавна |
| GPU Resident Drawer / BRG | `06_REFERENCE/01_INDUSTRY_PRACTICE.md` | Выключен из-за шейдеров |
| DOTS/Entities | нет | Не используется |

---

## 11. Интернет-ресерч: массовые объекты без склейки (100k+ low-poly)

### 11.1 100k+ объектов с физикой
- **PhysX (встроенный в Unity):** жёсткий лимит ~65k коллайдеров на сцену (16-bit index buffer в broadphase). 100k+ живых rigidbody — не потянет (CPU-bound, O(n²) в худшем случае).
- **Unity Physics (DOTS):** поддерживает 100k+ коллайдеров/rigidbody (ECS, Jobs, Burst). Но требует полного перехода на DOTS (SubScenes, EntityCommandBuffer, IJobEntity).
- **Без DOTS:** реалистичный потолок — сотни–тысячи активных rigidbody, десятки тысяч статических коллайдеров (через GPU RD для визуала, но коллайдеры всё равно CPU).
- **Вывод:** массовая физика (100k объектов с коллайдерами) без DOTS невозможна. С DOTS — возможна, но требует миграции (мы на NGO GameObjects).

### 11.2 Рендер 100k статических
- Возможен через GPU Resident Drawer (Forward+, DOTS_INSTANCING_ON шейдеры), но только для однотипных меш+материал.
- Ограничения: шейдеры должны поддерживать DOTS_INSTANCING_ON (наш блокер), LOD Group animated cross-fade несовместим с Drawer.
- Статические объекты без Rigidbody можно рендерить через GPU RD (десятки тысяч draw calls → 1 draw call на mesh+material).

### 11.3 Разрушаемость (destructible)
- **Pre-fractured (Fortnite-стиль):** объект разбивается на куски заранее (в Editor). Runtime — просто активация кусков. Реалистично без DOTS, но требует ручной работы по каждому объекту.
- **Voxel (Teardown-стиль):** мир из вокселей, разрушение = удаление вокселей. Требует собственного voxel-движка (большой проект). Teardown использует собственный движок.
- **Runtime boolean (OpenFracture):** boolean-операции в runtime. Дорого, не подходит для массовых объектов.
- **Mesh slicing:** разрезание меша по плоскости. Дорого, артефакты.
- **Вывод:** для нашего проекта реалистичен только pre-fractured (Fortnite-стиль) для отдельных объектов (стены, пропсы). Voxel — отдельный проект.

### 11.4 MMO: interest management
- NGO 2.13 даёт только примитивный visibility callback (`CheckObjectVisibility`, `NetworkShow/Hide`, `ContinuallyCheckVisibility`).
- Для 100k+ объектов нужна своя interest-management система поверх spatial hash (кто релевантен клиенту по дистанции/зоне).
- `ServerSceneManager.cs` — зачаток (per-client карта `clientId → SceneID`, `NetworkHide/NetworkShow`).

### 11.5 Примеры игр
- **Minecraft:** voxel-чанки, каждый блок — независимый (но рендерится через chunk mesh, не отдельным draw call). Физика минимальная (гравитация, коллизии). Разрушение = удаление блока.
- **Teardown:** voxel-движок, разрушение = удаление вокселей. Собственный движок, не Unity.
- **No Man's Sky:** procedural planets, impostors, LOD, streaming. Массовые объекты рендерятся через GPU instancing.
- **Fortnite:** pre-fractured здания, destructible meshes. PhysX для физики кусков.
- **Rust:** base building, destructible стены (pre-fractured).
- **Valheim:** destructible terrain (voxel-ish, but simpler).

### 11.6 Сравнение: склейка vs без склейки

| Аспект | Склейка (текущий путь) | Без склейки (массовые объекты) |
|---|---|---|
| Редактор/CPU | ✅ Меньше объектов → меньше overhead | ❌ 147k объектов → лаги |
| Draw calls | ❌ Склейка не снимает draw calls | ✅ GPU RD + instancing (если включён) |
| Физика | ✅ Боксы по классам (статика) | ❌ Mass physics требует DOTS |
| Разрушаемость | ❌ Нет (статика) | ✅ Pre-fractured (Fortnite-стиль) |
| MMO interest management | ✅ Проще (меньше объектов) | ❌ Нужна своя система |
| Complexity | ✅ Проще | ❌ Сложнее (culling, streaming, interest) |
| FO-совместимость | ✅ Контент под корнями едет | ⚠️ Нужно следовать пайплайну 09S |
| GPU instancing | ✅ Одинаковые меш+материал → 1 draw call | ✅ То же, но больше объектов |

---

## 12. Вывод: «а что если без склейки?»

### Что возможно без склейки (в рамках нашего стека)
1. **Визуальные пропсы (RuinsValleys, мелкие объекты):** уже работают как независимые инстансы с 2 материалами → 5–7 draw calls. Можно расширить на другие типы объектов (деревья, камни, мусор), если включить GPU Resident Drawer.
2. **Двери:** 2 161 инстанс, каждый = 2 рендерера. Можно оставить независимыми, но нужен GPU instancing (Drawer) или HLOD/impostors для дальних.
3. **Корабли:** compound-коллайдеры из боксов + детали остаются отдельными объектами. Не склеивать в один меш (теряется функциональность).
4. **Pre-fractured объекты:** для разрушаемости (стены, пропсы) — pre-fractured в Editor, runtime активация кусков.

### Что НЕ возможно без склейки (в рамках нашего стека)
1. **Массовая физика (100k объектов с коллайдерами):** PhysX не потянет. Нужен DOTS (Unity Physics), но мы на NGO GameObjects.
2. **Voxel-разрушаемость (Teardown-стиль):** отдельный проект, собственный движок.
3. **Runtime boolean/fracture:** дорого, не для массовых объектов.

### Рекомендация
- **Не склеивать дальше** (Э4 закрыт). Оставить каждый объект независимым там, где это нужно для функциональности (двери, корабли, пропсы).
- **Склеивать только там, где это оправдано** (например, мелкие декоративные элементы одного материала в одном чанке → батч). Но не склеивать корабли, двери, интерьеры.
- **Писать свою оптимизацию** (spatial partitioning + portal-based culling + distance-based culling + impostors + HLOD) поверх существующих объектов, не увеличивая их количество.
- **Разрушаемость** — отдельный проект (pre-fractured для отдельных объектов, не для всего города).

---

## 13. Ключевые файлы (пути)

### Скрипты физики/коллайдеров
- `C:\Projects\ProjectC_client\Assets\_Project\Editor\CombineMeshesToCollider.cs`
- `C:\Projects\ProjectC_client\Assets\_Project\Editor\BuildSettlementColliders.cs`
- `C:\Projects\ProjectC_client\Assets\_Project\Editor\StairCollidersTool.cs`
- `C:\Projects\ProjectC_client\Assets\_Project\Editor\CollectCollidersTool.cs`
- `C:\Projects\ProjectC_client\Assets\_Project\Editor\GenerateSimpleCityColliders.cs`
- `C:\Projects\ProjectC_client\Assets\_Project\Editor\Primum\PrimumHouseColliders.cs`
- `C:\Projects\ProjectC_client\Assets\_Project\Editor\Primum\PrimumBaseDeckColliders.cs`
- `C:\Projects\ProjectC_client\Assets\_Project\Editor\Primum\DoorPrefabBuilder.cs`

### Корабли
- `C:\Projects\ProjectC_client\Assets\_Project\Scripts\Player\ShipController.cs`
- `C:\Projects\ProjectC_client\Assets\_Project\Scripts\Ship\Combat\ShipHull.cs`
- `C:\Projects\ProjectC_client\Assets\_Project\Editor\ShipPresetCreator.cs`
- `C:\Projects\ProjectC_client\Assets\_Project\Editor\ShipSummaryWindow.cs`

### Двери
- `C:\Projects\ProjectC_client\Assets\_Project\Scripts\Ship\DoorController.cs`

### Стриминг/FO
- `C:\Projects\ProjectC_client\Assets\_Project\Scripts\World\Scene\ClientSceneLoader.cs`
- `C:\Projects\ProjectC_client\Assets\_Project\Scripts\World\Scene\ScenePlacedObjectSpawner.cs`
- `C:\Projects\ProjectC_client\Assets\_Project\Scripts\World\Scene\ServerSceneManager.cs`
- `C:\Projects\ProjectC_client\Assets\_Project\Scripts\World\Scene\SceneRegistry.cs`
- `C:\Projects\ProjectC_client\Assets\_Project\Scripts\World\Streaming\WorldStreamingManager.cs` (legacy)
- `C:\Projects\ProjectC_client\Assets\_Project\Scripts\World\Streaming\FloatingOriginMP.cs` (legacy)

### Culling/оптимизация
- `C:\Projects\ProjectC_client\Assets\_Project\Scripts\World\DetailDistanceCuller.cs`
- `C:\Projects\ProjectC_client\Assets\_Project\Scripts\World\ViewDistanceApplier.cs`
- `C:\Projects\ProjectC_client\Assets\_Project\Scripts\World\ViewDistanceConfig.cs`
- `C:\Projects\ProjectC_client\Assets\_Project\Scripts\World\PrimumLightPool.cs`
- `C:\Projects\ProjectC_client\Assets\_Project\Scripts\Rendering\DistantFocusRenderFeature.cs`
- `C:\Projects\ProjectC_client\Assets\_Project\Scripts\Rendering\FarFocusController.cs`

### Документы
- `C:\Projects\ProjectC_client\docs\world\primum_city\ROADMAP.md`
- `C:\Projects\ProjectC_client\docs\world\primum_city\04_COLLIDERS\01_COLLIDERS.md`
- `C:\Projects\ProjectC_client\docs\world\primum_city\04_COLLIDERS\04_STATUS_AND_GAPS.md`
- `C:\Projects\ProjectC_client\docs\world\primum_city\04_COLLIDERS\05_HOUSES_BOXES.md`
- `C:\Projects\ProjectC_client\docs\world\primum_city\beam_research\README.md`
- `C:\Projects\ProjectC_client\docs\world\primum_city\beam_research\01_our_city_optimization.md`
- `C:\Projects\ProjectC_client\docs\world\primum_city\06_REFERENCE\01_INDUSTRY_PRACTICE.md`
- `C:\Projects\ProjectC_client\docs\world\primum_city\06_REFERENCE\02_FO_NETWORK_AND_GAMEPLAY.md`
- `C:\Projects\ProjectC_client\docs\world\primum_city\06_REFERENCE\04_OCCLUSION_CULLING_RESEARCH.md`
- `C:\Projects\ProjectC_client\docs\world\optimization\README.md`
- `C:\Projects\ProjectC_client\docs\world\optimization\IMPLEMENTATION_PLAN.md`
- `C:\Projects\ProjectC_client\docs\world\chunk_system\01_CURRENT_STATE.md`
- `C:\Projects\ProjectC_client\docs\world\chunk_system\03_DISTANT_CITIES_TERRAIN.md`
- `C:\Projects\ProjectC_client\docs\Ships\SHIP_REFACTOR_PLAN_2026-07-21.md`

### Настройки
- `C:\Projects\ProjectC_client\Assets\_Project\Settings\ProjectC_URP.asset`
- `C:\Projects\ProjectC_client\Assets\_Project\Settings\ProjectC_URP_Renderer.asset`
- `C:\Projects\ProjectC_client\ProjectSettings\ProjectSettings.asset`
- `C:\Projects\ProjectC_client\ProjectSettings\GraphicsSettings.asset`
- `C:\Projects\ProjectC_client\Packages\manifest.json`

---

### Источники (интернет-ресерч)
- Unity Discussions: "Large World Coordinates" (https://discussions.unity.com/t/large-world-coordinates/1716339)
- Unity Manual: GPU Resident Drawer (URP) (https://docs.unity.com/en-us/engine/6000.6/manual/.../gpu-resident-drawer)
- Unity Manual: Mesh LOD (https://docs.unity.com/en-us/engine/6000.5/manual/analysis/graphics-performance-profiling/lod/mesh-lod)
- Unity Manual: Occlusion Culling (https://docs.unity3d.com/6000.5/Documentation/Manual/OcclusionCulling.html)
- Unity Manual: CullingGroup API (https://docs.unity3d.com/6000.0/Documentation/Manual/CullingGroupAPI-landing.html)
- Unity Entities Graphics (https://docs.unity.cn/Packages/com.unity.rendering.hybrid@0.10/manual/index.html)
- Intel Masked Occlusion Culling (https://github.com/GameTechDev/MaskedOcclusionCulling)
- Teardown (voxel destruction) — https://teardowngame.com/
- Fortnite (pre-fractured) — https://www.epicgames.com/fortnite
- Minecraft (voxel chunks) — https://www.minecraft.net/
- No Man's Sky (procedural + impostors) — https://www.nomanssky.com/
- Rust (destructible bases) — https://rust.facepunch.com/
- Valheim (destructible terrain) — https://www.valheimgame.com/
- OpenFracture (Unity) — https://github.com/dgreenheck/OpenFracture
- NativeTrees (MIT) — https://github.com/bartofzo/NativeTrees
- Spatial-Hashing (MIT) — https://github.com/Sylmerria/Spatial-Hashing
- Unity-Technologies/HLODSystem — https://github.com/Unity-Technologies/HLODSystem
- Abdns/ImpostorSystem_Unity — https://github.com/Abdns/ImpostorSystem_Unity

---

*Исследование проведено read-only. Код не писался, файлы не изменялись (кроме создания этого документа).*
