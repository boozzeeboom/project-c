# STORM/EVENT CLOUD INTEGRATION — IMPLEMENTATION LOG

**Дата:** 19 мая 2026 | **Status:** ✅ Phase 1 Complete
**Автор:** Claude Code Assistant

---

## 1. ЧТО СДЕЛАНО

### 1.1 CloudSpherePhysics.cs (НОВЫЙ)

**Путь:** `Assets/_Project/Scripts/Core/CloudSpherePhysics.cs`

**Назначение:** Физика parting — сферы разлетаются при пролёте игрока сквозь облако.

**Поля:**
- `Radius` — радиус сферы (для расчёта proximity)
- `PartingStrength = 50f` — сила импульса
- `PartingDistance = 30f` — дистанция срабатывания
- `SpringBack = true` — возвращаться на место
- `SpringK = 8f` — жёсткость пружины
- `Damping = 0.92f` — затухание

**Логика:**
```
Update():
  1. Найти игрока по тегу "Player"
  2. Если distance < PartingDistance → ApplyParting()
  3. Если SpringBack → применить силу пружины к смещённым сферам
  4. При parting: isKinematic = false, ragdoll physics
```

**API:**
- `Initialize(float radius)` — вызывается из StormCloudGenerator при создании

---

### 1.2 CloudLayerConfig.cs (РАСШИРЕН)

**Путь:** `Assets/_Project/Scripts/Core/CloudLayerConfig.cs`

**Добавлены поля для generator7.0:**

```csharp
[Header("Generator v7.0 Fields")]
public CloudArchetype archetype;          // Sphere/Column/Platform/Tree
public int generatorSeed;               // Сид для детерминированной генерации
public float jitter;                    // Джиттер позиций
public float clustering;                 // Кластеризация
public float positionVariation;          // Допвариация позиций

[Header("Sphere Archetype")]
public int cascadeDepth = 3;
public int bumpsPerLevel = 24;
public float childRatio = 30f;
...

[Header("Column Archetype")]
public ColumnParams columnParams;        // Height, BaseRadius, TopRadius, Floors, RingsPerFloor, Wobble

[Header("Parent Mesh (EventCloud)")]
public string parentMeshPath;            // Для "черепа" (пока Editor-only)
```

---

### 1.3 StormCloudGenerator.cs (ПЕРЕПИСАН)

**Путь:** `Assets/_Project/Scripts/Core/StormCloudGenerator.cs`

**Новая архитектура:**
- Пул storms: `Dictionary<uint, Storm>` (max 5 активных)
- Метод `SpawnStorm(stormId, position, pattern, intensity)`:
  1. Проверяет лимит пула, удаляет oldest если нужно
  2. Создаёт корень `Storm_{stormId}`
  3. Вызывает `generator7.0.CloudGenerator.Generate()` с конвертацией CloudLayerConfig → generator format
  4. Для КАЖДОЙ сферы создаёт отдельный GameObject с:
     - MeshFilter + MeshRenderer
     - Rigidbody (isKinematic = true)
     - CloudSpherePhysics
  5. Добавляет в пул

**Ключевое отличие от старой версии:**
- Старая: `Graphics.DrawMeshInstanced` — все сферы в один draw call, batching
- Новая: **GameObject per sphere** — индивидуальная физика parting, независимое управление

---

### 1.4 StormController.cs (ФИКС)

**Путь:** `Assets/_Project/Scripts/Core/StormController.cs`

**Исправления:**
- Удалён вызов `Initialize(cloudMaterial)` — StormCloudGenerator больше не имеет этого метода
- Добавлен вызов `_cloudGenerator.SpawnStorm()` в методе `Initialize(id, worldPos, intensity)`
- StormController теперь создаёт storm на стороне клиента через StormCloudGenerator

---

## 2. АРХИТЕКТУРА ВЗАИМОДЕЙСТВИЯ

