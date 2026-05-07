# Cloud Generator v5.1 — Multi-Layer Architecture

## Концепция

Структура: **стек слоёв** снизу вверх. Каждый слой — независимый генератор со своим архетипом. Слои объединяются в итоговую форму облака.

```
┌─────────────────────────────┐
│     Layer 3: Platform       │  ← верх (anvil / перистые)
│     _____ flat spread _____  │
├─────────────────────────────┤
│     Layer 2: Column         │  ← середина (башня)
│         ║ ║ ║               │
├─────────────────────────────┤
│     Layer 1: Sphere/Platform │  ← низ (основание кучевого)
│       ◉ ◉ ◉ ◉ ◉ ◉ ◉         │
└─────────────────────────────┘
```

## Архитектура

```
generateCloud(layers[])
    │
    └─► for each layer (bottom→top):
            │
            ├─► stackLayers()       — вычисляет Y offset для каждого слоя
            │
            └─► archetype dispatcher:
                    ├── sphere    → generateSphereLayer()    ← текущий алгоритм, переупакован
                    ├── tree      → generateTreeLayer()     ← НОВЫЙ: направленное ветвление
                    ├── column    → generateColumnLayer()   ← НОВЫЙ: вертикальная стойка
                    └── platform  → generatePlatformLayer() ← НОВЫЙ: плоский диск
```

### Почему раздельные генераторы, а не один

| Фактор | Один параметризованный | Раздельные генераторы |
|--------|------------------------|----------------------|
| Cyclomatic complexity | if/else цепочка → 15+ веток | Каждый ≤ 10 |
| Расширяемость | Нужно менять существующий код | Новый файл + регистрация в map |
| Тестируемость | Сложно тестировать | Каждый генератор независим |
| C# parity | Сложный enum switch | Естественно → interface + implementations |

## Архетипы

### Sphere (существующий)

Дочерние элементы равномерно обрастают поверхность родителя во всех направлениях. Итоговая форма — сферический/блибовидный комок.

**Параметры:** все текущие (cloudSize, cascadeDepth, bumpsPerLevel, childRatio, sizeVariation, jitter, clustering, parentCount, ellipsoidY, ellipsoidXZ)

**Алгоритм:** текущий `generateCascadeCloud`, переупакованный в `generateSphereLayer()`

### Tree (НОВЫЙ)

Направленное ветвление — родитель порождает каскад узлов, удлиняющихся в одном направлении (как ветви). Ствол → ветки → подветки.

```
Root ──► Branch ──► SubBranch ──► ...
        │
        └─► Lateral ──► ...
```

**Параметры:**

| Параметр | Описание | Диапазон |
|----------|----------|----------|
| `branchElongation` | Во сколько раз child дальше от parent по направлению | 0.5–3.0 |
| `branchAngle` | Макс. угол отклонения ветки от родителя (градусы) | 15–60° |
| `maxDepth` | Глубина рекурсии ветвления | 2–8 |
| `branchProbability` | Шанс боковой ветки на узел | 0.2–0.8 |
| `trunkUpBias` | Насколько ствол стремится вверх (0=случайно, 1=всегда вверх) | 0.0–1.0 |
| `lengthFalloff` | Уменьшение длины на каждом уровне | 0.5–0.9 |
| `thicknessFalloff` | Уменьшение толщины (radius) на уровне | 0.4–0.8 |
| `leafDensity` | Плотность "листьев" — поверхностных шишек на ветках | 0.0–1.0 |

**Алгоритм:**
1. Seed направление (с учётом trunkUpBias)
2. Идти по направлению, создавая сферы уменьшающегося радиуса
3. На каждом узле с probability создать боковую ветку (угол через branchAngle)
4. Рекурсия с уменьшением depth
5. Опционально: добавить поверхностные шишки (leafDensity)

### Column (НОВЫЙ)

Вертикальная стойка — сферы располагаются на дискретных "этажах" по вертикали. Итоговая форма — цилиндр/башня (кумулонимбус).

```
Floor 5:    ○ ○ ○       ← узкий верх
Floor 4:   ○ ○ ○ ○ ○
Floor 3:  ○ ○ ○ ○ ○ ○ ○
Floor 2:  ○ ○ ○ ○ ○ ○ ○
Floor 1:   ○ ○ ○ ○ ○     ← широкий низ
```

**Параметры:**

| Параметр | Описание | Диапазон |
|----------|----------|----------|
| `height` | Общая высота колонны | 10–100 |
| `baseRadius` | Радиус нижнего этажа | 5–30 |
| `topRadius` | Радиус верхнего этажа (0 = игла) | 0–baseRadius |
| `floors` | Количество этажей | 3–20 |
| `ringsPerFloor` | Сфер на этаже | 3–12 |
| `wobble` | Горизонтальное смещение этажа | 0.0–1.0 |
| `taperCurve` | Как radius уменьшается с высотой: `linear` \| `exponential` \| `smooth` | — |

