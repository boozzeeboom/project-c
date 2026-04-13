# Unity Specialist — Практическое руководство по реализации мира

**Версия:** 1.0 | **Дата:** 13 апреля 2026 г.
**Автор:** Unity Specialist
**Объект:** Project C: The Clouds — фиксированный мир (5 массивов, 28 пиков, 890+ облаков)
**Двигатель:** Unity 6, URP 17.4.0, Input System

---

## 1. URP НАСТРОЙКИ ДЛЯ БОЛЬШОГО МИРА

### 1.1. Текущее состояние

**Файлы конфигурации:**
- `Assets/_Project/Settings/ProjectC_URP.asset` — URP Pipeline Asset (версия 13, Unity 6)
- `Assets/_Project/Settings/ProjectC_URP_Renderer.asset` — UniversalRendererData (пустой, без Renderer Features)
- `GraphicsSettings.asset` → `m_CustomRenderPipeline` ссылается на ProjectC_URP (корректно)
- `m_LastMaterialVersion: 10` — материалы конвертированы в URP

**Критичные параметры текущего URP Asset:**

| Параметр | Текущее значение | Рекомендация | Причина |
|----------|-----------------|-------------|---------|
| `m_MSAA` | 1 (None) | **2 (Medium)** | Облака с alpha blend требуют сглаживания краёв |
| `m_ShadowDistance` | 50 | **500+** | Горы на расстоянии не получают теней — выглядит плоско |
| `m_CascadeCount` | 1 | **4 (Four Cascades)** | Один каскад не покрывает 28 пиков |
| `m_AdditionalLightsRenderingMode` | 1 (PerObject) | **1 (PerObject)** | OK — городам нужны точечные источники |
| `m_AdditionalLightsPerObjectLimit` | 4 | **2** | Экономия производительности, городам хватит 2 источников |
| `m_AdditionalLightShadowsSupported` | 0 | **0** | Тени от точечных источников — слишком дорого |
| `m_SupportsHDR` | 1 | **1** | Нужно для emissive маяков и завесы |
| `m_HDRColorBufferPrecision` | 0 (16-bit) | **0 (16-bit)** | Достаточно для emissive |
| `m_SupportsDynamicBatching` | 0 | **0** | Не включать — конфликтует с GPU Instancing |
| `m_UseSRPBatcher` | 1 | **1** | Включён — правильно |
| `m_VolumeFrameworkUpdateMode` | 0 (OnLoad) | **0 (OnLoad)** | Volume Profile не меняется runtime |
| `m_VolumeProfile` | **null** | **Создать** | КРИТИЧНО — без него нет fog, post-processing |
| `m_RequireDepthTexture` | 0 | **1** | Нужно для Depth Fade завесы и cloud soft edges |
| `m_SupportsLightLayers` | 0 | **1** | Нужно для разделения освещения зон |

### 1.2. URP Forward Renderer — какие passes нужны

**UniversalRendererData сейчас пустой (`m_RendererFeatures: []`). Это правильно** — лишние passes только тормозят.

**Какие passes НЕ нужны (отключить/не добавлять):**

| Pass | Статус | Причина |
|------|--------|---------|
| **Rendering** (default Forward) | **НУЖЕН** | Основной pass |
| **Depth Prepass** | НЕ НУЖЕН | Depth Texture достаточно (m_RequireDepthTexture = 1) |
| **Opaque Downsample** | НЕ НУЖЕН | m_OpaqueDownsampling = 1 — оставить |
| **SSAO** | НЕ НУЖЕН | Для cel-shaded мира не нужен, добавляет 3-5ms |
| **Bloom** | **НУЖЕН** | Emissive маяков, теплиц, завеса — без Bloom теряется эффект |
| **Color Adjustments** | **НУЖЕН** | Динамическая тонировка по климатическим зонам |
| **Depth of Field** | НЕ НУЖЕН | Cel-shaded стиль не использует DOF |
| **Motion Blur** | НЕ НУЖЕН | Дешевле делать в shader (vertex displacement) |
| **Vignette** | НЕ НУЖЕН | Субъективно, для прототипа не нужно |
| **Film Grain** | НЕ НУЖЕН | Для Ghibli стиля не подходит |

**Рекомендуемый минимальный набор Renderer Features:**

```
ProjectC_URP_Renderer (UniversalRendererData):
  [x] Post Process (встроенный, не через Renderer Feature)
  [x] Depth Texture (встроенный, m_RequireDepthTexture = 1)
  [+] Bloom (через Volume Profile, не Renderer Feature)
  [+] Color Adjustments (через Volume Profile)
```

**Важно:** Bloom и Color Adjustments настраиваются НЕ через Renderer Features, а через **Volume Profile**. Renderer Features — это только для кастомных passes (например, custom cloud renderer).

### 1.3. GPU Instancing для CloudGhibli.shader

**Текущее состояние:** CloudGhibli.shader НЕ поддерживает GPU Instancing.

**Проблема:** Нет директивы `#pragma multi_compile_instancing` и нет `UNITY_VERTEX_INPUT_INSTANCE_ID` в шейдере. 890 облаков = 890 draw calls.

**Что добавить в CloudGhibli.shader:**

```hlsl
// В начало SubShader Pass:
#pragma multi_compile_instancing
#pragma multi_compile _ CLOUD_CLIMATE_TINTING_ON

// В struct Attributes:
UNITY_VERTEX_INPUT_INSTANCE_ID

// В struct Varyings:
UNITY_VERTEX_INPUT_INSTANCE_ID

// В vert():
UNITY_SETUP_INSTANCE_ID(input);
UNITY_TRANSFER_INSTANCE_ID(input, output);

// Climate tinting — переменные instanced:
UNITY_INSTANCING_BUFFER_START(Props)
    UNITY_DEFINE_INSTANCED_PROP(half4, _InstanceColor)
    UNITY_DEFINE_INSTANCED_PROP(half, _InstanceDensity)
UNITY_INSTANCING_BUFFER_END(Props)
```

