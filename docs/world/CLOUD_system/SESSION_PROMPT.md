# CLOUD_SYSTEM + GENERATOR7.0 INTEGRATION — SESSION PROMPT

## Контекст

Мы провели глубокий анализ интеграции generator7.0 (sphere-based cloud generator) с существующей CLOUD_system.

**Документация:**
- `docs/world/CLOUD_system/INTEGRATION_RESEARCH.md` — полный анализ интеграции (ПРОЧИТАТЬ ПЕРВЫМ)
- `docs/world/CLOUD_system/CLOUD_ARCHITECTURE.md` — текущая архитектура CLOUD_system
- `docs/world/CLOUD_system/CLOUD_CODE_SUMMARY.md` — код референс

**Ключевые компоненты:**
| Компонент | Путь |
|-----------|------|
| generator7.0 | `Assets/CloudGenerator/CloudGenerator_v7.0/CloudGenerator_v7.0/` |
| CloudManager | `Assets/_Project/Scripts/Core/CloudManager.cs` |
| NearCloudRenderer | `Assets/_Project/Scripts/Core/NearCloudRenderer.cs` |
| StormCloudGenerator | `Assets/_Project/Scripts/Core/StormCloudGenerator.cs` |
| StormController | `Assets/_Project/Scripts/Core/StormController.cs` |
| WindManager | `Assets/_Project/Scripts/Core/WindManager.cs` |

---

## Цель сессии

**Приоритет 1 (Фаза 1): CloudGeneratorAdapter для Upper/Middle/Lower слоёв**

### Что нужно сделать

#### 1.1 CloudPatternConfig ScriptableObject (НОВОЕ)

Создать `Assets/_Project/Scripts/Core/Data/CloudPatternConfig.cs`:

```csharp
[CreateAssetMenu(fileName = "Pattern_Name", menuName = "ProjectC/Clouds/Pattern")]
public class CloudPatternConfig : ScriptableObject
{
    [Header("Generator Settings")]
    public CloudArchetype Archetype = CloudArchetype.Sphere;
    public int Seed = 42;
    public float Density = 0.6f;
    public float Jitter = 0.3f;
    public float Clustering = 0.5f;

    [Header("Sphere Params")]
    public int CascadeDepth = 3;
    public int BumpsPerLevel = 24;
    public float ChildRatio = 30f;
    public float SizeVariation = 1.0f;
    public int ParentCount = 1;
    public float EllipsoidY = 50f;
    public float EllipsoidXZ = 100f;

    [Header("Size Range")]
    public SizeRange SizeRange = new SizeRange { Min = 5, Max = 20 };

    [Header("Parent Mesh (optional - для форм)")]
    public string ParentMeshPath;
}
```

**Struct SizeRange** взять из `CloudTypes.cs` generator7.0:
```csharp
[System.Serializable]
public class SizeRange
{
    public float Min = 5f;
    public float Max = 20f;
}
```

#### 1.2 CloudGeneratorAdapter (НОВОЕ)

Создать `Assets/_Project/Scripts/Core/Adapters/CloudGeneratorAdapter.cs`:

```csharp
public static class CloudGeneratorAdapter
{
    public static CloudData[] ConvertToCloudData(
        List<CloudSphere> spheres,
        Vector3 playerOffset,
        CloudMeshEntry[] meshEntries)
    {
        // 1. Создать CloudData[] размером spheres.Count
        // 2. Для каждой сферы:
        //    - Vector3 pos = new Vector3(sphere.X, sphere.Y, sphere.Z) + playerOffset
        //    - float scale = sphere.Radius * 2f ( diameter)
        //    - Quaternion rot = Random.rotation (сохранить variety)
        //    - int meshIndex = GetMeshIndexByArchetype(sphere.Archetype, meshEntries)
        //    - _clouds[i] = new CloudData { Matrix = Matrix4x4.TRS(pos, rot, scale), Scale = scale, MeshIndex = meshIndex }
        // 3. Вернуть CloudData[]
    }

    public static List<CloudSphere> GenerateFromPattern(
        CloudPatternConfig pattern,
        Vector3 position,
        float cloudSize)
    {
        // 1. Build CloudLayerConfig[] from pattern
        // 2. Call CloudGenerator.Generate(layers)
        // 3. Return List<CloudSphere>
    }

    private static int GetMeshIndexByArchetype(CloudArchetype archetype, CloudMeshEntry[] entries)
    {
        // Map archetype to mesh index
        // Sphere/Tree -> entries[0]
        // Column -> entries[1] if exists
        // Platform -> entries[2] if exists
        // Fallback to 0
    }
}
```

**ВАЖНО:** generator7.0 использует namespace `ProjectC.CloudGenerator`, убедиться что using добавлен.

#### 1.3 Modify NearCloudRenderer

В `NearCloudRenderer.cs` добавить:

```csharp
// NEW METHOD - принимает паттерн вместо random generation
public void GenerateFromPattern(CloudPatternConfig pattern, Vector3 playerPos)
{
    // 1. Generate spheres from pattern
    var spheres = CloudGeneratorAdapter.GenerateFromPattern(pattern, playerPos, CloudSize);

    // 2. Convert to CloudData[]
    _clouds = CloudGeneratorAdapter.ConvertToCloudData(spheres, playerPos, MeshEntries);

    // 3. _currentCount = spheres.Count (capped at CloudCount)
    _currentCount = Mathf.Min(spheres.Count, CloudCount);
}

// MODIFY existing Generate() to call new method with default pattern?
// ИЛИ оставить Generate() как fallback, GenerateFromPattern() для advanced
```

