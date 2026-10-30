# 05. Пишем сами, ничего не покупаем: детальное видение

> Дата: 2026-10-30. Сценарий: **никаких покупок** (ни Amplify Impostors, ни Precomputed Culling, ни Drakkar). Используем только встроенные возможности Unity 6 / URP 17 + бесплатные open-source (NativeTrees, Spatial-Hashing) + собственный код. Код не пишем — это видение/архитектура для будущих тикетов.

## 0. Принципы

1. **Ничего не покупаем.** Все компоненты — либо встроенные в Unity 6/URP 17, либо open-source MIT, либо пишем сами.
2. **Динамический culling, не bake.** Floating Origin запрещает статику → никаких baked occlusion данных (Umbra/Precomputed). Всё runtime.
3. **Не склеивать город дальше.** Э4 закрыт (1 207 мешей). Склейка снимает CPU-оверхед на объект, но не draw calls. Каждый объект остаётся независимым; draw calls снимаются GPU-driven + LOD/HLOD + импосторами.
4. **FO-совместимость обязательна.** Все структуры хранятся в локальных координатах корней сцен (или перестраиваются при ребейзе). Никаких мировых `Vector3` в кэшах куллинга. FO-класс 🟢.
5. **Игроку отрисовывать только то, куда смотрит.** Остальное — координаты и состояния (данные), не рендер.

## 1. Что остаётся бесплатным/встроенным (базовый слой)

Эти вещи не требуют покупок и дают базовую экономию. Их включаем в первую очередь.

### 1.1 Frustum culling (built-in)
- Unity автоматически отсекает Renderers, не попадающие в view frustum камеры.
- CPU-side, дёшево, работает для всех объектов (включая динамические).
- Ограничение: не учитывает occlusion (стена/здания полностью скрывают объект — он всё равно рендерится → overdraw).
- **Что делаем:** ничего, работает из коробки. Но убеждаемся, что `Camera.cullingMask` настроен (слои PRIMUM/RuinsValleys и т.д.).
- **FO-совместимость:** frustum считается в camera-space, не зависит от world origin → 🟢.

### 1.2 Camera-Relative Culling (GraphicsSettings)
- Reference point для culling — позиция камеры (а не world origin). Критично для FO/больших миров: снижает flickering светов/теней далеко от камеры.
- Включается в GraphicsSettings → Camera-Relative Culling (Lights + Shadows).
- **Что делаем:** включить в C0. Бесплатно, встроено.

### 1.3 Mesh LOD (Unity 6, встроенный)
- LOD-уровни хранятся в **одном mesh** (индексные диапазоны `MeshLodRange`), генерируются при импорте.
- Минимальная память и CPU-overhead (нет отдельных GameObject/mesh).
- Ограничения: нет custom transition zones; не рекомендуется совмещать с LOD Group; SkinnedMeshRenderer не учитывает skin weights; только triangle topology; минимум 256 треугольников для генерации.
- Cross-fade требует GPU Resident Drawer.
- **Что делаем:** включить для всех импортируемых mesh (вместо LOD Group где возможно) — снижает память. Настройки в Import Settings → Mesh LOD.

### 1.4 LOD Group (встроенный)
- До 8 уровней LOD, переключение по screen-relative size.
- Cross-fade (dithering) для плавных переходов; в URP — `LOD Cross Fade` в URP Asset (требует GPU Resident Drawer).
- Плюсы: гибкость, поддержка анимаций/материалов.
- Минусы: полный mesh на каждый уровень → память; GameObject на каждый уровень; CPU-overhead.
- **Что делаем:** использовать для спец-объектов (корабли, ADMIN-дворец, важные здания), где нужен контроль над уровнями. Для массовой геометрии — Mesh LOD.

### 1.5 GPU Resident Drawer (URP 17, встроенный)
- Автоматически использует BRG API + GPU instancing → снижает draw calls и CPU-нагрузку.
- Требования: Forward+ path, compute shaders, Shader Stripping `BatchRendererGroup Variants = Keep All`, SRP Batcher включён.
- Несовместимо: LOD Group animated cross-fade (падает в static distance-based), shadow matrix override.
- **Блокер:** кастомные шейдеры (`ProjectC/ProceduralSteel` и др.) не поддерживают `DOTS_INSTANCING_ON` → `undeclared identifier 'unity_ObjectToWorld'`.
- **Что делаем:** адаптировать шейдеры (C1) → включить Drawer = Instanced Drawing, GPU occlusion culling, BRG Variants = Keep All, Forward+.

