// T-Q11b: NpcController — MonoBehaviour для scene-placed NPC.
// Trigger collider + NpcDefinition ref + server-side validation.
// См. docs/NPC_quests/02_V2_ARCHITECTURE.md §2.5 (NPC runtime), AGENTS.md: pattern
// от ChestController / MetaRequirementController (PlayerInteractor chain).
//
// Lifecycle:
//   - Scene-placed (не NetworkObject) — NPC state server-validated через QuestServer, не
//     через NGO replicated state. NPC "position" статичен в scene, dialogue state — server-side.
//   - OnTriggerEnter/Exit: tracks local player proximity (server uses QuestWorld for state).
//   - Interact(): client → server RequestTalkToNpcRpc через PlayerInteractor.
//   - Visual placeholder: Cube primitive с `displayName` text (T-Q18: portrait + animator).

using UnityEngine;
using TMPro;
using ProjectC.Quests;

namespace ProjectC.Quests
{
    /// <summary>
    /// Scene-placed NPC. Триггер при подходе игрока + dialog tree on E-key.
    /// </summary>
    /// <remarks>
    /// T-Q11b: NpcController — simple trigger + visual. T-Q15+ добавит:
    ///   - animator trigger prefix
    ///   - visual variations (portrait, sprite)
    ///   - faction-based outline (зелёный/красный)
    /// </remarks>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public class NpcController : MonoBehaviour
    {
        [Header("NPC Identity")]
        [Tooltip("NpcDefinition SO (id, displayName, factionId, defaultDialogTree).")]
        [SerializeField] private NpcDefinition definition;

        [Header("Interaction")]
        [Tooltip("Distance для E-key trigger (default 2.5 — близко к collider).")]
        [SerializeField] private float interactionDistance = 2.5f;

        [Header("Visual (placeholder)")]
        [Tooltip("Optional SpriteRenderer — если null, создаётся Cube primitive.")]
        [SerializeField] private SpriteRenderer portraitRenderer;
        [Tooltip("Optional TMPro label — создаётся автоматически если null.")]
        [SerializeField] private TextMeshPro nameLabel;

        // Cached collider (must be trigger)
        private Collider _triggerCollider;

        // Track player proximity (server-side authoritative)
        private bool _playerInRange;

        public string NpcId => definition != null ? definition.npcId : "";
        public NpcDefinition Definition => definition;
        public float InteractionDistance => interactionDistance;

        private void Awake()
        {
            // Ensure collider is trigger
            _triggerCollider = GetComponent<Collider>();
            if (_triggerCollider != null && !_triggerCollider.isTrigger)
            {
                Debug.LogWarning($"[NpcController:{name}] Collider is not trigger — fixing. " +
                                 "NPC interact area MUST be trigger to avoid blocking player movement.");
                _triggerCollider.isTrigger = true;
            }

            // Ensure visual: create Cube primitive if no portrait/label
            if (portraitRenderer == null && nameLabel == null && GetComponent<MeshRenderer>() == null)
            {
                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.transform.SetParent(transform, false);
                cube.transform.localPosition = new Vector3(0, 0.5f, 0);
                cube.transform.localScale = new Vector3(0.8f, 1.5f, 0.8f);
                // Remove cube collider (we have our own trigger)
                var cubeCol = cube.GetComponent<Collider>();
                if (cubeCol != null) Destroy(cubeCol);
                // Color it based on faction (placeholder — blue for friendly)
                var mr = cube.GetComponent<MeshRenderer>();
                if (mr != null)
                {
                    mr.material.color = Color.cyan;
                }
            }

            // Ensure name label
            if (nameLabel == null && definition != null && !string.IsNullOrEmpty(definition.displayName))
            {
                var labelGo = new GameObject("NameLabel");
                labelGo.transform.SetParent(transform, false);
                labelGo.transform.localPosition = new Vector3(0, 2.0f, 0);
                nameLabel = labelGo.AddComponent<TextMeshPro>();
                nameLabel.text = definition.displayName;
                nameLabel.fontSize = 32;
                nameLabel.alignment = TextAlignmentOptions.Center;
                nameLabel.color = Color.yellow;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player")) return;
            _playerInRange = true;
            if (Debug.isDebugBuild) Debug.Log($"[NpcController:{NpcId}] Player entered range");
        }

        private void OnTriggerExit(Collider other)
        {
            if (!other.CompareTag("Player")) return;
            _playerInRange = false;
            if (Debug.isDebugBuild) Debug.Log($"[NpcController:{NpcId}] Player exited range");
        }

        /// <summary>True если local player в trigger range. T-Q11b: cheap check без raycast.</summary>
        public bool PlayerInRange => _playerInRange;

        /// <summary>Distance check (alternative к trigger — fallback если collider doesn't have isTrigger).</summary>
        public bool IsWithinDistance(Vector3 playerPosition)
        {
            return Vector3.Distance(transform.position, playerPosition) <= interactionDistance;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Gizmos.color = _playerInRange ? Color.green : Color.yellow;
            Gizmos.DrawWireSphere(transform.position, interactionDistance);
        }
#endif
    }
}
