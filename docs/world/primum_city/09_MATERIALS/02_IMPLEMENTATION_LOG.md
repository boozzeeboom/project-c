# PRIMUM Э3.1 — журнал внедрения материалов

**Основа:** `01_MATERIAL_UNIFICATION.md` (§7.1 инструмент, §7.2 порядок работ), `material_map.csv`.
**Unity:** 6000.5.2f1 · **Сцена:** `Assets/_Project/Scenes/World/WorldScene_0_0.unity`.
Play Mode агентом не запускается; сцену агент не сохраняет (§2.9). Визуал, Play Mode, F8/F9 — `NOT RUN`
до проверки пользователем.

Отчёты инструмента лежат в `reports/` (генерируются меню 380–385, перезаписываются при повторном прогоне
с тем же тегом).

---

## Шаг 1 (§7.2 п.1) — инструмент, база Bronze, 31 цветовой вариант · 2026-10-25

### 1.1 Инструмент `Assets/_Project/Editor/Primum/PrimumMaterialMap.cs`

Меню `ProjectC/World/PRIMUM/Материалы: …`:

| Приоритет | Пункт | Что делает |
|---|---|---|
| 380 | `отчёт (без изменений)` | dry-run по всему `PRIMUM` → `reports/material_report_PRIMUM_report.csv` |
| 381 | `применить` | подстановка по всему `PRIMUM` (с подтверждением) → `…_apply.csv` |
| 382 | `проверить (verify)` | численная проверка §9 → `reports/material_verify_PRIMUM_verify.txt` |
| 383 | `собрать цветовые варианты` | база `Bronze` + отсутствующие варианты §5.4 → `reports/variants_build.csv` |
| 384 | `применить к выделенному чанку` | то же, что 381, но только под выделенным узлом `PRIMUM` |
| 385 | `проверить выделенный чанк` | то же, что 382, по выделенному узлу |

Скриптовый API (для пошаговых прогонов): `PrimumMaterialMap.Run(scope, apply, includeDoors, tag)`,
`Verify(scope, tag)`, `BuildVariants()`, `FindCityRoot()`, `FindChunk(name)`.

Как работает (контракт §2/§7.1):

* ключ — **имя** текущего материала слота; строка таблицы → `target`;
* цели ищутся по имени в `Assets/_Project/Materials/**`, папки `archive*` исключены; дубль имени цели — ошибка;
* подстановка только через `sharedMaterials`, **в тот же индекс слота**; `Undo.RecordObject` +
  `PrefabUtility.RecordPrefabInstancePropertyModifications`; одна Undo-группа на прогон; на диск
  ничего не сохраняется;
* идемпотентность: слот, уже стоящий на любой цели таблицы (или `PM_LAMP_GLOW_WARM`), — `already`;
* `DONE` / `HOLD` / `GAP` не трогаются (`skip_<kind>`);
* двери пропускаются (`skip_doors`): имена `PM_DOOR_*` **и** все рендереры внутри префабов
  `Assets/_Project/Prefabs/City/Doors/` (в `PRIMUM` это `ADMIN_DOOR_PREFABS` 712 слотов и
  `ADMIN_ENTRANCE_PORTAL` 12 слотов). Двери — шаг §7.2 п.5, правка префабов через `DoorPrefabBuilder`;
* итог печатается как `changed / already / unmapped / total` (+ skip/null/target_missing).

### 1.2 Базовый замер (до изменений) — `reports/material_verify_PRIMUM_00_baseline.txt`

| Показатель | Значение | Совпадает с §3 |
|---|---|---|
| Рендереров / слотов | 19 944 / 26 456 | ✅ |
| Слотов из FBX-подматериалов | 24 378 | ✅ |
| Различных `Material` | 444 | ✅ |
| Имён без строки в таблице | 0 | ✅ |
| Слотов на `URP/Lit` | 25 164 (= 26 456 − 1 292 `PM_LAMP_GLOW_WARM`) | — |

Dry-run до создания вариантов (`material_report_PRIMUM_00_baseline_dry.csv`): `would_change` 3 859,
`target_missing` 19 153 — ожидаемо, вариантов ещё не было.

### 1.3 База `Bronze`