**Альтернатива (проще, без instanced properties):**

CloudGhibli уже использует vertex displacement и noise для вариативности. GPU Instancing будет работать, если все облака используют **один и тот же материал с одинаковыми свойствами**. Вариативность достигается через:
- Разный масштаб (transform.localScale)
- Разную позицию UV (transform.position влияет на worldPos)
- Noise texture даёт визуальную уникальность

**Рекомендация:** Добавить `#pragma multi_compile_instancing` — это даст 890 -> 1-2 draw call без изменения логики шейдера. Climate tinting реализовать через **MaterialPropertyBlock** на каждом облаке (дешевле, чем instanced properties, и не ломает batching).

**Пошаговая инструкция GPU Instancing:**

1. В CloudGhibli.shader, в `HLSLPROGRAM` добавить:
   ```hlsl
   #pragma multi_compile_instancing
   ```

2. В `Attributes` добавить:
   ```hlsl
   UNITY_VERTEX_INPUT_INSTANCE_ID
   ```

3. В `Varyings` добавить:
   ```hlsl
   UNITY_VERTEX_INPUT_INSTANCE_ID
   ```

4. В начале `vert()`:
   ```hlsl
   UNITY_SETUP_INSTANCE_ID(input);
   UNITY_TRANSFER_INSTANCE_ID(input, output);
   ```

5. В начале `frag()`:
   ```hlsl
   UNITY_SETUP_INSTANCE_ID(input);
   ```

6. На материале облаков включить: Inspector → Material → Enable GPU Instancing (галка)

7. В CloudLayer.cs при создании облаков использовать **один Material instance** для всех облаков слоя (не MaterialPropertyBlock, а sharedMaterial).

**Ожидаемый результат:** 890 draw calls -> 1-3 draw call для облаков.

### 1.4. URP Volume Profile — динамическое освещение для 5 климатических зон

**Текущее состояние:** `m_VolumeProfile: {fileID: 0}` — Volume Profile НЕ создан.

**Проблема:** Без Volume Profile нет global fog, bloom, color grading. Завеса и климатические зоны не работают.

**Что создать:**

**Volume Profile:** `Assets/_Project/Settings/WorldVolumeProfile.asset`

| Override | Значение | Назначение |
|----------|----------|-----------|
| **Exponential Fog** | Distance=0.001, Start=50, Color=#8a8a8a | Базовый туман для завесы |
| **Bloom** | Threshold=0.8, Intensity=0.5, Scatter=0.7, Clamp=65472 | Emissive маяков |
| **Color Adjustments** | Post-exposure=0, Contrast=5, Saturation=0 | Базовая коррекция |
| **Shadows Midtones Highlights** | Shadows=чуть теплее, Highlights=чуть холоднее | Ghibli-стиль |

**Динамическое освещение по зонам:**

5 климатических зон НЕ могут использовать один Volume Profile. Варианты:

**Вариант A: Global Volume + Local Volumes (рекомендуется)**

- **Global Volume** (бесконечный) — базовые настройки (fog, bloom)
- **5 Local Volume** (Box shape, по одному на массив) — климатический тинт
  - Каждый Local Volume имеет свой `Color Adjustments` с уникальным tint
  - ShipController определяет, в каком массиве находится, и применяет blending

**Вариант B: Runtime Volume Override**

- Один Global Volume
- ShipController меняет параметры Volume через скрипт при входе в зону
- Дешевле, но менее гладкий переход

**Рекомендация — Вариант A:**

```
Volume Hierarchy:
├── GlobalVolume (infinite)
│   ├── Exponential Fog: density=0.001, color=#8a8a8a
│   ├── Bloom: threshold=0.8, intensity=0.5
│   └── Color Adjustments: neutral
├── PrimusVolume (Box: centered on Everest, 2000x200x2000)
│   └── Color Adjustments: tint=#e8dcc8 (тёплый, тропический)
├── SecundusVolume (Box: centered on Mont Blanc)
│   └── Color Adjustments: tint=#c8d4e8 (холодный, альпийский)
├── KiboVolume (Box: centered on Kilimanjaro)
│   └── Color Adjustments: tint=#e8d8c0 (сухой, африканский)
├── TertiusVolume (Box: centered on Aconcagua)
│   └── Color Adjustments: tint=#d0dce8 (умеренный, андийский)
└── QuartusVolume (Box: centered on Denali)
    └── Color Adjustments: tint=#c0d0e0 (арктический)
```

**Настройка в Unity:**
1. Create → Volume → Global Volume
2. В Profile добавить Exponential Fog, Bloom, Color Adjustments
3. Create → Volume → Local Volume (5 штук)
4. Каждый Local Volume: Mode = Local, Shape = Box, задать размер
5. В Profile каждого Local Volume добавить Color Adjustments с уникальным tint

**Важно:** URP автоматически blend-ит Volume когда камера пересекает границы. Никакого кода не нужно — это built-in feature.

### 1.5. URP LOD Group — баги и ограничения Unity 6

**Известные проблемы LOD Group в Unity 6 (URP 17):**

