# Броня 02_Armor: decimate + rebake — прогон T-ARM03a

> **Дата:** 2026-11-27 · **Тикет:** T-ARM03a
> **Статус:** прогон выполнен, decimated-арт лежит в `Assets/Generated_Models_Rebaked_Decimated`; в Unity не импортировался и в игре не проверялся.
> Инструмент: `Tools/Blender/CharacterRetopo/retopo_character.py` без изменений; источник — `Assets/Generated_Models_Rebaked`.

## 1. Итог

| Показатель | Значение |
|---|---:|
| Моделей всего | 93 |
| JSON-отчётов создано | 93 |
| Прошло все проверки | 66 |
| С замечаниями | 27 |
| Tris до (Rebaked) | 710 624 |
| Tris после (Decimated) | 179 352 |
| Фактический срез | 74.8 % |

Blender: `Blender 5.3.0`, `W:\SteamLibrary\steamapps\common\Blender\blender.exe` (проверен на существование до прогона).

Команда на каждую модель (из корня проекта, строго последовательно):

```
blender -b --factory-startup --python-exit-code 1 -P Tools\Blender\CharacterRetopo\retopo_character.py -- \
  --input "<REBAKED_FBX>" --out-dir "<OUT_DIR>" --out-name "<MODEL>_Dec" \
  --method decimate --target-tris <N> --planar 2 --res 2048 --samples 8 \
  --margin-px 4 --bleed-px 16 --report "<REPORT_JSON>" --no-render
```

`target = clamp(бюджет, 400, floor(src_tris * 0.55))`; для Legs подтип (BELT / THIGH / KNEE) определён по имени папки модели.

## 2. По категориям

| Категория | n | budget | src total | out total | avg out | max out | в target*1.02 | срез |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| Head_Helmet | 4 | 2500 | 30 104 | 9 999 | 2 500 | 2 500 | 4/4 | 66.8 % |
| Chest | 15 | 4000 | 114 384 | 59 965 | 3 998 | 4 000 | 15/15 | 47.6 % |
| Shoulders | 13 | 1500 | 100 332 | 19 491 | 1 499 | 1 500 | 13/13 | 80.6 % |
| Hands | 6 | 1200 | 46 517 | 7 752 | 1 292 | 1 756 | 5/6 | 83.3 % |
| Feet | 13 | 1500 | 98 591 | 19 489 | 1 499 | 1 500 | 13/13 | 80.2 % |
| Legs/THIGH | 13 | 1800 | 99 209 | 23 390 | 1 799 | 1 800 | 13/13 | 76.4 % |
| Legs/KNEE | 9 | 900 | 68 852 | 8 091 | 899 | 900 | 9/9 | 88.2 % |
| Legs/BELT | 8 | 1000 | 60 769 | 7 992 | 999 | 1 000 | 8/8 | 86.8 % |
| Backpacks_Airtanks | 8 | 2500 | 61 191 | 19 989 | 2 499 | 2 500 | 8/8 | 67.3 % |
| Utility_Belts | 4 | 800 | 30 675 | 3 194 | 798 | 799 | 4/4 | 89.6 % |
| **Всего** | **93** | — | **710 624** | **179 352** | — | — | **92/93** | **74.8 %** |

## 3. Модели с замечаниями

Всего 27 из 93. Единственная причина во всех случаях, кроме `CL_F_HANDS_BRACER_14_KORU`, — проверка `roundtrip.ok`.

