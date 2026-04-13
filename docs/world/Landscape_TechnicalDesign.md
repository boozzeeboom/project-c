# Landscape Technical Design — Project C: The Clouds

**Версия:** 1.0 | **Дата:** 13 апреля 2026 г. | **Статус:** Утверждён Technical Director
**Автор:** Technical Director
**Назначение:** Техническая архитектура системы фиксированного ландшафта, заменяющая процедурную генерацию

---

## 0. Решение Technical Director

### 0.1. Ключевые архитектурные решения

| Решение | Выбор | Обоснование |
|---------|-------|-------------|
| **Хранение данных горных массивов** | ScriptableObject + JSON fallback | ScriptableObject — нативная интеграция с Unity Inspector, префабрикация, addressables-ready. JSON — для динамической подгрузки сервером (серверная валидация координат) |
| **Модель мешей гор** | Runtime-генерация из Heightmap + ручные контрольные точки | Чистые процедурные меша не дают узнаваемых форм. Чистые ручные меша —太重 для 28 пиков. Гибрид: heightmap-основа + keypoint-деформация по реальным профилям |
| **LOD-система** | Unity LOD Group + 3 уровня на пик | 28 пиков x LOD0 = неприемлемо. LOD Group — стандарт Unity, интегрируется с occlusion culling |
| **Масштаб координат** | 1 unit = 1 метр (без изменений) | Текущая кодовая база уже использует эту систему. AltitudeCorridorSystem настроен на метры. Изменение масштаба = переписывание всей системы кораблей |
| **Фермерские платформы** | Отдельные Prefab-объекты, не часть горного меша | Террасы — игровые объекты с коллайдерами, триггерами, NPC. Должны быть независимы от ландшафта |
| **Завеса** | Огромная плоскость + Volume Fog + Particle System | Текущая CloudSystem уже содержит 3 слоя. Завеса — отдельный компонент, не мешается с обычными облаками |

---

## 1. Архитектура фиксированного мира

### 1.1. Общая схема

```
┌─────────────────────────────────────────────────────────────┐
│                    FixedWorldGenerator                       │
│  (заменяет WorldGenerator)                                   │
│                                                              │
│  Start() → LoadWorldData() → BuildMountainMassifs()          │
│         → BuildRidges() → BuildFarms() → BuildVeil()         │
│         → LinkAltitudeCorridors()                            │
└──────────────────────┬──────────────────────────────────────┘
                       │
        ┌──────────────┼──────────────┐
        ▼              ▼              ▼
┌──────────────┐ ┌─────────────┐ ┌──────────────┐
│ WorldData    │ │ Massif      │ │ Corridor      │
│ (Scriptable- │ │ Builder     │ │ Linker        │
│  Object)     │ │             │ │               │
└──────┬───────┘ └──────┬──────┘ └───────┬──────┘
       │                │                 │
       ▼                ▼                 ▼
┌──────────────┐ ┌─────────────┐ ┌──────────────┐
│ MountainMassif││ PeakData    │ │ Altitude      │
│ (Scriptable- │ │ (keypoints, │ │ Corridor      │
│  Object)     │ │ heightmap)  │ │ System        │
└──────────────┘ └─────────────┘ └──────────────┘
```

### 1.2. Структура данных: WorldData (ScriptableObject)

**Путь:** `Assets/_Project/Data/WorldData.asset`

```csharp
[CreateAssetMenu(menuName = "Project C/World Data", fileName = "WorldData")]
public class WorldData : ScriptableObject
{
    [Header("Масштаб")]
    public float heightScale = 0.01f;      // реальная_высота / 100
    public float distanceScale = 0.0005f;  // реальное_расстояние / 2000

    [Header("Границы мира")]
    public float worldMinX = -5500f;
    public float worldMaxX = 2500f;
    public float worldMinZ = -3500f;
    public float worldMaxZ = 5500f;

    [Header("Массивы")]
    public List<MountainMassif> massifs;

    [Header("Завеса")]
    public float veilHeight = 12.0f;
    public Color veilColor = new Color(0.176f, 0.106f, 0.306f, 1f); // #2d1b4e
    public float veilFogDensity = 0.003f;

    [Header("Облака")]
    public CloudLayerConfig upperLayerConfig;
    public CloudLayerConfig middleLayerConfig;
    public CloudLayerConfig lowerLayerConfig;
}
```

### 1.3. Структура данных: MountainMassif (ScriptableObject)

**Путь:** `Assets/_Project/Data/Massifs/` — по одному ассету на массив

```
Massifs/
├── HimalayanMassif.asset
├── AlpineMassif.asset
├── AfricanMassif.asset
├── AndeanMassif.asset
└── AlaskanMassif.asset
```

```csharp
[CreateAssetMenu(menuName = "Project C/Mountain Massif", fileName = "MountainMassif")]
public class MountainMassif : ScriptableObject
{
    [Header("Идентификация")]
    public string massifId;           // "himalayan", "alpine", ...
    public string displayName;        // "Гималайский массив"

    [Header("Центр массива")]
    public Vector3 centerPosition;    // координаты главного города
    public float massifRadius;        // радиус влияния (units)

    [Header("Климатический профиль")]
    public BiomeProfile biomeProfile; // ScriptableObject с цветами/атмосферой

    [Header("Пики")]
    public List<PeakData> peaks;

    [Header("Хребты")]
    public List<RidgeData> ridges;

    [Header("Фермерские угодья")]
    public List<FarmData> farms;

    [Header("Городской коридор")]
    public AltitudeCorridorData cityCorridor; // связь с системой высот
}
```

### 1.4. Структура данных: PeakData (inline в MountainMassif)

```csharp
[System.Serializable]
public class PeakData
{
    [Header("Идентификация")]
    public string peakId;             // "everest", "lhoteze", ...
    public string displayName;        // "Эверест"
    public PeakRole role;             // MainCity, Secondary, Farm, Military, Abandoned

    [Header("Позиция")]
    public Vector3 worldPosition;     // X, Y (scaled), Z в мировых координатах
    public float realHeightMeters;    // реальная высота в метрах (для HUD)

    [Header("Форма меша")]
    public PeakShapeType shapeType;   //尖锐(tectonic), volcanic, dome, isolated
    public float baseRadius;          // радиус основания (units)
    public AnimationCurve heightProfile; // профиль высоты от основания к вершине

    [Header("Heightmap keypoints")]
    public List<HeightmapKeypoint> keypoints; // для формы меша

    [Header("Визуальные")]
    public Color rockColor;           // цвет скал (переопределяет биом)
    public float snowLineY;           // высота снеговой линии
    public bool hasSnowCap;           // снежная шапка
    public bool hasCrater;            // вулканический кратер
}

public enum PeakRole
{
    MainCity,        // Главный город массива (Примум, Секунд...)
    Secondary,       // Вторичный пик с ролью (военная вышка, тюрьма...)
    Farm,            // Фермерский пик
    Military,        // Военный пост
    Abandoned,       // Заброшенная платформа
    Navigation       // Навигационный ориентир
}

public enum PeakShapeType
{
    Tectonic,        // Острые, тектонические (Гималаи, Альпы, Анды)
    Volcanic,        // Округлые, вулканические (Килиманджаро)
    Dome,            // Куполообразные (Форакер)
    Isolated         // Одиночные громады (Денали)
}
```

