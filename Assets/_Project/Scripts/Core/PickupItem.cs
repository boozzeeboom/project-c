using UnityEngine;

namespace ProjectC.Items
{
    /// <summary>
    /// Component for pickup items in the world.
    /// Attach to GameObject with trigger collider.
    /// Press E when nearby — item is picked up and added to Inventory.
    /// Implements IInteractable for trigger-based caching instead of FindObjectsByType.
    /// </summary>
    public class PickupItem : MonoBehaviour, Core.IInteractable
    {
        [Header("Item Data")]
        public ItemData itemData;

        [Header("Settings")]
        public float floatSpeed = 1f;
        public float floatAmplitude = 0.2f;

        [Header("Interaction")]
        [Tooltip("Radius for interaction (used by IInteractable)")]
        public float interactionRadius = 3f;

        private Vector3 _startPosition;
        private bool _isCollected = false;

        // IInteractable implementation
        public string InstanceId => gameObject.name + "_" + GetHashCode();
        public string DisplayName => itemData != null ? itemData.itemName : "Unknown Item";
        public float InteractionRadius => interactionRadius;
        public Vector3 Position => transform.position;

        private void Start()
        {
            _startPosition = transform.position;

            // Ensure trigger collider exists
            var collider = GetComponent<Collider>();
            if (collider == null)
            {
                collider = gameObject.AddComponent<SphereCollider>();
            }
            collider.isTrigger = true;
        }

        private void Update()
        {
            // Visual bobbing
            if (!_isCollected)
            {
                transform.position = _startPosition + Vector3.up * Mathf.Sin(Time.time * floatSpeed) * floatAmplitude;
                transform.Rotate(Vector3.up, 30f * Time.deltaTime);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            // Register with InteractableManager when player enters trigger
            if (other.CompareTag("Player") || other.GetComponent<CharacterController>() != null)
            {
                Core.InteractableManager.RegisterPickup(this);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            // Unregister from InteractableManager when player exits trigger
            if (other.CompareTag("Player") || other.GetComponent<CharacterController>() != null)
            {
                Core.InteractableManager.UnregisterPickup(this);
            }
        }

        private void OnDisable()
        {
            // Ensure cleanup when object is disabled
            Core.InteractableManager.UnregisterPickup(this);
        }

        /// <summary>
        /// Pick up the item. Called from NetworkPlayer.TryPickup().
        /// </summary>
        public void Collect()
        {
            if (_isCollected || itemData == null) return;
            _isCollected = true;

            // Hide the item
            gameObject.SetActive(false);
            
            // Unregister from manager
            Core.InteractableManager.UnregisterPickup(this);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, interactionRadius);
        }
    }
}