| Проблема | Описание | Обходное решение |
|----------|----------|-----------------|
| **LOD Crossfade glitch** | При crossfade видны оба LOD одновременно на 1-2 кадра | `m_LODCrossFadeDitheringType: 1` — использовать dithering (уже включено в ProjectC_URP) |
| **LOD2 Billboards не генерируются автоматически** | Нужно создавать billboards вручную | Сессия A4: создать billboard mesh для каждого пика |
| **LOD Group + GPU Instancing конфликт** | Instancing отключается на объектах с LOD Group | **КРИТИЧНО** — облака с GPU Instancing НЕ могут использовать LOD Group |
| **Frustum culling bounds incorrect** | После LOD switching bounds не обновляются | Вызывать `Renderer.updateHit()` после LOD change |
| **LOD Group + SkinnedMeshRenderer** | Не поддерживается | Не касается проекта (нет skinned meshes) |

**Рекомендация для Project C:**

**Горы (пики, хребты):** Использовать LOD Group — они статичные, не instanced.
- LOD0: 2048 tris, 0-500 units
- LOD1: 512 tris, 500-1500 units
- LOD2: Billboard, 1500-5000 units

**Облака:** НЕ использовать LOD Group (конфликт с GPU Instancing). Вместо этого:
- Distance-based culling в CloudLayer.cs — не создавать облака дальше 3000 units от камеры
- LOD через разные mesh-и (sphere с разными segment count) — но НЕ через LOD Group

---

## 2. MESH GENERATION RUNTIME

### 2.1. Memory management

**Когда меши создаются:**
- `FixedWorldGenerator.Start()` — создание всех 28 пиков + 15 хребтов + 9 ферм
- Время загрузки: ~1-2 секунды (async)

**Когда меши уничтожаются:**
- **НИКОГДА.** Мир фиксированный и не меняется. Меши живут всю сессию.

**Memory budget (из Landscape_TechnicalDesign.md):**

| Объект | Mesh размер (VRAM) | Итого |
|--------|-------------------|-------|
| 28 пиков (LOD0) | ~128 KB каждый | 3.5 MB |
| 15 хребтов (LOD0) | ~64 KB каждый | 1 MB |
| Фермы + города | ~32 KB каждый | 0.5 MB |
| **Итого** | | **~5 MB** |

**Это negligible.** Современный GPU имеет 4-8 GB VRAM.

### 2.2. Mesh Collider — нужен ли на каждом пике?

**Рекомендация: НЕТ.** MeshCollider на каждом пике — это 28 мешей с collision mesh, что даёт:

- 28 PhysicsCollider в PhysX
- Каждый кадр: проверка коллизий корабля с 28 мешами
- При радиусе 600 units — PhysX может варнить о large triangle

**Альтернативы:**

| Вариант | Точность | Производительность | Подходит |
|---------|----------|-------------------|----------|
| **MeshCollider** | Высокая | Низкая (28 сложных collider) | Прототип |
| **CapsuleCollider** | Средняя | Высокая (1 на пик) | **Рекомендуется** |
| **SphereCollider** | Низкая | Очень высокая | Прототип |
| **BoxCollider** | Низкая | Очень высокая | Не подходит (горы не боксы) |
| **TerrainCollider** | Высокая | Средняя | Не подходит (нет Terrain) |
| **Altitude check** | Средняя | Максимальная | Геймплей |

**Рекомендация: Composite подход**

1. **Каждый пик:** CapsuleCollider (ориентированный вертикально) — базовая коллизия
2. **Геймплейная защита:** ShipController проверяет высоту корабля vs высота ближайшего пика. Если корабль ниже safe altitude — force push-up (уже реализовано в AltitudeCorridorSystem).
3. **MeshCollider только для городов:** города имеют сложную геометрию зданий, где коллизия важна.

```
Для каждого пика:
  CapsuleCollider:
    - Center: (0, height/2, 0)
    - Height: height * 0.9
    - Radius: radius * 0.3
    - Direction: Y (vertical)
```

### 2.3. Mesh baking vs runtime generation

**Вариант A: Runtime generation (текущий план)**

Плюсы:
- Гибкость — можно менять параметры мира без пересоздания мешей
- Меньше размер билда (нет pre-baked мешей в ассетах)
- Проще итерировать на дизайне

Минусы:
- Загрузка 1-2 секунды при старте
- GC allocation при создании мешей
- Сложнее дебажить (меши не видны в Editor до play mode)

**Вариант B: Pre-baked меши (asset files)**

Плюсы:
- Мгновенная загрузка
- Меши видны в Editor
- Можно оптимизировать меши вручную

Минусы:
- Больше размер билда (~5 MB мешей)
- Сложнее менять дизайн (нужно re-bake)
- Нужно писать Editor-скрипты для baking

**Рекомендация: Runtime для прототипа, Pre-baked для релиза**

Для текущего прототипа — runtime generation (сессия A6). Когда дизайн стабилен — написать Editor-скрипт, который:
1. Запускает FixedWorldGenerator в Editor mode
2. Сохраняет все меши как `.asset` файлы
3. FixedWorldGenerator загружает pre-baked меши вместо генерации

### 2.4. Как избежать GC spikes при генерации 28+ мешей

**Проблема:** Создание 28 Mesh объектов с тысячами вершин — это несколько MB allocation, что вызывает GC spike.

**Решения:**

**1. Async mesh generation (Coroutine):**

```csharp
// Разбить генерацию на фреймы
IEnumerator GenerateWorldAsync() {
    for (int i = 0; i < massifs.Length; i++) {
        yield return null; // Пропустить 1 кадр между массивами
        GenerateMassif(massifs[i]);
    }
}
```

**2. Mesh.Reuse() — Unity 6 feature:**

Unity 6 поддерживает переиспользование Mesh объектов. Вместо создания нового Mesh каждый раз:

