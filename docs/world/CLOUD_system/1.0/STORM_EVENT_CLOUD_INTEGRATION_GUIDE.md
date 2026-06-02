# STORM/EVENT CLOUD INTEGRATION — ПОЛНАЯ ДОКУМЕНТАЦИЯ

**Дата:** 21 мая 2026 | **Status:** 🚀 Phase 1-4 Complete, ⚠️ Physics Testing Required

---

## ЧТО СДЕЛАНО

### ✅ Phase 1: StormCloudGenerator + CloudSpherePhysics (РАБОТАЕТ)

**Файлы:**
- `Assets/_Project/Scripts/Core/StormCloudGenerator.cs`
- `Assets/_Project/Scripts/Core/CloudSpherePhysics.cs`

**StormCloudGenerator:**
- Пул через `Dictionary<uint, Storm>`
- `SpawnStorm(stormId, position, pattern, intensity)` — создаёт storm object
- Интеграция с `generator7.0.Generate()`
- Создаёт GameObject на каждую сферу
- Добавляет Rigidbody (isKinematic) + CloudSpherePhysics

**CloudSpherePhysics:**
- `FixedUpdate()` — проверяет дистанцию до игрока
- `ApplyParting()` — applies impulse когда игрок близко
- SpringBack force для возврата сфер
- PartingCooldown для защиты от спама

**⚠️ НУЖНО ТЕСТИРОВАТЬ:** Parting визуально не проверен

---

### ✅ Phase 2: ServerStormManager + patternGUID (РАБОТАЕТ)

**Файлы:**
- `Assets/_Project/Scripts/Core/ServerStormManager.cs`
- `Assets/_Project/Scripts/Core/StormController.cs`
- `Assets/_Project/Prefabs/StormController.prefab`

**ServerStormManager:**
- `SpawnInitialStorms()` — спавнит `_maxStorms` штормов при старте
- `StormSpawnClientRpc(id, position, intensity, patternGUID)` — отправляет на клиенты
- Управляет движением штормов по ветру
- Синхронизирует позиции через `SyncStormStatesClientRpc`

**StormController:**
- Статический словарь `ClientControllers` для регистрации
- `Initialize(id, position, intensity, patternGUID)` — инициализация
- `LoadPatternByGUID()` — загрузка CloudLayerConfig по GUID (editor-only!)
- `Despawn()` — удаление шторма (НОВОЕ)

**⚠️ ВАЖНО:** StormController.prefab НЕ имеет NetworkObject!
- Причина: GlobalObjectIdHash конфликт (804704506)

---

### ✅ Phase 3: RuntimeMeshSampler (НОВОЕ — ЗАВЕРШЕНО)

**Файл:** `Assets/CloudGenerator/Generation/RuntimeMeshSampler.cs`

**Проблема:** `CloudParentMesh.SampleSurface()` работает только в Editor (`#if UNITY_EDITOR`)

**Решение:** Runtime аналог работает в runtime через `Resources.Load<Mesh>()`

**Методы:**
```csharp
// Surface sampling с weighted random triangle selection
RuntimeMeshSampler.SampleSurface(Mesh mesh, int pointCount = 2000)

// Scale, rotation, offset для sampled points
RuntimeMeshSampler.ApplyTransform(points, scale, rotation, offset)

// Загрузка mesh через Resources.Load()
RuntimeMeshSampler.LoadMeshFromResources(resourcePath)

// Combined — всё в одном
RuntimeMeshSampler.SampleMeshFromResources(resourcePath, pointCount, scale, rotation, offset)
```

**Требования:**
- Mesh должен лежать в `Assets/Resources/`
- Путь указывается БЕЗ расширения и БЕЗ `Assets/Resources/`
- Пример: `Assets/Resources/Meshes/CloudBase.mesh` → path = `Meshes/CloudBase`

**Изменения:**
- `CloudGenerator.cs` — удалён `#if UNITY_EDITOR` блок, теперь использует `RuntimeMeshSampler`

---

### ✅ Phase 4: EventCloud Integration (НОВОЕ — ЗАВЕРШЕНО)

**Файл:** `Assets/_Project/Scripts/Core/EventCloud.cs`

**EventCloud — клиентский компонент для event-driven штормов:**

```csharp
public class EventCloud : MonoBehaviour
{
    public void SpawnEventCloud()      // спавн event cloud
    public void DespawnEventCloud()    // despawn
    public void SetEventId(string)     // установить event ID
    public void SetPattern(CloudLayerConfig) // установить паттерн
    public void SetIntensity(float)     // установить интенсивность
    public void SetParentMeshPath(string) // parent mesh для Phase 3
}
```

**ServerStormManager новые методы:**
```csharp
// Server API — спавн event cloud
public void TriggerEventCloud(Vector3 position, CloudLayerConfig pattern, float intensity, string eventId)

// Server API — удаление event cloud
public void RemoveEventCloud(ushort stormId)

// Events для внешних систем
public event System.Action<ushort, Vector3, float, string> OnEventCloudSpawnRequested;
public event System.Action<ushort> OnEventCloudRemoveRequested;
```

**ClientRpc:**
- `EventCloudSpawnClientRpc(id, worldPos, intensity, patternGUID, eventId)`
- `EventCloudRemoveClientRpc(stormId)`

---

## АРХИТЕКТУРА

