# PRIMUM: работа по дверям админки — передача другому агенту (2026-10-23)

Статус: **двери расставлены, но пользователь видит их НЕ в своих местах и МЕНЬШЕ по размеру.** Эталон для сверки — объект `PRIMUM/ADMIN_FOR_COLLIDER` (RAW-админка 1:1). Кодить по этому доку будет другой агент; вмешательства в код выше (этот док) не требуются.

## Что сделано (последние 2 запроса)

### Запрос 1: возврат ADM_DOOR_PORTAL_GLASS + префаб главного входа + первые двери
Коммит: `59515f23`.

1. **Диагноз пропажи стекла:** в сджойненном чанке `PRIMUM/PRIMUM_ADMIN_S0_ADM_P1_01H` геометрия `ADM_DOOR_PORTAL_GLASS` попала в группу двери `DOORS/ADM_DOOR_L_12.63x4.17_X`, а узел `DOORS` выключен → стекло невидимо. В `DOOR_PARTS_UNASSIGNED` остались только острова `ADM_DOOR_PORTAL_FRAME_isl*`.
2. **Префаб главного входа:** из `ADMIN_FOR_COLLIDER` скопированы 8 объектов (`ADM_DOOR_L`, `ADM_DOOR_L_IRON`, `ADM_DOOR_R`, `ADM_DOOR_R_IRON`, `ADM_DOOR_PORTAL_FRAME`, `ADM_DOOR_PORTAL_GLASS`, `ADM_DOOR_PORTAL_SILL`, `ADM_DOOR_PORTAL_TRANSOM`) в
   `C:\Projects\ProjectC_client\Assets\_Project\Prefabs\City\Doors\PM_ADM_ENTRANCE_PORTAL.prefab`
   и поставлен в сцену как `PRIMUM/ADMIN_ENTRANCE_PORTAL`.
   - Поправленный баг: первая постановка уехала (позиция «портала» была посчитана до обновления transform'ов → ставился мусор вроде (79, 26.7, −21.1)). Исправлено установкой `localPosition` экземпляра = `localPosition` стекла из `ADMIN_FOR_COLLIDER` → сверка: **смещение стекла 0.000 м**, все 8 объектов 0.000 м.
3. **123 одиночные двери** в проёмы-рамы (`_FRAME_`) из узла `DOORS` → `PRIMUM/ADMIN_DOOR_PREFABS` (тип по этажу: ground/вестибюль=ARCH_C, F1=PANEL_B, F2=PLANK_A, F3=ARCH_C, F4=PANEL_B; масштаб = min((ширина_рамы−0.26)/ширина_префаба, (высота_рамы−0.13)/высота_префаба); позиция = pivot объекта-проёма, поворот = поворот объекта-проёма).

### Запрос 2: внутренние двери в комнатах (проёмы без геометрии рам)
Коммит: `db8d5e15`.

1. **Причина пустых проёмов:** у комнатных проёмов в RAW вообще нет объектов дверей/рам — это просто разрывы в стенах. Пайплайн видит только рамы (`_FRAME_`), поэтому комнотные проёмы не были заполнены.
2. **Детектор проёмов** `Q:\Yandex.disk\Yandex.Disk\Project-C\models\primum\New Folder\_work\detect_doorways3_admin.py`:
   - растровая карта этажа (шаг 0.75 м), признак проёма = пусто на высоте 1.2 м + перекрытие на 2.4 м;
   - фильтры: ширина кластера 0.7–3.4 м, ≤12 ячеек;
   - направление прохода — горизонтальные лучи ±X/±Y на 3 м;
   - выход: `Q:\Yandex.disk\Yandex.Disk\Project-C\models\primum\New Folder\_work\logs\admin_doorways.csv` (92 строки: x,y,z,width,nx,ny в координатах RAW); лог: `...\_work\logs\detect_doorways3.log`.
   - найдено: F1=26, F2=26, F3=10, F4=30.
3. **92 двери** `ADM_INT_DOOR_F<n>_<nn>` добавлены в `PRIMUM/ADMIN_DOOR_PREFABS` (итого 215: F1=39, F2=48, F3=54, F4=74). Тип: F1/F4=PANEL_B, F2=PLANK_A, F3=ARCH_C. Позиция: `localPosition = (−x_raw, z_raw, −y_raw)`; поворот: `yaw = atan2(−nx, −ny)`; масштаб: min((clamp(width−0.1, 0.9, 2.6))/ширина, 2.45/высота).

## Известная проблема (по слову пользователя)

> «двери не на своих местах, они меньшего размера — ты напутал что-то с размерами и трансформами. Эталон — ADMIN_FOR_COLLIDER, там 1в1 всё правильных размеров.»

Вероятные источники ошибки для расследования:
1. **Масштаб дверей-префабов** брался от габаритов объектов узла `DOORS` чанка (рамы 1.80×2.56 и т.п.) с вычитом «полей» 0.26/0.13 — это даёт двери заметно меньше реальных полотен. Реальные размеры полотен — в RAW: например `ADM_DOOR_L_IRON` ≈ 1.39×2.35 м, `ADM_CONN_*_LEAF_1.41x2.17`.
2. **Позиция** 123 первых дверей бралась с pivot'ов объектов `DOORS` чанка (пивот выставлен пайплайном = низ-центр bbox группы) — если пивот не совпадает с центром проёма, двери смещены.
3. **Координатная связка RAW↔Unity** (проверена на пустышках, 0.000 м): `PRIMUM-local = (−x_raw, z_raw, −y_raw)`; корень `PRIMUM` в мире: позиция (39460, 2475.67, 40000), поворот Y=180°, масштаб 1.
4. **Эталон для сверки:** `ADMIN_FOR_COLLIDER` — дети с `localScale=100` (компенсация fileScale=0.01 FBX), позиции в метрах. Детерминированные точки: `ADM_DOOR_L` мировой центр (39381.0, 2502.5, 40021.1), `ADM_DOOR_L_IRON` (39380.7, 2502.4, 40020.2), пустышки `E_ADM` и `Empty..Empty.014` (16 шт) — их мировые позиции совпадают с чанком.

## Как чинить (рекомендация следующему агенту)

1. Базовый размер/ориентацию брать **только из ADMIN_FOR_COLLIDER** (полотна `ADM_DOOR_*`, `ADM_CONN_*_LEAF` и т.д.), не из bbox рам чанка.
2. Пересчитать масштаб: например, для проёма использовать ширину/высоту **полотна** из RAW, сопоставленного с проёмом (или ширину проёма RAW из CSV, но с верным клирингом).
3. Позицию брать из CSV `admin_doorways.csv` (координаты RAW) + проверять визуально по `ADMIN_FOR_COLLIDER`.
4. После правок — сверить 3–5 дверей вручную по объектам `ADMIN_FOR_COLLIDER` (полотно должно стоять в плоскости проёма, по высоте от пола этажа).

## Каталог сцен и объектов

Сцена: `C:\Projects\ProjectC_client\Assets\_Project\Scenes\World\WorldScene_0_0.unity`
- `PRIMUM/PRIMUM_ADMIN_S0_ADM_P1_01H` — сджойненный игровой вариант (не трогать).
- `PRIMUM/ADMIN_FOR_COLLIDER` — RAW-админка 1:1 (**эталон**; пользователь выключит сам).
- `PRIMUM/ADMIN_ENTRANCE_PORTAL` — префаб главного входа (8 объектов, включая стекло).
- `PRIMUM/ADMIN_DOOR_PREFABS` — 215 дверей:
  - `ADM_DOOR_*` (123 шт, из запроса 1),
  - `ADM_INT_DOOR_F1..F4_*` (92 шт, из запроса 2).

## Абсолютные ссылки на файлы

- Префабы дверей: `C:\Projects\ProjectC_client\Assets\_Project\Prefabs\City\Doors\` — `PM_DOOR_SINGLE_ARCH_C.prefab`, `PM_DOOR_SINGLE_PANEL_B.prefab`, `PM_DOOR_SINGLE_PLANK_A.prefab`, `PM_DOOR_DOUBLE_A.prefab`, `PM_ADM_ENTRANCE_PORTAL.prefab`. Все *.prefab под gitignore проекта (живут локально).
- Модель для коллайдеров: `C:\Projects\ProjectC_client\Assets\_Project\Art\Models\Primum\TempCollider\ADMIN_FOR_COLLIDER.fbx` (11.27 МБ, 2758 мешей, без джойна).
- RAW-админка: `Q:\Yandex.disk\Yandex.Disk\Project-C\models\primum\New Folder\PRIMUM_chunks_RAW\PRIMUM_ADMIN_S0_ADM_P1_01H.blend`
- Скрипты (Blender, headless):
  - `Q:\Yandex.disk\Yandex.Disk\Project-C\models\primum\New Folder\_work\export_admin_collider.py` — экспорт RAW→ADMIN_FOR_COLLIDER (масштаб S=1.07 около C=(7.5,−148,18.5) запечён в меши, как в пайплайне).
  - `Q:\...\_work\detect_doorways3_admin.py` — детектор проёмов (работает).
  - `Q:\...\_work\detect_doorways_admin.py`, `Q:\...\_work\detect_doorways2_admin.py` — неудачные попытки (внутри-мешевый тест и разрывы между сегментами стен; оставлены для истории).
  - `Q:\...\_work\floor_map_admin.py` — диагностические ASCII-карты этажей (полезно смотреть проёмы глазами).
- Данные: `Q:\...\_work\logs\admin_doorways.csv`, `Q:\...\_work\logs\detect_doorways3.log`, `Q:\...\_work\logs\export_admin_collider.log`, `Q:\...\_work\logs\floor_map.log`.
- Единый пайплайн дверей города: `Q:\...\_work\run_all_doors_v13.py` (к этому запросу не применялся напрямую, но из него взяты K=1.07/C и настройки FBX-экспорта).

## Коммиты

- `1158d848` — ADMIN_FOR_COLLIDER (модель + постановка в сцену).
- `59515f23` — PM_ADM_ENTRANCE_PORTAL + 123 двери в рамы.
- `db8d5e15` — 92 внутренние двери в найденные проёмы.

## Исправление (2026-10-23, агент)

Удалены все 215 дверей из `PRIMUM/ADMIN_DOOR_PREFABS` (123 старые + 92 внутренние),
которые были расставлены с ошибочными размерами/позициями. Заново расставлены **123 двери**
по реестру `doors_registry_ADMIN_S0_ADM_P1_01H.csv` (только строки с `_FRAME_`):

- тип по этажу: вестибюль/ground = ARCH_C, F1 = PANEL_B, F2 = PLANK_A, F3 = ARCH_C, F4 = PANEL_B;
- позиция = `pos_x/pos_y/pos_z` из реестра (низ-центр проёма), конвертация в PRIMUM-local;
- поворот = `rot_y` реестра (180°) → локально 0°;
- масштаб = `w / ширина_префаба`, `h / высота_префаба`, `1` по глубине (без вычитания полей);
  префаб: PLANK_A 0.96×2.10, PANEL_B 1.06×2.15, ARCH_C 1.20×2.36.

Префаб `PM_ADM_ENTRANCE_PORTAL` (главный вход) не трогался — сверка 0.000 м.
Внутренние проёмы (92) оставлены без дверей: в RAW там нет геометрии рам.

## Проверки, которые уже сделаны (можно не повторять)

- ADMIN_FOR_COLLIDER vs чанк: 16 пустышек `E_ADM`/`Empty*` — смещение 0.000 м.
- Дверь RAW `ADM_DOOR_L` vs `ADM_DOOR_L_12.63x4.17_X` чанка: X/Z совпадают (пивот чанка внизу, ΔY≈1.5 м), минимальное расстояние дверь↔дверь 0.25 м.
- 92 новые двери не пересекаются с уже стоящими (проверка «покрытие» по центрам габаритов, порог 1.3 м, пересечений 0).
