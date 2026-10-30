# beam_research — исследование culling для Project C (город PRIMUM)

> Каталог: `docs/world/primum_city/beam_research/` · Дата: 2026-10-30
> Статус: исследование (research-only, код не менялся). Проведено сабагентами по трём профильным аспектам + синтез.

## 0. Цель

Пользователь: "мне нужно, чтобы ты с помощью сабагентов провёл детальное и точное исследование на тему culling. Мы сделали первые тесты бейка города с целью оптимизации (`04_OCCLUSION_CULLING_RESEARCH.md`) и выявили проблемы. В интернете много ассетов (платных и бесплатных) на тему улучшения Unity culling. Задача — провести ресерч с целью: мы можем сами написать ещё лучше оптимизацию под наши условия. Мы много времени провели на склеивание большого города из кучи простых объектов — это была лишь попытка и начало, и мне кажется оно заводит не туда, можно было не склеивать город и каждый объект оставить независимым, а всё решить путём отключения ненужных комбо слоёв+culling, так как у нас нет тяжёлых мешей. Тоже самое мы бы могли делать с тяжёлыми кораблями, что хранят кучу объектов. Ресерч нужен не быстрый и поверхностный, а детальный, со сводкой сабагентов профильных по нашим аспектам, включая FO большой мир и наполненность сцены. Задача игроку отрисовывать то, куда смотрит — остальное ему ведь не нужно — это лишь координаты и состояния, зачем игроку это рисовать он не будет везде и сразу."

## 1. Состав каталога

| Файл | Содержание |
|---|---|
| `00_README.md` (этот файл) | Индекс + цель + краткий вывод |
| `01_our_city_optimization.md` | Исследование нашей оптимизации города: скрипты, архитектура, FO, scene streaming, light pool, detail culler, куллинг, LOD, rendering, бюджеты, постмортемы |
| `02_internet_culling_assets.md` | Интернет-ресерч ассетов/методов culling Unity (paid/free, occlusion, frustum, distance, LOD, HLOD, impostor, batching, GPU occlusion) с таблицами и ссылками |
| `03_floating_origin_big_world.md` | Floating Origin и большие миры в Unity: FO, streaming, LOD, HLOD, impostor, cell-based, spatial partitioning, interest management, server authority, DOTS, FO-классы, пайплайн нового контента |
| `04_synthesis_and_plan.md` | Синтез: что писать самим, что покупать, многоуровневый pipeline, этапы, budgets, FO-классы, что НЕ делать |
| `05_write_ourselves_no_purchase.md` | **Видение «пишем сами, ничего не покупаем»:** детальная архитектура spatial partitioning (quadtree/spatial hash), portal-based culling, distance-based culling manager, impostor system (bake + switching без Amplify), HLOD (merged mesh bake), custom Hi-Z (опционально), интеграция с FO, этапы и оценки |
| `06_no_join_massive_objects.md` | **«А что если без склейки?»:** массовые независимые объекты, физика (PhysX vs DOTS), разрушаемость (pre-fractured/voxel/runtime boolean), корабли, двери, пропсы, DOTS/ECS отсутствие, GPU instancing/BRG статус, стриминг, budgets, примеры игр (Minecraft/Teardown/Fortnite/Rust/Valheim), что возможно/невозможно без склейки |
| `07_ships_unmerged_destruction.md` | **Корабли без склейки + разрушаемость:** текущая архитектура (PhysX+NGO), корпус/HP/урон, коллайдеры (shipMode, compound, подсчёт по префабам), двери, груз, NavMesh, Rigidbody, сеть/FO, pre-fractured (Fortnite-стиль), DOTS vs PhysX (почему не совмещать), «не нагружаемая» разрушаемость (пул кусков, debris lifetime, sleep states), совмещение PhysX root + DOTS chunks (не рекомендуется), реалистичные потолки без DOTS |

## 2. Краткий вывод (TL;DR)

1. **Склеивать город было лишней попыткой и началом.** Э4 закрыл зернистость (19 944 → 1 207 мешей), но draw calls остались 26 981–26 996 против бюджета 120. Склейка не снимает draw calls. Draw calls снимаются **GPU-driven рендером + LOD/HLOD + импосторами**. Каждый объект можно оставить независимым и решать всё через отключение ненужных слоёв/комбо + culling, потому что тяжёлых мешей у нас нет (медиана 1 100 вершин на рендерер). То же самое — для тяжёлых кораблей: не склеивать, а куллить.
2. **Umbra baked occlusion не подходит** (статик запрещён FO + бейк провален + террейн невидим). Нужен **runtime culling pipeline**.
3. **GPU Resident Drawer выключен** из-за кастомных шейдеров без `DOTS_INSTANCING_ON` — блокер Э5.
4. **0 LODGroup** в сцене → дальние объекты рисуются полностью. Э6 не начат.
5. **Floating Origin запрещает статику** → culling обязан быть динамическим. Контент под корнями `WorldScene_*` едет бесплатно → HLOD/импосторы должны быть детьми корня сцены и хранить только скаляры (FO-класс 🟢).
6. **Мы можем написать свою оптимизацию лучше**, чем покупать ассеты: portal-based culling для интерьеров + spatial partitioning (quadtree/spatial hash) + distance-based culling manager + impostor switching (Amplify bake) + custom Hi-Z (опционально). Покупаем только Amplify Impostors ($30) и используем бесплатные NativeTrees/Spatial-Hashing.

