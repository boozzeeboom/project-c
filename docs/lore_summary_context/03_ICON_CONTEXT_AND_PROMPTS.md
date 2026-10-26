# Project C: The Clouds — 2D-иконки: где применяются, полный реестр и промпты

**Каталог:** `docs/lore_summary_context/` · **Составлено:** 2026-10-26 · **Версия:** 1.0
**Назначение:** собрать в одном месте **все места в проекте, где используется 2D-картинка** (иконка предмета, навыка, фракции, метки карты, глифа UI), дать **полный реестр групп**, форматы и **готовые промпты** для генерации.
**Опора:** `ART_STYLE_MANIFEST.md` §2 (палитра), §5 (технологическая грамматика), §6 (язык поверхностей), `ART_BIBLE.md` §7 (иконки и UI), плюс **фактический код и ассеты проекта** (см. §0).

> Читать вместе с: `ART_STYLE_MANIFEST.md`, `02_MODULAR_CLOTHING.md`, `docs/ART_BIBLE.md` §7, `docs/Materials/README.md`.

---

## 0. Что проверено в проекте (evidence)

| Факт | Где проверено |
|---|---|
| Поле иконки у предмета — `Sprite icon` | `Assets/_Project/Scripts/Core/ItemType.cs:30` (`ItemData`) |
| Поле иконки у трейд-товара — `Sprite icon` | `Assets/_Project/Trade/Scripts/TradeItemDefinition.cs:16` |
| Поле иконки у навыка — `Sprite icon` | `Assets/_Project/Scripts/Skills/SkillNodeConfig.cs:102` |
| Иконка фракции — `Sprite iconSprite`, комментарий «**24x24, для UI списков**» | `Assets/_Project/Quests/Factions/FactionDefinition.cs:80-81` |
| Иконка рецепта — `Sprite _icon`, комментарий «**спрайт, 64x64**» | `Assets/_Project/Scripts/Crafting/RecipeData.cs:93-94` |
| Иконка станции крафта — `Sprite _icon` | `Assets/_Project/Scripts/Crafting/CraftingStationConfig.cs:43-44` |
| **Ни у одного предмета иконки нет** — во всех `.asset` строка `icon: {fileID: 0}` | grep по `Assets/_Project/Resources/Items/**/*.asset` |
| **Папка под иконки пуста** | `Assets/_Project/Trade/Icons/` → «No files found» |
| Объём инвентарной БД: **1004 строки** предметов (блок `# block=inventory`, строки 6–1009) + **108 трейд-товаров** (`# block=tradeItems`, 1013–1120) | `Assets/_Project/Resources/_docs/Resources_Import_v3.csv` |
| Иконки в CSV **не переносятся** — бинарный ассет живёт только в `.asset` | `Assets/_Project/Resources/_docs/Resources_Import_Schema.md:29` |
| Требование к иконкам инвентаря: **128×128, PNG с прозрачностью, flat/minimal, мягкие градиенты, тень, контур** | `docs/ART_BIBLE.md` §7.1 (строки 266–276) |
| Нет системы статус-эффектов (баффов/дебаффов) и нет системы достижений | grep `Buff/StatusEffect/Achievement` по `Assets/_Project/Scripts` — только внутренний `VengeanceMemory` AI-бафф |
| **Нет HUD-панели скиллов** (`SkillBar`/`Hotbar`) — но есть 6 биндинг-слотов | `SkillInputService.cs:39-48` (`Primary`, `Secondary`, `Slot1..4`) |

**Вывод:** 2D-арт в проекте сейчас **отсутствует полностью**, при этом код уже размечен под иконки в 7 подсистемах. Это не «добавить пару картинок», это **библиотека ~1300+ иконок**.

---

## 1. Полный реестр: где вообще может стоять 2D-картинка

| № | Подсистема | Куда попадает | Формат (по коду/доке) | Единиц |
|---|---|---|---|---|
| 1 | **Инвентарь** | `ItemData.icon` → `CharacterWindow` (список/детали), `InventoryWheel` (сублист), `Cargo`, `MarketWindow` (строки) | 128×128 PNG | **≈1004** |
| 2 | **Трейд / рынок** | `TradeItemDefinition.icon` → строки Buy/Sell/Contracts | 128×128 (реюз п.1) | 108 |
| 3 | **Корабельный груз** | те же иконки предметов + флаги `isDangerous/isFragile/isContraband` | 128 + 24×24 бейджи | 108 + 3 |
| 4 | **Навыки (combat)** | `SkillNodeConfig.icon` → `SkillTreeWindow` (узлы, tooltip, detail), слоты биндов | 64–128 | ~45 узлов (в проекте 30 `.asset`) |
| 5 | **Навыки (social)** | `SocialSkillTreeWindow` (тот же UXML), колонка «Социальные навыки» | 64–128 | 4 (+рост) |
| 6 | **Знания** | `CharacterWindow → ЗНАНИЯ`: 4 списка (Фракции / NPC / Навыки / Рецепты) | 24–64 | зависит от п.1/4/7/8 |
| 7 | **Фракции** | `FactionDefinition.iconSprite` → список знаний, репутация, диалоги | **24×24** | 15 (enum) / 10 `.asset` |
| 8 | **Крафт: рецепты** | `RecipeData._icon` → `CraftingWindow` список рецептов | **64×64** | = числу рецептов (сейчас 5) |
| 9 | **Крафт: станции** | `CraftingStationConfig._icon` → заголовок/список станций | 64–128 | 5 типов |
| 10 | **Квесты** | Журнал `CharacterWindow → КВЕСТЫ`, трекер, тосты | 24–32 (тип цели) | 8 типов + состояния |
| 11 | **Карта / чарт** | `ChartMarkType` → метки на карте, подписи, пеленг | 24–32 | 4 типа + POI |
| 12 | **Корабль** | модули в слотах, состояние корпуса, стыковка, топливо/груз | 32–64 | 4 слота + 4 типа + статусы |
| 13 | **Бой** | типы урона (`DamageType`), цифры урона, маркер цели | 24–32 | 5 типов |
| 14 | **NPC / социалка** | эмоции (`NpcEmotion`), сервисы (`NpcService`), портреты в диалоге | 24–64 | 6 + 6 |
| 15 | **UI-хром** | табы CharacterWindow (6), секторы колеса инвентаря (8), глифы клавиш, ESC-меню, главное меню | 16–32 | ~30 |
| 16 | **Спец-предметы лора** | МНП/сколы, старт-ключ, медальон-ключ, пломба (plumb), документы, печати | 128 | ~20 семейств |

