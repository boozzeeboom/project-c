# BUG ANALYSIS: Scene Loading/Unloading Issues
**Date:** 30.04.2026
**Status:** FIXES APPLIED
**Priority:** P0 - CRITICAL

---

## Исправления 30.04.2026

Все критические исправления внесены:

| Файл | Исправление | Статус |
|------|-------------|--------|
| `ClientSceneLoader.cs` | FIX-1: Sequential Load/Unload + FIX-3: Remove duplicate | ✅ ВНЕСЕНО |
| `ServerSceneManager.cs` | FIX-2: Prevent rapid transitions | ✅ ВНЕСЕНО |
| `BootstrapSceneGenerator.cs` | FIX-4: Cloud position aligned | ✅ ВНЕСЕНО |

---

## Executive Summary

При переходе в сцену 2,0 она не загружается, старые не выгружаются. Лог без ошибок - только warnings о "already unloading during load".

**Root Cause:** Race condition между параллельными операциями Load/Unload в `ClientSceneLoader`.

---

## Log Analysis

### Current Log Output
```
[ClientSceneLoader] Auto-loading initial scene for Host: Scene(0, 0)
[ClientSceneLoader] Scene WorldScene_0_0 was already unloading during load
[ClientSceneLoader] Scene WorldScene_0_1 was already unloading during load
[ClientSceneLoader] Scene WorldScene_1_0 was already unloading during load
[ClientSceneLoader] Scene WorldScene_1_1 was already unloading during load
```

**Interpretation:**
1. Host запущен
2. `ClientSceneLoader.AutoLoadInitialSceneCoroutine` запускает загрузку Scene(0,0) + соседей
3. `LoadSceneWithNeighborsCoroutine` загружает 3x3 сетку
4. После загрузки вызывается `UnloadDistantScenes()` 
5. `UnloadScene()` сразу удаляет сцены из `_loadedScenes`
6. Когда `LoadSceneAsync` проверяет `_loadingScenes.Contains(sceneId)` - сцены уже помечены как "выгружаемые"
7. Результат: сцены 0_0, 0_1, 1_0, 1_1 НЕ добавляются в `_loadedScenes`

---

## Root Causes Identified

### 1. **CRITICAL: Race Condition in LoadSceneWithNeighborsCoroutine**

**File:** `ClientSceneLoader.cs`, lines 283-308

```csharp
private IEnumerator LoadSceneWithNeighborsCoroutine(SceneID center)
{
    var scenesToLoad = sceneRegistry.GetSceneGrid3x3(center);
    var loadTasks = new List<Coroutine>();

    foreach (var sceneId in scenesToLoad)
    {
        if (!_loadedScenes.Contains(sceneId) && !_loadingScenes.Contains(sceneId))
        {
            if (sceneRegistry.IsValid(sceneId))
            {
                loadTasks.Add(StartCoroutine(LoadSceneAsync(sceneId))); // Запускаем параллельно
            }
        }
    }

    foreach (var task in loadTasks)
    {
        yield return task; // Ждём завершения
    }

    if (unloadDistantScenes)
    {
        UnloadDistantScenes(center); // ПРОБЛЕМА: вызывает UnloadScene() для дальних сцен
    }
}
```

**Problem:** `UnloadDistantScenes()` вызывает `UnloadScene()` для сцен, которые МОГУТ быть ещё в процессе загрузки (старые соседи).

**Secondary Problem:** `UnloadScene()` сразу удаляет из `_loadedScenes`:
```csharp
private IEnumerator UnloadSceneCoroutine(SceneID scene)
{
    // ...
    _loadedScenes.Remove(scene); // СРАЗУ удаляем
    // ...
}
```

Но `LoadSceneAsync` проверяет `_loadingScenes.Contains(sceneId)`:
```csharp
if (_loadingScenes.Contains(sceneId))
{
    _loadedScenes.Add(sceneId);  // Добавляем
    _loadingScenes.Remove(sceneId);
}
else
{
    Debug.LogWarning($"[ClientSceneLoader] Scene {sceneName} was already unloading during load");
}
```

### 2. **MEDIUM: Wrong Position Calculation for PlayerSpawner**

**File:** `BootstrapSceneGenerator.cs`, lines 298, 419, 425

```csharp
// Line 298: Camera position
cameraObj.transform.position = new Vector3(COLS * SCENE_SIZE / 2f, 3000f, ROWS * SCENE_SIZE / 2f);
// = (6 * 79999 / 2, 3000, 4 * 79999 / 2) = (239997, 3000, 159998)

// Line 201-203: PlayerSpawner position (CORRECT)
float spawnX = SCENE_SIZE / 2f; // 39999.5 - правильно для Scene(0,0)
float spawnZ = SCENE_SIZE / 2f; // 39999.5
Vector3 spawnPos = new Vector3(spawnX, 3000f, spawnZ);

// Line 419: LowerCloudLayer
layer1.transform.localPosition = new Vector3(COLS * SCENE_SIZE / 2f, 1500f, ROWS * SCENE_SIZE / 2f);
// = (239997, 1500, 159998) - В CENTRE мира, НЕ в Scene(0,0)

// Line 425: UpperCloudLayer  
layer2.transform.localPosition = new Vector3(COLS * SCENE_SIZE / 2f, 3000f, ROWS * SCENE_SIZE / 2f);
// = (239997, 3000, 159998) - В CENTRE мира
```

