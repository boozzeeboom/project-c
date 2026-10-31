# Исследование: улучшение culling в Unity 6 (URP) для большого мира Project C

> Дата: 30.10.2026. Составлено как ресёрч-отчёт (без кода). Источники — официальная документация Unity, Asset Store, GitHub, блоги, обсуждения. Ссылки в конце каждого раздела.

---

## 0. Контекст проекта (ограничения)

| Параметр | Значение |
|---|---|
| Unity | 6000.5.2f1 (Unity 6) |
| RP | URP 17.5.0 |
| Netcode | NGO 2.13.0 |
| Cinemachine | 3.1.7 |
| Input | New Input System 1.19.0 |
| Build | StandaloneWindows64 |
| Мир | 24 стриминговые сцены 6×4, `SCENE_SIZE = 79999f`, Floating Origin (ребейз корней сцен), динамика, без Static-флагов, много мелких объектов/дверей, корабли |
| Запреты | Play Mode/скриншоты — только пользователь; ProjectSettings/Packages — по согласованию |

**Ключевая проблема:** built-in occlusion culling (Umbra) требует Static-флагов (Occluder/Occludee) и baked-данных, не работает с runtime-геометрией и динамикой; Floating Origin сдвигает корни сцен — baked-occlusion данные привязаны к абсолютным координатам и ломаются. Поэтому нужен **динамический (runtime) culling**, независимый от статики.

---

## 1. Built-in Unity culling: что есть «из коробки»

### 1.1 Frustum culling
- Выполняется автоматически для всех Camera. Исключает Renderers, не попадающие в view frustum.
- Дёшево, CPU-side, работает для всех объектов (включая динамические).
- **Ограничение:** не учитывает occlusion (стена/здания полностью скрывают объект — он всё равно рендерится → overdraw).
- Ссылка: https://docs.unity3d.com/6000.5/Documentation/Manual/UnderstandingFrustum.html

### 1.2 Occlusion Culling (Umbra) — CPU, baked
- Бейкает данные о видимости между ячейками сцены. Runtime — queries по baked-данным.
- Работает лучше всего для **комнат/коридоров** (небольшие изолированные зоны).
- **Требует Static-флаги** (Occluder/Occludee). Dynamic GameObjects могут быть occludees, но не occluders.
- **Не подходит** для runtime-геометрии, процедурных сцен, Floating Origin (координаты сдвигаются → baked-данные невалидны).
- Память: baked-данные грузятся в RAM.
- Ссылки: https://docs.unity3d.com/6000.5/Documentation/Manual/OcclusionCulling.html ; https://docs.unity.com/en-us/engine/6000.5/manual/cameras/occlusion-culling

### 1.3 Distance culling / Camera-Relative Culling
- `Camera-Relative Culling` в GraphicsSettings: reference point для culling — позиция камеры (а не world origin). Критично для Floating Origin / больших миров: снижает flickering светов/теней далеко от камеры.
- `LOD Bias`, `Maximum LOD Level` в Quality settings — глобальные пороги.
- Ссылка: https://docs.unity.cn/Documentation/Manual/class-GraphicsSettings.html

### 1.4 Layer-based culling
- Camera.cullingMask — отключать целые слои по камере.
- Полезно для раздельного рендера (UI, skybox, спец-камеры).
- `Per-Layer Camera Culling` (Asset Store, бесплатный) — автоматическое включение/выключение слоёв по дистанции. Совместим с Cinemachine (с оговорками).

### 1.5 LOD Group
- До 8 уровней LOD, переключение по screen-relative size.
- Cross-fade (dithering) для плавных переходов; в URP — `LOD Cross Fade` в URP Asset (требует GPU Resident Drawer).
- Плюсы: гибкость, поддержка анимаций/материалов.
- Минусы: полный mesh на каждый уровень → память; GameObject на каждый уровень; CPU-overhead.
- Ссылка: https://docs.unity3d.com/6000.3/Documentation/Manual/lod/lod-group-landing.html

### 1.6 Mesh LOD (Unity 6)
- Новый фича: LOD-уровни хранятся в **одном mesh** (индексные диапазоны `MeshLodRange`), генерируются при импорте.
- Минимальная память и CPU-overhead (нет отдельных GameObject/mesh).
- Ограничения: нет custom transition zones; не рекомендуется совмещать с LOD Group; SkinnedMeshRenderer не учитывает skin weights; только triangle topology; минимум 256 треугольников для генерации.
- Cross-fade требует GPU Resident Drawer.
- Ссылка: https://docs.unity.com/en-us/engine/6000.5/manual/analysis/graphics-performance-profiling/lod/mesh-lod

