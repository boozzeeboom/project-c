using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using ProjectC.Core;
using ProjectC.Items;

namespace ProjectC.World.Chest
{
    /// <summary>
    /// Network-enabled chest container.
    /// Synchronizes open state across all clients.
    /// Handles loot generation and distribution via NetworkInventory.
    /// 
    /// Iteration 4: Адаптирован из ChestContainer.cs для работы с NGO + FloatingOrigin.
    /// </summary>
    public class NetworkChestContainer : NetworkBehaviour, IInteractable
    {
        [Header("Loot Table")]
        [SerializeField] private LootTable lootTable;

        [Header("Settings")]
        [SerializeField] private float openRadius = 3f;
        [SerializeField] private bool autoDestroy = false;
        [SerializeField] private float autoDestroyDelay = 2f;

        [Header("Animation")]
        [SerializeField] private float openDuration = 0.8f;
        [SerializeField] private Vector3 openRotationOffset = new Vector3(0, 0, -45f);
        [SerializeField] private Vector3 openScaleOffset = new Vector3(0.1f, 0.1f, 0.1f);

        [Header("Debug")]
        [SerializeField] private bool debugMode = true;

        // Network state
        private NetworkVariable<bool> _isOpen = new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        // Local state
        private Vector3 _startRotation;
        private Vector3 _startScale;
        private float _openTimer = 0f;
        private bool _animationPlayed = false;

        // IInteractable implementation
        public string InstanceId => gameObject.name + "_" + OwnerClientId;
        public string DisplayName => "Сундук";
        public float InteractionRadius => openRadius;
        public Vector3 Position => transform.position;

        // Chunk binding for streaming system
        private Streaming.ChunkId _owningChunkId;
        public Streaming.ChunkId OwningChunkId => _owningChunkId;

        public void SetChunk(Streaming.ChunkId chunkId)
        {
            _owningChunkId = chunkId;
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            
            _startRotation = transform.eulerAngles;
            _startScale = transform.localScale;

            if (debugMode)
                Debug.Log($"[NetworkChestContainer] OnNetworkSpawn - IsServer={IsServer}, OwnerClientId={OwnerClientId}");

            // Subscribe to network state changes
            _isOpen.OnValueChanged += OnOpenStateChanged;

            // Ensure trigger collider
            var collider = GetComponent<Collider>();
            if (collider == null)
            {
                collider = gameObject.AddComponent<BoxCollider>();
            }
            collider.isTrigger = true;

            // Play animation if already open (for late joiners)
            if (_isOpen.Value)
            {
                PlayOpenAnimationInstant();
            }
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();
            
            _isOpen.OnValueChanged -= OnOpenStateChanged;
        }

        private void OnOpenStateChanged(bool previousValue, bool newValue)
        {
            if (debugMode)
                Debug.Log($"[NetworkChestContainer] OnOpenStateChanged: {previousValue} -> {newValue}");
                
            if (newValue && !_animationPlayed)
            {
                PlayOpenAnimationInstant();
            }
        }

        private void Update()
        {
            if (!_isOpen.Value) return;
            
            // Open animation
            if (!_animationPlayed && _openTimer < openDuration)
            {
                _openTimer += Time.deltaTime;
                float t = Mathf.Clamp01(_openTimer / openDuration);
                t = Mathf.SmoothStep(0, 1, t);

                transform.eulerAngles = Vector3.Lerp(_startRotation, _startRotation + openRotationOffset, t);
                transform.localScale = Vector3.Lerp(_startScale, _startScale + openScaleOffset, t);
            }
        }

        private void OnDisable()
        {
            // Cleanup handled by trigger exit
        }

        /// <summary>
        /// Called by player interaction system.
        /// Sends ServerRpc to open the chest.
        /// </summary>
        public void TryOpen()
        {
            if (debugMode)
            {
                bool isNetworkSpawned = IsSpawned;
                bool isServer = IsServer;
                bool isHost = IsHost;
                Debug.Log($"[NetworkChestContainer] TryOpen - Spawned={isNetworkSpawned}, IsServer={isServer}, IsHost={isHost}, IsOpen={_isOpen.Value}");
            }

            // Проверка: объект должен быть сетевым и не открыт
            if (!IsSpawned)
            {
                if (debugMode)
                    Debug.LogWarning("[NetworkChestContainer] TryOpen FAILED: Not spawned (no NetworkObject?)");
                return;
            }

            if (_isOpen.Value)
            {
                if (debugMode)
                    Debug.Log("[NetworkChestContainer] TryOpen SKIPPED: Already open");
                return;
            }

            // Проверка: нужен либо сервер, либо хост (для отправки ServerRpc)
            if (!IsServer && !IsHost)
            {
                if (debugMode)
                    Debug.LogWarning("[NetworkChestContainer] TryOpen FAILED: Not server/host (client cannot send ServerRpc)");
                return;
            }

            RequestOpenChestServerRpc();
        }

