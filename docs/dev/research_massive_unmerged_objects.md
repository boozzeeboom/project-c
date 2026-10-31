# Ресерч: массовые независимые low-poly объекты в Unity (100k+) — сценарий «без склейки»

> Дата: 2026-10-30. Подготовлено для Project C (Stage 2.5, визуальный прототип, Unity 6000.5.2f1, URP 17.5, NGO 2.13, StandaloneWindows64). Без кода. Цель — ответить: можно ли держать 100k+ отдельных объектов с коллайдерами/физикой и разрушаемостью, и что реально применимо без полного перехода на DOTS.

---

## 1. Краткое резюме

1. **100k+ объектов с коллайдерами и физикой в Unity — это домен DOTS/ECS + Unity Physics (DOTS Physics).** Классический GameObject/PhysX стек не рассчитан на такое: жёсткий лимит PhysX — **65 536 коллайдеров на сцену** (исторически), и даже современные версии PhysX деградируют при десятках тысяч активных rigidbody.
2. **Рендеринг 100k+ объектов без склейки возможен** через GPU Resident Drawer (BRG) в Unity 6 + Forward+ + шейдеры с `DOTS_INSTANCING_ON`. Но это работает только для **одинаковых мешей/материалов** и даёт выгоду при массовых повторяющихся объектах; разнообразие мешей/материалов ломает батчинг.
3. **Разрушаемость** — самый дорогой аспект. Варианты: (а) voxel (Teardown-style) — полностью свой пайплайн мешей/коллайдеров; (б) pre-fractured meshes (Voronoi) — заранее нарезанные куски, дешёвый runtime; (в) runtime boolean/slicing (OpenFracture) — дорого, ограничено; (г) compound colliders + разборка на заранее подготовленные части.
4. **MMO + 100k объектов** требует interest management (Area of Interest) + чанковый стриминг. NGO 2.13 даёт только примитивный `CheckObjectVisibility` callback — для 100k объектов придётся писать свою систему видимости/стриминга.
5. **Без миграции на DOTS** (наш текущий стек) реалистичный потолок — **сотни–несколько тысяч** активных объектов с физикой, и **десятки тысяч** статических визуальных объектов через GPU Resident Drawer. Полноценные 100k «живых» (с коллайдером + физикой + разрушаемостью) объектов без DOTS **не достижимы**.
6. **Точечные подходы** (без полного перехода на DOTS): GPU Resident Drawer для визуала; статические коллайдеры-триггеры вместо rigidbody; LOD/импосторы; чанковый стриминг сцены; pre-fractured разрушаемость; pooled NetworkObject; spatial partitioning + interest management на сервере.

---

## 2. Unity DOTS/ECS для массовых объектов

### 2.1. Что это
- **Entities** (com.unity.entities) — ECS-фреймворк. Данные лежат в contiguous chunks (16 КБ блоки), что даёт cache-friendly итерацию. 100 000 сущностей с одним архетипом = ~несколько чанков, итерация за миллисекунды.
- **Jobs System + Burst** — многопоточные jobs с SIMD-компиляцией. `IJobEntity` / `IJobChunk` автоматически распараллеливаются по чанкам.
- **EntityCommandBuffer (ECB)** — отложенные структурные изменения (создание/удаление/добавление компонентов) из jobs без гонок. Критичен для массового спавна/дестроя.
- **SubScenes** — сцены, содержимое которых «запекается» в бинарные entity-данные и стримится. Закрытая подсцена занимает мало памяти и грузится за кадры.
- **Entities Graphics (бывший Hybrid Renderer)** — рендерит entity через BRG/GPU instancing. Поддерживает LOD, occlusion culling, motion vectors.