### 1.7 GPU Resident Drawer (URP 17)
- Автоматически использует BRG API + GPU instancing → снижает draw calls и CPU-нагрузку.
- Требования: Forward+ path, compute shaders, Shader Stripping `BatchRendererGroup Variants = Keep All`, SRP Batcher включён.
- Несовместимо: LOD Group animated cross-fade (падает в static distance-based), shadow matrix override.
- Ссылка: https://docs.unity.cn/6000.4/Documentation/Manual/urp/gpu-resident-drawer.html

### 1.8 GPU Occlusion Culling (URP 17)
- GPU-side occlusion: depth-текстуры текущего и предыдущего кадра → консервативное отсечение (bounding spheres vs downsampled depth).
- Требует GPU Resident Drawer.
- Эффективен: одинаковые mesh (instancing), много occlusion с высоким vertex count, малый screen-space радиус occluded объектов.
- **Важно:** не «настоящее» occlusion culling — снижает polycount, но не всегда culls объект целиком; в indoor может дать лишь 5%.
- Ограничения: Entities не всегда cull'ятся (GPU culling для Entities graphics — отдельная история, Burst Occlusion Culling).
- Ссылки: https://docs.unity3d.com/6000.0/Documentation/Manual/urp/gpu-culling.html

### 1.9 Burst Occlusion Culling (Entities Graphics)
- CPU-side masked occlusion culling (Intel MOC) на Burst/Jobs — для DOTS-сущностей.
- Подходит для больших open-world/cities/interiors с overdraw.
- Не подходит: instanced объекты (GPU быстрее), тонкие occluders.
- Ссылка: https://docs.unity3d.com/Packages/com.unity.entities.graphics@1.0/manual/burst-occlusion-culling-overview.html

### 1.10 CullingGroup API
- Интеграция своих систем в culling/LOD pipeline Unity.
- Bounding spheres, frustum + static occlusion; distance bands.
- Используется для: спавн/деспавн, AI-оптимизация, particle systems через DrawProcedural.
- Ограничение: только static occlusion как occluder; один camera на группу.
- Ссылка: https://docs.unity3d.com/6000.0/Documentation/Manual/CullingGroupAPI-landing.html

### 1.11 BatchRendererGroup (BRG) / custom rendering
- API для high-performance custom rendering в SRP. `OnPerformCulling` callback — точка входа для visibility culling + generation draw commands.
- Burst jobs для culling. Используется Entities Graphics и GPU Resident Drawer.
- Позволяет написать **свой** culling pipeline (frustum + distance + occlusion + impostor switching) с полным контролем.
- Ссылка: https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Rendering.BatchRendererGroup.html

### 1.12 HLOD (Unity HLOD System — GitHub, бесплатный)
- `Unity-Technologies/HLODSystem` — quadtree-based HLOD для **статических** объектов, 2021.3.3f1, Unity Companion License.
- Chunk Size → quadtree; High/Low/Cull категории по screen-space AABB; mesh merging + simplification; streaming.
- **Устарел/не поддерживается** для Unity 6 (разработан под 2021.x). Использовать как референс архитектуры, не как зависимость.
- Ссылки: https://github.com/Unity-Technologies/HLODSystem ; https://discussions.unity.com/t/tutorial-how-to-use-the-free-performance-optimization-draw-call-reducer-hlod/910660

---

## 2. Paid/Free ассеты Asset Store / GitHub / open-source

### 2.1 Occlusion culling

