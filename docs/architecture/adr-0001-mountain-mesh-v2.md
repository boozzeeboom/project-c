# ADR-0001: Новая система генерации горных пиков (Mountain Mesh V2)

## Status
Proposed

## Date
13 апреля 2026

## Context

### Problem Statement
После 5 неудачных итераций (A3.1-A3.3, A4) текущая система генрации горных мешей полностью провалилась. Пики выглядят как "странные вытянутые пипки/капли", а НЕ как горы. Пользователю пришлось установить scale родителя Mountains = 60 чтобы получить хоть какой-то приемлемый визуальный размер, но формы всё равно ужасны.

Корневые проблемы:
1. **Неправильный масштаб**: baseRadius = 100-280 units, meshHeight = 150-540 units — горы в 60 раз меньше нужного
2. **Ужасные формы**: Cylinder/Ellipsoid/Dome с radiusFactor Lerp дают blob-подобные формы, НЕ горные
3. **CapsuleCollider = сферы**: Не соответствуют форме гор
4. **Смешение единиц**: X,Z в scaled units (3000-8700), Y в real meters/100 (88.48), baseRadius непонятно в чём
5. **4 типа форм неразличимы**: Tectonic/Volcanic/Dome/Isolated все выглядят одинаково плохо

### Constraints
- Unity 6, URP
- Runtime mesh generation из ScriptableObject данных
- 29 пиков в 5 массивах (Himalayan, Alpine, African, Andean, Alaskan)
- Должно работать на 60 FPS
- Нельзя использовать Terrain system (нужны кастомные меши)
- existing scripts НЕ modifying (создаём новые)

### Requirements
- Визуальный размер: 400-800 units высота, 200-500 units радиус
- Правильные горные формы: конусообразные с естественной вариацией
- Уникальные силуэты для 5 главных пиков
- MeshCollider вместо CapsuleCollider
- 3 LOD уровня
- Ghibli aesthetic: exaggerated proportions, soft contours

## Decision

### Новая математическая модель: Power-Law Cone Profile

Вместо Cylinder/Ellipsoid/Dome используем **степенную функцию** для профиля горы:

```
r(h) = R_base * (1 - h/H)^exponent
```

где:
- `r(h)` — радиус на высоте h
- `R_base` — радиус основания
- `H` — полная высота меша
- `exponent` — параметр крутизны профиля

**Профили по типам:**
- **Tectonic** (Everest, Mont Blanc): exponent = 1.4 — concave, крутые склоны, острая вершина
- **Volcanic** (Kilimanjaro): exponent = 0.65 — convex, пологие склоны, округлая вершина
- **Dome** (Foraker): exponent = 0.5 — очень convex, широкий купол
- **Isolated** (Denali): exponent = 1.1 — массивные плечи, одинокая громада

### Добавление Shoulder Bulge (плечи горы)

Реальные горы имеют утолщение на средней высоте — "плечи". Добавляем Gaussian bump:

```
shoulder(h) = A * exp(-((h - h_center)^2) / (2 * σ^2))
```

где:
- A = R_base * 0.15 (амплитуда 15% от радиуса)
- h_center = 0.35-0.5 (центр на 35-50% высоты)
- σ = 0.2 (ширина Gaussian)

Это даёт характерный "горный" вид — утолщение на средней высоте, сужение к вершине.

### Новая стратегия шума (3 слоя)

**1. Large-scale noise (силуэт):**
- FBM в полярных координатах (угол, высота)
- Амплитуда = R_base * 0.1
- Частота = 2-3 octaves
- Пик на mid-height, ноль на вершине и базе
- Создаёт ridges, spurs, general irregularity

**2. Small-scale noise (детали поверхности):**
- FBM в декартовых координатах (X, Z)
- Амплитуда = R_base * 0.03
- Частота = 6-8 octaves
- Добавляет scree, rock faces, micro-detail

**3. Ridge noise (только Tectonic):**
- Ridge noise: `1 - 2*|Perlin - 0.5|`
- Создаёт линейные ridge структуры
- Амплитуда = R_base * 0.08
- Характерно для тектонических гор (Everest ridges)

### Новая стратегия масштаба

**Отказ от формулы `meshHeight = baseRadius * hRatio`**

Вместо этого — **явные размеры для каждого пика** в PeakData:

