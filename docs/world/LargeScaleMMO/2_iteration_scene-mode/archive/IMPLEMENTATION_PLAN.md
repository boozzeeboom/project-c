# Implementation Plan: Scene-Based World System (30 × 79,999)

**Дата:** 28.04.2026
**Проект:** ProjectC_client
**Статус:** ПЛАН ГОТОВ К РЕАЛИЗАЦИИ

---

## Executive Summary

План реализации сценовой системы для MMO мира 650,000 × 650,000 единиц.
30 сцен размером 79,999 × 79,999 с 1,600 overlap для визуальной непрерывности.

**Ключевое решение:** СЦЕНОВАЯ СИСТЕМА РАБОТАЕТ ПОВЕРХ СУЩЕСТВУЮЩЕЙ ЧАНКОВОЙ СИСТЕМЫ, НЕ ВМЕСТО НЕЁ.

---

## Архитектура: Два Уровня

```
┌─────────────────────────────────────────────────────────────┐
│                    SCENE LAYER (80,000 × 80,000)            │
│  - WorldSceneManager загружает/выгружает Unity Scenes       │
│  - SceneID = (gridX, gridZ) для идентификации               │
│  - Preload триггеры на границах сцен                       │
│  - Overlap 1,600 единиц для seamless terrain               │
└────────────────────────────┬────────────────────────────────┘
                             │ использует
                             ▼
┌─────────────────────────────────────────────────────────────┐
│                    CHUNK LAYER (2,000 × 2,000)              │
│  - WorldStreamingManager (существующий)                   │
│  - ChunkLoader загружает чанки вокруг игрока                │
│  - ProceduralChunkGenerator генерирует горы/облака         │
│  - РАБОТАЕТ НЕИЗМЕННО внутри загруженных сцен              │
└─────────────────────────────────────────────────────────────┘
```

**Принцип:** Scene Layer управляет ЗАГРУЗКОЙ сцен, Chunk Layer управляет КОНТЕНТОМ внутри сцен.

---

## Параметры мира

| Параметр | Значение |
|----------|----------|
| Мир | 650,000 × 650,000 единиц |
| Размер сцены | 79,999 × 79,999 единиц |
| Overlap зона | 1,600 единиц (2%) |
| Сетка сцен | 8 × 8 = 64 сцен максимум |
| Активных сцен | ~9 (3×3 вокруг игрока + буфер) |
| Размер чанка | 2,000 × 2,000 единиц |
| Чанков на сцену | 40 × 40 = 1,600 чанков |

---

## Структура данных

### SceneID

```csharp
[Serializable]
public struct SceneID : IEquatable<SceneID>, INetworkSerializable
{
    public int GridX;
    public int GridZ;

    public const float SCENE_SIZE = 79999f;
    public const float OVERLAP_SIZE = 1600f;

    public SceneID(int gridX, int gridZ)
    {
        GridX = gridX;
        GridZ = gridZ;
    }

    // Мировые координаты origin этой сцены
    public Vector3 WorldOrigin => new Vector3(GridX * SCENE_SIZE, 0, GridZ * SCENE_SIZE);

    // Центр сцены в мировых координатах
    public Vector3 WorldCenter => new Vector3(
        (GridX * SCENE_SIZE) + (SCENE_SIZE / 2f),
        0,
        (GridZ * SCENE_SIZE) + (SCENE_SIZE / 2f)
    );

    // Конвертация мировой позиции в SceneID
    public static SceneID FromWorldPosition(Vector3 worldPos)
    {
        int gridX = Mathf.FloorToInt(worldPos.x / SCENE_SIZE);
        int gridZ = Mathf.FloorToInt(worldPos.z / SCENE_SIZE);
        return new SceneID(gridX, gridZ);
    }

    // Конвертация мировой позиции в локальную позицию внутри сцены
    public Vector3 ToLocalPosition(Vector3 worldPos)
    {
        return new Vector3(
            worldPos.x - (GridX * SCENE_SIZE),
            worldPos.y,
            worldPos.z - (GridZ * SCENE_SIZE)
        );
    }

    // Конвертация локальной позиции в мировую
    public Vector3 ToWorldPosition(Vector3 localPos)
    {
        return new Vector3(
            localPos.x + (GridX * SCENE_SIZE),
            localPos.y,
            localPos.z + (GridZ * SCENE_SIZE)
        );
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref GridX);
        serializer.SerializeValue(ref GridZ);
    }
}
```

### SceneRegistry (ScriptableObject)

