using Unity.Netcode;
using UnityEngine;
using ProjectC.Core;

namespace ProjectC.World.Npc
{
    /// <summary>
    /// NPC Entity MonoBehaviour.
    /// Handles NPC state, animation, and network synchronization.
    /// 
    /// For local NPCs (non-networked), interaction happens on client.
    /// For networked NPCs, uses ServerRpc/ClientRpc for multi-player sync.
    /// </summary>
    public class NpcEntity : NetworkBehaviour
    {
        [Header("NPC Data")]
        [Tooltip("NPC data ScriptableObject")]
        [SerializeField] private NpcData npcData;

        [Header("Animation")]
        [Tooltip("Animator component")]
        [SerializeField] private Animator animator;

        [Header("Movement")]
        [Tooltip("Should NPC wander around?")]
        [SerializeField] private bool canWander = false;

        [Tooltip("Wander radius in meters")]
        [SerializeField] private float wanderRadius = 5f;

        [Tooltip("Time between wander moves")]
        [SerializeField] private float wanderInterval = 5f;

        [Tooltip("Movement speed")]
        [SerializeField] private float moveSpeed = 1.5f;

        [Header("Debug")]
        [SerializeField] private bool debugMode = true;

        // NPC States
        public enum NpcState
        {
            Idle,
            Walking,
            Talking,
            Waiting
        }

        // Current state
        private NpcState _currentState = NpcState.Idle;
        public NpcState CurrentState => _currentState;

        // Network state (if networked NPC)
        private NetworkVariable<NpcState> _networkState = new NetworkVariable<NpcState>(
            NpcState.Idle,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        // Local references
        private Vector3 _startPosition;
        private Vector3 _wanderTarget;
        private float _wanderTimer = 0f;
        private float _stateTimer = 0f;

        // IInteractable implementation (local or networked)
        public string InstanceId => npcData != null 
            ? $"{npcData.npcId}_{GetHashCode()}" 
            : $"npc_{GetHashCode()}";

        public string DisplayName => npcData?.displayName ?? "Unknown NPC";
        
        public float InteractionRadius => npcData?.interactionRadius ?? 3f;
        
        public Vector3 Position => transform.position;

        private void Awake()
        {
            _startPosition = transform.position;
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (debugMode)
            {
                Debug.Log($"[NpcEntity] OnNetworkSpawn - npcId={npcData?.npcId}, " +
                    $"IsServer={IsServer}, IsHost={IsHost}, OwnerClientId={OwnerClientId}");
            }

            // Subscribe to network state changes
            _networkState.OnValueChanged += OnNetworkStateChanged;

            // Initialize network state
            if (IsServer)
            {
                _networkState.Value = NpcState.Idle;
            }

            // Ensure we have a collider for interaction
            EnsureCollider();
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();
            _networkState.OnValueChanged -= OnNetworkStateChanged;
        }

        private void OnNetworkStateChanged(NpcState previousValue, NpcState newValue)
        {
            if (debugMode)
            {
                Debug.Log($"[NpcEntity] NetworkState changed: {previousValue} -> {newValue}");
            }
            
            _currentState = newValue;
            UpdateAnimation();
        }

        private void Update()
        {
            // State machine (runs on all instances)
            _stateTimer += Time.deltaTime;

            switch (_currentState)
            {
                case NpcState.Idle:
                    HandleIdleState();
                    break;

                case NpcState.Walking:
                    HandleWalkingState();
                    break;

                case NpcState.Talking:
                    // Talking is handled by NpcDialogueManager
                    break;

                case NpcState.Waiting:
                    // Waiting for player interaction
                    break;
            }

            // Local wandering for non-networked or server-authoritative NPCs
            if (canWander && (_currentState == NpcState.Idle || _currentState == NpcState.Walking))
            {
                UpdateWandering();
            }
        }

        private void HandleIdleState()
        {
            // Idle animation is default
            // Could add random idle animations here
        }

        private void HandleWalkingState()
        {
            // Move towards wander target
            if (_wanderTarget != Vector3.zero)
            {
                Vector3 direction = (_wanderTarget - transform.position).normalized;
                float distance = Vector3.Distance(transform.position, _wanderTarget);

                if (distance > 0.5f)
                {
                    // Move with rotation towards target
                    transform.position += direction * moveSpeed * Time.deltaTime;
                    
                    // Rotate towards movement direction (on Y axis only for top-down style)
                    Vector3 lookDirection = new Vector3(direction.x, 0, direction.z);
                    if (lookDirection != Vector3.zero)
                    {
                        Quaternion targetRotation = Quaternion.LookRotation(lookDirection);
                        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 5f * Time.deltaTime);
                    }
                }
                else
                {
                    // Reached target, go back to idle
                    SetState(NpcState.Idle);
                }
            }
        }