## 3. Что покупать / что не покупать / что писать самим

**Покупать:**
- Amplify Impostors ($30, скидка 50%) — золотой стандарт для URP, активно обновляется.
- NativeTrees (MIT, бесплатно) — Burst octree/quadtree.
- Spatial-Hashing (MIT, бесплатно) — spatial hash для динамики.

**Не покупать:**
- Perfect Culling ($97) — устаревший Unity 2019.4, bake-based.
- Frustum Culling ($20) — избыточно.
- Per-Layer Camera Culling (бесплатно) — проблемы с Cinemachine.
- Ultimate Optimization Bundle ($80) — устаревший.
- HLOD System (GitHub, 2021.x) — устаревший; референс.
- Floating Origin Network — мы уже реализовали свой FO.

**Писать самим:**
1. Portal-based culling (интерьеры) — 2–4 недели.
2. Spatial partitioning integration (NativeTrees) — 1 неделя.
3. Distance-based culling manager — 1 неделя.
4. Impostor switching logic — 1 неделя.
5. Custom Hi-Z (опционально) — 4–8 недель.

## 4. Многоуровневый pipeline

```
Уровень 0: Scene streaming (уже есть) — ClientSceneLoader.
Уровень 1: Spatial partitioning — quadtree (статика) + spatial hash (динамика) по XZ.
Уровень 2: Frustum culling (built-in + camera-relative) — бесплатно.
Уровень 3: Distance culling / LOD — Mesh LOD + LOD Group.
Уровень 4: Occlusion culling — custom (portal-based для интерьеров + Hi-Z/software для экстерьера).
Уровень 5: Impostor switching — Amplify bake для дальних объектов.
Уровень 6: GPU instancing — GPU Resident Drawer (для совместимых объектов).
```

## 5. Этапы (предложение, требует одобрения)

- **C0 (0–2 недели, бесплатно):** включить GPU Resident Drawer + GPU Occlusion Culling в URP, Camera-Relative Culling, Mesh LOD, NativeTrees.
- **C1 (2–4 недели):** адаптация шейдеров (Э5) → включить Drawer.
- **C2 (4–8 недель):** LOD/HLOD/Impostors (Э6) → Amplify bake.
- **C3 (4–8 недель):** Portal-based culling для интерьеров.
- **C4 (6–12 недель, опционально):** Custom Hi-Z.
- **C5 (параллельно):** Корабли (LOD + impostor + culling + pooling).

## 6. Ограничения и budgets

| Критерий | Норма |
|---|---|
| Draw calls (L0) | ≤ 120 |
| Зернистость | PRIMUM ≤ ~1 200 мешей (✅ 1 207), дверь = 2 рендерера (✅), чанк ≤ ~110 (✅ 104) |
| Дальность | 0.5 км полный, 5–10 км HLOD, 20–80 км 1–2 вызова |
| Ребейз | F8/F9: город, физика, двери, огни, HLOD/импостор едут; 0 errors |

## 7. Что НЕ делать

- Не использовать Umbra (baked occlusion) — несовместим с FO и динамикой.
- Не мигрировать на DOTS/Entities — мы на NGO GameObjects.
- Не покупать Perfect Culling / Frustum Culling / Ultimate Optimization Bundle.
- Не трогать ProjectSettings/Packages без согласования.
- Не склеивать город дальше (Э4 закрыт).
- Не запускать Play Mode / скриншоты (делает пользователь).
- Не писать `.meta` / `.asmdef` руками.

## 8. Источники

- `01_our_city_optimization.md`
- `02_internet_culling_assets.md`
- `03_floating_origin_big_world.md`
- `docs/world/primum_city/ROADMAP.md`
- `docs/world/primum_city/06_REFERENCE/04_OCCLUSION_CULLING_RESEARCH.md`
- `docs/world/primum_city/06_REFERENCE/01_INDUSTRY_PRACTICE.md`
- `docs/world/floatingorigin/09S_NEW_CONTENT_PIPELINE.md`
- `docs/world/optimization/LOD_VIEW_DISTANCE_DESIGN.md`

---

*Исследование подготовлено как исследовательский материал. Код не написан; рекомендации применяются через отдельные тикеты с одобрения пользователя.*
