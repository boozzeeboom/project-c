# 07. Корабли без склейки + разрушаемость: PhysX vs DOTS, совмещение, «не нагружаемая» разрушаемость

> Дата: 2026-10-30. Исследование по запросу пользователя: "что если мы не будем склеивать корабли — они тоже простые меш-материал — кусок корпуса и состоят почти все из простых элементов посмотри сам — и они именно получат разрушаемость притом абсолютно не нагружаемую? совмещение возможно? основной корабль летает по физикс а его куски корпуса под дотс или как?"

> Режим: research-only, код не менялся. Два сабагента: explore (кодовая база) + general (интернет-ресерч).

## 0. TL;DR

1. **Корабли уже частично «не склеены»:** визуал — combined mesh (`*_Combined.asset`), коллайдеры — compound из боксов (shipMode) или отдельные MeshCollider на группы. Двери, пилотское кресло, грузовой отсек — отдельные GO с коллайдерами. `CombineMeshesToCollider` (один concave MeshCollider) применялся к импортированным схемам (порт), не к кораблям.
2. **Корабли простые:** состоят из простых элементов (Platform, PilotSeat, Door, Exchanger, EngineVisuals, ModuleSlots). Визуал — combined mesh из простых мешей. Коллайдеры — box/capsule primitives.
3. **Разрушаемости нет:** урон — только HP (ShipHull), без визуальной деформации. Документация: «Визуальные эффекты (дым, искры, деформация) — Post-MVP».
4. **«Абсолютно не нагружаемая» разрушаемость достижима** — но только в стиле **pre-fractured (Fortnite)**: заранее нарезанные куски в пуле, активация (не Instantiate), debris lifetime, коллайдеры только на крупных, PhysX sleep states, ignore collisions между мелкими.
5. **Совмещать PhysX-root с DOTS-chunks — НЕ стоит.** Unity Physics (DOTS) и PhysX — два разных физических мира без общей broadphase/контактов. Гибрид даст дублирование состояния, проблемы с коллизиями, двойной ребейз при FO, не даст выигрыша при малом числе обломков (8–32 на корабль).
6. **Держать корабль и куски на PhysX (GameObject/Rigidbody).** DOTS — только при полной миграции проекта на ECS (включая сеть), что не входит в scope.
7. **NGO 2.13** работает с GameObjects; Netcode for Entities — отдельный пакет, миграция не в scope.
8. **Реалистичный потолок без DOTS:** сотни одновременно активных кусков с физикой; визуально — десятки тысяч обломков через GPU RD (если включён).

---

## 1. Текущая архитектура корабля (PhysX + NGO, один корень)

**Главный скрипт:** `Assets/_Project/Scripts/Player/ShipController.cs` (NetworkBehaviour, `IGlobalMotionActorParticipant`).

- `[RequireComponent(typeof(Rigidbody))]`, `[RequireComponent(typeof(NetworkObject))]`.
- `Rigidbody`: `linearDamping=0.4`, `angularDamping=8.0`, `useGravity=true`, `collisionDetectionMode=Continuous`, `interpolation=Interpolate`, `constraints=shipConstraints` (по умолчанию `RigidbodyConstraints.None`).
- Масса: `massLight=800 * massMultiplier(10)=8000`, `massMedium=1000*10=10000`, `massHeavy=1500*10=15000`, `massHeavyII=2000*10=20000` (применяется в `ApplyShipClass()`).
- `NetworkTransform` (ServerAuthority, Interpolate=true) — репликация движения.
- Сервер-авторитативное движение: `FixedUpdate` пишет `_rb` (AddForce/AddTorque) на сервере; клиенты интерполируют.
- Состояние: `_netIsDocked`, `_netEngineRunning` (NetworkVariable<bool> Server).
- Заморозка при отсутствии пилотов: `_frozenByNoPilot`.
- NPC-пилот: `_hasNpcPilot`, `ApplyServerInput`, `EnableNpcPilot`.
- Floating Origin: `GlobalMotionActorLink _coordinateLink`, `CanSimulateInCurrentCoordinates`, `ConfirmGlobalPhysicsPrepared`, `OnGlobalBaselineApplied`.