| Пик | meshHeight | baseRadius | h/r | Тип |
|-----|-----------|-----------|-----|-----|
| Эверест | 750 | 420 | 1.79 | Tectonic |
| Лхоцзе | 620 | 350 | 1.77 | Tectonic |
| Монблан | 650 | 350 | 1.86 | Tectonic |
| Кибо | 550 | 400 | 1.38 | Volcanic |
| Аконкагуа | 720 | 420 | 1.71 | Tectonic |
| Денали | 680 | 380 | 1.79 | Isolated |

**Вычисление для остальных пиков:**
```
meshHeight = Mathf.Lerp(400, 800, normalizedImportance)
baseRadius = meshHeight / targetHRatio
```

где `normalizedImportance` зависит от role (MainCity = 1.0, Secondary = 0.7, etc.)

### Стратегия коллайдеров

**CapsuleCollider → MeshCollider (convex = false)**

- Используем **упрощённую версию меша** для коллайдера (16 segments, 8 rings вместо 64/24)
- `convex = false` — точное соответствие визуальной форме
- Performance: упрощённый меш = меньше треугольников = быстрее collision detection
- Для ship collision это приемлемо (не нужна pixel-perfect точность)

### Уникальные силуэты по типам

**Tectonic (Everest, Mont Blanc, Aconcagua):**
- exponent = 1.4 (concave profile)
- Ridge noise ON
- Sharp gribes, narrow ridges
- Asymmetry: 10-15% variation по углу

**Volcanic (Kilimanjaro):**
- exponent = 0.65 (convex profile)
- Crater depression на вершине
- Gentle slopes, rounded top
- Asymmetry: 5-8% (почти симметричный)

**Dome (Foraker, Shira):**
- exponent = 0.5 (очень convex)
- Broad, flat-ish top
- Gentle slopes everywhere
- Asymmetry: 5% (почти симметричный)

**Isolated (Denali):**
- exponent = 1.1 (slightly concave)
- Massive shoulders (большой shoulder bulge)
- Tall and imposing
- Asymmetry: 20% (заметно асимметричный)

### LOD стратегия

**LOD0 (0-500m):** 64 segments, 24 rings — ~10K triangles
**LOD1 (500-1500m):** 32 segments, 12 rings — ~2.5K triangles
**LOD2 (1500m+):** Billboard (quad с текстурой горы)

Переход между LOD: Unity LOD Group component

### Архитектура новой системы

```
┌─────────────────────────────────────────────────────────┐
│                    PeakData (SO)                        │
│  - worldPosition (X,Y,Z)                               │
│  - meshHeight (ЯВНОЕ значение, НЕ формула!)            │
│  - baseRadius (ЯВНОЕ значение)                          │
│  - shapeType (Tectonic/Volcanic/Dome/Isolated)         │
│  - mountainProfile (новый field: exponent, shoulder)   │
└─────────────────────────────────────────────────────────┘
                          ↓
┌─────────────────────────────────────────────────────────┐
│           MountainMeshGenerator (Runtime)               │
│  1. Power-law cone: r(h) = R_base * (1-h/H)^exponent   │
│  2. Shoulder bulge: Gaussian bump                        │
│  3. Large-scale noise (polar FBM)                       │
│  4. Small-scale noise (Cartesian FBM)                   │
│  5. Ridge noise (Tectonic only)                         │
│  6. Asymmetry deformation                               │
│  7. Recalculate normals                                 │
└─────────────────────────────────────────────────────────┘
                          ↓
┌─────────────────────────────────────────────────────────┐
│              MeshCollider (convex=false)                │
│  - Упрощённая версия меша (16 seg, 8 rings)            │
│  - Exact shape match                                    │
└─────────────────────────────────────────────────────────┘
```

### Файлы новой системы

**НОВЫЕ файлы (НЕ модифицируют старые):**
1. `MountainProfile.cs` — mathematical model (presets для каждого типа)
2. `MountainMeshGenerator.cs` — runtime mesh generation (V2)
3. `MountainMeshBuilderV2.cs` — runtime component (заменяет MountainMeshBuilder)
4. `MountainMassifBuilderV2.cs` — editor tool (заменяет MountainMassifBuilder)
5. `PeakDataScaler.cs` — editor utility для обновления PeakData dimensions

**МОДИФИЦИРУЕТ:**
1. `WorldDataTypes.cs` — добавить `meshHeight` field в PeakData (default 600f)

**НЕ ТРОГАТЬ:**
- `MountainMeshBuilder.cs` (старый, оставляем для fallback)
- `MountainMassifBuilder.cs` (старый, оставляем для fallback)
- `NoiseUtils.cs` (переиспользуем)
- `PeakDataFiller.cs` (переиспользуем, но обновляем baseRadius)
- `WorldData.cs`, `MountainMassif.cs` (structure OK)

