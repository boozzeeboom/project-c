using Unity.Netcode;
using UnityEngine;
using ProjectC.Player;

namespace ProjectC.Trade
{
    /// <summary>
    /// Test script for diagnosing ClientRpc behavior on host.
    /// Session NEXT: understand how client targeting works.
    /// 
    /// Tests:
    /// - ClientRpcParams with TargetClientIds
    /// - Direct call on host (IsServer check)
    /// - Host = false attribute
    /// </summary>
    public class TradeDebugTest : NetworkBehaviour
    {
        [Header("Test Settings")]
        [SerializeField] private float testDelay = 2f;
        [SerializeField] private bool runOnStart = true;

        private float _testTimer = 0f;
        private bool _testStarted = false;

        private void Start()
        {
            if (runOnStart)
            {
                _testTimer = testDelay;
            }
        }

        private void Update()
        {
            if (!IsServer) return;
            if (_testStarted) return;
            
            _testTimer -= Time.deltaTime;
            if (_testTimer <= 0f)
            {
                _testStarted = true;
                RunAllTests();
            }
        }

        /// <summary>
        /// Run all diagnostic tests
        /// </summary>
        [ContextMenu("Run All Tests")]
        public void RunAllTests()
        {
            Debug.Log("=== TradeDebugTest: Starting all tests ===");
            Debug.Log($"IsServer={IsServer}, IsHost={IsHost}, IsClient={IsClient}");
            Debug.Log($"LocalClientId={NetworkManager.Singleton.LocalClientId}");
            
            // Test 1: Broadcast ClientRpc
            Test1_BroadcastClientRpc();
            
            // Test 2: Targeted ClientRpc with ClientRpcParams
            StartCoroutine(Test2_DelayedTargetedRpc());
            
            // Test 3: Direct call on host
            Test3_DirectCallOnHost();
        }

        /// <summary>
        /// Test 1: Simple broadcast ClientRpc - sent to everyone
        /// </summary>
        [ClientRpc]
        private void Test1_BroadcastClientRpc()
        {
            var nm = NetworkManager.Singleton;
            Debug.Log($"[Test1] Broadcast RPC received: " +
                     $"localId={nm.LocalClientId}, " +
                     $"IsOwner={IsOwner}, " +
                     $"OwnerClientId={OwnerClientId}, " +
                     $"IsServer={IsServer}, " +
                     $"IsHost={IsHost}");
        }

        /// <summary>
        /// Test 2: Targeted ClientRpc using ClientRpcParams
        /// </summary>
        private System.Collections.IEnumerator Test2_DelayedTargetedRpc()
        {
            yield return new WaitForSeconds(0.5f);
            
            var nm = NetworkManager.Singleton;
            
            // Find connected clients
            if (nm.ConnectedClientsIds.Count == 0)
            {
                Debug.Log("[Test2] No connected clients found for targeted RPC test");
                yield break;
            }

            // Test with first client (not the host if host is running)
            foreach (var clientId in nm.ConnectedClientsIds)
            {
                Debug.Log($"[Test2] Testing targeted RPC to client {clientId}");
                
                var clientParams = new ClientRpcParams
                {
                    Send = new ClientRpcSendParams
                    {
                        TargetClientIds = new ulong[] { clientId }
                    }
                };
                
                Test2_TargetedClientRpc(clientId, clientParams);
            }
        }

        [ClientRpc]
        private void Test2_TargetedClientRpc(ulong targetClientId, ClientRpcParams clientParams = default)
        {
            var nm = NetworkManager.Singleton;
            bool isTargeted = nm.LocalClientId == targetClientId;
            
            Debug.Log($"[Test2] Targeted RPC: " +
                     $"targetId={targetClientId}, " +
                     $"localId={nm.LocalClientId}, " +
                     $"isTargeted={isTargeted}, " +
                     $"IsOwner={IsOwner}");
            
            if (isTargeted)
            {
                Debug.Log($"[Test2] ✅ SUCCESS: This client IS the target!");
            }
            else
            {
                Debug.Log($"[Test2] ℹ️  INFO: This client is NOT the target");
            }
        }

