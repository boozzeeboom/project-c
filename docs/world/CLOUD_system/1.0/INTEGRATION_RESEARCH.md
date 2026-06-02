# CLOUD_SYSTEM + GENERATOR7.0 INTEGRATION RESEARCH

**Версия:** 1.0 | **Дата:** 12 мая 2026 | **Status:** 📋 Research Complete
**Автор:** Claude Analysis (3 subagent parallel deep dive)

---

## 1. Executive Summary

### Исходное состояние

| Система | Статус | Изолирована? |
|---------|--------|---------------|
| **generator7.0** | Готов, Editor-only | ✅ ДА |
| **CLOUD_system** (Upper/Middle/Lower) | Работает, Runtime | ✅ ДА |
| **STORM_system** | Работает (ручная генерация) | ✅ ДА |
| **VEIL_system** | Работает, НЕ трогаем | N/A |

**Критическая проблема:** Три независимые системы, работающие по отдельности. generator7.0 генерирует sphere hierarchies для Editor preview, но НЕ подключен к рантайм рендерингу. STORM использует упрощённую ручную генерацию вместо generator7.0 Column archetype.

### Цель интеграции

```
generator7.0 (sphere hierarchies, parent mesh projection, non-repeating patterns)
        │
        ▼
CloudGeneratorAdapter / StormGeneratorAdapter
        │
        ▼
NearCloudRenderer / StormCloudGenerator (существующий инстансинг)
        │
        ▼
WindManager + CloudSpherePhysics (parting, spring-back)
```

---

## 2. Архитектурная карта до интеграции

```
┌─────────────────────────────────────────────────────────────────────────┐
│           ДО: Три изолированные системы                                │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                         │
│  ┌─────────────────────────┐    ┌─────────────────────────┐             │
│  │   generator7.0         │    │   CLOUD_system          │             │
│  │   (Editor Only)        │    │   NearCloudRenderer     │             │
│  │                        │    │   DistantCloudManager   │             │
│  │   CloudGenerator.cs     │    │                         │             │
│  │   List<CloudSphere>     │    │   Random generation     │             │
│  │   CloudParentMesh.cs    │    │   CloudData[]           │             │
│  │   CloudTypes.cs         │    │   Matrix4x4[]           │             │
│  │   CloudMath.cs          │    │                         │             │
│  └───────────┬─────────────┘    └───────────┬─────────────┘             │
│              │                            │                            │
│              │                            │                            │
│              ▼                            ▼                            │
│  ┌─────────────────────────────────────────────────────────────┐      │
│  │              WindManager (shared wind)                       │      │
│  └─────────────────────────────────────────────────────────────┘      │
│                                                                         │
│  ┌─────────────────────────┐    ┌─────────────────────────┐             │
│  │   STORM_system          │    │   VEIL_system           │             │
│  │   StormCloudGenerator   │    │   HorizonVeilRenderer   │             │
│  │   PuffData[]           │    │   НЕ ТРОГАЕМ            │             │
│  │   Manual column gen     │    │                         │             │
│  └─────────────────────────┘    └─────────────────────────┘             │
│                                                                         │
└─────────────────────────────────────────────────────────────────────────┘
```

---

## 3. Архитектурная карта пocле интеграции

```
┌─────────────────────────────────────────────────────────────────────────┐
│           ПОСЛЕ: generator7.0 подключен ко всем системам               │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                         │
│  ┌─────────────────────────────────────────────────────────────────┐      │
│  │                    CloudGeneratorAdapter                       │      │
│  │                    (NEW - MISSING)                            │      │
│  │                    CloudSphere[] → CloudData[]                 │      │
│  │                    + Pattern Library ScriptableObjects         │      │
│  └────────────────────────────┬────────────────────────────────┘      │
│                               │                                        │
│  ┌────────────────────────────┴────────────────────────────────┐      │
│  │                         CloudPatternConfig                    │      │
│  │                         (ScriptableObject)                    │      │
│  │                         - Name, Archetype                     │      │
│  │                         - Sphere/Column/Platform/Tree params  │      │
│  │                         - ParentMeshPath (optional)           │      │
│  │                         - SizeRange                           │      │
│  └────────────────────────────┬────────────────────────────────┘      │
│                               │                                        │
│              ┌────────────────┼────────────────┐                      │
│              ▼                ▼                ▼                      │
│  ┌──────────────────┐ ┌──────────────┐ ┌──────────────┐               │
│  │ NearCloudRenderer│ │ DistantCM   │ │ StormGenAdapter│              │
│  │ Upper Layer     │ │             │ │               │               │
│  │ Middle Layer    │ │             │ │ STORM → Column │              │
│  │ Lower Layer     │ │             │ │               │               │
│  └────────┬─────────┘ └──────┬─────┘ └──────┬───────┘               │
│           │                 │              │                          │
│           └────────────────┴──────────────┴──────────────────────    │
│                               │                                     │
│                               ▼                                     │
│                    ┌──────────────────────┐                         │
│                    │   WindManager        │                         │
│                    │   + CloudSpherePhysics│                        │
│                    │   (parting, spring) │                         │
│                    └──────────────────────┘                         │
│                                                                         │
│  ┌─────────────────────────┐                                          │
│  │   VEIL_system           │                                          │
│  │   НЕ ТРОГАЕМ            │                                          │
│  └─────────────────────────┘                                          │
│                                                                         │
└─────────────────────────────────────────────────────────────────────────┘
```