**Итого:** ≈ **1004 иконки предметов** + ≈ **120 прочих** (навыки/фракции/рецепты/цели/метки/UI) ≈ **1300+ уникальных изображений**.

---

## 2. Форматы и технические требования

| Параметр | Значение | Источник |
|---|---|---|
| Базовый размер иконки предмета | **128×128** | `ART_BIBLE.md` §7.1 |
| Формат | **PNG, RGBA, прозрачный фон** | там же |
| Стиль | flat/minimal, мягкие градиенты, тень, читаемый контур; **не** casual | там же |
| Иконка рецепта | **64×64** | `RecipeData.cs:93` |
| Иконка фракции / строка списка | **24×24** | `FactionDefinition.cs:80`, `sub_inventory-tab/10_DESIGN.md:79,130` |
| Сектор колеса инвентаря | ~64×64 (крупный глиф на сектор) | `InventoryWheel.uxml` |
| Узел скилл-дерева | 64–128 (в узле + tooltip) | `SkillTreeWindow` / `SkillNodeConfig.icon` |
| Бейдж-индикатор (danger/fragile/contraband) | 16–24 | `TradeItemDefinition.cs:32-40` |
| Импорт в Unity | Texture Type = **Sprite (2D and UI)**, Alpha Is Transparency = on, Mip Maps = off, Max Size 128 (256 для UI-глифов), Compression = None/High Quality | стандарт URP 2D-спрайтов |
| Атлас | Иконки одной категории — в один атлас (смена текстур рвёт батчи UI) | `docs/UI/cloud-light-interactive-research-2026-09-29.md:225` |
| Именование файла | = имени ассета/`itemId` (см. §6) | — |

**Правило композиции иконки:** один объект, центр кадра, поворот 3/4 (для объёмных), мягкий ключевой свет **сверху-слева**, тонкий тёмный контур, лёгкая падающая тень **внутри** объекта (не на «пол»), никакого фона, никаких подписей и размерных линий.

---

## 3. Палитра и правила, обязательные для иконок

Из `ART_STYLE_MANIFEST.md` §2, применённые к иконке (маленький размер = жёсткая дисциплина цвета):

1. **Свечение = функция, не декор.** В иконке допустимы только три свечения:
   - `#4fc3f7` — **антигравий** (техника, сплавы, ядро);
   - `#66bb6a` — **мезий** (топливо, баллоны, опасность);
   - `#b366ff` — **Завеса** (в иконках предметов почти не встречается, только если предмет буквально из Завесы).
2. **Цвета гильдий не светятся.** Оранжевый/зелёный/красный/фиолетовый/жёлтый — только ткань, эмаль знака, флаг (`ART_STYLE_MANIFEST` §2.2).
3. **Материал читается фактурой, а не фото:** клёпка, сварной шов, патина, ржавчина в стыках, потёртые кромки (`docs/Materials/README.md`).
4. **Свет:** тёплый ключевой сверху, холодный контровой снизу (даже в иконке — даёт «наш» объём).
5. **Запреты:** магия/руны/кристаллы-магия, неон/киберпанк, хром-импланты, плазма/энергомечи, «стерильный белый sci-fi», фотореализм, текст на иконке.

---

## 4. Реестр по группам (что именно рисовать)

### 4.1 Инвентарь — 9 типов `ItemType`

`ItemType` (`Assets/_Project/Scripts/Core/ItemType.cs:5-20`): `Resources, Equipment, Food, Fuel, Antigrav, Meziy, Medical, Tech, Key`.
Колесо инвентаря показывает **8 секторов** (без `Key`): РЕСУРСЫ / ВЛАДЕНИЕ / ЕДА / ТОПЛИВО / АНТИГРАВИЙ / МЕЗИЙ / МЕДИКАМЕНТЫ / ТЕХНИКА (`InventoryWheel.uxml:44-67`).

Кроме того нужен **глиф категории** для каждого сектора (8 иконок) — он же используется в фильтрах.

