# T-SHIPPIPE01 — ремонт префабов `Ship_Light` / `Ship_medium` и перевод кораблей на процедурную палитру

Дата: 2026-11-02
Ветка: рабочее дерево `ProjectC_client`, Unity 6000.5.2f1 / URP 17.5.0 Forward+
Сцена: `WorldScene_0_0.unity` — **не изменялась**, `isDirty = false`.

---

## 1. Задача

1. Проверить и починить `Ship_Light_root_fbx_test.prefab` и `Ship_medium_base.prefab` так,
   чтобы визуал можно было безопасно переассайнить на другие корабли.
2. Перевести все три корабля (`Light`, `Medium`, `havyII`) с FBX-embedded материалов
   `Universal Render Pipeline/Lit` на существующие процедурные Shader Graph палитры.
   Новых базовых материалов не создавать — только цветовые наследники.

---

## 2. Что было измерено до правок

| Метрика | Light | Medium | HeavyII (эталон) |
|---|---|---|---|
| root scale | (6, 0.900, 12) | (6, 0.900, 12) | (6, 0.900, 12) |
| root world pos | (40122.10, 2502.36, 39975.20) | — | — |
| MeshRenderer | 428 | 118 | 616 |
| Collider | 17 | 6 | 250 |
| DoorController | **2 (вложенная пара)** | 1 | 9 |
| user `*_Colliders` внутри FBX | **10 деревьев** | **1 дерево** | 0 (уже вынесено в H1) |
| `SHIP_COLLIDERS` | отсутствовал | отсутствовал | присутствовал |
| `GlobalSceneSourceMarker` на корне | **отсутствовал** | присутствовал | присутствовал |
| material slots | 429 (1 null) | 123 (1 null) | 620 (1 null) |
| уникальных материалов | 16 | 15 | 15 |
| unmapped материал без правила | 0 | 0 | 0 |

### 2.1 Найденные дефекты

**Дверь light — двойное движение.** `CABIN_ENTRY_DOOR_PIVOT` и её ребёнок
`CABIN_ENTRY_DOOR_LEAF` оба несут `DoorController`. `DoorController` при
`doorModel == null` двигает **свой** transform (`SinglePanel = doorModel ?? transform`),
поэтому створка ехала дважды: движение пивота + собственное движение листа.

**Коллайдеры внутри FBX.** User-созданные деревья `<NODE>_Colliders/Root/BC_0000_*`
являются overrides на узлах nested FBX-инстанса. Переимпорт модели или замена визуала
сносит такие overrides — именно то, что мешало переассайнить визуал.

**`GlobalSceneSourceMarker` отсутствует на light-префабе.** Сценический инстанс несёт его
только как added-component override, то есть новый инстанс префаба не получил бы
`_sourceId`. **Не исправлено** — см. §5.

---

## 3. Что сделано

Инструмент: `Assets/_Project/Editor/Ships/ShipPipelineFixer.cs`
(меню `ProjectC → Ships → Pipeline → …`, отчёт `docs/dev/T-SHIPPIPE01_SHIP_PREFABS.csv`,
бэкапы `_local_backups/T-SHIPPIPE01/`).

### 3.1 Структурный ремонт (Light + Medium)

- Создан корень `SHIP_COLLIDERS` под prefab-корнем.
- В него перенесены все user-деревья `*_Colliders`, лежавшие внутри FBX-инстанса:
  **Light — 10 деревьев (10 коллайдеров), Medium — 1 дерево (1 коллайдер).**
  Перенос с сохранением мировой позы (`SetParent(..., true)`).
- Safety-gate перед переносом: дерево не должно содержать ничего, кроме
  `Transform`/`Collider`. Визуал, скрипты и свет не переносятся.
- Валидация в памяти до сохранения: геометрия коллайдеров в координатах prefab-корня
  не должна измениться (допуск `5e-3`, см. §6), внутри FBX не должно остаться
  user-деревьев, число `DoorController` должно совпасть с ожидаемым, у каждой двери
  должен быть коллайдер. **При провале префаб не сохраняется.**

