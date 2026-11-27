# T-ARM04 — 76 предметов брони из decimated-арта

Дата: 2026-11-27. Скоуп: `Assets/Generated_Models_Rebaked_Decimated/02_Armor` (93 `*_Dec.fbx`).

## 1. Итог

- 93/93 FBX переимпортированы с `ModelImporter.globalScale = 100` (`useFileScale = true`), `maxDim` всех моделей в диапазоне **0.9511 … 1.0044** (блокер 0.010 снят).
- 76 `ClothingItemData` собраны и добавлены в `ItemRegistry` append-only (**id 2029 … 2104**, существующие записи не тронуты, всего 2104).
- 76 `visualPrefab` + 76 `equippedVisualPrefab` (`EquipmentMirrorMount`; парные — ровно 2 узла L/R).
- 76 материалов URP/Lit `M_ARM_<LINE>_<SLOT>.mat` (для составных `LEGS` — sub-materials Belt/Knee со своими текстурами).
- 53 зеркальных Mesh-ассета в `Assets/_Project/Generated/Armor/Mirror/` по п.2.3 (`x → −x`, обратный порядок треугольников, `normals.x → −x`, `tangents.x → −x`, `w` не тронут, RecalculateBounds).
- Общий `Assets/_Project/Prefabs/Items/Pickup_Armor.prefab` (`PickupItem` + `BoxCollider isTrigger` + `NetworkObject`).
- Превью-префаб `Assets/_Project/Prefabs/Equipment/Armor/_Preview/ArmorPreview.prefab` (18 манекенов `NetworkPlayer/Body`, все 76 `_Eq` по костям).
- 0 ошибок компиляции; Play Mode не запускался; сцены не создавались и не менялись.

## 2. Таблица 76 предметов

Столбцы: имя (asset) | слот | LINE | меши-источники | кости | зеркало | масштаб | id | статус.
`MIR` = узел с зеркальным Mesh (`x→−x`); `—` = непарный, один узел.

