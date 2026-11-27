# Комплекты брони `02_Armor` — инвентаризация и план pickable items

> **Дата:** 2026-11-27 · **Тикет:** T-ARM01 (анализ)
> **Статус:** только анализ. Кода, ассетов, префабов и item-записей не создано.
>
> Источники: `Assets/Generated_Models/02_Armor`, `Assets/Generated_Models_Rebaked/02_Armor`,
> `docs/lore_summary_context/02_MODULAR_CLOTHING.md`, `Assets/_Project/Scripts/Equipment/*`,
> `Assets/_Project/Scripts/Core/PickupItem.cs`, `Assets/_Project/Items/*`,
> `Assets/_Project/Resources/Items/*`, `docs/Character/EquipmentVisual/02_CHARACTER_APPLIER.md`.
> Парные детали и зеркалирование: `docs/dev/ARMOR_MIRRORING_PAIRS.md`.

---

## 1. Коротко

| Что | Факт |
|---|---|
| Смоделировано | **93 FBX** в 10 категориях, 13 линий материалов/классов |
| Файлов в каталоге | ~331 (FBX + BaseColor + `.mat` + `_metadata.json`) — сходится с ретро v0.2.0 |
| Дубликаты арта | `Assets/Generated_Models_Rebaked/02_Armor` — те же **93** имени, `_Rebaked.fbx` + `_Rebaked_BaseColor.png` |
| Заведено как предметы | **0 из 93**. Ни одного `ItemData` / `ClothingItemData`, ни одного pickup-префаба |
| В `Resources/Items` | Ни одного ассета с `CL_F_*` / Pauldron / Gauntlet / Bracer |
| Готовность к игре | модели есть; data/visual/pickup/торговля — нет |

Генерация: Tripo `image_to_model`, `face_limit = 8000`, `pbr = false`, одна текстура BaseColor,
дата 2026-10-25 (по `_metadata.json`). То есть это не финальный PBR-ассет, а внятный прототипный меш.

---

## 2. Что именно лежит (полный список)

Все имена ниже — папки/модели с префиксом `CL_F_`; суффикс материала (`_STEEL`, `_COMPOSITE`, …)
у каждого имени свой.

**Head_Helmet — 4**
`HELMET_01_STEEL`, `HELMET_02_COMPOSITE`, `HELMET_03_BRONZE`, `HELMET_04_AGRAV`.

**Chest — 15**
`CHEST_PLATE_01_STEEL`, `02_COMPOSITE`, `03_BRONZE`, `04_AGRAV`, `05_MEDICAL`,
`05_MEDICAL_FIELD`, `07_THOUGHTS_ENGINEER_v2`, `09_SUPERHEAVY_v3b`, `10_TANK`, `11_RANGER`,
`12_CHROME`, `13_RUNIC`, `14_KORU`; плюс ламеллярные `CHEST_LAMES_09_SUPERHEAVY_v3b`, `CHEST_LAMES_10_TANK`.

**Shoulders — 13**
`ACC_PAULDRON_01`, `02_COMPOSITE`, `03_BRONZE`, `04_AGRAV`, `05_MEDICAL`, `05_MEDICAL_FIELD`,
`07_THOUGHTS_ENGINEER_v2`, `09_SUPERHEAVY_v3b`, `10_TANK`, `12_CHROME`, `13_RUNIC`, `14_KORU`;
плюс `ACC_MANTLE_11_RANGER`.

**Hands — 6**
`HANDS_GAUNTLET_09_SUPERHEAVY_v3b`, `HANDS_GAUNTLET_10_TANK`,
`HANDS_BRACER_11_RANGER_v2`, `12_CHROME`, `13_RUNIC`, `14_KORU`.

**Legs / Belt — 8**
`LEGS_BELT_01_STEEL`, `02_COMPOSITE`, `03_BRONZE`, `04_AGRAV`, `11_RANGER`, `12_CHROME`, `13_RUNIC`, `14_KORU`.

**Legs / Thigh — 13**
`LEGS_THIGH_01_STEEL`, `02_COMPOSITE`, `03_BRONZE`, `04_AGRAV`, `05_MEDICAL`, `05_MEDICAL_FIELD`,
`07_THOUGHTS_ENGINEER_v2`, `09_SUPERHEAVY_v3b`, `10_TANK`, `11_RANGER`, `12_CHROME`, `13_RUNIC`, `14_KORU`.

