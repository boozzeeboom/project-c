using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using ProjectC.World.FloatingOrigin.Network;

namespace ProjectC.World.FloatingOrigin.Pilot
{
    /// <summary>
    /// Test-only host launcher for the first global pilot. It starts the existing main player through the global
    /// bootstrap and leaves legacy NetworkManager startup untouched when this component is removed.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GlobalMotionPilotRuntime : MonoBehaviour
    {
        [SerializeField] private GlobalMotionNetworkProfile _profile;
        [SerializeField] private GlobalMotionPlayerBootstrap _bootstrap;
        [SerializeField] private bool _autoStartHost = true;

        private GlobalMotionNetworkStartup _startup;
        private NetworkManager _manager;
        private Coroutine _startRoutine;
        private bool _startedByPilot;
        private bool _startRequested;

        private void Start()
        {
            if (_autoStartHost && Application.isPlaying) StartPilotHost();
        }

        public void StartPilotHost()
        {
            if (_startedByPilot || _startRequested) return;
            _startRequested = true;
            _startRoutine = StartCoroutine(PrepareAndStartHost());
        }

        private IEnumerator PrepareAndStartHost()
        {
            _manager = GetComponent<NetworkManager>();
            if (_manager == null || _profile == null || _bootstrap == null)
            {
                _startRequested = false;
                Debug.LogError("[T-FO06L] Pilot host missing NetworkManager, profile or bootstrap.", this);
                yield break;
            }

            // Retire callbacks/coroutines before our first additive load, not just before NGO starts.
            if (!RetireLegacySceneLoadersForPilot())
            {
                _startRequested = false;
                yield break;
            }

            const string worldScenePath = "Assets/_Project/Scenes/World/WorldScene_0_0.unity";
            var worldScene = SceneManager.GetSceneByPath(worldScenePath);
            if (!worldScene.IsValid() || !worldScene.isLoaded)
            {
                var load = SceneManager.LoadSceneAsync(worldScenePath, LoadSceneMode.Additive);
                if (load == null)
                {
                    _startRequested = false;
                    Debug.LogError("[T-FO06L] Pilot world scene load was not created: " + worldScenePath, this);
                    yield break;
                }
                while (!load.isDone) yield return null;
                worldScene = SceneManager.GetSceneByPath(worldScenePath);
            }

            if (!worldScene.IsValid() || !worldScene.isLoaded)
            {
                _startRequested = false;
                Debug.LogError("[T-FO06L] Pilot world scene is not loaded: " + worldScenePath, this);
                yield break;
            }

            if (!RejectCatalogedSceneRootsFromDontDestroyOnLoad(out var restoreError))
            {
                _startRequested = false;
                Debug.LogError("[T-FO06L] Pilot could not restore cataloged scene roots from DontDestroyOnLoad: " + restoreError, this);
                yield break;
            }

            if (!RetireLegacySceneLoadersForPilot())
            {
                _startRequested = false;
                Debug.LogError("[T-FO06L] Pilot could not retire the legacy scene loader through the content bridge.", this);
                yield break;
            }

            var source = GetComponent<GlobalMotionPilotSpawnSource>();
            if (source == null || !source.RefreshPreparedContent())
            {
                _startRequested = false;
                Debug.LogError("[T-FO06L] Pilot spawn source could not prepare the loaded world scene.", this);
                yield break;
            }

            // Content preparation may execute legacy Awake/OnEnable paths that re-parent authored roots.
            // Repeat the cataloged DDOL restoration immediately before the native preflight boundary.
            if (!RejectCatalogedSceneRootsFromDontDestroyOnLoad(out restoreError))
            {
                _startRequested = false;
                Debug.LogError("[T-FO06L] Pilot could not restore cataloged scene roots after content preparation: " + restoreError, this);
                yield break;
            }

            if (!GlobalMotionNetworkStartup.TryPrepare(_manager, _profile, _bootstrap, GlobalMotionStartRole.Host, out _startup, out var error))
            {
                _startRequested = false;
                Debug.LogError("[T-FO06L] Pilot global startup refused: " + error, this);
                yield break;
            }

            if (!_manager.StartHost())
            {
                _startup.CancelBeforeStart();
                _startup = null;
                _startRequested = false;
                Debug.LogError("[T-FO06L] Pilot host failed to start.", this);
                yield break;
            }

            _startedByPilot = true;
            _startRequested = false;
            yield return CloseStartupMenusWhenLocalPlayerReady();
            _startRoutine = null;
        }

        private IEnumerator CloseStartupMenusWhenLocalPlayerReady()
        {
            // This launcher bypasses the menu button handlers that normally call Hide().
            // Connection callbacks are too early: the global bootstrap spawns the player later.
            // Wait a frame as well so MainMenuWindow.Start/Show has already run.
            yield return null;
            double deadline = Time.realtimeSinceStartupAsDouble + 60d;
            while (_manager != null && _manager == NetworkManager.Singleton && _manager.IsHost &&
                !_manager.ShutdownInProgress && _startup != null && !_startup.IsDisposed &&
                GlobalMotionNetworkStartup.IsInstalled(_manager))
            {
                if (_bootstrap != null && _bootstrap.CanAcceptScenePeer &&
                    _manager.ConnectedClients.TryGetValue(_manager.LocalClientId, out var client))
                {
                    var playerObject = client.PlayerObject;
                    if (playerObject != null && playerObject.IsSpawned && playerObject.IsPlayerObject &&
                        playerObject.IsOwner && playerObject.NetworkManager == _manager)
                    {
                        var player = playerObject.GetComponent<ProjectC.Player.NetworkPlayer>();
                        var adapter = playerObject.GetComponent<GlobalMotionPoseAdapter>();
                        if (player != null && player.UsesGlobalCoordinates && player.CanSimulateInCurrentCoordinates &&
                            adapter != null && adapter.IsBaselineReady)
                        {
                            int hiddenMenus = CloseStartupMenus();
                            var origin = adapter.Frame.Coordinates.Origin;
                            if (hiddenMenus == 0)
                                Debug.LogWarning("[T-FO06L] Local player ready, but no active Bootstrap startup menus matched; UI handoff was not confirmed.", this);
                            else
                                Debug.Log("[T-FO06L] Local pilot player ready; startup menus hidden=" + hiddenMenus + ";origin=(" +
                                    origin.X + "," + origin.Y + "," + origin.Z + ");local=" + playerObject.transform.position, this);
                            yield break;
                        }
                    }
                }
                if (Time.realtimeSinceStartupAsDouble >= deadline)
                {
                    Debug.LogWarning("[T-FO06L] Startup menus left visible: local pilot player did not become ready within 60s.", this);
                    yield break;
                }
                yield return null;
            }
            // Failed/disposed startup must not hide the only way back to the menu.
        }

        private int CloseStartupMenus()
        {
            int hiddenMenus = 0;
            var bootstrapScene = SceneManager.GetSceneByPath("Assets/_Project/Scenes/BootstrapScene.unity");
            foreach (var menu in UnityEngine.Object.FindObjectsByType<ProjectC.UI.MainMenu.MainMenuWindow>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (!menu.isActiveAndEnabled || menu.gameObject.scene != bootstrapScene) continue;
                menu.EnsureBuilt();
                menu.Hide();
                hiddenMenus++;
            }
            foreach (var menu in UnityEngine.Object.FindObjectsByType<ProjectC.UI.NetworkTestMenu>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (!menu.isActiveAndEnabled || menu.gameObject.scene != bootstrapScene) continue;
                menu.Hide();
                hiddenMenus++;
            }
            // One-shot startup handoff only. Do not close gameplay/Esc/settings panels or take over cursor policy.
            return hiddenMenus;
        }

        private bool RetireLegacySceneLoadersForPilot()
        {
            int retired = 0;
            var loaders = UnityEngine.Object.FindObjectsByType<ProjectC.World.Scene.ClientSceneLoader>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var loader in loaders)
            {
                if (loader == null) continue;
                bool alreadyRetired = loader.IsRetiredForGlobalPilot;
                if (!loader.TryRetireForGlobalPilot(out var error))
                {
                    Debug.LogError("[T-FO06L] Legacy loader handoff refused: " + loader.name + ";" + error, this);
                    return false;
                }
                if (!alreadyRetired) retired++;
            }

            if (retired > 0)
                Debug.Log("[T-FO06L] Retired " + retired + " legacy scene loader(s): network callbacks detached, coroutines stopped, load requests blocked for the pilot lifetime.", this);
            return true;
        }