**Ссылки/поиск корня:** `ShipRootReference` (`Assets/_Project/Scripts/Ship/ShipRootReference.cs`) — маркер на любой части корабля; `ShipComponentLocator` (`Assets/_Project/Scripts/Ship/ShipComponentLocator.cs`) — единый поиск `ShipController`.

**Композитная архитектура Phase 0:** корабль — составная конструкция; части (PilotSeat, Door, ModuleSlot) — дочерние GO с `ShipRootReference`. `PilotSeatController`, `DoorController` — поведение отдельных частей. (см. `docs/MMO_Development_Plan.md` §2.15, `docs/Ships/customisation/`).

---

## 2. Корпус, HP и урон

**Файл:** `Assets/_Project/Scripts/Ship/Combat/ShipHull.cs` (NetworkBehaviour, `IDamageTarget`).

- `NetworkVariable<int> _hull`, `_maxHull` (Server write).
- HP по классу: Light=100, Medium=200, Heavy=400, HeavyII=600 (`ShipDamageConfig`).
- `IsAlive()` всегда `true` — корабль не деспаунится при 0 HP («сломан», не «труп»).
- Источники урона:
  - **Столкновения:** `ShipController.OnCollisionEnter` → `ShipHull.ApplyCollisionDamage(energy)` (energy = `col.impulse.magnitude`). Формула: `floor((energy-8)*0.5)`, cap 50. Защиты: `minCollisionRelativeSpeed=3 м/с`, `postUndockGraceSeconds=3 с`, `IsDocked` guard.
  - **Боевое оружие:** `CombatServer.ResolveAttack` → `ShipHull.ApplyDamage(DamageResult)` (ERPR-формула, armorHull=5).
- 0 HP = Broken: скорости ×`brokenSpeedMultiplier=0.1`, груз обнуляется (`WipeCargo`), двигатель работает.
- Ремонт: `ShipModuleServer.RequestRepairHullRpc` (владение ключом + IsDocked + кредиты) → `RepairFull()` + `ClearHullBroken()`.
- Регистрация в `CombatServer` как `IDamageTarget` в `OnNetworkSpawn` (server).

**Файл:** `Assets/_Project/Scripts/Ship/Combat/ShipDamageConfig.cs` (ScriptableObject, `Resources/Ship_hull/ShipDamage*.asset`).

**Документация:** `docs/Ships/damage_subsystem/00_DESIGN.md`, `01_ARCHITECTURE.md`, `02_INTEGRATION_AND_REPAIR.md`.

---

## 3. Коллайдеры кораблей (shipMode, compound, CombineMeshesToCollider)

### 3.1. BuildSettlementColliders (shipMode)
**Файл:** `Assets/_Project/Editor/BuildSettlementColliders.cs`.

- Режим `shipMode=true`: холдеры/группы/корень генерации **не** помечаются static; иерархия `<Root>_Colliders` едет с Rigidbody как **compound-коллайдер** (боксы + капсулы для круглых баров).
- Генерирует BoxCollider/SphereCollider/CapsuleCollider по мешам.
- Меню: `Tools → ProjectC → Build Settlement Colliders` (есть Dry Run).

### 3.2. CombineMeshesToCollider
**Файл:** `Assets/_Project/Editor/CombineMeshesToCollider.cs`.

- Склеивает дочерние `MeshFilter` в один меш → `MeshCollider (convex=false)` на корне.
- Используется для **импортированных схем** (порт, «gorod port»), не для кораблей как стандарт.
- `CharacterController` требует одного невыпуклого mesh-коллайдера.

### 3.3. Фактическая структура префабов кораблей (подсчёт по YAML)
Команда: `Select-String -Pattern "MeshFilter:|MeshCollider:|BoxCollider:"` по `Assets/_Project/Prefabs/Ships/*.prefab`.

| Префаб | MeshFilter | MeshRenderer | MeshCollider | BoxCollider |
|---|---|---|---|---|
| NPC-корабли (20 шт., генерация ShipPresetCreator) | 6 | 6 | 0 | 5 |
| `DONT-DELETE-NPC REFERENCE SHIP` | 6 | 6 | 0 | 5 |
| `Ship_havyII_base` | 2 | 2 | 0 | **250** |
| `Ship_Light_root_fbx_test` | 2 | 2 | 2 | 15 |
| `Ship_medium_base` | 2 | 2 | 3 | 3 |