#### 1.4 Create Pattern Assets

Создать `Assets/_Project/Data/Clouds/Patterns/`:
- `Pattern_Upper_Cumulus.asset` — Sphere, CascadeDepth=3
- `Pattern_Middle_Cumulus.asset` — Sphere, CascadeDepth=4
- `Pattern_Lower_Stratocumulus.asset` — Platform, flatter

#### 1.5 Connect in CloudManager

В `CloudManager.Initialize()`:
```csharp
// Загрузить паттерны
_upperPattern = Resources.Load<CloudPatternConfig>("Clouds/Patterns/Pattern_Upper_Cumulus");
_middlePattern = Resources.Load<CloudPatternConfig>("Clouds/Patterns/Pattern_Middle_Cumulus");
_lowerPattern = Resources.Load<CloudPatternConfig>("Clouds/Patterns/Pattern_Lower_Stratocumulus");

// Вместо UpperLayer.Generate(playerPos) вызвать:
// UpperLayer.GenerateFromPattern(_upperPattern, playerPos)
```

---

## Тестирование — что проверять

### T1: Adapter unit tests
```csharp
[Test] public void CloudGeneratorAdapter_ConvertToCloudData_CorrectPositions()
{
    var spheres = new List<CloudSphere> {
        new CloudSphere { X=0, Y=0, Z=0, Radius=10, Archetype=CloudArchetype.Sphere }
    };
    var data = CloudGeneratorAdapter.ConvertToCloudData(spheres, Vector3.zero, meshEntries);
    Assert.AreEqual(1, data.Length);
    Assert.AreEqual(new Vector3(0,0,0), data[0].Matrix.GetColumn(3));
}
```

### T2: Integration test
```csharp
[Test] public void NearCloudRenderer_GenerateFromPattern_Renders()
{
    var pattern = Resources.Load<CloudPatternConfig>("Clouds/Patterns/Pattern_Upper_Cumulus");
    renderer.GenerateFromPattern(pattern, Vector3.zero);
    // Verify _clouds populated, _currentCount > 0
    // Visual: cauliflower-like sphere clusters
}
```

### T3: Wind test
```csharp
[Test] public void NearCloudRenderer_Wind_MovesClouds()
{
    WindManager.Instance.ApplyWindUpdate(Vector3.right, 10f);
    yield null;
    // Clouds should have moved right
}
```

---

## Отладка — что смотреть

**CloudGeneratorAdapter:**
- `spheres.Count` — должен быть < 5000 (MaxSphereCount limit)
- `CloudData[i].Matrix.GetColumn(3)` — позиция (должна быть в диапазоне от player)
- `MeshIndex` — должен соответствовать archetype

**NearCloudRenderer:**
- `_clouds` array заполнен после GenerateFromPattern()
- `_currentCount` соответствует spheres.Count
- `LateUpdate()` вызывает Graphics.DrawMeshInstanced

**Wind:**
- `WindManager.Instance.CurrentWindDirection` обновляется
- `SetWind()` вызывается из CloudManager.Update()

---

## Known Issues to Watch

1. **CloudGenerator namespace conflict** — генератор7.0 в `ProjectC.CloudGenerator`, CloudManager в `ProjectC.Core`
2. **#if UNITY_EDITOR** — CloudParentMesh.SampleSurface() editor-only, нужен RuntimeMeshSampler позже
3. **Seed not used** — CloudPatternConfig.Seed должен передаваться в DeterministicRandom
4. **CloudSphere.Depth → scale** — при рекурсии depth увеличивается, radius уменьшается (ChildRatio)

---

## Если что-то не работает

**Проблема: "CloudGenerator не найден"**
```csharp
using ProjectC.CloudGenerator;  // Добавить в using
// CloudGenerator.cs находится в namespace ProjectC.CloudGenerator
```

**Проблема: "spheres.Count = 0"**
- Проверить CloudLayerConfig.Seed — должен быть != 0
- Проверить Density parameter — слишком высокий threshold может отфильтровать все сферы
- Проверить MaxSphereCount limit

**Проблема: "Graphics.DrawMeshInstanced не рендерит"**
- Проверить _clouds array заполнен (_currentCount > 0)
- Проверить Matrix4x4 содержит валидные значения (не zero scale)
- Проверить material instancing enabled

---

## Следующая сессия (после Фазы 1)

После завершения Фазы 1, перейти к:
- **Фаза 2:** Storm → generator7.0 Column (StormGeneratorAdapter)
- **Фаза 3:** Wind Physics (CloudSpherePhysics)
- **Фаза 4:** Parent Mesh + World Automation

Подробности в `INTEGRATION_RESEARCH.md` Section 6.

---

## Коммит и пуш

После завершения работы:

```bash
git add -A
git commit -m "feat(clouds): CloudGeneratorAdapter for generator7.0 integration

- CloudPatternConfig ScriptableObject for pattern library
- CloudGeneratorAdapter: CloudSphere[] → CloudData[] conversion
- NearCloudRenderer.GenerateFromPattern(pattern) method
- Pattern assets for Upper/Middle/Lower layers
- Connected patterns in CloudManager.Initialize()

Phase 1 of generator7.0 integration"
git push
```
