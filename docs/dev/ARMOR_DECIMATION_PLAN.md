# Броня 02_Armor: decimation + rebake до рабочих бюджетов

> **Дата:** 2026-11-27 · **Тикет:** T-ARM03a (арт-подготовка)
> **Статус:** план. Прогон ещё не выполнялся, ассеты не менялись.
>
> Связано: `docs/dev/ARMOR_KITS_PICKABLE_ITEMS.md`, `docs/dev/ARMOR_MIRRORING_PAIRS.md`.

---

## 1. Что уже написано и где лежит

| Инструмент | Что делает | Для брони |
|---|---|---|
| `Tools/Blender/CharacterRetopo/retopo_character.py` | import → decimate/quadriflow/voxel → planar dissolve → UV → pack → bake BaseColor high→low → FBX + PNG + roundtrip QA + JSON | **не запускался** |
| `Tools/Blender/CharacterRebake/run_armor_all_v1.bat` + `rebake_batch_v1.ps1` | UV-ребейк без decimation | **запускался** → `Assets/Generated_Models_Rebaked/02_Armor` |
| `Tools/Blender/CharacterRebake/rebake_all.bat`, `rebake_library.bat`, `rebake_model.py` | UVRB v2 (UV + перепек, без decimation) | для брони не нужен |

Decimation-скрипт ровно один — `retopo_character.py`. Его же отчёт по персонажу подтверждает
работоспособность: 834 365 → 39 999 tris, 25 с, bake 6.3 с, 0 flips, 0 degenerate, roundtrip ok
(`Tools/Blender/CharacterRetopo/_reports/PC_Char_Adjudicator_Female_APOSE_v01_decimate.json`).

Ключевые параметры скрипта:
`--method decimate|quadriflow|voxel`, `--target-tris N`, `--planar <deg>` (DISSOLVE перед collapse),
`--keep-uv` (сохранить атлас, без перепека), `--res`, `--samples`, `--margin-px`, `--bleed-px`,
`--report`, `--renders`, `--no-render`, `--no-export`.

---

## 2. От чего гонять: Rebaked, не raw

| | raw `Generated_Models` | `Generated_Models_Rebaked` |
|---|---|---|
| UV-острова (все 93) | 132 771 | **11 458** |
| Вершины | ~×2 больше | сварены |
| BaseColor | JPG | **PNG без потерь, 2048** |
| Треугольники | 710 624 | 710 624 (не менялись) |

`retopo_character.py` делает high→low bake: источник = high. Если источник Rebaked, low получает
текстуру из уже перепечённого атласа — один перепек, без двойной потери. Прогон от raw даст второй
перепек и лишние швы без выгоды.

`--keep-uv` не использовать: collapse тянет UV и даёт искажения на швах; нужен полный
decimate + новый атлас + перепек.

---

## 3. Бюджеты треугольников

Сейчас: **93 модели × 7 641 tris = 710 624**, разброс 6 869–8 057.

Профиль **Quality** (рекомендуемый, ~25 % от исходного):

| Категория | n | Сейчас всего | Бюджет/шт | Итого | Срез |
|---|---:|---:|---:|---:|---:|
| Head_Helmet | 4 | 30 104 | 2 500 | 10 000 | 33 % |
| Chest | 15 | 114 384 | 4 000 | 60 000 | 52 % |
| Shoulders | 13 | 100 332 | 1 500 | 19 500 | 19 % |
| Hands | 6 | 46 517 | 1 200 | 7 200 | 15 % |
| Feet | 13 | 98 591 | 1 500 | 19 500 | 20 % |
| Thigh | 13 | 99 209 | 1 800 | 23 400 | 24 % |
| Knee | 9 | 68 852 | 900 | 8 100 | 12 % |
| Legs Belt | 8 | 60 769 | 1 000 | 8 000 | 13 % |
| Back | 8 | 61 191 | 2 500 | 20 000 | 33 % |
| Utility Belts | 4 | 30 675 | 800 | 3 200 | 10 % |
| **Всего** | **93** | **710 624** | — | **≈ 178 900** | **25 %** |

Надетый комплект на персонажа (шлем + грудь + 1 наплечник + руки + Legs-набор + ступни + спина):
2 500 + 4 000 + 1 500 + 1 200 + (1 000 + 1 800 + 900) + 1 500 + 2 500 ≈ **16 900 tris**.

