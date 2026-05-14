# STORM/EVENT CLOUD INTEGRATION — NEXT SESSION GUIDE

**Дата:** 14 мая 2026 | **Status:** 📋 Ready for Implementation
**Автор:** Deep Analysis + User Clarification

---

## 1. ЧТО ХОТИМ ПОЛУЧИТЬ

### Цель
generator7.0 для создания интерактивных storm/event облаков:
- Сервер передаёт позицию + паттерн (GUID) + интенсивность
- Все клиенты генерируют ИДЕНТИЧНЫЙ storm локально
- При пролёте игрока сквозь облако — сферы parting (физика)
- Parting — client-side only, сервер не синхронизирует

### Key Features
1. **Storm presets** — Light/Medium/Heavy Column patterns (3+ вариаций)
2. **Parent mesh projection** — "череп" летит на игрока, сферы parting
3. **Event clouds** — триггер в игре → сервер → все клиенты видят событие
4. **Non-repeating** — generator7.0 создаёт бесконечные вариации одного паттерна

### Разделение систем

| Система | Подход | Физика |
|---------|--------|--------|
| Upper/Middle/Lower layers | `Graphics.DrawMeshInstanced` | Нет (просто следуют ветру) |
| Storm/EventCloud | **GameObject per sphere** | Yes (parting при пролёте) |

---

## 2. ПОЧЕМУ ПРОШЛАЯ ИНТЕГРАЦИЯ НЕ УДАЛАСЬ

### 2.1 Фундаментальное несоответствие архитектур

**generator7.0:**
- Создаёт ОДИН cluster в локальных координатах (0,0,0)
- Возвращает `List<CloudSphere>` — данные о позициях сфер
- Editor-only для ParentMeshPath
- Не создан для runtime позиционирования

**NearCloudRenderer:**
- Ожидает 80-120 **НЕЗАВИСИМЫХ** cloud instances
- Каждый instance — это cloudData[i] с Matrix, Scale, MeshIndex
- Использует `Graphics.DrawMeshInstanced` — batching всех сфер в один draw call

**Проблема:** generator7.0 создаёт cluster (100-500 сфер как одна структура), а NearCloudRenderer ожидает много независимых instances. Это разные use cases.

### 2.2 Попытка была натянута

```
CloudGeneratorAdapter.ConvertToCloudData()
     ↓
Пытались конвертировать CloudSphere[] → CloudData[]
     ↓
Для cluster из 200 сфер — создать 200 cloudData[]
     ↓
Все сферы ОКОЛО ОДНОЙ позиции (origin + playerOffset)
     ↓
Результат: 1 облако вместо 80
```

### 2.3 Parting physics невозможен с instancing

`Graphics.DrawMeshInstanced` — это batching. Все сферы рендерятся как одна сущность. Нельзя сделать так чтобы каждая сфера независимо реагировала на игрока.

**Решение:** Storm/EventCloud должны использовать **реальные GameObject'ы** с Rigidbody, НЕ instancing.

---

## 3. АРХИТЕКТУРА STORM/EVENT CLOUD

### 3.1 Компоненты

```
StormCloudGenerator (MonoBehaviour)
├── SpawnStorm(position, patternGUID, intensity)
├── DespawnStorm(stormId)
├── ActiveStorms[] — пул storm objects
│
Storm (GameObject)
├── WorldPosition (from server)
├── PatternConfig (CloudLayerConfig asset)
├── Intensity (float)
├── SphereContainer (GameObject — parent for spheres)
│   │
│   └── CloudSphere_{i} (GameObject per sphere)
│       ├── MeshFilter (sphere mesh)
│       ├── MeshRenderer (cloud material)
│       ├── Rigidbody (isKinematic = true)
│       └── CloudSpherePhysics (component for parting)
```

### 3.2 Server → Client Communication

```
Server:
- StormEvent { stormId, worldPosition, patternGUID, intensity }
- Broadcasts via ClientRpc to all clients

Client:
1. Receive StormEvent
2. Load CloudLayerConfig by GUID
3. Call StormCloudGenerator.SpawnStorm(pos, config, intensity)
4. generator7.0.Generate(config) → List<CloudSphere>
5. Create GameObject per sphere
6. Attach to Storm.SphereContainer
```

### 3.3 Parting Physics (Client-Side Only)

```
Игрок летит через Storm
     ↓
CloudSpherePhysics.Update():
- Check distance to player
- If close enough (e.g., < 50m):
  - Calculate direction away from player
  - Apply impulse to Rigidbody
  - Set isKinematic = false
     ↓
After player passes:
- Spring-back force (optional)
- Or simply scatter and leave
```

