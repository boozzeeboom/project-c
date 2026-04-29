# Scene System Architecture (CORRECTED)

**Date:** 29.04.2026
**Status:** АРХИТЕКТУРА ИСПРАВЛЕНА
**Previous Status:** BROKEN INTEGRATION

---

## What Was Wrong

Scene system integration failed because:

1. **SceneTransitionCoordinator was an unnecessary middle layer**
   - ServerSceneManager sent RPCs to SceneTransitionCoordinator
   - SceneTransitionCoordinator forwarded to ClientSceneLoader
   - This caused "SceneTransitionCoordinator not found on NetworkManager" errors

2. **ClientSceneLoader didn't auto-load initial scene**
   - Player spawned but no scene was loaded
   - No automatic scene loading on Host start

3. **NetworkPlayerSpawner timing issues**
   - OnClientConnectedCallback fired BEFORE Start()
   - Host player never got spawned

---

## Corrected Architecture

```
┌─────────────────────────────────────────────────────────────┐
│ BootstrapScene.unity                                        │
│                                                             │
│ NetworkManager (GameObject "NetworkManager")                 │
│ ├── Unity.Netcode.NetworkManager                            │
│ └── NetworkManagerController                                │
│                                                             │
│ Runtime (GameObject "Runtime")                              │
│ ├── ClientSceneLoader (DontDestroyOnLoad)                   │
│ │   └── Подписывается на: OnClientConnectedCallback        │
│ │   └── При подключении: AutoLoadInitialScene()            │
│ │                                                          │
│ └── ServerSceneManager (NetworkBehaviour)                  │
│     └── OnClientConnected: FindPlayer + SendInitialScene    │
│     └── OnPlayerMove: CheckSceneTransition                 │
│     └── Отправляет ClientRpc напрямую в ClientSceneLoader  │
│                                                             │
│ PlayerSpawner (GameObject "PlayerSpawner")                  │
│ ├── NetworkObject                                          │
│ ├── NetworkPlayerSpawner (MonoBehaviour)                    │
│ │   └── Update(): если IsHost и не спавнен → SpawnLocal()  │
│ └── NetworkPlayer                                          │
│                                                             │
└─────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────┐
│ WorldScene_0_0.unity (additive loaded on demand)            │
│                                                             │
│ WorldRoot_0_0                                              │
│ ├── DirectionalLight                                        │
│ ├── GroundPlane_0_0                                         │
│ ├── SceneLabel                                             │
│ └── Boundaries_0_0                                         │
│                                                             │
│ [SceneBoundNetworkObject] на объектах мира                 │
└─────────────────────────────────────────────────────────────┘
```

---

## Component Responsibilities

### ServerSceneManager (Server-side)

```csharp
// OnNetworkSpawn - инициализация сервера
public override void OnNetworkSpawn()
{
    if (!IsServer) { enabled = false; return; }

    NetworkManager.Singleton.OnClientConnectedCallback += HandleClientConnected;
    NetworkManager.Singleton.OnClientDisconnectCallback += HandleClientDisconnected;
}

// HandleClientConnected - для каждого нового клиента
private void HandleClientConnected(ulong clientId)
{
    StartCoroutine(FindPlayerTransformCoroutine(clientId));
}

// FindPlayerTransformCoroutine - ждём спавн игрока
private IEnumerator FindPlayerTransformCoroutine(ulong clientId)
{
    yield return new WaitForSeconds(0.5f);
    // Найти NetworkObject игрока для clientId
    // Определить его SceneID по позиции
    // Зарегистрировать в _clientSceneMap
    // Отправить InitializeSceneClientRpc(targetClientId, scene)
}

// SendInitialSceneClientRpc - отправляет начальную сцену
// ClientSceneLoader получает напрямую (NO intermediate)
[ClientRpc]
private void InitializeSceneClientRpc(ulong targetClientId, SceneID scene, ...)
{
    var loader = FindAnyObjectByType<ClientSceneLoader>();
    loader.LoadScene(scene, spawnPosition);
}
```

### ClientSceneLoader (Client-side)