```csharp
// Один раз создать reusable mesh pool
private List<Mesh> _meshPool = new List<Mesh>();

Mesh GetOrCreateMesh() {
    if (_meshPool.Count > 0) {
        var mesh = _meshPool[0];
        _meshPool.RemoveAt(0);
        mesh.Clear();
        return mesh;
    }
    return new Mesh();
}
```

**3. Mesh.MarkDynamic() для runtime-updated мешей:**

Если меш обновляется (например, LOD generation):

```csharp
mesh.MarkDynamic(); // Оптимизирует для частых обновлений
```

**4. ObjectPool для GameObject-ов пиков:**

```csharp
ObjectPool<PeakGameObject> _peakPool = new ObjectPool<PeakGameObject>(
    createFunc: () => new PeakGameObject(),
    actionOnRelease: (peak) => peak.gameObject.SetActive(false)
);
```

**Рекомендация:** Для прототипа достаточно Coroutine-based async generation (вариант 1). Mesh pool и ObjectPool — на этапе оптимизации.

---

## 3. CLOUD SYSTEM OPTIMIZATION

### 3.1. GPU Instancing (подробно)

**Текущее состояние:** 890+ облаков, каждое — отдельный GameObject со Sphere MeshRenderer + CloudGhibli material.

**Draw call analysis:**
- Без instancing: 890 draw calls (1 на облако)
- С instancing: 1-3 draw calls (batch по материалу)
- Со SRP Batcher: ещё лучше (SRP Batcher уже включён: `m_UseSRPBatcher: 1`)

**GPU Instancing + SRP Batcher — совместимость:**

Они работают ВМЕСТЕ. SRP Batcher оптимизирует shader variant setup, GPU Instancing оптимизирует draw calls. Оба включены — идеально.

**Что нужно сделать:**

1. **Добавить instancing в CloudGhibli.shader** (см. секцию 1.3)
2. **Использовать sharedMaterial для всех облаков** — НЕ создавать Material instance для каждого облака:

```csharp
// НЕПРАВИЛЬНО (ломает instancing):
cloudRenderer.material = cloudMaterial; // Создаёт instance

// ПРАВИЛЬНО (сохраняет instancing):
cloudRenderer.sharedMaterial = cloudMaterial; //共用 material
```

3. **Вариативность без MaterialPropertyBlock** — MaterialPropertyBlock ЛОМАЕТ GPU Instancing. Вместо этого:
   - Вариативность через transform.scale (разные размеры)
   - Вариативность через UV offset (разные позиции UV)
   - Climate tinting через vertex color (передать через mesh colors)

### 3.2. Compute Shaders — стоит ли?

**Compute Shaders для облаков — это overkill для текущего масштаба.**

890 облаков — это мало для compute shader overhead. Compute shader эффективен при 10,000+ объектов.

**Когда рассмотреть Compute Shaders:**
- Если нужно 5000+ облаков
- Если нужна физика облаков (wind simulation, collision)
- Если нужна dynamic density simulation

**Сейчас:** GPU Instancing достаточно.

### 3.3. Climate tinting в CloudGhibli.shader

**Текущее состояние:** CloudGhibli.shader имеет `_BaseColor` (uniform для всех облаков). Climate tinting требует уникального цвета для каждого облака.

**Проблема:** С GPU Instancing нельзя использовать MaterialPropertyBlock.

**Решение: Vertex Color tinting**

Передать климатический tint через vertex color (mesh.colors):

```hlsl
// В Attributes:
half4 color : COLOR;

// В Varyings:
half4 tint : TEXCOORD5;

// В vert():
output.tint = input.color; // Передать vertex color

// В frag():
half3 cloudColor = _BaseColor.rgb * combinedNoise * input.tint.rgb;
```

**Настройка mesh.colors при создании облака:**

```csharp
// В CloudLayer.cs при создании облака:
Color tintColor = GetClimateColorForPosition(cloudPosition);
Color[] colors = new Color[vertices.Length];
for (int i = 0; i < colors.Length; i++)
    colors[i] = tintColor;
mesh.colors = colors;
```

**Climate цвета по массивам (из WorldLandscape_Design.md):**

| Массив | Climate Tint | Описание |
|--------|-------------|----------|
| Примум (Эверест) | #e8dcc8 | Тёплый, тропический |
| Секунд (Монблан) | #c8d4e8 | Холодный, альпийский |
| Кибо (Килиманджаро) | #e8d8c0 | Сухой, африканский |
| Тертиус (Аконкагуа) | #d0dce8 | Умеренный, андийский |
| Квартус (Денали) | #c0d0e0 | Арктический |

**Альтернатива (проще):** Передавать tint через `_BaseColor` на material, но использовать **5 разных материалов** (по одному на климатическую зону). Облака в каждой зоне используют свой материал. Это НЕ ломает instancing (instancing группирует по материалу).

**Рекомендация:** 5 материалов для облаков — проще, надёжнее, сохраняет GPU Instancing.

### 3.4. Cumulonimbus — вертикальные столбы

**Архитектура: 3 секции (рекомендация Technical Director)**

```
┌─────────────────────┐
│   Anvil (наковальня) │  Y=75-90+  | Расширенный цилиндр
├─────────────────────┤
│   Body (тело)        │  Y=30-75   | Цилиндр
├─────────────────────┤
│   Base (основание)   │  Y=12-30   | Цилиндр, merge с завесой
└─────────────────────┘
```

**Shader: модификация CloudGhibli, не новый**

CloudGhibli уже поддерживает:
- Vertex displacement (морфинг формы)
- Noise-based volume
- Rim glow

