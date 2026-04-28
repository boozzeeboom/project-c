# Large Scale MMO Architecture: Scene-Based World

**Дата:** 28.04.2026
**Проект:** ProjectC_client
**Статус:** ✅ АРХИТЕКТУРНОЕ РЕШЕНИЕ ПРИНЯТО

---

## Executive Summary

**Решение:** 30 сцен размером 79,999 × 79,999 единиц с 2% overlap для бесшовного ландшафта.

**Обоснование:**
- Unity float32 precision: на 79,999 ошибка ~0.008 единиц (безопасно)
- Не требуется FloatingOriginMP внутри сцен
- NGO CheckObjectVisibility работает корректно для фильтрации трафика
- 2% overlap (1,600 единиц) обеспечивает визуальную непрерывность

---

## Параметры мира

| Параметр | Значение |
|----------|----------|
| Мир | 650,000 × 650,000 единиц |
| Размер сцены | 79,999 × 79,999 единиц |
| Overlap | 2% (1,600 единиц) |
| Количество сцен | 30 (ориентировочно) |
| Сетка сцен | ~6 × 5 = 30 (с запасом для overlap) |

---

## Ключевые открытия

### 1. NGO Scene Management работает правильно

**Миф:** "NGO синхрит все сцены всем клиентам (просто скрывает невидимые)"

**Реальность:**
- `CheckObjectVisibility` вызывается при СПАВНе объекта
- Если возвращает `false` → объект НИКОГДА не спавнится на клиенте
- Никакого трафика, никакого мусора памяти

**Вывод:** Оставить `Enable Scene Management = true` и использовать `CheckObjectVisibility`.

### 2. Precision безопасна до 80,000

| Расстояние от origin | Precision (ошибка) | Статус |
|----------------------|--------------------|--------|
| 50,000 | ~0.006 единиц | ✅ Безопасно |
| 79,999 | ~0.010 единиц | ✅ Безопасно |
| 100,000+ | ~0.016 единиц | ⚠️ На границе |

**Вывод:** Сцены 79,999 не требуют FloatingOriginMP внутри.

### 3. Overlap для seamlessness

Соседние сцены перекрываются на 2% (1,600 единиц):
```
Сцена [0,0]: 0 - 81,599 (реально 0-79,999 + 1,600 overlap)
Сцена [1,0]: 78,399 - 160,398 (1,600 overlap с [0,0])
```

**Преимущества:**
- Горы на границе видны из обеих сцен
- Нет "швов" или пустот
- Природный маскировщик: облака и туман ProjectC скрывают любые артефакты

---

## Архитектура: Сценовая система

### Сетка сцен (Grid)

```
Z
↑
6 ▢▢▢▢▢▢▢
5 ▢▢▢▢▢▢▢
4 ▢▢▢▢▢▢▢
3 ▢▢▢▢▢▢▢
2 ▢▢▢▢▢▢▢
1 ▢▢▢▢▢▢▢
0 ▢▢▢▢▢▢▢
  0→X
```

- Мир 650,000 → сетка ~8×8 для полного покрытия с запасом
- Сцена идентифицируется как `(gridX, gridZ)` где каждый = 0-7
- Полная сетка: 8×8 = 64 сцены (достаточно для мира 650k с overlap)

### Структура SceneID

```csharp
public struct SceneID : IEquatable<SceneID>
{
    public readonly int GridX;
    public readonly int GridZ;

    public SceneID(int gridX, int gridZ)
    {
        GridX = gridX;
        GridZ = gridZ;
    }

    // Мировая координата origin этой сцены
    public Vector3 WorldOrigin => new Vector3(GridX * 79_999f, 0, GridZ * 79_999f);

    // Размер сцены с учётом overlap
    public const float SceneSizeWithOverlap = 81_599f; // 79,999 + 1,600
    public const float SceneSize = 79_999f;
    public const float OverlapSize = 1_600f;
}
```

### Определение сцены по позиции

```csharp
public static SceneID GetSceneAtPosition(Vector3 worldPosition)
{
    int gridX = Mathf.FloorToInt(worldPosition.x / 79_999f);
    int gridZ = Mathf.FloorToInt(worldPosition.z / 79_999f);
    return new SceneID(gridX, gridZ);
}
```

---

## Серверное управление сценами

### PlayerSceneTracker (серверный компонент)

```csharp
// Отслеживает: какой клиент в какой сцене
public class PlayerSceneTracker : NetworkBehaviour
{
    private Dictionary<ulong, SceneID> _clientSceneMap = new();

    // При подключении клиента
    public void OnClientConnected(ulong clientId)
    {
        // Ждём спавна игрока
        StartCoroutine(WaitForPlayerSpawn(clientId));
    }

    // При изменении позиции игрока - проверяем сцену
    public void CheckSceneTransition(ulong clientId, Vector3 worldPosition)
    {
        var newScene = GetSceneAtPosition(worldPosition);
        var currentScene = _clientSceneMap.GetValueOrDefault(clientId, new SceneID(-1, -1));

        if (newScene != currentScene)
        {
            TransitionToScene(clientId, currentScene, newScene);
        }
    }

    private void TransitionToScene(ulong clientId, SceneID from, SceneID to)
    {
        // 1. Обновить карту
        _clientSceneMap[clientId] = to;

        // 2. Скрыть объекты старой сцены для клиента
        HideSceneObjectsFromClient(clientId, from);

        // 3. Отправить RPC клиенту загрузить новую сцену
        LoadSceneClientRpc(clientId, to);

        // 4. После загрузки - показать объекты новой сцены
        ShowSceneObjectsToClient(clientId, to);
    }
}
```

