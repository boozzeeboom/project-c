# 04. Синтез и план: собственная оптимизация culling под наши условия

> Дата: 2026-10-30. Синтез трёх исследований: нашей оптимизации города (`01`), интернет-ресерча ассетов/методов (`02`), Floating Origin/большой мир (`03`). Цель: мы можем сами написать ещё лучше оптимизацию под наши условия. Не склеивать город (Э4 уже сделано), не бейкать Umbra. Динамический culling + GPU-driven + LOD/HLOD + impostors + portal-based.

## 0. Главный вывод

1. **Склеивать город было лишней попыткой и началом.** Э4 закрыл зернистость (19 944 → 1 207 мешей), но draw calls остались 26 981–26 996 против бюджета 120. Склейка не снимает draw calls — она снимает CPU-оверхед на объект. Draw calls снимаются **GPU-driven рендером + LOD/HLOD + импосторами**, а не склейкой. Каждый объект можно оставить независимым и решать всё через отключение ненужных слоёв/комбо + culling, потому что тяжёлых мешей у нас нет (медиана 1 100 вершин на рендерер, p90 23 982). То же самое — для тяжёлых кораблей, хранящих кучу объектов: не склеивать, а куллить.
2. **Umbra baked occlusion не подходит** (статик запрещён FO + бейк провален + террейн невидим). Нужен **runtime culling pipeline**.
3. **GPU Resident Drawer выключен** из-за кастомных шейдеров без `DOTS_INSTANCING_ON` — это блокер Э5. Без него нет GPU-инстансинга и GPU-occlusion.
4. **0 LODGroup** в сцене → дальние объекты рисуются полностью. Э6 не начат.
5. **Floating Origin запрещает статику** → culling обязан быть динамическим (frustum/distance/GPU-occlusion/portal), а не bake. Контент под корнями `WorldScene_*` едет бесплатно → HLOD/импосторы должны быть детьми корня сцены и хранить только скаляры (FO-класс 🟢).
6. **Мы можем написать свою оптимизацию лучше**, чем покупать ассеты: portal-based culling для интерьеров (корабли/здания) + spatial partitioning (quadtree для статики, spatial hash для динамики) + distance-based culling manager + impostor switching (Amplify bake) + custom Hi-Z (опционально). Покупаем только Amplify Impostors ($30) и используем бесплатные NativeTrees/Spatial-Hashing.

## 1. Что покупать / что не покупать / что писать самим

### Покупать (рекомендация)

| Ассет | Цена | Зачем | Совместимость |
|---|---|---|---|
| **Amplify Impostors** | $30 (скидка 50%) | Octahedral impostors для дальних объектов (здания, деревья, камни, корабли). Золотой стандарт для URP, активно обновляется (Unity 6.4/6.5). | Unity 6.4/6.5, URP/HDRP/BiRP |
| **NativeTrees** (MIT, бесплатно) | $0 | Burst-совместимые sparse Octree/Quadtree для DOTS/ECS — база пространственного партиционирования. | Unity 2021.3+ |
| **Spatial-Hashing** (MIT, бесплатно) | $0 | Spatial hashing для ECS/DOTS — для динамики (корабли, NPC, двери). | Unity 2022.2+ |

### Не покупать

| Ассет | Почему |
|---|---|
| Perfect Culling ($97) | Устаревший Unity 2019.4, bake-based (ломается при FO). |
| Frustum Culling ($20) | Избыточно — Unity делает frustum culling сам. |
| Per-Layer Camera Culling (бесплатно) | Проблемы с Cinemachine. |
| Ultimate Optimization Bundle ($80) | Устаревший (2021.3), HDRP не рекомендуется. |
| HLOD System (GitHub, 2021.x) | Устаревший; использовать как референс архитектуры. |
| Precomputed Culling ($150) | Только если нужна pixel-perfect occlusion для интерьеров И есть бюджет; иначе portal-based самому. |
| Floating Origin Network | Мы уже реализовали свой FO. |

### Писать самим