Профиль **Performance** (если понадобится ~15 %): Head 1 500, Chest 2 500, Shoulders 900,
Hands 700, Feet 900, Thigh 1 100, Knee 600, Belt 600, Back 1 500, Utility 500 → ≈ 110 000,
комплект ≈ 10 500 tris.

Правило: `target = clamp(профиль, 400, 0.55 × исходник)` — не режем больше 45 % и не уходим ниже 400.

---

## 4. Текстуры

Первая итерация — **2048 везде** (как в Rebaked). Если память важна, второй итерацией:
2048 для Head/Chest/Back/Thigh/Belt, 1024 для Shoulders/Hands/Feet/Knee/Utility.
Разрешение задаётся `--res`; перепек 2048 на броне — ~6–8 с на модель.

---

## 5. Куда писать результат

```
Assets/Generated_Models_Rebaked_Decimated/02_Armor/<CATEGORY>/<MODEL>/
    <MODEL>_Dec.fbx
    <MODEL>_Dec_BaseColor.png
Tools/Blender/CharacterRetopo/_reports/armor_decimate/<MODEL>_decimate.json
```

`Assets/Generated_Models_Rebaked` **не перезаписывать**: items собираются из decimated-копии,
а Rebaked остаётся эталоном для сравнения.

---

## 6. Шаблон прогона одной модели

```bat
set "BLENDER_EXE=W:\SteamLibrary\steamapps\common\Blender\blender.exe"

"%BLENDER_EXE%" -b --factory-startup --python-exit-code 1 ^
  -P Tools\Blender\CharacterRetopo\retopo_character.py -- ^
  --input  "Assets\Generated_Models_Rebaked\02_Armor\Chest\CL_F_CHEST_PLATE_01_STEEL\CL_F_CHEST_PLATE_01_STEEL_Rebaked.fbx" ^
  --out-dir "Assets\Generated_Models_Rebaked_Decimated\02_Armor\Chest\CL_F_CHEST_PLATE_01_STEEL" ^
  --out-name "CL_F_CHEST_PLATE_01_STEEL_Dec" ^
  --method decimate --target-tris 4000 --planar 2 --res 2048 --samples 8 ^
  --margin-px 4 --bleed-px 16 ^
  --report "Tools\Blender\CharacterRetopo\_reports\armor_decimate\CL_F_CHEST_PLATE_01_STEEL_decimate.json" ^
  --no-render
```

- `--planar 2` — снять плоские пластины перед collapse, бюджет уходит на изгибы.
- `--no-render` в пакете; рендеры — только для 6 контрольных моделей (без `--no-render`).
- Blender 5.2.3 LTS, путь из README пайплайна. Один процесс на модель, последовательно.

---

## 7. Приёмка

- По каждому отчёту: `retopo.tris <= target * 1.02`, `uv.flips == 0`, `uv.degenerate_uv_tris == 0`,
  `roundtrip.ok == true`, `source.tris` совпадает с ожидаемым.
- 6 контрольных моделей (шлем, нагрудник, ботинок, наруч, наплечник, Legs-набор) — рендеры
  до/после, глазами: ремни и заклёпки не пропали, швы не поплыли.
- Unity Play Mode не запускать; приёмка в инспекторе/Prefab Mode.

---

## 8. Риски

1. Collapse «съедает» тонкие ремни и мелкие заклёпки. Лечение: +20–30 % к бюджету категории.
2. Bake cage `0.012 * H` на раздельных оболочках может дать тени на швах. Смотреть контрольные.
3. `--planar` с `delimit={"UV"}` может наделать n-gon'ов; в конце скрипт триангулирует при collapse.
4. Время: ~15–25 с на модель → 93 модели ≈ 25–40 мин.
5. Скрипт писался под персонажа; на мелких деталях UV-паковка может дать низкое покрытие —
   порог/разрешение смотреть по отчёту.

---

## 9. Порядок работ

1. T-ARM03a — этот прогон (decimate + rebake 93 моделей).
2. Приёмка бюджетов по отчётам и 6 рендерам.
3. T-ARM02/03 — сборка предметов уже из `Generated_Models_Rebaked_Decimated`.
4. Если качество где-то просело — точечно поднять бюджет и перегнать только эти модели.