### 1.6 GPU Occlusion Culling (URP 17, встроенный)
- GPU-side occlusion: depth-текстуры текущего и предыдущего кадра → консервативное отсечение (bounding spheres vs downsampled depth).
- Требует GPU Resident Drawer.
- Эффективен: одинаковые mesh (instancing), много occlusion с высоким vertex count, малый screen-space радиус occluded объектов.
- Ограничения: не «настоящее» occlusion culling — снижает polycount, но не всегда culls объект целиком; в indoor может дать лишь 5%.
- Entities не всегда cull'ятся (GPU culling для Entities graphics — отдельная история, Burst Occlusion Culling).
- **Что делаем:** включить вместе с Drawer (C1). Бесплатно, встроено.

### 1.7 CullingGroup API (встроенный)
- Интеграция своих систем в culling/LOD pipeline Unity.
- Bounding spheres, frustum + static occlusion; distance bands.
- Используется для: спавн/деспавн, AI-оптимизация, particle systems через DrawProcedural.
- Ограничение: только static occlusion как occluder; один camera на группу.
- **Что делаем:** использовать для спавна/деспавна объектов по дистанции (CullingGroup distance bands → enable/disable Renderer/GameObject). Бесплатно, встроено.

### 1.8 BatchRendererGroup (BRG) API (встроенный)
- API для high-performance custom rendering в SRP. `OnPerformCulling` callback — точка входа для visibility culling + generation draw commands.
- Burst jobs для culling. Используется Entities Graphics и GPU Resident Drawer.
- Позволяет написать **свой** culling pipeline (frustum + distance + occlusion + impostor switching) с полным контролем.
- **Что делаем:** опционально, если нужен полный контроль над рендером (C4). Сложно, но бесплатно.

## 2. Spatial partitioning (пишем сами)

### 2.1 Зачем

Пространственное партиционирование позволяет быстро находить объекты в радиусе/ячейке, без перебора всех объектов. Нужно для:
- Distance-based culling (кто в радиусе N метров от камеры).
- Portal-based culling (какие ячейки видны).
- Impostor switching (какие объекты дальше M метров).
- Interest management (MMO): кто релевантен клиенту.

### 2.2 Выбор структуры

| Структура | Плюсы | Минусы | Применение |
|---|---|---|---|
| **Uniform grid / Spatial hashing** | O(1) вставка/запрос, Burst-friendly | Плохо для кластеризованных данных (города) | Динамика: корабли, NPC, двери, лут |
| **Quadtree (2D) / Octree (3D)** | Адаптивное разбиение, хорошо для неравномерного распределения | Сложнее реализовать, overhead на узлы | Статика: здания, деревья, камни, пропсы |
| **BVH** | Динамические объекты, быстрый rebuild | Сложно | Raycast/occlusion queries |
| **K-d tree** | Статические сцены, быстрый поиск соседей | Не динамический | Статика (одноразовый bake) |

**Для нас:** XZ-плоскость (мир плоский/слабо рельефный) → **quadtree для статики + spatial hash для динамики**.

### 2.3 Quadtree (пишем сами или берём NativeTrees)

**Алгоритм:**
- Корень — AABB всего мира (или чанка сцены).
- Каждый узел делится на 4 квадранта при превышении capacity (например, 8 объектов).
- Листья хранят ссылки на объекты (индексы, а не GameObject — чтобы не держать ссылки на уничтоженные).
- Запрос: frustum/радиус → обход дерева, сборка видимых объектов.

**FO-совместимость:**
- Координаты объектов хранятся **относительно корня сцены** (локальные). При ребейзе корень сдвигается → локальные координаты не меняются → quadtree не перестраивается.
- Если quadtree хранит абсолютные мировые координаты — перестраивать при ребейзе (или хранить локальные + offset).
- **Решение:** хранить локальные координаты (относительно корня сцены). FO-класс 🟢.