**Алгоритм:**
1. Для каждого floor: вычислить Y = floor * floorHeight
2. Interpolate radius между baseRadius и topRadius по taperCurve
3. Расположить ringsPerFloor сфер по кольцу на радиусе
4. Применить wobble (Perlin offset) для органичности
5. Опционально: ядро в центре каждого этажа

### Platform (НОВЫЙ)

Плоское распространение — сферы плотно заполняют XZ плоскость с минимальным Y. Итоговая форма — диск/блин.

```
    ○ ○ ○ ○ ○ ○ ○
  ○ ○ ○ ○ ○ ○ ○ ○ ○
○ ○ ○ ○ ○ ○ ○ ○ ○ ○ ○
○ ○ ○ ○ ○ ◉ ○ ○ ○ ○ ○   ← ядро толще
○ ○ ○ ○ ○ ○ ○ ○ ○ ○ ○
  ○ ○ ○ ○ ○ ○ ○ ○ ○
    ○ ○ ○ ○ ○ ○ ○
```

**Параметры:**

| Параметр | Описание | Диапазон |
|----------|----------|----------|
| `width` | Горизонтальный размер X | 20–200 |
| `depth` | Горизонтальный размер Z | 20–200 |
| `centerThickness` | Толщина в центре (Y) | 1–10 |
| `edgeThickness` | Толщина на краю | 0.1–2 |
| `falloffCurve` | Как плотность падает от центра: `gaussian` \| `linear` \| `step` | — |
| `interiorDensity` | Целевое количество сфер внутри | 20–100 |
| `edgeRings` | Количество граничных колец | 1–5 |

**Алгоритм:**
1. Anchor сфера в центре (0,0,0)
2. Fibonacci spiral в XZ для interior сфер
3. Y = noise * thickness (минимальный вертикальный разброс)
4. Radius уменьшается к краю (edge taper)
5. Edge rings — концентрические кольца по периметру

## Структура данных

### CloudSphere (существующая + расширенная)

```javascript
// Существующие поля (используются rebuildMesh):
{ x, y, z, radius, density }

// Новые поля (метаданные для генерации):
{ archetype, layerIndex, pathId? }
```

### LayerConfig

```javascript
{
  archetype: 'sphere' | 'tree' | 'column' | 'platform',
  enabled: true,

  // Вертикальное позиционирование
  yOffset: 0,              // смещение от предыдущего слоя (undefined = auto-stack)
  verticalDensity: 0.5,     // как плотно этажи прилегают

  //Population
  sphereCountRange: { min: 30, max: 60 },
  sizeRange: { min: 5, max: 25 },

  // Общие параметры генерации (работают для всех архетипов)
  seed: 42,
  density: 0.6,
  jitter: 0.3,
  clustering: 0.5,

  // Архетип-специфичные параметры (только один активен)
  sphereParams: { ... },      // archetype='sphere'
  treeParams: { ... },        // archetype='tree'
  columnParams: { ... },      // archetype='column'
  platformParams: { ... }     // archetype='platform'
}
```

## Вертикальный стек

### Auto-stack логика

```javascript
let cumulativeY = 0;
for (const layer of layers) {
  // Генерируем слой в локальных координатах (Y=0 — внизу)
  const rawSpheres = generator(layer);

  // Вычисляем bounding box
  const bounds = computeBounds(rawSpheres);
  const layerHeight = bounds.maxY - bounds.minY;

  // Если yOffset явно задан — используем его, иначе auto
  const y = layer.yOffset !== undefined
    ? layer.yOffset
    : cumulativeY - bounds.minY + gap;  // gap = 2 units

  // Применяем offset
  for (const s of rawSpheres) {
    s.y += y;
  }

  cumulativeY = y + bounds.maxY + gap;
  allSpheres.push(...rawSpheres);
}
```

### Ручной offset

Пользователь может задать `yOffset` явно для точного контроля:
- `yOffset = 0` — слой начинается сразу после предыдущего
- `yOffset = 50` — 50 единиц зазор
- Отрицательный — слои перекрываются

## UI для слоёв

### Концепт: Layer Stack Editor