**Legs / Knee — 9**
`LEGS_KNEE_05_MEDICAL`, `05_MEDICAL_FIELD`, `07_THOUGHTS_ENGINEER_v2`, `09_SUPERHEAVY_v3b`,
`10_TANK`, `11_RANGER_v2`, `12_CHROME`, `13_RUNIC`, `14_KORU`.

**Feet — 13**
`FEET_BOOT_01_STEEL`, `03_COMPOSITE`, `04_BRONZE`, `05_AGRAV`, `05_MEDICAL`, `05_MEDICAL_FIELD`,
`07_THOUGHTS_ENGINEER_v2`, `09_SUPERHEAVY_v3b`, `10_TANK`, `11_RANGER`, `12_CHROME`, `13_RUNIC`, `14_KORU`.
(Ботинка `02_COMPOSITE` нет — композитный ботинок назван `03`.)

**Backpacks_Airtanks — 8**
`AIRTANK_01_STEEL`, `02_COMPOSITE`, `03_BRONZE`, `04_AGRAV`;
`BACKPACK_05_MEDICAL`, `06_CHEMICAL`, `07_ENGINEER`, `08_RESEARCH`.

**Utility_Belts — 4**
`BELT_05_MEDICAL`, `06_CHEMICAL`, `07_ENGINEER`, `08_RESEARCH`.

Итого: 4 + 15 + 13 + 6 + 8 + 13 + 9 + 13 + 8 + 4 = **93**.

---

## 3. Матрица покрытия по линиям

`B/T/K` = по отдельности пояс / набедренник / наколенник. `×2` — вариант `MEDICAL_FIELD`.
«Меш» — число моделей, «Предм.» — сколько pickable-предметов получится, если один предмет = один слот
(набедренник + наколенник + пояс одной линии упаковываются в один `Legs`-предмет, см. §6).

| Линия | Head | Chest | Shoulders | Hands | Legs B/T/K | Feet | Back | Utility | Меш | Предм. |
|---|:---:|:---:|:---:|:---:|:---:|:---:|:---:|:---:|---:|---:|
| 01 STEEL | ✔ | ✔ | ✔ | — | ✔ / ✔ / — | ✔ | ✔ | — | 7 | 6 |
| 02 COMPOSITE | ✔ | ✔ | ✔ | — | ✔ / ✔ / — | — | ✔ | — | 7 | 6 |
| 03 BRONZE | ✔ | ✔ | ✔ | — | ✔ / ✔ / — | ✔ | ✔ | — | 7 | 6 |
| 04 AGRAV | ✔ | ✔ | ✔ | — | ✔ / ✔ / — | ✔ | ✔ | — | 7 | 6 |
| 05 MEDICAL | — | ✔×2 | ✔×2 | — | — / ✔×2 / ✔×2 | ✔×2 | ✔ | ✔ | 12 | 10 |
| 06 CHEMICAL | — | — | — | — | — / — / — | — | ✔ | ✔ | 2 | 2 |
| 07 ENGINEER | — | ✔ | ✔ | — | — / ✔ / ✔ | ✔ | ✔ | ✔ | 7 | 6 |
| 08 RESEARCH | — | — | — | — | — / — / — | — | ✔ | ✔ | 2 | 2 |
| 09 SUPERHEAVY | — | ✔×2 | ✔ | ✔ | — / ✔ / ✔ | ✔ | — | — | 7 | 6 |
| 10 TANK | — | ✔×2 | ✔ | ✔ | — / ✔ / ✔ | ✔ | — | — | 7 | 6 |
| 11 RANGER | — | ✔ | ✔ | ✔ | ✔ / ✔ / ✔ | ✔ | — | — | 7 | 5 |
| 12 CHROME | — | ✔ | ✔ | ✔ | ✔ / ✔ / ✔ | ✔ | — | — | 7 | 5 |
| 13 RUNIC | — | ✔ | ✔ | ✔ | ✔ / ✔ / ✔ | ✔ | — | — | 7 | 5 |
| 14 KORU | — | ✔ | ✔ | ✔ | ✔ / ✔ / ✔ | ✔ | — | — | 7 | 5 |
| **Всего** | **4** | **15** | **13** | **6** | **8 / 13 / 9** | **13** | **8** | **4** | **93** | **76** |

