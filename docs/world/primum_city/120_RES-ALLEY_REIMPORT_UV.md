# 120 — S9 RES-ALLEY: переимпорт с фиксами (z-fight/фасады/нормали) + UV + подмена в Unity (T-CITY-ZFIGHT3, 2026-10-26)

Закрывает цепочку `117` (z-fight) → `118` (фасады) → `119` (нормали) **выгрузкой в город**:
исправленный чанк заново собран по дверному пайплайну, получил UV и подменён в Unity 1:1.

## 1. Что было в сцене до этой работы (регресс, найденный при аудите)

`16:49` в проект был подменён FBX из `_deliver/` — это **выгрузка первого поколения**
(без узлов `DOORS/LAMPS/DOOR_PARTS_UNASSIGNED/OUTLIERS`): 219 детей, 201 рендерер, двери
лежали плоскими объектами `RES_*_DOOR_LEAF` и были **включены** → двойники с 147 префаб-инстансами
в `PRIMUM_DOORS`, плюс «потерянные» 172 объекта. Для 22 остальных чанков структура v13.8
(с узлами) — этот чанк выбивался.

Правильная выгрузка для города — **дверной пайплайн v13.8** (`_work/run_all_doors_v13.py`),
она и повторена на исправленном чанке.

## 2. Blender-этап

### 2.1 Вход и стейджинг

* Рабочий источник — `PRIMUM_chunks/HOUSES/PRIMUM_HOUSES_S9_RES-ALLEY_P1_18H_v3.blend`
  (после фиксов 117–119; проверено headless: 9 716 объектов, 78 725 граней).
* Стейдж-копия для прогона: `_work/rawfix_RESALLEY_20261026/PRIMUM_HOUSES_S9_RES-ALLEY_P1_18H.blend`
  (каноническое имя, чтобы v13.8 нашёл файл; рабочий файл пользователя не тронут).
* Перед прогоном на стейдже добиты остатки: нормали 37 объектов (33 открытых кровельных
  острова + 4 BASE-острова) и z-fight 15 → 0 пар за 4 прохода (`_work/prep_staging_fix.py`).
  Причина: правки нормалей BASE-объектов — это linked-library данные, в файл не сохраняются;
  добиваются на копии, а в FBX BASE не попадает (linked-объекты удаляются перед экспортом).

### 2.2 Дверной пайплайн v13.8

```powershell
$env:DOORS_RAWDIR = "...\New Folder\_work\rawfix_RESALLEY_20261026"
$env:DOORS_LIST  = "...\New Folder\_work\lists\resalley_only.txt"
$env:DOORS_WORKER = "resalley_v13fix"
blender.exe -b --factory-startup --python "...\New Folder\_work\run_all_doors_v13.py"
```

Гейты (лог `_work/logs/run_v13_resalley_v13fix.log`):

| гейт | результат |
|---|---|
| грани | 78 725 → 78 725 **OK** |
| утечки `_DOOR` | **0** |
| якоря → двери | 192 → **147** (манифест 171) |
| привязано частей | 732 объектов |
| конструктив остался в батчах | 56 |
| улетевшие / высокие (>2.9 м) | 0 / **0 OK** |
| лампы | 198 |
| крыши (перевёрнуто/дублей) | 0 / 0 |
| время | 227 с |

### 2.3 UV smart unwrap — **после join** (это и есть ответ на вопрос «до или после»)

`_work/uv_unwrap_export.py`, запуск:

```powershell
blender.exe -b --factory-startup --python "...\New Folder\_work\uv_unwrap_export.py" -- PRIMUM_HOUSES_S9_RES-ALLEY_P1_18H
```

* Порядок: `join/батчи → k=1.07 → нормали → **unwrap** → save → export` (как в 26.10-скрипте).
* Параметры: `smart_project(angle_limit=66°, island_margin=0.02, area_weight=0,
  correct_aspect=True, scale_to_bounds=False)`, UV-слой `UVMap`.