**Реализация:**
- Вариант A: использовать **NativeTrees** (MIT, Burst-совместимый sparse Octree/Quadtree) — бесплатно, готовая база.
- Вариант B: написать свой quadtree на C# (без Burst) — проще, но медленнее. Для начала подойдёт.
- **Рекомендация:** начать с варианта B (свой простой quadtree), затем мигрировать на NativeTrees (Burst) для производительности.

**Структура узла:**
```
Node {
  Bounds (AABB в локальных координатах)
  List<int> objectIndices (или NativeList<int> для Burst)
  Node[] children (4 для quadtree)
  int depth
}
```

**Операции:**
- `Insert(index, bounds)` — вставка объекта.
- `Remove(index)` — удаление.
- `QueryFrustum(frustum)` → List<int> — объекты во фрустуме.
- `QueryRadius(center, radius)` → List<int> — объекты в радиусе.
- `Rebuild()` — полная перестройка (при изменении сцены).

**Производительность:**
- Вставка: O(log n) среднее.
- Запрос: O(k + log n), где k — число найденных объектов.
- Для 1 207 мешей города + 4 949 дверей + 496 RuinsValleys + ... — quadtree легко справляется.

### 2.4 Spatial hash (пишем сами)

**Алгоритм:**
- Мир делится на ячейки фиксированного размера (например, 64 м × 64 м).
- Хэш-таблица: ключ = (cellX, cellZ) → список объектов в этой ячейке.
- Вставка: вычислить ячейку объекта → добавить в список.
- Запрос радиуса: вычислить ячейки, пересекающие радиус → собрать объекты.
- Запрос фрустума: вычислить ячейки, пересекающие frustum → собрать объекты.

**FO-совместимость:**
- Ячейки в локальных координатах корня сцены → не меняются при ребейзе → 🟢.

**Реализация:**
- Вариант A: использовать **Spatial-Hashing** (MIT, Burst-совместимый) — бесплатно.
- Вариант B: написать свой на `Dictionary<long, List<int>>` или `NativeHashMap` — проще, но медленнее.
- **Рекомендация:** начать с варианта B (свой простой spatial hash на Dictionary), затем мигрировать на Spatial-Hashing (Burst) для производительности.

**Структура:**
```
SpatialHash {
  float cellSize
  Dictionary<long, List<int>> cells  // key = (cellX << 32) | cellZ
  void Insert(index, position)
  void Remove(index, position)
  List<int> QueryRadius(center, radius)
  List<int> QueryFrustum(frustum)
  void Clear()
}
```

### 2.5 Когда перестраивать

- При загрузке сцены (ClientSceneLoader) → Rebuild quadtree/spatial hash для новой сцены.
- При ребейзе → не перестраивать (локальные координаты не меняются).
- При спавне/деспавне объектов → Insert/Remove.
- При изменении сцены (добавление/удаление объектов) → Rebuild или инкрементальное обновление.

## 3. Portal-based culling (пишем сами)

### 3.1 Зачем

Portal-based culling — идеален для интерьеров (корабли, здания, пещеры). Мир делится на «ячейки» (rooms/zones) + порталы (двери/окна/арки). Если портал не виден — всё за ним cull'ится. Дёшево, детерминированно.

**Почему идеально для нас:**
- Много дверей (2 161 инстанс) → каждая дверь = портал.
- Корабли с каютами → порталы = двери/люки.
- Здания PRIMUM → порталы = двери/арки/окна.
- Совместим с FO: ячейки в локальных координатах сцены.

### 3.2 Архитектура

**Компоненты:**
1. **PortalCell** — ячейка (комната/зона). Содержит список объектов (Renderers) в этой ячейке.
2. **Portal** — портал (дверь/окно/арка). Связывает две ячейки. Имеет состояние (открыт/закрыт).
3. **PortalCullingManager** — управляет видимостью: DFS от камеры через видимые порталы → множество видимых ячеек → enable/disable Renderers.