---

## 4. Компонентный анализ

### 4.1 generator7.0 — Что умеет

| Возможность | Реализация | Готов к рантайму? |
|-------------|------------|-------------------|
| **Sphere archetype** | Fibonacci sphere sampling + FBM + Worley + cascade | ✅ Да |
| **Column archetype** | Floor/ring stacking + wobble + Perlin | ✅ Да |
| **Platform archetype** | 2D Fibonacci spiral + gaussian thickness | ✅ Да |
| **Tree archetype** | Recursive branching + lateral | ✅ Да |
| **Parent mesh projection** | CloudParentMesh.SampleSurface() | ⚠️ Editor-only (#if UNITY_EDITOR) |
| **Non-repeating via** | XORSHIFT32 PRNG + φ ratio + per-child noise | ✅ Да |
| **Deterministic seed** | DeterministicRandom(seed) | ✅ Да |
| **Возвращает** | `List<CloudSphere>` {X,Y,Z,Radius,Depth,Density,Archetype} | ✅ Структура готова |

### 4.2 NearCloudRenderer — Что нужно

| Требуется | Сейчас | Нужно |
|-----------|--------|-------|
| **Входные данные** | `CloudData[]` (Matrix4x4, Scale, MeshIndex) | `CloudSphere[]` от generator7.0 |
| **Генерация** | Random ring placement | `CloudGeneratorAdapter.Convert()` |
| **Паттерны** | CloudLayerConfig (бедный) | CloudPatternConfig (богатый) |
| **Recycling** | ✅ Есть | Preserve structure, only reposition |

### 4.3 StormCloudGenerator — Что нужно

| Требуется | Сейчас | Нужно |
|-----------|--------|-------|
| **Генерация** | Manual 5-layer column (PuffData[]) | generator7.0 Column |
| **Вход** | PuffData {pos, scale, MeshIndex} | `CloudSphere[]` → PuffData[] |
| **Паттерны** | Hardcoded | ColumnParams in ScriptableObject |
| **Intensity scaling** | ❌ None | density/count/cascade от intensity |

---

## 5. Пути реализации

### 5.1 CloudGeneratorAdapter (Фаза 1)

**Назначение:** Конвертировать `List<CloudSphere>` в `CloudData[]` для NearCloudRenderer.

```csharp
// Assets/_Project/Scripts/Core/Adapters/CloudGeneratorAdapter.cs
public static class CloudGeneratorAdapter
{
    public static CloudData[] Convert(
        List<CloudSphere> spheres,
        Vector3 playerOffset,
        Vector3 windDirection,
        CloudMeshEntry[] meshEntries)
    {
        // 1. CloudSphere[] → CloudData[]
        // 2. Map Archetype → MeshIndex
        // 3. Apply playerOffset (player-relative positions)
        // 4. Depth → scale multiplier (child spheres smaller)
        // 5. Density → alpha/scale variation
    }
    
    public static CloudLayerConfig[] BuildConfigsFromPattern(
        CloudPatternConfig pattern)
    {
        // CloudPatternConfig (ScriptableObject) → CloudLayerConfig[] для generator7.0
    }
}
```

**Слот использования:**
```
NearCloudRenderer
├── .Generate(playerPos)  ← OLD: random ring
└── .GenerateFromPattern(pattern, playerPos)  ← NEW: adapter-based
```

### 5.2 StormGeneratorAdapter (Фаза 2)

**Назначение:** Подключить STORM к generator7.0 Column archetype.

```csharp
// Assets/_Project/Scripts/Core/Adapters/StormGeneratorAdapter.cs
public static class StormGeneratorAdapter
{
    public static PuffData[] GenerateStormColumn(
        CloudLayerConfig columnConfig,
        Vector3 worldPos,
        float intensity,
        DeterministicRandom rng)
    {
        // 1. CloudGenerator.GenerateColumnLayer(config) → List<CloudSphere>
        // 2. Scale by intensity (density, count boost)
        // 3. CloudSphere[] → PuffData[]
        // 4. Apply worldPos offset
    }
}
```

**Слот использования:**
```
StormCloudGenerator
├── .GeneratePuffs()  ← OLD: manual column
└── .InitializeWithPattern(pattern, intensity)  ← NEW: generator7.0
```

### 5.3 CloudPatternConfig (ScriptableObject)

**Назначение:** Паттерн-библиотека для дизайнеров и мира.

```csharp
// Assets/_Project/Scripts/Core/Data/CloudPatternConfig.cs
[CreateAssetMenu(fileName = "Pattern_Name", menuName = "ProjectC/Clouds/Pattern")]
public class CloudPatternConfig : ScriptableObject
{
    [Header("Archetype")]
    public CloudArchetype Archetype = CloudArchetype.Sphere;
    
    [Header("Sphere Params")]
    public int CascadeDepth = 3;
    public int BumpsPerLevel = 24;
    public float ChildRatio = 30f;
    public float Jitter = 0.3f;
    public float Clustering = 0.5f;
    
    [Header("Column Params")]
    public ColumnParams ColumnParams;  // from generator7.0
    
    [Header("Parent Mesh (optional)")]
    public string ParentMeshPath;  // для форм типа "череп"
    
    [Header("Size")]
    public SizeRange SizeRange = new SizeRange { Min = 5, Max = 20 };
}
```

### 5.4 CloudSpherePhysics (Фаза 3)

**Назначение:** Физика расступания при прохождении через облако + spring-back.

```csharp
// Assets/_Project/Scripts/Core/Physics/CloudSpherePhysics.cs
public struct CloudSpherePhysics
{
    public Vector3 BasePosition;   // от generator7.0
    public Vector3 CurrentPosition;
    public Vector3 Velocity;
    public float SpringK;         // 5f default
    public float Damping;        // 0.9f default
    
    public void ApplyWind(Vector3 dir, float speed, float dt);
    public void ApplyParting(Vector3 fromDirection, float strength);
    public void SpringBack(float dt);
}

// Hook в StormCloudGenerator.Update():
// 1. ApplyWindToAllSpheres()
// 2. CheckPlayerProximity() → ApplyParting() if close
// 3. SpringBack() for all
```

### 5.5 RuntimeMeshSampler (Фаза 4)

**Назначение:** Runtime версия CloudParentMesh.SampleSurface().

```csharp
// Assets/_Project/Scripts/Core/Utils/RuntimeMeshSampler.cs
public static class RuntimeMeshSampler
{
    // CloudParentMesh.SampleSurface() обёрнут в #if UNITY_EDITOR
    // Для runtime нужен отдельный метод
    
    public static List<Vector3> SampleSurfaceRuntime(Mesh mesh, int pointCount)
    {
        // 1. Build triangle cumulative areas (runtime-safe)
        // 2. Weighted random selection
        // 3. Barycentric interpolation
        // 4. Cache results (mesh rarely changes)
    }
}
```

---

## 6. Фазы реализации

### ФАЗА 1: CloudGeneratorAdapter для Upper/Middle/Lower

| Задача | Приоритет | Тестирование |
|--------|-----------|--------------|
| **A. Создать CloudPatternConfig ScriptableObject** | P0 | Валидация в Inspector |
| **B. Создать CloudGeneratorAdapter** | P0 | Convert() возвращает корректные CloudData[] |
| **C. Добавить GenerateFromPattern() в NearCloudRenderer** | P0 | Clouds рендерятся с паттерном |
| **D. Создать 3 паттерна (Upper/Middle/Lower)** | P1 | Визуальное сравнение с текущими |
| **E. Подключить паттерны в CloudManager.Initialize()** | P1 | Wind работает, recycling работает |

**Критерии успеха:**
- Clouds Upper/Middle/Lower визуально сложнее чем текущие random sphere
- Non-repeating: при рестарте — новые формы ( тот же seed? разный?)
- Performance: ≤3ms GPU, ≤1ms CPU overhead

**Тестирование:**
```csharp
// Тест 1: Паттерн применяется корректно
[Test] public void Pattern_Upper_RendersCorrectly()
{
    var pattern = Resources.Load<CloudPatternConfig>("Patterns/Pattern_Upper_Cumulus");
    renderer.SetPattern(pattern);
    // Visual inspection: cauliflower-like shapes
}

// Тест 2: Non-repeating
[Test] public void DifferentSeed_DifferentOutput()
{
    pattern1.Seed = 12345;
    pattern2.Seed = 67890;
    // CloudData[] должны отличаться
}

// Тест 3: Wind integration
[Test] public void Wind_CloudsMoveTogether()
{
    WindManager.Instance.ApplyWindUpdate(Vector3.right, 10f);
    yield null;
    // Все clouds двигаются вправо
}
```

---

### ФАЗА 2: Storm → generator7.0 Column

| Задача | Приоритет | Тестирование |
|--------|-----------|--------------|
| **A. Создать StormGeneratorAdapter** | P0 | PuffData[] корректные |
| **B. Добавить useGeneratorV7 flag в StormCloudGenerator** | P0 | Toggle switch работает |
| **C. StormCloudGenerator.UseGeneratorV7 path** | P0 | Column из generator7.0 рендерится |
| **D. Intensity scaling** | P1 | High intensity = denser column |
| **E. Создать 3 Storm паттерна (light/medium/heavy)** | P1 | Визуальное сравнение |

**Критерии успеха:**
- Storm column визуально сложнее чем 5-layer manual
- Worley erosion + Perlin wobble видны
- Intensity влияет на внешний вид (больше/fлучше)

**Тестирование:**
```csharp
// Тест 1: Column archetype выглядит естественно
[Test] public void Storm_UsesColumnArchetype()
{
    var adapter = new StormGeneratorAdapter();
    var puffs = adapter.GenerateStormColumn(heavyConfig, pos, 0.9f, rng);
    // Visual inspection: stacked floors, natural erosion
}

// Тест 2: Intensity scales properly
[Test] public void Storm_IntensityAffectsDensity()
{
    var light = adapter.GenerateStormColumn(config, pos, 0.3f, rng);
    var heavy = adapter.GenerateStormColumn(config, pos, 1.0f, rng);
    // light должен иметь меньше сфер чем heavy
}

// Тест 3: Lightning still works
[Test] public void Storm_LightningFlashWorks()
{
    controller.TriggerLightning();
    // Material flash + VFX
}
```

---

### ФАЗА 3: Wind Physics (Parting + Spring-back)

| Задача | Приоритет | Тестирование |
|--------|-----------|--------------|
| **A. Создать CloudSpherePhysics struct** | P1 | Spring-back работает корректно |
| **B. Добавить ApplyParting() в StormCloudGenerator** | P1 | Spheres расступаются при proximity |
| **C. Spring-back after player passes** | P1 | Spheres возвращаются в formation |
| **D. Tune SpringK и Damping** | P2 | Плавное поведение |

**Критерии успеха:**
- При пролёте через Storm → сферы расступаются
- После пролёта → сферы возвращаются (spring)
- Нет резких "прыжков" позиций

**Тестирование:**
```csharp
// Тест 1: Parting on proximity
[Test] public void Storm_PartsWhenPlayerApproaches()
{
    storm.transform.position = playerPos + Vector3.forward * 50f;
    yield return null;
    // Spheres должны сместиться от игрока
}

// Тест 2: Spring-back returns to formation
[Test] public void Storm_SpringsBackAfterPassing()
{
    // Симулировать proximity → ApplyParting()
    // Wait 2 seconds
    // Spheres должны быть ~90% возвращены
}
```

---

### ФАЗА 4: Parent Mesh Projection + World Automation

| Задача | Приоритет | Тестирование |
|--------|-----------|--------------|
| **A. Создать RuntimeMeshSampler** | P2 | Точки на surface mesh совпадают с Editor version |
| **B. Добавить ParentMeshPath в CloudPatternConfig** | P2 | Skull cloud формируется по мешу |
| **C. Создать WorldCloudPopulator** | P2 | Регионы заполняются паттернами автоматически |
| **D. Создать WorldRegion данных** | P3 | Region-based presets работают |

**Критерии успеха:**
- Parent mesh (skull) → spheres на поверхности черепа
- При пролёте через skull cloud → spheres расступаются
- World regions получают правильные паттерны

**Тестирование:**
```csharp
// Тест 1: Runtime sampling matches Editor
[Test] public void ParentMesh_RuntimeMatchesEditor()
{
    var editorPoints = CloudParentMesh.SampleSurface(mesh, 2000);
    var runtimePoints = RuntimeMeshSampler.SampleSurfaceRuntime(mesh, 2000);
    // Points должны быть похожи (within tolerance)
}

// Тест 2: Skull cloud forms correct shape
[Test] public void SkullCloud_FormsOnMesh()
{
    pattern.ParentMeshPath = "Assets/Shapes/skull_cloud.mesh";
    var spheres = CloudGenerator.Generate(pattern);
    // Spheres分布 вокруг skull mesh
}
```

---

## 7. Тестирование — чеклист

### 7.1 CloudGeneratorAdapter (Фаза 1)

| Тест | Метод | Критерий успеха |
|------|-------|-----------------|
| T1.1: Convert returns correct CloudData[] | Unit | CloudData.Matrix = TRS(pos, rot, scale) |
| T1.2: Archetype maps to MeshIndex | Unit | Sphere → 0, Platform → 1, etc |
| T1.3: Depth affects scale | Unit | Depth 0 > Depth 1 > Depth 2 |
| T1.4: Pattern loads from asset | Integration | CloudPatternConfig загружается |
| T1.5: NearCloudRenderer renders with pattern | Integration | Graphics.DrawMeshInstanced вызывается |
| T1.6: Wind offset applied | Integration | Clouds двигаются с wind |
| T1.7: Recycling preserves pattern | Integration | Cloud реcycled = новая позиция, тот же scale |

### 7.2 StormGeneratorAdapter (Фаза 2)

| Тест | Метод | Критерий успеха |
|------|-------|-----------------|
| T2.1: GenerateStormColumn returns PuffData[] | Unit | PuffData[] с Column archetype |
| T2.2: Intensity affects sphere count | Unit | high > medium > light |
| T2.3: useGeneratorV7 flag toggles generation | Integration | Flag = true → Column, false → manual |
| T2.4: Lightning flash still works | Integration | _LightningFlash uniform > 0 |
| T2.5: Storm moves with wind | Integration | offset applied in Update |

### 7.3 Wind Physics (Фаза 3)

| Тест | Метод | Критерий успеха |
|------|-------|-----------------|
| T3.1: Spring-back returns to BasePosition | Unit | After 2s, distance < 10% of max displacement |
| T3.2: Parting pushes spheres away from player | Unit | Displacement direction = away from player |
| T3.3: Wind offset + physics combine correctly | Integration | Clouds двигаются и spring-back |
| T3.4: No oscillation instability | Unit | Damped oscillation, converges |

### 7.4 Parent Mesh (Фаза 4)

| Тест | Метод | Критерий успеха |
|------|-------|-----------------|
| T4.1: Runtime sampling works | Unit | List<Vector3> returned |
| T4.2: Points on mesh surface | Unit | All points within mesh bounds |
| T4.3: Skull cloud pattern renders | Integration | Spheres distributed on skull shape |
| T4.4: Proximity parting works with parent mesh | Integration | Spheres расступаются при пролёте |

---

## 8. Конфигурация паттернов — предварительные значения

### 8.1 Layer Patterns (Upper/Middle/Lower)

```csharp
// Pattern_Upper_Cumulus
Archetype = CloudArchetype.Sphere;
CascadeDepth = 3;
BumpsPerLevel = 24;
ChildRatio = 30f;
Jitter = 0.3f;
Clustering = 0.5f;
SizeRange = { Min = 5, Max = 20 };  // 80-120m effective

// Pattern_Middle_Cumulus  
Archetype = CloudArchetype.Sphere;
CascadeDepth = 4;  // More detail
BumpsPerLevel = 30;
ChildRatio = 25f;  // Denser
Jitter = 0.4f;
Clustering = 0.6f;
SizeRange = { Min = 8, Max = 25 };

// Pattern_Lower_Stratocumulus
Archetype = CloudArchetype.Platform;  // Flatter clouds
CascadeDepth = 2;
BumpsPerLevel = 20;
Jitter = 0.5f;
Clustering = 0.7f;  // More clustering
SizeRange = { Min = 10, Max = 30 };
```

### 8.2 Storm Patterns

```csharp
// Pattern_Storm_Light
Archetype = CloudArchetype.Column;
ColumnParams.Floors = 8;
ColumnParams.RingsPerFloor = 6;
ColumnParams.BaseRadius = 150f;
ColumnParams.TopRadius = 250f;
ColumnParams.Wobble = 0.2f;

// Pattern_Storm_Medium
Archetype = CloudArchetype.Column;
ColumnParams.Floors = 12;
ColumnParams.RingsPerFloor = 8;
ColumnParams.BaseRadius = 200f;
ColumnParams.TopRadius = 400f;
ColumnParams.Wobble = 0.3f;

// Pattern_Storm_Heavy
Archetype = CloudArchetype.Column;
ColumnParams.Floors = 16;
ColumnParams.RingsPerFloor = 12;
ColumnParams.BaseRadius = 300f;
ColumnParams.TopRadius = 600f;
ColumnParams.Wobble = 0.4f;
```

---

## 9. Отладка — что смотреть

### 9.1 CloudGeneratorAdapter

```csharp
// CloudGeneratorAdapter.cs — debug точки:
// 1. После Convert(): проверить CloudData[].Matrix positions
// 2. Archetype → MeshIndex mapping
// 3. Depth → scale ratio

// NearCloudRenderer.GenerateFromPattern() — debug:
// 1. Spheres count (should be < MaxSphereCount)
// 2. playerOffset applied correctly
// 3. Wind offset в Update() применяется
```

### 9.2 StormGeneratorAdapter

```csharp
// StormCloudGenerator — debug:
// 1. PuffData[] count vs manual
// 2. Intensity scaling applied
// 3. useGeneratorV7 flag path taken
// 4. Column archetype: floors, rings correct

// StormController — debug:
// 1. Position lerp to target
// 2. Lightning flash triggered
// 3. Wind offset applied
```

### 9.3 Wind Physics

```csharp
// CloudSpherePhysics — debug:
// 1. BasePosition vs CurrentPosition after wind
// 2. Displacement after parting
// 3. Spring-back convergence over time

// StormCloudGenerator.Update() — debug:
// 1. Check player proximity each frame
// 2. Parting force applied (direction, magnitude)
// 3. Spring-back force applied
```

---

## 10. Known Limitations

| Ограничение | Влияние | Workaround |
|-------------|--------|------------|
| **CloudParentMesh.Editor-only** | Parent mesh projection недоступна в runtime | RuntimeMeshSampler (Фаза 4) |
| **generator7.0 не thread-safe** | Нельзя вызывать из job | Вызывать в main thread, кешировать |
| **MaxSphereCount = 5000** | Dense columns могут превысить | Reduce CascadeDepth или Density |
| **Storm intensity → manual tuning** | Нет автоматического scaling | ручная настройка ColumnParams |
| **Recycling granularity** | При recycling форма меняется (новая позиция, та же структура) | если нужно сохранить форму — нужен world-space anchoring |

---

## 11. Следующий шаг

Рекомендуемый порядок запуска в новой сессии:

```
SESSÃO 1: CloudGeneratorAdapter (Фаза 1)
├── Создать CloudPatternConfig ScriptableObject
├── Создать CloudGeneratorAdapter
├── Модифицировать NearCloudRenderer.AddPattern()
├── Создать 3 паттерна для слоёв
└── Тестировать Upper layer → Middle → Lower

SESSÃO 2: Storm → generator7.0 (Фаза 2)  
├── Создать StormGeneratorAdapter
├── Добавить useGeneratorV7 flag
├── Модифицировать StormCloudGenerator
├── Создать 3 Storm паттерна
└── Тестировать intensity scaling

SESSÃO 3: Wind Physics (Фаза 3)
├── CloudSpherePhysics struct
├── ApplyParting() API
└── Spring-back implementation

SESSÃO 4: Parent Mesh + Automation (Фаза 4)
├── RuntimeMeshSampler
├── CloudPatternConfig.ParentMeshPath
└── WorldCloudPopulator (опционально)
```

---

**Status:** ✅ Research Complete — Ready for implementation