```csharp
// OnEnable - подписка на события
private void OnEnable()
{
    NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnectedCallback;
}

// OnClientConnectedCallback - при подключении локального клиента
private void OnClientConnectedCallback(ulong clientId)
{
    if (clientId == NetworkManager.Singleton.LocalClientId && _currentScene.Equals(default))
    {
        StartCoroutine(AutoLoadInitialSceneCoroutine());
    }
}

// AutoLoadInitialSceneCoroutine - загружает Scene(0,0) для хоста
private IEnumerator AutoLoadInitialSceneCoroutine()
{
    yield return new WaitForSeconds(1f);
    yield return LoadSceneWithNeighborsCoroutine(new SceneID(0, 0));
}

// LoadScene() - загружает сцену + телепортирует игрока
public void LoadScene(SceneID targetScene, Vector3 localSpawnPos)
{
    StartCoroutine(LoadSceneCoroutine(targetScene, localSpawnPos));
}
```

### NetworkPlayerSpawner

```csharp
// Update - проверяем каждый фрейм (решает timing issue)
private void Update()
{
    if (useScenePlayerAsHost && !_hasSpawnedHostPlayer &&
        NetworkManager.Singleton != null &&
        (NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsServer))
    {
        _hasSpawnedHostPlayer = true;
        SpawnLocalPlayer();
    }
}

private void SpawnLocalPlayer()
{
    var networkObject = GetComponent<NetworkObject>();
    if (networkObject != null && !networkObject.IsSpawned)
    {
        networkObject.SpawnAsPlayerObject(NetworkManager.Singleton.LocalClientId);
    }
}
```

---

## Data Flow

### Host Start Flow

```
1. User clicks "Host" в NetworkTestMenu
   ↓
2. NetworkManagerController.StartHost()
   ↓
3. Unity.Netcode.NetworkManager.StartHost()
   - Fires OnClientConnectedCallback(0) для LocalClientId
   ↓
4. NetworkPlayerSpawner.Update()
   - IsHost == true, _hasSpawnedHostPlayer == false
   → SpawnLocalPlayer()
   ↓
5. NetworkObject.SpawnAsPlayerObject(0)
   - NetworkPlayer.OnNetworkSpawn() fires
   ↓
6. ClientSceneLoader.OnClientConnectedCallback(0)
   - LocalClientId == 0, _currentScene == default
   → AutoLoadInitialSceneCoroutine()
   ↓
7. LoadSceneWithNeighborsCoroutine(SceneID(0,0))
   - Loads WorldScene_0_0 + 3x3 grid
   ↓
8. Player appears in world at (39999.5, 0, 39999.5)
```

### Client Join Flow (future)

```
1. Client connects via NMC.ConnectToServer()
   ↓
2. Server receives OnClientConnectedCallback(clientId)
   ↓
3. ServerSceneManager.FindPlayerTransformCoroutine(clientId)
   - Waits 0.5s for player spawn
   ↓
4. Server sends InitializeSceneClientRpc(targetClientId, scene)
   ↓
5. Client's ClientSceneLoader.LoadScene(scene)
   - Loads scene + teleports player
```

---

## Scene Transition Flow

```
1. Player moves in world
   ↓
2. ServerSceneManager.Update() checks player positions
   - Interval: 0.5 seconds (configurable)
   ↓
3. CheckSceneTransition(clientId, playerPos)
   - Calculate SceneID from world position
   - Compare with _clientSceneMap[clientId]
   ↓
4. If scene changed → TransitionClient(clientId, from, to)
   - HideSceneObjectsFromClient(clientId, from)
   - Update _clientSceneMap
   - Send SceneTransitionClientRpc with new scene
   ↓
5. Client receives RPC
   - ClientSceneLoader.LoadScene(newScene)
   - Unload old scene, Load new scene
```

---

## Changes Applied (29.04.2026)

### FIX-001: ServerSceneManager - Direct RPC to ClientSceneLoader

```csharp
// REMOVED: SceneTransitionCoordinator dependency
var coordinator = NetworkManager.Singleton.GetComponent<SceneTransitionCoordinator>();

// ADDED: Direct call
var loader = FindAnyObjectByType<ClientSceneLoader>();
if (loader != null)
{
    loader.LoadScene(scene, spawnPosition);
}
```

### FIX-002: ClientSceneLoader - Auto-load on Host

