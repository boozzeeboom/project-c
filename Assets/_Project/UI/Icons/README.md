# UI Icons — батчи B1 + B2 + B3 + B4 + B5 + B6

Каталог иконок HUD / инвентаря / экипировки / крафта / корабля / карты / квестов / боя / контрактов / сервисов NPC / добычи. **168 иконок: батч B1 (40) + батч B2 (34) + батч B3 (22) + батч B4 (25) + батч B5 (22) + батч B6 (25).**

Стиль: стимпанк + дизельпанк, западный комикс, тонкий тёмный контур, эмаль по тёмной стали, латунная окантовка, потёртости по кромкам.
Свечение только функциональное: антиграв — циан, мезий — зелёный, ядро техники — циан. Декоративного свечения нет.
Текст, буквы и цифры в иконках отсутствуют по требованию (никаких встроенных подписей).

## Структура

```
Assets/_Project/UI/Icons/
├── Categories/                  9  Icon_Cat_*           128 px  ItemType
├── Factions/                   15  Icon_Faction_*        32 px  FactionId (→24)
├── Factions/Relations/          3  Icon_Relation_*       64 px  FactionRelation
├── Combat/DamageTypes/          5  Icon_Damage_*         64 px  DamageType
├── Combat/Dice/                 6  Icon_Dice_*           64 px  DamageDice
├── Combat/Disciplines/          5  Icon_Discipline_*     64 px  CombatDiscipline
├── Map/                         4  Icon_Map_*            64 px  ChartMarkType
├── NPC/Services/                6  Icon_Service_*        64 px  NpcService
├── Quests/Objectives/           8  Icon_Quest_Obj_*      48 px  QuestObjectiveType
├── Quests/States/               6  Icon_QuestState_*     64 px  QuestState
├── Contracts/Types/             3  Icon_ContractType_*   64 px  ContractType
├── Contracts/Debt/              5  Icon_Debt_*           64 px  DebtLevel
├── Trade/Operations/            4  Icon_TradeOp_*        64 px  TradeOp
├── Equipment/Slots/            10  Icon_Slot_*           64 px  EquipSlot
├── Equipment/ModuleTypes/       4  Icon_ModuleType_*     64 px  ModuleItemData.ModuleType
├── Stats/                       3  Icon_Stat_*           64 px  StatType
├── Actions/                     3  Icon_Action_*         64 px  SkillInputSlot
├── Crafting/RecipeCategories/   5  Icon_Craft_*          64 px  RecipeCategory
├── Crafting/StationTypes/       5  Icon_Station_*        64 px  StationType
├── Ship/Crew/                   6  Icon_Crew_*           64 px  ShipCrewRole
├── Ship/Hull/                   2  Icon_Hull_*           64 px  HullState
├── Ship/Altitude/               5  Icon_Altitude_*       64 px  AltitudeStatus
├── Ship/Classes/                4  Icon_ShipClass_*      64 px  ShipClass / ShipFlightClass
├── Ship/ModuleSlots/            4  Icon_ShipSlot_*       64 px  SlotType
├── Ship/Status/                11  Icon_ShipStatus_*     64 px  NpcShipStatus
├── Skills/                      4  Icon_Skill_*          64 px  SkillType
├── Skills/Aoe/                  5  Icon_Aoe_*            64 px  AoeFormula
├── Skills/Categories/           2  Icon_SkillCat_*       64 px  SkillCategory
├── Weapons/Classes/             9  Icon_WeaponClass_*    64 px  WeaponClass
├── Gathering/Types/             3  Icon_Gather_*         64 px  GatherType
└── Knowledge/                   4  Icon_Know_*           64 px  RecipeKnowledgeUnlockType
```

Исходники сгенерированы в 1024×1024 RGBA с прозрачным фоном и перенесены в эти каталоги из `Assets/Generated_Images/`.
Каждый PNG пришёл вместе со своим `.meta`, поэтому GUID'ы **уже существуют** и стабильны — но импорт-настройки в них дефолтные (Texture, mipmaps **on**, Max Size 2048), а не целевые спрайтовые. То есть файл можно адресовать, но как `Sprite` он ещё не импортирован.