**Важно:** Parting — client-side only. Сервер НЕ синхронизирует позиции individual spheres.

### 3.4 Пул storm objects

```
StormCloudGenerator
├── MaxActiveStorms = configurable (default 5)
├── ActiveStorms = Dictionary<stormId, Storm>
│
├── SpawnStorm() — создаёт новый storm
├── DespawnStorm() — удаляет storm по Id
├── DespawnAll() — очистка при выходе из игры
```

---

## 4. ФАЙЛЫ — НАЗНАЧЕНИЕ И ЧТО ТРОГАТЬ

### 4.1 generator7.0 (НЕ ТРОГАТЬ основной код)

**Путь:** `Assets/CloudGenerator/CloudGenerator_v7.0/CloudGenerator_v7.0/`

| Файл | Назначение | Трогать? |
|------|------------|----------|
| `CloudGenerator.cs` | Генерация сфер по CloudLayerConfig | ❌ Нет |
| `CloudTypes.cs` | CloudSphere, CloudLayerConfig, SizeRange, etc. | ❌ Нет |
| `CloudMath.cs` | Perlin, FBM, Worley, Fibonacci sphere | ❌ Нет |

**Использование:**
```csharp
using ProjectC.CloudGenerator;

var spheres = CloudGenerator.Generate(List<CloudLayerConfig> layers);
// spheres — это List<CloudSphere> с данными
```

### 4.2 CloudLayerConfig (УЖЕ СУЩЕСТВУЕТ)

**Путь:** `Assets/_Project/Scripts/Core/CloudLayerConfig.cs`

```csharp
[CreateAssetMenu(fileName = "CloudLayerConfig", menuName = "Project C/Cloud Layer Config")]
public class CloudLayerConfig : ScriptableObject
{
    public CloudArchetype Archetype; // Sphere, Column, Platform, Tree
    public int Seed;
    public float Density;
    public int CascadeDepth;
    public int BumpsPerLevel;
    public float ChildRatio;
    // ... etc
}
```

**НЕ СОЗДАВАТЬ новый CloudPatternConfig.** Использовать существующий CloudLayerConfig.

### 4.3 StormCloudGenerator (ПЕРЕПИСАТЬ ПОЛНОСТЬЮ)

**Путь:** `Assets/_Project/Scripts/Core/StormCloudGenerator.cs`

**Текущий Status:** Не работает, логика недоделана

**Новый функционал:**
```csharp
public class StormCloudGenerator : MonoBehaviour
{
    [Header("Pool Settings")]
    public int MaxActiveStorms = 5;

    private Dictionary<uint, Storm> _activeStorms = new Dictionary<uint, Storm>();

    public void SpawnStorm(uint stormId, Vector3 position, CloudLayerConfig pattern, float intensity)
    {
        // 1. Проверить лимит пула
        // 2. Создать корневой GameObject "Storm_{stormId}"
        // 3. Вызвать generator7.0: CloudGenerator.Generate()
        // 4. Создать GameObject per sphere
        // 5. Добавить CloudSpherePhysics component
        // 6. Добавить в _activeStorms
    }

    public void DespawnStorm(uint stormId)
    {
        // Удалить storm из пула
    }

    public void DespawnAll()
    {
        // Очистить все active storms
    }
}
```

### 4.4 CloudSpherePhysics (НОВЫЙ КОМПОНЕНТ)

**Путь:** `Assets/_Project/Scripts/Core/CloudSpherePhysics.cs`

```csharp
public class CloudSpherePhysics : MonoBehaviour
{
    public Vector3 BasePosition;
    public float Radius;
    public float PartingStrength = 10f;
    public bool SpringBack = true;
    public float SpringK = 5f;
    public float Damping = 0.9f;

    private Rigidbody _rb;
    private Vector3 _displacement;

    void Update()
    {
        // 1. Проверить расстояние до игрока
        // 2. Если близко — ApplyParting()
        // 3. Если SpringBack — ApplySpringBack()
        // 4. Синхронизировать позицию GameObject
    }

    public void ApplyParting(Vector3 fromDirection, float strength)
    {
        // Rigidbody.AddForce() в направлении fromDirection
    }

    public void ApplySpringBack()
    {
        // F = -k * displacement
    }
}
```

### 4.5 NearCloudRenderer (НЕ ТРОГАТЬ)

**Путь:** `Assets/_Project/Scripts/Core/NearCloudRenderer.cs`

**Status:** Работает ✅

**Не трогать** потому что:
- Upper/Middle/Lower слои не требуют physics
- Инстансинг работает корректно
- Wind integration настроена