Читается так:

- Полный «от шлема до ботинок» комплект есть только у **01–04** (6 предметов), и у них нет перчаток/наручей.
- **09–14** — боевые линии: есть руки, но нет шлема, пояса и спины.
- **05 MEDICAL** — единственная линия, где закрыты и грудь, и ноги, и плечи, и ступни, и спина (10 предметов).
- **06 CHEMICAL / 08 RESEARCH** — фактически только ранец + пояс. Комплектами их называть нельзя.
- **11–14** — самые «комплектные» из поздних: 5 предметов, но без головы и спины.

---

## 4. Что уже есть в коде под эти предметы

Проверено по живому проекту, не по докам (часть доков устарела).

**`ClothingItemData : ItemData`** (`Assets/_Project/Scripts/Equipment/ClothingItemData.cs`) — это целевой тип:
`slot`, `tier` (1–10), `strength/dexterity/intelligence Bonus` и `Multiplier`, **`armorDefense` (0–50)**,
`requiredSkills`, плюс из `ItemData`: `visualPrefab`, `attachBoneOverride`, `attachPositionOffset`,
`attachRotationOffset`, `attachScale`, `icon`, `weightKg`, `basePrice`, `maxStack`.

**Слоты** — `EquipSlot` (Head, Chest, Legs, Feet, Back, Hands, Accessory1/2, WeaponMain/Off, Module1..3),
`EquipmentData` — ровно **13 слотов, по одному предмету на слот**. Слоёв нет.

**Визуал** — `CharacterEquipmentVisualApplier` держит один `GameObject` на слот, вешает на кость из
`EquipSlotToBone`: Chest → Spine, Legs → Hips, Feet → **LeftFoot**, Hands → **LeftHand**,
Accessory1/2 → **Spine**. Переопределение кости и offset/scale — per-item поля.

**Pickup** — `PickupItem` требует только `ItemData` + trigger-коллайдер; если у него нет child-renderer,
он сам инстансит `itemData.visualPrefab` ребёнком (identity transform) и глушит коллайдеры.

**ID предметов** — `ItemRegistry` (явный список `id → ItemData`) — основной источник; при его отсутствии
`InventoryWorld`/`InventoryServer` делают fallback `Resources.LoadAll<ItemData>("Items")` с нумерацией
по алфавиту. Порядок записей менять нельзя.

**CSV-импорт** (`ResourcesCsvSchema`) умеет только `itemName / itemType / description / maxStack / weightKg`.
Задать `slot`, `visualPrefab`, `armorDefense`, `tier` через CSV нельзя.

**Бой** — `PlayerTarget.GetArmorDefense()` суммирует `armorDefense` только по слотам
**Head / Chest / Legs / Feet / Back**. Руки и аксессуары в защиту не попадают.

**Иконки** — есть только иконки слотов (`Icon_Slot_Head`, `…Chest`, `…Hands`, `…Accessory` и т.д.),
поштучных иконок брони нет.

**Плейсхолдеры одежды** — 3 ассета: `Clothing_MiningHelmet`, `Clothing_SteelToeBoots`,
`Clothing_WorkCoveralls` (в `Resources/Equipment/Clothing`), у всех `visualPrefab = null`,
`armorDefense = 0`; у шлема `slot = Head`, но `equipSlot = None` — дубль поля, читается `slot`.

---

## 5. Пробелы и ловушки

1. **Нет ни одного предмета.** 93 меша не связаны ни с инвентарём, ни с экипировкой, ни с пикапами.
2. **Один слот — один визуал.** `Legs` не может одновременно держать пояс, набедренник и наколенник,
   а `Chest` — обвязку и пластину. Модульность из `02_MODULAR_CLOTHING` движком сейчас не поддержана.
3. **Кости не совпадают с бронёй.** `Accessory1/2` висят на `Spine`, а наплечники должны сидеть на
   `LeftShoulder`/`RightShoulder`. `Hands` и `Feet` берут **левую** кость — парные меши лягут в одну сторону.
4. **Нумерация линий непоследовательна.** AGRAV — `04` везде, кроме ботинка (`05_AGRAV`); COMPOSITE —
   `02`, но ботинок `03_COMPOSITE`. Если строить itemId/имена по номеру линии, будет рассинхрон.
