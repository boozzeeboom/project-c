using UnityEngine;
using Unity.Netcode;
using UnityEngine.SceneManagement;
using ProjectC.World.Scene;

namespace ProjectC.World.Scene
{
    /// <summary>
    /// Серверный спавнер scene-placed NetworkObject.
    ///
    /// Проблема: у некоторых scene-placed NetworkObject InScenePlacedSourceGlobalObjectIdHash == 0
    /// (когда объект добавлен в сцену вручную, не через префаб/не при первой сериализации сцены).
    /// В этом случае NGO через NetworkSceneManager НЕ спавнит их автоматически.
    /// Любой RPC на таких объектах → NRE в __endSendRpc (см. INTEGRATION_SHIPS_TO_WORLD_0_0.md).
    ///
    /// Решение: этот скрипт на сервере при загрузке каждой стриминговой сцены
    /// находит все NetworkObject с !IsSpawned и вызывает Spawn() вручную.
    /// Использует GlobalObjectIdHash (он есть у всех NetworkObject).
    ///
    /// Ссылка: docs/dev/INTEGRATION_SHIPS_TO_WORLD_0_0.md
    /// </summary>
    public class ScenePlacedObjectSpawner : MonoBehaviour
    {
        [Header("Debug")]
        [SerializeField] private bool showDebugLogs = true;

        private void Start()
        {
            var loader = ClientSceneLoader.Instance;
            if (loader != null)
            {
                loader.OnSceneLoaded += HandleSceneLoaded;
                if (showDebugLogs)
                    Debug.Log("[ScenePlacedObjectSpawner] Subscribed to ClientSceneLoader.OnSceneLoaded");
            }
            else
            {
                Debug.LogWarning("[ScenePlacedObjectSpawner] ClientSceneLoader.Instance not found at Start(). Will retry on first scene load.");
                // Повторная попытка через кадр (на случай если ClientSceneLoader инициализируется позже)
                StartCoroutine(RetrySubscribe());
            }

            // T-E04 FIX: подпишемся на OnServerStarted — на случай если [ExchangeServer] в BootstrapScene
            // не был спавнут в Start() (тогда IsServer ещё false). После StartHost нужно повторно
            // попытаться спавнить scene-placed NetworkObject в уже загруженных сценах.
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnServerStarted += HandleServerStarted;
                if (showDebugLogs)
                    Debug.Log("[ScenePlacedObjectSpawner] Subscribed to NetworkManager.OnServerStarted");
            }
            else
            {
                // NWM может быть ещё не готов — попробуем через кадр
                StartCoroutine(SubscribeToNetworkManager());
            }

            // Также спавним объекты в уже загруженных сценах (на случай если сцена загружена до нашего Start)
            SpawnInAllLoadedScenes();
        }