```csharp
[CreateAssetMenu(fileName = "SceneRegistry", menuName = "ProjectC/World/Scene Registry")]
public class SceneRegistry : ScriptableObject
{
    public int GridColumns = 8;  // X dimension
    public int GridRows = 8;    // Z dimension

    public string SceneNamePrefix = "WorldScene_";

    public string GetSceneName(SceneID sceneId)
    {
        if (!IsValid(sceneId)) return string.Empty;
        return $"{SceneNamePrefix}{sceneId.GridX}_{sceneId.GridZ}";
    }

    public bool IsValid(SceneID sceneId)
    {
        return sceneId.GridX >= 0 && sceneId.GridX < GridColumns &&
               sceneId.GridZ >= 0 && sceneId.GridZ < GridRows;
    }

    public IEnumerable<SceneID> GetAllSceneIDs()
    {
        for (int x = 0; x < GridColumns; x++)
            for (int z = 0; z < GridRows; z++)
                yield return new SceneID(x, z);
    }
}
```

---

## Серверные компоненты

### ServerSceneManager

```csharp
public class ServerSceneManager : NetworkBehaviour
{
    // ClientId → SceneID
    private readonly Dictionary<ulong, SceneID> _clientSceneMap = new();

    // SceneID → List<ClientId>
    private readonly Dictionary<SceneID, List<ulong>> _sceneClients = new();

    [SerializeField] private SceneRegistry sceneRegistry;
    [SerializeField] private float sceneLoadRadius = 1f;  // scenes
    [SerializeField] private float sceneUnloadRadius = 2f;

    public override void OnNetworkSpawn()
    {
        if (!IsServer) { enabled = false; return; }

        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
    }

    // Проверка позиции игрока - вызывается из NetworkPlayer
    public void CheckSceneTransition(ulong clientId, Vector3 worldPosition)
    {
        SceneID current = _clientSceneMap.GetValueOrDefault(clientId, default);
        SceneID target = SceneID.FromWorldPosition(worldPosition);

        if (!target.Equals(current))
        {
            TransitionClient(clientId, current, target);
        }
    }

    private void TransitionClient(ulong clientId, SceneID from, SceneID to)
    {
        // 1. Обновить карту
        if (_clientSceneMap.ContainsKey(clientId))
            RemoveClientFromScene(clientId, from);

        _clientSceneMap[clientId] = to;
        AddClientToScene(clientId, to);

        // 2. Отправить RPC клиенту загрузить новую сцену
        LoadSceneClientRpc(clientId, new SceneTransitionData
        {
            TargetScene = to,
            LocalPosition = to.ToLocalPosition(/* client world pos */)
        });
    }

    [ClientRpc]
    private void LoadSceneClientRpc(ulong clientId, SceneTransitionData data)
    {
        // Клиент обрабатывает в ClientSceneLoader
    }

    public SceneID GetClientScene(ulong clientId)
    {
        return _clientSceneMap.GetValueOrDefault(clientId, default);
    }
}
```

### PlayerSceneTracker (модификация)

```csharp
// Модификация существующего PlayerChunkTracker для поддержки сцен

public class PlayerSceneTracker : NetworkBehaviour
{
    // Добавить: ClientId → SceneID
    private readonly Dictionary<ulong, SceneID> _clientScenes = new();

    // При каждом обновлении позиции - проверять сцену
    public void UpdatePlayerPosition(ulong clientId, Vector3 worldPosition)
    {
        // Проверка сцены
        SceneID newScene = SceneID.FromWorldPosition(worldPosition);
        if (_clientScenes.TryGetValue(clientId, out var oldScene) && !oldScene.Equals(newScene))
        {
            // Scene transition!
            OnSceneChanged(clientId, oldScene, newScene);
        }
        _clientScenes[clientId] = newScene;

        // Существующая логика для чанков
        UpdatePlayerChunk(clientId, worldPosition);
    }

    public SceneID GetClientScene(ulong clientId)
    {
        return _clientScenes.GetValueOrDefault(clientId, default);
    }
}
```

---

## Клиентские компоненты

### ClientSceneLoader

```csharp
public class ClientSceneLoader : MonoBehaviour
{
    [SerializeField] private SceneRegistry sceneRegistry;
    [SerializeField] private Transform playerTransform;

    private readonly HashSet<SceneID> _loadedScenes = new();
    private SceneID _currentScene;

    // Вызывается из ServerSceneManager.LoadSceneClientRpc
    public void OnSceneTransitionReceived(SceneTransitionData data)
    {
        StartCoroutine(LoadSceneCoroutine(data.TargetScene, data.LocalPosition));
    }

    private IEnumerator LoadSceneCoroutine(SceneID targetScene, Vector3 spawnLocalPos)
    {
        // 1. Выгрузить старую сцену если далеко
        if (!_currentScene.Equals(targetScene) && _loadedScenes.Contains(_currentScene))
        {
            yield return UnloadScene(_currentScene);
        }

        // 2. Загрузить целевую + соседние (для overlap)
        yield return LoadSceneWithNeighbors(targetScene);

        // 3. Позиционировать игрока
        playerTransform.position = spawnLocalPos;

        // 4. Подтвердить серверу
        NotifyServerSceneLoaded();
    }

    private IEnumerator LoadSceneWithNeighbors(SceneID center)
    {
        // Загрузить 3×3 сетку сцен
        for (int dx = -1; dx <= 1; dx++)
            for (int dz = -1; dz <= 1; dz++)
            {
                var sceneId = new SceneID(center.GridX + dx, center.GridZ + dz);
                if (sceneRegistry.IsValid(sceneId) && !_loadedScenes.Contains(sceneId))
                {
                    string sceneName = sceneRegistry.GetSceneName(sceneId);
                    var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
                    while (!op.isDone) yield return null;
                    _loadedScenes.Add(sceneId);
                }
            }

        _currentScene = center;
    }
}
```