- NPC-корабли: 6 кубов-примитивов (Platform/PilotSeat/Door/Exchanger/EngineVisuals/Cylinder) + 5 BoxCollider.
- Тяжёлый корабль: 250 BoxCollider (compound, shipMode) — корпус разбит на боксы.
- FBX-тест: 2 MeshCollider (concave, импортированные combined-меша) + 15 BoxCollider.
- Средний: 3 MeshCollider (импортированные) + 3 BoxCollider.

**Вывод:** в проекте уже применяется «не склеивать в один меш» для кораблей: визуал — combined mesh (`*_Combined.asset` в `Assets/_Project/Generated/`, напр. `BARGE_MAIN_DECK_Combined.asset`), коллайдеры — compound из боксов (shipMode) или отдельные MeshCollider на группы. Склейка `CombineMeshesToCollider` применялась к импортированным схемам (порт), не к кораблям как стандарт. (см. `docs/world/primum_city/beam_research/06_no_join_massive_objects.md` §3, §8.)

---

## 4. Меша/материалы кораблей

- Визуал: `MeshFilter.sharedMesh` → либо primitive Cube (10202), либо combined asset (`Assets/_Project/Generated/*_Combined.asset`, GUID sub-asset mesh `4300000`).
- Материалы: стандартные URP Lit (`Packages/.../Runtime/Materials/Lit.mat`) или кастомные шейдеры (`ProjectC/ProceduralSteel` и др.).
- GPU Resident Drawer (Forward+, SRP Batcher, `Keep All` BRG variants, `DOTS_INSTANCING_ON` в шейдерах) — выключен сейчас (откат 18.10 из-за кастомных шейдеров без `DOTS_INSTANCING_ON`). (см. `docs/world/primum_city/beam_research/01_our_city_optimization.md`.)
- Корабли — простые (кусок корпуса + простые элементы): платформа, палуба, мачта, орудие, дверь, грузовой отсек. Не сложные многоуровневые конструкции.

---

## 5. Двери кораблей

**Файл:** `Assets/_Project/Scripts/Ship/DoorController.cs` (MonoBehaviour, `[RequireComponent(typeof(Collider))]`).

- Типы: SlidingSingle/Double, HingedSingle/Double, LiftUp.
- Анимация в **локальных координатах** (FO-safe).
- Полотно — твёрдый BoxCollider (`isTrigger=false`); DoorController не меняет isTrigger.
- Toggle() вызывается из `NetworkPlayer` (E-key). Совместимость с MetaRequirement (замок).
- Вешается на дочерний объект с `ShipRootReference`.
- В ShipPresetCreator: Door — Cube scale (0.04, 1.49, 0.13), sliding single, manual distance 0.2 м, duration 0.8 с, startOpen=true.

---

## 6. Груз (ShipCargoVisual, CargoSystem)

**Файл:** `Assets/_Project/Scripts/Ship/Cargo/ShipCargoVisual.cs` (клиент-сайд).

- Подписывается на `ShipTelemetryClientState.OnShipStateChanged`.
- Спавнит ящики (префабы) внутри BoxCollider `_spawnZone` (grid, bottom→top).
- Object pool (Stack<GameObject>), инкрементальное обновление.
- `ShipCargoConsole` — точка взаимодействия (SphereCollider trigger, radius 1.35).
- Сервер: `TradeWorld`, `ShipCargoRegistry`, `ShipCargoLimitsConfig`, `ShipCargoServer`.
- При поломке корпуса: `WipeCargo()` → `CargoData.Clear()` + `NotifyCargoChanged`.

**Документация:** `docs/Ships/cargo_system/`, `CARGO_VIS_01_DESIGN_2026-07-02.md`.

---

## 7. NavMesh на палубе

**Файл:** `Assets/_Project/Scripts/Ship/ShipDeckNav.cs` (NetworkBehaviour).

- `NavMeshData _deckNavMeshData` (запечённый в редакторе через NavMeshSurface при ShipRoot в origin/identity).
- Регистрация в «нав-песочнице»: `navFrameOrigin = ShipRoot.position` в момент спавна (`_registerUnderShip=true`).
- LateUpdate: следит за сдвигом ShipRoot; при превышении `_navFrameSeparation/2` (5000 м) — пере-регистрация (Remove + Add).
- Round-robin очередь: ≤1 `AddNavMeshData` за кадр (perf).
- Floating Origin: `NotifyWorldRebased()` сбрасывает кулдаун всем инстансам; snapshot/restore для ребейза.
- Конвертация координат: `WorldToDeckLocal`, `DeckLocalToWorld`, `DeckLocalToNav`, `NavToDeckLocal`, `SampleOnDeck`.