### 1.5. Структура данных: HeightmapKeypoint

```csharp
[System.Serializable]
public class HeightmapKeypoint
{
    public float normalizedRadius;  // 0..1 от центра к краю
    public float normalizedHeight;  // 0..1 от основания к вершине
    public float noiseWeight;       // вес шума (0 = гладкий, 1 = максимальный шум)
}
```

**Пример для Эвереста (упрощённо):**

| Norm. Radius | Norm. Height | Noise Weight | Описание |
|-------------|-------------|--------------|----------|
| 0.00 | 1.00 | 0.1 | Вершина — гладкая |
| 0.10 | 0.95 | 0.2 | Подвершинная зона |
| 0.25 | 0.80 | 0.4 | Верхние склоны |
| 0.40 | 0.60 | 0.6 | Средние склоны — выраженный шум |
| 0.60 | 0.35 | 0.8 | Нижние склоны — максимальная неровность |
| 0.80 | 0.15 | 0.5 | Предгорья |
| 1.00 | 0.00 | 0.2 | Основание — плавный спад |

### 1.6. Структура данных: RidgeData (хребты)

```csharp
[System.Serializable]
public class RidgeData
{
    public string ridgeId;            // "himalayan_south", ...
    public string displayName;

    [Header("Связанные пики")]
    public string[] peakIds;          // IDs пиков, соединённых хребтом (минимум 2)

    [Header("Параметры хребта")]
    public float ridgeHeight;         // средняя высота гребня (units)
    public float ridgeWidth;          // ширина хребта (units)
    public float saddleDrop;          // насколько седловины ниже пиков (units)

    [Header("Фермы на хребте")]
    public string[] farmIds;          // IDs ферм, расположенных на этом хребте
}
```

### 1.7. Структура данных: FarmData

```csharp
[System.Serializable]
public class FarmData
{
    [Header("Идентификация")]
    public string farmId;             // "everest_010", "mb_s010", ...
    public string displayName;
    public int farmNumber;            // номер по системе лора (10, 20, 110...)

    [Header("Позиция")]
    public Vector3 worldPosition;     // X, Y, Z
    public string parentRidgeId;      // ID хребта, на котором расположена

    [Header="Платформа")]
    public float platformSizeX = 40f; // размер антигравийной плиты
    public float platformSizeZ = 20f;
    public int terraceCount = 3;      // количество террас
    public float terraceSpacing = 5f; // расстояние между террасами (units)

    [Header("Геймплей")]
    public string productionType;     // "latex", "grain", "vegetables", ...
    public bool hasGreenhouse;        // есть ли теплицы
    public bool hasDockingPad;        // есть ли посадочная площадка
    public bool hasFerryStation;      // есть ли паромная станция (подвесной трос)

    [Header("Визуальные")]
    public Color platformGlowColor = new Color(0.31f, 0.76f, 0.97f); // #4fc3f7
}
```

### 1.8. Система масштабирования

**Принцип:** Координаты в ScriptableObject — уже масштабированные. Конвертация происходит один раз при создании ассетов.

| Реальное значение | Масштаб | Игровое значение |
|------------------|---------|-----------------|
| Эверест 8848 м | /100 | Y = 88.48 units |
| Монблан 4808 м | /100 | Y = 48.08 units |
| Расстояние 6200 км | /2000 | 3100 units |
| Основание горы ~15 км | /2000 | 7.5 units → **НЕТ** |

**Важное решение по масштабу основания гор:**

Реальные горы имеют основание 10-30 км в диаметре. При масштабе 1:2000 это 5-15 units — слишком мало для играбельности.

**Решение:** Масштабировать ТОЛЬКО высоты (/100), а основания — **игровой масштаб**:

| Пик | Реальная высота | Игровая Y | Игровой радиус основания | Обоснование |
|-----|----------------|-----------|------------------------|-------------|
| Эверест | 8848 м | 88.48 | 600 units | Достаточно для облёта |
| Монблан | 4808 м | 48.08 | 350 units | Компактный, крутой |
| Килиманджаро | 5895 м | 58.95 | 400 units | Вулканический, широкий |
| Аконкагуа | 6962 м | 69.62 | 500 units | Вытянутый вдоль Анд |
| Денали | 6190 м | 61.90 | 450 units | Одиночная громада |
| Вторичные пики | 3000-5000 м | 30-50 | 100-250 units | Меньше главных |
| Фермерские пики | 2000-3000 м | 20-30 | 50-100 units | Минимальные |

**Соотношение высоты к радиусу (игровое):**

| Пик | Высота | Радиус | Соотношение | Реальный прототип |
|-----|--------|--------|------------|------------------|
| Эверест | 88.48 | 600 | 0.147 | Массивный, широкий |
| Монблан | 48.08 | 350 | 0.137 | Крутой, компактный |
| Килиманджаро | 58.95 | 400 | 0.147 | Широкий вулкан |
| Аконкагуа | 69.62 | 500 | 0.139 | Длинный хребет |
| Денали | 61.90 | 450 | 0.138 | Одиночный |

---

## 2. Генерация горных мешей

### 2.1. Архитектура: MountainMeshBuilder

**Путь:** `Assets/_Project/Scripts/World/MountainMeshBuilder.cs`

**Принцип работы:**

```
Input: PeakData (position, shapeType, heightProfile, keypoints, baseRadius)
  │
  ├─► GenerateBaseMesh(shapeType, baseRadius, height)
  │     │
  │     ├─ Tectonic:  конус с острыми гранями + ridge noise
  │     ├─ Volcanic:  округлый конус + crater depression
  │     ├─ Dome:     пологий купол
  │     └─ Isolated:  широкий конус с крутой вершиной
  │
  ├─► ApplyHeightProfile(heightProfile)
  │     → корректирует Y каждой вершины по AnimationCurve
  │
  ├─► ApplyKeypointDeformation(keypoints)
  │     → локальные возмущения: выступы, впадины, седловины
  │
  ├─► ApplyRidgeConnection(ridgeData)
  │     → соединяет с соседними пиками через хребты
  │
  ├─► ApplyNoiseDisplacement(noiseWeight)
  │     → FBM noise для естественной неровности
  │
  └─► Output: Mesh (vertices, triangles, normals, UVs)
```

