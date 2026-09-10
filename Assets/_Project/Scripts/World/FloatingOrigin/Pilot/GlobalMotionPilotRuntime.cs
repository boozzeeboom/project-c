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

            if (!RestoreBootstrapSceneRootsFromDontDestroyOnLoad())
            {
                _startRequested = false;
                Debug.LogError("[T-FO06L] Pilot could not restore cataloged Bootstrap roots from DontDestroyOnLoad.", this);
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
            _startRoutine = null;
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

        private bool RestoreBootstrapSceneRootsFromDontDestroyOnLoad()
        {
            if (_profile == null || _profile.SceneCatalog == null) return false;

            var bootstrapScene = SceneManager.GetSceneByPath("Assets/_Project/Scenes/BootstrapScene.unity");
            if (!bootstrapScene.IsValid() || !bootstrapScene.isLoaded) return false;

            string bootstrapGuid = null;
            foreach (var scene in _profile.SceneCatalog.Data.scenes)
            {
                if (scene != null && scene.assetPath == bootstrapScene.path)
                {
                    bootstrapGuid = scene.sceneGuid;
                    break;
                }
            }
            if (string.IsNullOrEmpty(bootstrapGuid)) return false;

            var catalogedBootstrapSources = new System.Collections.Generic.HashSet<string>(System.StringComparer.Ordinal);
            foreach (var entry in _profile.SceneCatalog.Data.entries)
                if (entry != null && entry.sceneGuid == bootstrapGuid)
                    catalogedBootstrapSources.Add(entry.sourceId);

            var roots = new System.Collections.Generic.HashSet<GameObject>();
            var markers = UnityEngine.Object.FindObjectsByType<GlobalSceneSourceMarker>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var marker in markers)
            {
                if (marker == null || marker.gameObject.scene.name != "DontDestroyOnLoad" ||
                    !catalogedBootstrapSources.Contains(marker.SourceId)) continue;
                var root = marker.transform.root.gameObject;
                if (root.scene.name == "DontDestroyOnLoad") roots.Add(root);
            }

            int restored = 0;
            foreach (var root in roots)
            {
                SceneManager.MoveGameObjectToScene(root, bootstrapScene);
                restored++;
            }
            if (restored > 0)
                Debug.Log("[T-FO06L] Restored " + restored + " cataloged Bootstrap root(s) from DontDestroyOnLoad before native preparation.", this);
            return true;
        }

        private void OnDestroy()
        {
            if (_startup != null && _manager != null && !_manager.IsListening)
                _startup.CancelBeforeStart();
        }
    }
}