| Задача | Оценка | Приоритет |
|---|---|---|
| **Portal-based culling** (интерьеры: корабли, здания) | 2–4 недели | Высокий |
| **Spatial partitioning integration** (NativeTrees) | 1 неделя | Высокий |
| **Distance-based culling manager** (вкл/выкл объектов по дистанции + LOD switching) | 1 неделя | Высокий |
| **Impostor switching logic** (интеграция с Amplify bake) | 1 неделя | Средний |
| **Custom Hi-Z** (опционально, только если GPU Occlusion недостаточен) | 4–8 недель | Низкий |

## 2. Многоуровневый pipeline (рекомендация)

```
Уровень 0: Scene streaming (уже есть) — ClientSceneLoader грузит сцены по дистанции.
Уровень 1: Spatial partitioning — quadtree (статика) + spatial hash (динамика) по XZ.
Уровень 2: Frustum culling (built-in + camera-relative) — бесплатно.
Уровень 3: Distance culling / LOD — Mesh LOD (встроенный) + LOD Group для спец-объектов.
Уровень 4: Occlusion culling — custom (portal-based для интерьеров + Hi-Z/software для экстерьера).
Уровень 5: Impostor switching — Amplify bake для дальних объектов.
Уровень 6: GPU instancing — GPU Resident Drawer (для совместимых объектов).
```

### 2.1 Custom occlusion: рекомендуемый подход

**Вариант A (рекомендуемый): Portal-based + Hi-Z hybrid**

1. **Portal-based для интерьеров (корабли, здания, пещеры):**
   - Мир делится на «ячейки» (rooms/zones) + порталы (двери/окна/арки).
   - Runtime: DFS от камеры через видимые порталы → множество видимых ячеек.
   - Объекты в невидимых ячейках → `Renderer.enabled = false` / не рендерятся.
   - Двери = порталы (открыта/закрыта → visibility).
   - Совместим с FO: ячейки хранятся в локальных координатах корня сцены, ребейз сдвигает корень — ячейки едут со сценой.

2. **Hi-Z / software occlusion для экстерьера (города, ландшафт):**
   - GPU Occlusion Culling (встроенный) — первый шаг, бесплатно.
   - Если недостаточно — custom Hi-Z compute shader (bounding boxes vs mip-chain depth).
   - Для CPU-bound: Intel MOC (software) — но требует интеграции C++ библиотеки (не тривиально в Unity, нужен native plugin).

3. **Distance-based impostors:**
   - Дальние объекты (>N м) → impostor (Amplify bake).
   - Дальние кластеры (>M м) → HLOD (merged mesh) → impostor.

### 2.2 Совместимость с Floating Origin

- **Все baked-данные** (Umbra, Precomputed Culling) — НЕсовместимы с FO (координаты сдвигаются).
- **Portal-based** — совместим: ячейки в локальных координатах сцены, ребейз сдвигает корень сцены целиком.
- **Spatial partitioning** — совместим: перестраивается при ребейзе (или хранится в локальных координатах).
- **GPU Occlusion Culling** — совместим: работает в camera-space, не зависит от world origin.
- **Camera-Relative Culling** — включить в GraphicsSettings (Lights + Shadows) для FO.

## 3. Задача "игроку отрисовывать то, куда смотрит"

Исходная идея пользователя: "игроку отрисовывать то куда смотрит — остальное ему ведь не нужно — это лишь координаты и состояния, зачем игроку это рисовать он не будет везде и сразу."

Это точно описывает **visibility-driven rendering**: рендерить только то, что в frustum + не окклюдировано + в пределах distance. Остальное — координаты и состояния (данные), не рендер. Это и есть цель culling pipeline. Наша архитектура:

- **Frustum** (built-in) — отсекает то, что за спиной/сбоку.
- **Occlusion** (portal + Hi-Z/GPU) — отсекает то, что за стенами/зданиями/горами.
- **Distance** (LOD/impostor/HLOD) — дальние объекты упрощаются/импостерятся.
- **State vs Render** — состояние (координаты, HP, инвентарь) хранится на сервере и синкается независимо от рендера; клиент рендерит только видимое.