| Модель | Категория | src | target | out | Замечание |
|---|---|---:|---:|---:|---|
| CL_F_HEAD_HELMET_01_STEEL | Head_Helmet | 7 933 | 2500 | 2 500 | roundtrip.ok=false (в FBX 2488 tris против 2500 до экспорта, потеря 12) |
| CL_F_HEAD_HELMET_03_BRONZE | Head_Helmet | 7 416 | 2500 | 2 500 | roundtrip.ok=false (в FBX 2491 tris против 2500 до экспорта, потеря 9) |
| CL_F_CHEST_PLATE_04_AGRAV | Chest | 7 851 | 4000 | 3 999 | roundtrip.ok=false (в FBX 3993 tris против 3999 до экспорта, потеря 6) |
| CL_F_ACC_PAULDRON_07_THOUGHTS_ENGINEER_v2 | Shoulders | 7 705 | 1500 | 1 499 | roundtrip.ok=false (в FBX 1483 tris против 1499 до экспорта, потеря 16) |
| CL_F_ACC_PAULDRON_13_RUNIC | Shoulders | 8 057 | 1500 | 1 500 | roundtrip.ok=false (в FBX 1489 tris против 1500 до экспорта, потеря 11) |
| CL_F_HANDS_BRACER_12_CHROME | Hands | 7 626 | 1200 | 1 200 | roundtrip.ok=false (в FBX 1197 tris против 1200 до экспорта, потеря 3) |
| CL_F_HANDS_BRACER_14_KORU | Hands | 7 839 | 1200 | 1 756 | tris 1756 > target*1.02 (1224); roundtrip.ok=false (в FBX 1755 tris против 1756 до экспорта, потеря 1; dims_delta 0.04692 > 0.02·H = 0.01996) |
| CL_F_FEET_BOOT_01_STEEL | Feet | 7 582 | 1500 | 1 500 | roundtrip.ok=false (в FBX 1490 tris против 1500 до экспорта, потеря 10) |
| CL_F_FEET_BOOT_03_COMPOSITE | Feet | 7 514 | 1500 | 1 497 | roundtrip.ok=false (в FBX 1455 tris против 1497 до экспорта, потеря 42) |
| CL_F_FEET_BOOT_04_BRONZE | Feet | 7 694 | 1500 | 1 499 | roundtrip.ok=false (в FBX 1453 tris против 1499 до экспорта, потеря 46) |
| CL_F_FEET_BOOT_05_AGRAV | Feet | 7 522 | 1500 | 1 500 | roundtrip.ok=false (в FBX 1491 tris против 1500 до экспорта, потеря 9) |
| CL_F_FEET_BOOT_05_MEDICAL | Feet | 7 413 | 1500 | 1 500 | roundtrip.ok=false (в FBX 1496 tris против 1500 до экспорта, потеря 4) |
| CL_F_FEET_BOOT_05_MEDICAL_FIELD | Feet | 7 631 | 1500 | 1 499 | roundtrip.ok=false (в FBX 1496 tris против 1499 до экспорта, потеря 3) |
| CL_F_FEET_BOOT_07_THOUGHTS_ENGINEER_v2 | Feet | 7 684 | 1500 | 1 498 | roundtrip.ok=false (в FBX 1495 tris против 1498 до экспорта, потеря 3) |
| CL_F_FEET_BOOT_11_RANGER | Feet | 7 755 | 1500 | 1 499 | roundtrip.ok=false (в FBX 1496 tris против 1499 до экспорта, потеря 3) |
| CL_F_FEET_BOOT_12_CHROME | Feet | 6 869 | 1500 | 1 500 | roundtrip.ok=false (в FBX 1477 tris против 1500 до экспорта, потеря 23) |
| CL_F_LEGS_THIGH_04_AGRAV | Legs/THIGH | 7 525 | 1800 | 1 800 | roundtrip.ok=false (в FBX 1796 tris против 1800 до экспорта, потеря 4) |
| CL_F_LEGS_THIGH_05_MEDICAL_FIELD | Legs/THIGH | 7 837 | 1800 | 1 799 | roundtrip.ok=false (в FBX 1796 tris против 1799 до экспорта, потеря 3) |
| CL_F_LEGS_THIGH_07_THOUGHTS_ENGINEER_v2 | Legs/THIGH | 7 511 | 1800 | 1 799 | roundtrip.ok=false (в FBX 1794 tris против 1799 до экспорта, потеря 5) |
| CL_F_LEGS_KNEE_05_MEDICAL | Legs/KNEE | 7 538 | 900 | 899 | roundtrip.ok=false (dims_delta 0.04796 > 0.02·H = 0.01996) |
| CL_F_LEGS_KNEE_12_CHROME | Legs/KNEE | 7 391 | 900 | 899 | roundtrip.ok=false (в FBX 895 tris против 899 до экспорта, потеря 4) |
| CL_F_LEGS_KNEE_13_RUNIC | Legs/KNEE | 7 968 | 900 | 898 | roundtrip.ok=false (в FBX 893 tris против 898 до экспорта, потеря 5) |
| CL_F_LEGS_BELT_03_BRONZE | Legs/BELT | 7 711 | 1000 | 998 | roundtrip.ok=false (в FBX 984 tris против 998 до экспорта, потеря 14) |
| CL_F_LEGS_BELT_04_AGRAV | Legs/BELT | 7 484 | 1000 | 999 | roundtrip.ok=false (в FBX 982 tris против 999 до экспорта, потеря 17) |
| CL_F_AIRTANK_02_COMPOSITE | Backpacks_Airtanks | 7 493 | 2500 | 2 497 | roundtrip.ok=false (в FBX 2489 tris против 2497 до экспорта, потеря 8) |
| CL_F_BELT_06_CHEMICAL | Utility_Belts | 7 868 | 800 | 798 | roundtrip.ok=false (в FBX 795 tris против 798 до экспорта, потеря 3) |
| CL_F_BELT_07_ENGINEER | Utility_Belts | 7 744 | 800 | 799 | roundtrip.ok=false (в FBX 795 tris против 799 до экспорта, потеря 4) |