        private System.Collections.IEnumerator SubscribeToNetworkManager()
        {
            yield return null;
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnServerStarted += HandleServerStarted;
                if (showDebugLogs)
                    Debug.Log("[ScenePlacedObjectSpawner] Subscribed to NetworkManager.OnServerStarted (retry)");
            }
        }

        private void HandleServerStarted()
        {
            if (showDebugLogs)
                Debug.Log("[ScenePlacedObjectSpawner] HandleServerStarted: re-spawn scene-placed NetworkObjects");
            // После StartHost: ещё раз пробежимся по загруженным сценам (BootstrapScene в частности),
            // потому что при Start() мы могли не заспавнить (IsServer был false).
            SpawnInAllLoadedScenes();
        }

        private System.Collections.IEnumerator RetrySubscribe()
        {
            yield return null; // один кадр
            var loader = ClientSceneLoader.Instance;
            if (loader != null)
            {
                loader.OnSceneLoaded += HandleSceneLoaded;
                if (showDebugLogs)
                    Debug.Log("[ScenePlacedObjectSpawner] Subscribed (retry) to ClientSceneLoader.OnSceneLoaded");
                SpawnInAllLoadedScenes();
            }
        }

        private void OnDestroy()
        {
            var loader = ClientSceneLoader.Instance;
            if (loader != null)
            {
                loader.OnSceneLoaded -= HandleSceneLoaded;
            }
        }

        private void HandleSceneLoaded(SceneID sceneId)
        {
            // Только на сервере
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;

            SpawnInScene(sceneId);
        }

        private void SpawnInAllLoadedScenes()
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;

            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded) continue;

                // Извлечь SceneID из имени "WorldScene_X_Z" (если это WorldScene)
                // Иначе — спавним всё в сцене (например, BootstrapScene содержит [InventoryServer], [MarketServer], [ContractServer])
                bool isWorldScene = scene.name.StartsWith("WorldScene_");
                SceneID sceneId = default;
                if (isWorldScene)
                {
                    var parts = scene.name.Substring("WorldScene_".Length).Split('_');
                    if (parts.Length != 2) continue;
                    if (!int.TryParse(parts[0], out int gridX) || !int.TryParse(parts[1], out int gridZ)) continue;
                    sceneId = new SceneID(gridX, gridZ);
                    SpawnInScene(sceneId);
                }
                else
                {
                    // Для не-WorldScene сцен (BootstrapScene) — спавним ВСЕ scene-placed NetworkObject
                    // вручную, потому что NGO scene manager их не спавнит (InScenePlacedSourceGlobalObjectIdHash=0).
                    // FIX 2026-06-05: [InventoryServer] в BootstrapScene требовал этого.
                    int spawned = 0, already = 0, failed = 0;
                    foreach (var root in scene.GetRootGameObjects())
                    {
                        var networkObjects = root.GetComponentsInChildren<NetworkObject>(true);
                        foreach (var netObj in networkObjects)
                        {
                            if (netObj == null) continue;
                            if (netObj.IsSpawned) { already++; continue; }
                            try
                            {
                                netObj.Spawn(destroyWithScene: false);
                                spawned++;
                            }
                            catch (System.Exception ex)
                            {
                                failed++;
                                if (showDebugLogs) Debug.LogWarning($"[ScenePlacedObjectSpawner] Failed to spawn {netObj.name} in {scene.name}: {ex.Message}");
                            }
                        }
                    }
                    if (showDebugLogs && (spawned > 0 || already > 0 || failed > 0))
                    {
                        Debug.Log($"[ScenePlacedObjectSpawner] Scene {scene.name}: spawned={spawned}, already={already}, failed={failed}");
                    }
                }
            }
        }

        private void SpawnInScene(SceneID sceneId)
        {
            var scene = SceneManager.GetSceneByName($"WorldScene_{sceneId.GridX}_{sceneId.GridZ}");
            if (!scene.isLoaded)
            {
                if (showDebugLogs) Debug.LogWarning($"[ScenePlacedObjectSpawner] Scene {sceneId} is not loaded, skipping spawn");
                return;
            }

            int spawnedCount = 0;
            int alreadySpawnedCount = 0;
            int failedCount = 0;

            foreach (var root in scene.GetRootGameObjects())
            {
                var networkObjects = root.GetComponentsInChildren<NetworkObject>(true);
                foreach (var netObj in networkObjects)
                {
                    if (netObj == null) continue;

                    if (netObj.IsSpawned)
                    {
                        alreadySpawnedCount++;
                        continue;
                    }

                    try
                    {
                        // destroyWithScene: true — стандарт NGO, объект исчезнет при unload сцены
                        netObj.Spawn(destroyWithScene: true);
                        spawnedCount++;
                    }
                    catch (System.Exception ex)
                    {
                        failedCount++;
                        Debug.LogWarning($"[ScenePlacedObjectSpawner] Failed to spawn {netObj.name} in scene {sceneId}: {ex.Message}");
                    }
                }
            }

            if (showDebugLogs && (spawnedCount > 0 || failedCount > 0))
            {
                Debug.Log($"[ScenePlacedObjectSpawner] Scene {sceneId}: spawned={spawnedCount}, already={alreadySpawnedCount}, failed={failedCount}");
            }
        }
    }
}