Это соответствует MMO interest management: сервер шлёт состояние только релевантным сущностям (по дистанции/видимости/зоне), клиент рендерит только видимое.

## 4. Этапы (предложение, требует одобрения)

### Этап C0: Немедленно (0–2 недели, бесплатно)

1. Включить **GPU Resident Drawer** + **GPU Occlusion Culling** в URP (бесплатно, встроено). Проверить совместимость шейдеров (блокер: `ProjectC/ProceduralSteel` и др. без `DOTS_INSTANCING_ON`).
2. Включить **Camera-Relative Culling** (Lights + Shadows) в GraphicsSettings для FO.
3. Включить **Mesh LOD** для всех импортируемых mesh (вместо LOD Group где возможно) — снижает память.
4. Добавить **NativeTrees** (Burst quadtree) как пакет для пространственного партиционирования.

### Этап C1: Адаптация шейдеров (Э5, 2–4 недели)

- Привести кастомные шейдеры (`ProjectC/ProceduralSteel` и др.) к поддержке DOTS-инстансинга: `#pragma multi_compile _ DOTS_INSTANCING_ON`, `UNITY_DOTS_INSTANCED_PROP` / `unity_ObjectToWorld` из `UnityDOTSInstancing.hlsl`.
- Включить `GPU Resident Drawer = Instanced Drawing`, GPU occlusion culling, `BRG Variants = Keep All`, Forward+.
- Приёмка: 0 shader errors; Frame Debugger показывает `Hybrid Batch Group`; SetPass падает; сравнение «до/после» в `ITERATIONS.md`.

### Этап C2: LOD / HLOD / Impostors (Э6, 4–8 недель)

- L0 (0–3 км): полные блоки (как сейчас).
- L1 (3–12 км): HLOD по кварталу/чанку — 1 меш на блок, окна эмиссией, мелочь скрыта (`DetailDistanceCuller`), bake в редакторе + `LODGroup`.
- L2 (12–80 км): 1 билборд-квад на город (camera-facing, pre-rendered силуэт с альфой) + аддитивный квад ночных огней; позиция пересчитывается от камеры каждый кадр, хранятся только скаляры → FO-класс 🟢.
- Купить Amplify Impostors ($30) → bake impostors для дальних объектов (деревья, здания, камни, корабли).
- Импосторы: bake один раз в Editor (RenderCamera → RenderTexture → Sprite), ассеты рядом со слоями террейна (пути вне `*Textures*`). Запрещено генерировать в рантайме из сида.
- Открытое решение: HLOD по чанку (23 группы) или сначала нарезать блоки 20–60.

### Этап C3: Portal-based culling (4–8 недель)

- Реализовать portal-based culling для интерьеров (корабли, здания): деление на ячейки + порталы (двери/окна/арки), DFS от камеры через видимые порталы, `Renderer.enabled = false` для невидимых ячеек.
- Двери = порталы (открыта/закрыта → visibility).
- Интеграция с NativeTrees (quadtree для статики, spatial hash для динамики).
- Distance-based culling manager (вкл/выкл объектов по дистанции + LOD switching).

### Этап C4: Custom Hi-Z (опционально, 6–12 недель)

- Только если встроенный GPU Occlusion недостаточен.
- Compute shader: bounding boxes vs mip-chain depth.
- Для CPU-bound: Intel MOC (software) — native plugin.

### Этап C5: Корабли (параллельно с C2–C3)

- Не склеивать (`CombineMeshesToCollider` — только для коллайдеров, не для рендера).
- LOD для корабля: корпус (LOD0-2), детали (антенны, тросы, кабинка) — отдельные LOD/cull.
- Impostor для дальнего корабля (особенно для других игроков/трафика).
- Culling: frustum + occlusion + distance. Детали интерьера не рендерить снаружи.
- Runtime spawn: пулинг (ObjectPool), не Instantiate/Destroy; NGO `INetworkPrefabInstanceHandler`.
- NavMesh на деке: отдельный sandbox (`ShipDeckNav`).
- Static batching для корабля — нет (двигается).