| # | Имя | Слот | LINE | Меши-источники | Кости | Зеркало | Масштаб | id | Статус |
|---|---|---|---|---|---|---|---|---|---|
| 1 | Clothing_ARM_01_STEEL_HEAD | Head | 01_STEEL | CL_F_HEAD_HELMET_01_STEEL | Head | — | 0.30 | 2029 | OK |
| 2 | Clothing_ARM_02_COMPOSITE_HEAD | Head | 02_COMPOSITE | CL_F_HEAD_HELMET_02_COMPOSITE | Head | — | 0.30 | 2030 | OK |
| 3 | Clothing_ARM_03_BRONZE_HEAD | Head | 03_BRONZE | CL_F_HEAD_HELMET_03_BRONZE | Head | — | 0.30 | 2031 | OK |
| 4 | Clothing_ARM_04_AGRAV_HEAD | Head | 04_AGRAV | CL_F_HEAD_HELMET_04_AGRAV | Head | — | 0.30 | 2032 | OK |
| 5 | Clothing_ARM_01_STEEL_CHEST | Chest | 01_STEEL | CL_F_CHEST_PLATE_01_STEEL | Spine | — | 0.55 | 2033 | OK |
| 6 | Clothing_ARM_02_COMPOSITE_CHEST | Chest | 02_COMPOSITE | CL_F_CHEST_PLATE_02_COMPOSITE | Spine | — | 0.55 | 2034 | OK |
| 7 | Clothing_ARM_03_BRONZE_CHEST | Chest | 03_BRONZE | CL_F_CHEST_PLATE_03_BRONZE | Spine | — | 0.55 | 2035 | OK |
| 8 | Clothing_ARM_04_AGRAV_CHEST | Chest | 04_AGRAV | CL_F_CHEST_PLATE_04_AGRAV | Spine | — | 0.55 | 2036 | OK |
| 9 | Clothing_ARM_05_MEDICAL_CHEST | Chest | 05_MEDICAL | CL_F_CHEST_PLATE_05_MEDICAL | Spine | — | 0.55 | 2037 | OK |
| 10 | Clothing_ARM_05_MEDICAL_FIELD_CHEST | Chest | 05_MEDICAL_FIELD | CL_F_CHEST_PLATE_05_MEDICAL_FIELD | Spine | — | 0.55 | 2038 | OK |
| 11 | Clothing_ARM_07_THOUGHTS_ENGINEER_CHEST | Chest | 07_THOUGHTS_ENGINEER | CL_F_CHEST_PLATE_07_THOUGHTS_ENGINEER_v2 | Spine | — | 0.55 | 2039 | OK |
| 12 | Clothing_ARM_09_SUPERHEAVY_CHEST | Chest | 09_SUPERHEAVY | CL_F_CHEST_PLATE_09_SUPERHEAVY_v3b | Spine | — | 0.55 | 2040 | OK |
| 13 | Clothing_ARM_09_SUPERHEAVY_LAMES_CHEST | Chest | 09_SUPERHEAVY_LAMES | CL_F_CHEST_LAMES_09_SUPERHEAVY_v3b | Spine | — | 0.55 | 2041 | OK |
| 14 | Clothing_ARM_10_TANK_CHEST | Chest | 10_TANK | CL_F_CHEST_PLATE_10_TANK | Spine | — | 0.55 | 2042 | OK |
| 15 | Clothing_ARM_10_TANK_LAMES_CHEST | Chest | 10_TANK_LAMES | CL_F_CHEST_LAMES_10_TANK | Spine | — | 0.55 | 2043 | OK, yaw 90° |
| 16 | Clothing_ARM_11_RANGER_CHEST | Chest | 11_RANGER | CL_F_CHEST_PLATE_11_RANGER | Spine | — | 0.55 | 2044 | OK |
| 17 | Clothing_ARM_12_CHROME_CHEST | Chest | 12_CHROME | CL_F_CHEST_PLATE_12_CHROME | Spine | — | 0.55 | 2045 | OK |
| 18 | Clothing_ARM_13_RUNIC_CHEST | Chest | 13_RUNIC | CL_F_CHEST_PLATE_13_RUNIC | Spine | — | 0.55 | 2046 | OK |
| 19 | Clothing_ARM_14_KORU_CHEST | Chest | 14_KORU | CL_F_CHEST_PLATE_14_KORU | Spine | — | 0.55 | 2047 | OK |
| 20 | Clothing_ARM_01_STEEL_SHOULDERS | Accessory1 | 01_STEEL | CL_F_ACC_PAULDRON_01 | LeftShoulder / RightShoulder | MIR | 0.24 | 2048 | OK |
| 21 | Clothing_ARM_02_COMPOSITE_SHOULDERS | Accessory1 | 02_COMPOSITE | CL_F_ACC_PAULDRON_02_COMPOSITE | LeftShoulder / RightShoulder | MIR | 0.24 | 2049 | OK |
| 22 | Clothing_ARM_03_BRONZE_SHOULDERS | Accessory1 | 03_BRONZE | CL_F_ACC_PAULDRON_03_BRONZE | LeftShoulder / RightShoulder | MIR | 0.24 | 2050 | OK |
| 23 | Clothing_ARM_04_AGRAV_SHOULDERS | Accessory1 | 04_AGRAV | CL_F_ACC_PAULDRON_04_AGRAV | LeftShoulder / RightShoulder | MIR | 0.24 | 2051 | OK |
| 24 | Clothing_ARM_05_MEDICAL_SHOULDERS | Accessory1 | 05_MEDICAL | CL_F_ACC_PAULDRON_05_MEDICAL | LeftShoulder / RightShoulder | MIR | 0.24 | 2052 | OK |
| 25 | Clothing_ARM_05_MEDICAL_FIELD_SHOULDERS | Accessory1 | 05_MEDICAL_FIELD | CL_F_ACC_PAULDRON_05_MEDICAL_FIELD | LeftShoulder / RightShoulder | MIR | 0.24 | 2053 | OK |
| 26 | Clothing_ARM_07_THOUGHTS_ENGINEER_SHOULDERS | Accessory1 | 07_THOUGHTS_ENGINEER | CL_F_ACC_PAULDRON_07_THOUGHTS_ENGINEER_v2 | LeftShoulder / RightShoulder | MIR | 0.24 | 2054 | OK |
| 27 | Clothing_ARM_09_SUPERHEAVY_SHOULDERS | Accessory1 | 09_SUPERHEAVY | CL_F_ACC_PAULDRON_09_SUPERHEAVY_v3b | LeftShoulder / RightShoulder | MIR | 0.24 | 2055 | OK |
| 28 | Clothing_ARM_10_TANK_SHOULDERS | Accessory1 | 10_TANK | CL_F_ACC_PAULDRON_10_TANK | LeftShoulder / RightShoulder | MIR | 0.24 | 2056 | OK |
| 29 | Clothing_ARM_11_RANGER_SHOULDERS | Accessory1 | 11_RANGER | CL_F_ACC_MANTLE_11_RANGER | Spine (мантия) | — | 0.55 | 2057 | OK, непарный |
| 30 | Clothing_ARM_12_CHROME_SHOULDERS | Accessory1 | 12_CHROME | CL_F_ACC_PAULDRON_12_CHROME | LeftShoulder / RightShoulder | MIR | 0.24 | 2058 | OK |
| 31 | Clothing_ARM_13_RUNIC_SHOULDERS | Accessory1 | 13_RUNIC | CL_F_ACC_PAULDRON_13_RUNIC | LeftShoulder / RightShoulder | MIR | 0.24 | 2059 | OK |
| 32 | Clothing_ARM_14_KORU_SHOULDERS | Accessory1 | 14_KORU | CL_F_ACC_PAULDRON_14_KORU | LeftShoulder / RightShoulder | MIR | 0.24 | 2060 | OK |
| 33 | Clothing_ARM_09_SUPERHEAVY_HANDS | Hands | 09_SUPERHEAVY | CL_F_HANDS_GAUNTLET_09_SUPERHEAVY_v3b | LeftHand / RightHand | MIR | 0.20 | 2061 | OK |
| 34 | Clothing_ARM_10_TANK_HANDS | Hands | 10_TANK | CL_F_HANDS_GAUNTLET_10_TANK | LeftHand / RightHand | MIR | 0.20 | 2062 | OK |
| 35 | Clothing_ARM_11_RANGER_HANDS | Hands | 11_RANGER | CL_F_HANDS_BRACER_11_RANGER_v2 | LeftHand / RightHand | MIR | 0.20 | 2063 | OK |
| 36 | Clothing_ARM_12_CHROME_HANDS | Hands | 12_CHROME | CL_F_HANDS_BRACER_12_CHROME | LeftHand / RightHand | MIR | 0.20 | 2064 | OK |
| 37 | Clothing_ARM_13_RUNIC_HANDS | Hands | 13_RUNIC | CL_F_HANDS_BRACER_13_RUNIC | LeftHand / RightHand | MIR | 0.20 | 2065 | OK |
| 38 | Clothing_ARM_14_KORU_HANDS | Hands | 14_KORU | CL_F_HANDS_BRACER_14_KORU | LeftHand / RightHand | MIR | 0.20 | 2066 | OK |
| 39 | Clothing_ARM_01_STEEL_FEET | Feet | 01_STEEL | CL_F_FEET_BOOT_01_STEEL | LeftFoot / RightFoot | MIR | 0.36 | 2067 | OK |
| 40 | Clothing_ARM_02_COMPOSITE_FEET | Feet | 02_COMPOSITE | CL_F_FEET_BOOT_03_COMPOSITE | LeftFoot / RightFoot | MIR | 0.36 | 2068 | OK |
| 41 | Clothing_ARM_03_BRONZE_FEET | Feet | 03_BRONZE | CL_F_FEET_BOOT_04_BRONZE | LeftFoot / RightFoot | MIR | 0.36 | 2069 | OK |
| 42 | Clothing_ARM_04_AGRAV_FEET | Feet | 04_AGRAV | CL_F_FEET_BOOT_05_AGRAV | LeftFoot / RightFoot | MIR | 0.36 | 2070 | OK |
| 43 | Clothing_ARM_05_MEDICAL_FEET | Feet | 05_MEDICAL | CL_F_FEET_BOOT_05_MEDICAL | LeftFoot / RightFoot | MIR | 0.36 | 2071 | OK |
| 44 | Clothing_ARM_05_MEDICAL_FIELD_FEET | Feet | 05_MEDICAL_FIELD | CL_F_FEET_BOOT_05_MEDICAL_FIELD | LeftFoot / RightFoot | MIR | 0.36 | 2072 | OK |
| 45 | Clothing_ARM_07_THOUGHTS_ENGINEER_FEET | Feet | 07_THOUGHTS_ENGINEER | CL_F_FEET_BOOT_07_THOUGHTS_ENGINEER_v2 | LeftFoot / RightFoot | MIR | 0.36 | 2073 | OK |
| 46 | Clothing_ARM_09_SUPERHEAVY_FEET | Feet | 09_SUPERHEAVY | CL_F_FEET_BOOT_09_SUPERHEAVY_v3b | LeftFoot / RightFoot | MIR | 0.36 | 2074 | OK |
| 47 | Clothing_ARM_10_TANK_FEET | Feet | 10_TANK | CL_F_FEET_BOOT_10_TANK | LeftFoot / RightFoot | MIR | 0.36 | 2075 | OK |
| 48 | Clothing_ARM_11_RANGER_FEET | Feet | 11_RANGER | CL_F_FEET_BOOT_11_RANGER | LeftFoot / RightFoot | MIR | 0.36 | 2076 | OK |
| 49 | Clothing_ARM_12_CHROME_FEET | Feet | 12_CHROME | CL_F_FEET_BOOT_12_CHROME | LeftFoot / RightFoot | MIR | 0.36 | 2077 | OK |
| 50 | Clothing_ARM_13_RUNIC_FEET | Feet | 13_RUNIC | CL_F_FEET_BOOT_13_RUNIC | LeftFoot / RightFoot | MIR | 0.36 | 2078 | OK |
| 51 | Clothing_ARM_14_KORU_FEET | Feet | 14_KORU | CL_F_FEET_BOOT_14_KORU | LeftFoot / RightFoot | MIR | 0.36 | 2079 | OK |
| 52 | Clothing_ARM_01_STEEL_BACK | Back | 01_STEEL | CL_F_AIRTANK_01_STEEL | Spine | — | 0.55 | 2080 | OK |
| 53 | Clothing_ARM_02_COMPOSITE_BACK | Back | 02_COMPOSITE | CL_F_AIRTANK_02_COMPOSITE | Spine | — | 0.55 | 2081 | OK |
| 54 | Clothing_ARM_03_BRONZE_BACK | Back | 03_BRONZE | CL_F_AIRTANK_03_BRONZE | Spine | — | 0.55 | 2082 | OK |
| 55 | Clothing_ARM_04_AGRAV_BACK | Back | 04_AGRAV | CL_F_AIRTANK_04_AGRAV | Spine | — | 0.55 | 2083 | OK |
| 56 | Clothing_ARM_05_MEDICAL_BACK | Back | 05_MEDICAL | CL_F_BACKPACK_05_MEDICAL | Spine | — | 0.55 | 2084 | OK |
| 57 | Clothing_ARM_06_CHEMICAL_BACK | Back | 06_CHEMICAL | CL_F_BACKPACK_06_CHEMICAL | Spine | — | 0.55 | 2085 | OK |
| 58 | Clothing_ARM_07_ENGINEER_BACK | Back | 07_ENGINEER | CL_F_BACKPACK_07_ENGINEER | Spine | — | 0.55 | 2086 | OK |
| 59 | Clothing_ARM_08_RESEARCH_BACK | Back | 08_RESEARCH | CL_F_BACKPACK_08_RESEARCH | Spine | — | 0.55 | 2087 | OK |
| 60 | Clothing_ARM_05_MEDICAL_BELT | Accessory2 | 05_MEDICAL | CL_F_BELT_05_MEDICAL | Hips | — | 0.44 | 2088 | OK |
| 61 | Clothing_ARM_06_CHEMICAL_BELT | Accessory2 | 06_CHEMICAL | CL_F_BELT_06_CHEMICAL | Hips | — | 0.44 | 2089 | OK |
| 62 | Clothing_ARM_07_ENGINEER_BELT | Accessory2 | 07_ENGINEER | CL_F_BELT_07_ENGINEER | Hips | — | 0.44 | 2090 | OK |
| 63 | Clothing_ARM_08_RESEARCH_BELT | Accessory2 | 08_RESEARCH | CL_F_BELT_08_RESEARCH | Hips | — | 0.44 | 2091 | OK |
| 64 | Clothing_ARM_01_STEEL_LEGS | Legs | 01_STEEL | BELT_01 + THIGH_01 | Hips; UpperLeg L/R | MIR (thigh) | 0.48 / 0.46 | 2092 | OK, 3 узла |
| 65 | Clothing_ARM_02_COMPOSITE_LEGS | Legs | 02_COMPOSITE | BELT_02 + THIGH_02 | Hips; UpperLeg L/R | MIR (thigh) | 0.48 / 0.46 | 2093 | OK, 3 узла |
| 66 | Clothing_ARM_03_BRONZE_LEGS | Legs | 03_BRONZE | BELT_03 + THIGH_03 | Hips; UpperLeg L/R | MIR (thigh) | 0.48 / 0.46 | 2094 | OK, 3 узла |
| 67 | Clothing_ARM_04_AGRAV_LEGS | Legs | 04_AGRAV | BELT_04 + THIGH_04 | Hips; UpperLeg L/R | MIR (thigh) | 0.48 / 0.46 | 2095 | OK, 3 узла |
| 68 | Clothing_ARM_05_MEDICAL_LEGS | Legs | 05_MEDICAL | THIGH_05 + KNEE_05 | UpperLeg L/R; LowerLeg L/R | MIR | 0.46 / 0.20 | 2096 | OK, 4 узла |
| 69 | Clothing_ARM_05_MEDICAL_FIELD_LEGS | Legs | 05_MEDICAL_FIELD | THIGH_05_FIELD + KNEE_05_FIELD | UpperLeg L/R; LowerLeg L/R | MIR | 0.46 / 0.20 | 2097 | OK, 4 узла |
| 70 | Clothing_ARM_07_THOUGHTS_ENGINEER_LEGS | Legs | 07_THOUGHTS_ENGINEER | THIGH_07 + KNEE_07 | UpperLeg L/R; LowerLeg L/R | MIR | 0.46 / 0.20 | 2098 | OK, 4 узла |
| 71 | Clothing_ARM_09_SUPERHEAVY_LEGS | Legs | 09_SUPERHEAVY | THIGH_09 + KNEE_09 | UpperLeg L/R; LowerLeg L/R | MIR | 0.46 / 0.20 | 2099 | OK, 4 узла |
| 72 | Clothing_ARM_10_TANK_LEGS | Legs | 10_TANK | THIGH_10 + KNEE_10 | UpperLeg L/R; LowerLeg L/R | MIR | 0.46 / 0.20 | 2100 | OK, 4 узла |
| 73 | Clothing_ARM_11_RANGER_LEGS | Legs | 11_RANGER | BELT_11 + THIGH_11 + KNEE_11 | Hips; UpperLeg L/R; LowerLeg L/R | MIR | 0.48 / 0.46 / 0.20 | 2101 | OK, 5 узлов |
| 74 | Clothing_ARM_12_CHROME_LEGS | Legs | 12_CHROME | BELT_12 + THIGH_12 + KNEE_12 | Hips; UpperLeg L/R; LowerLeg L/R | MIR | 0.48 / 0.46 / 0.20 | 2102 | OK, 5 узлов |
| 75 | Clothing_ARM_13_RUNIC_LEGS | Legs | 13_RUNIC | BELT_13 + THIGH_13 + KNEE_13 | Hips; UpperLeg L/R; LowerLeg L/R | MIR | 0.48 / 0.46 / 0.20 | 2103 | OK, 5 узлов |
| 76 | Clothing_ARM_14_KORU_LEGS | Legs | 14_KORU | BELT_14 + THIGH_14 + KNEE_14 | Hips; UpperLeg L/R; LowerLeg L/R | MIR | 0.48 / 0.46 / 0.20 | 2104 | OK, 5 узлов |