| Сектор (`ItemType`) | ≈ шт. | Что внутри (примеры реальных ассетов) | Визуальный код иконки |
|---|---|---|---|
| `Antigrav` | 15 | `Antigrav`, `Purified_Antigrav_AG-1`, `Rod_Antigrav`, `Sheet_Antigrav`, `Antigrav_Cable`, `Carbon-Antigrav_Composite`, `Antigrav_Steel_AGS-40`, `Antigrav_Bronze_AGB-12`, `Antigrav_Brass_AGL-8`, `Antigrav_Concrete`, `Antigrav_Chips` | тёмный графит/навы с **голубым** свечением в зазорах, керамические изоляторы, клёпаные зажимы |
| `Meziy` | 6 | `Mezium`, `Raw_Mezium`, `Stabilized_Mezium_M-1`, `Mezium_Catalyst_M-K-01`, `Anti-Mezium_Serum` | стеклянная канистра/ампула, **зелёное** кипение, латунные вентили, копоть сверху |
| `Fuel` | 7 | `Mezium_Fuel_Cylinder`, `Refined_Mezium_Fuel`, `Kerosene_Oil_Lamp`, `Kerosene_Lantern_Bat`, `Lighter_Spark`, `Oxygen_Cylinder` | баллон/бак, предупреждающая полоса, манометр; мезий — зелёный |
| `Medical` | 45 | `Roll_Bandage`, `Sterile_Gauze_Bandage`, `Surgical_Scalpel`, `Glass_Syringe_5ml`, `Onboard_First_Aid_Kit`, `Mezium_Antidote_M-7`, `Tourniquet`, `Mercury_Thermometer`, `Stethoscope` | белое стекло/эмаль, зелёная эмаль Созидания, латунные футляры, чернильные штампы |
| `Resources` | ~66 | `Железный_слиток`, `Медный_слиток`, `Brass_Sheet`, `Galvanized_Sheet_Steel`, `Teak_Beam`, `Aviation_Plywood`, `Farm_Timber`, `Cord_Nylon`, `Steel_Cable_6mm`, `Borosilicate_Glass`, `Pressed_Cork`, `Skol_5__Bronze_` … `Skol_50__Gold_`, `Говно` | сырьё: слитки, листы, брус, мотки, мешки; валютные сколы — **плоские жетоны** |
| `Equipment` | ~500 | **смешанный бакет:** одежда (`Flight_Jacket_Primium_Standard`, `Fur_Ushanka`, `Woolen_Gloves`), инструмент (`Adjustable_Wrench`, `Hacksaw`, `Bench_Vise`), оружие (`Rifle_GS-1_Export`, `Pistol_Hoffman-9K`, `Hunting_Crossbow`, `Assault_Machete`), документы (`NP_Citizen_Passport`, `Weapons_Permit`, `Visa_Card`, `Cargo_Manifest`), посуда (`Enamel_Mug_0_3L`, `Cast_Iron_Pot`), мебель (`Folding_Chair_Pilot`, `Narrow_Wardrobe`) | **требует подгрупп** — см. §4.1.1 |
| `Food` | ~250 | `Rye_Bread_Primium`, `Field_Borscht`, `Primium_Dumplings`, `Dark_Chocolate_Himalayan`, `Beer_Primium_Stille`, `Vodka_Primium_Pure`, `Apple_Jam`, `Hard_Mountain_Cheese` | тёплая еда: хлеб/посуда/бутыль; холодная тара: стекло, жесть, этикетка с печатью |
| `Tech` | ~100 | `MAG__Mezium-Antigrav_Generator_`, `GMAG-7`, `Industrial_MAG-5`, `Bourdon_Tube_Pressure_Gauge`, `Gyrocompass_North-1`, `Portable_SOL_Terminal`, `MNP_Coin_0_5`, `MAG_Startup_Manual` | тяжёлый кожух, патрубки, **стрелочные приборы**, латунь, изоляторы; ядро — голубое |
| `Key` | — | `KeyRod` (ключ-стержень корабля) | зелёный стержень-лампа (лор: `ART_STYLE_MANIFEST` §5) |

#### 4.1.1 `Equipment` → разбить на визуальные подгруппы (иначе 500 иконок будут «кашей»)

Рекомендуемые подсемейства (по фактическим именам ассетов):

1. **Одежда/обувь/головное** (`Flight_*`, `Fur_*`, `Woolen_*`, `Hat_*`, `Cap`, `Boots`, `Shoes`, `Tailcoat`, `Peacoat`, `Tunic`, `Apron`) — ≈120.
2. **Инструмент ручной** (`Wrench`, `Pliers`, `Chisel`, `File`, `Hacksaw`, `Riveter`, `Punch`, `Tap`, `Reamer`, `Vise`, `Clamp`) — ≈90.
3. **Оружие и боеприпасы** (`Rifle`, `Pistol`, `Revolver`, `SG-MG`, `SMG`, `Sniper`, `Shotgun`, `Ammo_*`, `Bayonet`, `Machete`, `Baton-Whip`, `Brass_Knuckles`) — ≈40.
4. **Документы и бумаги** (`Passport`, `Permit`, `Visa`, `Manifest`, `Certificate`, `Receipt`, `Invoice`, `Summons`, `Warrant`, `Timesheet`, `Schedule`, `Log`) — ≈60. **Правило: плоский лист с печатью/подписью, без читаемого текста.**
5. **Посуда и кухня** (`Mug`, `Pot`, `Pan`, `Plate`, `Bowl`, `Colander`, `Ladle`, `Kettle`, `Teapot`) — ≈50.
6. **Бельё/текстиль/быт** (`Blanket`, `Mattress`, `Pillow`, `Sheet`, `Towel`, `Napkin`, `Tablecloth`, `Clothespin`) — ≈40.
7. **Приборы и связь** (`Binoculars`, `Telescope`, `Barograph`, `Stopwatch`, `Chronometer`, `Compass`, `Telephone`, `Radio`) — ≈40.
8. **Груз и такелаж** (`Cargo_Net`, `Sling`, `Ramp`, `Lashing_Strap`, `Carabiner`, `Pulley`, `Winch`, `Hawse`, `Cleat`) — ≈40.
9. **Мебель/стационар** (`Wardrobe`, `Shelf`, `Bulletin_Board`, `Doormat`, `Awning`) — ≈25.
10. **Гигиена/бытовая химия** (`Soap`, `Tooth_Powder`, `Shaving_Brush`, `Shoe_Polish`, `Laundry_Soap`) — ≈25.

### 4.2 Оружие — `WeaponClass` (9) × `WeaponHandling` (4)

`WeaponClass` (`WeaponItemData.cs:45-56`): `Sword, Dagger, Spear, Mace, Crossbow, Pneumatic, AntigravBlade, MesiumRifle, Throwable`.
`WeaponHandling` (`WeaponItemData.cs:33-39`): `Melee, Ranged, Thrown, Placed`.

**Уже смоделированные семейства (готовые референсы для иконок — 4 линии × 9 классов):**
`CL_F_WPN_SWORD_0X`, `CL_F_WPN_GSWORD_0X`, `CL_F_WPN_RAPIER_0X`, `CL_F_WPN_KNIFE_0X`, `CL_F_WPN_AXE_0X`, `CL_F_WPN_BOW_0X`, `CL_F_WPN_XBOW_01_STEEL`, `CL_F_WPN_PISTOL_0X`, `CL_F_WPN_CANE_0X`, где `0X` = `01_STEEL / 02_COMPOSITE / 03_BRONZE / 04_AGRAV`.

**Требования к иконке оружия:** вертикально, клинок/ствол вверх, рукоять внизу; либо 45° по диагонали (единообразно во всей серии); в кадре одно оружие; боеприпасы — `Throwable/Placed` (граната/мина) отдельным глифом.

### 4.3 Броня и одежда — `EquipSlot` (13) и `ModuleType` (4)