### 3.2 Дедупликация двери (Light)

Снят `DoorController` с `CABIN_ENTRY_DOOR_LEAF`. Оставлен верхний — на
`CABIN_ENTRY_DOOR_PIVOT`: он везёт и створку, и собственный `MeshCollider` проёма.
Обратный вариант (убрать пивот) оставил бы статичное перекрытие в проёме.

**Коллайдеры не удалялись ни на одном узле:** `MeshCollider` проёма на пивоте и
`BoxCollider` створки на листе сохранились. Итог: 17 коллайдеров до = 17 после.

### 3.3 Материалы: FBX/Lit → процедурная палитра

Переведены все три корабля, 100 % слотов, `unmapped = 0`:

| Light | Medium | Heavy |
|---|---|---|
| renderers=427, slots=428 | renderers=117, slots=122 | renderers=615, slots=619 |

Соответствие (правила упорядочены, специфичные раньше общих):

| Исходный материал | → | Материал палитры |
|---|---|---|
| `Metal_Brass`, `Metal_Bronze` | → | `M_PC_Bronze_Brass` |
| `*_Bronze` | → | `M_PC_Bronze` |
| `Metal_Bronze_Dark` | → | `M_PC_Bronze_Dark` **(новый)** |
| `*_Steel`, `Lit` | → | `M_PC_Steel_v2` |
| `Metal_Steel_Dark` | → | `M_PC_Steel_v2_Dark` **(новый)** |
| `*_Steel_Light` | → | `M_PC_Steel_v2_Light` **(новый)** |
| `*_Safety_Yellow` | → | `M_PC_Steel_v2_Safety` **(новый)** |
| `*_Painted_Hull` | → | `M_PC_Steel_v2_Hull` **(новый)** |
| `*_Painted_Hull_Dark` | → | `M_PC_Steel_v2_Hull_Dark` **(новый)** |
| `Metal_Rust_Worn`, `Smoke_Soot` | → | `M_PC_IronRust` |
| `Deck_Plating`, `Metal_Reflector` | → | `M_PC_Plate` |
| `Glass_Green`, `Glass_Green_Matte`, `*_Cabin_Glass` | → | `M_PC_Glass_Green` |
| `Lamp_Red` | → | `M_PC_Lantern_Red` **(новый)** |
| `Lamp_Green` | → | `M_PC_Lantern_Green` **(новый)** |
| `Lamp_White`, `*_Gravity_Core`, `AntiGrav_Emitter` | → | `M_PC_Lantern_Cool` |
| `*_Worklight`, `Glass_Lamp_Warm` | → | `M_PC_Lantern` |
| `*_Rubber`, `Rubber_Cable`, `*_Cable_Insulation` | → | `M_PC_Plastic_Rubber` **(новый)** |
| `*_Cable_Blue` | → | `M_PC_Plastic_Blue` **(новый)** |
| `*_Cable_Red` | → | `M_PC_Plastic_Red` **(новый)** |
| `*_Canvas` | → | `M_PC_Fabric_Canvas` |
| `<null>` | → | оставлен `<null>` (отключённый корневой `Cube`) |

Создано 11 цветовых наследников (все — на существующих графах палитры):

| Файл | Граф | Базовый материал |
|---|---|---|
| `Steel/M_PC_Steel_v2_Light.mat` | `Shader Graphs/Steel_v2` | `M_PC_Steel_v2` |
| `Steel/M_PC_Steel_v2_Dark.mat` | `Shader Graphs/Steel_v2` | `M_PC_Steel_v2` |
| `Steel/M_PC_Steel_v2_Hull.mat` | `Shader Graphs/Steel_v2` | `M_PC_Steel_v2` |
| `Steel/M_PC_Steel_v2_Hull_Dark.mat` | `Shader Graphs/Steel_v2` | `M_PC_Steel_v2` |
| `Steel/M_PC_Steel_v2_Safety.mat` | `Shader Graphs/Steel_v2` | `M_PC_Steel_v2` |
| `Bronze/M_PC_Bronze_Dark.mat` | `Bronze` | `M_PC_Bronze` |
| `Lantern/M_PC_Lantern_Red.mat` | `Lantern` | `M_PC_Lantern` |
| `Lantern/M_PC_Lantern_Green.mat` | `Lantern` | `M_PC_Lantern` |
| `Plastic/M_PC_Plastic_Rubber.mat` | `Plastic` | `M_PC_Plastic` |
| `Plastic/M_PC_Plastic_Blue.mat` | `Plastic` | `M_PC_Plastic` |
| `Plastic/M_PC_Plastic_Red.mat` | `Plastic` | `M_PC_Plastic` |