Статы: `tier` по линейке (01=1, 02=2, 03=3, 04=4, 05=5, 05F=5, 06=3, 07=5, 08=4, 09=8, 10=9, 11=6, 12=7, 13=7, 14=10);
`armorDefense = round(base × (1 + 0.15 × (tier−1)))` при base Head 4 / Chest 9 / Legs 7 / Feet 3 / Hands 2 / Shoulders 2 / Back 2 / Belt 1;
`weightKg` Head 1.6 / Chest 6.5 / Legs 5.0 / Feet 1.3 / Hands 0.7 / Shoulders 1.4 / Back 2.6 / Belt 0.9; `basePrice = 120 × tier`; `equipSlot = None`; `maxStack = 1`; `attachBoneOverride = LastBone`.

## 3. Этап 0 — фикс импорта

- `globalScale = 100` применён напрямую (не эвристика), сошлось на всех 93. `useFileScale = true` оставлен.
- `maxDim` в Unity: 92 модели в 0.9817…1.0044; `CL_F_HANDS_BRACER_14_KORU` — 0.9511 (дрейф 4.8%, принят как есть).
- Поэкземплярная таблица габаритов/центров/трис: `Tools/Blender/CharacterRetopo/_reports/armor_items/mesh_inspection.md` (игнорируется .gitignore).
- Текстуры не менялись (2048², sRGB, mips, compressed).