```
ServerStormManager (Server)
  │
  ├─ SpawnStorm() → StormSpawnClientRpc(id, worldPos, intensity)
  │
  ▼
StormController (Client)
  │
  ├─ Initialize(id, worldPos, intensity)
  │
  ├─ _cloudGenerator.SpawnStorm(id, worldPos, pattern, intensity)
  │
  ▼
StormCloudGenerator
  │
  ├─ CloudGenerator.Generate(layers) → List<CloudSphere>
  │
  ├─ for each sphere:
  │     Create GameObject
  │     AddComponent<CloudSpherePhysics>
  │     AddComponent<Rigidbody> (isKinematic)
  │
  ▼
Storm (GameObject with sphere children)
  │
  ▼
CloudSpherePhysics.Update()
  │
  ├─ Detect player proximity
  ├─ Apply parting impulse (if close)
  └─ Spring-back (if enabled)
```

---

## 3. ТЕСТИРОВАНИЕ — ПОДРОБНАЯ ИНСТРУКЦИЯ

### 3.1 Подготовка сцены

**Шаг 1.** Открыть сцену с WindManager и CloudManager.

**Шаг 2.** Убедиться что в сцене есть:
- WindManager (должен быть в сцене по умолчанию)
- CloudManager (если используется)

**Шаг 3.** Проверить есть ли StormCloudGenerator в сцене. Если нет:
1. Создать пустой GameObject
2. Переименовать в "StormCloudGenerator"
3. Добавить компонент `StormCloudGenerator`

---

### 3.2 Создание тестового CloudLayerConfig

**Шаг 1.** В Project window:
1. Right-click на папку `Assets/_Project/Art/`
2. Create → Project C → Cloud Layer Config
3. Назвать `Storm_Column_Light`

**Шаг 2.** Заполнить параметры:

```
Inspector → Cloud Layer Config:

【Тип слоя】
Layer Type: Storm

【Generator v7.0 Fields】
Archetype: Column
Generator Seed: 12345 (любое число, разное для разных вариаций)
Jitter: 0.3
Clustering: 0.5
Position Variation: 0.5

【Sphere Archetype】 (оставить по умолчанию)
Cascade Depth: 3
Bumps Per Level: 24
...

【Column Archetype】 (КЛЮЧЕВОЕ)
Height: 300
Base Radius: 150
Top Radius: 250
Floors: 8
Rings Per Floor: 6
Wobble: 0.3

【Size Range】
Min: 5
Max: 20
```

**Шаг 3.** Сохранить: File → Save Project или Ctrl+S

---

### 3.3 Настройка StormCloudGenerator

**Шаг 1.** Выбрать StormCloudGenerator в сцене.

**Шаг 2.** В Inspector:

```
【Pool Settings】
Max Active Storms: 5

【Cloud Material】
(оставить пустым — создастся default)

【Default Storm Pattern】
(присвоить наш CloudLayerConfig)
  → Click circle selector
  → Find "Storm_Column_Light"
  → Click to select
```

---

### 3.4 Добавление тестового скрипта

**Шаг 1.** Создать тестовый скрипт для ручного спавна storm:

В любой папке Create → C# Script → назвать `TestStormSpawner`

```csharp
using UnityEngine;
using ProjectC.Core;

public class TestStormSpawner : MonoBehaviour
{
    public CloudLayerConfig testPattern;
    public uint testStormId = 1;
    public Vector3 testPosition = new Vector3(0, 1200, 0);

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.T))
        {
            var generator = FindAnyObjectByType<StormCloudGenerator>();
            if (generator != null && testPattern != null)
            {
                generator.SpawnStorm(testStormId, testPosition, testPattern, 1f);
                Debug.Log($"[Test] Spawned storm {testStormId} at {testPosition}");
            }
        }

        if (Input.GetKeyDown(KeyCode.Y))
        {
            var generator = FindAnyObjectByType<StormCloudGenerator>();
            if (generator != null)
            {
                generator.DespawnStorm(testStormId);
                Debug.Log($"[Test] Despawned storm {testStormId}");
            }
        }

        if (Input.GetKeyDown(KeyCode.U))
        {
            var generator = FindAnyObjectByType<StormCloudGenerator>();
            if (generator != null)
            {
                generator.DespawnAll();
                Debug.Log($"[Test] Despawned ALL storms");
            }
        }
    }
}
```