### Почему падает `roundtrip.ok`

Проверка скрипта требует, чтобы число треугольников после записи FBX и повторного импорта отличалось от собранного меша не больше чем на `max(2, 0.1%)`.
На части моделей Blender 5.3 при экспорте FBX отбрасывает узкие/вырожденные треугольники, которые оставляет Collapse Decimate: меш до экспорта полный,
в FBX приходит на 1–46 tris меньше (0.07–3.1 %). Это воспроизведено отдельно: тот же меш, те же флаги экспорта, без bake и UV-паковки, даёт ту же убыль
(`CL_F_FEET_BOOT_03_COMPOSITE`: 1499 → 1458 tris). Это не ошибка bake и не потеря UV: `uv.flips = 0`, `degenerate_uv_tris = 0`, число вершин, габариты
(`dims_delta ≤ 0.02·H`), UV-слой и текстура в roundtrip совпадают, а итоговый FBX всё равно ниже бюджета.

Отдельный случай — допуск по габаритам у `CL_F_LEGS_KNEE_05_MEDICAL` (единственная причина его FAIL) и `CL_F_HANDS_BRACER_14_KORU`: `dims_delta 0.048` против `0.02·H ≈ 0.020`
(H — максимальный габарит модели). Collapse сдвинул крайние точки сильнее допуска, хотя tris, UV-слой и текстура на месте.

Иной случай — `CL_F_HANDS_BRACER_14_KORU`: расплав плоскостей дал 7725 tris, Collapse Decimate с ratio 0.1553 (запрос 1200) остановился на 1756 tris (фактический ratio 0.227) —
упёрся в предел коллапса на этой геометрии. В target*1.02 модель не попала; бюджет руки (1200) превышен на 46 %.

## 4. Где что лежит

- Арт: `Assets/Generated_Models_Rebaked_Decimated/02_Armor/<CATEGORY>/<MODEL>/<MODEL>_Dec.fbx` и `<MODEL>_Dec_BaseColor.png` (2048 px).
- JSON на модель: `Tools/Blender/CharacterRetopo/_reports/armor_decimate/<MODEL>_decimate.json`.
- CSV: `Tools/Blender/CharacterRetopo/_reports/armor_decimate/_armor_decimate_summary.csv`.
- Контрольные рендеры: `C:\Projects\ProjectC_client\Tools\Blender\CharacterRetopo\_reports\armor_decimate\renders` — по подпапке на модель, в каждой `A_high_*` (Rebaked, до) и `B_low_*` (decimated, после), ракурсы front / side / threeq.

  - `CL_F_ACC_PAULDRON_01`: `A_high_front.png`, `A_high_side.png`, `A_high_threeq.png`, `B_low_front.png`, `B_low_side.png`, `B_low_threeq.png`
  - `CL_F_CHEST_PLATE_01_STEEL`: `A_high_front.png`, `A_high_side.png`, `A_high_threeq.png`, `B_low_front.png`, `B_low_side.png`, `B_low_threeq.png`
  - `CL_F_FEET_BOOT_01_STEEL`: `A_high_front.png`, `A_high_side.png`, `A_high_threeq.png`, `B_low_front.png`, `B_low_side.png`, `B_low_threeq.png`
  - `CL_F_HANDS_BRACER_11_RANGER_v2`: `A_high_front.png`, `A_high_side.png`, `A_high_threeq.png`, `B_low_front.png`, `B_low_side.png`, `B_low_threeq.png`
  - `CL_F_HEAD_HELMET_01_STEEL`: `A_high_front.png`, `A_high_side.png`, `A_high_threeq.png`, `B_low_front.png`, `B_low_side.png`, `B_low_threeq.png`
  - `CL_F_LEGS_THIGH_11_RANGER`: `A_high_front.png`, `A_high_side.png`, `A_high_threeq.png`, `B_low_front.png`, `B_low_side.png`, `B_low_threeq.png`

## 5. Что не проверялось

- Импорт в Unity (масштаб, материалы, слоты), сборка префабов и предметов, визуал в игре, анимации — следующий этап, проверяет пользователь.
- Визуальная приёмка контрольных рендеров глазами (ремни, заклёпки, швы, тени на швах) — пользовательская.
- Исходники `Assets/Generated_Models` и `Assets/Generated_Models_Rebaked` не изменялись; decimation только в Blender, в Unity ничего не резалось.