## 4. Нарезка парных мешей (split / twoLobe) и что оставлено

Правило п.2.2 применялось только к парным слотам (Shoulders, Hands, Feet, Thigh, Knee). Замеры по обеим осям меша — `.../armor_items/orientation_analysis.md`, визуальный контроль — `.../armor_items/paired_53_view_along_X.png` и `all_93_contact_sheet.png`.

**Результат: ни один из 53 парных мешей не разрезан; все 53 оставлены целыми (SINGLE).**
Причина: локальная ось X меша — ось «перёд-зад» детали (спереди бронещиток, сзади ремень обхвата ноги/руки), а не ось «лево-право». Поэтому формальный `splitX ∈ [0.35…0.65]` и `twoLobeX ≥ 0.30` у части моделей — это просвет между щитком и задним ремнём одной детали, а не пара левый+правый. По оси Y (реальная лево-право) условие двух полноценных половин с пустым центром не выполняется, связность подтверждает одну деталь.

Кандидаты, формально попадавшие под порог и проверенные глазами:

| Модель | splitX | twoLobeX | splitY | twoLobeY | Решение |
|---|---|---|---|---|---|
| CL_F_LEGS_KNEE_13_RUNIC | 0.464 | 0.750 | 0.366 | 0.600 | целая (щиток + задние застёжки одного наколенника) |
| CL_F_LEGS_KNEE_14_KORU | 0.452 | 0.700 | 0.497 | 0.550 | целая |
| CL_F_LEGS_THIGH_11_RANGER | 0.480 | 0.800 | 0.295 | 0.600 | целая (щиток + ремень вокруг бедра) |
| CL_F_LEGS_THIGH_14_KORU | 0.431 | 0.650 | 0.497 | 0.900 | целая |
| CL_F_ACC_PAULDRON_09_SUPERHEAVY_v3b | 0.485 | 0.450 | 0.397 | 0.050 | целая |
| CL_F_LEGS_KNEE_12_CHROME | 0.189 | 0.850 | 0.433 | 0.600 | целая |
| CL_F_LEGS_KNEE_11_RANGER_v2 | 0.283 | 0.550 | 0.484 | 0.350 | целая |
| CL_F_LEGS_THIGH_01_STEEL | 0.150 | 0.450 | 0.283 | 0.550 | целая |
| CL_F_LEGS_THIGH_02_COMPOSITE | 0.202 | 0.650 | 0.328 | 0.500 | целая |
| CL_F_LEGS_THIGH_07_THOUGHTS_ENGINEER_v2 | 0.412 | 0.300 | 0.384 | 0.400 | целая |
| CL_F_LEGS_THIGH_05_MEDICAL_FIELD | 0.216 | 0.450 | 0.500 | 0.600 | целая |