---

## 8. Rigidbody, constraints, interpolation, масса, torque

См. §1. Дополнительно:
- `shipConstraints` (RigidbodyConstraints) — по умолчанию None; можно freeze позиции/ротации.
- `antiGravity` (0..1.5) — компенсация гравитации.
- `linearDrag=0.4`, `angularDrag=8.0`.
- Стабилизация: `pitchStabForce=15`, `rollStabForce=20`, `maxPitchAngle=20`, `autoStabilize=true`.
- Ветер: `windInfluence=0.5`, `windExposure` (1.2/1.0/0.7/0.5 по классу), `SplineWindZone` registry.
- Мезий-тяга: `MeziyModuleActivator`, torque/force отдельно (`_activeMeziyTorque`).
- Altitude Corridor: `AltitudeCorridorSystem`, `TurbulenceEffect`, `SystemDegradationEffect`.

---

## 9. Урон: ShipHull HP, ApplyCollisionDamage, CombatServer

См. §2. Дополнительно:
- `ShipController.OnCollisionEnter`: фильтр NPC-пилот без игроков (`_hasNpcPilot && _pilots.Count==0 → return`); ленивая регистрация cargo; `TryDamageCargo`; затем `_hull.ApplyCollisionDamage(energy)` если `relSpeed >= minCollisionRelativeSpeed`.
- `_hullBroken` флаг умножает скорости в FixedUpdate (`hullSpeedMult = 0.1`).
- `ClearHullBroken()` вызывается из `ShipModuleServer` после ремонта.

---

## 10. NetworkObject, scene-placed, ScenePlacedObjectSpawner, FO Teleport

- Корабли — **scene-placed NetworkObject** в `WorldScene_0_0` (и других сценах). Спавнятся через `ScenePlacedObjectSpawner` (иначе [Rpc] падают с NRE).
- `NetworkManager` — один на весь рантайм (`DontDestroyOnLoad`), в BootstrapScene.
- `ClientSceneLoader` грузит сцены обычным `SceneManager.LoadSceneAsync` (не NetworkSceneManager).
- Floating Origin: корабли — участники ребейза. Сдвиг корней сцен целиком; `NetworkTransform.Teleport` для NetworkObject; `ShipDeckNav.NotifyWorldRebased()` для навмеша; snapshot/restore для ShipDeckNav.
- 22 ship/Rigidbody roots в `WorldScene_0_0`: `Ship_Light_root`, `Ship_Light_root (копия...)`, `Альбатрос`, `Берег`, `Вавилон`, `Ветроворот`, `Гигант`, `Горгона`, `Жук`, `Летучий`, `Лорейн`, `Мастодонт`, `Олимп`, `Пещера`, `Река`, `Сильфида`, `Скат`, `Странник`, `Торренс`, `Угольщик`, `Цитадель`, `Шмель`. (см. `docs/world/floatingorigin/06L_GLOBAL_STATIC_WORLD_PILOT.md` §548.)
- Префабы: 24 файла в `Assets/_Project/Prefabs/Ships/` (20 NPC-кораблей + `DONT-DELETE-NPC REFERENCE SHIP`, `Ship_havyII_base`, `Ship_Light_root_fbx_test`, `Ship_medium_base`).

**Документация:** `docs/world/floatingorigin/README.md`, `09S_NEW_CONTENT_PIPELINE.md`, `08C_SHIP_ADAPTER_DESIGN.md`.

---

## 11. Pre-fractured / voxel / fracture / breakable / destructible для кораблей

**В коде проекта:** упоминаний fracture/breakable/destructible для кораблей **нет**. Урон — только HP (ShipHull), без визуальной деформации. Документация явно: «Визуальные эффекты (дым, искры, деформация) — Post-MVP».