        /// <summary>
        /// ServerRpc: Player requests to open the chest.
        /// Server validates distance, generates loot, adds to inventory.
        /// </summary>
        [Rpc(SendTo.Server)]
        private void RequestOpenChestServerRpc()
        {
            if (debugMode)
                Debug.Log($"[NetworkChestContainer] RequestOpenChestServerRpc received from client");

            // Already opened check
            if (_isOpen.Value)
            {
                if (debugMode)
                    Debug.Log("[NetworkChestContainer] ServerRpc: Already open, skipping");
                return;
            }

            // Get the client who requested (new NGO API)
            ulong clientId = NetworkManager.Singleton.LocalClientId;

            // Find the player object
            var networkManager = NetworkManager.Singleton;
            if (networkManager == null)
            {
                if (debugMode)
                    Debug.LogError("[NetworkChestContainer] ServerRpc: NetworkManager.Singleton is null!");
                return;
            }

            // Get player position for distance validation
            var playerObject = networkManager.SpawnManager.GetPlayerNetworkObject(clientId);
            if (playerObject == null)
            {
                if (debugMode)
                    Debug.LogError($"[NetworkChestContainer] ServerRpc: Player object not found for client {clientId}");
                return;
            }

            float dist = Vector3.Distance(playerObject.transform.position, transform.position);
            
            // Distance validation (anti-cheat)
            if (dist > openRadius + 2f) // 2m tolerance
            {
                Debug.Log($"[NetworkChestContainer] Client {clientId} too far: {dist:F1}m (max: {openRadius:F1}m)");
                return;
            }

            // Generate loot from LootTable
            var lootItems = GenerateLoot();
            
            if (debugMode)
                Debug.Log($"[NetworkChestContainer] Generated {lootItems.Count} loot items");

            // Add items to player's NetworkInventory
            var networkInventory = playerObject.GetComponent<NetworkInventory>();
            if (networkInventory != null)
            {
                foreach (var item in lootItems)
                {
                    int itemId = NetworkInventory.GetItemId(item);
                    networkInventory.AddItem(itemId, item.itemType);
                }
                
                Debug.Log($"[NetworkChestContainer] Added {lootItems.Count} items to player {clientId}");
            }
            else
            {
                if (debugMode)
                    Debug.LogWarning("[NetworkChestContainer] NetworkInventory not found on player!");
            }

            // Set open state (server authoritative)
            _isOpen.Value = true;

            // Notify all clients to play animation
            OpenChestClientRpc();

            // Auto-destroy if configured
            if (autoDestroy)
            {
                StartCoroutine(AutoDestroyCoroutine());
            }
        }

        /// <summary>
        /// ClientRpc: Play open animation on all clients.
        /// </summary>
        [Rpc(SendTo.Everyone)]
        private void OpenChestClientRpc()
        {
            if (debugMode)
                Debug.Log("[NetworkChestContainer] OpenChestClientRpc received");
                
            _animationPlayed = true;
            _openTimer = openDuration; // Skip to end for instant feel
            transform.eulerAngles = _startRotation + openRotationOffset;
            transform.localScale = _startScale + openScaleOffset;
        }

        /// <summary>
        /// Play open animation instantly (for late joiners).
        /// </summary>
        private void PlayOpenAnimationInstant()
        {
            _animationPlayed = true;
            _openTimer = openDuration;
            transform.eulerAngles = _startRotation + openRotationOffset;
            transform.localScale = _startScale + openScaleOffset;
        }

        /// <summary>
        /// Generate loot from LootTable.
        /// </summary>
        private List<ItemData> GenerateLoot()
        {
            if (lootTable == null)
            {
                if (debugMode)
                    Debug.LogWarning($"[NetworkChestContainer] LootTable not assigned for {gameObject.name}");
                return new List<ItemData>();
            }
            return lootTable.GenerateLoot();
        }

        /// <summary>
        /// Get list of items from LootTable (for preview/debug).
        /// </summary>
        public List<ItemData> GetLootPreview()
        {
            return GenerateLoot();
        }

        /// <summary>
        /// Get the radius for opening the chest.
        /// </summary>
        public float GetOpenRadius()
        {
            return openRadius;
        }

        private System.Collections.IEnumerator AutoDestroyCoroutine()
        {
            yield return new WaitForSeconds(autoDestroyDelay);
            
            // Only despawn if no one is nearby
            if (IsServer)
            {
                GetComponent<NetworkObject>().Despawn();
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, openRadius);
        }
    }
}