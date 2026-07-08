// Project C: Real-Time Combat Engine — T-HIGHLIGHT-01
// TargetHighlightService: client-side singleton for target outline highlighting.
// Applies/removes M_TargetOutline material on target's SkinnedMeshRenderer/MeshRenderer.
// Auto-expires highlights after configurable duration.
//
// Design: docs/Character/Skills/real-time-combat/100_TARGET_HIGHLIGHT_AND_SWITCHING.md §1.1

using System.Collections;
using UnityEngine;

namespace ProjectC.Combat.Client
{
    /// <summary>
    /// Client-only singleton. Manages outline material on the current highlighted target.
    /// Created in NetworkManagerController alongside CombatClientState.
    /// </summary>
    public class TargetHighlightService : MonoBehaviour
    {
        public static TargetHighlightService Instance { get; private set; }

        [Header("Outline Settings")]
        [Tooltip("Outline material (M_TargetOutline.mat). Loaded from Resources if null.")]
        [SerializeField] private Material _outlineMaterial;

        [Tooltip("Default highlight duration in seconds. 0 = infinite (until Clear).")]
        [SerializeField] private float _defaultDuration = 1.5f;

        private GameObject _currentTarget;
        private Coroutine _expireRoutine;

        // === Lifecycle ===

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[TargetHighlightService] Replacing existing Instance (duplicate).");
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // Load outline material from Resources if not assigned.
            if (_outlineMaterial == null)
            {
                _outlineMaterial = Resources.Load<Material>("Materials/M_TargetOutline");
                if (_outlineMaterial == null)
                {
                    Debug.LogError("[TargetHighlightService] M_TargetOutline.mat not found in Resources/Materials/. Outline highlighting will not work.");
                }
            }
        }

        private void OnDestroy()
        {
            Clear();
            if (Instance == this) Instance = null;
        }

        // === Public API ===

        /// <summary>
        /// Highlight a target GameObject with outline for the specified duration.
        /// Pass duration=0 for infinite highlight (until Clear() is called).
        /// Pass target=null to clear immediately.
        /// </summary>
        public void Highlight(GameObject target, float duration = -1f)
        {
            if (duration < 0f) duration = _defaultDuration;

            // Same target — just refresh the timer.
            if (target == _currentTarget)
            {
                if (duration > 0f) RestartExpireTimer(duration);
                return;
            }

            // Switch target: clear old, apply new.
            Clear();

            if (target == null) return;

            _currentTarget = target;
            ApplyOutline(target);

            if (duration > 0f) RestartExpireTimer(duration);
        }

        /// <summary>
        /// Remove outline from the current target immediately.
        /// </summary>
        public void Clear()
        {
            if (_expireRoutine != null)
            {
                StopCoroutine(_expireRoutine);
                _expireRoutine = null;
            }

            if (_currentTarget != null)
            {
                RemoveOutline(_currentTarget);
                _currentTarget = null;
            }
        }

        /// <summary>
        /// Returns the currently highlighted GameObject, or null.
        /// </summary>
        public GameObject CurrentTarget => _currentTarget;

        // === Internal ===

        private void ApplyOutline(GameObject target)
        {
            if (_outlineMaterial == null) return;

            var renderers = target.GetComponentsInChildren<SkinnedMeshRenderer>();
            foreach (var r in renderers)
            {
                AddOutlineToRenderer(r);
            }

            var meshRenderers = target.GetComponentsInChildren<MeshRenderer>();
            foreach (var r in meshRenderers)
            {
                // Skip renderers that are part of UI or debug visuals.
                if (r.gameObject.name.Contains("VisualMarker")) continue;
                AddOutlineToRenderer(r);
            }
        }

        private void AddOutlineToRenderer(Renderer renderer)
        {
            if (renderer == null || _outlineMaterial == null) return;

            var sharedMats = renderer.sharedMaterials;
            // Check if outline is already applied.
            for (int i = 0; i < sharedMats.Length; i++)
            {
                if (sharedMats[i] == _outlineMaterial) return; // already present
            }

            // Append outline material as a second material.
            var newMats = new Material[sharedMats.Length + 1];
            sharedMats.CopyTo(newMats, 0);
            newMats[newMats.Length - 1] = _outlineMaterial;
            renderer.sharedMaterials = newMats;
            // Convert to instance materials so we don't pollute shared assets.
            renderer.materials = newMats; // triggers material instantiation
        }

        private void RemoveOutline(GameObject target)
        {
            if (_outlineMaterial == null) return;

            var renderers = target.GetComponentsInChildren<SkinnedMeshRenderer>();
            foreach (var r in renderers)
            {
                RemoveOutlineFromRenderer(r);
            }

            var meshRenderers = target.GetComponentsInChildren<MeshRenderer>();
            foreach (var r in meshRenderers)
            {
                RemoveOutlineFromRenderer(r);
            }
        }

        private void RemoveOutlineFromRenderer(Renderer renderer)
        {
            if (renderer == null) return;

            var mats = renderer.sharedMaterials;
            int outlineCount = 0;
            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] == _outlineMaterial) outlineCount++;
            }
            if (outlineCount == 0) return;

            var newMats = new Material[mats.Length - outlineCount];
            int dst = 0;
            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] != _outlineMaterial)
                {
                    newMats[dst++] = mats[i];
                }
            }
            renderer.materials = newMats;
        }

        private void RestartExpireTimer(float duration)
        {
            if (_expireRoutine != null)
            {
                StopCoroutine(_expireRoutine);
            }
            _expireRoutine = StartCoroutine(ExpireAfter(duration));
        }

        private IEnumerator ExpireAfter(float duration)
        {
            yield return new WaitForSeconds(duration);
            Clear();
        }
    }
}