### 2.2. Базовые формы по типам

#### 2.2.1. Tectonic (Эверест, Монблан, Анды, Альпы)

**Алгоритм:**
- Цилиндрическая сетка с 64 сегментами по окружности, 16 кольцами по высоте
- Профиль: крутые склоны (угол 60-75 градусов), острая вершина
- Noise: high-frequency FBM (frequency=8, octaves=6, amplitude=5% от радиуса)
- Ridge noise: добавляем "линейные" выступы, имитирующие тектонические складки

**Полигоны (LOD0):** ~2048 треугольников на пик

#### 2.2.2. Volcanic (Килиманджаро)

**Алгоритм:**
- Сфера, вытянутая по Y (scale Y = 1.5)
- Кратер: depression в вершине (радиус 10% от baseRadius, глубина 5% от height)
- Профиль: пологие склоны (угол 30-45 градусов), округлая вершина
- Noise: low-frequency FBM (frequency=4, octaves=4, amplitude=3%)

**Полигоны (LOD0):** ~1536 треугольников

#### 2.2.3. Dome (Форакер, вторичные пики)

**Алгоритм:**
- Полусфера с основанием
- Профиль: очень пологий (угол 20-35 градусов)
- Noise: minimal (frequency=3, octaves=3, amplitude=2%)

**Полигоны (LOD0):** ~1024 треугольника

#### 2.2.4. Isolated (Денали)

**Алгоритм:**
- Конус с широким основанием и крутой вершиной
- Профиль: экспоненциальный — широкий у основания, резко к вершине
- Noise: medium-frequency FBM (frequency=6, octaves=5, amplitude=4%)
- Боковые выступы: 2-3 локальных возвышения (имитация боковых хребтов)

**Полигоны (LOD0):** ~2048 треугольников

### 2.3. Система LOD

**Компонент:** `MountainLOD.cs` (вешается на каждый пик)

| LOD | Дистанция от камеры | Детализация | Треугольники | Когда используется |
|-----|-------------------|-------------|-------------|-------------------|
| **LOD0** | 0-500 units | Full mesh + noise + ridges | 1500-2048 | Игрок рядом, видна детализация |
| **LOD1** | 500-2000 units | Simplified mesh (32 сегмента, 8 колец) | 512-768 | Средняя дистанция |
| **LOD2** | 2000+ units | Billboard / impostor | 2 (quad) | Далеко, только силуэт |

**Реализация:**

```csharp
[RequireComponent(typeof(LODGroup))]
public class MountainLOD : MonoBehaviour
{
    public LOD[] lodLevels;

    void SetupLOD(Mesh lod0Mesh, Mesh lod1Mesh, Material sharedMat)
    {
        LODGroup lodGroup = GetComponent<LODGroup>();

        // LOD0: полный меш
        LOD lod0 = new LOD(0.5f, new Renderer[] { CreateRenderer(lod0Mesh, sharedMat) });
        // LOD1: упрощённый меш
        LOD lod1 = new LOD(0.25f, new Renderer[] { CreateRenderer(lod1Mesh, sharedMat) });
        // LOD2: биллборд
        LOD lod2 = new LOD(0.1f, new Renderer[] { CreateBillboardRenderer(sharedMat) });

        lodGroup.SetLODs(new LOD[] { lod0, lod1, lod2 });
        lodGroup.RecalculateBounds();
    }
}
```

**Billboard для LOD2:**
- Camera-facing quad (2 треугольника)
- Текстура — pre-rendered sprite пика с альфа-каналом
- Генерируется один раз при старте: RenderCamera → RenderTexture → Texture2D → Sprite

### 2.4. Хребты (Ridge Lines)

**Архитектура:** `RidgeMeshBuilder.cs`

**Принцип:** Хребет — это "мост" между двумя пиками, созданный как вытянутый меш с седловинами.

```
Пик A (Y=88.48)                        Пик B (Y=72.00)
        *                                    *
         \                                  /
          \    ridge line (Y ~55-65)       /
           *-----*-----*-----*-----*-----*
          /      \     \     \     \      \
         /   saddle   \     \     \       \
        /    (Y~45)    \     \     \       \
       ───────────────────────────────────────
```

**Алгоритм генерации хребта:**

1. **Path:** Catmull-Rom spline между пиками A и B через 3-5 контрольных точек
2. **Cross-section:** V-образный профиль (широкий сверху, узкий снизу)
3. **Saddle points:** На 30-50% высоты ниже соединяемых пиков
4. **Ширина:** 20-40 units (достаточно для фермерских террас)
5. **Noise:** low-frequency FBM (frequency=3, octaves=3) для естественности
6. **Соединение с пиками:** Вершины хребта сливаются с вершинами пиков (shared vertices)

**Полигоны на хребет:** ~512-1024 треугольника (зависит от длины)

**Количество хребтов:** ~15 на весь мир (см. раздел 3.2 каждого массива в WorldLandscape_Design.md)

### 2.5. Узнаваемые формы главных пиков

Для 5 главных пиков используем **ручные heightmap-профили**, основанные на реальных топографических данных.

#### 2.5.1. Эверест

**Реальный профиль (южный маршрут):**

| Расстояние от вершины (км) | Высота (м) | Игровой Y | Игровой радиус |
|---------------------------|-----------|-----------|---------------|
| 0 (вершина) | 8848 | 88.48 | 0 |
| 1 | 8200 | 82.0 | 30 |
| 2 | 7500 | 75.0 | 60 |
| 3 | 6800 | 68.0 | 90 |
| 5 | 5500 | 55.0 | 150 |
| 7 | 4200 | 42.0 | 220 |
| 10 | 3000 | 30.0 | 350 |
| 15 | 1500 | 15.0 | 500 |
| 20 (основание) | 500 | 5.0 | 600 |

**Особенности:**
- Южная стена круче северной (асимметрия)
- Характерный "женский профиль" — двугорбая форма (Эверест + Лхоцзе видны как единый массив)
- Снеговая линия ~55 units

#### 2.5.2. Монблан

| Расстояние (км) | Высота (м) | Игровой Y | Игровой радиус |
|----------------|-----------|-----------|---------------|
| 0 (вершина) | 4808 | 48.08 | 0 |
| 0.5 | 4400 | 44.0 | 15 |
| 1 | 3800 | 38.0 | 35 |
| 2 | 2800 | 28.0 | 80 |
| 3 | 1800 | 18.0 | 150 |
| 5 (основание) | 800 | 8.0 | 350 |