```
Server                                    Client
────────────────────────────────────────────────────────────────────
ServerStormManager                        StormController (local MonoBehaviour)
│                                          │
├── OnNetworkSpawn()                      │
│   └── SpawnInitialStorms()               │
│       └── SpawnStorm() × N              │
│           └── StormData{pos, guid}      │
│                                          │
└── StormSpawnClientRpc() ────────────────┼── Instantiate(prefab)
     (id, position, intensity, guid)        │
                                            │
                                            └── StormController.Initialize()
                                                  │
                                                  └── _cloudGenerator.SpawnStorm()
                                                        │
                                                        └── generator7.0.Generate()
                                                              │
                                                              └── ~48-500 spheres

EVENT CLOUD FLOW:
Server                                   Client
────────────────────────────────────────────────────────────────────
TriggerEventCloud() ──────────────────►  EventCloudSpawnClientRpc()
     │                                       │
     │                                       └── EventCloud.Initialize()
     │                                             │
     │                                             └── EventCloud.SpawnEventCloud()
     │                                                   │
     │                                                   └── StormCloudGenerator.SpawnStorm()
```

---

## ФАЙЛЫ И СТАТУС

| Файл | Назначение | Status |
|------|------------|--------|
| `CloudGenerator.cs` | generator7.0 | ✅ Работает |
| `CloudTypes.cs` | Типы данных | ✅ Работает |
| `StormCloudGenerator.cs` | Storm spawning | ✅ Работает |
| `CloudSpherePhysics.cs` | Parting physics | ⚠️ Нужен тест |
| `StormController.cs` | Storm management | ✅ Работает |
| `ServerStormManager.cs` | Server-side control | ✅ Работает |
| `StormController.prefab` | Prefab | ✅ БЕЗ NetworkObject |
| `CloudLayerConfig.cs` | Паттерн конфиг | ✅ Работает |
| `RuntimeMeshSampler.cs` | Runtime mesh sampling | ✅ НОВЫЙ |
| `EventCloud.cs` | Event cloud component | ✅ НОВЫЙ |

---

## ⚠️ ЧТО ОСТАЛОСЬ (PENDING)

### 1. ТЕСТ PHYSICS (САМЫЙ ВАЖНЫЙ)

**Проблема:** Parting physics работает в коде, но НЕ проверен визуально

**Нужно:**
1. Уменьшить паттерн: Floors=8, RingsPerFloor=6 (~48 сфер)
2. Запустить игру, нажать T для спавна
3. Пролететь сквозь шторм
4. Проверить что сферы расступаются

**Настройка CloudSpherePhysics:**
```csharp
PartingDistance: 30-100m  // увеличь если не работает
PartingStrength: 50       // увеличь если сферы не расступаются
SpringBack: true/false   // возвращать ли сферы обратно
SpringK: 8               // жёсткость пружины
```

**Проверить тег игрока в CloudSpherePhysics:94:**
```csharp
GameObject.FindGameObjectWithTag("Player"); // должен быть "Player"
```

---

### 2. СОЗДАТЬ ПРОМЕЖУТОЧНЫЕ ПАТТЕРНЫ

Сейчас только один паттерн. Нужны пресеты:
```
Light:    Floors=8,  Rings=6,  (~48 spheres)
Medium:   Floors=12, Rings=8,  (~200 spheres)
Heavy:    Floors=16, Rings=12, (~500+ spheres)
```

---

### 3. RUNTIME ЗАГРУЗКА ПАТТЕРНОВ

`LoadPatternByGUID()` использует `AssetDatabase` — только Editor.

**Решения:**
- **Addressables** (правильное) — загрузка по адресу
- **Resources.Load()** — положить CloudLayerConfig в Resources

---

### 4. OPTIMIZATION

12,000+ GameObject'ов при 5 штормах — тяжело.

**Возможные решения:**
- Instanced rendering (GPU instancing)
- LOD для сфер
- Уменьшение количества сфер
- Batching

---

### 5. LIGHTNING EFFECTS

StormController имеет `TriggerLightning()` но VFX не подключен.

**Нужно:**
- Добавить ParticleSystem в префаб
- Привязать к `_lightningVFX`

---

## НАСТРОЙКА В EDITOR

### World/Scene Dimensions:
- **24 scenes** total world
- **Each scene: 80000 x 80000 units**
- Storm spawn range: **5000-35000 units** from scene center (within bounds)

### ServerStormManager:
```
_maxStorms: 2-5
_spawnMinDistance: 5000   // min distance from scene center
_spawnMaxDistance: 35000  // max distance from scene center (within 80000x80000 scene)
_baseAltitude: 1200
_altitudeVariation: 500
_stormPatterns: [Storm_Column_light.asset]
_useRandomPattern: true
_stormControllerPrefab: StormController.prefab
```

### TestStormSpawner:
```
useRandomPosition: true (default)
spawnMinDistance: 5000
spawnMaxDistance: 35000
baseAltitude: 1200
altitudeVariation: 500
```

### Storm Pattern (для теста physics):
```
Archetype: Column
ColumnParams:
  Floors: 8
  RingsPerFloor: 6
  BaseRadius: 150
  TopRadius: 250
  Wobble: 0.3
```

### EventCloud:
```
_eventPattern: Storm_Column_light.asset (или свой)
_parentMeshResourcePath: Meshes/CloudBase (опционально)
_autoStart: false
_intensity: 1.0
```

---

## HOTKEYS ДЛЯ ТЕСТА

| Клавиша | Действие |
|---------|----------|
| T | Spawn тестовый шторм |
| Y | Despawn storm 1 |
| U | Despawn all storms |

---

## ПЛАН НА БУДУЩЕЕ

1. ⚠️ **ТЕСТ PHYSICS** — главная задача
2. ⏳ Проверить parting визуально
3. ⏳ Создать Medium/Heavy паттерны
4. ⏳ Runtime pattern loading (Addressables)
5. ⏳ Lightning effects
6. ⏳ Performance optimization (instancing)

---

**Status:** 🚀 Phase 1-4 complete, physics testing and optimization pending