**Impact:** CloudSystem создаётся в центре мира (239997, 3000, 159998), а игрок спавнится в Scene(0,0) center (39999.5, 3000, 39999.5). Clouds не будут видны игроку пока он не долетит до центра.

### 3. **MEDIUM: SceneTransition Flow Broken**

**Flow:**
1. ServerSceneManager.TransitionClient() → RPC to Client
2. Client receives LoadSceneTransitionClientRpc
3. ClientSceneLoader.LoadScene() starts
4. BUT: Server still tracking old position because _playerTransforms not updated

**Root cause in ServerSceneManager.cs:**
```csharp
private void TransitionClient(ulong clientId, SceneID from, SceneID to)
{
    // ... hides old scene objects ...
    _clientSceneMap[clientId] = to;
    
    if (_playerTransforms.TryGetValue(clientId, out var playerTransform))
    {
        // RPC sent to client with new scene
        // BUT: Server doesn't update its tracking of player position
    }
}
```

**Problem:** After `TransitionClient()`, server keeps tracking player's world position. When player moves within new scene, `CheckSceneTransition` is called again and may trigger another transition immediately because `_clientSceneMap` is updated but player is still at old world coordinates.

---

## Architecture Summary

```
BootstrapScene (persistent)
├── NetworkManager + NetworkManagerController + ServerSceneManager
├── Runtime (DontDestroyOnLoad)
│   └── ClientSceneLoader
├── PlayerSpawner (NetworkPlayer prefab) at (39999.5, 3000, 39999.5)
├── MainCamera at (239997, 3000, 159998)
├── CloudSystem at (239997, 3000, 159998)
├── AltitudeCorridorSystem
└── NetworkTestCanvas + NetworkTestMenu

WorldScene_X_Y (24 scenes, additive loaded)
├── WorldRoot
│   ├── Mountains
│   ├── Clouds
│   ├── Farms
│   └── TradeZones
└── Runtime objects per scene
```

---

## Fixes Required

### FIX-1: Sequential Load/Unload (CRITICAL)

**File:** `ClientSceneLoader.cs`

**Problem:** Race condition between Load and Unload

**Solution:** Execute Unload ONLY AFTER all Loads complete, and use proper synchronization:

```csharp
private IEnumerator LoadSceneWithNeighborsCoroutine(SceneID center)
{
    var scenesToLoad = sceneRegistry.GetSceneGrid3x3(center);
    var loadTasks = new List<Coroutine>();

    foreach (var sceneId in scenesToLoad)
    {
        if (!_loadedScenes.Contains(sceneId) && !_loadingScenes.Contains(sceneId))
        {
            if (sceneRegistry.IsValid(sceneId))
            {
                loadTasks.Add(StartCoroutine(LoadSceneAsync(sceneId)));
            }
        }
    }

    foreach (var task in loadTasks)
    {
        yield return task;
    }

    // FIX-1: Unload AFTER all loads complete
    if (unloadDistantScenes)
    {
        yield return UnloadDistantScenesCoroutine(center);
    }
}

private IEnumerator UnloadDistantScenesCoroutine(SceneID center)
{
    var keepScenes = sceneRegistry.GetSceneGrid5x5(center);
    var unloadTasks = new List<Coroutine>();

    foreach (var loaded in _loadedScenes.ToList())
    {
        bool shouldKeep = false;
        foreach (var keep in keepScenes)
        {
            if (loaded.Equals(keep))
            {
                shouldKeep = true;
                break;
            }
        }

        if (!shouldKeep)
        {
            // Queue unload coroutine
            unloadTasks.Add(StartCoroutine(UnloadSceneCoroutine(loaded)));
        }
    }

    // Wait for all unloads to complete
    foreach (var task in unloadTasks)
    {
        yield return task;
    }
}
```

**Also fix UnloadScene to not remove from _loadedScenes until actually unloaded:**

```csharp
private IEnumerator UnloadSceneCoroutine(SceneID scene)
{
    string sceneName = sceneRegistry.GetSceneName(scene);

    if (!_loadedScenes.Contains(scene))
    {
        yield break;
    }

    LogDebug($"Unloading scene: {sceneName}");

    var asyncOp = SceneManager.UnloadSceneAsync(sceneName);

    while (!asyncOp != null && !asyncOp.isDone)
    {
        yield return null;
    }

    // FIX-1: Remove AFTER successful unload
    _loadedScenes.Remove(scene);

    LogDebug($"Scene unloaded: {sceneName}");
    OnSceneUnloaded?.Invoke(scene);
}
```