**Особенности:**
- Острая вершина, крутые склоны (альпийский тип)
- Асимметричный: южная сторона круче
- Характерный "зубчатый" силуэт

#### 2.5.3. Килиманджаро (Кибо)

| Расстояние (км) | Высота (м) | Игровой Y | Игровой радиус |
|----------------|-----------|-----------|---------------|
| 0 (вершина) | 5895 | 58.95 | 0 |
| 1 | 5500 | 55.0 | 25 |
| 2 | 4800 | 48.0 | 50 |
| 3 | 4000 | 40.0 | 80 |
| 5 | 2500 | 25.0 | 140 |
| 8 (основание) | 1000 | 10.0 | 200 |

**Особенности:**
- Округлый вулканический конус
- Кратер на вершине (диаметр ~2 units в игре)
- Более пологие склоны, чем у тектонических гор

#### 2.5.4. Аконкагуа

| Расстояние (км) | Высота (м) | Игровой Y | Игровой радиус |
|----------------|-----------|-----------|---------------|
| 0 (вершина) | 6962 | 69.62 | 0 |
| 1 | 6200 | 62.0 | 30 |
| 2 | 5200 | 52.0 | 60 |
| 4 | 3800 | 38.0 | 130 |
| 6 | 2500 | 25.0 | 220 |
| 10 (основание) | 1200 | 12.0 | 500 |

**Особенности:**
- Вытянут с юга на север (андийский тип)
- Западная стена круче восточной
- Сухой, мало снега (коричневые тона)

#### 2.5.5. Денали (Мак-Кинли)

| Расстояние (км) | Высота (м) | Игровой Y | Игровой радиус |
|----------------|-----------|-----------|---------------|
| 0 (вершина) | 6190 | 61.90 | 0 |
| 1 | 5600 | 56.0 | 35 |
| 2 | 4800 | 48.0 | 70 |
| 4 | 3500 | 35.0 | 150 |
| 7 | 2000 | 20.0 | 280 |
| 12 (основание) | 600 | 6.0 | 450 |

**Особенности:**
- Одиночная громада с широким основанием
- Наибольшее вертикальное превышение над основанием (от 600м до 6190м = 5590м)
- Массивный, доминирующий силуэт

---

## 3. Система фермерских угодий

### 3.1. Архитектура: FarmPlatform

**Путь:** `Assets/_Project/Prefabs/Farms/FarmPlatform.prefab`

**Структура префаба:**

```
FarmPlatform (root)
├── AntiGravPlatform (MeshRenderer + MeshCollider)
│   ├── EmissiveEdge (Child with emissive material)
│   └── LandingPad (Child, trigger zone)
├── Terraces (Empty GameObject)
│   ├── Terrace_01 (Mesh + Collider)
│   ├── Terrace_02 (Mesh + Collider)
│   └── Terrace_03 (Mesh + Collider)
├── Buildings (Empty GameObject)
│   ├── ResidentialModule x2
│   ├── Greenhouse (if hasGreenhouse)
│   └── IrrigationSystem
├── DockingBay (if hasDockingPad)
│   ├── LandingPad (trigger)
│   └── Beacon (Point Light + Particle System)
└── FerryStation (if hasFerryStation)
    ├── CableAnchor (mesh)
    ├── CableLine (LineRenderer)
    └── CableCar (animated prefab)
```

### 3.2. Антигравийные платформы

**Параметры:**

| Параметр | Значение | Обоснование |
|----------|---------|-------------|
| Размер плиты | 40 x 20 units | Достаточно для 3 террас + здания |
| Толщина | 2 units | Визуально заметна, не перегружает |
| Emissive edge | #4fc3f7, intensity 0.5 | Свечение антигравийного поля |
| Collider | BoxCollider (trigger для зоны посадки) | Физика для кораблей |
| Позиция Y | На 2-5 units ниже хребта | "Прикреплена" к склону хребта |

### 3.3. Террасы

**Генерация:** `TerraceBuilder.cs`

```
Для каждой террасы (i от 0 до terraceCount-1):
  Y = platformY - (i * terraceSpacing)
  XZ = проекция на склон хребта
  Размер: уменьшается с каждой террасой (40→30→20 units)
  Форма: следующая за рельефом хребта (raycast к ridge mesh)
  Материал: зелёный #5d8a4a с текстурой почвы
```

**Полигоны на террасу:** ~128 треугольников (простой mesh)

### 3.4. Паромные станции (подвесные тросы)

**Архитектура:** `FerryCableSystem.cs`

**Принцип:** Визуальная + геймплейная связь между двумя фермами/поселениями.

```
Ферма A ─────────────────────────────── Ферма B
           \                        /
            \  catenary curve      /
             \                    /
              ─── CableCar ──────
```

**Параметры троса:**

| Параметр | Значение |
|----------|---------|
| Максимальная длина | 1500 units |
| Провисание (catenary) | 5-10% от длины |
| Толщина LineRenderer | 0.3 units |
| Скорость вагонетки | 50 units/sec |
| Collider | CapsuleCollider вдоль троса |

**Реализация:**

```csharp
public class FerryCableSystem : MonoBehaviour
{
    public FarmData stationA;
    public FarmData stationB;

    private LineRenderer cableLine;
    private GameObject cableCar;

    void GenerateCable()
    {
        // Catenary curve: y = a * cosh(x/a)
        int segments = 32;
        Vector3[] points = new Vector3[segments];

        for (int i = 0; i < segments; i++)
        {
            float t = i / (float)(segments - 1);
            points[i] = CalculateCatenaryPoint(
                stationA.worldPosition,
                stationB.worldPosition,
                t,
                sagAmount: 20f
            );
        }

        cableLine.positionCount = segments;
        cableLine.SetPositions(points);
    }
}
```

### 3.5. Координаты фермерских угодий для прототипа

Данные из WorldLandscape_Design.md, секция 4.3:

| Угодье | Массив | X | Y | Z | Высота от Завесы |
|--------|--------|------|------|------|-----------------|
| Эверест 010 | Гималаи | +300 | 42.00 | -800 | +30.00 |
| Эверест 020 | Гималаи | +600 | 35.00 | -1200 | +23.00 |
| Эверест 110 | Гималаи | +900 | 30.00 | -1000 | +18.00 |
| Монблан С010 | Альпы | -1,800 | 32.00 | +2,600 | +20.00 |
| Кибо К010 | Африка | -2,300 | 38.00 | -3,100 | +26.00 |
| Аконкагуа Т010 | Анды | -4,500 | 45.00 | -1,500 | +33.00 |
| Аконкагуа Т020 | Анды | -4,800 | 38.00 | -1,000 | +26.00 |
| Денали Кв010 | Аляска | +800 | 35.00 | +5,000 | +23.00 |
| Дон 1010 | Гималаи | +1,200 | 25.00 | -600 | +13.00 |

