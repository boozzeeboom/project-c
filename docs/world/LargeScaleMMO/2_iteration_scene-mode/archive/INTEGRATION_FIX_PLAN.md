# INTEGRATION_FIX_PLAN - Scene System Architecture

**Date:** 29.04.2026
**Status:** ДЕТАЛЬНЫЙ АНАЛИЗ + ПЛАН ИСПРАВЛЕНИЙ
**Priority:** P0 (Critical blocks startup)

---

## Executive Summary

Интеграция scene-based системы (24 сцены) сломала network architecture. Проблема: компоненты созданы, но не соединены друг с другом. При запуске - игрок не появляется, ошибки "SceneTransitionCoordinator not found", "NetworkPlayer not found".

---

## АРХИТЕКТУРНАЯ ПРОБЛЕМА: Лишний слой

### Текущая (сломана):

```
ServerSceneManager.OnClientConnected
    ↓
FindPlayerTransformCoroutine (ждёт 0.5 сек)
    ↓
SendInitialSceneToClient()
    ↓
[ClientRpc] InitializeSceneClientRpc(targetClientId, scene)
    ↓
SceneTransitionCoordinator.HandleInitialScene()
    ↓
ClientSceneLoader.LoadScene()
```

**Проблема:** `SceneTransitionCoordinator` - лишний промежуточный слой. Он не нужен.

### Правильная архитектура:

```
NetworkManager.OnClientConnectedCallback
    ↓ (host StartHost → SpawnAsPlayerObject для LocalClientId)
NetworkPlayerSpawner.SpawnLocalPlayer()
    ↓
ClientSceneLoader.WaitForPlayer() → OnClientConnected
    ↓ (если ClientSceneLoader уже инициализирован - загрузить сцену)

ИЛИ:

ServerSceneManager.OnClientConnectedCallback
    ↓
FindPlayerTransformCoroutine
    ↓
SendInitialSceneToClient()
    ↓
[ClientRpc] → Direct call to ClientSceneLoader (NO intermediate)
```

---

## ДЕТАЛЬНЫЙ АНАЛИЗ КОМПОНЕНТОВ

### 1. ServerSceneManager.cs

**Файл:** `Assets/_Project/Scripts/World/Scene/ServerSceneManager.cs`

**Проблема 1 (P0): GetComponent<SceneTransitionCoordinator>() ищет на неправильном объекте**

```csharp
// Line 264:
var coordinator = NetworkManager.Singleton.GetComponent<SceneTransitionCoordinator>();

// SceneTransitionCoordinator - NetworkBehaviour
// NetworkManager.Singleton - это Unity.Netcode.NetworkManager
// GetComponent<SceneTransitionCoordinator>() ищет на GameObject.networkManager
// НО SceneTransitionCoordinator находится на "Runtime" GameObject, НЕ на NetworkManager
```

**Статус:** ❌ АРХИТЕКТУРНО НЕВЕРНО

**Fix:** Убрать SceneTransitionCoordinator как посредник. ServerSceneManager отправляет ClientRpc напрямую, а ClientSceneLoader подписывается на события.

---

### 2. SceneTransitionCoordinator.cs

**Файл:** `Assets/_Project/Scripts/World/Scene/SceneTransitionCoordinator.cs`

**Проблема:** Этот компонент - лишний слой абстракции. Он не делает ничего кроме переадресации вызовов.

```csharp
// Line 66-76: HandleSceneTransition()
public void HandleSceneTransition(SceneTransitionData transitionData)
{
    if (clientSceneLoader != null)
    {
        clientSceneLoader.LoadScene(transitionData.TargetScene, transitionData.LocalPosition);
    }
}

// Это просто wrapper: ServerSceneManager → Coordinator → ClientSceneLoader
// Вместо: ServerSceneManager → ClientSceneLoader напрямую
```

**Решение:** УБРАТЬ SceneTransitionCoordinator полностью. Заменить на прямые вызовы.

---

### 3. ClientSceneLoader.cs

**Файл:** `Assets/_Project/Scripts/World/Scene/ClientSceneLoader.cs`

**Проблема 1 (P0): Не загружает начальную сцену автоматически**

```csharp
// Line 66-69:
private void Start()
{
    FindLocalPlayer(); // Ищет игрока, но не загружает сцену!
}

// При StartHost():
// 1. NetworkManager.StartHost()
// 2. SpawnAsPlayerObject() для LocalClientId
// 3. NetworkPlayer.OnNetworkSpawn()
// 4. ClientSceneLoader.Start() - уже прошло, игнорируется
// 5. ClientSceneLoader.LoadScene() НЕ вызывается!
```