```
┌─────────────────────────────────────────────────────────┐
│ LAYERS                                          [+ Add] │
├─────────────────────────────────────────────────────────┤
│ ≡  Layer 3: Platform ☑  [🗑]                           │
│    Offset: [  80]  Size: [20─60]  Archetype: [Platform▾]│
│    ┌─ Platform Params ──────────────────────────────┐   │
│    │  Width: [══○═══] 100   Depth: [══○═══] 100   │   │
│    │  Thickness: C:[══○══] 5  E:[○═══] 1          │   │
│    │  Falloff: ( )Linear (•)Gaussian ( )Step     │   │
│    └──────────────────────────────────────────────┘   │
├─────────────────────────────────────────────────────────┤
│ ≡  Layer 2: Column ☑  [🗑]                             │
│    Offset: [  60]  Size: [10─30]  Archetype: [Column ▾] │
│    ┌─ Column Params ───────────────────────────────┐   │
│    │  Height: [═══○══] 50   BaseR: [══○═══] 15   │   │
│    │  TopR: [○══════] 5   Floors: [═══○═] 8       │   │
│    │  Rings: [══○══] 5    Wobble: [○══════] 0.2   │   │
│    └──────────────────────────────────────────────┘   │
├─────────────────────────────────────────────────────────┤
│ ≡  Layer 1: Sphere ☑  [🗑]                              │
│    Offset: [   0]  Size: [15─45]  Archetype: [Sphere ▾] │
│    ┌─ Sphere Params ───────────────────────────────┐   │
│    │  Depth: [═══○═] 3   Bumps: [════○══] 48      │   │
│    │  Ratio: [══○══] 30  Variation: [══○══] 0.5  │   │
│    │  Parents: [1]   EllipsoidY: [══○══] 0.5      │   │
│    └──────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────┘
```

### Элементы UI

- **Reorderable list** — drag handle (≡) для изменения порядка слоёв
- **Archetype dropdown** — выбор архетипа per layer
- **Foldable params** — сворачиваемая секция с архетип-специфичными параметрами
- **Enabled checkbox** — временно отключить слой без удаления
- **Delete button** — удалить слой
- **Add Layer** — добавить новый слой (по умолчанию sphere)

## C# экспорт

### CloudArchetype enum

```csharp
public enum CloudArchetype
{
    Sphere,
    Tree,
    Column,
    Platform
}
```

### CloudLayerConfig

```csharp
[System.Serializable]
public class CloudLayerConfig
{
    public string LayerName = "Layer";
    public bool Enabled = true;
    public CloudArchetype Archetype = CloudArchetype.Sphere;

    public float YOffset = 0f;
    [Range(0f, 1f)] public float VerticalDensity = 0.5f;

    public Vector2Int SphereCountRange = new Vector2Int(30, 60);
    public Vector2 SizeRange = new Vector2(10f, 40f);

    public int Seed = 42;
    [Range(0f, 1f)] public float Density = 0.6f;
    [Range(0f, 1f)] public float Jitter = 0.3f;
    [Range(0f, 1f)] public float Clustering = 0.5f;

    // Archetype-specific params (only active one used)
    public SphereParams SphereParams = new SphereParams();
    public TreeParams TreeParams = new TreeParams();
    public ColumnParams ColumnParams = new ColumnParams();
    public PlatformParams PlatformParams = new PlatformParams();
}
```

### CloudGenerator API

```csharp
public static class CloudGenerator
{
    public static List<CloudSphere> Generate(List<CloudLayerConfig> layers);

    private static List<CloudSphere> GenerateSphereLayer(CloudLayerConfig layer, int layerIndex);
    private static List<CloudSphere> GenerateTreeLayer(CloudLayerConfig layer, int layerIndex);
    private static List<CloudSphere> GenerateColumnLayer(CloudLayerConfig layer, int layerIndex);
    private static List<CloudSphere> GeneratePlatformLayer(CloudLayerConfig layer, int layerIndex);
}
```

## Roadmap реализации

| Шаг | Что | Статус |
|-----|-----|--------|
| 1 | Extract `generateCascadeCloud` → `generateSphereLayer()` + dispatcher infrastructure | ✅ Готово (v5.0) |
| 2 | Добавить `resolveLayers()` с backward-compat для single-layer | ✅ Готово (v5.0) |
| 3 | Реализовать `generateColumnLayer()` | ✅ Готово (v5.0) |
| 4 | Реализовать `generatePlatformLayer()` | ✅ Готово (v5.0) |
| 5 | Реализовать `generateTreeLayer()` | ✅ Готово (v5.0) |
| 6 | Добавить UI layer editor | ✅ Готово (v5.1) |
| 7 | C# parity — CloudArchetype enum + CloudGenerator refactor | ✅ Готово (v5.1) |

## Known Issues / TODO

- [ ] Flat bottom profile (condensation level) — пока не реализовано
- [ ] Storm mode (cumulonimbus: column + anvil) — archetype stacking решает
- [ ] Wind animation (curl noise для анимации) — для будущего
- [ ] Layer preview in Scene View — требует Unity-side реализации
- [ ] Reorder layers (drag & drop) — для управления порядком слоёв
- [ ] Clone/copy layer — для быстрого создания вариаций
- [ ] Undo/redo для изменений параметров