`EquipSlot` (`EquipSlot.cs:18-34`): `Head, Chest, Legs, Feet, Back, Hands, Accessory1, Accessory2, WeaponMain, WeaponOff, Module1..3`.
`ModuleItemData.ModuleType` (`ModuleItemData.cs:19-25`): `Sensor, Processor, Weapon, Utility`.

**Готовые линии брони (субъекты для иконок):**
`01_STEEL`, `02_COMPOSITE`, `03_BRONZE`, `04_AGRAV`, `05_MEDICAL`, `05_MEDICAL_FIELD`, `07_THOUGHTS_ENGINEER`, `09_SUPERHEAVY`, `10_TANK`, `11_RANGER`, `12_CHROME`, `13_RUNIC`, `14_KORU` × слоты (`ACC_PAULDRON / CHEST_PLATE / CHEST_LAMES / LEGS_BELT / LEGS_THIGH / LEGS_KNEE / FEET_BOOT / HANDS_GAUNTLET / HANDS_BRACER / HEAD_HELMET / BACKPACK / GADGET_* / AIRTANK`).
Плюс `CL_F_BACKPACK_05_MEDICAL … 08_RESEARCH`, `CL_F_BELT_05…08`, `CL_F_GADGET_GRAVHOOK_01 / MAGPACK_01 / MEZIMETER_01 / STABILIZER_01`.

**Правило:** иконка брони — **сама деталь на прозрачном фоне**, не «человек в броне» (иначе не читается в слоте 64 px). Исключение — полный сет (иконка комплекта).

### 4.4 Навыки

`SkillNodeConfig` (`Skills/SkillNodeConfig.cs`):
- `SkillCategory`: `Social = 0`, `Combat = 1`;
- `CombatDiscipline`: `None, Combat, Melee, Ranged, Defense, Placed`;
- `CombatSubtype`: `None, Throwables, Traps, Bows, Crossbows`;
- `KnowledgeUnlockType`: `Hidden, LearnFirst, Blueprint, NpcTeach, QuestReward, AlwaysVisible` (в UI — состояние «замок/неизвестно»);
- `isActive` → активный (в слот) / пассивный (только в дереве).

**Социальные навыки (4, `docs/Character/Social-skills/01_IMPLEMENTATION.md:31-38`):** `social_basic_talk` «Базовый разговор», `social_barter` «Торговля», `social_persuasion` «Убеждение», `social_leadership` «Лидерство».

**Боевые корневые (4):** `Skill_Combat_BasicStrike`, `DodgeRoll`, `HeavySwing`, `PrecisionStrike`.

**Ветки по дисциплинам (дизайн — `docs/Character/Skills/Battle/20_SKILL_TREES.md`):**
- **Melee** (7): `melee_basic_sword/dagger/spear/mace`, `melee_great_sword`, `melee_parry`, `melee_riposte`, `melee_precision_strike`.
- **Ranged** (6): `ranged_basic_crossbow`, `ranged_basic_pneumatic`, `ranged_aimed_shot`, `ranged_quick_reload`, `ranged_mesium_propulsion`, `ranged_mesium_mastery`.
- **Placed/Explosives** (5): `explosives_basic_grenade`, `mine_setting`, `charge_crafting`, `antigrav_mine`, `antigrav_grenade`.
- **Antigrav** (5): `antigrav_basic`, `blade_wield`, `hammer_wield`, `push`, `pull`, `aura`.
- **Defense**: `Defense_BasicArmor`, `HeavyArmor`, `Master`, `AntigravShield` (+ существующие `.asset`).
- **Фактические `.asset` в проекте (30):** `Skill_Antigrav_Aura/BasicPulse/Shield`, `Skill_Combat_BasicStrike/DodgeRoll/HeavySwing/PrecisionStrike`, `Skill_Defense_*`, `Skill_Explosives_BasicBomb/Grenade/Mine`, `Skill_Melee_BasicSword/DaggerMastery/DualWield/HeavySwing/PrecisionStrike/SpearReach`, `Skill_Ranged_BasicBow/CrossbowMastery/QuickReload`, `Skill_Social_*`.

**Где видно иконку:** узел `SkillTreeWindow` (обычный/изученный/недоступный/скрытый «?»), detail-панель, чипы фильтра по дисциплинам, слоты биндов `Primary / Secondary / Slot1..4` (`SkillInputService.cs:39-48`) — HUD-бара пока нет, но иконки под него нужны заранее.

**Цветовое кодирование дисциплин (рекомендация, не канон):**
Melee — сталь/красный акцент, Ranged — латунь/оливковый, Defense — синий-сталь, Placed — оранжевый, Antigrav — голубой `#4fc3f7`, Social — тёплый янтарь без свечения.
> Свечение в иконке навыка допустимо **только** для antigrav/мезий-навыков, иначе нарушается правило «свечение = функция».

### 4.5 Знания, рецепты, станции

- Вкладка **ЗНАНИЯ** имеет 4 списка: «Фракции», «Отношения к NPC», «Навыки», «Рецепты» (`CharacterWindow.uxml:172-184`) → нужны иконки строк и «неизвестно/замок».
- `RecipeCategory` (`RecipeData.cs:31-38`): `Module, Consumable, Ship, Material, Misc`.
- `RecipeKnowledgeUnlockType` (`RecipeData.cs:55-62`): `None, Blueprint, NpcTeach, QuestReward, Station`.
- `StationType` (`CraftingStationConfig.cs:27-34`): **`CraftingTable, Shipyard, Forge, Loom, Alchemy`**.
- `SkillType` (гейт) (`RecipeData.cs:41-48`): `None, Engineering, Piloting, Trading, Combat`.

### 4.6 Квесты

`QuestObjectiveType` (`Quests/Quests/QuestObjective.cs:15-25`) — **8 типов, каждому нужен свой глиф**:
`TalkToNpc, DeliverItem, ReachLocation, KillEntity, HaveItem, ReputationAtLeast, WaitForEvent, EventDriven`.
`QuestState`: `Discovered / Active / Completed / TurnedIn / Failed` — иконки статуса для журнала и тостов.
UI: `CharacterWindow → КВЕСТЫ` (фильтры Активные/Выполненные/Найденные), трекер, тост-уведомление.

### 4.7 Карта / чарт

