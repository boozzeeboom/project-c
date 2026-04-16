using System.Collections.Generic;
using UnityEngine;

namespace ProjectC.Items
{
    /// <summary>
    /// Component for chest/container with multiple items.
    /// Press E when nearby — opens, delivers all items from LootTable to inventory.
    /// Implements IInteractable for trigger-based caching instead of FindObjectsByType.
    /// </summary>
    public class ChestContainer : MonoBehaviour, Core.IInteractable
    {
        [Header("Loot Table")]
        public LootTable lootTable;

        [Header("Settings")]
        public float openRadius = 3f;
        public bool autoDestroy = true;

        [Header("Animation")]
        public float openDuration = 0.8f;
        public Vector3 openRotationOffset = new Vector3(0, 0, -45f);
        public Vector3 openScaleOffset = new Vector3(0.1f, 0.1f, 0.1f);

        private bool _isOpen = false;
        private Vector3 _startRotation;
        private Vector3 _startScale;
        private float _openTimer = 0f;

        // IInteractable implementation
        public string InstanceId => gameObject.name + "_" + GetHashCode();
        public string DisplayName => "Chest";
        public float InteractionRadius => openRadius;
        public Vector3 Position => transform.position;
        
        // Chunk binding for streaming system
        private World.Streaming.ChunkId _owningChunkId;
        public World.Streaming.ChunkId OwningChunkId => _owningChunkId;
        
        /// <summary>
        /// Привязать сундук к чанку (вызывается из ChunkNetworkSpawner).
        /// </summary>
        public void SetChunk(World.Streaming.ChunkId chunkId)
        {
            _owningChunkId = chunkId;
        }

        private void Start()
        {
            _startRotation = transform.eulerAngles;
            _startScale = transform.localScale;

            // Ensure trigger collider
            var collider = GetComponent<Collider>();
            if (collider == null)
            {
                collider = gameObject.AddComponent<BoxCollider>();
            }
            collider.isTrigger = true;
        }

        private void Update()
        {
            // Open animation
            if (_isOpen && _openTimer < openDuration)
            {
                _openTimer += Time.deltaTime;
                float t = Mathf.Clamp01(_openTimer / openDuration);
                t = Mathf.SmoothStep(0, 1, t);

                transform.eulerAngles = Vector3.Lerp(_startRotation, _startRotation + openRotationOffset, t);
                transform.localScale = Vector3.Lerp(_startScale, _startScale + openScaleOffset, t);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            // Register with InteractableManager when player enters trigger
            if (other.CompareTag("Player") || other.GetComponent<CharacterController>() != null)
            {
                Core.InteractableManager.RegisterChest(this);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            // Unregister from InteractableManager when player exits trigger
            if (other.CompareTag("Player") || other.GetComponent<CharacterController>() != null)
            {
                Core.InteractableManager.UnregisterChest(this);
            }
        }

        private void OnDisable()
        {
            // Ensure cleanup when object is disabled
            Core.InteractableManager.UnregisterChest(this);
        }

        /// <summary>
        /// Open the chest. Starts ONLY the animation.
        /// Inventory is managed separately.
        /// </summary>
        public void Open()
        {
            if (_isOpen) return;
            _isOpen = true;
            // Animation only — inventory managed separately
        }

        /// <summary>
        /// Get list of items from LootTable (without opening, for server logic).
        /// </summary>
        public List<ItemData> GetLootItems()
        {
            if (lootTable == null) return new List<ItemData>();
            return lootTable.GenerateLoot();
        }

        /// <summary>
        /// Interaction distance.
        /// </summary>
        public float GetOpenRadius()
        {
            return openRadius;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, openRadius);
        }
    }
}