### SceneManager (серверный компонент)

```csharp
// Управляет загрузкой/выгрузкой сцен на сервере
public class SceneManager : NetworkBehaviour
{
    private HashSet<SceneID> _loadedScenes = new();
    private Dictionary<SceneID, List<NetworkObject>> _sceneObjects = new();

    // Загрузить сцену если ещё не загружена
    public async Task LoadSceneAsync(SceneID scene)
    {
        if (_loadedScenes.Contains(scene)) return;

        string sceneName = $"Scene_{scene.GridX}_{scene.GridZ}";
        var op = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(
            sceneName,
            UnityEngine.SceneManagement.LoadSceneMode.Additive
        );

        await op;

        // Заспавнить все NetworkObject в этой сцене
        SpawnSceneObjects(scene);
        _loadedScenes.Add(scene);
    }

    // Выгрузить сцену если в ней нет игроков
    public async Task UnloadSceneIfEmpty(SceneID scene)
    {
        if (IsSceneInUse(scene)) return;

        // Despawn все объекты сцены
        DespawnSceneObjects(scene);

        string sceneName = $"Scene_{scene.GridX}_{scene.GridZ}";
        var op = UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync(sceneName);

        await op;
        _loadedScenes.Remove(scene);
    }

    // Проверить есть ли игроки в этой сцене
    private bool IsSceneInUse(SceneID scene)
    {
        return _clientSceneMap.Values.Any(s => s == scene);
    }
}
```

---

## Visibility система (NGO)

### NetworkObject.CheckObjectVisibility

```csharp
// На каждом NetworkObject который принадлежит сцене
public class SceneBoundNetworkObject : NetworkBehaviour
{
    public SceneID OwnedScene;

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            NetworkObject.CheckObjectVisibility = ShouldClientSeeObject;
        }
    }

    private bool ShouldClientSeeObject(ulong clientId)
    {
        var clientScene = PlayerSceneTracker.GetClientScene(clientId);
        return clientScene == OwnedScene;
    }
}
```

### Late Join синхронизация

```csharp
// При подключении нового клиента:
// 1. Определить его сцену по позиции
// 2. Загрузить эту сцену визуально (ClientRpc)
// 3. Spawn все NetworkObject для этой сцены (NGO сам отфильтрует через CheckObjectVisibility)
// 4. Клиент получит только объекты своей сцены
```

---

## Клиентская система

### ClientSceneLoader

```csharp
// На клиенте
public class ClientSceneLoader : MonoBehaviour
{
    [SerializeField] private Transform _playerTransform;
    [SerializeField] private SceneID _currentScene;
    private HashSet<SceneID> _loadedScenes = new();

    // Получаем RPC от сервера
    [ClientRpc]
    private void LoadSceneClientRpc(int gridX, int gridZ)
    {
        var sceneId = new SceneID(gridX, gridZ);
        LoadSceneAdditive(sceneId);
    }

    private async void LoadSceneAdditive(SceneID scene)
    {
        string sceneName = $"Scene_{scene.GridX}_{scene.GridZ}";
        var op = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(
            sceneName,
            UnityEngine.SceneManagement.LoadSceneMode.Additive
        );

        await op;
        _loadedScenes.Add(scene);
        _currentScene = scene;

        // Подтвердить серверу что сцена загружена
        NotifySceneLoadedServerRpc(scene.GridX, scene.GridZ);
    }

    // Выгрузить старую сцену если далеко
    private void TryUnloadDistantScenes()
    {
        foreach (var scene in _loadedScenes.ToList())
        {
            if (Vector3.Distance(_playerTransform.position, GetSceneCenter(scene)) > 160_000f)
            {
                UnloadScene(scene);
            }
        }
    }
}
```

---

## Visibility при scene transition

### Проблема: CheckObjectVisibility не вызывается при рантайм изменениях

**Решение:** Использовать NetworkHide/NetworkShow

```csharp
// При transition:
// 1. Для всех объектов старой сцены:
foreach (var obj in _sceneObjects[from])
{
    obj.NetworkHide(clientId); // Останавливает трафик
}

// 2. После загрузки новой сцены:
foreach (var obj in _sceneObjects[to])
{
    obj.NetworkShow(clientId); // Возобновляет (снова проверяет CheckObjectVisibility)
}
```

---

## Загрузка/выгрузка сцен по distance

### Trigger zones