**Структура данных:**
```
PortalCell {
  int id
  Bounds bounds (локальные координаты)
  List<Renderer> renderers
  List<Portal> portals (порталы, ведущие из этой ячейки)
}

Portal {
  int id
  PortalCell cellA, cellB
  Bounds bounds (портал как прямоугольник)
  bool isOpen (открыта/закрыта)
  bool isVisible (виден ли портал из камеры)
}

PortalCullingManager {
  List<PortalCell> cells
  Camera camera
  HashSet<int> visibleCells (множество видимых ячеек)
  void Update() // каждый кадр или по таймеру
}
```

**Алгоритм (каждый кадр или по таймеру, например 0.1 с):**
1. Найти ячейку камеры (по позиции камеры).
2. DFS/BFS от ячейки камеры:
   - Для каждой ячейки, проверить все порталы.
   - Для каждого портала: если открыт И виден из камеры (frustum test + occlusion test) → добавить соседнюю ячейку в очередь.
3. Собрать множество видимых ячеек.
4. Для каждой ячейки: если видима → enable все Renderers; если невидима → disable все Renderers.

**Проверка видимости портала:**
- Frustum test: портал в frustum камеры?
- Occlusion test (опционально): портал не окклюдирован другими объектами? (можно использовать простой raycast или пропустить для начала).
- Расстояние: портал дальше N метров → считать невидимым (distance culling).

### 3.3 Интеграция с дверями

- Каждая дверь (`PM_DOOR_*` префаб) = портал.
- `DoorController` управляет состоянием (открыта/закрыта) → обновляет `Portal.isOpen`.
- При открытии двери → портал становится видимым → DFS добавляет соседнюю ячейку → объекты за дверью рендерятся.
- При закрытии двери → портал становится невидимым → соседняя ячейка cull'ится.

**Оптимизация:**
- Не проверять все порталы каждый кадр. Использовать таймер (например, 0.1 с) + событие при изменении состояния двери (открытие/закрытие).
- Кэшировать результат DFS на несколько кадров (если камера не сильно сдвинулась).

### 3.4 FO-совместимость

- Ячейки и порталы хранятся в локальных координатах корня сцены → при ребейзе не меняются → 🟢.
- Позиции камеры читаются живьём (`camera.transform.position`) → 🟢.
- Состояние дверей (открыта/закрыта) хранится в `DoorController` (экземплярный) → 🟢.

### 3.5 Производительность

- DFS по порталам: O(V + E), где V — число ячеек, E — число порталов. Для 100 ячеек + 200 порталов — мгновенно.
- Enable/disable Renderers: O(R), где R — число Renderers в невидимых ячейках. Для 1 000 Renderers — ~1 мс.
- Рекомендуемый интервал обновления: 0.1–0.2 с (не каждый кадр), чтобы не перегружать CPU.

### 3.6 Что portal-based НЕ решает

- Экстерьер (города, ландшафт) — нет чётких ячеек/порталов. Для экстерьера используем distance-based + GPU occlusion + impostors.
- Динамические объекты (корабли, NPC) — они не привязаны к ячейкам. Для них используем spatial hash + distance-based.

## 4. Distance-based culling manager (пишем сами)

### 4.1 Зачем

Distance-based culling — отключение объектов по дистанции (не только LOD, а полностью `Renderer.enabled = false` / `GameObject.SetActive(false)`). Критично для:
- Мелких пропсов (руины, камни, мусор) на дистанции > N метров.
- Интерьеров (мебель, детали) — не рендерить, если игрок далеко.
- Кораблей/NPC — не рендерить, если далеко.

### 4.2 Архитектура

**Компонент: `DistanceCullingManager`**

```
DistanceCullingManager : MonoBehaviour {
  [SerializeField] float cullDistance = 500f;       // дальше — полностью отключить
  [SerializeField] float lodDistance = 200f;        // дальше — переключить на LOD/impostor
  [SerializeField] float checkInterval = 0.5f;      // интервал проверки
  [SerializeField] Transform[] roots;               // корни для куллинга (PRIMUM, RuinsValleys, ...)

  List<CullGroup> groups;  // группы объектов (как в DetailDistanceCuller)
  float timer;

  void Update() {
    timer += Time.deltaTime;
    if (timer < checkInterval) return;
    timer = 0f;
    if (groups.Count == 0) Rebuild();
    Check();
  }

  void Rebuild() { ... }  // собрать группы из roots
  void Check() { ... }    // проверить дистанцию, включить/выключить
}
```

