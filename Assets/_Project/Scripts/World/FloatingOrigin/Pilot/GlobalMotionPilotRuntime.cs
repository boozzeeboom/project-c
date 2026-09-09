using Unity.Netcode;
using UnityEngine;
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
        private bool _startedByPilot;

        private void Start()
        {
            if (!_autoStartHost || !Application.isPlaying) return;
            StartPilotHost();
        }

        public void StartPilotHost()
        {
            if (_startedByPilot) return;
            _manager = GetComponent<NetworkManager>();
            if (_manager == null || _profile == null || _bootstrap == null)
            {
                Debug.LogError("[T-FO06L] Pilot host missing NetworkManager, profile or bootstrap.", this);
                return;
            }
            if (!GlobalMotionNetworkStartup.TryPrepare(_manager, _profile, _bootstrap, GlobalMotionStartRole.Host, out _startup, out var error))
            {
                Debug.LogError("[T-FO06L] Pilot global startup refused: " + error, this);
                return;
            }
            if (!_manager.StartHost())
            {
                _startup.CancelBeforeStart();
                _startup = null;
                Debug.LogError("[T-FO06L] Pilot host failed to start.", this);
                return;
            }
            _startedByPilot = true;
        }

        private void OnDestroy()
        {
            if (_startup != null && _manager != null && !_manager.IsListening)
                _startup.CancelBeforeStart();
        }
    }
}
