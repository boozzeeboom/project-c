using Unity.Netcode;
using UnityEngine;

namespace ProjectC.World.Scene
{
    /// <summary>
    /// NetworkBehaviour wrapper для клиента - получает RPCs от ServerSceneManager
    /// и передаёт их в ClientSceneLoader для выполнения загрузки/выгрузки.
    /// Также подтверждает загрузку сцен обратно на сервер.
    /// </summary>
    public class SceneTransitionCoordinator : NetworkBehaviour
    {
        [Header("References")]
        [SerializeField] private ClientSceneLoader clientSceneLoader;

        private void Awake()
        {
            if (clientSceneLoader == null)
            {
                clientSceneLoader = FindAnyObjectByType<ClientSceneLoader>();
            }
        }

        public override void OnNetworkSpawn()
        {
            if (!IsClient)
            {
                enabled = false;
                return;
            }

            if (clientSceneLoader != null)
            {
                clientSceneLoader.OnSceneLoaded += HandleSceneLoaded;
            }
        }

        public override void OnNetworkDespawn()
        {
            if (clientSceneLoader != null)
            {
                clientSceneLoader.OnSceneLoaded -= HandleSceneLoaded;
            }
        }

        private void HandleSceneLoaded(SceneID scene)
        {
            NotifySceneLoadedServerRpc(NetworkManager.Singleton.LocalClientId, scene);
        }

        [ServerRpc]
        public void NotifySceneLoadedServerRpc(ulong clientId, SceneID scene)
        {
            Debug.Log($"[Coordinator] Client {clientId} confirmed scene loaded: {scene}");
        }

        [ServerRpc]
        public void RequestSceneLoadServerRpc(SceneID scene)
        {
            Debug.Log($"[Coordinator] Client {OwnerClientId} requested scene load: {scene}");
        }

        /// <summary>
        /// Вызывается из ServerSceneManager.LoadSceneClientRpc
        /// </summary>
        public void HandleSceneTransition(SceneTransitionData transitionData)
        {
            if (clientSceneLoader != null)
            {
                clientSceneLoader.LoadScene(transitionData.TargetScene, transitionData.LocalPosition);
            }
            else
            {
                Debug.LogWarning("[SceneTransitionCoordinator] ClientSceneLoader not found!");
            }
        }

        /// <summary>
        /// Вызывается из ServerSceneManager.InitializeSceneClientRpc
        /// </summary>
        public void HandleInitialScene(SceneID scene)
        {
            if (clientSceneLoader != null)
            {
                Vector3 localSpawn = new Vector3(SceneID.SCENE_SIZE / 2f, 0, SceneID.SCENE_SIZE / 2f);
                clientSceneLoader.LoadScene(scene, localSpawn);
            }
        }

        /// <summary>
        /// Вызывается из ServerSceneManager.UnloadSceneClientRpcInternal
        /// </summary>
        public void HandleSceneUnload(SceneID scene)
        {
            if (clientSceneLoader != null)
            {
                clientSceneLoader.UnloadScene(scene);
            }
        }
    }
}