**Группы (как в `DetailDistanceCuller`):**
- Целыми подгруппами (хамлетами/кварталами), а не отдельными инстансами — чтобы не ломать инстансинг-батчи.
- Гистерезис 10% против мигания на границе.

**Интеграция с CullingGroup API:**
- Использовать `CullingGroup` для distance bands → Unity сам вызывает callback при пересечении границы.
- Это эффективнее, чем ручной `Update` с таймером, потому что Unity оптимизирует проверки.
- Ограничение: один camera на группу; только static occlusion как occluder.

### 4.3 FO-совместимость

- Позиции читаются живьём (`transform.position` / `bounds`) → 🟢.
- Дистанции считаются от текущей позиции камеры → 🟢.
- Никаких мировых `Vector3` в кэшах → 🟢.

### 4.4 Что куллить по дистанции

| Объект | Cull distance | LOD distance | Примечание |
|---|---|---|---|
| RuinsValleys (хамлеты) | 8 000 м | 4 000 м | Уже реализовано в `DetailDistanceCuller` |
| PRIMUM (здания) | не куллить целиком | 3 000 м → HLOD | Город нужен как ориентир |
| Двери (PRIMUM_DOORS) | 2 000 м | 500 м → simplified mesh | 2 161 инстанс, 84% draw calls |
| Корабли (дальние) | 5 000 м | 1 000 м → impostor | Крупные occluders |
| NPC | 500 м | 200 м → simplified | AI-оптимизация |
| Мелкие пропсы (камни, мусор) | 500 м | 200 м | DetailDistanceCuller-стиль |
| Интерьеры (мебель) | 100 м | 50 м | Только если игрок внутри здания |

## 5. Impostor system (пишем сами, без Amplify)

### 5.1 Зачем

Impostor — билборд с текстурами объекта с разных ракурсов. Для дальних объектов (>N м) заменяет полную геометрию на 2 треугольника + текстура. Экономит draw calls и GPU.

### 5.2 Архитектура (без покупок)

**Шаг 1: Bake (Editor tool, один раз)**

Создать Editor-инструмент (меню `ProjectC → Culling → Bake Impostor`), который:
1. Размещает камеру вокруг объекта (8–16 позиций на сфере/октаэдре).
2. Рендерит объект с каждой позиции в RenderTexture (цвет + глубина + нормаль).
3. Собирает атлас текстур (например, 4×4 = 16 видов).
4. Сохраняет атлас как Texture2D asset рядом с объектом (пути вне `*Textures*`).
5. Сохраняет метаданные (bounds, pivot, scale) в ScriptableObject или JSON.

**Шаг 2: Runtime switching**

Компонент `ImpostorSwitcher`:
```
ImpostorSwitcher : MonoBehaviour {
  [SerializeField] float impostorDistance = 500f;  // дальше — impostor
  [SerializeField] Texture2D atlas;                // baked atlas
  [SerializeField] int viewsPerAxis = 4;            // 4×4 = 16 видов
  [SerializeField] MeshRenderer originalRenderer;
  [SerializeField] MeshRenderer impostorRenderer;   // quad + impostor material

  void Update() {
    float d = Vector3.Distance(camera.position, transform.position);
    bool useImpostor = d > impostorDistance;
    originalRenderer.enabled = !useImpostor;
    impostorRenderer.enabled = useImpostor;
    if (useImpostor) UpdateView();
  }

  void UpdateView() {
    // Вычислить ближайший вид по углу камеры
    // Выбрать UV-rect в атласе
    // Применить к материалу impostorRenderer
  }
}
```

**Шаг 3: Impostor material (Shader Graph)**

Создать Shader Graph `ProjectC/Impostor`:
- Входы: `_Atlas` (Texture2D), `_UVRect` (Vector4), `_BillboardMatrix` (Matrix4x4).
- Биллбординг: квад всегда повёрнут к камере (camera-facing).
- Выбор вида: по углу камеры (yaw + pitch) → индекс в атласе → UV-rect.
- Depth write: impostor пишет глубину (для интерсекций с другими объектами).
- Cross-fade: dithering при переключении LOD↔impostor (опционально).