### 2.2. Масштабирование до 100k+
- 100k entity с позицией+ротацией+масштабом + MeshRenderer — типичный benchmark Unity («cube grid»). В standalone билде — сотни FPS.
- Ограничение — **не количество сущностей, а количество архетипов/чанков**. Если 100k сущностей имеют 100k уникальных архетипов — каждая в своём чанке → катастрофа памяти и производительности. Нужно держать архетипов мало.
- **Chunk allocations**: Unity выделяет чанки по 16 КБ; 100k сущностей одного архетипа ≈ несколько десятков чанков. Ок.
- **Streaming**: SubScenes загружаются асинхронно; сцены 79999² (как у нас) делятся на чанки-подсцены.

### 2.3. Жёсткие ограничения SubScenes (из changelog Entities Graphics 1.0.x)
Следующие компоненты **не поддерживаются** внутри подсцены (конвертируются с ошибками или не рендерятся):
- **UI и Canvas** (весь uGUI).
- **TextMeshPro** (компонент TMP не поддерживается в subscene).
- **URP Decal Projector** (не работает внутри подсцены).
- **SpeedTree** шейдеры (URP) — не поддерживаются.
- **Terrains** — не поддерживаются.
- **SkinnedMeshRenderer** с шейдерами без skinning-поддержки — ошибки.
- **ParticleSystem / VFX Graph** — preview не поддерживается, Light module не рендерит свет.
- **LOD crossfade** — не поддерживается для entity; LOD max level не работает.
- **Lightmaps** — auto-generated lightmaps не поддерживаются; нужно bake вручную.
- **HDRP PlanarReflectionProbe**, **Lens flare**, **LocalVolumetricFog** — проблемы.
- **Universal Render Pipeline/2D shaders** на MeshRenderer — не поддерживаются (SpriteRenderer — companion-компонент).

**Следствие для MMO-песочницы:** любая UI-надпись над объектом (имя игрока, HP), декали (следы, взрывы), деревья (SpeedTree), террейн — должны жить **вне** подсцен (в обычной сцене / в отдельном authoring-слое), что усложняет архитектуру.

### 2.4. Ограничения DOTS как такового
- Нет `MonoBehaviour`-логики в обычном виде; нужен переход на Systems. Большой рефакторинг.
- NGO 2.13 работает с GameObjects, а не с Entities. Связь DOTS↔сеть — через Netcode for Entities (отдельный пакет), который мы не используем.
- Отладка сложнее; экосистема сторонних ассетов (UI, анимация, аудио) не всегда DOTS-совместима.

---

## 3. Unity Physics (DOTS Physics) vs PhysX

### 3.1. PhysX (встроенный, GameObject-based)
- **Лимит: 65 536 коллайдеров на сцену** (историческое ограничение PhysX в Unity; в новых версиях лимит снят, но производительность деградирует).
- **Rigidbody:** десятки тысяч активных rigidbody → CPU-bound, особенно при контактах. Статические коллайдеры (без Rigidbody) дёшевы, но всё равно занимают память и участвуют в broadphase.
- **MeshCollider:** convex — до 255 треугольников; non-convex не сталкиваются с non-convex. Compound colliders — предпочтительны.
- **CCD, фрикция, стекинг** — хорошо работают, но дорого.
- **Итог для 100k:** PhysX не потянет 100k *живых* коллайдеров с физикой. Статические триггеры — можно держать больше, но обновление позиций/запросы Raycast будут узким местом.

### 3.2. Unity Physics (DOTS Physics, com.unity.physics)
- Детерминированный, Burst-компилируемый, ECS-native. Коллайдеры — `PhysicsCollider` (blob-ассеты, общие для сущностей → экономия памяти).
- **Масштаб:** 100k+ rigidbody в benchmark'ах (падающие кубы) — десятки FPS на десктопе; 10k — комфортно.
- **Ограничения:**
  - Менее стабильный стекинг (башни падают хуже, чем в PhysX).
  - Фрикция/отскок исторически имели баги (см. известный баг-репорт Giannis Akritidis).
  - Нет «спящего» состояния как в PhysX (все тела активны, хотя есть оптимизации для статичных).
  - Ограниченная поддержка сложных joint'ов, ragdoll'ов.
  - Детерминизм требует фиксированного timestep и избегания операций >50 единиц от origin (переполнение float) — **критично для floating origin!** (наш проект использует floating origin с ребейзом).