## Alternatives Considered

### Alternative 1: Оставить scale 60 на родителе
- **Description**: Пользователь уже нашёл что scale=60 даёт правильный размер. Оставить его, только исправить формы мешей.
- **Pros**: Быстрое решение, минимум изменений
- **Cons**: Костыль, непрозрачная логика, проблемы с child transforms
- **Rejection Reason**: Пользователь хочет чистое решение, НЕ костыли

### Alternative 2: Unity Terrain system
- **Description**: Использовать встроенный Terrain вместо custom meshes
- **Pros**: Built-in LOD, painting tools, optimized
- **Cons**: НЕ поддерживает runtime creation из ScriptableObject, limited shape control, не подходит для isolated peaks
- **Rejection Reason**: Architectural mismatch — нам нужен runtime generation из данных

### Alternative 3: Pre-baked meshes (Editor-time)
- **Description**: Запекать меши в Editor, сохранять как .asset файлы
- **Pros**: Быстрее runtime, нет генерации в runtime
- **Cons**: Негибко, нельзя менять в runtime, большие файлы
- **Rejection Reason**: Нужна runtime flexibility для прототипа

## Consequences

### Positive
- ✅ Правильные горные формы (power-law cone + shoulder + noise)
- ✅ Визуально впечатляющие размеры (400-800 units tall)
- ✅ Уникальные силуэты для каждого типа
- ✅ MeshCollider соответствует форме горы
- ✅ Чистая архитектура (новые файлы, НЕ ломаем старые)
- ✅ Ghibli aesthetic (exaggerated proportions, soft contours)

### Negative
- ⚠️ Больше triangles (power-law cone + noise > simple cylinder)
- ⚠️ Сложнее математика (нужно правильно реализовать)
- ⚠️ Нужно обновить все 29 PeakData assets

### Риски
1. **Performance**: MeshCollider convex=false медленнее CapsuleCollider
   - **Mitigation**: Упрощённый меш для коллайдера (16 seg, 8 rings)
2. **Noise artefacts**: FBM может дать ugly artefacts если неправильно настроить
   - **Mitigation**: Тщательный tuning amplitude/frequency, clamping
3. **Scaling wrong**: Если meshHeight/baseRadius всё ещё неправильные
   - **Mitigation**: PeakDataScaler utility для явного задания размеров

## Performance Implications
- **CPU**: Mesh generation runtime ~5-10ms per peak (acceptable for 29 peaks at load)
- **Memory**: ~50-100KB per peak mesh (LOD0: 10K tris = 120KB vertices+indices)
- **Load Time**: ~200-300ms total for all 29 peaks (target < 2 sec)
- **Runtime**: No per-frame cost (static meshes, batched)

## Migration Plan

### Phase 1: Создание новой системы (текущая сессия)
1. Создать `MountainProfile.cs`
2. Создать `MountainMeshGenerator.cs`
3. Создать `MountainMeshBuilderV2.cs`
4. Создать `MountainMassifBuilderV2.cs`
5. Создать `PeakDataScaler.cs`

### Phase 2: Тестирование
1. Запустить PeakDataScaler: `Tools → Project C → Scale Peak Data (V2)`
2. Запустить MountainMassifBuilderV2: `Tools → Project C → Build All Mountain Meshes (V2)`
3. Проверить визуально в Unity:
   - Размеры пиков (400-800 units tall)
   - Формы (горные, НЕ пипки)
   - Коллайдеры (MeshCollider match visual)
   - Silhouettes (уникальные для каждого типа)

### Phase 3: Validation
1. Если формы правильные — удалить старые скрипты (MountainMeshBuilder, MountainMassifBuilder)
2. Если формы неправильные — tuning параметров (exponent, shoulder, noise)
3. Benchmark performance (target 60 FPS)

## Validation Criteria
- [ ] Все 29 пиков построены без ошибок
- [ ] Визуальный размер: 400-800 units tall (проверить в Scene view)
- [ ] Формы горные, НЕ "пипки" (пользователь подтвердит)
- [ ] h/r ratio = 1.5-2.0 для большинства пиков
- [ ] MeshCollider соответствует форме (проверить в Gizmos)
- [ ] FPS >= 60 в игре
- [ ] Пользователь доволен (самый важный критерий!)

## Related Decisions
- WorldLandscape_Design.md §2.1 (scale system)
- Landscape_TechnicalDesign.md §3 (mesh generation)
- SESSION_A4_CONTEXT.md (previous failed attempts analysis)