* Результат: **373/373 меша**, ошибок 0, пустых/одноточечных UV 0, 10 с.
* В FBX: `LayerElementUV` = **746** записей (2 на меш), `UVMap` = 373.

**Почему после join, а не до:** join сохраняет UV-каналы, но не переупаковывает острова —
если развернуть каждый исходный объект отдельно, после join у всех объектов UV лежат в 0…1 и
перекрываются (атласа нет, любой тайл-текстур ляжет «поверх»). После join каждый финальный
батч режется на острова и упаковывается в собственный 0…1 — это и есть смысл unwrap для
последующей текстуризации/лайтмапов. Побочный эффект: texel density на крупный батч ниже;
если понадобится «каждый исходный объект = полный 0…1» (trim-sheet/tiling), это делается
на входе и меняет контракт — тогда скажите, переделаем.

### 2.4 Выход

| файл | что |
|---|---|
| `_deliver_doors/PRIMUM_HOUSES_S9_RES-ALLEY_P1_18H.fbx` | 3.52 МБ, узлы `DOORS`(147) / `LAMPS`(198) / `DOOR_PARTS_UNASSIGNED`(0) / `OUTLIERS`(0), UV внутри |
| `_deliver_doors/doors_S9_RES-ALLEY_P1_18H.csv` | 147 дверей |
| `PRIMUM_chunks_reimport_doors/PRIMUM_HOUSES_S9_RES-ALLEY_P1_18H.blend` | 2.54 МБ — joined+scaled+нормали+UV (395 объектов, 373 меша, 22 пустышки) |

Бэкапы до прогона: `_versions/…_v13_joined_before_fix_2026-10-26.blend`,
`_versions/…_deliver_doors_before_fix_2026-10-26.fbx`, `doors_…_before_fix_2026-10-26.csv`.

## 3. Unity-этап (1:1)

1. **Бэкап и подмена**: `Assets/_Project/Art/Models/Primum/Chunks/<чанк>.fbx` ← стейдж,
   MD5 стейджа == MD5 в проекте (`B3BE7778DD66C24AA6B099C59906EF90`); `.meta` не трогали — GUID
   сохранён (`b7861b55325cdff47a89d3e842dc3ffb`). Бэкап — `_versions/…_before_fix_swap_2026-10-26.fbx`.
2. **Импорт**: force-refresh всех ассетов ≈ **12 мин** (тяжёлый проект; в следующий раз —
   `mode=if_dirty` и точечный `ImportAsset(fbx)`).
3. **Пост-проход по префабу** `PRIMUM_CITY.prefab` (LoadPrefabContents → правки → SaveAsPrefabAsset):
   * слой `PRIMUM` (8) рекурсивно — нарушений 0;
   * `DOORS` 147 → **выключены** (манифест этого чанка без `keep_original`);
   * `LAMPS` 198 → **включены**;
   * `DOOR_PARTS_UNASSIGNED` 0, `OUTLIERS` 0.
4. **Сброс устаревших оверрайдов сцены**: после подмены FBX у батчей остались оверрайды
   `MeshFilter` → старые `Rebased`-меши (визуально старая геометрия). Снято
   `PrefabUtility.RevertObjectOverride` с 373 `MeshFilter` + 373 `Transform` → всё вернулось
   к FBX-мешам и FBX-трансформам (rot X≈270°, scale 100).
   ⚠️ Это заняло **≈26 мин** (каждый revert пересчитывает prefab-instance диффы). В следующий раз
   делать иначе: восстанавливать только меши по `AssetDatabase.LoadAssetAtPath<GameObject>(fbx)`
   (по путям) — без полного revert.
5. **Y-up rebase (меню 388, `PrimumMeshRebase.RebaseChunk`)** — обязателен после подмены:
   * 373/373 меша, 0 skipped, 66 новых саб-ассетов + 307 обновлено;
   * вершин 328 098; вырожденных тангенсов **48 109 → 0**;
   * max vertex delta 3.1·10⁻⁵ м, `VERDICT 1:1 = PASS`;
   * отчёт: `09_MATERIALS/reports/mesh_rebase_chunk_PRIMUM_HOUSES_S9_RES-ALLEY_P1_18H.txt`.
