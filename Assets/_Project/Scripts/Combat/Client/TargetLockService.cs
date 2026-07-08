// Project C: Real-Time Combat Engine — T-LOCK-01
// TargetLockService: client-side persistent target lock with Q/E cycling.
// Maintains a LockedTargetId across frames. Q/E cycles through targets.
// Only works on foot (IsInShip check in SkillInputService).
//
// Design: docs/Character/Skills/real-time-combat/100_TARGET_HIGHLIGHT_AND_SWITCHING.md §2

using System;
using System.Collections.Generic;
using UnityEngine;
using ProjectC.Combat.Core;

namespace ProjectC.Combat.Client
{
    /// <summary>
    /// Режим сортировки целей для Q/E cycling.
    /// В будущем — настройка игрока в UI Settings.
    /// </summary>
    public enum TargetCycleSortMode : byte
    {
        /// <summary>Ближайшая к персонажу цель — первая. CycleNext → дальше, CyclePrev → ближе.</summary>
        ByDistance = 0,

        /// <summary>Слева-направо по экрану (от camera.transform.right). CycleNext → правее, CyclePrev → левее.</summary>
        ByScreenAngle = 1,
    }

    /// <summary>
    /// Client-only singleton. Maintains a persistent lock on one IDamageTarget.
    /// Created in NetworkManagerController alongside TargetHighlightService.
    /// </summary>
    public class TargetLockService : MonoBehaviour
    {
        public static TargetLockService Instance { get; private set; }

        [Header("Target Cycling")]
        [Tooltip("Maximum range (meters) for target cycling. Targets beyond this are ignored.")]
        [SerializeField] private float _maxCycleRange = 50f;

        [Tooltip("How often (seconds) the target cache refreshes.")]
        [SerializeField] private float _cacheRefreshInterval = 0.3f;

        [Tooltip("Режим сортировки целей для Q/E. ByDistance = ближайшая первая (дефолт). ByScreenAngle = слева-направо по экрану.")]
        [SerializeField] private TargetCycleSortMode _sortMode = TargetCycleSortMode.ByDistance;

        /// <summary>Currently locked target ID. 0 = no lock.</summary>
        public ulong LockedTargetId { get; private set; }

        /// <summary>GameObject of the currently locked target, or null.</summary>
        public GameObject LockedTargetObject { get; private set; }

        /// <summary>Fired when target changes: (oldTarget, newTarget). Both can be null.</summary>
        public event Action<GameObject, GameObject> OnTargetChanged;

        // Cached list of valid targets, refreshed periodically.
        private readonly List<TargetEntry> _cachedTargets = new List<TargetEntry>();
        private float _nextCacheRefreshTime;

        private struct TargetEntry
        {
            public ulong targetId;
            public GameObject gameObject;
            public IDamageTarget damageTarget;
            public Vector3 position;
            public float sortKey; // distance (метры) для ByDistance, screenAngle (radians) для ByScreenAngle
        }

        // === Lifecycle ===

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[TargetLockService] Replacing existing Instance (duplicate).");
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Debug.Log("[TargetLockService] Awake: singleton created.");
        }

        private void OnDestroy()
        {
            Unlock();
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            // Auto-unlock if locked target dies or goes too far.
            if (LockedTargetId != 0UL)
            {
                bool valid = false;
                if (LockedTargetObject != null)
                {
                    var dt = LockedTargetObject.GetComponentInParent<IDamageTarget>();
                    if (dt != null && dt.IsAlive())
                    {
                        float dist = Vector3.Distance(
                            LockedTargetObject.transform.position,
                            GetPlayerPosition());
                        if (dist <= _maxCycleRange * 1.5f)
                        {
                            valid = true;
                        }
                    }
                }
                if (!valid)
                {
                    Unlock();
                    CycleNext();
                }
            }
        }

        // === Public API ===

        /// <summary>
        /// Lock onto a specific target by ID. If already locked on the same target, unlock (toggle).
        /// </summary>
        public void Lock(ulong targetId)
        {
            if (targetId == 0UL)
            {
                Unlock();
                return;
            }

            if (targetId == LockedTargetId)
            {
                Unlock();
                return;
            }

            var newTarget = FindGameObjectByTargetId(targetId);
            if (newTarget == null)
            {
                Debug.LogWarning($"[TargetLockService] Lock: targetId {targetId} not found in scene.");
                return;
            }

            SetLockedTarget(targetId, newTarget);
        }

        /// <summary>
        /// Clear the current lock.
        /// </summary>
        public void Unlock()
        {
            if (LockedTargetId == 0UL) return;

            var oldTarget = LockedTargetObject;
            LockedTargetId = 0UL;
            LockedTargetObject = null;

            TargetHighlightService.Instance?.Clear();
            OnTargetChanged?.Invoke(oldTarget, null);

            if (Debug.isDebugBuild) Debug.Log("[TargetLockService] Unlock: lock cleared.");
        }

        /// <summary>
        /// Cycle to the previous target (closer / more-left depending on sort mode).
        /// </summary>
        public void CyclePrev()
        {
            RefreshCache();
            if (_cachedTargets.Count == 0) return;

            SortTargets();

            int currentIndex = FindCurrentIndex();
            int newIndex;
            if (currentIndex < 0)
            {
                // No current lock — pick last in sorted list.
                newIndex = _cachedTargets.Count - 1;
            }
            else
            {
                newIndex = currentIndex - 1;
                if (newIndex < 0) newIndex = _cachedTargets.Count - 1;
            }

            var entry = _cachedTargets[newIndex];
            SetLockedTarget(entry.targetId, entry.gameObject);
        }