### FIX-2: Prevent Re-triggering Transition (MEDIUM)

**File:** `ServerSceneManager.cs`

**Add flag to prevent immediate re-transition:**

```csharp
private readonly Dictionary<ulong, float> _lastTransitionTimes = new Dictionary<ulong, float>();
private const float MIN_TRANSITION_INTERVAL = 1.0f; // Minimum 1 second between transitions

private void TransitionClient(ulong clientId, SceneID from, SceneID to)
{
    // Check if we recently transitioned this client
    if (_lastTransitionTimes.TryGetValue(clientId, out float lastTime))
    {
        if (Time.time - lastTime < MIN_TRANSITION_INTERVAL)
        {
            LogDebug($"Skipping rapid transition for client {clientId}");
            return;
        }
    }

    _lastTransitionTimes[clientId] = Time.time;
    // ... rest of transition logic
}
```

### FIX-3: Remove Duplicate Scene Loading (MEDIUM)

**File:** `ClientSceneLoader.cs`

**Problem:** `AutoLoadInitialSceneCoroutine` AND `OnClientConnectedCallback` BOTH load scenes:

```csharp
// Line 98-113: AutoLoadInitialSceneCoroutine
private IEnumerator AutoLoadInitialSceneCoroutine()
{
    yield return new WaitForSeconds(1f);
    if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost)
    {
        SceneID initialScene = new SceneID(0, 0);
        Debug.Log($"[ClientSceneLoader] Auto-loading initial scene for Host: {initialScene}");
        yield return LoadSceneWithNeighborsCoroutine(initialScene); // LOADS SCENE
    }
}

// Line 90-96: OnClientConnectedCallback
private void OnClientConnectedCallback(ulong clientId)
{
    if (clientId == NetworkManager.Singleton.LocalClientId && _currentScene.Equals(default))
    {
        StartCoroutine(AutoLoadInitialSceneCoroutine()); // ALSO LOADS SCENE
    }
}
```

**Solution:** Remove duplicate - use only `OnClientConnectedCallback`:

```csharp
private void Start()
{
    FindLocalPlayer();
    
    // REMOVED: AutoLoadInitialSceneCoroutine() call
    // Now handled by OnClientConnectedCallback
}

// FIX-3: Ensure we only load once
private void OnClientConnectedCallback(ulong clientId)
{
    // If this is our local client and we haven't loaded any scene yet
    if (clientId == NetworkManager.Singleton.LocalClientId && 
        _currentScene.Equals(default) &&
        !_isLoadingInitialScene)
    {
        _isLoadingInitialScene = true;
        StartCoroutine(AutoLoadInitialSceneCoroutine());
    }
}

private bool _isLoadingInitialScene = false;

private IEnumerator AutoLoadInitialSceneCoroutine()
{
    yield return new WaitForSeconds(1f);

    if (!_currentScene.Equals(default))
    {
        _isLoadingInitialScene = false;
        yield break;
    }

    if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost)
    {
        SceneID initialScene = new SceneID(0, 0);
        Debug.Log($"[ClientSceneLoader] Auto-loading initial scene for Host: {initialScene}");
        yield return LoadSceneWithNeighborsCoroutine(initialScene);
    }
    
    _isLoadingInitialScene = false;
}
```

### FIX-4: Align Cloud System Position (LOW)

**File:** `BootstrapSceneGenerator.cs`

```csharp
// Change from world center to Scene(0,0) center
// Line 419:
layer1.transform.localPosition = new Vector3(SCENE_SIZE / 2f, 1500f, SCENE_SIZE / 2f);

// Line 425:
layer2.transform.localPosition = new Vector3(SCENE_SIZE / 2f, 3000f, SCENE_SIZE / 2f);
```

---

## Files to Modify

| File | Priority | Changes |
|------|----------|---------|
| `ClientSceneLoader.cs` | P0 | FIX-1 (sequential load/unload), FIX-3 (remove duplicate) |
| `ServerSceneManager.cs` | P1 | FIX-2 (prevent rapid transitions) |
| `BootstrapSceneGenerator.cs` | P2 | FIX-4 (align cloud positions) |

---

## Testing Checklist

After fixes:

- [ ] Start Host → Scene(0,0) + 8 neighbors load without warnings
- [ ] Move to scene boundary → New scene loads
- [ ] Old scenes unload properly
- [ ] No "already unloading during load" warnings
- [ ] Multiple rapid transitions don't cause issues
- [ ] CloudSystem visible near player spawn point

---

## Related Documents

- `docs/world/LargeScaleMMO/2_iteration_scene-mode/INTEGRATION_FIX_PLAN.md`
- `docs/world/LargeScaleMMO/2_iteration_scene-mode/SCENE_TRANSITION_ANALYSIS.md`
- `docs/world/LargeScaleMMO/2_iteration_scene-mode/CORRECTED_ARCHITECTURE.md`