**Решение:** При OnNetworkSpawn() проверять - если мы уже подключены (IsHost) - загрузить начальную сцену.

**Проблема 2 (P1): FindLocalPlayer() слишком сложный**

```csharp
// Line 71-98: FindLocalPlayer() - слишком много поиска
// Сначала ищет NetworkObject.IsOwner, потом GameObject.FindGameObjectWithTag("Player")
// Это не нужно - подписка на событие NetworkManager.OnClientConnectedCallback достаточно
```

---

### 4. BootstrapSceneGenerator.cs

**Файл:** `Assets/_Project/Editor/BootstrapSceneGenerator.cs`

**Проблема 1 (P0): SceneTransitionCoordinator добавлен в Runtime, не в NetworkManager**

```csharp
// Line 177-195: CreateSceneManagement()
private void CreateSceneManagement()
{
    GameObject runtimeObj = new GameObject("Runtime");
    
    var clientLoader = runtimeObj.AddComponent<ClientSceneLoader>();
    var serverSceneManager = runtimeObj.AddComponent<ServerSceneManager>();
    var coordinator = runtimeObj.AddComponent<SceneTransitionCoordinator>(); // WRONG!
    var networkObject = runtimeObj.AddComponent<NetworkObject>();
}
```

**Правильно:** `SceneTransitionCoordinator` должен быть на том же GameObject что и `NetworkManager`, потому что `ServerSceneManager.GetComponent<SceneTransitionCoordinator>()` ищет его там!

**Проблема 2 (P1): PlayerSpawner структура**

```csharp
// Line 155-175: CreatePlayerSpawner()
// Создаёт PlayerSpawner с NetworkPlayerSpawner + NetworkPlayer на том же объекте
// Это правильно для NGO - SpawnAsPlayerObject() требует NetworkObject на том же объекте
```

---

### 5. NetworkPlayerSpawner.cs

**Файл:** `Assets/_Project/Scripts/Network/NetworkPlayerSpawner.cs`

**Проблема (P1): Timing issue - OnClientConnectedCallback вызывается ДО Start()**

```csharp
// Line 14-31: Start()
private void Start()
{
    if (NetworkManager.Singleton != null)
    {
        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        
        if (useScenePlayerAsHost && NetworkManager.Singleton.IsHost)
        {
            SpawnLocalPlayer();
        }
    }
}

// Проблема: Start() вызывается ПОСЛЕ OnNetworkSpawn() у NetworkBehaviour
// Но OnClientConnectedCallback(0) для хоста вызывается ВО ВРЕМЯ StartHost()
// Это значит что Start() еще не выполнился, а callback уже fires
```

**Решение:** Проверять в Update() или использовать корутину с задержкой.

---

## ИСПРАВЛЕНИЯ (в порядке приоритета)

### FIX-001: Убрать SceneTransitionCoordinator (P0)

**Файлы:**
- `Assets/_Project/Scripts/World/Scene/SceneTransitionCoordinator.cs` - УДАЛИТЬ
- `Assets/_Project/Scripts/World/Scene/ServerSceneManager.cs` - убрать依赖 от Coordinator
- `Assets/_Project/Editor/BootstrapSceneGenerator.cs` - не создавать Coordinator

**Изменения в ServerSceneManager.cs:**

```csharp
// БЫЛО (line 257-273):
[ClientRpc]
private void InitializeSceneClientRpc(ulong targetClientId, SceneID scene, ClientRpcParams clientRpcParams = default)
{
    if (targetClientId != NetworkManager.Singleton.LocalClientId)
        return;

    LogDebug($"[Client] Received initial scene: {scene}");

    var coordinator = NetworkManager.Singleton.GetComponent<SceneTransitionCoordinator>();
    if (coordinator != null)
    {
        coordinator.HandleInitialScene(scene);
    }
    else
    {
        Debug.LogWarning("[ServerSceneManager] SceneTransitionCoordinator not found on NetworkManager!");
    }
}

// СТАЛО:
[ClientRpc]
private void InitializeSceneClientRpc(ulong targetClientId, SceneID scene, ClientRpcParams clientRpcParams = default)
{
    if (targetClientId != NetworkManager.Singleton.LocalClientId)
        return;

    LogDebug($"[Client] Received initial scene: {scene}");

    // Direct call to ClientSceneLoader (no intermediate)
    var loader = FindAnyObjectByType<ClientSceneLoader>();
    if (loader != null)
    {
        Vector3 localSpawn = new Vector3(SceneID.SCENE_SIZE / 2f, 0, SceneID.SCENE_SIZE / 2f);
        loader.LoadScene(scene, localSpawn);
    }
    else
    {
        Debug.LogWarning("[ServerSceneManager] ClientSceneLoader not found!");
    }
}
```