**Исследовательские документы (без кода):**
- `docs/dev/research_ship_destruction.md` — детальный ресёрч: pre-fractured (Fortnite-стиль) рекомендуется; runtime boolean/slicing — нет; воксель — отдельный проект.
- `docs/world/primum_city/beam_research/06_no_join_massive_objects.md` §9: «Разрушаемости окружения нет. Урон/HP существуют только для игроков, NPC и кораблей (ShipHull как IDamageTarget). Город, пропсы, двери — статика, коллайдеры не разрушаемые. Voxel/fracture/breakable-систем в проекте нет.»

**Рекомендация из ресёрча (для кораблей):**
- Корпус заранее нарезается в редакторе (Blender Cell Fracture / ProBuilder) на **8–32 куска**.
- Каждый кусок — GameObject с MeshFilter + MeshRenderer + (опц.) MeshCollider (convex, pre-baked через `Physics.BakeMesh`).
- При ударе: скрыть целый корпус, показать куски, применить силу к Rigidbody кусков.
- Пул кусков (inactive → SetActive), не Instantiate/Destroy.
- Debris lifetime: мелкие — 3–5 с, крупные — 30–60 с.
- Коллайдеры только на крупных кусках; мелкие — без коллайдеров или триггеры.
- `Physics.IgnoreCollision` между мелкими (layer matrix: Debris ↔ Debris = ignore).
- Ограничение: ~100 активных кусков с коллайдерами одновременно.

---

## 12. DOTS / Entities / Unity Physics в контексте кораблей

**В проекте DOTS не используется:**
- `Packages/manifest.json` не содержит `com.unity.entities`, `com.unity.physics`, `com.unity.rendering.hybrid`.
- `grep Unity.Entities` по `*.cs` — 0 совпадений (только `Unity.Collections`).
- `docs/world/primum_city/beam_research/06_no_join_massive_objects.md` §6: «DOTS/ECS отсутствует. Не мигрировать на DOTS/Entities — мы на NGO GameObjects.»

**Вывод из ресёрча (`research_ship_destruction.md` §4):**
- **Совмещать PhysX-root корабля с DOTS-chunks не стоит.** Unity Physics (DOTS) и PhysX — два разных физических мира; нет общей сцены/контактов без ручной конвертации. Гибрид создаёт дублирование состояния, проблемы с коллизиями, не даёт выигрыша при малом числе обломков.
- **Держать и корабль, и куски на PhysX (GameObject/Rigidbody).**
- DOTS имеет смысл только при переходе всего проекта на ECS (включая сеть) — что не входит в текущий scope.
- NGO 2.13 работает с GameObjects; Netcode for Entities — отдельный пакет, требует полной миграции.

**Floating Origin + DOTS:** детерминизм Unity Physics требует фиксированного timestep и избегания операций >50 единиц от origin — критично для нашего FO. У нас уже реализован PhysX-ребейз; добавление DOTS-мира удваивает работу.

---

## 13. Ответ на вопрос: «не склеивать + простые элементы + разрушаемость + PhysX root / DOTS chunks»

### 13.1. Можно ли не склеивать корабли?
**Да, и проект уже частично так делает:**
- Визуал: combined mesh (`*_Combined.asset`) — склейка для рендера (один draw call на корабль).
- Коллайдеры: compound из боксов (shipMode) или отдельные MeshCollider на группы — **не** один concave mesh на весь корабль.
- Двери, пилотское кресло, грузовой отсек — отдельные GO с коллайдерами (не слиты в общий меш).
- `CombineMeshesToCollider` (один concave MeshCollider) применялся к импортированным схемам (порт), не к кораблям.

**«Не склеивать» для визуала:** возможно через GPU Resident Drawer (Forward+, `DOTS_INSTANCING_ON`), но сейчас выключен из-за кастомных шейдеров. Для уникальных кораблей (каждый уникален) GPU RD не поможет без стандартизации мешей/материалов → fallback на обычный рендеринг → тысячи draw calls. Рекомендация: LOD-цепочки + impostors для дальних.

### 13.2. Простые меш-материал элементы (кусок корпуса)
**Да:** корабли состоят из простых элементов (Platform, PilotSeat, Door, Exchanger, EngineVisuals, ModuleSlots). Визуал — combined mesh из простых мешей. Коллайдеры — box/capsule primitives. Это уже «простые элементы».