### 5.3 Octahedral impostors (продвинутый)

Octahedral impostors — более эффективны, чем spherical (меньше видов для того же качества). Алгоритм:
1. Разместить камеру на точках октаэдра (8–16 точек).
2. Рендерить объект с каждой точки.
3. В runtime: по углу камеры выбрать 4 ближайших вида → интерполяция (билинейная) → смешивание.

**Сложность:** требует custom shader с интерполяцией. Для начала можно использовать spherical (проще).

### 5.4 FO-совместимость

- Атласы хранятся как ассеты (не зависят от координат) → 🟢.
- Позиции читаются живьём → 🟢.
- Биллбординг вычисляется в camera-space → 🟢.

### 5.5 Ограничения

- Bake занимает время (8–16 рендеров на объект). Для 1 207 мешей города — долго, но делается один раз.
- Импосторы не обновляются при изменении объекта (если объект динамический/разрушаемый — нужен re-bake).
- Качество зависит от разрешения атласа (512×512 на вид → 2048×2048 атлас для 16 видов).
- Память: атласы занимают RAM/VRAM. Для 100 объектов × 2048×2048 × RGBA32 = ~1.6 ГБ → слишком много. Нужно сжатие (DXT/BC) и разумное разрешение (256×256 на вид).

### 5.6 Альтернатива: impostor-квады с простыми текстурами

Если bake слишком сложен, можно использовать простые биллборд-квады с одной текстурой (не octahedral). Качество ниже, но реализация проще:
- Квад всегда повёрнут к камере.
- Одна текстура (вид спереди).
- Для дальних объектов (>1 км) достаточно.

## 6. HLOD (пишем сами)

### 6.1 Зачем

HLOD (Hierarchical LOD) — один объединённый меш-прокси для группы объектов на дистанции. Снижает draw calls: вместо 100 мешей — 1 merged mesh.

### 6.2 Архитектура (без покупок)

**Шаг 1: Группировка (Editor tool)**

Создать Editor-инструмент (меню `ProjectC → Culling → Bake HLOD`), который:
1. Берёт группу объектов (например, все меша в чанке/квартале).
2. Объединяет их меша в один (используя `Mesh.CombineMeshes` или `MeshUtility`).
3. Упрощает объединённый меш (decimation) — опционально, можно использовать `MeshSimplifier` (open-source).
4. Сохраняет merged mesh как asset.
5. Создаёт GameObject с MeshFilter + MeshRenderer (HLOD proxy) на месте группы.
6. Скрывает оригинальные объекты при активации HLOD.

**Шаг 2: Runtime switching**

Компонент `HLODSwitcher`:
```
HLODSwitcher : MonoBehaviour {
  [SerializeField] float hlodDistance = 3000f;  // дальше — HLOD
  [SerializeField] GameObject originalGroup;     // оригинальные объекты
  [SerializeField] GameObject hlodProxy;         // merged mesh

  void Update() {
    float d = Vector3.Distance(camera.position, transform.position);
    bool useHLOD = d > hlodDistance;
    originalGroup.SetActive(!useHLOD);
    hlodProxy.SetActive(useHLOD);
  }
}
```

### 6.3 FO-совместимость

- HLOD proxy — ребёнок корня сцены → едет с корнем → 🟢.
- Позиции читаются живьём → 🟢.

### 6.4 Ограничения

- `Mesh.CombineMeshes` имеет лимит 65 535 вершин на меш (16-bit indices). Для больших групп нужен 32-bit indices (`mesh.indexFormat = IndexFormat.UInt32`).
- Объединённый меш теряет индивидуальность (нельзя кликнуть на отдельный дом). Для интерактива (двери) нужно оставить двери отдельными.
- Bake занимает время. Для 23 чанков города — долго, но делается один раз.
- Упрощение меша (decimation) требует алгоритма. Можно использовать open-source `MeshSimplifier` или `UnityEditor.MeshUtility` (если доступен).

### 6.5 Альтернатива: impostor вместо HLOD

Вместо merged mesh можно использовать impostor (биллборд) для дальних кластеров. Качество ниже, но реализация проще (не нужно объединять меша). Для L2 (12–80 км) impostor достаточен.