---

## 4. Связь с системой облаков

### 4.1. Привязка завесы к фиксированной высоте

**Текущее:** CloudLayerConfig.lowerLayerConfig.minHeight/maxHeight определяют нижний слой облаков.

**Новое:** Завеса — отдельный компонент, не зависит от CloudLayerConfig.

```
┌──────────────────────────────────────────────────┐
│            VeilSystem (новый компонент)            │
│                                                    │
│  VeilPlane: Y = 12.0 (фиксировано)                 │
│  VeilTrigger: Y = 14.0 (зона предупреждения)       │
│  VeilFog: URP Volume, exponential fog             │
│  VeilLightning: Particle System                    │
│                                                    │
│  Интеграция:                                       │
│  - При Y < 14.0 → жёлтый предупреждение            │
│  - При Y < 12.0 → красный вход в Завесу            │
│  - При Y < 8.0 → урон, отключение систем           │
│  - Нижние облака получают фиолетовый оттенок        │
└──────────────────────────────────────────────────┘
```

### 4.2. Привязка Altitude Corridor к реальным высотам городов

**Текущий AltitudeCorridorSystem** уже использует ScriptableObject AltitudeCorridorData с minAltitude/maxAltitude.

**Изменение:** Обновить значения коридоров по данным из WorldLandscape_Design.md:

| Город | Y города | Min коридора | Max коридора | Ширина | Статус |
|-------|---------|-------------|-------------|--------|--------|
| Примум | 88.48 | 41.00 | 95.00 | 54.00 | Обновить |
| Секунд | 48.08 | 30.00 | 55.00 | 25.00 | Обновить |
| Кибо | 58.95 | 40.00 | 65.00 | 25.00 | Обновить |
| Тертиус | 69.62 | 45.00 | 80.00 | 35.00 | Обновить |
| Квартус | 61.90 | 40.00 | 70.00 | 30.00 | Обновить |
| Глобальный | — | 12.00 | 44.50 | 32.50 | Обновить |

**Связь с завесой:** Global minAltitude = 12.0 = высота завесы. Это автоматически создаёт warning при приближении к завесе.

### 4.3. Гарантия: игрок НЕ видит землю под облаками

**Проблема:** При полёте на нижних высотах игрок может увидеть "землю" под облачным слоем.

**Решение — многоуровневая защита:**

| Уровень | Механизм | Описание |
|---------|---------|----------|
| **1. Завеса** | Непрозрачная плоскость Y=12 | Физически закрывает всё ниже |
| **2. Нижний слой облаков** | Плотные облака Y=15-40 | Визуальная преграда до завесы |
| **3. Exponential Fog** | URP Volume, плотность 0.002 | Дальность видимости ~500 units |
| **4. Height clamp** | Camera far clip plane | Ниже Y=10 — рендеринг отключён |
| **5. Gameplay boundary** | Trigger at Y=14 | Предупреждение + автоматический возврат |

**Реализация (Level 4):**

```csharp
// В CameraController:
if (cameraTransform.position.y < 10f)
{
    // Отключаем рендеринг всего ниже Y=0
    cameraComponent.cullingMask &= ~(1 << LayerMask.NameToLayer("Ground"));
}
```

**Реализация (Level 5):**

```csharp
// В VeilSystem:
private void OnTriggerEnter(Collider other)
{
    if (other.CompareTag("Player"))
    {
        StartCoroutine(ForcePullUp());
    }
}

private IEnumerator ForcePullUp()
{
    // Мягкое принуждение: автоматическое поднятие на 3 секунды
    float timer = 0f;
    while (timer < 3f)
    {
        shipController.ApplyUpwardForce(pullUpStrength * Time.deltaTime);
        timer += Time.deltaTime;
        yield return null;
    }
}
```

### 4.4. Цветовая связь облаков с климатом массива

**Проблема:** Облака одинакового цвета во всём мире, но каждый массив имеет свой климат.

**Решение:** Dynamic cloud tinting на основе позиции игрока.

```csharp
public class CloudClimateTinter : MonoBehaviour
{
    public List<MassifCloudTint> massifTints;

    void Update()
    {
        // Найти ближайший массив
        var closest = FindClosestMassif(playerPosition);

        // Lerp цвета облаков
        foreach (var cloud in activeClouds)
        {
            Color tint = closest.biomeProfile.cloudTintColor;
            cloud.material.SetColor("_CloudTint", tint);
        }
    }
}
```

**Цвета тинта по массивам:**

| Массив | Cloud Tint | Описание |
|--------|-----------|----------|
| Гималаи | #c8d4e0 | Холодный голубоватый |
| Альпы | #d8d8d0 | Нейтральный серо-зелёный |
| Килиманджаро | #e0d0b8 | Тёплый золотистый |
| Анды | #d4c8b8 | Сухой коричневатый |
| Аляска | #c0c8d0 | Холодный серо-синий |

---

## 5. Производительность

### 5.1. Бюджет полигонов

| Категория | Количество | Полигонов на объект | Итого треугольников |
|-----------|-----------|-------------------|-------------------|
| **5 главных пиков (LOD0)** | 5 | 2048 | 10,240 |
| **23 вторичных пиков (LOD0)** | 23 | 1024 | 23,552 |
| **~15 хребтов (LOD0)** | 15 | 768 | 11,520 |
| **9 фермерских платформ** | 9 | 256 | 2,304 |
| **Террасы (27 штук, 3 на ферму)** | 27 | 128 | 3,456 |
| **Завеса (плоскость)** | 1 | 2 | 2 |
| **Облака (300 объектов, LOD0)** | 300 | 24 | 7,200 |
| **ИТОГО (все LOD0)** | **380** | — | **58,274** |

**Target: 60 FPS на среднем ПК (GTX 1060 / RTX 3050)**

**Бюджет на кадр:** 16.67ms

| Подсистема | Бюджет | Примечание |
|-----------|--------|-----------|
| Rendering | 8ms | Основная нагрузка |
| Culling | 2ms | Frustum + occlusion |
| Animation | 1ms | Облака, морфинг |
| Physics | 1ms | Коллайдеры ферм |
| Scripts | 2ms | Логика, AI |
| Audio | 0.5ms | Звуки ветра, гул |
| Reserve | 1.5ms | Запас |