`ChartMarkType` (`Scripts/World/ChartMarkManager.cs:8-14`) — **4 типа**:

| Тип | Глиф (по доку) |
|---|---|
| `Landmark` (ориентир) | кольцо |
| `Danger` (опасность) | треугольник |
| `Note` (заметка) | квадрат |
| `Destination` (цель) | двойное кольцо |

Плюс метки мира на карте: **город/пик**, **док-платформа**, **СОЛ-пост**, **маршрут парома**, **точка задания**, **свой корабль** (иконка баржи — `docs/Fun/SPEC.md:78`), **трек пути** (`ChartTrackSource`).

### 4.8 Корабль

- `ModuleSlot.SlotType` (`Ship/ModuleSlot.cs:8-14`): `Propulsion, Utility, Special, Engine`.
- `ShipModule.ModuleType` (`Ship/ShipModule.cs:10`): `Propulsion, …`.
- `HullState` (`Ship/Combat/ShipHull.cs:37`): `Operational / …` — иконка состояния корпуса.
- `DockingStatus` (`Docking/Dto/DockingDto.cs:104`) и `PadVisualState` (`DockPadVisualMarker.cs:6-10`, 7 значений) — глифы состояния пада.
- `AltitudeStatus` (`Ship/AltitudeCorridorData.cs:79`) — безопасно / выше / ниже коридора.
- `MeziyAxis` (`Ship/MeziyModuleActivator.cs:9`): `Pitch, Roll, Yaw, Thrust` — глифы осей мезиевых модулей.
- Топливо/груз — кастомные бары (`MyShipsTab`), нужны глифы **топливо / груз / масса**.

### 4.9 Бой и социалка

- `DamageType` (`Combat/Core/DamageType.cs:14-21`) — **5 типов**, каждому свой глиф:
  `Physical` (сталь/удар), `Ballistic` (пуля/болт), `Antigrav` (голубая g-волна), `Explosive` (оранжевый взрыв), `Mesium` (зелёное облако).
- `NpcEmotion` (`AI/NpcEmotion.cs:11-25`): `Calm, Alert, Fear, Anger, Despair, Victory`.
- `NpcService` (`Quests/Npcs/NpcDefinition.cs:44-53`, flags): `Trade, Repair, Refuel, Restock, Banking, Healing` — иконки услуг в диалоге/на карте.
- Цифры урона и маркер цели — отдельные изометрические спрайты (`DamageNumberConfig`, `110_DAMAGE_NUMBERS.md`).

### 4.10 UI-хром

- **Табы CharacterWindow (6):** ПЕРСОНАЖ / КОРАБЛЬ / ЗНАНИЯ / КОНТРАКТЫ / ИНВЕНТАРЬ / КВЕСТЫ (`CharacterWindow.uxml:26-31`).
- **Рынок (3 таба):** Buy / Sell / Contracts.
- **Колесо инвентаря (8 секторов)** + подписи.
- **Глифы клавиш** (WASD, E, F, TAB, ESC, 1–4, ЛКМ/ПКМ) — `ControlHintsUI` рисует подсказки текстом, но глифы клавиш нужны для «ненавязчивого» HUD.
- **ESC-меню:** Продолжить / Настройки (Управление / Графика / Звук / Геймплей) / Выход (`docs/UI/esc-menu/01_implementation_plan.md:44-57`).
- Прочее: `✕` закрытия, стрелки, «замок», «плюс/минус» репутации, индикаторы buy/sell-стрелок рынка.

### 4.11 Спец-предметы лора (обязательные иконки-«якоря» мира)

| Предмет | Иконка-канон |
|---|---|
| **МНП / сколы** (`Skol_5/10/25/50`, `Skollch`, `MNP_Coin_0_5`) | плоский удлинённый металлический жетон, край «расколот» (может делиться) |
| **Старт-ключ** (`ShipKey*`, `SOL_Key`) | зелёная лампа-стержень |
| **Медальон-ключ** | спиралевидный пурпурный медальон |
| **Пламб** (`Plumb`, `Plumb_Recorder`, `Colored_Plumb_Pencil`) | пишущий стержень из свинцового сплава |
| **MAG / GMAG** (`Industr._MAG-5`, `GMAG-7/9`) | тяжёлый кожух, патрубки, стрелочные приборы, голубое ядро |
| **СОЛ-карта/терминал** (`SOL_Distributor_Card`, `Portable_SOL_Terminal`) | гравитационный сканер, арка/экран, чёрная форма |
| **Печать/пломба** (`Wax_Seal_with_Emblem`, `Lead_Seal`, `Sealing_Wax_Stick`) | воск/свинец с оттиском, без читаемых букв |

---

## 5. Промпты

### 5.1 Стилевой блок для иконок (вставлять в каждый промпт)

```
STYLE: game UI item icon, single object centered, three-quarter view, isolated on transparent background,
stylized comic-book sci-fi look with a thin soft dark contour, simplified readable silhouette,
flat shading with soft gradients, one warm key light from upper-left and a cool rim from lower-right,
subtle ambient occlusion inside the object, slight drop shadow directly under the object, no floor, no cast shadow on ground,
steampunk + dieselpunk industrial materials: riveted steel plate, welded seam, brass and copper fittings,
pressure gauge, valve, gear, heavy bolts, rust streaks in joints, oxidized green patina on recesses,
worn bright edges on convex corners, granite, brick, wood grain, leather, canvas weave, glass with grime,
warm palette grounded in #4a4a4a steel, #8b5e3c rust, #5a7a6a patina, #7a6455 stone, #b08b52 brass,
glow grammar only where functional: cyan #4fc3f7 = antigravity, green #66bb6a = mesium,
nothing decorative glowing, no magic, no neon cyberpunk, no photorealism, no text, no labels
```

### 5.2 Негативный промпт (обязателен для иконок)

```
NEGATIVE: photorealistic, 3d render, ray-traced reflections, unreal engine screenshot, realistic photo,
busy background, floor, ground plane, cast shadow on ground, scenery, character, hands, multiple objects,
collage, sheet with several items, dimension lines, annotations, lettering, numbers, watermark, logo, signature,
cyberpunk neon, holograms, chrome cybernetic implants, white sterile plastic sci-fi, glowing magic runes,
mana, crystals with magic glow, energy sword, plasma, jet exhaust, medieval fantasy, grimdark horror, gore,
casual mobile game style, hyper-casual flat icons, anime, oversaturated neon green/pink, blurry, low contrast,
asymmetric broken silhouette, cropped object, object touching image edge
```