| Ассет | Цена | Совместимость | Что делает | Плюсы/минусы |
|---|---|---|---|---|
| **Perfect Culling** (Koenigz) | $97.46 | Unity 2019.4.41, BiRP/URP/HDRP | CPU-side occlusion culling, pixel-perfect, bake + runtime, moving occlusion areas | + точный, динамические occlusion areas; − старая версия Unity, нет нативной поддержки FO/large worlds, цена |
| **Precomputed Culling** (Prographers) | $150 | Unity 6000.0.77+ | Precomputed visibility (portal/cell), bake в рантайм или до билда, moving occlusion area | + высокая производительность (30→72 fps Quest 2), full source; − $150, не для 2D, bake-зависим |
| **Drakkar Culling Pro** (Drakkar Dev) | $59.99 | Unity 2022.3.28+ | Multithreaded visibility culling, occlusion, distance | + дешевле Perfect Culling, multithreaded; − мало отзывов, неясна поддержка FO |
| **Frustum Culling** (Pathiral) | $20 | Unity 2022.3.53, BiRP/URP/HDRP | Frustum culling компоненты | + дёшево; − избыточно (Unity уже делает frustum culling), сомнительная польза |
| **Per-Layer Camera Culling** (Umbra Evolution) | FREE | (старые версии) | Авто вкл/выкл слоёв по дистанции | + бесплатно; − проблемы с Cinemachine, устаревший |
| **Light Culling Occlusion** (Heart State Games) | $7.50 (скидка) | Unity 6000.4.12 | Culling светов по occlusion | + дёшево; узкая задача (только света) |

**Важно:** ни один из occlusion-ассетов не решает проблему Floating Origin / no-static-flags нативно. Все они либо bake-based (ломаются при FO), либо CPU-side (конкурируют с gameplay-логикой).

### 2.2 LOD / HLOD / Mesh simplification

