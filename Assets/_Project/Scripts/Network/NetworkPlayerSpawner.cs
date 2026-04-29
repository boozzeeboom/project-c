using UnityEngine;
using Unity.Netcode;

namespace ProjectC.Network
{
    public class NetworkPlayerSpawner : MonoBehaviour
    {
        [SerializeField] private bool useScenePlayerAsHost = true;
        
        private bool _hasSpawnedHostPlayer = false;

        private void Start()
        {
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            }
        }

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
                Debug.Log("[NetworkPlayerSpawner] Host/Server player spawned");
            }
        }

        private void OnClientConnected(ulong clientId)
        {
            Debug.Log($"[NetworkPlayerSpawner] Client connected: {clientId}");
            
            // Only server spawns players for clients
            if (NetworkManager.Singleton.IsServer && clientId != NetworkManager.Singleton.LocalClientId)
            {
                SpawnPlayerForClient(clientId);
            }
        }

        private void SpawnPlayerForClient(ulong clientId)
        {
            var thisNetworkObject = GetComponent<NetworkObject>();
            if (thisNetworkObject != null)
            {
                // Instantiate a new player for the connecting client
                var spawnPos = new Vector3(clientId * 3f, 2f, 0f);
                var clone = Instantiate(thisNetworkObject.gameObject, spawnPos, Quaternion.identity);
                var cloneNetworkObject = clone.GetComponent<NetworkObject>();
                
                if (cloneNetworkObject != null)
                {
                    cloneNetworkObject.SpawnAsPlayerObject(clientId);
                    Debug.Log($"[NetworkPlayerSpawner] Spawned player for client {clientId} at {spawnPos}");
                }
            }
        }

        private void OnDestroy()
        {
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            }
        }
    }
}