## 7. Custom Hi-Z occlusion (опционально, пишем сами)

### 7.1 Зачем

Custom Hi-Z — GPU-side occlusion culling с полным контролем. Если встроенный GPU Occlusion Culling (URP) недостаточен, пишем свой.

### 7.2 Архитектура

**Шаг 1: Depth mip-chain (Hi-Z buffer)**

Compute shader, который строит mip-chain depth buffer:
1. Взять depth texture текущего кадра.
2. Построить mip-уровни (каждый следующий — 2×2 min/max depth).
3. Сохранить в RenderTexture (mip-chain).

**Шаг 2: Occlusion test**

Compute shader, который для каждого объекта проверяет:
1. Вычислить screen-space AABB объекта.
2. Выбрать подходящий mip-уровень (по размеру AABB).
3. Сравнить depth объекта с Hi-Z depth в этой области.
4. Если объект полностью за occluder → mark as culled.

**Шаг 3: Culling result**

- Результат (массив bool/flags) читается CPU или используется GPU-side (через indirect draw).
- CPU-side: обновить `Renderer.enabled` для culled объектов.
- GPU-side: использовать indirect draw (более сложно, но эффективнее).

### 7.3 FO-совместимость

- Depth buffer в camera-space → не зависит от world origin → 🟢.
- Объекты проверяются по screen-space AABB → 🟢.

### 7.4 Ограничения

- Сложность реализации: 4–8 недель.
- Требует compute shaders (Direct3D12/Vulkan — у нас DX12, ок).
- Латентность 1 кадр (depth предыдущего кадра) → возможны артефакты при быстром движении.
- Для начала достаточно встроенного GPU Occlusion Culling (URP). Custom Hi-Z — только если встроенный недостаточен.

## 8. Интеграция с Floating Origin

### 8.1 Правила

1. **Все структуры куллинга хранятся в локальных координатах корней сцен.** При ребейзе корень сдвигается → локальные координаты не меняются → структуры не перестраиваются.
2. **Позиции камеры читаются живьём** (`camera.transform.position`) → каждый кадр. Не кэшировать.
3. **Дистанции считаются от текущей позиции камеры** → каждый кадр/по таймеру.
4. **Состояние объектов (открыта/закрыта дверь) хранится в экземплярных компонентах** → 🟢.
5. **Атласы/текстуры импосторов хранятся как ассеты** → не зависят от координат → 🟢.
6. **HLOD proxy — ребёнок корня сцены** → едет с корнем → 🟢.

### 8.2 Что НЕ делать

- Не хранить мировые `Vector3` в кэшах куллинга.
- Не использовать baked-данные (Umbra/Precomputed) — несовместимы с FO.
- Не использовать static batching на объектах, которые едут с миром.
- Не использовать `Camera.main` (две камеры с тегом) — использовать `PrimumLightPool.FindFocusCamera`-паттерн.

## 9. Этапы (без покупок)

### Этап W0: Немедленно (0–2 недели, бесплатно)
1. Включить Camera-Relative Culling (Lights + Shadows) в GraphicsSettings.
2. Включить Mesh LOD для всех импортируемых mesh (Import Settings).
3. Использовать CullingGroup API для distance bands (спавн/деспавн по дистанции).

### Этап W1: Адаптация шейдеров (2–4 недели)
- Привести кастомные шейдеры к `DOTS_INSTANCING_ON`.
- Включить GPU Resident Drawer + GPU Occlusion Culling + Forward+ + BRG Keep All.
- Приёмка: 0 shader errors, Frame Debugger показывает `Hybrid Batch Group`.

### Этап W2: Distance-based culling manager (1–2 недели)
- Реализовать `DistanceCullingManager` (по образцу `DetailDistanceCuller`).
- Интегрировать CullingGroup API.
- Настроить cull distances для разных типов объектов.

### Этап W3: Spatial partitioning (2–3 недели)
- Реализовать простой quadtree (или использовать NativeTrees).
- Реализовать простой spatial hash (или использовать Spatial-Hashing).
- Интегрировать в DistanceCullingManager.