        private static bool IsDontDestroyOnLoadScene(UnityEngine.SceneManagement.Scene scene)
        {
            // Unity exposes the DDOL pseudo-scene without an asset path; its name is empty in some editor/runtime versions.
            return scene.IsValid() &&
                (string.IsNullOrEmpty(scene.path) || string.Equals(scene.path, "DontDestroyOnLoad", System.StringComparison.Ordinal)) &&
                (string.IsNullOrEmpty(scene.name) || string.Equals(scene.name, "DontDestroyOnLoad", System.StringComparison.Ordinal));
        }

        private bool RejectCatalogedSceneRootsFromDontDestroyOnLoad(out string error)
        {
            error = null;
            if (_profile == null || _profile.SceneCatalog == null)
            {
                error = "profile_or_scene_catalog_missing";
                return false;
            }

            const string bootstrapPath = "Assets/_Project/Scenes/BootstrapScene.unity";
            var bootstrapScene = SceneManager.GetSceneByPath(bootstrapPath);
            if (!bootstrapScene.IsValid() || !bootstrapScene.isLoaded)
            {
                var activeScene = SceneManager.GetActiveScene();
                var managerScene = gameObject.scene;
                error = "cataloged_bootstrap_scene_not_loaded;expectedPath=" + bootstrapPath +
                    ";activePath=" + (activeScene.IsValid() ? activeScene.path : "<invalid>") +
                    ";managerScene=" + (managerScene.IsValid() ? managerScene.path : "<invalid>");
                return false;
            }

            var loadedScenesByGuid = new System.Collections.Generic.Dictionary<string, UnityEngine.SceneManagement.Scene>(System.StringComparer.Ordinal);
            var sourceSceneById = new System.Collections.Generic.Dictionary<string, string>(System.StringComparer.Ordinal);
            foreach (var scene in _profile.SceneCatalog.Data.scenes)
            {
                if (scene == null || string.IsNullOrEmpty(scene.sceneGuid) || string.IsNullOrEmpty(scene.assetPath)) continue;
                var loaded = SceneManager.GetSceneByPath(scene.assetPath);
                if (loaded.IsValid() && loaded.isLoaded) loadedScenesByGuid[scene.sceneGuid] = loaded;
            }
            foreach (var entry in _profile.SceneCatalog.Data.entries)
            {
                if (entry == null || string.IsNullOrEmpty(entry.sourceId) || string.IsNullOrEmpty(entry.sceneGuid)) continue;
                if (sourceSceneById.TryGetValue(entry.sourceId, out var previousGuid) && previousGuid != entry.sceneGuid)
                {
                    error = "catalog_source_spans_scene_guids:" + entry.sourceId;
                    return false;
                }
                sourceSceneById[entry.sourceId] = entry.sceneGuid;
            }

            var roots = new System.Collections.Generic.Dictionary<GameObject, UnityEngine.SceneManagement.Scene>();
            int ddolMarkerCount = 0;
            int catalogedDdolMarkerCount = 0;
            var markers = UnityEngine.Resources.FindObjectsOfTypeAll<GlobalSceneSourceMarker>();
            foreach (var marker in markers)
            {
                if (marker == null || !IsDontDestroyOnLoadScene(marker.gameObject.scene)) continue;
                ddolMarkerCount++;
                if (!sourceSceneById.TryGetValue(marker.SourceId, out var sceneGuid)) continue;
                catalogedDdolMarkerCount++;
                if (!loadedScenesByGuid.TryGetValue(sceneGuid, out var targetScene))
                {
                    error = "cataloged_ddol_source_scene_not_loaded;sourceId=" + marker.SourceId + ";sceneGuid=" + sceneGuid;
                    return false;
                }

                var root = marker.transform.root.gameObject;
                if (!IsDontDestroyOnLoadScene(root.scene)) continue;
                if (roots.TryGetValue(root, out var previousScene) && previousScene != targetScene)
                {
                    error = "ddol_root_contains_multiple_cataloged_scene_guids;root=" + root.name +
                        ";firstScene=" + previousScene.path + ";secondScene=" + targetScene.path;
                    return false;
                }
                roots[root] = targetScene;
            }

            Debug.Log("[T-FO06L] Cataloged DDOL audit: markers=" + ddolMarkerCount +
                ";catalogedMarkers=" + catalogedDdolMarkerCount + ";roots=" + roots.Count + ";restored=0", this);
            if (roots.Count > 0)
            {
                var descriptions = new System.Collections.Generic.List<string>();
                foreach (var pair in roots) descriptions.Add(pair.Key.name + "->" + pair.Value.path);
                error = "cataloged_source_in_ddol;restoration_forbidden;roots=" + string.Join("|", descriptions);
                return false;
            }
            return true;
        }

        private void OnDestroy()
        {
            if (_startup != null && _manager != null && !_manager.IsListening)
                _startup.CancelBeforeStart();
        }
    }
}