### 5.3 Параметрический шаблон (подставлять по группе)

```
[STYLE BLOCK]
SUBJECT: {один предмет, что именно, из чего, какое состояние/износ}
MATERIAL: {основной материал} + {второй материал} + {крепёж}
FUNCTION DETAIL: {одна читаемая функциональная деталь: заклёпки по углам / манометр / шкала / кольцо / ремень}
GLOW: {none | cyan #4fc3f7 in the core seams | green #66bb6a inside the glass}
COMPOSITION: {vertical / 45° diagonal / flat document sheet / stacked raw material}, centered, 10% margin to frame edge
[NEGATIVE]
```

### 5.4 Готовые промпты по группам предметов

**Ресурсы — слиток/лист/брус**
```
SUBJECT: a single stack of three weathered iron ingots, rough cast surface, chipped edges, faint rust bloom
MATERIAL: dark grey cast iron, matte, no shine; stamped mill mark pressed into the top face
GLOW: none
COMPOSITION: 45° diagonal stack, centered, 10% margin
```

**Ресурсы — текстиль/канат/кожа**
```
SUBJECT: a coiled roll of 12 mm hemp rope, tightly wound, frayed end, dust in the fibres
MATERIAL: natural hemp, canvas weave visible, faint tar stains
GLOW: none
COMPOSITION: coaxial coil seen at 3/4, centered
```

**Антигравий (главный визуальный код мира)**
```
SUBJECT: rectangular slab of cold graphite antigravity alloy, two plates hovering 8 mm apart,
held by three gunmetal field clamps, dark woven copper cable entering a capped white ceramic connector
MATERIAL: graphite-navy alloy, patina-free, machined edges, brass ferrules on the cable
GLOW: cyan #4fc3f7 only in the gap between the plates, soft, no bloom
COMPOSITION: 3/4 view, slight lift, centered
```

**Мезий**
```
SUBJECT: thick glass canister of liquid mesium, brass screw valve on top, hazard band around the body, soot on the upper third
MATERIAL: green-tinted borosilicate glass, brass collar, dark iron cage
GLOW: green #66bb6a from the liquid, boiling bubbles at room temperature, condensation on the glass
COMPOSITION: vertical, centered
```

**Топливо/баллон**
```
SUBJECT: squat steel fuel cylinder with a brass regulator and a mechanical pressure gauge, red warning stripe
MATERIAL: chipped olive paint over steel, rust at the welds, brass gauge face with a black needle
GLOW: none (gauge glass lightly reflective)
COMPOSITION: vertical, centered
```

**Медикамент**
```
SUBJECT: amber glass medicine bottle with a cork, a paper label with a wax seal, and two rolled gauze bandages beside it
MATERIAL: amber glass, cream paper, dark wax, cotton gauze
ACCENT: green enamel cross badge of the Guild of Creation, matte, not glowing
GLOW: none
COMPOSITION: small cluster, 3/4, centered
```

**Еда**
```
SUBJECT: a dark rye loaf on a worn wooden board with a linen cloth, one slice cut, crumbs
MATERIAL: cracked rye crust, flour dust, oak board, linen
GLOW: none
COMPOSITION: 3/4 top view, centered
```

**Напиток/бутыль**
```
SUBJECT: faceted glass bottle of dark beer with a stamped paper label and a crown cap, condensation
MATERIAL: dark green glass, damp paper label, brass cap
GLOW: none
COMPOSITION: vertical, centered
```

**Инструмент ручной**
```
SUBJECT: adjustable steel wrench, jaw slightly open, oiled leather grip wrap, one chipped chrome edge
MATERIAL: forged steel, black oxide, oiled leather, brass adjustment wheel
GLOW: none
COMPOSITION: 45° diagonal, head to upper right, centered
```

**Прибор/техника**
```
SUBJECT: portable MAG generator: riveted steel housing, two pipe unions, a round pressure gauge, a brass valve wheel,
and a caged core window
MATERIAL: dark steel plate with rivets, copper tubing, brass fittings, soot streaks upward
GLOW: cyan #4fc3f7 inside the caged window only
COMPOSITION: 3/4 view, centered
```

**Документ/бумага**
```
SUBJECT: a single sheet of yellowed official paper, one fold, a round purple ink stamp and a wax seal in the corner,
a ribbon on the edge, small torn notch
MATERIAL: aged paper, iron-gall ink stains, wax, faded silk ribbon
GLOW: none, no readable text, abstract ink lines only
COMPOSITION: flat sheet seen slightly from above with 10° tilt, centered
```

**Валюта (МНП)**
```
SUBJECT: an elongated flat metal coin-token with an engraved rim and one chipped end showing it can be split in two,
a second smaller half-token beside it
MATERIAL: worn bronze with bright polished edges, faint stamped numerals suggested as abstract marks
GLOW: none
COMPOSITION: two tokens, flat lay, slight 3/4 tilt, centered
```

**Ключ корабля**
```
SUBJECT: a slim metal key-rod with a green glass lamp head, ribbed grip, brass collar, worn serial band
MATERIAL: blued steel, brass, green glass
GLOW: green #66bb6a inside the lamp head, subtle
COMPOSITION: vertical, centered
```

**Оружие — ближний бой (линейка steel / composite / bronze / agrav)**
```
SUBJECT ({steel}): one-handed sword, straight single-edged blade with a fuller, crossguard of dark iron,
oiled leather grip, faceted steel pommel; edge worn bright, rust spots at the guard
SUBJECT ({composite}): one-handed sword with a carbon-fibre core, ceramic edge strip, exposed skeleton guard,
bolts, visible tang, matte black frame with brass fasteners
SUBJECT ({bronze}): cast bronze sword body with a raised spine, bronze rivets, attached steel edge that stops before the tip,
patina in the recesses, cast finger ridges
SUBJECT ({agrav}): one-handed sword whose blade is split into three passive suspended segments held by gunmetal clamps,
dark woven cable to a ceramic terminal, graphite-navy alloy, cyan glow only at the clamp seams
COMPOSITION: vertical, blade up, hilt down, centered
```