**Аналогично для:**
- `LoadSceneTransitionClientRpc` (line 292-308)
- `UnloadSceneClientRpc` (line 327-339)

---

### FIX-002: ClientSceneLoader - автозагрузка начальной сцены (P0)

**Файл:** `Assets/_Project/Scripts/World/Scene/ClientSceneLoader.cs`

**Изменения:**

```csharp
// БЫЛО (Start):
private void Start()
{
    FindLocalPlayer();
}

// СТАЛО:
private void Start()
{
    FindLocalPlayer();
    
    // FIX-002: Если мы Host - загрузить начальную сцену автоматически
    if (NetworkManager.Singleton != null && 
        NetworkManager.Singleton.IsHost && 
        _currentScene.Equals(default))
    {
        Debug.Log("[ClientSceneLoader] Auto-loading initial scene for Host");
        // Загрузить сцену 0,0 (center of world)
        StartCoroutine(LoadSceneWithNeighborsCoroutine(new SceneID(0, 0)));
    }
}
```

**Новая проблема:** ClientSceneLoader.Start() вызывается ДО NetworkManager.StartHost(). Поэтому проверка `IsHost` в Start() вернёт false.

**Решение:** Использовать OnNetworkSpawn() для подписки на событие, и проверять при каждом новом подключении:

```csharp
private void OnEnable()
{
    if (NetworkManager.Singleton != null)
    {
        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnectedCallback;
    }
}

private void OnDisable()
{
    if (NetworkManager.Singleton != null)
    {
        NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnectedCallback;
    }
}

private void OnClientConnectedCallback(ulong clientId)
{
    // Если это мы (локальный клиент) и мы еще не в сцене
    if (clientId == NetworkManager.Singleton.LocalClientId && _currentScene.Equals(default))
    {
        StartCoroutine(LoadInitialSceneForLocalClient());
    }
}

private IEnumerator LoadInitialSceneForLocalClient()
{
    yield return new WaitForSeconds(0.5f); // Дать время на спавн игрока
    
    // Загрузить центр мира (0,0)
    SceneID initialScene = new SceneID(0, 0);
    Debug.Log($"[ClientSceneLoader] Loading initial scene for local client: {initialScene}");
    yield return LoadSceneWithNeighborsCoroutine(initialScene);
}
```

---

### FIX-003: BootstrapSceneGenerator - убрать SceneTransitionCoordinator (P0)

**Файл:** `Assets/_Project/Editor/BootstrapSceneGenerator.cs`

**Изменения:**

```csharp
// БЫЛО (line 177-195):
private void CreateSceneManagement()
{
    GameObject runtimeObj = new GameObject("Runtime");
    
    var clientLoader = runtimeObj.AddComponent<ClientSceneLoader>();
    var so = new SerializedObject(clientLoader);
    so.FindProperty("loadNeighbors").boolValue = true;
    so.FindProperty("unloadDistantScenes").boolValue = true;
    so.ApplyModifiedProperties();
    
    var serverSceneManager = runtimeObj.AddComponent<ServerSceneManager>();
    so = new SerializedObject(serverSceneManager);
    so.FindProperty("updateInterval").floatValue = 0.5f;
    so.ApplyModifiedProperties();
    
    var coordinator = runtimeObj.AddComponent<SceneTransitionCoordinator>(); // REMOVE
    var networkObject = runtimeObj.AddComponent<NetworkObject>(); // REMOVE - не нужен
}

// СТАЛО:
private void CreateSceneManagement()
{
    GameObject runtimeObj = new GameObject("Runtime");
    runtimeObj.transform.position = Vector3.zero;
    
    var clientLoader = runtimeObj.AddComponent<ClientSceneLoader>();
    var so = new SerializedObject(clientLoader);
    so.FindProperty("loadNeighbors").boolValue = true;
    so.FindProperty("unloadDistantScenes").boolValue = true;
    so.ApplyModifiedProperties();
    
    var serverSceneManager = runtimeObj.AddComponent<ServerSceneManager>();
    so = new SerializedObject(serverSceneManager);
    so.FindProperty("updateInterval").floatValue = 0.5f;
    so.ApplyModifiedProperties();
    
    // SceneTransitionCoordinator УДАЛЁН - не нужен
    // ServerSceneManager отправляет RPC напрямую в ClientSceneLoader
}
```

