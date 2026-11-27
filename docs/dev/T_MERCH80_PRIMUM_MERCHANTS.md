# T-MERCH80 — 80 именных торговцев PRIMUM

> **Статус:** реализовано 2026-11-27 (Edit Mode). Play Mode не запускался — runtime-проверку делает пользователь.
> **Сцена:** только `Assets/_Project/Scenes/World/WorldScene_0_0.unity`.

## Что сделано

- **80 `NpcDefinition`** — `Assets/_Project/Quests/Data/Npcs/Merchants/Merchant_001..080.asset`.
- **80 `NpcMerchantTable`** — `Assets/_Project/Quests/Data/Merchants/Merchant_001..080_Table.asset`, у каждого свой ассортимент.
- **80 `DialogTree`** — `Assets/_Project/Quests/Data/Dialogs/Merchants/dlg_merchant_primum_001..080.asset`.
- Все NPC и деревья аддитивно добавлены в `QuestDatabase` (`npcs`, `dialogTrees`, `factions` без дублей).
- В `WorldScene_0_0` создана группа **`NPC_PRIMUM_MERCHANTS`** под `WorldRoot_0_0`: 80 экземпляров канонического префаба `[Mira] - DON`T DELETE DEFOULT.prefab`.
- Floating Origin: на все 80 экземпляров запечены `GlobalSceneSourceMarker`, observation/entry в `GlobalMotionPilotSceneCatalog`, digest в `GlobalMotionPilotProfile`. Каталог компилируется, digest сходится.

## Группы ассортимента (11 категорий, 80 торговцев)

| Группа | Торговцев | Что продают |
|---|---:|---|
| `grocery` | 11 | бакалея, продукты, хлеб, рыба, крупы, сладости |
| `weapons` | 10 | оружие (`WeaponItemData`) |
| `clothing` | 10 | одежда и броня (`ClothingItemData` + вещи-экипировка) |
| `equipment` | 10 | инструмент, крепёж, снаряжение |
| `medical` | 8 | аптека, бинты, лекарства, травы |
| `tech` | 10 | техника, связь, батареи, провода, платы |
| `resources` | 8 | руда, слитки, стройматериалы, волокно |
| `fuel` | 3 | горючее, керосин, уголь |
| `antigrav` | 4 | антиграв и комплектующие |
| `meziy` | 3 | мезий, катализаторы, сырьё |
| `implants` | 3 | импланты/модули (`ModuleItemData`) |

Внутри группы товары схожие; таблица у каждого торговца своя (6–10 позиций, детерминированный выбор из пула категории, цены считаются `MerchantPriceResolver`). Часть позиций имеет ограниченный `stock`.

## Конфигурация экземпляров в сцене

| Поле | Значение |
|---|---|
| `NpcController.Definition` | свой `NpcDefinition` (не Мира) |
| `NpcController.Interaction Distance` | `3` |
| `Text (TMP)` | полное имя, uppercase, auto-sizing `12..36` |
| `NpcSocialBrain.Faction` | по группе товара: `GuildOfSuccess` / `FreeTraders` / `Villagers` (см. ниже) |
| `NpcSocialBrain.Idle Activity` | `Wander`, `Wander Radius = 3` |
| `NpcBrain.Behavior Type` | `Passive` |
| Контейнер | под `WorldRoot_0_0` (простой контент, FO двигает вместе с корнем) |

## Фракции и модели (T-MERCH80b)

У каждого торговца `NpcDefinition.factionRef` и `NpcSocialBrain.Faction` указывают на один и тот же ассет; legacy `NpcDefinition.faction` синхронизирован (5 / 8 / 15). Разрешены только три фракции:

| Группы товара | Фракция | Кол-во |
|---|---|---:|
| `weapons`, `tech`, `implants`, `antigrav`, `meziy` | `GuildOfSuccess` (`GuildOfSuccess.asset`) | 30 |
| `equipment`, `resources`, `fuel` | `FreeTraders` (`FreeTraders.asset`) | 21 |
| `grocery`, `medical`, `clothing` | `Villagers` (`Faction_Villagers.asset`, `factionKey: Villagers`) | 29 |

Модель выбирается по полу (из имени) и группе товара, из `Assets/Generated_Models_Rigged_NPC/01_Characters/.../<Model>/<Model>_Rigged_NPC_Humanoid.fbx`:

- **Guild of Success** — базово `Guilds/PC_Char_Guild_Success_Male|Female_APOSE_v01`.
- **Villagers** — базово `City/PC_Char_City_LowerCraftsman_APOSE_v02` (М) / `City/PC_Char_City_MerchantWoman_APOSE_v02` (Ж).
- **FreeTraders** — базово `Archetypes/PC_Char_Merchant_Intermediary_Male_APOSE_v01` / `..._Female_APOSE_v02` (часть женщин — `City_MerchantWoman`).
- **Особые** (небольшая часть точек, «особый товар»): кузнецы — `Blacksmith_*`; техника — `Engineers/*`/`City_GuildEngineer`; импланты — `Stylized_Engineer_*_Hazard`; антиграв — `Pilots/*`; мезий — `Mesium_Handler_*`; медицина — `Medics/*`; ресурсы/топливо — `Scavenger_*`.

Модель в сцене: один дочерний объект у `Visual`, имя `Model_<Model>`, `localPosition (0, 1.5, 0)` (пивот моделей совпадает со старой `HumanM_Model`: ступни ≈ −0.12 м от корня), Animator — `NpcAnimator_Goblin.overrideController`, `applyRootMotion = false`, аватар модели humanoid. `NpcBrain._animator` берётся в рантайме через `GetComponentInChildren<Animator>()`, поэтому ссылки в сцене не ломаются.

Проверка: `VerifyFactionsAndModels` — 80/80 без проблем, `FACTION Villagers=29 / GuildOfSuccess=30 / FreeTraders=21`. `AuditAllTradeNpcs` по всему проекту показывает 2 вне трёх фракций — `mira_01` (`GuildOfThoughts`) и `Zipun` (`GuildOfCreation`); они не входят в набор 80 и намеренно не тронуты.

Стартовая расстановка — сетка 10×8 с шагом 3 м восточнее Миры; каждая точка проверена по запечённому `NavMesh-WorldRoot_0_0.asset` и лежит на навмеше. Пользователь переносит их в магазины; `Wander 3 м` уже настроен и продолжит работать на новом месте, если агент стоит на NavMesh.

## Floating Origin — что важно

- 80 NPC — scene-placed `NetworkObject`, поэтому каждый получил маркер + запись каталога + observation (treatment `Unmanaged`, ownership `SceneOwnedNetworkGameplay`).
- Заодно в каталог были допущены два ранее незарегистрированных корня `probe-skillinput` и `probe-skills-state` (не NetworkObject, `AuthoredSceneContent`); иначе Host падал бы на `uncontrolled_network_source_before_native_sweep`.
- `QuestDatabase` не пересобирался целиком — массивы дополнены аддитивно.
- `Assets/_Project/Quests/Data/Npcs` в `.gitignore` (правило для массовой толпы/квестов), поэтому 80 новых `NpcDefinition` добавлены в git принудительно (`git add -f`), как и уже отслеживаемые именные NPC.

## Проверка (пользователь, Play Mode)

1. Compile: Console → 0 errors.
2. Запустить Bootstrap → WorldScene_0_0, найти группу `NPC_PRIMUM_MERCHANTS` рядом с Мирой.
3. Подойти к любому торговцу, `E` → слева диалог, справа магазин; купить/продать предмет.
4. Убедиться, что торговец ходит в радиусе ~3 м (после переноса в магазин — агент на NavMesh).
5. F8 → группа остаётся на месте, 0 errors; F9 → возврат.

## Замечания

- Предупреждения `Failed to create agent because it is not close enough to the NavMesh` в Edit Mode — следствие того, что данные `NavMeshSurface` не загружены в редакторе; при Play Mode поверхность грузится. Позиции группы при этом лежат на запечённом навмеше (проверено `NavMesh.SamplePosition` после `NavMesh.AddNavMeshData`).
- 80 полноценных humanoid-префабов в одной сцене тяжёлые для редактора; после переноса по магазинам группа станет равномернее.
- Генератор (идемпотентный): `Tools/MerchantPrimumn80/Generator.cs` — `Preview`, `CreateAssets`, `PlaceInScene`, `BakeFo`, `PreviewModels`, `ApplyFactionsAndModels`, `VerifyFactionsAndModels`, `AuditAllTradeNpcs`.
- FBX-модели лежат в `Assets/Generated_Models_Rigged_NPC/`, который в `.gitignore` (Lore VCS); в git коммитятся только сцены/ассеты/скрипты, ссылающиеся на них.