- **Havok Physics** (com.unity.physics.havok) — альтернатива, более стабильная, платная лицензия для продакшена.

### 3.3. Практический вывод
| Метрика | PhysX (GameObject) | Unity Physics (DOTS) |
|---|---|---|
| Коллайдеров на сцену | ~65k hard cap | 100k+ (ограничено памятью) |
| Активных rigidbody 60 FPS | ~1–5k (контакты) | ~10–30k (падающие) |
| Стекинг/фрикция | Хорошо | Средне (баги) |
| Детерминизм | Нет | Да |
| Интеграция с NGO | Нативно | Через Netcode for Entities (миграция) |
| Стоимость перехода | 0 | Высокая (весь геймплей на ECS) |

---

## 4. GPU instancing / GPU Resident Drawer / BRG

### 4.1. Классический GPU instancing
- `Graphics.DrawMeshInstanced` — **максимум 1023 инстанса за вызов** (ограничение API). Для 100k нужно ~100 вызовов на меш+материал.
- Требует одинаковый меш и материал; шейдер должен поддерживать instancing (`#pragma multi_compile_instancing`).
- Дёшево по CPU (один draw call на батч), но каждый батч — отдельный вызов.

### 4.2. GPU Resident Drawer (Unity 6, URP)
- Автоматически использует BRG API для GameObject'ов с MeshRenderer.
- **Требования:**
  - Forward+ rendering path.
  - Графический API с compute shaders (не OpenGL ES).
  - SRP Batcher включён.
  - Шейдеры с поддержкой **DOTS Instancing** (`DOTS_INSTANCING_ON` variant) — без этого объекты рендерятся без instancing.
  - `BatchRendererGroup Variants = Keep All` в Project Settings.
- **Выгода:** группирует одинаковые меш+материал в один «Hybrid Batch Group» draw call; CPU-стоимость рендеринга падает в разы.
- **Ограничения:**
  - Работает только для MeshRenderer (не SkinnedMeshRenderer с проблемными шейдерами, не SpriteRenderer, не VFX).
  - Build time растёт (компиляция всех BRG-вариантов).
  - Разные меши/материалы → разные батчи; 1000 уникальных мешей = 1000 батчей.
  - Объекты, меняющиеся каждый кадр, обновляются (есть overhead).
  - На практике 100k *разных* объектов (разные меш/материал) не дадут одного draw call — будет сотни/тысячи.

### 4.3. BatchRendererGroup (BRG) напрямую
- Низкоуровневый API: сам формируешь batches и GPU-буферы. Используется Entities Graphics и GPU RD.
- Потолок — память GPU и лимиты драйвера (обычно миллионы инстансов, но батчи разбиваются).
- Требует написания рендерера на C# — высокая сложность.

### 4.4. Практический вывод по рендеру
- **Одинаковые low-poly объекты (ящики, камни, мусор)** — GPU RD даст 1 draw call на тип объекта, 100k объектов → несколько draw calls. Реально.
- **Разнообразие** (каждый объект уникален) — GPU RD не поможет, fallback на обычный рендеринг → тысячи draw calls → CPU-bound.
- **Решение:** атласы материалов, общие меш-варианты, LOD-цепочки, импосторы для дальних объектов.

---

## 5. Разрушаемость (destructible)

### 5.1. Подходы (без покупок)