---

## Visibility система (NGO)

### CheckObjectVisibility на NetworkObject

```csharp
// Прикрепить к каждому NetworkObject который принадлежит сцене
public class SceneBoundNetworkObject : NetworkBehaviour
{
    [SerializeField] private SceneID ownedScene;

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            NetworkObject.CheckObjectVisibility = (clientId) =>
            {
                var clientScene = PlayerSceneTracker.Instance.GetClientScene(clientId);
                return clientScene.Equals(ownedScene);
            };
        }
    }
}
```

**Важно:** CheckObjectVisibility вызывается ТОЛЬКО при спавне объекта. Для runtime изменений используем NetworkHide/NetworkShow.

---

## Preload триггер зоны

### WorldSceneManager

```csharp
public class WorldSceneManager : MonoBehaviour
{
    [SerializeField] private float preloadTriggerDistance = 10000f; // 10k до границы
    [SerializeField] private int loadRadius = 1;  // загружать 3×3
    [SerializeField] private int unloadRadius = 2; // выгружать вне 5×5

    private readonly HashSet<SceneID> _loadedScenes = new();
    private SceneID _currentCenterScene;

    private void Update()
    {
        var playerPos = GetPlayerPosition();
        var centerScene = SceneID.FromWorldPosition(playerPos);

        if (!centerScene.Equals(_currentCenterScene))
        {
            _currentCenterScene = centerScene;
            UpdateLoadedScenes(playerPos);
        }

        // Preload соседние сцены если близко к границе
        CheckPreloadTriggers(playerPos, centerScene);
    }

    private void CheckPreloadTriggers(Vector3 playerPos, SceneID current)
    {
        Vector3 localPos = current.ToLocalPosition(playerPos);

        // Проверка каждой границы
        if (localPos.x > (SCENE_SIZE - preloadTriggerDistance))
            PreloadScene(new SceneID(current.GridX + 1, current.GridZ));

        if (localPos.x < preloadTriggerDistance)
            PreloadScene(new SceneID(current.GridX - 1, current.GridZ));

        if (localPos.z > (SCENE_SIZE - preloadTriggerDistance))
            PreloadScene(new SceneID(current.GridX, current.GridZ + 1));

        if (localPos.z < preloadTriggerDistance)
            PreloadScene(new SceneID(current.GridX, current.GridZ - 1));
    }

    private void PreloadScene(SceneID sceneId)
    {
        if (_loadedScenes.Contains(sceneId)) return;
        StartCoroutine(LoadSceneAsync(sceneId));
    }
}
```

---

## Интеграция с существующими системами

### Что НЕ меняется:

| Компонент | Почему |
|-----------|--------|
| **ProceduralChunkGenerator** | Работает с ChunkId, не зависит от сцен. Локальная генерация |
| **ChunkLoader** | Загружает чанки вокруг игрока в пределах загруженной сцены |
| **WorldChunkManager** | Глобальный реестр чанков, работает в любой сцене |
| **FloatingOriginMP** | Сдвигает WorldRoot, сцены загружены в правильных позициях |

### Что меняется:

| Компонент | Изменение |
|-----------|-----------|
| **WorldStreamingManager** | Добавить проверку: загружена ли сцена прежде чем грузить чанки |
| **NetworkPlayer** | Отправлять позицию в PlayerSceneTracker (уже делает частично) |
| **PlayerChunkTracker** | Добавить отслеживание SceneID для каждого клиента |

### Конфликт: FloatingOriginMP vs Scenes

**Проблема:** FloatingOriginMP сдвигает WorldRoot когда игрок далеко от origin. Но если сцены загружены на своих позициях (500,000 и т.д.), сдвиг WorldRoot не нужен — сцены уже на месте.

**Решение:** Внутри сцены (0-80,000) FloatingOriginMP не нужен. Отключить его когда игрок внутри загруженной сцены. Включать только если нужно загрузить удалённую сцену.