        /// <summary>
        /// Test 3: Direct call on host (IsServer check)
        /// </summary>
        private void Test3_DirectCallOnHost()
        {
            Debug.Log($"[Test3] Direct call check: IsServer={IsServer}, IsHost={IsHost}");
            
            if (IsServer)
            {
                // This code runs on server/host
                Debug.Log("[Test3] ✅ Running on SERVER/HOST - direct call works!");
                
                // Simulate what we want to do: call TradeUI directly
                if (TradeUI.Instance != null)
                {
                    Debug.Log("[Test3] TradeUI.Instance is available on server!");
                }
                else
                {
                    Debug.Log("[Test3] TradeUI.Instance is NULL on server");
                }
            }
        }

        /// <summary>
        /// Manual test from button - sends targeted RPC to a specific client
        /// </summary>
        public void SendTestToClient(ulong targetClientId)
        {
            Debug.Log($"[ManualTest] Sending targeted RPC to client {targetClientId}");
            
            var clientParams = new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new ulong[] { targetClientId }
                }
            };
            
            ManualTestClientRpc(targetClientId, clientParams);
        }

        [ClientRpc]
        private void ManualTestClientRpc(ulong targetId, ClientRpcParams clientParams = default)
        {
            var nm = NetworkManager.Singleton;
            bool isTarget = nm.LocalClientId == targetId;
            
            Debug.Log($"[ManualTest] Received: localId={nm.LocalClientId}, target={targetId}, isTarget={isTarget}");
        }

        /// <summary>
        /// Test the actual TradeResult scenario
        /// Call this to simulate a trade result being sent
        /// </summary>
        public void SimulateTradeResult()
        {
            Debug.Log("=== Simulating Trade Result ===");
            
            // Get all connected clients
            var nm = NetworkManager.Singleton;
            foreach (var clientId in nm.ConnectedClientsIds)
            {
                Debug.Log($"Processing client {clientId}...");
                
                // Current approach (BROKEN on host): send via NetworkPlayer
                var player = GetNetworkPlayerForClient(clientId);
                if (player != null)
                {
                    Debug.Log($"Found NetworkPlayer for client {clientId}, OwnerClientId={player.OwnerClientId}");
                    
                    // Try with ClientRpcParams
                    var clientParams = new ClientRpcParams
                    {
                        Send = new ClientRpcSendParams
                        {
                            TargetClientIds = new ulong[] { clientId }
                        }
                    };
                    
                    // This is how we SHOULD send the RPC
                    player.TradeResultDebugClientRpc(true, "Test message", 1000f, "test_item", 1, true, clientParams);
                }
            }
        }

        /// <summary>
        /// Find NetworkPlayer for a specific client
        /// </summary>
        private NetworkPlayer GetNetworkPlayerForClient(ulong clientId)
        {
            // Try via NetworkManager
            if (NetworkManager.Singleton != null && 
                NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client))
            {
                return client.PlayerObject?.GetComponent<NetworkPlayer>();
            }
            
            // Fallback: search all NetworkPlayers
            var players = FindObjectsByType<NetworkPlayer>(FindObjectsInactive.Exclude);
            foreach (var player in players)
            {
                if (player.OwnerClientId == clientId)
                {
                    return player;
                }
            }
            
            return null;
        }
    }

    /// <summary>
    /// Extension to NetworkPlayer for debug RPC
    /// </summary>
    public static class NetworkPlayerDebugExtensions
    {
        /// <summary>
        /// Debug version of TradeResultClientRpc with ClientRpcParams support
        /// </summary>
        [ClientRpc]
        public static void TradeResultDebugClientRpc(
            this NetworkPlayer player,
            bool success, 
            string message, 
            float newCredits, 
            string itemId, 
            int itemQuantity, 
            bool isPurchase,
            ClientRpcParams clientParams = default)
        {
            var nm = NetworkManager.Singleton;
            Debug.Log($"[Debug TradeResult] localId={nm.LocalClientId}, " +
                     $"OwnerClientId={player.OwnerClientId}, " +
                     $"IsOwner={player.IsOwner}, " +
                     $"success={success}");
            
            // Check if this client should receive the update
            bool shouldReceive = nm.LocalClientId == player.OwnerClientId;
            
            Debug.Log($"[Debug TradeResult] Should receive: {shouldReceive}");
            
            if (shouldReceive && TradeUI.Instance != null)
            {
                TradeUI.Instance.OnTradeResult(success, message, newCredits, itemId, itemQuantity, isPurchase);
            }
        }
    }
}