| Подход | Суть | Плюсы | Минусы | Примеры игр |
|---|---|---|---|---|
| **Voxel** | Мир из вокселей; разрушение = удаление вокселей + регенерация меша/коллайдера чанка | Полностью разрушаемый мир; детерминизм | Свой пайплайн мешей/коллайдеров; дорого по памяти/CPU; визуал «кубический» | Teardown, Minecraft, (частично) No Man's Sky |
| **Pre-fractured (Voronoi)** | Заранее нарезать меш на куски (в редакторе/при загрузке), при ударе — показать куски + физика | Дёшевый runtime; красивый визуал | Фиксированное разрушение; память на куски; коллайдеры на каждый кусок | Fortnite (building damage), многие шутеры |
| **Runtime slicing/boolean** | Реальное разрезание меша по плоскости (OpenFracture, MeshCutting) | Любое разрезание | Дорого (мс на операцию); сложные коллайдеры; артефакты | Half-Life 2 (старый), демки |
| **Compound + разборка** | Объект состоит из заранее подготовленных частей, при ударе — отсоединять части | Просто; предсказуемо | Не «настоящее» разрушение | Большинство игр |
| **Процедурный fracture (Voronoi runtime)** | Генерировать куски в момент разрушения | Гибко | 3–9 мс на объект (см. unity-mesh-fracture); коллайдеры отдельно | Демки |

### 5.2. Open-source инструменты (без покупок)
- **OpenFracture** (github.com/dgreenheck/OpenFracture) — runtime slicing + fracturing, поддерживает convex/non-convex меша, работает в runtime. Ограничение: slicing только в runtime (не в редакторе).
- **unity-mesh-fracture** (github.com/sinanata) — Voronoi pre-bake на loading screen, runtime spawn кусков с физикой, dissolve.
- **Mesh-Cutting** (github.com/KristinLague) — простое разрезание меша по плоскости.
- **BOSS** — процедурное разрушение, но платный (упомянут для сравнения).
- **Unity built-in:** нет встроенного fracture. MeshCollider + MeshFilter; генерация мешей через `Mesh` API.