**Также удалить:**
```csharp
// line 45 - удалить "• SceneTransitionCoordinator" из описания
// line 193 - удалить создание coordinator
```

---

### FIX-004: WorldSceneSetup.cs - убрать SceneTransitionCoordinator (P0)

**Файл:** `Assets/_Project/Editor/WorldSceneSetup.cs`

**Изменения:**

```csharp
// БЫЛО (line 142-150):
private void AddSceneTransitionCoordinator(Transform parent)
{
    GameObject obj = new GameObject("SceneTransitionCoordinator");
    obj.transform.SetParent(parent);
    obj.transform.localPosition = Vector3.zero;
    
    obj.AddComponent<SceneTransitionCoordinator>();
    obj.AddComponent<NetworkObject>();
}

// СТАЛО: Удалить метод полностью

// БЫЛО (line 78-104):
private void AddRuntimeObjects(Scene scene)
{
    GameObject runtimeObj = new GameObject("WorldRuntime");
    runtimeObj.transform.position = Vector3.zero;
    
    AddWorldSceneManager(runtimeObj.transform);
    AddClientSceneLoader(runtimeObj.transform);
    AddServerSceneManager(runtimeObj.transform);
    AddSceneTransitionCoordinator(runtimeObj.transform); // REMOVE
    AddWorldStreamingManager(runtimeObj.transform);
    AddMainCamera(runtimeObj.transform);
    AddFloatingOriginMP(runtimeObj.transform);
    AddAltitudeCorridorSystem(runtimeObj.transform);
    AddCloudSystem(runtimeObj.transform);
}

// СТАЛО:
private void AddRuntimeObjects(Scene scene)
{
    GameObject runtimeObj = new GameObject("WorldRuntime");
    runtimeObj.transform.position = Vector3.zero;
    
    AddWorldSceneManager(runtimeObj.transform);
    AddClientSceneLoader(runtimeObj.transform);
    AddServerSceneManager(runtimeObj.transform);
    // AddSceneTransitionCoordinator REMOVED - не нужен в scene-based architecture
    AddWorldStreamingManager(runtimeObj.transform);
    AddMainCamera(runtimeObj.transform);
    AddFloatingOriginMP(runtimeObj.transform);
    AddAltitudeCorridorSystem(runtimeObj.transform);
    AddCloudSystem(runtimeObj.transform);
}
```

---

### FIX-005: NetworkPlayerSpawner timing fix (P1)

**Файл:** `Assets/_Project/Scripts/Network/NetworkPlayerSpawner.cs`

**Изменения:**

```csharp
// БЫЛО (Start):
private void Start()
{
    if (NetworkManager.Singleton != null)
    {
        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;

        if (useScenePlayerAsHost && NetworkManager.Singleton.IsHost)
        {
            SpawnLocalPlayer();
        }
    }
}

// СТАЛО:
private void Start()
{
    if (NetworkManager.Singleton != null)
    {
        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        
        // FIX-005: Проверяем IsHost в Update (первый фрейм) а не в Start
        // Это решает timing issue когда callback вызывается до Start()
    }
}

private void Update()
{
    // FIX-005: Spawn для хоста в первом Update, не в Start
    if (useScenePlayerAsHost && NetworkManager.Singleton != null && 
        NetworkManager.Singleton.IsHost && !_hasSpawnedHostPlayer)
    {
        _hasSpawnedHostPlayer = true;
        SpawnLocalPlayer();
    }
}

private bool _hasSpawnedHostPlayer = false;
```

---

### FIX-006: ClientSceneLoader - улучшенный FindLocalPlayer (P1)

**Файл:** `Assets/_Project/Scripts/World/Scene/ClientSceneLoader.cs`

**Упрощённая версия:**