## 5. Ограничения и budgets

| Критерий | Норма |
|---|---|
| Draw calls (L0) | ≤ 120 |
| Зернистость | PRIMUM ≤ ~1 200 мешей (✅ 1 207), дверь = 2 рендерера (✅), чанк ≤ ~110 (✅ 104) |
| Дальность | 0.5 км полный, 5–10 км HLOD, 20–80 км 1–2 вызова |
| Ребейз | F8/F9: город, физика, двери, огни, HLOD/импостор едут; 0 errors |

## 6. FO-классы для нового контента (из `09S` + `03`)

| Класс | Правило | Применение |
|---|---|---|
| 🟢 Простой контент (без `NetworkObject`) | Под корень `WorldScene_*` — едет бесплатно | Здания PRIMUM, статик, разметка, декор, HLOD/импосторы |
| 🟡 Скрипт с мировыми `Vector3` | `ApplyRebaseTranslation` + вызов в shift-блоке | Все кэши: `deathY`, камера+collision, платформа, carry, шторм, AABB ветра, коридоры высот, fallback-точки, NPC targets, ship deck nav |
| 🔴 Рантайм-спавн вне корней сцен | Регистрация участником или parent под участника | Спавн кораблей/лута/NPC, пулы |
| 💾 Сейвы мировых координат | Хранить + применять кумулятив при загрузке | `ShipPositionSaveData`, player persistence, чекпоинты |
| 🔵 Сетевой объект (с `NetworkObject`) | Полный пайплайн §3.2 `09S` | Корабли, паром, `Road to Quartus`, квест-трекеры |
| ⚫ Запрещённое | `static` поля с мировыми `Vector3`; static batching на движущихся объектах; рантайм-спавн без регистрации | Код-ревью, CI-проверки |

## 7. Что НЕ делать

- Не использовать Umbra (baked occlusion) — несовместим с FO и динамикой.
- Не мигрировать на DOTS/Entities — мы на NGO GameObjects, миграция неоправданна.
- Не покупать Perfect Culling / Frustum Culling / Ultimate Optimization Bundle — устаревшие/избыточные.
- Не трогать ProjectSettings/Packages без согласования.
- Не склеивать город дальше (Э4 закрыт; склейка не снимает draw calls).
- Не запускать Play Mode / скриншоты (делает пользователь).
- Не писать `.meta` / `.asmdef` руками.

## 8. Источники

- `01_our_city_optimization.md` — наша оптимизация города (скрипты, архитектура, FO, scene streaming, light pool, detail culler, куллинг, LOD, rendering).
- `02_internet_culling_assets.md` — интернет-ресерч ассетов/методов culling Unity (paid/free, occlusion, frustum, distance, LOD, HLOD, impostor, batching, GPU occlusion).
- `03_floating_origin_big_world.md` — Floating Origin и большие миры в Unity (FO, streaming, LOD, HLOD, impostor, cell-based, spatial partitioning, interest management, server authority, DOTS).
- `docs/world/primum_city/ROADMAP.md` — дорожная карта Э1–Э8, бюджеты, DoD.
- `docs/world/primum_city/06_REFERENCE/04_OCCLUSION_CULLING_RESEARCH.md` — Umbra failure, замеры draw calls.
- `docs/world/primum_city/06_REFERENCE/01_INDUSTRY_PRACTICE.md` — GPU-путь, SRP Batcher/Drawer.
- `docs/world/floatingorigin/09S_NEW_CONTENT_PIPELINE.md` — пайплайн нового контента, FO-классы.
- `docs/world/optimization/LOD_VIEW_DISTANCE_DESIGN.md` — дизайн 4 колец.

---

*Синтез подготовлен как исследовательский материал. Код не написан; рекомендации применяются через отдельные тикеты с одобрения пользователя.*
