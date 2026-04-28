using Unity.Netcode;
using UnityEngine;

namespace ProjectC.World.Scene
{
    /// <summary>
    /// NetworkBehaviour компонент для привязки NetworkObject к сцене.
    /// Использует CheckObjectVisibility для фильтрации при спавне.
    /// Использует NetworkHide/NetworkShow для runtime переходов между сценами.
    /// </summary>
    public class SceneBoundNetworkObject : NetworkBehaviour
    {
        [Header("Scene Ownership")]
        [Tooltip("ID сцены которой принадлежит этот объект. Устанавливается автоматически или вручную.")]
        [SerializeField] private SceneID _ownedScene;

        [Tooltip("Автоматически определять сцену по мировой позиции при спавне.")]
        [SerializeField] private bool _autoDetectScene = true;

        [Header("Debug")]
        [SerializeField] private bool _showDebugLogs = false;

        private ServerSceneManager _serverSceneManager;
        private bool _isRegistered = false;

        public SceneID OwnedScene => _ownedScene;

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                if (_autoDetectScene && _ownedScene.Equals(default))
                {
                    _ownedScene = SceneID.FromWorldPosition(transform.position);
                    LogDebug($"Auto-detected scene: {_ownedScene}");
                }

                _serverSceneManager = FindAnyObjectByType<ServerSceneManager>();

                if (_serverSceneManager == null)
                {
                    Debug.LogWarning($"[SceneBoundNetworkObject] ServerSceneManager not found! Object {gameObject.name} will use default visibility.");
                    NetworkObject.CheckObjectVisibility = _ => true;
                }
                else
                {
                    NetworkObject.CheckObjectVisibility = ShouldClientSeeObject;
                    _serverSceneManager.RegisterSceneObject(_ownedScene, NetworkObject);
                    _isRegistered = true;
                }

                LogDebug($"SceneBoundNetworkObject spawned in scene {_ownedScene}");
            }

            base.OnNetworkSpawn();
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer && _isRegistered && _serverSceneManager != null)
            {
                _serverSceneManager.UnregisterSceneObject(_ownedScene, NetworkObject);
                _isRegistered = false;
            }

            base.OnNetworkDespawn();
        }

        private bool ShouldClientSeeObject(ulong clientId)
        {
            if (_serverSceneManager == null)
                return true;

            SceneID clientScene = _serverSceneManager.GetClientScene(clientId);
            bool shouldSee = clientScene.Equals(_ownedScene);

            LogDebug($"CheckObjectVisibility: Client {clientId} in scene {clientScene}, object in scene {_ownedScene}, visible={shouldSee}");

            return shouldSee;
        }

        /// <summary>
        /// Установить сцену вручную. Вызывать ДО спавна.
        /// </summary>
        public void SetScene(SceneID scene)
        {
            _ownedScene = scene;
            _autoDetectScene = false;
        }

        /// <summary>
        /// Проверить, может ли клиент видеть этот объект в данный момент.
        /// </summary>
        public bool CanClientSee(ulong clientId)
        {
            if (_serverSceneManager == null)
                return true;

            SceneID clientScene = _serverSceneManager.GetClientScene(clientId);
            return clientScene.Equals(_ownedScene);
        }

        private void LogDebug(string message)
        {
            if (_showDebugLogs)
                Debug.Log($"[SceneBoundNetworkObject:{gameObject.name}] {message}");
        }
    }
}