Мешей, разрезанных по п.2.2, нет; все 53 парных оставлены целиком.

## 5. 4 проблемных модели (п.4.4)

| Модель | Факт | Решение |
|---|---|---|
| CL_F_HANDS_BRACER_14_KORU | 1756 tris (бюджет 1200), `maxDim` 0.9511 | принят как есть; один наруч, не разрезан; в `_Eq` пара L(оригинал)/R(зеркало) |
| CL_F_LEGS_KNEE_05_MEDICAL | дрейф габарита 4.8% | принят; один наколенник; на ноге сидит по чашечке (yaw 90°) |
| CL_F_HANDS_BRACER_12_CHROME | `Size.X ≈ 0.997`, `splitX 0.460` | это ОДИН наруч, лежащий вдоль оси X меша (труба предплечья); не разрезан, поставлен вдоль руки |
| CL_F_HANDS_BRACER_14_KORU | `Size.X ≈ 0.951`, `splitX 0.440` | то же: один наруч вдоль X; не разрезан |

Двух деталей в одном меше не обнаружено ни в одной из 4 моделей и ни в одной из 53 парных.

## 6. Зеркало и сторона (п.4.5)

Зеркальный Mesh строится строго по п.2.3 в локальных координатах: `x → −x`, обратный порядок вершин треугольников, `normals.x → −x`, `tangents.x → −x` (`w` не тронут), `RecalculateBounds`. Отрицательного `localScale.x` и рантайм-зеркала нет.