### 13.3. Разрушаемость «абсолютно не нагружаемая»
**Достижимо только в стиле pre-fractured (Fortnite):**
- Заранее нарезанные куски в пуле (inactive → SetActive).
- Не Instantiate/Destroy (дорого).
- Debris lifetime (автоудаление мелких).
- Коллайдеры только на крупных кусках.
- Sleep states (PhysX автоматически).
- Ignore collisions между мелкими.
- Сеть: серверная истина — `damageMask` (битовая маска), клиенты локально активируют куски по маске. Крупные куски (влияющие на геймплей) — NetworkObject; мелкие — локальные.
- **Не использовать runtime boolean/slicing** для корпуса (дорого, нестабильно).
- **Не делать каждый кусок NetworkObject'ом** (убьёт сеть).

### 13.4. Совмещение: основной корабль PhysX, куски под DOTS?
**Нет, не стоит.** (см. `research_ship_destruction.md` §4.3.)
- Два разных физических мира (PhysX + Unity Physics), нет общей broadphase/контактов.
- Дублирование состояния (позиция в обоих мирах), ручная синхронизация каждый кадр.
- Floating Origin: нужно сдвигать оба мира.
- Сеть: NGO работает с GameObjects; DOTS-entity не имеют NetworkObject; связь — через Netcode for Entities (миграция).
- **Выигрыша нет** при малом числе обломков (8–32 на корабль).
- **Рекомендация:** держать корабль и куски на PhysX (GameObject/Rigidbody). DOTS — только при полной миграции проекта на ECS (включая сеть), что не входит в scope.

### 13.5. Реалистичные потолки без DOTS (из `research_massive_unmerged_objects.md` §9.1)

| Категория | Потолок без DOTS |
|---|---|
| Активных GameObject с Update | ~1–5k |
| Статических визуальных (GPU RD) | ~100k–1M (одинаковые меш+материал) |
| Коллайдеров PhysX на сцену | ~65k (hard cap) |
| Активных rigidbody 60 FPS | ~1–5k (с контактами — меньше) |
| Сетевых объектов (NGO) | ~1–5k активных |
| Разрушаемых объектов (pre-fractured) | ~сотни одновременно |

---

## 14. Ключевые файлы (пути)

| Файл | Назначение |
|---|---|
| `Assets/_Project/Scripts/Player/ShipController.cs` | Главный контроллер корабля (PhysX, NGO, FO) |
| `Assets/_Project/Scripts/Ship/Combat/ShipHull.cs` | HP корпуса, IDamageTarget, ApplyCollisionDamage |
| `Assets/_Project/Scripts/Ship/Combat/ShipDamageConfig.cs` | SO конфиг урона |
| `Assets/_Project/Scripts/Ship/Cargo/ShipCargoVisual.cs` | 3D визуал груза (пул ящиков) |
| `Assets/_Project/Scripts/Ship/ShipDeckNav.cs` | NavMesh палубы, FO rebase |
| `Assets/_Project/Scripts/Ship/ShipRootReference.cs` | Маркер корня корабля |
| `Assets/_Project/Scripts/Ship/ShipComponentLocator.cs` | Поиск ShipController |
| `Assets/_Project/Scripts/Ship/DoorController.cs` | Двери (локальная анимация) |
| `Assets/_Project/Editor/BuildSettlementColliders.cs` | Генератор коллайдеров (shipMode) |
| `Assets/_Project/Editor/CombineMeshesToCollider.cs` | Склейка мешей → MeshCollider |
| `Assets/_Project/Editor/ShipPresetCreator.cs` | Создатель префабов кораблей |
| `Assets/_Project/Editor/ShipSummaryWindow.cs` | Сводный редактор кораблей |
| `Assets/_Project/Scripts/World/Scene/ScenePlacedObjectSpawner.cs` | Спавн scene-placed NO |
| `docs/Ships/damage_subsystem/00_DESIGN.md` | Дизайн урона |
| `docs/Ships/damage_subsystem/01_ARCHITECTURE.md` | Архитектура урона |
| `docs/Ships/damage_subsystem/02_INTEGRATION_AND_REPAIR.md` | Интеграция и ремонт |
| `docs/Ships/SHIP_REFACTOR_PLAN_2026-07-21.md` | План рефакторинга |
| `docs/Ships/ship_collision_analysis_2026-07-17.md` | Анализ коллизий |
| `docs/Ships/customisation/` | Кастомизация, модули |
| `docs/Ships/Modul_system/` | Модульная система |
| `docs/world/floatingorigin/08C_SHIP_ADAPTER_DESIGN.md` | Адаптер кораблей для FO |
| `docs/world/floatingorigin/09S_NEW_CONTENT_PIPELINE.md` | Пайплайн нового контента |
| `docs/dev/research_ship_destruction.md` | Ресёрч разрушаемости кораблей |
| `docs/dev/research_massive_unmerged_objects.md` | Ресёрч массовых объектов |
| `docs/world/primum_city/beam_research/06_no_join_massive_objects.md` | «Без склейки» — корабли, двери, пропсы |