5. **Дырки в линиях.** Нет шлемов 05–14, рук 01–08, наколенников 01–04, поясов 05/07/09/10 в `Legs`,
   ботинка `02`. «Собрать полный сет» из большинства линий нельзя.
6. **Стабильность id.** Если просто положить 76 новых `ItemData` в `Resources/Items`, алфавитный
   fallback перенумерует существующие предметы → слетят сохранки, квесты, лут и стоки торговцев.
   Нужна явная дозапись в `ItemRegistry` (append-only).
7. **Два каталога арта.** `Generated_Models` и `Generated_Models_Rebaked` — одинаковые 93 имени.
   Если импортировать оба, получатся дубли моделей и материалов. Нужно выбрать один источник.
8. **Почти наверняка неполный `.mat`.** В части папок есть готовый `_Material.mat`, в части только
   `_BaseColor.jpg` — при сборке `visualPrefab` материал надо назначать единообразно.
9. **Нет иконок.** 76 предметов будут показываться иконкой слота — визуально неотличимы в инвентаре.
10. **`equipSlot` vs `slot`.** У `ClothingItemData` два поля слота. Движок читает `slot` (и `equipSlot`
    только для не-одежды). Если заполнить оба по-разному — UI и сервер разойдутся.
11. **Защита только с 5 слотов.** Наплечники, наручи и пояса не дадут `armorDefense`, даже если поле
    заполнено. Либо это осознанно «косметика», либо нужен отдельный тикет.
12. **Устаревший комментарий.** В `ItemType.cs` есть ссылка на `SetupEquipmentVisualAssets.cs` — такого
    скрипта в проекте нет. Не искать, не восстанавливать по комментарию.
13. **Все 93 модели нормализованы.** Максимальный габарит у каждой — ровно `0.998` (проверено по bounds
    всех FBX): абсолютного масштаба нет, каждому префабу нужен свой `attachScale`.
14. **Пары внутри мешей, а визуал — один на кость.** Часть мешей содержит две детали (`PAULDRON_09`,
    `THIGH_09/11`, `KNEE_13`, часть ботинок), а `CharacterEquipmentVisualApplier` вешает один
    GameObject на одну кость. Решение — `docs/dev/ARMOR_MIRRORING_PAIRS.md`.

---

## 6. Предлагаемая модель предметов (на утверждение)

### 6.1. Один предмет = один слот, визуал = склейка деталей

Это единственный вариант, который не требует менять `EquipmentData`, сеть и UI:

| Слот | Что входит в предмет | Пример |
|---|---|---|
| Head | шлем | `HELMET_01_STEEL` |
| Chest | нагрудник (PLATE; LAMES — либо отдельный айтем, либо альтернативный визуал) | `CHEST_PLATE_01_STEEL` |
| Legs | пояс + набедренник + наколенник одной линии в одном префабе | `BELT_01 + THIGH_01` |
| Feet | ботинок (пара в одном меше/префабе) | `BOOT_01_STEEL` |
| Hands | наруч/рукавица (пара) | `BRACER_11_RANGER_v2` |
| Accessory1 | один наплечник; на персонаже спавнится дважды (L/R, зеркало) | `PAULDRON_01` |
| Accessory2 | утилитарный пояс 05–08 | `BELT_05_MEDICAL` |
| Back | ранец/баллон | `AIRTANK_01_STEEL` |

Почему так: набедренники вынесены в `Legs` (как в `02_MODULAR_CLOTHING` §10.8). Наплечники занимают
один слот и спавнятся зеркально на обе кости, поэтому `Accessory2` остаётся свободным. Итог — **76**
предметов вместо 93, без изменения 13-слотового контракта.

Минусы, которые надо принять осознанно:

- нельзя смешивать пояс одной линии с набедренником другой;
- наплечники нельзя снять по одному;
- `Accessory2` занят поясом, свободных аксессуаров не остаётся;
- `armorDefense` рук/плеч/пояса в бою не учитывается (см. §5.11).

### 6.2. Альтернативы (дороже)

- **Поштучно** (93 предмета): нужны новые слоты (`ChestHarness`, `LegsBelt`, `ThighL`, `ThighR`,
  `KneeL`, `KneeR`, `ShoulderL`, `ShoulderR`) → правки `EquipSlot`, `EquipmentData` (13→N),
  сети, сохранок, UI. Большой тикет.