## Правила нэйминга

`Icon_<Домен>_<Имя>[_<Вариант>].png`

| Домен | Значение | Enum-источник | Члены |
|---|---|---|---|
| `Cat` | категория предмета | `ProjectC.Items.ItemType` | `Resources`, `Equipment`, `Food`, `Fuel`, `Antigrav`, `Meziy`, `Medical`, `Tech`, `Key` |
| `Faction` | эмблема факции | `ProjectC.Factions.FactionId` | 15 значений без `None` |
| `Damage` | тип урона | `ProjectC.Combat.Core.DamageType` | `Physical`, `Ballistic`, `Antigrav`, `Explosive`, `Mesium` |
| `Dice` | грань кости урона | `ProjectC.Combat.Core.DamageDice` | `d4`, `d6`, `d8`, `d10`, `d12`, `d20` |
| `Discipline` | боевая дисциплина | `ProjectC.Skills.CombatDiscipline` | `Combat`, `Melee`, `Ranged`, `Defense`, `Placed` |
| `Map` | метка карты | `ProjectC.World.ChartMarkType` | `Landmark`, `Danger`, `Note`, `Destination` |
| `Quest_Obj` | тип цели квеста | `QuestObjectiveType` | `TalkToNpc`, `DeliverItem`, `ReachLocation`, `KillEntity`, `HaveItem`, `ReputationAtLeast`, `WaitForEvent`, `EventDriven` |
| `QuestState` | состояние квеста | `ProjectC.Quests.QuestState` | `Discovered`, `Offered`, `Active`, `Completed`, `Failed`, `TurnedIn` |
| `ContractType` | тип контракта | `ProjectC.Trade.ContractType` | `Standard`, `Urgent`, `Receipt` |
| `Debt` | уровень долга | `ProjectC.Trade.DebtLevel` | `Warning`, `Restricted`, `Hunted`, `Bounty`, `Headhunt` (`None` без иконки) |
| `Slot` | слот экипировки | `ProjectC.Equipment.EquipSlot` | 10 глифов на 12 непустых значений |
| `Stat` | характеристика | `ProjectC.Stats.StatType` | `Strength`, `Dexterity`, `Intelligence` |
| `Action` | слот действия | `ProjectC.Skills.SkillInputSlot` | `Primary`, `Secondary` + один общий `HotbarSlot` |
| `Craft` | категория рецепта | `ProjectC.Crafting.RecipeCategory` | `Module`, `Consumable`, `Ship`, `Material`, `Misc` |
| `ShipSlot` | тип слота модуля | `ProjectC.Ship.SlotType` | `Propulsion`, `Utility`, `Special`, `Engine` |
| `ShipStatus` | состояние NPC-корабля | `ProjectC.PeacefulShip.Core.NpcShipStatus` | `Idle`, `Departing`, `InTransit`, `Approaching`, `Holding`, `Diverting`, `Docking`, `Docked`, `Loading`, `Undocking`, `Done` |
| `Skill` | навык | `ProjectC.Crafting.SkillType` | `Engineering`, `Piloting`, `Trading`, `Combat` |
| `SkillCat` | категория дерева навыков | `ProjectC.Skills.SkillCategory` | `Social`, `Combat` |
| `WeaponClass` | класс оружия | `ProjectC.Equipment.WeaponClass` | `Sword`, `Dagger`, `Spear`, `Mace`, `Crossbow`, `Pneumatic`, `AntigravBlade`, `MesiumRifle`, `Throwable` |
| `Know` | способ открытия знания | `ProjectC.Crafting.RecipeKnowledgeUnlockType` | `Blueprint`, `NpcTeach`, `QuestReward`, `Station` |
| `Service` | услуга NPC | `ProjectC.Quests.NpcService` (flags) | `Trade`, `Repair`, `Refuel`, `Restock`, `Banking`, `Healing` (`None` без иконки) |
| `Station` | тип станции крафта | `ProjectC.Crafting.StationType` | `CraftingTable`, `Shipyard`, `Forge`, `Loom`, `Alchemy` |
| `Crew` | роль члена экипажа | `ProjectC.PeacefulShip.Crew.ShipCrewRole` | `Pilot`, `Captain`, `Engineer`, `Gunner`, `Navigator`, `Passenger` |
| `Relation` | боевое отношение фракций | `ProjectC.Factions.FactionRelation` | `Allied`, `Neutral`, `Hostile` |
| `Hull` | состояние корпуса корабля | `ProjectC.Ship.Combat.HullState` | `Operational`, `Broken` |
| `Altitude` | статус высоты полёта | `ProjectC.Ship.AltitudeStatus` | `Safe`, `WarningLower`, `WarningUpper`, `DangerLower`, `DangerUpper` |
| `Aoe` | форма зоны навыка | `ProjectC.Skills.AoeFormula` | `SingleTarget`, `Cone`, `Sphere`, `Line`, `Box` |
| `ModuleType` | тип импланта | `ModuleItemData.ModuleType` | `Sensor`, `Processor`, `Weapon`, `Utility` |
| `ShipClass` | класс корабля | `Trade.Core.ShipClass` / `Player.ShipFlightClass` | `Light`, `Medium`, `Heavy` (=`HeavyI`), `HeavyII` |
| `TradeOp` | торговая операция | `ProjectC.Trade.TradeOp` | `Buy`, `Sell`, `LoadToShip`, `UnloadFromShip` |
| `Gather` | тип добычи | `ProjectC.ResourceNode.GatherType` | `Mining`, `Lambering`, `Gathering` |