| Ассет | Цена | Совместимость | Что делает |
|---|---|---|---|
| **Automatic LOD** (Ultimate Game Tools) | $90 | Unity 2020.3.7+ | Автогенерация LOD-уровней при импорте, mesh decimation |
| **Ultimate LOD System MT** (MT Assets) | $16.90 | Unity 6000.0.22f1, BiRP/URP/HDRP | LOD-генератор + mesh simplifier |
| **Advanced LOD Tool** (DTDev's Team) | $59.99 | Unity 6000.3/6000.4, BiRP/URP/HDRP | LOD + crossfade + batch tool + mesh repair |
| **The Ultimate Optimization Bundle** (Léo Chaumartin) | $80 | Unity 2021.3.2, BiRP/URP (HDRP не рекомендуется) | AutoLOD Mesh Decimator + Mirage Pro runtime impostors |
| **Mesh LOD Visualizer** (XR Dev Space) | $10 | Unity 6000.3.11 | Визуализация Mesh LOD |
| **LOD for Animator** (TopsonGames) | $19.99 | Unity 6000.2.6 | LOD для аниматоров (оптимизация anim) |

**Рекомендация:** для Unity 6 предпочтительнее **встроенный Mesh LOD** (бесплатно, минимальная память) + при необходимости **Automatic LOD / Ultimate LOD System MT** для автогенерации уровней. Платные LOD-ассеты оправданы только если нужен batch-инструментарий.

### 2.3 Impostors

| Ассет | Цена | Совместимость | Что делает |
|---|---|---|---|
| **Amplify Impostors** (Amplify Creations) | $30 (скидка 50% от $60) | Unity 2022.3.22+, URP/HDRP/BiRP, Unity 6.4/6.5 | Octahedral/spherical/hemi impostors, 1-click bake, GPU instancing, dithered cross-fade, LOD Group compatible. GPU-оптимизации Early-Z occlusion |
| **Mirage Pro** (Léo Chaumartin) | $35 (в bundle $70→$35) | Unity 2021.3+, HDRP не рекомендуется | Runtime baking impostors |
| **Rawteous** (voidoverzero, itch.io) | ? (itch.io) | Unity 6+, URP 12+ | Octahedral impostor baker, Grid/Fibonacci/Octahedron capture, live preview |
| **Abdns/ImpostorSystem_Unity** (GitHub) | FREE (MIT) | Unity 6000.0+, URP 17.3+, Shader Model 4.5 | Octahedral impostor baking + rendering, parallax correction, PBR, URP. **Ограничение: только Forward path**, source shaders должны иметь GBuffer pass |

**Рекомендация:** **Amplify Impostors** — золотой стандарт для URP, активно обновляется (Unity 6.4/6.5), $30 со скидкой. Open-source альтернатива — Abdns (MIT) для прототипа.

### 2.4 Пространственные структуры / open-source

| Репозиторий | Лицензия | Что делает |
|---|---|---|
| **bartofzo/NativeTrees** | MIT | Burst-совместимые sparse Octree/Quadtree для DOTS/ECS, raycast/range/k-NN queries |
| **Sylmerria/Spatial-Hashing** | MIT | Spatial hashing для ECS/DOTS, Unity 2022.2+ |
| **dolejska-daniel/unity-quadtree** | MIT | Generic quadtree для GameObjects, 2D spatial partitioning |
| **shadowndacorner/Unity-Spatial-Partitioning** | ? | Sparse octree + компоненты для поддержания актуальности |
| **Unity-Technologies/HLODSystem** | Unity Companion License | Quadtree HLOD, 2021.x (устаревший, референс) |
| **Jaskowicz1/Software-OC** | Apache-2.0 | Software occlusion culling для UE5 (не Unity, но алгоритм MOC) |
| **GameTechDev/MaskedOcclusionCulling** (Intel) | ? | Intel MOC library — референс для CPU occlusion |
| **nvpro-samples/gl_occlusion_culling** | ? | Hi-Z occlusion culling (OpenGL sample, референс) |
| **gigc/Janua** | ? | Open-source portal/cell PVS engine (C++, референс) |

### 2.5 Специализированные

| Ассет | Цена | Комментарий |
|---|---|---|
| **Floating Origin Network (FONet)** | (Single Entity) | Endless worlds, jitter-free, URP/HDRP, Unity 6000.5.6. Совместим с нашим FO-подходом (но мы уже реализовали свой) |
| **CullingGroup API** (встроенный) | FREE | См. 1.10 |

---

## 3. Методы culling для больших миров / open-world / MMO

### 3.1 Пространственное партиционирование

- **Uniform grid / Spatial hashing** — O(1) вставка/запрос, отлично для равномерного распределения (деревья, камни, двери). Burst-friendly. Плохо для кластеризованных данных (города).
- **Quadtree (2D) / Octree (3D)** — адаптивное разбиение, хорошо для неравномерного распределения (здания vs пустыня). Sparse-варианты экономят память.
- **BVH (Bounding Volume Hierarchy)** — динамические объекты, быстрый rebuild, хорош для raycast/occlusion.
- **K-d tree** — статические сцены, быстрый поиск соседей.
- **Выбор для нас:** для XZ-плоскости (мир плоский/слабо рельефный) — **quadtree для статики + spatial hash для динамики** (корабли, двери, NPC). NativeTrees (Burst) — готовая база.

### 3.2 Cell-based / Portal-based / PVS

- **Cell-based (Umbra-стиль):** мир делится на ячейки, baked visibility между соседними. Хорошо для interiors. Ломается при FO и runtime-изменениях.
- **Portal-based:** мир делится на комнаты/зоны, связанные порталами (двери/арки/окна). Если портал не виден — всё за ним cull'ится. Дёшево, детерминированно. **Идеально для нашего проекта** (много дверей, корабли с каютами, постройки).
- **PVS (Potentially Visible Set):** для каждой view-cell предвычисляется набор видимых объектов. Runtime — lookup. Хорошо для статики, плохо для динамики.
- **Janua** (open-source PVS engine) — референс для portal/cell генерации.

### 3.3 Frustum + distance (базовый слой)
- Frustum culling (built-in) + distance thresholds (LOD/cull distances).
- Distance-based culling: объекты дальше N метров — полностью отключаются (не только LOD, а GameObject.active = false).
- **CullingGroup API** — distance bands для спавна/деспавна/AI.

### 3.4 Occlusion queries (GPU)
- **Hardware occlusion queries** (OpenGL/Vulkan/DX): рендер bounding box, проверка видимости. Латентность 1-2 кадра.
- **Hi-Z (Hierarchical Z-buffer):** mip-chain depth buffer, проверка AABB против нужного LOD. Полностью GPU-side, масштабируется. **Это то, что делает Unity GPU Occlusion Culling** (консервативно, на bounding spheres).
- **Software occlusion (CPU, MOC):** Intel Masked Occlusion Culling — SIMD CPU rasterization depth buffer. 3x быстрее предыдущих методов. Подходит для GPU-bound сцен. Референс: GameTechDev/MaskedOcclusionCulling.
- **Software occlusion (UE5 Nanite-style):** UE5 использует software rasterization для мелких треугольников + hardware для крупных.

### 3.5 Impostor pipelines
- **Octahedral impostors** (Amplify/Rawteous/Abdns): bake 8-64 видов объекта в атлас, runtime — выбор ближайшего вида по углу камеры. Depth-write для интерсекций.
- **Billboard impostors** (2-triangle quad + texture): для деревьев/растительности.
- **Streaming impostors:** impostor-текстуры грузятся по требованию (MegaCity подход).
- **HLOD + impostors:** дальние кластеры → merged mesh → impostor.

### 3.6 Динамические объекты (корабли, двери, NPC)
- Built-in occlusion не работает для динамики как occluder.
- Подход: **custom occlusion на bounding volumes** (AABB/sphere) с conservative test (Hi-Z или software).
- Для дверей: portal-based (дверь = портал, открыта/закрыта → visibility).
- Для кораблей: корабль как единый occluder (bounding box), внутренние объекты — отдельные occludees.

---

## 4. Как другие проекты решают visibility

### 4.1 Unity Fantasy Kingdom (URP, Unity 6)
- GPU Resident Drawer + GPU Occlusion Culling + STP (upscaling) + Render Graph + APV.
- Статическая геометрия (Synty assets) — LOD Group + GPU occlusion.
- Ссылка: https://unity.com/demos/fantasy-kingdom

### 4.2 Unity Megacity / Megacity Metro (DOTS, URP)
- ECS + Subscenes + Entities Graphics (BRG) → frustum + distance culling built-in.
- HLOD для дальних кластеров.
- Streaming impostors (octahedral) для деревьев.
- Burst Occlusion Culling для occlusion.
- Floating Origin (scene-origin shifting) — встроенная поддержка в Entities.
- Ссылки: https://github.com/Unity-Technologies/megacity-metro ; https://discussions.unity.com/t/hlod-streaming-octahedral-impostors-using-megacity-subscenes/742223

### 4.3 DOTS Sample / Entities Graphics
- `com.unity.entities.graphics` — frustum culling + LOD (MeshLODComponent) + Burst Occlusion Culling.
- LOD pivot point хранится один раз на группу (`LODGroupWorldReferencePoint`).
- Subscenes conversion: MeshRenderer → RenderMesh entity.
- Ограничения: UI/Canvas/TextMeshPro/Decal/SpeedTree не поддерживаются в subscenes.
- Ссылка: https://docs.unity.cn/Packages/com.unity.rendering.hybrid@0.10/manual/index.html

### 4.4 Unreal Engine 5 (World Partition + HLOD) — референс
- World Partition: grid cells, streaming по дистанции.
- HLOD Layers: Instancing (ISM для impostors), Merged Mesh, Simplified Mesh.
- Nanite: virtualized geometry, software rasterization для мелких треугольников.
- Lumen: software/hardware ray tracing.
- **Вывод для нас:** Unity не имеет прямого аналога Nanite/World Partition. Нужно строить свой pipeline.

### 4.5 Fortnite (UE4/UE5)
- HLOD + impostors для деревьев (octahedral, 12x12 views).
- Streaming impostors с LOD-transition.
- Ссылка: https://discussions.unity.com/t/hlod-streaming-octahedral-impostors-using-megacity-subscenes/742223

### 4.6 Valve Source 2 (Visibility)
- PVS: cell/portal, voxelize world, toolsskybox для границ, visblocker для блокировки LOS.
- Console commands для debug.
- Ссылка: https://developer.valvesoftware.com/wiki/Source_2/Docs/Level_Design/Visibility

---

## 5. Архитектура custom culling для Project C

### 5.1 Требования
- Большие миры (24 сцены, streaming).
- Floating Origin (ребейз корней сцен) → нельзя использовать абсолютные координаты в baked-данных.
- Динамика (корабли, двери, NPC, разрушаемые объекты).
- No Static flags (Umbra не подходит).
- Много мелких объектов/дверей → overhead на GameObject/Renderer.
- Корабли → крупные occluders + внутренние occludees.
- LOD/HLOD + impostor + GPU instancing.

### 5.2 Многоуровневый pipeline (рекомендация)

```
Уровень 0: Scene streaming (уже есть) — ClientSceneLoader грузит сцены по дистанции.
Уровень 1: Spatial partitioning — quadtree (статика) + spatial hash (динамика) по XZ.
Уровень 2: Frustum culling (built-in + camera-relative) — бесплатно.
Уровень 3: Distance culling / LOD — Mesh LOD (встроенный) + LOD Group для спец-объектов.
Уровень 4: Occlusion culling — custom (portal-based для интерьеров + Hi-Z/software для экстерьера).
Уровень 5: Impostor switching — Amplify/Abdns для дальних объектов.
Уровень 6: GPU instancing — GPU Resident Drawer (для совместимых объектов).
```

### 5.3 Custom occlusion: рекомендуемый подход

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

### 5.4 Что писать самим vs покупать

**Покупать (рекомендация):**
- **Amplify Impostors** ($30) — золотой стандарт для URP, активно обновляется. Обязательно.
- **NativeTrees** (MIT, бесплатно) — Burst octree/quadtree как база пространственного партиционирования.
- **Precomputed Culling** ($150) — **только если** нужна pixel-perfect occlusion для интерьеров И есть бюджет. Иначе portal-based самому.

**Не покупать:**
- Frustum Culling ($20) — избыточно (Unity делает сам).
- Per-Layer Camera Culling (бесплатно) — проблемы с Cinemachine.
- Perfect Culling ($97) — устаревший Unity 2019.4, bake-based (ломается при FO).
- HLOD System (GitHub) — устаревший (2021.x), использовать как референс.
- Floating Origin Network ($?) — мы уже реализовали свой FO.

**Писать самим:**
1. **Portal-based culling** (интерьеры) — ~2-4 недели. Критичен для кораблей/зданий.
2. **Spatial partitioning integration** (NativeTrees) — ~1 неделя. Quadtree для статики, spatial hash для динамики.
3. **Distance-based culling manager** — ~1 неделя. Включение/выключение объектов по дистанции + LOD switching.
4. **Impostor switching logic** — ~1 неделя. Интеграция с Amplify bake.
5. **Custom Hi-Z (опционально)** — ~4-8 недель. Только если встроенный GPU occlusion недостаточен.

### 5.5 Совместимость с Floating Origin

- **Все baked-данные** (Umbra, Precomputed Culling) — НЕсовместимы с FO (координаты сдвигаются).
- **Portal-based** — совместим: ячейки в локальных координатах сцены, ребейз сдвигает корень сцены целиком.
- **Spatial partitioning** — совместим: перестраивается при ребейзе (или хранится в локальных координатах).
- **GPU Occlusion Culling** — совместим: работает в camera-space, не зависит от world origin.
- **Camera-Relative Culling** — включить в GraphicsSettings (Lights + Shadows) для FO.

### 5.6 Ограничения Unity 6 / URP 17

- GPU Resident Drawer требует Forward+ path + compute shaders + Shader Stripping.
- Mesh LOD cross-fade требует GPU Resident Drawer.
- GPU Occlusion работает только с GPU Resident Drawer.
- LOD Group animated cross-fade не поддерживается с GPU Resident Drawer.
- Entities Graphics (DOTS) — отдельный путь, несовместим с NGO GameObjects (мы используем NGO, не Netcode for Entities).
- **Важно:** мы используем NGO 2.13 (GameObject-based), не DOTS. Поэтому Burst Occlusion Culling (Entities) — недоступен. Используем GameObject-based подход.

---

## 6. Сравнительная таблица ассетов

| Ассет | Цена | Тип | Unity 6 / URP | FO-совместим | Рекомендация |
|---|---|---|---|---|---|
| Amplify Impostors | $30 (скидка) | Impostor baker | ✅ (6.4/6.5) | ✅ | **Купить** |
| Precomputed Culling | $150 | Occlusion (bake) | ✅ (6000.0.77) | ❌ (bake-based) | Только с бюджетом |
| Perfect Culling | $97 | Occlusion (bake) | ❌ (2019.4) | ❌ | Не покупать |
| Drakkar Culling Pro | $60 | Occlusion (runtime) | ⚠️ (2022.3) | ⚠️ | Опционально |
| Frustum Culling | $20 | Frustum | ✅ (2022.3) | ✅ | Не покупать (избыточно) |
| Per-Layer Camera Culling | FREE | Layer culling | ⚠️ (старый) | ✅ | Не покупать (Cinemachine issues) |
| Automatic LOD | $90 | LOD gen | ✅ (2020.3+) | ✅ | Опционально |
| Ultimate LOD System MT | $17 | LOD gen | ✅ (6000.0) | ✅ | Опционально |
| Advanced LOD Tool | $60 | LOD + batch | ✅ (6000.3/4) | ✅ | Опционально |
| Ultimate Optimization Bundle | $80 | LOD + impostor | ⚠️ (2021.3) | ✅ | Не покупать (устаревший) |
| NativeTrees | FREE (MIT) | Spatial partition | ✅ (2021.3+) | ✅ | **Использовать** |
| Spatial-Hashing | FREE (MIT) | Spatial hash | ✅ (2022.2+) | ✅ | **Использовать** |
| HLOD System (Unity) | FREE | HLOD | ❌ (2021.x) | ⚠️ | Референс только |
| Abdns ImpostorSystem | FREE (MIT) | Impostor | ✅ (6000.0) | ✅ | Прототип |
| Rawteous | ? | Impostor | ✅ (6+) | ✅ | Опционально |
| Floating Origin Network | ? | FO system | ✅ (6000.5) | ✅ (свой FO) | Не нужен (свой FO) |

---

## 7. Рекомендации (приоритеты)

### Немедленно (0-2 недели)
1. Включить **GPU Resident Drawer** + **GPU Occlusion Culling** в URP (бесплатно, встроено). Проверить совместимость шейдеров.
2. Включить **Camera-Relative Culling** (Lights + Shadows) в GraphicsSettings для FO.
3. Включить **Mesh LOD** для всех импортируемых mesh (вместо LOD Group где возможно) — снижает память.
4. Добавить **NativeTrees** (Burst quadtree) как пакет для пространственного партиционирования.

### Средний срок (2-6 недель)
5. Купить **Amplify Impostors** ($30) → bake impostors для дальних объектов (деревья, здания, камни).
6. Реализовать **Portal-based culling** для интерьеров (корабли, здания) — критичен.
7. Реализовать **Distance-based culling manager** (вкл/выкл объектов по дистанции + LOD switching).
8. Интегрировать spatial hash для динамики (корабли, NPC, двери).

### Долгий срок (6-12 недель)
9. HLOD для дальних кластеров (на основе Unity HLODSystem как референс, переписать под Unity 6).
10. Custom Hi-Z occlusion (только если GPU Occlusion недостаточен) — compute shader.
11. Streaming impostors (по требованию) для дальних зон.

### Не делать
- Не использовать Umbra (baked occlusion) — несовместим с FO и динамикой.
- Не мигрировать на DOTS/Entities — мы на NGO GameObjects, миграция неоправданна.
- Не покупать Perfect Culling / Frustum Culling / Ultimate Optimization Bundle — устаревшие/избыточные.
- Не трогать ProjectSettings/Packages без согласования.

---

## 8. Источники (URLs)

### Официальная документация Unity
- GPU Occlusion Culling URP: https://docs.unity3d.com/6000.0/Documentation/Manual/urp/gpu-culling.html
- Occlusion Culling: https://docs.unity3d.com/6000.5/Documentation/Manual/OcclusionCulling.html
- GPU Resident Drawer: https://docs.unity.cn/6000.4/Documentation/Manual/urp/gpu-resident-drawer.html
- Mesh LOD: https://docs.unity.com/en-us/engine/6000.5/manual/analysis/graphics-performance-profiling/lod/mesh-lod
- LOD Group: https://docs.unity3d.com/6000.3/Documentation/Manual/lod/lod-group-landing.html
- BatchRendererGroup: https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Rendering.BatchRendererGroup.html
- CullingGroup API: https://docs.unity3d.com/6000.0/Documentation/Manual/CullingGroupAPI-landing.html
- Burst Occlusion Culling: https://docs.unity3d.com/Packages/com.unity.entities.graphics@1.0/manual/burst-occlusion-culling-overview.html
- GraphicsSettings (Camera-Relative Culling): https://docs.unity.cn/Documentation/Manual/class-GraphicsSettings.html
- Fantasy Kingdom URP: https://unity.com/demos/fantasy-kingdom
- Megacity Metro: https://github.com/Unity-Technologies/megacity-metro

### Asset Store
- Amplify Impostors: https://assetstore.unity.com/packages/tools/utilities/amplify-impostors-119877
- Perfect Culling: https://assetstore.unity.com/packages/tools/utilities/perfect-culling-occlusion-culling-system-193611
- Precomputed Culling: https://assetstore.unity.com/packages/tools/utilities/precomputed-culling-221372
- Drakkar Culling Pro: https://assetstore.unity.com/packages/tools/game-toolkits/drakkar-culling-pro-323413
- Frustum Culling: https://assetstore.unity.com/packages/tools/camera/frustum-culling-184023
- Per-Layer Camera Culling: https://assetstore.unity.com/packages/tools/camera/per-layer-camera-culling-35100
- Automatic LOD: https://assetstore.unity.com/packages/tools/modeling/automatic-lod-43652
- Ultimate LOD System MT: https://assetstore.unity.com/packages/tools/game-toolkits/ultimate-lod-system-mt-automatic-lod-generator-mesh-simplifier-m-170425
- Advanced LOD Tool: https://assetstore.unity.com/packages/tools/game-toolkits/advanced-lod-tool-353906
- Ultimate Optimization Bundle: https://assetstore.unity.com/packages/tools/utilities/the-ultimate-optimization-bundle-automatic-lod-and-impostor-syst-309040
- Floating Origin Network: https://assetstore-fallback.unity.com/packages/templates/systems/floating-origin-network-real-floating-origin-215830
- Fantasy Kingdom URP: https://assetstore.unity.com/packages/essentials/tutorial-projects/fantasy-kingdom-in-unity-6-urp-298128

### GitHub / Open Source
- Unity HLOD System: https://github.com/Unity-Technologies/HLODSystem
- NativeTrees: https://github.com/bartofzo/NativeTrees
- Spatial-Hashing: https://github.com/Sylmerria/Spatial-Hashing
- unity-quadtree: https://github.com/dolejska-daniel/unity-quadtree
- Unity-Spatial-Partitioning: https://github.com/shadowndacorner/Unity-Spatial-Partitioning
- Abdns ImpostorSystem: https://github.com/Abdns/ImpostorSystem_Unity
- Precomputed Culling (docs): https://github.com/Prographers/Precomputed-Culling
- Masked Occlusion Culling (Intel): https://github.com/GameTechDev/MaskedOcclusionCulling
- gl_occlusion_culling (NVIDIA): https://github.com/nvpro-samples/gl_occlusion_culling
- Janua (PVS): https://github.com/gigc/Janua
- Software-OC (UE5): https://github.com/Jaskowicz1/Software-OC

### Статьи / блоги / обсуждения
- HLOD + Streaming Impostors (Unity Discussions): https://discussions.unity.com/t/hlod-streaming-octahedral-impostors-using-megacity-subscenes/742223
- GPU Resident Drawer discussion: https://discussions.unity.com/t/gpu-render-drawer-in-what-situation-is-it-faster/1545183
- Large World Coordinates (Unity Discussions): https://discussions.unity.com/t/large-world-coordinates-double-precision-data/902530
- Floating Origin gist: https://gist.github.com/brihernandez/9ebbaf35070181fa1ee56f9e702cc7a5
- Advanced occlusion culling (portals/Hi-Z): https://daydreamsoft.com/blog/advanced-occlusion-culling-portals-hi-z-buffers-and-clustered-rendering-explained
- Masked Software Occlusion Culling (Intel): https://www.intel.com/content/www/us/en/developer/articles/technical/masked-software-occlusion-culling.html
- Simplygon HLOD/impostor blog: https://www.simplygon.com/posts/760b9ef6-7068-4001-bac0-f1abb366f835
- Valve Source 2 Visibility: https://developer.valvesoftware.com/wiki/Source_2/Docs/Level_Design/Visibility
- Unreal World Partition HLOD: https://dev.epicgames.com/documentation/en-us/unreal-engine/world-partition---hierarchical-level-of-detail-in-unreal-engine
- Godot Visibility Ranges (HLOD): https://docs.godotengine.org/en/stable/tutorials/3d/visibility_ranges.html
- Unity CullingGroup API video: https://www.youtube.com/watch?v=eYM3Wv3R6uA
- URP Advanced Creators (Unity 6): https://unity.com/resources/introduction-to-urp-advanced-creators-unity-6

---

*Отчёт составлен на основе web-поиска. Цены и совместимость проверены по состоянию на октябрь 2026. Перед покупкой ассетов — проверять актуальные версии на Asset Store.*