# 05. Скрипты, данные и бэкапы (карта)

**Рабочий каталог Blender:** `Q:\Yandex.disk\Yandex.Disk\Project-C\models\primum\New Folder\`

---

## 1. Скрипты (`_work/`)

| Скрипт | Назначение |
|---|---|
| **`run_all_doors_v13.py`** | **основной** конвейер: склады→острова→сборки→двери, батчи, нормали, k=1.07, экспорт, гейты (поддерживает воркеры через env `DOORS_WORKER`/`DOORS_LIST`) |
| `run_all_doors_fast.py`, `run_all_doors.py`, `pilot_doors_ring*.py` | предыдущие версии (история; не использовать) |
| `final_join.py` | базовый джойн чанка + определение дверей + масштаб (первая версия пайплайна) |
| `fix_normals2.py`, `fix_normals_missing.py` | островной разворот нормалей (signed volume по связным островам) |
| `export_missing.py` | экспорт 5 отсутствовавших чанков (SE×2, S10×3) |
| `audit_batch_islands.py` | аудит остатков в батчах (острова у дверей) — **чтение** |
| `audit_residual.py` | аудит объектов, которые могут не попасть в двери — **чтение** |
| `audit_door_leaks.py`, `diag_*`, `inspect_*` | диагностика прошлых итераций |
| `swap_all_chunks.ps1`, `swap_five.ps1` | подмена FBX в проекте (с бэкапом) |
| `run_parallel_doors.ps1`, `run_all_doors.ps1` | запуск N воркеров Blender |

## 2. Данные и каталоги

| Путь | Что |
|---|---|
| `PRIMUM_chunks_RAW/` | **исходники (read-only!)**: 23 чанка + пре-джойн копии отдельных файлов + `ADDENDUM_2026-10-18.md` |
| `PRIMUM_chunks_reimport_doors/` | рабочие версионные бленды (после конвейера) |
| `PRIMUM_chunks/` | старые рабочие бленды (k=1.07, до дверей) |
| `_deliver/` | FBX первого поколения (без дверей) |
| `_deliver_doors/` | **актуальные FBX** (v13) + `doors_<чанк>.csv` + `summary.csv` |
| `_pilot/` | пилотные артефакты (RING) |
| `_versions/` | **бэкапы**: `*_before_final_swap_*`, `*_v11_damaged_*`, `*_before_v13*_swap_*`, бэкапы TerrainData |
| `_work/logs/` | логи всех прогонов (`run_v13_*.log`, `audit_*.log`, `diag_*.log`) |
| `_work/lists/` | файлы-списки чанков для воркеров |

## 3. Проект Unity

| Путь | Что |
|---|---|
| `Assets/_Project/Art/Models/Primum/Chunks/*.fbx` | 23 модели города (GUID стабильны) |
| `Assets/_Project/Prefabs/City/PRIMUM_CITY.prefab` | корень города (23 ребёнка-модели) |
| `Assets/_Project/Scenes/World/WorldScene_0_0.unity` | сцена с инстансом города |
| `docs/world/primum_city/03_DOORS/registries/` | реестры дверей (23 CSV) |

## 4. Бэкапы и откат

* Каждая подмена FBX оставляет копию в `_versions/<чанк>_before_<версия>_swap_<дата>.fbx`.
* Повреждённые варианты сохраняются с явной пометкой (`*_v11_damaged_*`).
* Откат = `Copy-Item _versions/<...>.fbx Assets/.../<чанк>.fbx` + `refresh_unity` + пост-проход.
* Террейн: `_versions/terrain_backup_2026-10-18/`.

## 5. Окружение

| Что | Значение |
|---|---|
| Blender | `W:\SteamLibrary\steamapps\common\Blender\blender.exe` (5.2.2 LTS), headless: `-b --factory-startup --python <script>` |
| FBX-экспорт | `apply_scale_options='FBX_SCALE_NONE'`, `global_scale=1.0`, `axis_forward='-Z'`, `axis_up='Y'`, `use_mesh_modifiers=True`, `object_types={'MESH','EMPTY'}` |
| Unity | 6000.5.2f1, URP 17.5, MCP-порт **6401** (`unityMCP`) |
| RAM/CPU | 64 ГБ / i7-8700 — комфортно 4–6 воркеров Blender |
