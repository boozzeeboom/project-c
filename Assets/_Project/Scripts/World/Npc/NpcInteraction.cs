using UnityEngine;
using ProjectC.Core;

namespace ProjectC.World.Npc
{
    /// <summary>
    /// NPC Interaction component.
    /// Implements IInteractable for the interaction system.
    /// Can be used standalone or alongside NpcEntity.
    /// </summary>
    public class NpcInteraction : MonoBehaviour, IInteractable
    {
        [Header("NPC Data")]
        [Tooltip("Reference to NpcData ScriptableObject")]
        [SerializeField] private NpcData npcData;

        [Header("Settings")]
        [Tooltip("Override interaction radius (uses NpcData if 0)")]
        [SerializeField] private float overrideRadius = 0f;

        [Tooltip("Auto-find NpcEntity component if not assigned")]
        [SerializeField] private bool autoFindEntity = true;

        [Header("Debug")]
        [SerializeField] private bool debugMode = true;

        // Cached NpcEntity reference
        private NpcEntity _npcEntity;

        // IInteractable implementation
        public string InstanceId => npcData != null 
            ? $"{npcData.npcId}_{GetHashCode()}" 
            : $"npc_interaction_{GetHashCode()}";

        public string DisplayName => npcData?.displayName ?? "Unknown NPC";

        public float InteractionRadius => 
            overrideRadius > 0f ? overrideRadius : 
            (npcData != null ? npcData.interactionRadius : 3f);

        public Vector3 Position => transform.position;

        // Event for when interaction occurs
        public System.Action<NpcInteraction> OnInteracted;

        private void Awake()
        {
            // Auto-find NpcEntity if needed
            if (autoFindEntity && _npcEntity == null)
            {
                _npcEntity = GetComponent<NpcEntity>();
            }

            // Ensure we have an interaction collider
            EnsureCollider();
        }

        private void Start()
        {
            // Register with InteractableManager for nearby NPC detection
            InteractableManager.RegisterNpc(this);
        }

        private void OnDestroy()
        {
            // Unregister when destroyed
            InteractableManager.UnregisterNpc(this);
        }

        private void OnEnable()
        {
            // Re-register when enabled
            InteractableManager.RegisterNpc(this);
        }

        private void OnDisable()
        {
            // Unregister when disabled
            InteractableManager.UnregisterNpc(this);
        }

        /// <summary>
        /// Called by the interaction system when player presses interact key.
        /// </summary>
        public void Interact()
        {
            if (debugMode)
            {
                Debug.Log($"[NpcInteraction] Interact called on: {DisplayName}");
            }

            if (npcData == null)
            {
                if (debugMode)
                {
                    Debug.LogWarning($"[NpcInteraction] NpcData is null on {gameObject.name}!");
                }
                return;
            }

            // Trigger event
            OnInteracted?.Invoke(this);

            // If we have an NpcEntity, use its dialogue system
            if (_npcEntity != null)
            {
                _npcEntity.StartDialogue();
            }
            else
            {
                // Direct dialogue without entity
                NpcDialogueManager.Instance?.StartDialogue(npcData, null);
            }

            if (debugMode)
            {
                Debug.Log($"[NpcInteraction] Started dialogue with: {DisplayName}");
            }
        }

        /// <summary>
        /// Show greeting text to player.
        /// </summary>
        public void ShowGreeting()
        {
            if (npcData != null && npcData.showGreeting && !string.IsNullOrEmpty(npcData.greetingText))
            {
                // TODO: Show floating text or notification
                Debug.Log($"[NpcInteraction] {DisplayName} says: \"{npcData.greetingText}\"");
            }
        }

        /// <summary>
        /// Set the NPC data at runtime.
        /// </summary>
        public void SetNpcData(NpcData data)
        {
            npcData = data;
            
            if (debugMode)
            {
                Debug.Log($"[NpcInteraction] SetNpcData: {data?.npcId}");
            }
        }

        /// <summary>
        /// Get the associated NpcData.
        /// </summary>
        public NpcData GetNpcData()
        {
            return npcData;
        }

        /// <summary>
        /// Get the associated NpcEntity (if any).
        /// </summary>
        public NpcEntity GetNpcEntity()
        {
            return _npcEntity;
        }

        /// <summary>
        /// Check if player can interact with this NPC.
        /// </summary>
        public bool CanInteract()
        {
            // TODO: Add checks for quest state, reputation, etc.
            return npcData != null;
        }

        /// <summary>
        /// Get the NPC's faction for reputation checks.
        /// </summary>
        public NpcFaction GetFaction()
        {
            return npcData?.faction ?? NpcFaction.Neutral;
        }

        private void EnsureCollider()
        {
            var collider = GetComponent<Collider>();
            if (collider == null)
            {
                var sphereCollider = gameObject.AddComponent<SphereCollider>();
                sphereCollider.radius = InteractionRadius;
                sphereCollider.isTrigger = true;
                
                if (debugMode)
                {
                    Debug.Log($"[NpcInteraction] Added SphereCollider with radius {InteractionRadius}");
                }
            }
            else
            {
                // Ensure it's a trigger
                collider.isTrigger = true;
            }
        }

        private void OnDrawGizmosSelected()
        {
            // Draw interaction radius
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, InteractionRadius);

            // Draw line to NPC if has NpcEntity
            if (_npcEntity != null)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(transform.position, _npcEntity.transform.position);
            }
        }
    }
}