Цвета взяты из исходных FBX-материалов (например `Safety_Yellow` = 0.865/0.650/0.221,
`Painted_Hull` = 0.304/0.437/0.473, `Rubber` = 0.172/0.190/0.190,
`Lamp_Red` glow = 0.955/0.248/0.248), чтобы облик кораблей не поменялся.

### 3.4 Правка R1 — вся бронза heavy на дочку `M_PC_Plate`

Решение пользователя: на `Ship_havyII_base` вся бронза переводится на **один**
новый материал `Plate/M_PC_Plate_Bronze.mat` — дочку `M_PC_Plate` (тот же Shader Graph
`Plate`). Свет и medium не затронуты.

| Корабль | Затронуто слотов | Итог |
|---|---|---|
| light | 0 | файл не перезаписан |
| medium | 0 | файл не перезаписан |
| heavy | **414** | `M_PC_Bronze` ×6, `M_PC_Bronze_Brass` ×253, `M_PC_Bronze_Dark` ×155 → `M_PC_Plate_Bronze` |

Свойства цвета **не переопределялись** — дочка наследует родителя (`_Plate_Color`
0.500/0.320/0.160, `_Bare_Color` 0.750/0.600/0.380, `_Seam_Color` 0.200/0.170/0.140).
Тон бронзы теперь задаётся палитрой Plate, а не прежними яркими FBX-цветами.
Отдельный материал (а не сам `M_PC_Plate`) нужен, чтобы бронзу можно было тюнить,
не трогая `Deck_Plating` и `Metal_Reflector`, которые остались на `M_PC_Plate` (22 слота).

**Страж идемпотентности.** Имена палитры содержат те же токены, что и FBX-источники
(`m_pc_steel_v2_dark` → `_steel`, `m_pc_bronze_brass` → `_bronze`), поэтому повторный
прогон правил схлопнул бы Steel-варианты в базовый `M_PC_Steel_v2` (117 + 8 + 4 слота)
и перезаписал бы уже готовый результат. Введены два ограничения:

1. `IsPaletteMaterial(m)` — материал, лежащий в `Assets/_Project/Materials/`, повторно
   не переводится (после первого назначения все материалы палитровые);
2. `MatchShipOverride(name, shipKey)` — по-корабельные переопределения
   (`heavy`: `metal_bronze_dark`, `metal_bronze`, `metal_brass`, `_bronze` → `PlateBronze`)
   срабатывают **до** стража, поэтому смена решения проходит и на палитровых именах.

Проверка после применения: `M_PC_Plate_Bronze` — shader `Plate`, `isSupported = true`,
цвета совпадают с родителем; heavy: 620 слотов, `urpLit = 0`, старых `M_PC_Bronze*` = 0,
616 `MeshRenderer`, 250 коллайдеров, 9 дверей, root scale (6, 0.900, 12).

---

## 4. Верификация (read-only, `ProjectC → Ships → Pipeline → Verify`)

| Проверка | Light | Medium | Heavy |
|---|---|---|---|
| `collider_trees_inside_fbx == 0` | PASS | PASS | PASS |
| `door_controllers` | PASS (1) | PASS (1) | PASS (9) |
| `nested_door_controllers == 0` | PASS | PASS | PASS |
| `degenerate_colliders == 0` | PASS | PASS | PASS |
| `urp_lit_slots == 0` | PASS | PASS | PASS |
| `missing_scripts == 0` | PASS | PASS | PASS |

Инварианты после правок:

| | Light | Medium | Heavy |
|---|---|---|---|
| root scale | (6, 0.900, 12) | (6, 0.900, 12) | (6, 0.900, 12) |
| root `MeshRenderer` (отключён) | сохранён | сохранён | сохранён |
| MeshRenderer | 428 | 118 | 616 |
| Collider | 17 | 6 | 250 |
| `SHIP_COLLIDERS` коллайдеров | 10 | 1 | 238 |

Компиляция: `hasCompilationErrors = false`. Сцена: `isDirty = false`, не сохранялась.

---

## 5. Что осознанно НЕ сделано

- **`GlobalSceneSourceMarker` в light-префаб не добавлен.** Сценический инстанс
  `Ship_Light_root` на `WorldScene_0_0` уже несёт компонент как
  added-component override. Бейк компонента в префаб дал бы на сцене дубликат
  `[DisallowMultipleComponent]`. Требуется синхронная правка префаба + снятие override
  + сохранение сцены — отдельное решение.
- **`MainVisual`-обёртка не вводится.** Имя упоминается только в `ShipPresetCreator`
  и документации; рантайм его не читает, поэтому обёртка ничего не даёт, а перенос
  FBX-инстанса глубже меняет все пути.
- **Слияние визуала и `LODGroup`** — это H2 и отдельное решение по бюджетам DC.
  Без слияния бюджеты `S ≤ 30` / `M ≤ 75` DC не достигаются (модель: Light ≈ 1 284,
  Medium ≈ 354). Это не регрессия: состояние до и после одинаково.
- **`shadowCastingMode`** в edit mode не трогался.
- **`WorldScene_0_0.unity`** не изменялась и не сохранялась.
- **Play Mode не запускался**; визуальная приёмка палитры — за пользователем.

---

## 6. Технические заметки

- **Допуск сравнения поз = `5e-3` в координатах prefab-корня, не `1e-4` как в H1.**
  Корень light-префаба авторится на мировых ~`4.0e4`; float даёт там шаг ≈ 5e-3,
  и перенос через мировое пространство смещает центр на ~6.5e-4 в координатах корня.
  Гейт `1e-4` ловил шум округления (на нём первый прогон корректно отменился, ничего
  не сохранив). Реальная поломка (сбитый пивот, потерянный поворот) — это метры.
- **Бэкап «первая запись побеждает»**: повторный apply не затирает исходный файл.
- **Порядок правил материалов важен:** `_painted_hull_dark` раньше `_painted_hull`,
  `_steel_light` раньше `_steel`, `metal_bronze_dark` раньше `metal_bronze`.
- **`new Material(parent)` в Unity — не Material Variant.** `Material.parent == null`,
  `m_Parent` сериализуется только у уже созданного варианта, а публичного API
  связывания нет (проверено рефлексией по загруженным сборкам). Материалы независимы,
  но несут тот же Shader Graph.
- **`AssetDatabase.FindAssets(name + " t:Material")`** исключает `ARCHIVE_NOTUSED`
  и `SMALL_*`, иначе `M_PC_Bronze` резолвился бы в архивный дубль.
- Отчёт и CSV лежат вне `Assets/` (`docs/dev/`), бэкапы — в `_local_backups/`.

---

## 7. Откат

`ProjectC → Ships → Pipeline → Rollback from backups` восстанавливает все три префаба
из `_local_backups/T-SHIPPIPE01/*.prefab.bak`. Созданные 12 материалов (11 из первого
прохода + `M_PC_Plate_Bronze`) откат не удаляет — их нужно удалить вручную, если
палитра будет пересмотрена. Бэкап first-write-wins: для heavy в нём лежит исходное
состояние с FBX-материалами, поэтому откат вернёт и бронзу на прежние материалы.

## 8. Git

`.gitignore:153: *.prefab` — изменения самих префабов в коммит не попадут.
В коммит попадают: `ShipPipelineFixer.cs`, 11 `.mat`, `T-SHIPPIPE01_SHIP_PREFABS.csv`,
этот отчёт. Force-add префабов не выполнялся. Push не выполнялся.