### 5.2. Оптимизация отрисовки

#### 5.2.1. Frustum Culling

**Автоматический** в Unity. Все горные меши — отдельные GameObjects с Renderer. Unity автоматически не рендерит то, что за пределами камеры.

#### 5.2.2. Occlusion Culling

**Проблема:** Горы — большие объекты, часто перекрывают друг друга.

**Решение:** Unity Occlusion Culling (baked) для статичных гор.

```
Window → Rendering → Occlusion Culling
→ Bake
→ Min Region Size: 5 units
→ Smallest Occluder: 10 units
```

**Ожидаемый выигрыш:** 20-30% для сцен, где игрок внутри массива (горы за спиной не рендерятся).

#### 5.2.3. LOD-переключение

**Автоматическое** через Unity LOD Group.

**Реальное использование:**

| Позиция игрока | Видимые LOD0 | Видимые LOD1 | Видимые LOD2 |
|----------------|-------------|-------------|-------------|
| В городе (Примум) | 1-3 (ближайшие пики) | 3-5 (средние пики) | 20+ (дальние) |
| На ферме | 1 (пик фермы) | 2-3 (соседние) | 25+ (остальные) |
| В полёте (середина маршрута) | 0-1 | 4-6 | 22+ |
| Высоко (Y=70+) | 2-4 (вершины видны) | 8-12 | 14+ |

**Реальное число треугольников в кадре (типичная сцена):**

| Сцена | LOD0 | LOD1 | LOD2 | Итого |
|-------|------|------|------|-------|
| Город | 3 × 2048 = 6,144 | 4 × 512 = 2,048 | 21 × 2 = 42 | ~8,234 |
| Ферма | 1 × 1024 = 1,024 | 3 × 384 = 1,152 | 24 × 2 = 48 | ~2,224 |
| Полёт | 1 × 2048 = 2,048 | 5 × 512 = 2,560 | 22 × 2 = 44 | ~4,652 |
| Высоко | 3 × 2048 = 6,144 | 10 × 512 = 5,120 | 15 × 2 = 30 | ~11,294 |

**Все сцены укладываются в бюджет.**

#### 5.2.4. GPU Instancing

**Применение:** Террасы, здания ферм, элементы хребтов.

```csharp
// На материале террасы:
material.enableInstancing = true;

// Для 27 террас с одинаковым материалом:
// Draw calls: 27 → 1
// Треугольники: те же, но меньше CPU overhead
```

#### 5.2.5. Static Batching

**Применение:** Хребты и вторичные пики — статичные объекты.

```
Inspector → GameObject → Static → Batching (для всех горных мешей)
```

**Результат:** Несколько draw calls для статичных объектов объединяются.

### 5.3. Бюджет памяти

| Ресурс | Размер | Примечание |
|--------|--------|-----------|
| Горные меши (все LOD) | ~15 MB | 28 пиков x 3 LOD x ~200KB |
| Heightmap текстуры | ~2 MB | 28 x 512x512 R16 = ~7MB, сжатые = ~2MB |
| Материалы гор | ~1 MB | 5 климатических профилей |
| Облака | ~3 MB | 300 объектов + материалы |
| Фермы (префабы) | ~5 MB | 9 префабов + текстуры |
| Завеса | ~0.5 MB | Плоскость + particle system |
| **ИТОГО** | **~26.5 MB** | **Далеко до лимита (2GB VRAM)** |

### 5.4. Бюджет CPU

| Операция | Частота | Время | Примечание |
|----------|---------|-------|-----------|
| Генерация мешей | Один раз при старте | ~500ms | Можно разбить на корутины |
| LOD переключение | Каждый кадр (автоматический) | <0.1ms | Unity internal |
| Обновление облаков | Каждый кадр | ~1ms | 300 облаков x движение |
| Проверка коридоров | Каждый кадр (на корабле) | <0.05ms | Простой Distance check |
| Завеса (trigger check) | OnTriggerEnter | <0.01ms | Event-based |

### 5.5. Стратегия асинхронной загрузки

**Проблема:** Генерация 28 пиков с хребтами и фермами = ~500ms, фризит кадр.

**Решение:** Coroutine-based loading.

```csharp
public class FixedWorldGenerator : MonoBehaviour
{
    public IEnumerator GenerateWorldAsync()
    {
        yield return BuildMassifsAsync();     // ~150ms, разбито на 3 кадра
        yield return BuildRidgesAsync();       // ~100ms, разбито на 2 кадра
        yield return BuildFarmsAsync();        // ~100ms, разбито на 2 кадра
        yield return BuildVeilAsync();         // ~50ms
        yield return SetupCorridorsAsync();    // ~50ms
        yield return BuildCloudsAsync();       // ~50ms
    }

    private IEnumerator BuildMassifsAsync()
    {
        for (int i = 0; i < worldData.massifs.Count; i++)
        {
            BuildMassif(worldData.massifs[i]);
            if (i % 2 == 0) yield return null; // yield каждые 2 массива
        }
    }
}
```

**Общее время загрузки:** ~500ms, разбитое на ~10 кадров = **никаких заметных фриз**

---

## 6. Данные для прототипа

### 6.1. Полный список 28 пиков с координатами

Данные на основе WorldLandscape_Design.md, секции 3.1-3.5.
Y = реальная_высота / 100. XZ — игровые координаты.

#### Гималайский массив (8 пиков)

| # | Пик | X | Y | Z | Радиус | Форма | Роль |
|---|-----|------|------|------|--------|-------|------|
| 1 | Эверест | 0 | 88.48 | 0 | 600 | Tectonic | MainCity (Примум) |
| 2 | Лхоцзе | +600 | 72.00 | -400 | 400 | Tectonic | Military |
| 3 | Макалу | +1200 | 70.00 | +300 | 380 | Tectonic | Secondary |
| 4 | Чо-Ойю | -800 | 65.00 | +600 | 350 | Tectonic | Farm |
| 5 | Шишапангма | -1400 | 62.00 | +900 | 320 | Tectonic | Abandoned |
| 6 | Пик Северный | +200 | 55.00 | +1200 | 250 | Dome | Farm (010) |
| 7 | Пик Южный | -300 | 50.00 | -1000 | 220 | Dome | Farm (020) |
| 8 | Пик Восточный | +1500 | 45.00 | -200 | 200 | Tectonic | Navigation |

#### Альпийский массив (6 пиков)