---

## Terrain Overlap: Сложность

### Проблема анализ от subagent:

**Текущая система ProceduralChunkGenerator:**
- Использует локальные координаты (local-space noise)
- Каждая гора генерируется независимо от своей позиции
- Нет понятия "overlap zone" — чанки изолированы

**Что нужно для overlap:**
1. Расширить границы чанков у краёв сцены (на 400 единиц)
2. Добавить флаг IsOverlap для чанков
3. Clouds/farms НЕ генерировать в overlap зонах (избежать дубликатов)

### Упрощённый подход (рекомендуется):

```csharp
// Не делать сложный overlap.
// Вместо этого:
// 1. Процедурная генерация детерминирована по ChunkId
// 2. Если два чанка генерируют одинаковые позиции - результат идентичен
// 3. Края сцен будут "стыковаться" если seed одинаковый для граничных чанков

// В overlap зоне:
// - Рендерить terrain только из "родительской" сцены
// - Не генерировать clouds/farms в overlap зоне
```

---

## Следующие шаги реализации

### Фаза 1: Основы (1-2 дня)

| # | Задача | Файл |
|---|--------|------|
| 1.1 | Создать SceneID struct | `Assets/_Project/Scripts/World/Scene/SceneID.cs` |
| 1.2 | Создать SceneRegistry ScriptableObject | `Assets/_Project/Scripts/World/Scene/SceneRegistry.cs` |
| 1.3 | Создать ServerSceneManager | `Assets/_Project/Scripts/World/Scene/ServerSceneManager.cs` |
| 1.4 | Создать ClientSceneLoader | `Assets/_Project/Scripts/World/Scene/ClientSceneLoader.cs` |
| 1.5 | Интегрировать в NetworkManagerController | Добавить ServerSceneManager |

### Фаза 2: Интеграция (2-3 дня)

| # | Задача | Файл |
|---|--------|------|
| 2.1 | Модифицировать PlayerSceneTracker для SceneID | Существующий |
| 2.2 | Добавить CheckObjectVisibility к SceneBoundNetworkObject | `Assets/_Project/Scripts/World/Scene/SceneBoundNetworkObject.cs` |
| 2.3 | Создать WorldSceneManager для загрузки сцен | `Assets/_Project/Scripts/World/Scene/WorldSceneManager.cs` |
| 2.4 | Интегрировать с WorldStreamingManager | Проверить冲突 |

### Фаза 3: Тестирование (2-3 дня)

| # | Задача |
|---|--------|
| 3.1 | Создать 2-3 тестовых сцены (80k×80k) |
| 3.2 | Тест с 2 игроками в разных сценах |
| 3.3 | Тест scene transition |
| 3.4 | Проверить что NGO visibility работает |

### Фаза 4: Preload система (1-2 дня)

| # | Задача |
|---|--------|
| 4.1 | Реализовать CheckPreloadTriggers в WorldSceneManager |
| 4.2 | Тест seamless transition |

### Фаза 5: Terrain Overlap (отложить)

| # | Задача | Статус |
|---|--------|--------|
| 5.1 | Дизайн overlap чанков | Отложить |
| 5.2 | Реализация | Отложить |
| 5.3 | Тест | Отложить |

---

## Вопросы требующие ответа перед реализацией

| # | Вопрос | Почему важен |
|---|--------|-------------|
| 1 | Сцены - это реальные Unity Scene файлы или логические зоны? | Влияет на загрузку |
| 2 | SceneRegistry - Resources.Load или Addressables? | Влияет на загрузку |
| 3 | Нужна ли поддержка cross-scene объектов (босса)? | Сложность сильно возрастает |
| 4 | FloatingOriginMP - отключать внутри сцены или нет? | Влияет на архитектуру |

---

## Вердикт реализуемости

| Компонент | Сложность | Реализуемость |
|-----------|-----------|---------------|
| SceneID + SceneRegistry | Низкая | ✅ Да |
| ServerSceneManager | Средняя | ✅ Да |
| ClientSceneLoader | Средняя | ✅ Да |
| NGO CheckObjectVisibility | Низкая | ✅ Да |
| WorldSceneManager (preload) | Средняя | ✅ Да |
| Terrain Overlap | Высокая | ⚠️ Отложить |
| Cross-scene объекты | Очень высокая | ❌ Нет (не нужно) |

**Общий вердикт:** ✅ РЕАЛИЗУЕМО

Основные риски:
1. NGO scene management integration (решаемо)
2. Terrain seams (решаемо отложенным overlap)
3. Тестирование требует реальных сцен

**Рекомендация:** Начать с Фазы 1-2, протестировать с 2 клиентами, затем продолжить.

---

**Документ подготовлен:** Claude Code + Subagents
**Дата:** 28.04.2026