### 4.6 CloudManager (НЕ ТРОГАТЬ для начала)

**Путь:** `Assets/_Project/Scripts/Core/CloudManager.cs`

**Status:** Работает ✅

Пока не трогать. StormCloudGenerator будет работать отдельно.

### 4.7 ServerStormManager (ПРОВЕРИТЬ)

**Путь:** `Assets/_Project/Scripts/Core/ServerStormManager.cs`

**Нужно:**
- Проверить отправляет ли сервер patternGUID
- Проверить что ClientRpc работает корректно

---

## 5. ПЛАН РЕАЛИЗАЦИИ

### ФАЗА 1: StormCloudGenerator (Core)

**Задачи:**
1. [ ] Переписать StormCloudGenerator.cs полностью
2. [ ] Создать пул storms (Dictionary<uint, Storm>)
3. [ ] Реализовать SpawnStorm(position, pattern, intensity)
4. [ ] Интегрировать generator7.0.Generate()
5. [ ] Создание GameObject per sphere

**Входные данные:**
- `CloudLayerConfig` pattern (Asset GUID от сервера)
- `Vector3` position (world position от сервера)
- `float` intensity (0.0 - 1.0)

**Выход:**
- Storm GameObject с child spheres
- Spheres созданы через generator7.0

**Проверка:**
```
Server: создать storm с паттерном Column_Heavy
Client 1: видит storm в правильном месте ✓
Client 2: видит storm в том же месте ✓
Все видят одинаковую структуру сфер ✓
```

### ФАЗА 2: CloudSpherePhysics (Parting)

**Задачи:**
1. [ ] Создать CloudSpherePhysics.cs
2. [ ] Добавить Rigidbody isKinematic
3. [ ] Реализовать ApplyParting()
4. [ ] (Опционально) SpringBack

**Проверка:**
```
Игрок летит сквозь storm
Сферы разлетаются в стороны ✓
После пролёта сферы остаются разбросанными (или возвращаются)
```

### ФАЗА 3: EventCloud (Parent Mesh)

**Задачи:**
1. [ ] Загрузить меш "череп" в ассеты
2. [ ] Создать CloudLayerConfig с ParentMeshPath
3. [ ] Генерация сфер по поверхности меша
4. [ ] Trigger события через сервер

**Проверка:**
```
Сервер отправляет: EventCloud с черепом
Все клиенты видят: сферы по поверхности черепа
Игрок летит через череп: сферы parting
```

### ФАЗА 4: ServerStormManager (Server-Side)

**Задачи:**
1. [ ] Добавить patternGUID в StormEvent
2. [ ] ClientRpc передаёт GUID
3. [ ] Синхронизация Intensity

---

## 6. КАК ИСПОЛЬЗОВАТЬ GENERATOR7.0

### 6.1 Базовый вызов

```csharp
using ProjectC.CloudGenerator;

public List<CloudSphere> GenerateStormSpheres(CloudLayerConfig config, Vector3 offset)
{
    var layers = new List<CloudLayerConfig>();
    config.YOffset = 0f; // Отключаем внутренний offset
    layers.Add(config);

    var spheres = CloudGenerator.Generate(layers);

    // Добавить offset к позициям
    foreach (var sphere in spheres)
    {
        sphere.X += offset.x;
        sphere.Y += offset.y;
        sphere.Z += offset.z;
    }

    return spheres;
}
```

### 6.2 CloudSphere структура

```csharp
public class CloudSphere
{
    public float X, Y, Z;       // Позиция
    public float Radius;        // Размер сферы
    public int Depth;           // Глубина каскада (0 = parent)
    public float Density;       // Плотность (для visual)
    public CloudArchetype Archetype; // Sphere/Column/Platform/Tree
    public int LayerIndex;      // Индекс слоя
    public bool CutByCondensation;
}
```

### 6.3 Создание GameObject per sphere

```csharp
public void CreateStormSpheres(List<CloudSphere> spheres, GameObject container, Material cloudMat)
{
    var sphereMesh = CreateDefaultSphereMesh();

    foreach (var sphere in spheres)
    {
        var go = new GameObject($"StormSphere");
        go.transform.SetParent(container.transform);
        go.transform.position = new Vector3(sphere.X, sphere.Y, sphere.Z);

        go.AddComponent<MeshFilter>().mesh = sphereMesh;
        go.AddComponent<MeshRenderer>().material = cloudMat;

        var rb = go.AddComponent<Rigidbody>();
        rb.isKinematic = true;

        var physics = go.AddComponent<CloudSpherePhysics>();
        physics.Initialize(sphere.Radius);
    }
}
```