Имена файлов == имена C# enum-членов (без префикса домена), чтобы маппинг был прямым без таблиц-исключений.
Значения `None` / `None = 0` иконок не имеют по определению (пустой слот, отсутствие факции, «всегда виден»).

## Настройки импорта (не применены)

Импорт в Unity **не выполнялся** — редактор в момент всех четырёх батчей был недоступен.

Целевые настройки для всех файлов:

- Texture Type: `Sprite (2D and UI)`
- Sprite Mode: `Single`
- Alpha Is Transparency: `on`
- Generate Mip Maps: `off` (UI рисуется в фиксированном размере)
- Wrap Mode: `Clamp`
- Compression: `None` (мелкие иконки; иначе полосы на градиентах)
- Max Size: по роли (см. таблицу выше), Filter Mode `Bilinear`
- Pixels Per Unit: `100`

## Статус

`GENERATED / NOT VERIFIED`. Импорт-настройки не применены (дефолтный Texture Importer), Max Size не выставлены, ни одна иконка не подключена к UI.
Не сгенерированы (обоснование в `docs/UI/ICONS.md`): `CombatSubtype` (designer-only), `WeaponHandling` (дублирует дисциплины), `WeaponClassMask` (flags-зеркало), `DockingStatus` (4 из 7 значений дублируют `NpcShipStatus`), `ContractState` (дублирует `QuestState`), `FactionAttitude` (три состояния = `FactionRelation`), `ShipCrewRespawnPolicy` (авторская политика, не UI), `PilotSeatType` (= `ShipCrewRole`), `ShipModule.ModuleType` (= `SlotType`), валюты и статусы (систем в коде нет).
Список конкретных иконок и замечаний по каждой — `docs/UI/ICONS.md`.

**Не сгенерировано (24 иконки, остановлено лимитом генератора изображений):** `Icon_Xp_*` (10, `XpSource`) → `Stats/XpSources/`, `Icon_BodyPreset_*` (5) → `Customisation/BodyPresets/`, `Icon_ViewDistance_*` (4) → `Settings/ViewDistance/`, `Icon_BodyType_*` (2) → `Customisation/BodyTypes/`, `Icon_Hair_*` (2) → `Customisation/HairStyles/`, `Icon_Know_LearnFirst` (1) → `Knowledge/`. Имена, домены и папки уже зафиксированы — нужна только повторная генерация, когда снимется `Image generation rate limit exceeded`.