Симметричный мировой поворот правого узла вычисляется аналитически:
`fwdR = (−fwdL.x, fwdL.y, fwdL.z)`, `upR = (−upL.x, upL.y, upL.z)`, `worldRotR = LookRotation(fwdR, upR)`, что даёт точное `p_R = M_world_x · p_L` для любого `worldRotL`.

Перестановок L/R (`swapLR`) в итоговой сборке нет: после проверки на превью все оригиналы смотрят на ту сторону, под которую нарисованы. Проверены и не потребовали swap: 12 наплечников (yaw 0°), 6 наручей/перчаток (вдоль руки), 13 ботинок (11× yaw 0°, 2 модели с длинной стопой также 0°), 9 наколенников (yaw 90°), 13 набедренников (yaw 0°, `THIGH_10_TANK` — 90°).

## 7. Префабы и код

- `Assets/_Project/Scripts/Equipment/Visual/EquipmentMirrorMount.cs` — компонент узла (`bone`, localPosition/Euler/Scale).
- `Assets/_Project/Scripts/Player/CharacterEquipmentVisualApplier.cs` — поддержка `equippedVisualPrefab`, перенос всех mount-узлов на кости, отключение коллайдеров, `Dictionary<EquipSlot, List<GameObject>>`, удаление пустого корня, старый путь `visualPrefab` сохранён, публичный API (`ActiveVisualCount`, `TryGetVisual`, `GetVisualForSlot`, `Reapply`, `DebugReapply`, `OnBodySwapped`) на месте.
- `Assets/_Project/Scripts/Core/ItemType.cs` — добавлено только поле `equippedVisualPrefab`.
- `Assets/_Project/Editor/Equipment/ArmorKitBuilder.cs` — идемпотентный билдер (`Tools/Project C/Armor/Build Armor Kits (Idempotent)`) + рендер превью в Prefab Mode.
- `Pickup_Armor.prefab` — `PickupItem` + `BoxCollider (isTrigger)` + `NetworkObject`.