Для cumulonimbus нужно добавить:
- **Вертикальную вытянутость** — через transform.scale (Y намного больше X,Z)
- **Anvil расширение** — через отдельный mesh (sphere на вершине)
- **Фиолетовый base у завесы** — через climate tinting (цвет завесы #2d1b4e)

**Не нужен новый shader.** Нужен **CumulonimbusMaterial** — вариация CloudGhibli material с:
- `_BaseColor` = #5a5a5a (тёмно-серый)
- `_VertexDisplacement` = 5.0 (больше, чем у обычных облаков)
- `_RimColor` = #b366ff (фиолетовый glow)

**Молнии:**

**Particle System (рекомендация для прототипа):**
- Particle System с Sub Emitters (spark при ударе молнии)
- Lightning bolt через Line Renderer с random points
- Trigger: каждые 20-60 секунд (random)

**VFX Graph (отложить на пост-прототип):**
- Более красивые молнии с branching
- Но требует VFX Graph package и настройки

**Рекомендация:** Particle System + Line Renderer для прототипа.

---

## 4. SCRIPTABLEOBJECT ARCHITECTURE

### 4.1. Организация ассетов в Project window

**Текущая структура:**

```
Assets/_Project/
├── Data/
│   ├── AltitudeCorridors/      # 6 corridor ассетов
│   ├── Modules/                # Module ассеты
│   └── WindZones/              # Wind ассеты
├── Art/
│   ├── CloudLayerConfig.asset  # 3 layer конфига
│   └── Resources/
│       └── WorldGenerationSettings.asset
└── Settings/
    ├── ProjectC_URP.asset
    └── ProjectC_URP_Renderer.asset
```

**Рекомендуемая структура для фиксированного мира:**

```
Assets/_Project/
├── Data/
│   └── World/
│       ├── WorldData.asset                         # Главный ассет мира
│       ├── Massifs/
│       │   ├── Massif_Primus.asset                 # Эверест + 6 пиков
│       │   ├── Massif_Secundus.asset               # Монблан + 5 пиков
│       │   ├── Massif_Kibo.asset                   # Килиманджаро + 5 пиков
│       │   ├── Massif_Tertius.asset                # Аконкагуа + 6 пиков
│       │   └── Massif_Quartus.asset                # Денали + 6 пиков
│       ├── Peaks/
│       │   ├── Peak_Everest.asset
│       │   ├── Peak_MontBlanc.asset
│       │   ├── ... (28 peak ассетов)
│       ├── Ridges/
│       │   ├── Ridge_Primus_Main.asset
│       │   ├── ... (15 ridge ассетов)
│       ├── Farms/
│       │   ├── Farm_Don1010.asset
│       │   ├── ... (9 farm ассетов)
│       └── Climate/
│           ├── Biome_Tropical.asset
│           ├── Biome_Alpine.asset
│           ├── Biome_Arid.asset
│           ├── Biome_Temperate.asset
│           └── Biome_Arctic.asset
│
├── Art/
│   ├── Clouds/
│   │   ├── CloudGhibli.shader
│   │   ├── CloudGhibli_Tropical.mat
│   │   ├── CloudGhibli_Alpine.mat
│   │   ├── CloudGhibli_Arid.mat
│   │   ├── CloudGhibli_Temperate.mat
│   │   ├── CloudGhibli_Arctic.mat
│   │   ├── CloudLayerConfig_Upper.asset
│   │   ├── CloudLayerConfig_Middle.asset
│   │   └── CloudLayerConfig_Lower.asset
│   ├── Materials/
│   │   ├── Rock_Standard.mat
│   │   ├── Snow_Standard.mat
│   │   ├── Emissive_Beacon_Primus.mat
│   │   └── ... (материалы городов/ферм)
│   └── Textures/
│       ├── Noise_Cloud_01.png
│       ├── Noise_Cloud_02.png
│       └── ... (текстуры)
│
├── Prefabs/
│   ├── World/
│   │   ├── Peak_Prefab.prefab                      # Базовый префаб пика
│   │   ├── Ridge_Prefab.prefab
│   │   ├── FarmPlatform.prefab
│   │   └── FerryStation.prefab
│   ├── Cities/
│   │   ├── CityModule_Small.prefab
│   │   ├── CityModule_Medium.prefab
│   │   ├── CityModule_Platform.prefab
│   │   ├── CityModule_Bridge.prefab
│   │   ├── CityModule_Beacon.prefab
│   │   ├── CityModule_Dome.prefab
│   │   ├── City_Primus.prefab                      # Полный город
│   │   └── ... (4 других города)
│   └── Clouds/
│       ├── Cloud_Prefab.prefab
│       └── Cumulonimbus_Prefab.prefab
│
├── Scenes/
│   ├── ProjectC_Main.unity                         # Главная сцена
│   └── SubScenes/
│       ├── SubScene_Primus.unity                   # Sub-scene для Примум
│       ├── SubScene_Secundus.unity
│       └── ... (4 других sub-scene)
│
├── Settings/
│   ├── ProjectC_URP.asset
│   ├── ProjectC_URP_Renderer.asset
│   ├── WorldVolumeProfile.asset
│   └── InputSystem_Actions.inputactions
│
└── Scripts/
    └── World/
        ├── WorldData.cs
        ├── MountainMassif.cs
        ├── PeakData.cs
        ├── FixedWorldGenerator.cs
        ├── MountainMeshBuilder.cs
        ├── RidgeMeshBuilder.cs
        ├── MountainLOD.cs
        ├── FarmPlatform.cs
        ├── CityBuilder.cs
        └── WorldBridge.cs
```

### 4.2. Addressables — нужно ли?

**НЕТ для текущего масштаба.**

Аргументы:
- Мир помещается в память: ~10-15 MB (меши + текстуры + данные)
- 5 массивов — недостаточно для addressable overhead
- Addressables добавляет сложность билда и runtime overhead

**Когда рассмотреть Addressables:**
- Мир расширяется до 20+ массивов
- Нужно загружать/выгружать массивы по мере приближения камеры
- Мультиплеер с разными версиями мира

**Сейчас:** ScriptableObject + Resources.LoadScene (если нужно) достаточно.

### 4.3. Обновление ассетов без потери ссылок

**Проблема:** При изменении структуры ScriptableObject (добавление/удаление полей) Unity может потерять ссылки.

**Защита:**

1. **НЕ переименовывать поля** в ScriptableObject классах. Если нужно новое имя — добавить новое поле, скопировать данные через Editor-скрипт, удалить старое.

2. **Использовать `[FormerlySerializedAs]`** при переименовании:

```csharp
[FormerlySerializedAs("oldFieldName")]
[SerializeField] private int newFieldName;
```

3. **НЕ перемещать .asset файлы** через файловую систему. Перемещать только через Unity Editor (drag-and-drop в Project window).

4. **GUID сохраняются при перемещении через Unity Editor.** Если файл перемещён вне Unity — ссылка сломается.

5. **Version control для .meta файлов.** Каждый .asset имеет .meta файл с GUID. Обязательно коммитить .meta файлы в git.

---

## 5. SCENE ORGANIZATION

### 5.1. Организация главной сцены

**Рекомендация: ОДНА сцена без sub-scenes для прототипа.**

**Текущий размер мира:** 5 массивов, 28 пиков, 15 хребтов, 9 ферм, 5 городов — всё помещается в ~15 MB. Sub-scene overhead не оправдан.

**Когда sub-scenes нужны:**
- Проект > 100 MB ассетов
- Открытый мир с streaming
- Командная работа (разные дизайнеры на разных сценах)

**Структура ProjectC_Main.unity:**

```
Scene Hierarchy:
├── ── Systems ──
│   ├── CloudSystem (CloudSystem.cs)
│   ├── AltitudeCorridorSystem
│   ├── VeilSystem (B1)
│   ├── FixedWorldGenerator (A6)
│   ├── DayNightController
│   └── WorldVolumeManager
│
├── ── World ──
│   ├── Massif_Primus (runtime-generated)
│   ├── Massif_Secundus
│   ├── Massif_Kibo
│   ├── Massif_Tertius
│   └── Massif_Quartus
│
├── ── UI ──
│   ├── Canvas_Main (HUD, Altitude, Speed)
│   └── Canvas_Minimap
│
├── ── Player ──
│   ├── Ship (ShipController v2.7)
│   └── CameraRig
│
├── ── Lighting ──
│   ├── Sun (Directional Light)
│   ├── GlobalVolume
│   └── LocalVolumes (5 штук)
│
└── ── Debug ──
    ├── DebugOverlay
    └── GizmoControllers
```

### 5.2. Префабы городов и ферм

**Городские модули:**

Каждый город — набор префабов-модулей. Модули размещаются вручную на пике (не процедурно).

```
CityModule_Small.prefab:
  ├── Mesh ( LOD Group с 3 уровнями)
  ├── CapsuleCollider
  ├── Material (Rock_Standard или Snow_Standard)
  └── Emissive_Beacon (если это маяк)
```

**Рекомендация для редактирования:**

1. Создать **CityPrefab_Preview.unity** — отдельная сцена для сборки городов
2. Разместить пик (меш пика) в центре сцены
3. Расставить модули вручную
4. Сохранить как City_Primus.prefab
5. FixedWorldGenerator инстанцирует City_Primus.prefab на нужной позиции

**Фермы аналогично:**

FarmPlatform.prefab — полный префаб с террасами, зданиями, ирригацией. FixedWorldGenerator размещает его на позиции хребта.

---

## 6. FEATURE TOGGLE

### 6.1. Безопасное переключение с WorldGenerator на FixedWorldGenerator

**Рекомендация: Feature Toggle через ScriptableObject, не через enum.**

```csharp
// WorldGenerationMode.asset (ScriptableObject)
public class WorldGenerationMode : ScriptableObject {
    public bool useFixedGenerator = false;
    public int worldVersion = 1;
}
```

**В сцене:**

```
WorldGenerator (active: useFixedGenerator == false)
FixedWorldGenerator (active: useFixedGenerator == true)
```

**Почему ScriptableObject, не enum:**
- Можно менять без перекомпиляции
- Можно создать разные ассеты для разных тестовых сцен
- Легко откатиться (change 1 значение в ассете)

### 6.2. WorldBridge адаптер

`WorldCamera.cs` и `PeakNavigationUI.cs` зависят от `WorldGenerator.GetAllPeaks()`.

**Решение:** Создать `IWorldDataProvider`:

```csharp
public interface IWorldDataProvider {
    List<PeakInfo> GetAllPeaks();
    WorldData GetWorldData();
}
```

`WorldGenerator` и `FixedWorldGenerator` оба реализуют этот интерфейс. `WorldCamera` и `PeakNavigationUI` зависят от интерфейса, не от конкретного класса.

**WorldBridge.cs** — singleton, который находит активный IWorldDataProvider:

```csharp
public class WorldBridge : MonoBehaviour {
    public static IWorldDataProvider Instance { get; private set; }

    void Awake() {
        // Найти активный генератор
        var fixedGen = FindObjectOfType<FixedWorldGenerator>();
        if (fixedGen != null && fixedGen.enabled) {
            Instance = fixedGen;
        } else {
            Instance = FindObjectOfType<WorldGenerator>();
        }
    }
}
```

---

## 7. DEBUGGING TOOLS

### 7.1. Необходимые инструменты отладки

| Инструмент | Назначение | Приоритет |
|------------|-----------|-----------|
| **Gizmos для хребтов** | Визуализация ridge lines в Scene view | P0 |
| **Debug draw для коридоров высот** | Показать min/max altitude corridor | P0 |
| **Wireframe mode** | Проверка mesh topology гор | P0 |
| **Peak info overlay** | Показывать название/высоту пика при наведении | P1 |
| **Cloud density visualization** | Heatmap плотности облаков | P1 |
| **FPS + Draw Call counter** | Stats overlay в реальном времени | P0 |
| **Altitude indicator** | Текущая высота корабля с цветовой индикацией | P0 |
| **Volume boundary gizmos** | Показать границы Local Volumes | P2 |

### 7.2. Реализация

**Gizmos для хребтов (MountainMeshBuilder):**

```csharp
void OnDrawGizmosSelected() {
    Gizmos.color = Color.yellow;
    // Draw ridge line as series of points
    for (int i = 0; i < ridgePoints.Length - 1; i++) {
        Gizmos.DrawLine(ridgePoints[i], ridgePoints[i + 1]);
    }
    // Draw peak positions
    Gizmos.color = Color.red;
    foreach (var peak in peaks) {
        Gizmos.DrawWireSphere(peak.position, 10f);
    }
}
```

**Debug draw для коридоров:**

```csharp
void OnDrawGizmos() {
    // Draw corridor min/max as horizontal planes
    Gizmos.color = Color.green; // Safe zone
    DrawHorizontalPlane(corridor.minAltitude, 5000f);
    Gizmos.color = Color.red; // Danger zone
    DrawHorizontalPlane(corridor.maxAltitude, 5000f);
}
```

**Wireframe mode:**

В Game view: Gizmos -> Wireframe (встроенная функция Unity).

Для кастомного wireframe — добавить toggle в ShipController:

```csharp
if (Input.GetKeyDown(KeyCode.F3)) {
    Camera.main.allowHDR = !Camera.main.allowHDR; // Toggle wireframe-ish
}
```

**FPS + Draw Call counter:**

```csharp
void OnGUI() {
    GUILayout.Label($"FPS: {1f / Time.deltaTime:F1}");
    GUILayout.Label($"Draw Calls: {UnityStats.batches}");
    GUILayout.Label($"Triangles: {UnityStats.triangles}");
    GUILayout.Label($"Clouds: {cloudSystem.GetTotalCloudCount()}");
}
```

**Altitude indicator:**

UI Text в HUD, цвет зависит от corridor status:
- Зелёный: в safe zone
- Жёлтый: в warning margin
- Красный: в critical zone

---

## 8. ПОШАГОВЫЙ ПЛАН МИГРАЦИИ

### Фаза 0: Подготовка (1-2 часа)

| Шаг | Действие | Файл/Объект |
|-----|----------|------------|
| 0.1 | Создать Volume Profile | `Assets/_Project/Settings/WorldVolumeProfile.asset` |
| 0.2 | Обновить ProjectC_URP.asset | ShadowDistance=500, Cascades=4, DepthTexture=1 |
| 0.3 | Обновить Corridor_Global.asset | maxAltitude=9500 (95.0 units) |
| 0.4 | Создать IWorldDataProvider.cs | `Assets/_Project/Scripts/World/` |
| 0.5 | Добавить GPU Instancing в CloudGhibli.shader | `#pragma multi_compile_instancing` |

### Фаза 1: ScriptableObject ассеты (1.5 часа) — Сессия A1

| Шаг | Действие |
|-----|----------|
| 1.1 | Создать WorldData.asset |
| 1.2 | Создать 5 MountainMassif.asset |
| 1.3 | Создать 5 BiomeProfile.asset |
| 1.4 | Создать 28 PeakData.asset |
| 1.5 | Создать 15 RidgeData.asset |
| 1.6 | Создать 9 FarmData.asset |

### Фаза 2: Генерация мешей (2.5 часа) — Сессия A2+A3

| Шаг | Действие |
|-----|----------|
| 2.1 | Реализовать MountainMeshBuilder.cs (4 типа) |
| 2.2 | Настроить AnimationCurve heightProfile для 5 главных пиков |
| 2.3 | Реализовать RidgeMeshBuilder.cs (Catmull-Rom) |
| 2.4 | Тест: проверить меш Эвереста против реального профиля |

### Фаза 3: LOD и коллизии (1 час) — Сессия A4

| Шаг | Действие |
|-----|----------|
| 3.1 | Создать MountainLOD.cs (3 уровня) |
| 3.2 | Сгенерировать LOD1/LOD2 меши для каждого пика |
| 3.3 | Создать billboard меши для LOD2 |
| 3.4 | Добавить CapsuleCollider на каждый пик |
| 3.5 | Тест: LOD switching при облёте |

### Фаза 4: Фермы и города (2 часа) — Сессия A5+A7

| Шаг | Действие |
|-----|----------|
| 4.1 | Создать FarmPlatform.prefab (террасы, здания, ирригация) |
| 4.2 | Создать FerryStation.prefab |
| 4.3 | Создать CityModule префабы (Small, Medium, Platform, Bridge, Beacon, Dome) |
| 4.4 | Собрать City_Primus.prefab (Примум на Эвересте) |
| 4.5 | Разместить 9 ферм на хребтах |
| 4.6 | Разместить 5 городов на главных пиках |
| 4.7 | Добавить NetworkTransform на паромные вагонетки |

### Фаза 5: FixedWorldGenerator (1.5 часа) — Сессия A6

| Шаг | Действие |
|-----|----------|
| 5.1 | Создать FixedWorldGenerator.cs |
| 5.2 | Реализовать IWorldDataProvider интерфейс |
| 5.3 | Создать WorldBridge.cs адаптер |
| 5.4 | Порядок загрузки: Massifs -> Ridges -> Farms -> Cities -> Corridors |
| 5.5 | Async loading (coroutine-based) |
| 5.6 | Feature toggle: WorldGenerationMode.asset |
| 5.7 | Отключить WorldGenerator (не удалять) |
| 5.8 | Тест: переключение между генераторами |

### Фаза 6: Облака — VeilSystem (1.5 часа) — Сессия B1

| Шаг | Действие |
|-----|----------|
| 6.1 | Создать VeilSystem.cs |
| 6.2 | Плоскость Y=12.0, материал с noise |
| 6.3 | URP Exponential Fog (цвет #2d1b4e) |
| 6.4 | Particle System молний (фиолетовые) |
| 6.5 | Box Collider Y=14.0 — зона предупреждения |
| 6.6 | Интеграция с AltitudeCorridorSystem |
| 6.7 | Тест: спуск к завесе — предупреждение -> тряска -> урон |

### Фаза 7: Облака — Cumulonimbus (2 часа) — Сессия B2

| Шаг | Действие |
|-----|----------|
| 7.1 | Создать CumulonimbusCloud.cs |
| 7.2 | 3 секции: Base, Body, Anvil |
| 7.3 | CumulonimbusMaterial (вариация CloudGhibli) |
| 7.4 | Particle System молний внутри столба |
| 7.5 | Разместить 3-5 столбов в мире |
| 7.6 | Тест: столбы видны, молнии работают |

### Фаза 8: Облака — обновление слоёв (1 час) — Сессия B3

| Шаг | Действие |
|-----|----------|
| 8.1 | Обновить CloudLayerConfig ассеты (высоты, плотность, цвета) |
| 8.2 | Создать 5 climate материалов для облаков |
| 8.3 | CloudClimateTinter — tint по позиции |
| 8.4 | Density modifier вокруг ферм (радиус 50-80 units) |
| 8.5 | Тест: облака меняют цвет по массиву |

### Фаза 9: Интеграция и валидация (2.5 часа) — Сессия A8+B6

| Шаг | Действие |
|-----|----------|
| 9.1 | Полёт по всем 5 массивам — проверка координат |
| 9.2 | Проверка LOD на всех объектах |
| 9.3 | Проверка FPS при полном облёте |
| 9.4 | Спуск к завесе — все 5 уровней защиты |
| 9.5 | Проверка: игрок НЕ видит землю |
| 9.6 | Обновить GDD_02_World_Environment.md |
| 9.7 | Создать docs/world/PrototypeValidationReport.md |

### Итого: ~15.5 часов

---

## 9. ЧЕК-ЛИСТ ГОТОВНОСТИ URP

Перед началом сессий проверить:

- [ ] `m_VolumeProfile` назначен в ProjectC_URP.asset
- [ ] `m_RequireDepthTexture = 1`
- [ ] `m_ShadowDistance >= 500`
- [ ] `m_CascadeCount = 4`
- [ ] `m_SupportsLightLayers = 1`
- [ ] `m_MSAA = 2`
- [ ] CloudGhibli.shader имеет `#pragma multi_compile_instancing`
- [ ] Все материалы конвертированы в URP (m_LastMaterialVersion = 10)
- [ ] GraphicsSettings → Custom Render Pipeline → ProjectC_URP
- [ ] CloudGhibli.shader компилируется без ошибок
- [ ] WorldGenerationSettings.asset обновлён (или заменён WorldData)
- [ ] Corridor_Global.asset: maxAltitude = 9500

---

## 10. СВЯЗЬ С СУЩЕСТВУЮЩИМ КОДОМ

### 10.1. Зависимости от WorldGenerator.cs

| Файл | Зависимость | Решение |
|------|------------|---------|
| `WorldCamera.cs` | `FindAnyObjectByType<WorldGenerator>().GetAllPeaks()` | WorldBridge + IWorldDataProvider |
| `PeakNavigationUI.cs` | `FindAnyObjectByType<WorldGenerator>().GetAllPeaks()` | WorldBridge + IWorldDataProvider |
| `ShipController.cs` | НЕ зависит | Не требует изменений |
| `CloudSystem.cs` | НЕ зависит | Не требует изменений |
| `AltitudeCorridorSystem.cs` | НЕ зависит | Не требует изменений |

### 10.2. Совместимость с ShipController v2.7

ShipController использует AltitudeCorridorSystem для проверки высот. После обновления Global Corridor (max=95.0) ShipController будет корректно работать с городами на любой высоте.

### 10.3. Совместимость с CloudSystem

CloudSystem генерирует облака в Start(). FixedWorldGenerator НЕ должен дублировать эту логику. FixedWorldGenerator только конфигурирует CloudLayerConfig, CloudSystem генерирует.

**Порядок инициализации:**

```
1. FixedWorldGenerator.Start() — создаёт меши мира
2. CloudSystem.Start() — создаёт облака (уже работает)
3. AltitudeCorridorSystem.Start() — проверяет corridor (уже работает)
4. VeilSystem.Start() — создаёт завесу (новая сессия B1)
```

---

**Версия:** 1.0 | **13 апреля 2026 г.**
**Unity Specialist Implementation Guide — Project C: The Clouds**