        private void UpdateWandering()
        {
            if (!canWander) return;

            _wanderTimer += Time.deltaTime;

            if (_wanderTimer >= wanderInterval && _currentState == NpcState.Idle)
            {
                // Decide to move to a new random position
                if (Random.Range(0f, 1f) > 0.3f) // 70% chance to start wandering
                {
                    GenerateWanderTarget();
                    SetState(NpcState.Walking);
                }
                
                _wanderTimer = 0f;
            }
        }

        private void GenerateWanderTarget()
        {
            // Generate random point within wander radius
            float angle = Random.Range(0f, Mathf.PI * 2f);
            float radius = Random.Range(1f, wanderRadius);
            
            _wanderTarget = _startPosition + new Vector3(
                Mathf.Cos(angle) * radius,
                0,
                Mathf.Sin(angle) * radius
            );
        }

        /// <summary>
        /// Set NPC state. Server-authoritative for networked NPCs.
        /// </summary>
        public void SetState(NpcState newState)
        {
            if (npcData?.isNetworked == true && IsServer)
            {
                _networkState.Value = newState;
            }
            else
            {
                // Local-only NPC or client prediction
                _currentState = newState;
            }
            
            UpdateAnimation();
            
            if (debugMode)
            {
                Debug.Log($"[NpcEntity] SetState: {newState}");
            }
        }

        /// <summary>
        /// Start dialogue with player.
        /// </summary>
        public void StartDialogue()
        {
            if (debugMode)
            {
                Debug.Log($"[NpcEntity] StartDialogue called for {DisplayName}");
            }
            
            SetState(NpcState.Talking);
            
            // Open dialogue UI
            if (npcData != null)
            {
                NpcDialogueManager.Instance?.StartDialogue(npcData, this);
            }
        }

        /// <summary>
        /// End dialogue.
        /// </summary>
        public void EndDialogue()
        {
            SetState(NpcState.Idle);
        }

        private void UpdateAnimation()
        {
            if (animator == null) return;

            // Reset all states
            animator.ResetTrigger("Idle");
            animator.ResetTrigger("Walk");
            animator.ResetTrigger("Talk");

            // Set current state
            switch (_currentState)
            {
                case NpcState.Idle:
                    animator.SetTrigger("Idle");
                    break;
                case NpcState.Walking:
                    animator.SetTrigger("Walk");
                    break;
                case NpcState.Talking:
                    animator.SetTrigger("Talk");
                    break;
            }
        }

        private void EnsureCollider()
        {
            var collider = GetComponent<Collider>();
            if (collider == null)
            {
                // Add sphere collider for interaction
                var sphereCollider = gameObject.AddComponent<SphereCollider>();
                sphereCollider.radius = InteractionRadius;
                sphereCollider.isTrigger = true;
            }
            else
            {
                collider.isTrigger = true;
            }
        }

        /// <summary>
        /// Get NPC data reference.
        /// </summary>
        public NpcData GetNpcData()
        {
            return npcData;
        }

        /// <summary>
        /// Set NPC data at runtime (for procedural NPCs).
        /// </summary>
        public void SetNpcData(NpcData data)
        {
            npcData = data;
            
            if (debugMode)
            {
                Debug.Log($"[NpcEntity] SetNpcData: {data?.npcId}");
            }
        }

        private void OnDrawGizmosSelected()
        {
            // Draw interaction radius
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, InteractionRadius);

            // Draw wander radius
            if (canWander)
            {
                Gizmos.color = Color.blue;
                Gizmos.DrawWireSphere(_startPosition, wanderRadius);
            }

            // Draw wander target
            if (_wanderTarget != Vector3.zero)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawSphere(_wanderTarget, 0.5f);
                Gizmos.DrawLine(transform.position, _wanderTarget);
            }
        }
    }
}