**Оружие — дальний бой / метательное**
```
SUBJECT: crossbow seen from a slight top-three-quarter angle, wooden stock, steel prod, cord, stirrup, top lock housing
GLOW: none
COMPOSITION: horizontal, prod to the left, stock to the right, centered
SUBJECT (throwable): dark iron fragmentation grenade with a brass fuse cap and a paper safety tag (no text)
COMPOSITION: 3/4, centered
```

**Броня/деталь слота**
```
SUBJECT: a single shoulder pauldron module: three overlapping cold blue-grey steel plates with rolled edges,
an inner steel ring and hook on the reverse, two leather attachment petals, a cotter pin
MATERIAL: steel plate, brass rivets, oiled leather backing, restrained rust at rivets and lower plate
GLOW: none
COMPOSITION: 3/4 exterior view, centered, sits on nothing, 10% margin
```

**Модуль (имплант-слот `Module1..3`)**
```
SUBJECT: a compact cylindrical sensor module, milled aluminium body, glass lens, brass contacts, ceramic isolator ring
MATERIAL: aluminum, glass, brass, ceramic
GLOW: faint cyan #4fc3f7 in the lens only
COMPOSITION: horizontal, 3/4, centered
```

### 5.5 Иконки навыков

**Combat skill (общий шаблон)**
```
[STYLE BLOCK]
SUBJECT: emblem-icon for the combat skill "{Название}": {главный жест/объект навыка, 1 предмет или 1 силуэт движения}
   примеры: {parry} two crossed blades at the moment of contact with a spark-free bright edge;
            {quick_reload} a crossbow bolt being seated into a lock housing, motion arc;
            {heavy_swing} a heavy blade with a wide angular motion arc and dust
MATERIAL: steel / brass / leather per discipline
GLOW: none, except antigrav skills (cyan seams) and mesium skills (green vapour)
FRAMING: no character, no face, no hands — only the tool and the motion read
COMPOSITION: centered, strong silhouette, readable at 64 px
```

**Social skill (отдельный визуальный язык — без оружия)**
```
SUBJECT: emblem-icon for the social skill "{Название}": {объект}
   {basic_talk} two facing speech plaques, brass rivets, no letters;
   {barter} two counter-balanced brass scale pans over a coin-token;
   {persuasion} an open palm over a sealed wax letter;
   {leadership} three upright pennants on one staff, guild cloth, no glow
MATERIAL: warm amber brass, wool cloth, paper, wax — no steel edge, no glow
COMPOSITION: centered, readable at 64 px
```

**Состояния узла в дереве (накладывать пост-обработкой, не генерировать заново):**
изучен = полный цвет + латунная рамка; доступен = полный цвет; недоступен = 55% насыщенности; скрыт = силуэт «?» в рамке.

### 5.6 Эмблемы фракций (24×24)

`FactionId`: `GuildOfThoughts` (оранжевый), `GuildOfCreation` (зелёный), `GuildOfStrength` (красный), `GuildOfSecrets` (фиолетовый), `GuildOfSuccess` (жёлтый), `Underground`, `Resistance`, `FreeTraders`, `SOL_Patrol`, `Pirates`, `Neutral`, `Bandits`, `Cultists`, `Guards`, `Villagers`.

```
SUBJECT: flat heraldic emblem for "{Фракция}", {символ по сфере: gear+compass / retort+leaf / shield+fist / key+eye / coin+scales},
inside a simple geometric badge shape, {цвет гильдии} enamel on dark steel
MATERIAL: enamel badge, brass rim, chipped paint at edges — NO glow, no letters, no heraldic animals
COMPOSITION: flat, centered, legible at 24 px, thick shapes only, no fine hatching
```

### 5.7 Иконки типов урона (5)

```
SUBJECT ({Physical}): a flat steel impact wedge with a cracked edge;
{ Ballistic}: a lead bullet with a rifling trace and a small trail;
{Antigrav}: three concentric cyan #4fc3f7 wave arcs around a graphite plate;
{Explosive}: a black-powder blast star with soot and a brass fuse cap;
{Mesium}: a green #66bb6a vapour cloud inside a cracked glass ampoule
STYLE: same icon style block, single symbol, no background, legible at 32 px
```

### 5.8 Карта, квесты, крафт, корабль

```
MAP MARK ({Landmark}): thin brass ring with a small granite peak inside, flat top view
MAP MARK ({Danger}): flat warning triangle, rust-orange enamel, dark rim
MAP MARK ({Note}): small folded paper square with a wax pin, top view
MAP MARK ({Destination}): double concentric ring, brass with a dark centre, flat top view

QUEST OBJECTIVE ({TalkToNpc}): two speech plaques; ({DeliverItem}): crate with a ribboned parcel;
({ReachLocation}): a stepped granite arch; ({KillEntity}): a broken blade over a rust stain;
({HaveItem}): an open hand holding a stack of ingots; ({ReputationAtLeast}): a brass laurel around a badge;
({WaitForEvent}): an hourglass with iron frame; ({EventDriven}): a signal bell with motion arcs
STYLE: same block, single flat symbol, 24–32 px legible

RECIPE ICON (64×64): {output item} shown small, plus one tool glyph in the corner (hammer / needle / retort / saw)
STATION ICON: {CraftingTable} heavy oak bench with steel vise and tool rack; {Shipyard} gantry crane over a hull rib;
{Forge} stone forge with a leather bellows and a glowing (orange, non-magical) coal bed;
{Loom} wooden loom with stretched wool warp; {Alchemy} copper retort and glass condensers on a stone bench

SHIP MODULE SLOT ({Propulsion}): wind-blade rotor in a protective grille; ({Utility}): cable drum and winch;
({Special}): brass console with a gauge; ({Engine}): engine bell with a cyan antigrav ring
SHIP STATUS: hull (plate with a crack), fuel (canister gauge), cargo (crate stack + weight mark),
dock (pad ring with a lock), altitude (vertical corridor band with a marker)
```

### 5.9 Глифы UI