6. **Материалы Э3.1** (меню 384 → dry → 385):
   * apply: 373 рендерера, изменено 105 / 200 слотов, `unmapped 0`, ошибок 0;
   * dry: `changed 0 / already 531` — идемпотентно;
   * verify: **373 рендерера / 531 слот**, null 0, саб-материалы модели 0, `URP/Lit` **0**,
     не-ShaderGraph 0, **15** различных материалов (DoD ≤ 60), `names without row 0`.
7. **Сцена сохранена**: `Assets/_Project/Scenes/World/WorldScene_0_0.unity` (dirty=false).

## 4. Приёмка (замер после подмены)

| проверка | ожидание | факт |
|---|---|---|
| детей чанка | 18 пустышек + 4 узла + 28 батчей | **50** |
| рендереров | 373 (как в FBX) | **373** |
| `DOORS` | 147, все выключены | **0 вкл / 147 выкл** |
| `LAMPS` | 198, включены | **198 вкл** |
| `DOOR_PARTS_UNASSIGNED` / `OUTLIERS` | вкл / выкл | 0 / 0 |
| слой вне `PRIMUM` | 0 | **0** |
| меши | `Rebased/*_YUP` с UV | да (UV 16 056 вершин у пробы) |
| материалы | `M_PC_*`, URP/Lit 0 | **15 шт, URP/Lit 0** |
| коллайдеры | не тронуты (`PRIMUM_COLLIDERS`, из `houses_boxes.json`) | **376 боксов** (364 HOUSE + 12 прочих) |
| геометрия 1:1 | грани 78 725, delta ≤ 3.1·10⁻⁵ м | PASS |

## 5. Что осталось / риски

1. **Старые `Rebased`-саб-ассеты не вычищены**: в `Rebased/<чанк>/<чанк>_YUP_meshes.asset`
   остались меши под старые имена полотен (`RES_*_DOOR_LEAF_YUP` и т.п.). На рендер не влияют,
   но ассет пухнет. Отдельная уборка: удалить саб-ассеты, на которые нет ссылок.
2. **Манифест/реестры дверей устарели**: `placement_manifest.csv` — 171 строка (сейчас 147 дверей:
   66 place + 81 skip), реестры — провенанс от 20.10. Расстановка живёт по сцене (`PRIMUM_DOORS`),
   ничего не переставляли; если нужно — обновлять отдельным проходом (меню 301 пересобирает и
   **само сохраняет сцену** — осторожно).
3. **Git**: `Assets/_Project/Art/*` и `*.prefab` — в `.gitignore` (FBX и префаб не версионируются);
   изменились только сцена и отчёты. В рабочем дереве были **чужие незакоммиченные правки**
   (CharacterRebake, иконки, материалы) — коммит не делал, чтобы не смешать.
4. Следующий прогон ускорить: точечный импорт FBX вместо force-refresh; revert оверрайдов
   только для мешей, ссылающихся на `Rebased`; rebase и материалы — те же меню 388/384/385.

## 6. Файлы

* Скрипты: `_work/run_all_doors_v13.py` (без изменений), `_work/uv_unwrap_export.py`,
  `_work/prep_staging_fix.py`, `_work/inspect_chunk_files.py`, `_work/verify_v3_headless.py`,
  `_work/lists/resalley_only.txt`.
* Логи: `_work/logs/run_v13_resalley_v13fix.log`, `_work/logs/uv_unwrap_20261026_182529.log`.
* Отчёты Unity: `09_MATERIALS/reports/mesh_rebase_chunk_…`, `material_report_…_apply.csv`,
  `material_report_…_after_dry.csv`, `material_verify_…_verify.txt`.