```csharp
private void FindLocalPlayer()
{
    if (playerTransform != null) return; // Уже найден

    if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
    {
        var networkObjects = FindObjectsByType<NetworkObject>();
        foreach (var netObj in networkObjects)
        {
            if (netObj.IsOwner && netObj.GetComponent<ProjectC.Player.NetworkPlayer>() != null)
            {
                playerTransform = netObj.transform;
                _isInitialized = true;
                LogDebug($"Found local player: {netObj.name}");
                return;
            }
        }
    }

    // Fallback: ищем по тегу если не в сети
    var playerByTag = GameObject.FindGameObjectWithTag("Player");
    if (playerByTag != null)
    {
        playerTransform = playerByTag.transform;
        _isInitialized = true;
        LogDebug($"Found player by tag: {playerByTag.name}");
        return;
    }

    // Продолжаем искать
    StartCoroutine(WaitForPlayer());
}

private IEnumerator WaitForPlayer()
{
    yield return new WaitForSeconds(1f);
    
    if (playerTransform != null) yield break;
    
    // Попробуем еще раз
    var networkObjects = FindObjectsByType<NetworkObject>();
    foreach (var netObj in networkObjects)
    {
        if (netObj.IsOwner && netObj.GetComponent<ProjectC.Player.NetworkPlayer>() != null)
        {
            playerTransform = netObj.transform;
            _isInitialized = true;
            LogDebug($"Found local player after wait: {netObj.name}");
            yield break;
        }
    }

    var playerByTag = GameObject.FindGameObjectWithTag("Player");
    if (playerByTag != null)
    {
        playerTransform = playerByTag.transform;
        _isInitialized = true;
        LogDebug($"Found player by tag after wait: {playerByTag.name}");
        yield break;
    }

    Debug.LogWarning("[ClientSceneLoader] NetworkPlayer not found after waiting. Will retry...");
    StartCoroutine(WaitForPlayer());
}
```

---

## ФАЙЛЫ ДЛЯ УДАЛЕНИЯ

```diff
- Assets/_Project/Scripts/World/Scene/SceneTransitionCoordinator.cs
- Assets/_Project/Scripts/World/Scene/SceneTransitionCoordinator.cs.meta
```

---

## ИЗМЕНЁННЫЕ ФАЙЛЫ (7 файлов)

| Файл | Изменение | FIX |
|------|-----------|-----|
| `ServerSceneManager.cs` | Убрать зависимость от SceneTransitionCoordinator | FIX-001 |
| `ClientSceneLoader.cs` | Автозагрузка сцены + улучшенный поиск | FIX-002, FIX-006 |
| `NetworkPlayerSpawner.cs` | Timing fix | FIX-005 |
| `BootstrapSceneGenerator.cs` | Не создавать Coordinator | FIX-003 |
| `WorldSceneSetup.cs` | Не создавать Coordinator | FIX-004 |

---

## ПОРЯДОК ИСПОЛНЕНИЯ

```
1. FIX-001: Удалить SceneTransitionCoordinator из ServerSceneManager.cs
2. FIX-002: Обновить ClientSceneLoader.cs для автозагрузки
3. FIX-005: Исправить NetworkPlayerSpawner.cs timing
4. FIX-003: Обновить BootstrapSceneGenerator.cs
5. FIX-004: Обновить WorldSceneSetup.cs
6. УДАЛИТЬ SceneTransitionCoordinator.cs и .meta
7. Проверить SceneRegistry.asset существует
8. Запустить Unity и проверить ошибки
9. Если ошибки - анализировать консоль
```

---

## ОЖИДАЕМЫЙ РЕЗУЛЬТАТ ПОСЛЕ ИСПРАВЛЕНИЙ

```
1. Запуск Play Mode
2. NetworkTestMenu появляется
3. Нажатие "Host":
   - NetworkManager.StartHost()
   - ServerSceneManager.OnNetworkSpawn() на сервере
   - NetworkPlayerSpawner.SpawnLocalPlayer() для LocalClientId=0
   - NetworkPlayer.OnNetworkSpawn() для игрока
4. ClientSceneLoader.OnClientConnectedCallback(LocalClientId=0)
   - Обнаруживает что мы в сети и ещё нет сцены
   - Загружает WorldScene_0_0 + соседей
5. Игрок появляется в мире (0,0) на высоте ~3000
6. Ошибки "SceneTransitionCoordinator not found" исчезают
```

---

## МЕТРИКИ УСПЕХА

| Метрика | До | После |
|---------|-----|-------|
| Ошибка "SceneTransitionCoordinator not found" | Да | Нет |
| Игрок не появляется | Да | Нет |
| ClientSceneLoader.WaitForPlayer retry loops | Много | 0-1 |
| NetworkPrefab null errors | Да | Нет |

---

## СВЯЗАННЫЕ ДОКУМЕНТЫ

- `SCENE_ARCHITECTURE_DECISION.md` - полная архитектура системы сцен
- `BOOTSTRAP_GENERATOR_FIXES.md` - исправления генераторов
- `ERRORS_ANALYSIS_28042026.md` - анализ ошибок
- `WORLD_SCENE_GENERATOR_REFACTORING.md` - изменения генераторов

---

**Автор:** Claude Code
**Дата:** 29.04.2026
**Версия:** 1.0