```csharp
// ADDED: OnEnable subscription
private void OnEnable()
{
    NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnectedCallback;
}

// ADDED: Auto-load coroutine
private IEnumerator AutoLoadInitialSceneCoroutine()
{
    yield return new WaitForSeconds(1f);
    if (NetworkManager.Singleton.IsHost)
    {
        yield return LoadSceneWithNeighborsCoroutine(new SceneID(0, 0));
    }
}
```

### FIX-003: NetworkPlayerSpawner - Update-based spawn

```csharp
// CHANGED: Start() only subscribes to callback
// Spawn moved to Update() to solve timing issue
private void Update()
{
    if (useScenePlayerAsHost && !_hasSpawnedHostPlayer &&
        NetworkManager.Singleton != null &&
        (NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsServer))
    {
        _hasSpawnedHostPlayer = true;
        SpawnLocalPlayer();
    }
}
```

### FIX-004: BootstrapSceneGenerator - No Coordinator

```csharp
// REMOVED from CreateSceneManagement():
// - SceneTransitionCoordinator
// - NetworkObject (was unused)

private void CreateSceneManagement()
{
    var clientLoader = runtimeObj.AddComponent<ClientSceneLoader>();
    var serverSceneManager = runtimeObj.AddComponent<ServerSceneManager>();
    // SceneTransitionCoordinator REMOVED
}
```

### FIX-005: WorldSceneSetup - No Coordinator

```csharp
// REMOVED: AddSceneTransitionCoordinator call and method
```

### DELETED:

```
Assets/_Project/Scripts/World/Scene/SceneTransitionCoordinator.cs
Assets/_Project/Scripts/World/Scene/SceneTransitionCoordinator.cs.meta
```

---

## Files Changed (Summary)

| File | Change |
|------|--------|
| `ServerSceneManager.cs` | FIX-001: Direct RPC to ClientSceneLoader |
| `ClientSceneLoader.cs` | FIX-002: Auto-load on Host connect |
| `NetworkPlayerSpawner.cs` | FIX-003: Update-based spawn timing |
| `BootstrapSceneGenerator.cs` | FIX-004: No Coordinator created |
| `WorldSceneSetup.cs` | FIX-005: No Coordinator method |
| `SceneTransitionCoordinator.cs` | DELETED |
| `SceneTransitionCoordinator.cs.meta` | DELETED |

---

## Expected Behavior After Fixes

```
[NetworkTestMenu] Select connection mode
[NetworkTestMenu] User clicks Host
[NetworkManagerController] StartHost() called
[ServerSceneManager] ServerSceneManager initialized on server
[NetworkPlayerSpawner] Client connected: 0
[NetworkPlayerSpawner] Host/Server player spawned
[NetworkPlayer] OnNetworkSpawn - IsOwner=true
[ClientSceneLoader] Client connected: 0
[ClientSceneLoader] Auto-loading initial scene for Host: Scene(0, 0)
[ClientSceneLoader] Loading scene: WorldScene_0_0
[SceneManager] LoadSceneAsync Additive: WorldScene_0_0
[SceneManager] LoadSceneAsync Additive: WorldScene_0_1
...
[SceneManager] Scene loaded: WorldScene_0_0
[ClientSceneLoader] Scene loaded: WorldScene_0_0
[NetworkPlayer] Player appears at (39999.5, 0, 39999.5)
```

---

## Graph Relationships

### New Nodes (FIX-001 to FIX-005)

- `server_scene_manager_direct_rpc` - ServerSceneManager calls ClientSceneLoader directly
- `client_scene_loader_auto_load` - Auto-loads scene on Host connect
- `network_player_spawner_update_spawn` - Spawn in Update() to solve timing
- `scene_transition_coordinator_removed` - No longer needed in architecture

### Hyperedges

- **Scene System Integration Fixed** - SceneTransitionCoordinator removed, direct RPC

---

## Related Documents

- `INTEGRATION_FIX_PLAN.md` - Full fix plan with code changes
- `SCENE_ARCHITECTURE_DECISION.md` - Original architecture decision
- `BOOTSTRAP_GENERATOR_FIXES.md` - Generator fixes from 28.04.2026

---

**Author:** Claude Code
**Date:** 29.04.2026
**Type:** Architecture Correction