**Шаг 2.** Добавить на пустой GameObject "TestScripts":
1. Создать пустой GameObject
2. Добавить компонент `TestStormSpawner`
3. Присвоить `Test Pattern` = наш `Storm_Column_Light`

---

### 3.5 Запуск и тестирование

**Шаг 1.** Play mode.

**Шаг 2.** Нажать **T** — спавн storm по позиции (0, 1200, 0).

**Ожидаемый результат:**
```
[StormCloudGenerator] Awake...
[StormCloudGenerator] SpawnStorm(1, (0.0, 1200.0, 0.0), Storm_Column_Light, 1.0)
[StormController] Initialized storm 1 at (0.0, 1200.0, 0.0)
```

**Шаг 3.** Осмотреться — видим storm в небе.

**Шаг 4.** Подлететь близко к storm (< 30m к сферам).

**Ожидаемый результат:**
```
[CloudSpherePhysics] Player close! Applying parting...
```
Сферы должны разлетаться в стороны от игрока.

**Шаг 5.** Нажать **Y** — деспавн storm.

**Шаг 6.** Нажать **U** — деспавн всех storms.

---

### 3.6 Дополнительные тесты

**Тест с разными паттернами:**

Создать `Storm_Column_Medium`:
- Generator Seed: 54321
- Height: 400
- Base Radius: 200
- Top Radius: 400
- Floors: 12
- Rings Per Floor: 8

Создать `Storm_Column_Heavy`:
- Generator Seed: 99999
- Height: 500
- Base Radius: 300
- Top Radius: 600
- Floors: 16
- Rings Per Floor: 12

**Тест Spring Back:**

В CloudSpherePhysics:
1. `SpringBack = true` — сферы возвращаются
2. `SpringBack = false` — сферы остаются разбросанными

**Тест Parting Distance:**

Уменьшить `PartingDistance` до 10 — parting только когда очень близко.
Увеличить до 50 — parting с большего расстояния.

---

## 4. ИЗВЕСТНЫЕ ОГРАНИЧЕНИЯ

### 4.1 ParentMeshPath (EventCloud)

Работает только в Editor (#if UNITY_EDITOR). Для runtime нужен отдельный RuntimeMeshSampler (пока не реализован).

### 4.2 MaxSphereCount = 5000

Dense паттерны могут превысить лимит. При генерации проверять count.

### 4.3 Seed для variety

Одинаковый Seed = одинаковый результат. Для разных storm использовать разный Seed.

### 4.4 Material

StormCloudGenerator создаёт default URP Lit material. Для красивых облаков нужен custom shader с soft edges, transparency.

---

## 5. СЛЕДУЮЩИЕ ШАГИ

### Phase 2: ServerStormManager модификация

Добавить передачу `patternGUID` от сервера:
```csharp
[ClientRpc]
void StormSpawnClientRpc(ushort id, Vector3 worldPos, float intensity, string patternGUID)
```

Клиент загружает CloudLayerConfig по GUID и передаёт в SpawnStorm.

### Phase 3: EventCloud (Parent Mesh)

1. Импортировать mesh "череп" в Assets
2. Установить ParentMeshPath в CloudLayerConfig
3. Генерация сфер по поверхности меша

### Phase 4: Оптимизация

- Object pooling для sphere GameObjects
- LOD для distant spheres
- Batching renderer настройки

---

## 6. ФАЙЛЫ

| Файл | Изменения |
|------|-----------|
| `CloudSpherePhysics.cs` | **НОВЫЙ** — parting physics |
| `CloudLayerConfig.cs` | Расширен generator7.0 полями |
| `StormCloudGenerator.cs` | Переписан полностью |
| `StormController.cs` | Фикс для нового API |
| `TestStormSpawner.cs` | **НОВЫЙ** — тестовый скрипт |

---

**Status:** ✅ Phase 1 Complete — Ready for Testing