## 8. Приёмка без Play Mode

Рендеры в Prefab Mode (`ArmorPreview.prefab`, 1024 px; листы 6×N по манекенам):

- `Tools/Blender/CharacterRetopo/_reports/armor_items/preview/front.png`
- `Tools/Blender/CharacterRetopo/_reports/armor_items/preview/side.png`
- `Tools/Blender/CharacterRetopo/_reports/armor_items/preview/back.png`
- `Tools/Blender/CharacterRetopo/_reports/armor_items/preview/mannequins_front.png`
- `Tools/Blender/CharacterRetopo/_reports/armor_items/preview/mannequins_side.png`
- `Tools/Blender/CharacterRetopo/_reports/armor_items/preview/mannequins_back.png`

Проверено по каждому из 76: одна деталь в `visualPrefab` с материалом и текстурой; корректные кости; у парных ровно 2 узла (L оригинал, R зеркало); у `LEGS` 3/4/5 узлов по составу; деталь не проваливается в тело; сзади нормали не вывернуты; двойной геометрии нет; половины симметричны.

Подгонка (п.4.3) выполнена в `_Eq`-префабах через `EquipmentMirrorMount`: масштабы по слотам (Head 0.30 / Chest 0.55 / Shoulders 0.24 / Hands 0.20 / Feet 0.36 / Thigh 0.46 / Knee 0.20 / Back 0.55 / Belt 0.48 / Utility 0.44), смещения от пивота кости и мировые повороты. Исходные FBX не менялись.

