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

        private void OnDestroy()
        {
            if (_startup != null && _manager != null && !_manager.IsListening)
                _startup.CancelBeforeStart();
        }
    }
}