        /// <summary>
        /// Cycle to the next target (further / more-right depending on sort mode).
        /// </summary>
        public void CycleNext()
        {
            RefreshCache();
            if (_cachedTargets.Count == 0) return;

            SortTargets();

            int currentIndex = FindCurrentIndex();
            int newIndex;
            if (currentIndex < 0)
            {
                // No current lock — pick first in sorted list (closest / most-right).
                newIndex = 0;
            }
            else
            {
                newIndex = currentIndex + 1;
                if (newIndex >= _cachedTargets.Count) newIndex = 0;
            }

            var entry = _cachedTargets[newIndex];
            SetLockedTarget(entry.targetId, entry.gameObject);
        }

        // === Internal ===

        private void SetLockedTarget(ulong targetId, GameObject targetObj)
        {
            var oldTarget = LockedTargetObject;
            LockedTargetId = targetId;
            LockedTargetObject = targetObj;

            TargetHighlightService.Instance?.Highlight(targetObj, 0f);

            OnTargetChanged?.Invoke(oldTarget, targetObj);

            if (Debug.isDebugBuild)
            {
                var dt = targetObj != null ? targetObj.GetComponentInParent<IDamageTarget>() : null;
                float dist = targetObj != null
                    ? Vector3.Distance(targetObj.transform.position, GetPlayerPosition())
                    : 0f;
                Debug.Log($"[TargetLockService] Lock: targetId={targetId} displayName='{dt?.GetDisplayName() ?? "?"}' dist={dist:F1}m mode={_sortMode}");
            }
        }

        private int FindCurrentIndex()
        {
            if (LockedTargetId == 0UL) return -1;
            for (int i = 0; i < _cachedTargets.Count; i++)
            {
                if (_cachedTargets[i].targetId == LockedTargetId) return i;
            }
            return -1;
        }

        private void SortTargets()
        {
            Vector3 playerPos = GetPlayerPosition();

            switch (_sortMode)
            {
                case TargetCycleSortMode.ByDistance:
                    // Sort closest-first.
                    for (int i = 0; i < _cachedTargets.Count; i++)
                    {
                        var e = _cachedTargets[i];
                        e.sortKey = Vector3.Distance(e.position, playerPos);
                        _cachedTargets[i] = e;
                    }
                    _cachedTargets.Sort((a, b) => a.sortKey.CompareTo(b.sortKey));
                    break;

                case TargetCycleSortMode.ByScreenAngle:
                    // Sort left-to-right by horizontal screen angle.
                    var cam = Camera.main;
                    Vector3 camForward = cam != null ? cam.transform.forward : Vector3.forward;
                    Vector3 camRight = cam != null ? cam.transform.right : Vector3.right;
                    for (int i = 0; i < _cachedTargets.Count; i++)
                    {
                        var e = _cachedTargets[i];
                        Vector3 toTarget = (e.position - playerPos).normalized;
                        float dotRight = Vector3.Dot(toTarget, camRight);
                        float dotForward = Vector3.Dot(toTarget, camForward);
                        e.sortKey = Mathf.Atan2(dotRight, dotForward);
                        _cachedTargets[i] = e;
                    }
                    _cachedTargets.Sort((a, b) => a.sortKey.CompareTo(b.sortKey));
                    break;
            }
        }

        private void RefreshCache()
        {
            float now = Time.unscaledTime;
            if (now < _nextCacheRefreshTime && _cachedTargets.Count > 0) return;
            _nextCacheRefreshTime = now + _cacheRefreshInterval;

            _cachedTargets.Clear();
            Vector3 playerPos = GetPlayerPosition();
            float rangeSq = _maxCycleRange * _maxCycleRange;

            foreach (var npc in FindObjectsByType<NpcTarget>(FindObjectsSortMode.None))
            {
                if (npc == null || !npc.IsAlive()) continue;
                float dSq = (npc.transform.position - playerPos).sqrMagnitude;
                if (dSq > rangeSq) continue;
                _cachedTargets.Add(new TargetEntry
                {
                    targetId = npc.GetTargetId(),
                    gameObject = npc.gameObject,
                    damageTarget = npc,
                    position = npc.transform.position,
                });
            }

            foreach (var pt in FindObjectsByType<PlayerTarget>(FindObjectsSortMode.None))
            {
                if (pt == null || !pt.IsAlive()) continue;
                if (pt.GetTargetId() == GetLocalPlayerId()) continue;
                float dSq = (pt.transform.position - playerPos).sqrMagnitude;
                if (dSq > rangeSq) continue;
                _cachedTargets.Add(new TargetEntry
                {
                    targetId = pt.GetTargetId(),
                    gameObject = pt.gameObject,
                    damageTarget = pt,
                    position = pt.transform.position,
                });
            }
        }

        private Vector3 GetPlayerPosition()
        {
            var np = FindFirstObjectByType<ProjectC.Player.NetworkPlayer>();
            return np != null ? np.transform.position : Vector3.zero;
        }

        private ulong GetLocalPlayerId()
        {
            var np = FindFirstObjectByType<ProjectC.Player.NetworkPlayer>();
            return np != null ? np.OwnerClientId : 0UL;
        }

        private GameObject FindGameObjectByTargetId(ulong targetId)
        {
            if (targetId == 0UL) return null;
            foreach (var npc in FindObjectsByType<NpcTarget>(FindObjectsSortMode.None))
            {
                if (npc != null && npc.GetTargetId() == targetId) return npc.gameObject;
            }
            foreach (var pt in FindObjectsByType<PlayerTarget>(FindObjectsSortMode.None))
            {
                if (pt != null && pt.GetTargetId() == targetId) return pt.gameObject;
            }
            return null;
        }
    }
}