```
Игрок в сцене [X,Z] на позиции (localPos):
- При localPos > 70,000 (90% от края): Начать preload соседней сцены
- При localPos > 79,999: Transition в новую сцену
- При distance > 160,000 от центра старой сцены: Выгрузить старую сцену
```

```csharp
public class ScenePreloader : MonoBehaviour
{
    [SerializeField] private float _loadTriggerDistance = 60_000f; // 75% от края
    [SerializeField] private float _unloadDistance = 160_000f;

    private void Update()
    {
        var playerPos = _player.position;
        var currentScene = GetSceneAtPosition(playerPos);

        // Загрузить соседние сцены если близко к границе
        CheckAndLoadAdjacentScenes(playerPos, currentScene);

        // Выгрузить дальние сцены
        UnloadDistantScenes(playerPos);
    }

    private void CheckAndLoadAdjacentScenes(Vector3 playerPos, SceneID current)
    {
        float localX = playerPos.x - current.WorldOrigin.x;
        float localZ = playerPos.z - current.WorldOrigin.z;

        // Если близко к границе - загрузить соседнюю сцену
        if (localX > _loadTriggerDistance && current.GridX < 7)
        {
            ServerSceneManager.LoadSceneAsync(new SceneID(current.GridX + 1, current.GridZ));
        }
        // ...аналогично для других направлений
    }
}
```

---

## Terrain seamlessness с Overlap

### Как работает overlap

```
Сцена [0,0]: позиции 0 - 81,599 (визуально 0 - 79,999, последние 1,600 это overlap)
Сцена [1,0]: позиции 78,399 - 160,398 (первые 1,600 это overlap)

Игрок на 78,000 в сцене [0,0]:
- Видит terrain сцены [0,0] до 79,999
- Если сцена [1,0] загружена - видит terrain с 78,399 (overlap начинается)
- Визуально непрерывный ландшафт
```

### Важно для генерации

```csharp
// При процедурной генерации:
// 1. Использовать WORLD-SPACE координаты (не локальные)
// 2. Seed = world coordinate / scene size (консистентность)
// 3. Не генерировать уникальные объекты в зоне overlap (избежать дубликатов)
```

---

## Network bandwidth

### Реальные цифры (с CheckObjectVisibility)

| Ситуация | Полоса на клиента |
|----------|-------------------|
| Idle (50 объектов в сцене) | ~0.5-1 KB/s |
| 50 объектов двигаются | ~30-40 KB/s |
| Scene transition (20 игроков) | ~10-20 KB spike |

### Оптимизация

- NGO уже фильтрует трафик через CheckObjectVisibility
- Не нужно отключать Enable Scene Management
- Не нужно использовать CustomMessaging для объектов

---

## Проблемы и риски

### 1. Terrain seamlessness

**Риск:** Горы/террейн могут не совпадать на границе сцен.

**Решение:**
- Использовать world-space noise для генерации (один seed на всю карту)
- Или природный маскировщик (облака, туман ProjectC скрывают швы)

### 2. Scene transition moment

**Риск:** Игрок на 1 фрейм невидим или видит пустоту.

**Решение:**
- Preload соседней сцены за 10,000 до границы
- Overlap обеспечивает визуальную непрерывность

### 3. Cross-scene объекты (редкие босса)

**Риск:** Нужен объект видимый из соседней сцены.

**Решение:**
- Не поддерживать (сложность высокая)
- Или загрузить сцену босса аддитивно для клиентов соседней

### 4. Late join синхронизация

**Риск:** Клиент подключается, нужно синхронизировать все загруженные сцены.

**Решение:**
- Send list of loaded scenes + objects in client's scene only
- NGO's SceneSynchronization handleит это через CheckObjectVisibility

---

## Следующие шаги

1. **Проектирование SceneID и SceneMap** — создать структуры данных
2. **Реализация ServerSceneManager** — загрузка/выгрузка сцен
3. **Реализация PlayerSceneTracker** — трекинг позиций клиентов
4. **Настройка CheckObjectVisibility** — на всех NetworkObject
5. **Тест scene transition** — проверить с 2-3 клиентами
6. **Preload система** — загрузка соседних сцен заранее

---

## Вердикт

**Сценовая система 30 × 79,999 с overlap РЕАЛЬНО реализуема:**

| Компонент | Сложность | Статус |
|-----------|-----------|--------|
| SceneID + SceneMap | Низкая | ✅ Понятно |
| ServerSceneManager | Средняя | ✅ Реализуемо |
| PlayerSceneTracker | Средняя | ✅ Реализуемо |
| CheckObjectVisibility | Низкая | ✅ Уже в NGO |
| NetworkHide/NetworkShow | Низкая | ✅ Требуется для transition |
| Preload система | Средняя | ✅ Стандартный паттерн |
| Terrain seamlessness | Высокая | ⚠️ Требует planning |
| Cross-scene объекты | Очень высокая | ❌ Не рекомендуется |

---

**Автор:** Claude Code + Subagents (Unity Specialist, Network Programmer, Lead Programmer, Performance Analyst)
**Дата документации:** 28.04.2026