## 9. Что не сделано и почему

- **Префабы и `Assets/Generated_Models*` не попадают в Git**: в `.gitignore` проекта есть `*.prefab` (строка 153) и `Assets/Generated_Models*` (строка 223). Префабы/арт созданы и лежат в проекте, но в коммит входят только материалы, Mesh-ассеты, предметы, код и отчёт. Force-add не делал — это нарушило бы конвенцию репозитория.
- **`NetworkPrefabsList` не чинился** (запрет ТЗ). `Pickup_Armor.prefab` собран с `NetworkObject`; чтобы дроп брони регистрировался в сети, префаб нужно добавить в NetworkPrefabsList отдельной задачей.
- **Play Mode не запускался** (запрет): проверка надевания в живой игре — за пользователем.
- Существующие плейсхолдеры одежды (`MiningHelmet`, `SteelToeBoots`, `WorkCoveralls` и др.) не тронуты.
- `BootstrapScene.unity` и `QuestDatabase.asset` имеют посторонние незакоммиченные изменения в рабочем дереве (GlobalSceneSourceMarker); в этот коммит они не включены.

## 10. Проверка клиента/сервера по ItemRegistry

`InventoryWorld.RegisterAllItems()` и `InventoryServer.OnNetworkSpawn()` читают `Resources.Load<ItemRegistry>("Items/Data/ItemRegistry")` и берут `entry.id`; фолбэк на последовательный `Resources.LoadAll` срабатывает только если реестр пуст. Новые 76 записей добавлены в конец (id 2029…2104), порядок существующих не изменён.