```
SUBJECT: UI glyph set — {tab: character silhouette / ship side profile / open book / contract scroll / backpack / quest scroll},
{arrow up/down}, {check}, {cross}, {lock}, {plus}, {minus}, {magnifier}, {pin}, {gear}, {speaker}, {mouse}, {keyboard key cap}
STYLE: single-colour line+fill glyphs in #e0e0e0 with #4fc3f7 accent on the active state, 2 px visual stroke at 32 px,
no gradients on glyphs, no glow, no 3D, no text
COMPOSITION: grid of individual glyphs on transparent background, each glyph centered in its own square
```

---

## 6. Нейминг и структура папок (предложение)

```
Assets/_Project/UI/Icons/
  Items/
    Resources/<itemId>.png          # напр. Item_Resources_Железный_слиток.png
    Equipment/<подгруппа>/<itemId>.png
    Food/<itemId>.png
    Fuel/<itemId>.png
    Antigrav/<itemId>.png
    Meziy/<itemId>.png
    Medical/<itemId>.png
    Tech/<itemId>.png
    Key/<itemId>.png
  Categories/                        # 8 глифов секторов колеса
  Skills/Combat/<skillId>.png
  Skills/Social/<skillId>.png
  Factions/<FactionId>.png           # 24×24
  Recipes/<recipeId>.png             # 64×64
  Stations/<StationType>.png
  Quests/Objectives/<QuestObjectiveType>.png
  Quests/States/<QuestState>.png
  Map/<ChartMarkType>.png
  Combat/DamageTypes/<DamageType>.png
  Ship/<ModuleSlotType>.png
  UI/<glyphName>.png
```

Правила:
- имя файла = `itemId`/`skillId`/`enum`-имя (латиница, `snake_case` там, где id уже snake_case);
- русские имена ассетов сохранять как есть (`Item_Resources_Железный_слиток`) — проект уже так живёт;
- **не переименовывать** существующие `.asset` — только вписывать `Sprite` в поле `icon`.

---

## 7. Объём и очерёдность (реалистичные батчи)

| Батч | Что | ≈ иконок | Зачем первым |
|---|---|---|---|
| **B1** | 8 глифов категорий + 15 фракций + 5 типов урона + 4 метки карты + 8 типов квест-целей | ~40 | мгновенно «оживляет» весь UI, дёшево, задаёт стиль |
| **B2** | Оружие и броня по готовым 3D-линиям (`CL_F_WPN_*`, `CL_F_*` слоты) | ~120 | уже есть референсы → консистентность 1:1 с моделью |
| **B3** | Навыки (30 `.asset` + 4 social) | ~35 | закрывает `SkillTreeWindow` |
| **B4** | Ресурсы + антигравий + мезий + топливо + медикаменты | ~140 | крафт и рынок становятся читаемыми |
| **B5** | Инструмент + приборы + документы + посуда (подгруппы `Equipment`) | ~250 | основная масса |
| **B6** | Еда/напитки | ~250 | самая «вкусная» часть, но не блокирует |
| **B7** | Одежда/обувь/бытовой текстиль | ~160 | можно вариациями по шаблону |
| **B8** | UI-хром, глифы клавиш, ESC-меню | ~30 | полировка |

**Итого ~1000–1300.** Практический приём: для однотипных семейств (ложки/вилки/плошки, носки/перчатки, гайки/болты) генерировать **один мастер-промпт + 3–5 вариаций силуэта**, а не уникальную сцену на каждый предмет.

---

## 8. Чек-лист приёмки иконки

- [ ] 128×128 (или 64/24 по назначению), PNG RGBA, **фон полностью прозрачен**, нет «пола» и тени на земле.
- [ ] Один объект, силуэт не касается краёв, поля ≥ 8–10%.
- [ ] Читается **на 64 px** (уменьшить и посмотреть на 100%): силуэт ясен, без мелкой штриховки.
- [ ] Свечение только по правилу: голубой = антигравий, зелёный = мезий; гильдейские цвета **не светятся**.
- [ ] Материал читается фактурой (клёпка/шов/патина/ржавчина в стыках), а не фотореализмом.
- [ ] Нет магии, рун, неона, энергомечей, хрома-имплантов, «стерильного белого sci-fi».
- [ ] Нет текста, цифр, подписей, размерных линий, водяных знаков.
- [ ] Стиль совпадает с уже принятыми иконками серии (один ракурс, один свет, один контур).
- [ ] Для оружия/брони сохранена **линия материала** (`steel / composite / bronze / agrav`) — она видна в иконке.
- [ ] Файл назван по `itemId`/`skillId` и лежит в правильной папке.
- [ ] Sprite Import: Type = Sprite (2D and UI), Alpha Is Transparency, Mip Maps off, Max Size 128.

---

## 9. Что не подтверждено / открытые вопросы

1. **HUD-панель навыков не реализована** (`SkillBar`/`Hotbar` в коде отсутствует), но 6 слотов биндов уже есть → иконки слотов нужны «на вырост».
2. **Системы статус-эффектов нет** — баффы/дебаффы существуют только как внутренний AI-множитель (`VengeanceMemory`). Иконки статусов рисовать **не требуется** до появления системы.
3. **Достижений/коллекций нет** — отдельной иконной ветки не нужно.
4. **`Equipment` — свалка из 500 предметов** (одежда + инструмент + оружие + документы + мебель). Перед генерацией нужно либо разбить на подгруппы (§4.1.1), либо принять, что стиль будет «лоскутным».
5. **Точные счётчики по `ItemType`** требуют прогона по `Resources_Import_v3.csv` / `AssetDatabase` (Unity-сессия сейчас недоступна — цифры в §4.1 даны по фактическому listing папок и CSV).
6. **Иконки оружия vs существующие модели:** 3D-линии `CL_F_WPN_*` уже определены (4 линии), но у проекта нет **ни одной** 2D-иконки — первым делом стоит рисовать именно их, т.к. силуэт уже утверждён моделью.
7. **Цветовое кодирование дисциплин навыков** (§4.4) — рекомендация, не канон; требует утверждения вместе с палитрой UI.
8. **Иконка «полного сета» брони** (комплект одной картинкой) — решение не принято; в слотах нужны поштучные.