---

## 7. СОЗДАНИЕ ПАТТЕРНОВ

### 7.1 Где создавать

**Через Unity Editor:**
1. Right-click в Project
2. Create → Project C → Cloud Layer Config
3. Named: `Storm_Column_Light`, `Storm_Column_Medium`, `Storm_Column_Heavy`
4. Заполнить параметры:
   - **Archetype:** Column
   - **Seed:** 12345 (разный для variety)
   - **CascadeDepth:** 3-4
   - **BumpsPerLevel:** 20-30
   - **ChildRatio:** 25-35
   - **ColumnParams:**
     - Floors: 8-16
     - RingsPerFloor: 6-12
     - BaseRadius: 150-300
     - TopRadius: 250-600
     - Wobble: 0.2-0.4

### 7.2 Параметры для Column

```
Light:    Floors=8,  Rings=6,  BaseR=150, TopR=250
Medium:   Floors=12, Rings=8,  BaseR=200, TopR=400
Heavy:    Floors=16, Rings=12, BaseR=300, TopR=600
```

### 7.3 Parent Mesh (EventCloud)

1. Import mesh (e.g., skull.fbx) в Assets
2. Create CloudLayerConfig
3. Set:
   - **ParentMeshPath:** "Assets/Path/To/skull.fbx"
   - **Archetype:** Sphere (для projection на поверхность)
4. generator7.0 создаст сферы по поверхности меша

---

## 8. ИЗВЕСТНЫЕ ОГРАНИЧЕНИЯ

### 8.1 generator7.0 Editor-only

`CloudParentMesh.SampleSurface()` работает только в Editor (#if UNITY_EDITOR).

**Для Runtime:**
- Либо убрать ParentMeshPath из CloudLayerConfig
- Либо создать RuntimeMeshSampler (позже, не в этой фазе)

### 8.2 MaxSphereCount = 5000

Dense patterns могут превысить лимит. При генерации проверить count.

### 8.3 Seed для variety

Одинаковый Seed = одинаковый результат. Для разных storm использовать разный Seed.

---

## 9. REFERENCE FILES

| Файл | Назначение | Status |
|------|------------|--------|
| `Assets/CloudGenerator/.../CloudGenerator.cs` | generator7.0 | ✅ Работает, не трогать |
| `Assets/CloudGenerator/.../CloudTypes.cs` | Типы данных | ✅ Работает |
| `Assets/_Project/Scripts/Core/CloudLayerConfig.cs` | Паттерн пресет | ✅ Существует |
| `Assets/_Project/Scripts/Core/StormCloudGenerator.cs` | Storm manager | ❌ Переписать |
| `Assets/_Project/Scripts/Core/NearCloudRenderer.cs` | Простые облака | ✅ Работает, не трогать |
| `Assets/_Project/Scripts/Core/CloudManager.cs` | Cloud manager | ✅ Работает, не трогать |
| `Assets/_Project/Scripts/Core/WindManager.cs` | Wind system | ✅ Работает |
| `Assets/_Project/Scripts/Core/ServerStormManager.cs` | Server-side | ⚠️ Проверить/допилить |

---

## 10. НАЧАЛЬНЫЙ ПЛАН ДЛЯ СЕССИИ

```
1. Создать CloudSpherePhysics.cs
   - Rigidbody isKinematic
   - ApplyParting() method
   
2. Переписать StormCloudGenerator.cs
   - SpawnStorm() method
   - Пул storms
   - Интеграция generator7.0
   
3. Создать тестовый CloudLayerConfig
   - Archetype = Column
   - Простой preset для теста
   
4. Протестировать локально
   - Storm появляется в правильном месте
   - Структура сфер соответствует паттерну
   - Spheres созданы как отдельные GameObject
   
5. Проверить ServerStormManager
   - Передаёт ли patternGUID
   - ClientRpc работает
```

---

## 11. ВАЖНО

### Что НЕ ДЕЛАТЬ

- ❌ НЕ пытаться интегрировать generator7.0 с NearCloudRenderer
- ❌ НЕ использовать `Graphics.DrawMeshInstanced` для Storm
- ❌ НЕ создавать новый CloudPatternConfig (использовать CloudLayerConfig)
- ❌ НЕ удалять/модифицировать NearCloudRenderer

### Что ДЕЛАТЬ

- ✅ StormCloudGenerator = GameObject per sphere подход
- ✅ CloudSpherePhysics для parting
- ✅ Пул storm objects
- ✅ generator7.0 для генерации данных (не для рендеринга напрямую)

---

**Status:** 📋 Documentation Complete — Ready for Implementation