- **Предмет-комплект** (1 предмет = весь сет): нужен предмет, занимающий несколько слотов. Тоже
  правка `EquipmentData` + UI. Даёт красивые «сеты», но убивает микширование.

Рекомендация: **6.1**, поштучность — отдельным тикетом позже, если понадобится.

---

## 7. Что задавать каждому предмету (чек-лист)

- [ ] Тип ассета — `ClothingItemData` (не голый `ItemData`).
- [ ] `slot` — по таблице §6.1; `equipSlot` оставить `None`, чтобы не было двух истин.
- [ ] `itemName` — человекочитаемое, с линией и слотом (`Steel Chest Plate`, `Ranger Bracers`).
- [ ] `tier` 1–10 — по линии (01→1, 14→10 или по балансу, решить отдельно).
- [ ] `armorDefense` — по слоту и линии; 0 для чисто косметических.
- [ ] `weightKg`, `basePrice`, `maxStack = 1`.
- [ ] `icon` — пока `Icon_Slot_*`, позже поштучные.
- [ ] `visualPrefab` — собранный префаб (меш + материал, pivot в точке крепления, масштаб в метрах).
- [ ] Масштаб: все меши нормализованы к `0.998` — `attachScale` подбирается по реальному размеру детали.
- [ ] Парные меши: отделить одну половину, вторая получается зеркалом (`ARMOR_MIRRORING_PAIRS.md`).
- [ ] `attachBoneOverride` + `attachPositionOffset/Rotation/Scale` — обязательно для Accessory/Hands/Feet.
- [ ] Запись в `ItemRegistry` — **только в конец**, без перестановок.
- [ ] Один общий pickup-шаблон (`PickupItem` + trigger), `itemData` подставляется при спавне дропа.

Именование файлов (предложение): `Clothing_ARM_<NN>_<LINE>_<SLOT>.asset`, например
`Clothing_ARM_01_STEEL_CHEST.asset`. Префикс `Clothing_` сохраняет совместимость с текущим
`Resources/Equipment/Clothing` и меню `Project C/Equipment/Clothing`.

---

## 8. План этапов

| Тикет | Что делаем | Зависит от |
|---|---|---|
| **T-ARM01** | Этот документ: инвентаризация, матрица, риски | — |
| **T-ARM02** | Утвердить модель §6.1, карту слотов, имена, источник арта (`Generated_Models` **или** `Rebaked`) | T-ARM01 |
| **T-ARM03** | Собрать 76 `visualPrefab`: меш(и) + материал, pivot/scale, пары в один префаб | T-ARM02 |
| **T-ARM04** | Сгенерировать 76 `ClothingItemData` (идемпотентный editor-tool), дозаписать в `ItemRegistry`, поставить иконки-заглушки | T-ARM03 |
| **T-ARM05** | Парный спавн L/R (зеркало) для плеч/рук/ступней/бёдер + офсеты; проверка на персонаже | T-ARM04 |
| **T-ARM06** | Бой: включить Hands/Accessory в `GetArmorDefense()` **или** зафиксировать их как косметику; `ArmorClass`-гейт при появлении enum | T-ARM04 |
| **T-ARM07** | Экономика: стоки торговцев, цены, лут-таблицы, квестовые награды | T-ARM04 |

Вне рамок: новая генерация арта, слоистые слоты, «сет одним предметом», PBR-перегон.

---

## 9. Решения, которые нужны от заказчика

1. **Модель:** один предмет на слот со склейкой деталей (76) — или поштучно с расширением слотов (93+)?
2. **Источник арта:** оригиналы `Generated_Models` или `Rebaked`? (оба каталога полные, 93/93)
3. **Наплечники:** решено — один предмет, зеркальный спавн L/R (`ARMOR_MIRRORING_PAIRS.md` §1).
4. **Утилитарные пояса 05–08:** в `Accessory2` — или в `Legs` отдельным предметом?
5. **LAMES 09/10:** отдельные предметы — или альтернативный визуал к `PLATE` той же линии?
6. **Защита:** руки/плечи/пояс дают `armorDefense` — или остаются косметикой?
7. **Дырки:** добивать арт (шлемы 05–14, руки 01–08, наколенники 01–04, ботинок 02) — или жить с тем, что есть?
