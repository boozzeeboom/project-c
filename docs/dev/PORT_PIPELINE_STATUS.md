# PRIMUM_PORT — пайплайн оптимизации: состояние работ

**Префаб:** `Assets/_Project/Prefabs/City/Port/PRIMUM_PORT.prefab` (0,77 МБ) · root `PRIMUM_PORT`, scale `1.060`, 1 ребёнок `gorod_port_model`
**Инструмент:** `Assets/_Project/Editor/Primum/PrimumPortPipeline.cs`, меню `ProjectC → World → PRIMUM PORT → …`
**Бэкап (вне Assets, не в git):** `_local_backups/MAIN_PIPELINE/PRIMUM_PORT/PRIMUM_PORT.prefab.pre-opt.bak` (+ копия с меткой `_2026-11-04_162945`), снят **до** любых правок — то есть с уже подготовленными пользователем коллайдерами.
**Отчёт:** `docs/world/primum_city/10_CULLING_RESEARCH/08_DATA/W_port_pipeline_2026-11-04.csv`

---

## Исходное состояние (замерено до правок)

| Параметр | Значение |
|---|---|
| Трансформы / рендереры / меши | 5 444 / 4 745 / 4 745 |
| Материалы | **163**, все `Universal Render Pipeline/Lit`, 2 прозрачных (`MAT_ADMIN_GLASS_BLUE`, `MAT_ADMIN_SERVICE_GLASS`) |
| Коллайдеры | **410 `BoxCollider`** на узлах-держателях `BC_*` (414 узлов) + **8 `MeshCollider`** на узлах стекла входа ADMIN |
| Двери | `DoorController` — **0** (двери существуют только как геометрия FBX) |
| Свет | `Light` — **0** |
| Сабмешей > 1 | 1 рендерер (2 сабмеша) |

---

## Выполнено: шаг «Материалы» (apply + verify, PASS)

163 исходных материала → **22 материала палитры** (гейт COMMON.md §5.2 ≤24), 4 746 слотов переназначено.
Новые материалы не создавались: цветовые вариации взяты **готовыми дочерними Material Variant** (`m_Parent`), как требует COMMON.md §5.

| Проверка | Результат |
|---|---|
| слотов `URP/Lit` после | **0** |
| различных материалов на ассете | **22** |
| пустых слотов | 0 |
| стекло (`*GLASS*`/`*WINDOW*`) на `M_PC_Glass_Green` | 124 слота |
| материал лампы на стекле | **0** |
| `PM_LAMP_GLOW_WARM` всего | 134 слота — только приборы (`WarmLamp`, `WorkLight`, `Dock_Light`, `Service_Lamp`, `Interior_Lamp`, `AntiGravGlow`) |
| коллайдеры | 410 box + 8 mesh — без изменений |
| root scale | 1.060 — без изменений |

Распределение по палитре: `Steel_v2_Dark` 1142, `Steel_v2` 624, `Steel_v2_Safety` 348, `Steel_v2_Light` 297,
`Wood_Pine` 521, `Bronze` 420, `IronRust` 239, `Concrete` 214, `Glass_Green` 180, `Lamp` 134,
`Fabric_Canvas` 108, `Plastic_Blue` 100, `Plastic_Rubber` 29, `Plate` 14, `Wood_Dark` 12, `Concrete_Limewash` 11,
`Bronze_Brass` 9, `IronRust_Lead` 6, `Marble` 4, `Wood` 331, `Cobble` 2, `Cobble_Granite` 1.

**Сознательные ограничения шага:**
- подсветка вывесок (`HUB2_MAT_Nav_Text`, `HUB2_MAT_Sign_Text`, `HUB3_MAT_Interior_Text`, `MD2_MAT_Sign_Text_Stage03`)
  переведена на `M_PC_Steel_v2_Light`, то есть **потеряла эмиссию** — в палитре нет отдельного эмиссивного
  материала вывески, а материал лампы по правилу ставится только на приборы;
- прозрачность `MAT_ADMIN_*_GLASS` ушла вместе с заменой на `M_PC_Glass_Green` (палитра Glass — очередь 3000).

---

## Принятые решения по дверям (подтверждены пользователем)

1. **8 ворот → «ангарные»**: все `RollingPanel` кроме двух пешеходных —
   `HUB2_South_Gate_01…04`, `HUB2_East_Service_Gate_01/02`, `HUB2_West_Service_Gate_01/02` (14,12 × 8,93 и 12,00 × 7,65 м).
   Пешеходные `HUB2_North_Pedestrian_Left/Right` не трогаем. Оформление — **префаб из геометрии ангаров**
   (`HANGAR_*_CargoDoor_Left/Right`, 1,70 × 17,66 м), а не новая параметрическая дверь.
2. **Одиночные двери → префабы `PM_DOOR_*`**: **все одностворчатые** — 7 проёмов ADMIN без полотна
   (`WEST_OFFICE_01…04`, `EAST_CREW`, `EAST_DISPATCH`, `EAST_SANITARY`) + 32 служебные двери ангаров
   (`HANGAR_001…008` × `East/West` × `PersonnelDoor/TechDoor`).

**Открытый риск шага дверей:** служебные двери ангаров в модели огромные (≈5,5 м по высоте, полотно 1,48–1,59 м),
поэтому `PM_DOOR_*` (2,10–2,40 м) требуют масштаба ≈0,4; проверять посадку надо по OBB полотна вдоль его собственных
осей, а не по мировому AABB (полотна повёрнуты, AABB врёт).

---

## Осталось

| # | Шаг | Состояние |
|---|---|---|
| 3 | Двери: аудит проёмов (CSV) → замена одностворчатых префабами | не начат (нужен живой эдитор) |
| 4 | Префаб ангарных ворот из геометрии `HANGAR_*_CargoDoor_*` + инстансы на 8 ворот | не начат |
| 5 | Свет: префабы `PM_LIGHT_*` на реальные приборы, имя `PRIMUM_OUTLIGHT_*`/`PRIMUM_INLIGHT_*`, `enabled=false`, range 3,5–14 м, `shadows=None` | не начат |
| 6 | Мерж визуала по `(группа × материал)`, cap 60 000 верт., защита дверей/коллайдеров/8 узлов стекла/света, `.asset` в git | не начат |
| 7 | Verify + `W_port_pipeline` CSV + коммит (push не выполнять) | не начат |

**Блокер:** MCP-мост к эдитору завис на `Suppressed duplicate command resent on a new connection;
awaiting the in-flight result instead of it running twice` — эдитор не отвечает ни на один инструмент,
CLI не видит эдитор (пакет Pipeline не установлен). Лечится перезапуском Unity;
данные не теряются — префаб сохранён, шаг материалов зафиксирован в отчёте.