### 5.3. Как делают эталонные игры
- **Teardown:** мир полностью из вокселей (маленькие кубики). Разрушение = удаление вокселей + перестройка меша/коллайдера чанка на лету. Физика — свои rigidbody на куски. Рендеринг — ray tracing по воксельной сетке (не полигональный). **В Unity повторить 1:1 невозможно без собственного рендерера.**
- **Minecraft:** чанки 16×256×16 вокселей; меш генерится из видимых граней; коллайдеры — упрощённые (AABB на блок). Разрушение = изменение блока + пересчёт меша чанка. Масса объектов держится за счёт чанковой модели (не 100k отдельных GameObject'ов).
- **No Man's Sky:** процедурная генерация планет; разрушение террейна (копание); impostors для дальних объектов; LOD-агрессивный.
- **Fortnite:** здания — pre-fractured (каждое здание заранее нарезано на куски с коллайдерами), при уроне куски отваливаются. Не настоящий boolean.
- **Rust:** базы из строительных блоков (стены/полы/крыши) — каждый блок отдельный объект с коллайдером; разрушение = удаление блока (не разрезание). Сетевой синк — по блокам.
- **Valheim:** террейн + строительные куски; разрушение = удаление куска; физика на отдельных частях.

### 5.4. Воксельная библиотека для Unity
- **Gamelogic Grids** — сетки, не воксельный движок.
- **PolyVox / Thermite3D** — C++ воксельный движок, есть Unity-обёртки (устаревшие).
- **VoxelBuster / Voxel Play** — платные ассеты (упоминаем как ориентир, не используем).
- **Свой воксельный чанк:** MeshFilter + MeshCollider, генерация меша из occupancy grid (marching cubes или greedy meshing). Реализуемо, но это большой проект.

---

## 6. Стриминг массовых объектов, MMO, interest management

### 6.1. Чанковый стриминг
- Мир делится на ячейки (chunks/cells) фиксированного размера (например 64–256 м).
- Игрок подписывается на чанки в радиусе R; при перемещении — загрузка новых, выгрузка дальних.
- В Unity: `SceneManager.LoadSceneAsync` для сцен-чанков (как у нас — WorldScene_X_Z), либо Addressables для префабов.
- **Ключевое:** объекты живут в чанках; чанк — единица загрузки/выгрузки. 100k объектов распределены по чанкам; активны только объекты ближних чанков (обычно 1–5k).

### 6.2. Spatial partitioning
- **Uniform grid** (ячейки фиксированного размера) — просто, быстро, хорошо для равномерного распределения.
- **Quadtree / Octree** — для неравномерного (города vs пустыня).
- **Hash grid** (spatial hash) — для динамических объектов, O(1) lookup.
- Используется для: nearest-neighbor queries, collision broadphase, interest management, AI.

### 6.3. Interest management (MMO)
- **Суть:** сервер не шлёт всем игрокам состояние всех объектов; только объекты в радиусе интереса (Area of Interest, AoI).
- **Подходы:**
  - **Grid-based AoI:** мир на сетку; игрок получает обновления объектов в соседних ячейках.
  - **Distance-based:** радиус вокруг игрока.
  - **Visibility callback:** как в NGO — `CheckObjectVisibility(clientId)` на каждый NetworkObject.
- **NGO 2.13:** даёт только `NetworkObject.CheckObjectVisibility` callback + `NetworkShow/NetworkHide`. **Нет встроенного AoI/grid.** Для 100k объектов naive-подход (callback на каждый объект каждый тик) убьёт сервер. Нужно писать свою систему: пространственный индекс + батчевые show/hide.
- **Netcode for Entities** имеет более продвинутый interest management (relevancy), но требует DOTS.

### 6.4. Как держать 100k в памяти
- **Не держать все 100k активными.** Только ближние чанки в RAM; дальние — на диске/в бандлах.
- **Entity/GameObject pooling:** не Destroy/Instantiate, а переиспользование.
- **Blob-ассеты для коллайдеров:** общий меш-коллайдер для одинаковых объектов (Unity Physics делает это автоматически).
- **Impostors/billboards** для дальних объектов — заменять меш на спрайт.
- **LOD-цепочки:** дальние объекты → низкополигональные/точечные.

---

## 7. Примеры игр/проектов — что можно взять

| Игра | Техника | Что взять для Project C |
|---|---|---|
| **Minecraft** | Воксельные чанки 16×256×16; меш из видимых граней; коллайдеры = AABB блока | Чанковая модель стриминга; коллайдеры только на поверхности; не 100k отдельных объектов, а чанки-меша |
| **Teardown** | Полностью воксельный мир; ray tracing; разрушение = удаление вокселей + перестройка чанка | Если нужна тотальная разрушаемость — только воксельный путь; иначе pre-fractured |
| **No Man's Sky** | Процедурные планеты; impostors; агрессивный LOD | Impostors для дальних объектов; процедурная генерация вместо ручной расстановки |
| **Fortnite** | Здания pre-fractured на куски; при уроне куски отваливаются | Pre-fractured подход для зданий/конструкций — реалистичен в Unity без DOTS |
| **Rust** | Базы из строительных блоков (стены/полы/крыши); каждый блок — объект с коллайдером; разрушение = удаление блока | Модульное строительство из готовых блоков; сетевой синк по блокам; коллайдеры только на блоках |
| **Valheim** | Террейн + строительные куски; разрушение = удаление куска | То же, что Rust, но с террейном |

**Главный вывод из примеров:** ни одна из этих игр не держит 100k *отдельных GameObject'ов с физикой*. Все используют **чанки/блоки/модульные элементы** и **агрессивный стриминг**. «100k объектов» в реальности = «100k элементов данных», из которых активны 1–5k.

---

## 8. Сравнение: склейка vs без склейки

| Аспект | Склейка (merged meshes/chunks) | Без склейки (независимые объекты) |
|---|---|---|
| **Редактор** | Сложнее редактировать (нужны инструменты распаковки/пересборки) | Удобно: каждый объект — отдельный GameObject/Prefab |
| **CPU (трансформы)** | Один объект → один Transform update | 100k Transform updates → CPU-bound (без DOTS) |
| **GPU (draw calls)** | Один draw call на чанк | Тысячи draw calls (без GPU RD) / несколько (с GPU RD + одинаковые меша) |
| **Физика** | Один compound collider на чанк — дёшево | 100k коллайдеров — PhysX не потянет; DOTS Physics потянет, но нужна миграция |
| **Разрушаемость** | Сложно: нужно разрезать меш чанка | Легко: удалили/заменили объект; но коллайдеры на каждый кусок |
| **MMO/сеть** | Синк чанков (diff) — мало трафика | Синк каждого объекта — много трафика; нужен interest management |
| **Память** | Меньше (общие меша) | Больше (каждый объект — overhead GameObject ~0.5–2 KB) |
| **Сложность кода** | Высокая (генерация мешей, коллайдеров) | Низкая (стандартный Unity) |
| **Масштаб** | 100k+ элементов данных, активны чанки | 100k активных объектов — только с DOTS |

**Вывод:** «без склейки» выигрывает в простоте разработки и разрушаемости, но проигрывает в масштабе. Гибридный подход (визуально — чанки/склейка для дальних, независимые объекты для ближних/интерактивных) — стандарт индустрии.

---

## 9. Что реально возможно в Unity 6 с нашим стеком (без DOTS-миграции)

**Наш стек:** Unity 6000.5.2f1, URP 17.5, NGO 2.13 (GameObject-based), StandaloneWindows64, New Input System, Cinemachine 3.1.7. **Без DOTS, без Entities, без Unity Physics (DOTS).**

### 9.1. Реалистичные потолки
| Категория | Потолок без DOTS | Комментарий |
|---|---|---|
| Активных GameObject'ов с Update | ~1–5k | Transform + компоненты каждый кадр |
| Статических визуальных объектов (GPU RD) | ~100k–1M | Если одинаковые меш+материал, Forward+, DOTS_INSTANCING_ON шейдеры |
| Коллайдеров PhysX на сцену | ~65k (hard cap) | Статические триггеры — больше, но broadphase деградирует |
| Активных rigidbody 60 FPS | ~1–5k | С контактами — меньше |
| Сетевых объектов (NGO) | ~1–5k активных | Ограничено трафиком и CPU сервера |
| Разрушаемых объектов (pre-fractured) | ~сотни одновременно | Каждый кусок = объект + коллайдер + rigidbody |

### 9.2. Жёсткие ограничения
1. **NGO 2.13 не работает с Entities.** Если мы хотим DOTS-физику/рендер, придётся либо мигрировать на Netcode for Entities, либо держать два мира (GameObject для сети, Entities для физики) — очень сложно.
2. **PhysX 65536 коллайдеров** — жёсткий потолок на сцену. 100k объектов с коллайдерами в одной сцене невозможны.
3. **GPU Resident Drawer требует Forward+ и DOTS_INSTANCING_ON шейдеры.** Стандартные URP шейдеры поддерживают; кастомные — нужно добавлять `DOTS_INSTANCING_ON` (иначе fallback без instancing).
4. **Floating origin** (наш проект) — PhysX/Unity Physics требуют осторожности: операции >50 единиц от origin теряют точность. Ребейз корней сцен работает, но все мировые координаты в коде должны быть кумулятивными/пересчитываемыми.
5. **SubScenes недоступны без DOTS** — мы не можем использовать entity-стриминг; только SceneManager/Additive сцены.

### 9.3. Что из «без склейки» реально
- **Визуально:** да, через GPU Resident Drawer для однотипных объектов (мусор, камни, ящики). 100k статических мешей → несколько draw calls.
- **С физикой:** нет для 100k. Только для сотен–тысяч активных rigidbody.
- **Разрушаемость:** да, но через pre-fractured (Fortnite-стиль) или compound-разборку, не через настоящий boolean на 100k объектов.
- **MMO:** да, но с собственным interest management поверх NGO visibility; 100k сетевых объектов — нет, только 1–5k активных + стриминг.

---

## 10. Точечные подходы (без полного перехода на DOTS)

Чтобы получить разрушаемость и массовые объекты «без склейки» в нашем стеке, рекомендуемые точечные меры:

### 10.1. Рендер
1. **Включить GPU Resident Drawer** (Forward+, SRP Batcher, Keep All BRG variants, DOTS_INSTANCING_ON в шейдерах). Даёт массовый рендер однотипных объектов.
2. **Атласы материалов** — один материал на класс объектов, чтобы GPU RD мог батчить.
3. **LOD-группы + impostors** для дальних объектов (HLOD вручную или через ProBuilder/LOD Group).
4. **Статический батчинг отключить** при включённом GPU RD (конфликт).
5. **Occlusion culling** ( baked + dynamic) — не рендерить скрытое.

### 10.2. Физика
1. **Статические триггеры вместо rigidbody** где возможно (коллайдеры без Rigidbody — дёшевы, но всё равно 65k cap).
2. **Compound colliders** вместо MeshCollider на сложных объектах.
3. **Convex MeshCollider** с лимитом 255 треугольников — для разрушаемых кусков.
4. **Не держать 100k коллайдеров в одной сцене** — стримить чанками (активны только ближние).
5. **Physics.IgnoreCollision / layer matrix** — уменьшить пары проверок.
6. **Fixed timestep** и уменьшение solver iterations где допустимо.

### 10.3. Разрушаемость
1. **Pre-fractured meshes (Voronoi)** для зданий/конструкций — заранее нарезать, при ударе показать куски + физика. Использовать OpenFracture/unity-mesh-fracture как референс/библиотеку.
2. **Compound + разборка** для мебели/техники — заранее подготовленные части, отсоединение при ударе.
3. **Воксельный путь** — только если нужна тотальная разрушаемость (Teardown-стиль); это большой отдельный проект (свой меш/коллайдер-генератор на чанки).
4. **Ограничить количество одновременно активных кусков** — пул + автоудаление мелких обломков через N секунд.
5. **Коллайдеры на куски — только крупные** (мелкие обломки — без коллайдеров или триггеры).

### 10.4. MMO / стриминг
1. **Чанковый стриминг сцен** (как у нас — WorldScene_X_Z) + радиус загрузки вокруг игрока.
2. **Собственный interest management** поверх NGO: пространственный хеш-индекс объектов + батчевые `NetworkShow/NetworkHide` по тикам (не на каждый объект каждый кадр).
3. **Не делать каждый объект NetworkObject'ом.** Статические объекты мира — не сетевые (синхронизируются через состояние чанка/seed). Сетевыми делаем только интерактивные/разрушаемые.
4. **Delta-синк состояния чанков** вместо per-object RPC.
5. **Object pooling** для NetworkObject (NGO Object Pooling) — избежать Instantiate/Destroy.

### 10.5. Архитектура данных
1. **Разделить «данные» и «представление»:** 100k элементов хранятся как данные (массивы/ScriptableObject/JSON), а GameObject'ы создаются только для ближних/интерактивных.
2. **Spatial hash grid** для запросов «что рядом» (AI, взаимодействие, interest).
3. **Floating origin discipline:** все мировые координаты — через кумулятивный offset, пересчёт при ребейзе (см. docs/world/floatingorigin/).

### 10.6. Что НЕ делать
- Не пытаться держать 100k активных GameObject'ов с Update — убьёт CPU.
- Не делать каждый объект NetworkObject'ом — убьёт сеть и сервер.
- Не использовать runtime boolean на каждый удар — дорого и нестабильно.
- Не переходить на DOTS «частично» без плана миграции сети (NGO→Netcode for Entities) — получится два несвязанных мира.

---

## Источники

**DOTS/ECS/SubScenes:**
- Unity Entities manual: Subscenes overview — https://docs.unity3d.com/Packages/com.unity.entities@1.0/manual/conversion-subscenes.html
- Entities Graphics changelog (unsupported in subscenes: UI/Canvas, TMP, Decal, SpeedTree, Terrains) — https://docs.unity3d.com/Packages/com.unity.entities.graphics@1.0/changelog/CHANGELOG.html
- Code Monkey: What are Subscenes in Unity — https://unitycodemonkey.com/video.php?v=91kJtsDLTyE

**GPU Resident Drawer / BRG / instancing:**
- Unity 6 manual: GPU Resident Drawer (URP) — https://docs.unity3d.com/6000.0/Documentation/Manual/analysis/graphics-performance-profiling/in-urp/reduce-draw-calls-urp/reduce-rendering-work-on-cpu/gpu-resident-drawer.html
- Unity Discussions: Entities Graphics + GPU RD + instancing — https://discussions.unity.com/t/entities-graphics-gpu-resident-drawer-gpu-instancing/1594113
- gamedevllm: Custom Shaders and GPU Resident Drawer (DOTS_INSTANCING_ON) — https://gamedevllm.com/en/unity6-custom-shader-resident-drawer-dots-instancing-en/
- Unity Discussions: GPU instancing count limit (1023) — https://discussions.unity.com/t/gpu-instancing-count-limit/723510

**Physics:**
- Unity Discussions: max 65K colliders in a scene — https://discussions.unity.com/t/what-to-do-about-the-max-65k-colliders-in-a-scene/538434
- NVIDIA forums: max rigidbodies (65536 scene-query limit removed) — https://forums.developer.nvidia.com/t/max-number-of-rigidbodies/50157
- Unity Physics changelog — https://docs.unity3d.com/Packages/com.unity.physics@1.3/changelog/CHANGELOG.html
- Giannis Akritidis: Why Unity ECS Physics Isn't Ready — https://giannisakritidis.com/blog/ECS-Physics-Bug/
- Unity Discussions: ECS Physics vs PhysX test — https://discussions.unity.com/t/ecs-physics-vs-physx-test/948446

**Destruction:**
- OpenFracture — https://github.com/dgreenheck/OpenFracture
- unity-mesh-fracture — https://github.com/sinanata/unity-mesh-fracture
- Mesh-Cutting — https://github.com/KristinLague/Mesh-Cutting
- Game Developer: Teardown voxel destruction — https://www.gamedeveloper.com/design/combining-bombastic-heists-with-a-fully-destructible-voxel-world-in-i-teardown-i-
- Unity Discussions: recreate Teardown voxel physics — https://discussions.unity.com/t/want-to-recreate-teardown-destructable-voxel-physics/828648
- Reddit: voxel destruction in Unity — https://www.reddit.com/r/Unity3D/comments/l553q3/voxel_destruction_in_my_voxel_unity_game/

**MMO / interest management / streaming:**
- NGO manual: Object visibility — https://docs.unity3d.com/Packages/com.unity.netcode.gameobjects@2.5/manual/basics/object-visibility.html
- Mirror: Interest Management — https://mirror-networking.gitbook.io/docs/manual/interest-management
- Dynetis: interest management for MMO — https://dynetisgames.com/2017/04/05/interest-management-mog/
- Game Programming Patterns: Spatial Partition — https://gameprogrammingpatterns.com/spatial-partition.html
- Unity Discussions: large number of pickups in MMO — https://gamedev.stackexchange.com/questions/150463/how-to-handle-a-large-number-of-pickups-in-a-mmo-game

**Примеры игр:**
- Teardown (Wikipedia) — https://en.wikipedia.org/wiki/Teardown_(video_game)
- Rust base building (Unity Discussions) — https://discussions.unity.com/t/what-is-the-best-networking-solution-for-a-game-like-rust-mmo/659560

---

*Отчёт подготовлен как исследовательский документ. Реализация любого из подходов — отдельная задача с дизайн-нотой и одобрением.*