---

## 15. Резюме для принятия решения

1. **Не склеивать корабли** — возможно и частично уже делается (коллайдеры compound, двери/кресло отдельные GO). Для визуала без склейки нужен GPU RD (сейчас выключен) + стандартизация мешей/материалов. Реалистичный путь: LOD + impostors для дальних, combined mesh для ближних.
2. **Простые элементы** — уже так: корабли из простых мешей/боксов.
3. **Разрушаемость** — только pre-fractured (Fortnite-стиль), не runtime boolean. Пул кусков, activation, debris lifetime, коллайдеры только на крупных. Сеть — damageMask + локальная активация.
4. **PhysX root + DOTS chunks** — **не рекомендуется**. Держать всё на PhysX. DOTS — только при полной миграции проекта на ECS (включая сеть).
5. **NGO 2.13** — работает с GameObjects; Netcode for Entities — отдельный пакет, миграция не в scope.

**Следующий шаг (если решите реализовывать):** дизайн-нота в `docs/dev/` + маленькие диффы + проверка Compile/Tests/Manual. Агент сам Play Mode не запускает.

---

### Источники

**Внутренние:**
- `docs/dev/research_ship_destruction.md` (детальный ресёрч разрушаемости)
- `docs/dev/research_massive_unmerged_objects.md` (массовые объекты без склейки)
- `docs/world/primum_city/beam_research/06_no_join_massive_objects.md` (без склейки — корабли, двери, пропсы)
- `docs/Ships/damage_subsystem/00_DESIGN.md`, `01_ARCHITECTURE.md`, `02_INTEGRATION_AND_REPAIR.md`
- `docs/Ships/SHIP_REFACTOR_PLAN_2026-07-21.md`, `ship_collision_analysis_2026-07-17.md`
- `docs/world/floatingorigin/08C_SHIP_ADAPTER_DESIGN.md`, `09S_NEW_CONTENT_PIPELINE.md`
- `docs/world/primum_city/beam_research/01_our_city_optimization.md`

**Внешние (интернет-ресерч):**
- Teardown (voxel destruction) — https://teardowngame.com/
- Fortnite (pre-fractured) — https://www.epicgames.com/fortnite
- Sea of Thieves (ship damage) — https://www.seaofthieves.com/
- World of Warships — https://worldofwarships.com/
- Star Citizen Maelstrom (destructible ships) — https://robertsspaceindustries.com/
- BattleBit Remastered — https://battlebit.com/
- OpenFracture (Unity) — https://github.com/dgreenheck/OpenFracture
- Unity Physics (DOTS) — https://docs.unity3d.com/Packages/com.unity.physics@latest
- PhysX vs Unity Physics — https://docs.unity3d.com/Manual/PhysicsOverview.html
- Netcode for Entities — https://docs.unity3d.com/Packages/com.unity.netcode@latest
- Unity Forum: PhysX + DOTS hybrid — https://forum.unity.com/threads/combining-physx-and-unity-physics.929400/
- Unity Blog: DOTS Physics — https://blog.unity.com/technology/dots-physics
- NGO Object Visibility — https://docs.unity3d.com/Packages/com.unity.netcode.gameobjects@1.15/manual/basics/object-visibility.html
- Floating Origin + DOTS — https://discussions.unity.com/t/large-world-coordinates-double-precision-data/902530
- GPU Resident Drawer — https://docs.unity.com/en-us/engine/6000.6/manual/.../gpu-resident-drawer
- Mesh LOD — https://docs.unity.com/en-us/engine/6000.5/manual/analysis/graphics-performance-profiling/lod/mesh-lod

---

*Исследование проведено read-only. Код не писался, файлы не изменялись (кроме создания этого документа).*