| # | Пик | X | Y | Z | Радиус | Форма | Роль |
|---|-----|------|------|------|--------|-------|------|
| 9 | Монблан | -1,310 | 48.08 | +2,810 | 350 | Tectonic | MainCity (Секунд) |
| 10 | Гранд-Жорасс | -900 | 42.00 | +3,100 | 280 | Tectonic | Military |
| 11 | Маттерхорн | -600 | 40.00 | +2,400 | 250 | Tectonic | Military |
| 12 | Финстераархорн | -1,000 | 38.00 | +3,500 | 260 | Tectonic | Secondary |
| 13 | Вайсхорн | -1,800 | 40.00 | +2,500 | 230 | Tectonic | Farm (С010) |
| 14 | Пик ЮЗ | -1,700 | 35.00 | +2,200 | 200 | Dome | Abandoned |

#### Африканский массив (4 пика)

| # | Пик | X | Y | Z | Радиус | Форма | Роль |
|---|-----|------|------|------|--------|-------|------|
| 15 | Кибо | -1,881 | 58.95 | -3,010 | 400 | Volcanic | MainCity |
| 16 | Мавензи | -1,400 | 48.00 | -2,800 | 280 | Volcanic | Secondary |
| 17 | Шира | -2,300 | 35.00 | -3,200 | 220 | Volcanic | Farm (К010) |
| 18 | Пик Восточный | -1,200 | 30.00 | -2,600 | 180 | Dome | Secondary |

#### Андийский массив (6 пиков)

| # | Пик | X | Y | Z | Радиус | Форма | Роль |
|---|-----|------|------|------|--------|-------|------|
| 19 | Аконкагуа | -4,176 | 69.62 | -2,110 | 500 | Tectonic | MainCity (Тертиус) |
| 20 | Охос-дель-Саладо | -3,600 | 58.00 | -1,400 | 350 | Tectonic | Secondary |
| 21 | Невадо-Трес-Крусес | -3,400 | 52.00 | -1,000 | 300 | Tectonic | Secondary |
| 22 | Пик Северный | -4,500 | 55.00 | -1,200 | 250 | Dome | Farm (Т010) |
| 23 | Пик Южный | -4,000 | 48.00 | -2,800 | 230 | Dome | Secondary |
| 24 | Пик Западный | -5,200 | 42.00 | -2,400 | 200 | Tectonic | Abandoned |

#### Аляскинский массив (5 пиков)

| # | Пик | X | Y | Z | Радиус | Форма | Роль |
|---|-----|------|------|------|--------|-------|------|
| 25 | Денали | +1,255 | 61.90 | +4,685 | 450 | Isolated | MainCity (Квартус) |
| 26 | Форакер | +700 | 52.00 | +4,200 | 300 | Dome | Secondary |
| 27 | Хантер | +1,700 | 42.00 | +4,500 | 250 | Tectonic | Secondary |
| 28 | Пик СЗ | +500 | 40.00 | +5,200 | 220 | Dome | Farm (Кв010) |

### 6.2. Хребты (15 штук)

| # | Хребет | Массив | Пик A | Пик B | Длина (units) | Средняя Y |
|---|--------|--------|-------|-------|--------------|-----------|
| 1 | Южный хребет | Гималаи | Эверест | Лхоцзе | 721 | ~55 |
| 2 | Южный-2 | Гималаи | Лхоцзе | Пик Южный | 1342 | ~40 |
| 3 | Западный хребет | Гималаи | Эверест | Чо-Ойю | 1000 | ~50 |
| 4 | Западный-2 | Гималаи | Чо-Ойю | Шишапангма | 664 | ~45 |
| 5 | Восточный хребет | Гималаи | Эверест | Макалу | 1237 | ~55 |
| 6 | Северный хребет | Гималаи | Эверест | Пик Северный | 1217 | ~45 |
| 7 | СВ хребет | Альпы | Монблан | Гранд-Жорасс | 534 | ~35 |
| 8 | СВ-2 | Альпы | Гранд-Жорасс | Маттерхорн | 791 | ~30 |
| 9 | ЮВ хребет | Альпы | Монблан | Финстераархорн | 806 | ~30 |
| 10 | Западный хребет | Альпы | Монблан | Вайсхорн | 510 | ~30 |
| 11 | Восточный хребет | Африка | Кибо | Мавензи | 527 | ~35 |
| 12 | Восточный-2 | Африка | Мавензи | Пик Восточный | 762 | ~25 |
| 13 | Западный хребет | Африка | Кибо | Шира | 458 | ~30 |
| 14 | Северный хребет | Анды | Аконкагуа | Охос-дель-Саладо | 901 | ~50 |
| 15 | Северный-2 | Анды | Охос | Трес-Крусес | 812 | ~40 |

*Дополнительные хребты (не нумерованы): Южный Анды, Западный Анды, ЮЗ Аляска, ЮВ Аляска, СЗ Аляска*

### 6.3. Паромные станции (подвесные тросы)

Связи между фермами и городами, требующие FerryCableSystem:

| # | От | Кому | Длина (units) | Перепад Y |
|---|-----|------|--------------|-----------|
| 1 | Эверест 010 | Эверест 020 | 539 | 7 units |
| 2 | Эверест 020 | Эверест 110 | 424 | 5 units |
| 3 | Эверест 010 | Дон 1010 | 1020 | 17 units |
| 4 | Монблан С010 | Секунд (Монблан) | 640 | 16 units |
| 5 | Кибо К010 | Кибо (главный) | 527 | 21 units |
| 6 | Аконкагуа Т010 | Аконкагуа Т020 | 640 | 7 units |
| 7 | Аконкагуа Т010 | Тертиус | 781 | 25 units |
| 8 | Денали Кв010 | Квартус (Денали) | 640 | 27 units |

---

## 7. Структура файлов проекта

### 7.1. ScriptableObject ассеты

```
Assets/_Project/Data/
├── WorldData.asset                        # Главный ассет мира
├── BiomeProfiles/
│   ├── HimalayanBiome.asset
│   ├── AlpineBiome.asset
│   ├── AfricanBiome.asset
│   ├── AndeanBiome.asset
│   └── AlaskanBiome.asset
├── Massifs/
│   ├── HimalayanMassif.asset
│   ├── AlpineMassif.asset
│   ├── AfricanMassif.asset
│   ├── AndeanMassif.asset
│   └── AlaskanMassif.asset
└── Corridors/
    ├── GlobalCorridor.asset
    ├── PrimusCorridor.asset
    ├── SecundusCorridor.asset
    ├── KiboCorridor.asset
    ├── TertiusCorridor.asset
    └── QuartusCorridor.asset
```

### 7.2. Скрипты