### Этап W4: Portal-based culling (3–5 недель)
- Реализовать `PortalCell`, `Portal`, `PortalCullingManager`.
- Интегрировать с `DoorController` (дверь = портал).
- Настроить ячейки для кораблей и зданий.

### Этап W5: Impostor system (3–5 недель)
- Создать Editor-инструмент bake impostor (камера вокруг объекта → атлас).
- Создать Shader Graph `ProjectC/Impostor` (биллбординг + выбор вида).
- Реализовать `ImpostorSwitcher` (distance-based switching).
- Bake impostors для дальних объектов (деревья, здания, камни).

### Этап W6: HLOD (3–5 недель)
- Создать Editor-инструмент bake HLOD (объединение мешей → merged mesh).
- Реализовать `HLODSwitcher` (distance-based switching).
- Bake HLOD для дальних кластеров (кварталы/чанки).

### Этап W7: Custom Hi-Z (опционально, 4–8 недель)
- Только если встроенный GPU Occlusion недостаточен.
- Compute shader: depth mip-chain + occlusion test.

### Этап W8: Корабли (параллельно с W4–W6)
- LOD для корабля (корпус + детали).
- Impostor для дальнего корабля.
- Culling: frustum + occlusion + distance.
- Runtime spawn: пулинг (ObjectPool).
- NavMesh на деке: отдельный sandbox.

## 10. Оценка суммарного времени

| Этап | Оценка (недели) |
|---|---|
| W0 | 0–2 |
| W1 | 2–4 |
| W2 | 1–2 |
| W3 | 2–3 |
| W4 | 3–5 |
| W5 | 3–5 |
| W6 | 3–5 |
| W7 | 4–8 (опционально) |
| W8 | параллельно |
| **Итого (без W7)** | **~14–26 недель** |

## 11. Риски и митигация

| Риск | Митигация |
|---|---|
| Bake impostors/HLOD занимает много времени | Делать постепенно, по чанкам; приоритизировать дальние объекты |
| Качество impostors недостаточно | Увеличить разрешение атласа; использовать octahedral вместо spherical |
| Portal-based culling сложен для экстерьера | Использовать только для интерьеров; для экстерьера — distance + GPU occlusion + impostors |
| Custom Hi-Z слишком сложен | Использовать встроенный GPU Occlusion; custom Hi-Z — только если недостаточен |
| FO-несовместимость | Следовать правилам §8; тестировать F8/F9 после каждого этапа |
| Производительность CPU (DFS по порталам) | Использовать таймер (0.1–0.2 с) вместо каждого кадра |
| Память (атласы impostors) | Сжатие текстур (DXT/BC); разумное разрешение (256×256 на вид) |

## 12. Что НЕ делать

- Не использовать Umbra (baked occlusion) — несовместим с FO и динамикой.
- Не мигрировать на DOTS/Entities — мы на NGO GameObjects.
- Не покупать ассеты (по условию сценария).
- Не трогать ProjectSettings/Packages без согласования.
- Не склеивать город дальше (Э4 закрыт).
- Не запускать Play Mode / скриншоты (делает пользователь).
- Не писать `.meta` / `.asmdef` руками.
- Не использовать `Camera.main` (две камеры с тегом).

## 13. Источники

- `01_our_city_optimization.md` — наша оптимизация города.
- `02_internet_culling_assets.md` — интернет-ресерч ассетов/методов.
- `03_floating_origin_big_world.md` — Floating Origin и большие миры.
- `04_synthesis_and_plan.md` — синтез (с покупками).
- `docs/world/primum_city/ROADMAP.md` — дорожная карта.
- `docs/world/primum_city/06_REFERENCE/04_OCCLUSION_CULLING_RESEARCH.md` — Umbra failure.
- `docs/world/primum_city/06_REFERENCE/01_INDUSTRY_PRACTICE.md` — GPU-путь.
- `docs/world/floatingorigin/09S_NEW_CONTENT_PIPELINE.md` — пайплайн нового контента.
- `docs/world/optimization/LOD_VIEW_DISTANCE_DESIGN.md` — дизайн 4 колец.

---

*Видение подготовлено как исследовательский материал. Код не написан; рекомендации применяются через отдельные тикеты с одобрения пользователя.*