`Assets/_Project/Materials/Bronze/M_PC_Bronze.mat` — **копия** архивного пресета
`Bronze/ARCHIVE_NOTUSED/M_PC_Bronze.mat` (шейдер `Bronze.shadergraph`, `_Bronze_Color` #805229).
Архив не тронут; инструмент исключает папки `archive*`, поэтому двойного имени для поиска цели нет.
Замороженный эталон `BronzeEdgeNoise_forTEST.shadergraph` / `ProceduralBronzeNoise.hlsl` не затронут.
Вопрос §10.5 (единый `M_PC_Bronze` или зоны) по-прежнему открыт; по умолчанию — единый.

### 1.4 31 цветовой вариант (§5.4) — `reports/variants_build.csv`

Каждый вариант — `Material Variant` своей базы, лежит **рядом с базой**, переопределяет только
цветовые свойства тона (+ `_Rust_Amount` = 0 у свинца). Рельеф, шум, сиды, масштабы — наследуются.

Как подобран цвет: оттенок и светлота главного свойства = `hex` **доминирующего по слотам** источника
из `material_map.csv` (плейсхолдеры `#CCCCCC` пропущены); остальные свойства тона пересчитаны в HSV
пропорционально пресету (тёмные волокна/швы остаются темнее главного, светлые — светлее, с потолком 0.97).
У крашеных/светлых поверхностей (`Wood_Red/Blue/Green/White/Ochre`, `Fabric_Cream`,
`Concrete_Limewash`) разброс светлоты сжат (`Contrast` 0.6), чтобы краска не выглядела сырым деревом.
У `Lantern_Cool` свечение (`_Glow_Color`, `_Core_Color`) меняет только оттенок, яркость сохранена.

| Семейство | Варианты | Переопределено | Hex-ориентир (источник) |
|---|---|---|---|
| Wood (7) | `_Dark`, `_Pine`, `_Red`, `_Blue`, `_Green`, `_White`, `_Ochre` | `_Wood/_LateWood/_Grain/_Pore/_Wear_Color` | #3D2616 `M_WOOD_DARK`, #C1A370 `M_TQ_PINE`, #843026 `M_TQ_TRIM_R`, #324C6D `M_TQ_TRIM_B`, #385642 `M_TQ_TRIM_G`, #EDEAE2 `M_TQ_TRIM_W`, #C6A847 `M_TQ_TRIM_Y` |
| Brick (2) | `_Yellow`, `_Red` | `_Brick_Color`, `_Brick_Light_Color` | #9E653D `M_BRICK_YELLOW`, #8E4C3D `M_TQ_BRICK_RED` |
| Cobble (3) | `_Granite`, `_Beige`, `_Rock` | `_Stone_Color`, `_Stone_Light_Color` | #6B6B72, #9E8C70, #47423F |
| Concrete (2) | `_Limewash`, `_Rose` | `_Concrete/_Mottle/_Aggregate/_Aggregate_Light/_Wear_Color` | #CCC1A8 `M_LIME_WASH`, #B28C7A `M_PLASTER_ROSE` |
| Tiles (6) | `_Bordeaux`, `_Copper`, `_Green`, `_Shingle`, `_Slate`, `_Steel` | `_Tile_Color`, `_Tile_Light_Color` | #59211E, #729989, #425B4C, #563F30, #495463, #656870 |
| Glass (4) | `_Green`, `_Dark`, `_Amber`, `_Blue` | `_Glass/_Murk/_Murk_Deep_Color` | #265947, #212832, #B75B19, #284C8C |
| Fabric (4) | `_Cream`, `_Green`, `_Bordeaux`, `_Canvas` | `_Fabric/_Gap/_Heather/_Pilling/_Wear_Color` | #F3EEE1, #0C301C, #5B231E `M_AWNING`, #AD9975 `M_CANVAS` |
| Bronze (1) | `_Brass` | `_Bronze_Color`, `_Scratch_Color` | #B28E47 `M_TQ_BRASS` |
| IronRust (1) | `_Lead` | `_Iron_Color`, `_Scratch_Color`, `_Rust_Amount` = 0 | #1C1E23 `M_LEAD` |
| Lantern (1) | `_Cool` | `_Glass_Color` (+ оттенок `_Glow/_Core_Color`) | #A5CCE5 `G_COOL` |

Glass-варианты лежат в `Glass/EMPTY_COLOR/` — там же, где база `M_PC_Glass` проекта.

### 1.5 Проверки шага 1

| Проверка | Результат |
|---|---|
| C# компилируется | ✅ `No compile errors` |
| Console | ✅ 0 errors |
| Создано / уже было / проблем | 31 / 0 / 0; повторный прогон 383 → 0 / 31 / 0 (идемпотентно) |
| `isVariant` и `parent` = база у всех 31 | ✅ |
| Переопределены только цветовые свойства (+ `_Rust_Amount`) | ✅ (`IsPropertyOverriden`, колонка `overrides`) |
| `ShaderUtil.ShaderHasError` у баз и вариантов | ✅ нет ошибок |
| Dry-run `PRIMUM` после вариантов (`…_01_after_variants_dry.csv`) | `would_change` **23 012**, `already` 1 292, `unmapped` 0, `target_missing` **0**, `skip_doors` 724, `skip_DONE/HOLD/GAP` 1 428 |
| Сцена | не изменена (`isDirty = false`) |
| Визуал вариантов, Play Mode | `NOT RUN` |

Сверка 23 012: рабочие имена 23 736 слотов (§3) − 712 `PM_DOOR_*` − 12 слотов портала-префаба = 23 012.