```
Assets/_Project/Scripts/World/
├── FixedWorldGenerator.cs                 # Главный генератор (заменяет WorldGenerator)
├── MountainMeshBuilder.cs                 # Генерация мешей гор
├── RidgeMeshBuilder.cs                    # Генерация мешей хребтов
├── MountainLOD.cs                         # LOD компонент
├── TerraceBuilder.cs                      # Генерация террас
├── FerryCableSystem.cs                    # Паромные тросы
├── VeilSystem.cs                          # Система завесы
├── CloudClimateTinter.cs                  # Тинт облаков по климату
└── WorldData.cs                           # ScriptableObject определения
```

### 7.3. Префабы

```
Assets/_Project/Prefabs/World/
├── MountainPeak_LOD0.prefab               # Базовый префаб пика (LOD0)
├── MountainPeak_LOD1.prefab               # Базовый префаб пика (LOD1)
├── MountainPeak_LOD2.prefab               # Billboard префаб
├── Ridge.prefab                           # Базовый префаб хребта
└── Farms/
    ├── FarmPlatform.prefab                # Базовая фермерская платформа
    ├── FarmWithGreenhouse.prefab          # Ферма с теплицей
    └── FerryStation.prefab                # Паромная станция
```

### 7.4. Материалы

```
Assets/_Project/Art/Materials/Landscape/
├── Rock_Himalayan.mat                     # Тёмно-серый гранит
├── Rock_Alpine.mat                        # Светло-серый известняк
├── Rock_Volcanic.mat                      # Красно-коричневый вулканический
├── Rock_Andean.mat                        # Коричнево-серый
├── Rock_Alaskan.mat                       # Тёмный базальт
├── Snow.mat                               # Голубоватый снег
├── Ice.mat                                # Полупрозрачный голубой лёд
├── Terrace.mat                            # Зелёный #5d8a4a
├── AntiGravPlatform.mat                   # Emissive #4fc3f7
└── Veil.mat                               # Фиолетовый #2d1b4e
```

---

## 8. План миграции (от WorldGenerator к FixedWorldGenerator)

### Фаза 1: Подготовка (1 день)
- [ ] Создать ScriptableObject: WorldData, 5 MountainMassif, 5 BiomeProfile
- [ ] Заполнить координаты 28 пиков, 15 хребтов, 9 ферм
- [ ] Создать AltitudeCorridorData ассеты для 5 городов + глобальный

### Фаза 2: Генерация мешей (2 дня)
- [ ] Реализовать MountainMeshBuilder (4 типа форм)
- [ ] Реализовать RidgeMeshBuilder (Catmull-Rom + V-profile)
- [ ] Реализовать MountainLOD (3 уровня + billboard)
- [ ] Протестировать на Эвересте (сравнить с реальным профилем)

### Фаза 3: Фермы и инфраструктура (2 дня)
- [ ] Создать FarmPlatform префаб
- [ ] Реализовать TerraceBuilder
- [ ] Реализовать FerryCableSystem
- [ ] Разместить 9 ферм на хребтах

### Фаза 4: Завеса и облака (1 день)
- [ ] Создать VeilSystem (плоскость + fog + lightning)
- [ ] Настроить CloudClimateTinter
- [ ] Обновить AltitudeCorridorData значения
- [ ] Протестировать height clamping

### Фаза 5: Оптимизация (1 день)
- [ ] Настроить LOD Group для всех пиков
- [ ] Запечь Occlusion Culling
- [ ] Включить GPU Instancing для террас
- [ ] Профилировщик: проверить FPS на 3 сценах (город, ферма, полёт)

### Фаза 6: Интеграция (1 день)
- [ ] Заменить WorldGenerator → FixedWorldGenerator
- [ ] Убедиться, что CloudSystem работает с новым миром
- [ ] Убедиться, что AltitudeCorridorSystem работает
- [ ] Финальное тестирование

**Итого:** ~8 рабочих дней

---

## 9. Риски и митигация

| Риск | Вероятность | Влияние | Митигация |
|------|------------|--------|-----------|
| **Горы выглядят одинаково** | Средняя | Высокое | Ручные heightmap-профили для 5 главных пиков, уникальные AnimationCurve |
| **Хребты проходят сквозь пики** | Средняя | Среднее | Shared vertices на стыках; пост-генерационная проверка коллизий |
| **FPS падает при облёте мира** | Низкая | Высокое | LOD2 (billboard) для дальних пиков; frustum culling; async loading |
| **Завеса видна с большого расстояния как плоскость** | Высокая | Среднее | Depth fade шейдер; horizon blending; curvature на краях |
| **Игрок видит землю под облаками** | Средняя | Критическое | Multi-layer: завеса + fog + camera culling + gameplay boundary |
| **ScriptableObject слишком большой** | Низкая | Низкое | Разделить на 5 massif ассетов + 1 world data; addressables для ленивой загрузки |
| **Масштаб 1:2000 слишком мал для геймплея** | Низкая | Среднее | Прототипировать 1 маршрут; замерить время полёта; скорректировать при необходимости |

---

## 10. Метрики успеха прототипа

| Метрика | Target | Как измерить |
|---------|--------|-------------|
| **FPS (средний)** | >= 60 | Unity Profiler, сцена "Full World Flythrough" |
| **FPS (1% low)** | >= 45 | Unity Profiler |
| **Время загрузки мира** | < 2 секунд | Time.realtimeSinceStartup |
| **Полигонов в кадре (макс)** | < 15,000 | Frame Debugger |
| **Draw calls (макс)** | < 200 | Frame Debugger |
| **VRAM usage** | < 100 MB | Profiler → Memory |
| **Узнаваемость пиков** | 80% игроков называют правильный пик по силуэту | Playtest |
| **Невидимость земли** | 0 случаев, когда игрок видит землю под облаками | Playtest + automated check |

---

## 11. Глоссарий

| Термин | Определение |
|--------|------------|
| **Массив (Massif)** | Группа горных пиков, соединённых хребтами |
| **Хребет (Ridge)** | Горная цепь, соединяющая два или более пика |
| **Седловина (Saddle)** | Низшая точка хребта между двумя пиками |
| **Heightmap Keypoint** | Контрольная точка профиля высоты меша |
| **Завеса (Veil)** | Смертельный фиолетовый слой на Y=12, скрывающий землю |
| **Коридор высот** | Допустимый диапазон высот для полёта (глобальный или городской) |
| **LOD (Level of Detail)** | Уровни детализации меша (0=полный, 1=средний, 2=billboard) |
| **Терраса** | Антигравийная платформа на склоне хребта для фермерства |
| **Паромная станция** | Подвесной трос между двумя поселениями |
| **Биом** | Климатическая зона с уникальными цветами и атмосферой |
