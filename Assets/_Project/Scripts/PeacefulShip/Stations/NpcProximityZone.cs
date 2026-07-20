// T-NS-AV01: NpcProximityZone — две зоны вокруг NPC-корабля для расхождения.
// T-NS-BZ06: ZoneShape (Sphere | Box) — дизайнер выбирает форму avoidance-зоны.
//
// Sphere: awarenessRadius + avoidanceRadius (как раньше).
// Box:    awarenessRadius (сфера «кого я знаю») + avoidancePadding вокруг коллайдера.
//
// Паттерн: OuterCommZone (Docking/Zones/OuterCommZone.cs).

using ProjectC.PeacefulShip.Network; // NpcShipZoneRegistry, NpcBuildZoneRegistry
using UnityEngine;

namespace ProjectC.PeacefulShip.Stations
{
    public enum ZoneShape : byte { Sphere, Box }

    public class NpcProximityZone : MonoBehaviour
    {
        // ── Zone shape ──
        [Header("Zone Shape")]
        [Tooltip("Sphere: две сферы (awareness + avoidance радиусы).\nBox: awareness-сфера + avoidance-бокс вокруг коллайдера.")]
        [SerializeField] private ZoneShape zoneShape = ZoneShape.Sphere;

        // ── Sphere params ──
        [Header("Sphere (used when ZoneShape = Sphere)")]
        [Tooltip("Большая зона связи: в её радиусе корабль 'знает' о других NPC-кораблях.")]
        [Min(1f)] [SerializeField] private float awarenessRadius = 400f;

        [Tooltip("Малая зона расхождения: пересечение с чужой малой зоной запускает манёвр.")]
        [Min(1f)] [SerializeField] private float avoidanceRadius = 120f;

        // ── Box params ──
        [Header("Box (used when ZoneShape = Box)")]
        [Tooltip("Отступ от коллайдера корабля для avoidance-зоны. Аналог avoidanceRadius, но для бокса.")]
        [Min(1f)] [SerializeField] private float avoidancePadding = 20f;

        // ── Shared ──
        [Header("Hysteresis (shared)")]
        [Tooltip("Гистерезис выхода: clear-зона = avoidance-зона * этот множитель.")]
        [Range(1f, 3f)] [SerializeField] private float clearHysteresis = 1.5f;

        [Header("Buildings")]
        [Tooltip("Учитывать NpcProximityZoneBuilds (здания/препятствия) при поиске конфликтов.")]
        [SerializeField] private bool considerBuildings = true;

        [Header("Debug")]
        [SerializeField] private bool drawGizmos = true;
        [SerializeField] private bool verboseBuildLogging = false;

        // ── Public accessors ──
        public ZoneShape Shape => zoneShape;
        public float AwarenessRadius => awarenessRadius;
        public bool ConsiderBuildings => considerBuildings;

        /// <summary>Effective avoidance extent (sphere: radius, box: padding).</summary>
        public float AvoidanceExtent => zoneShape == ZoneShape.Sphere ? avoidanceRadius : avoidancePadding;

        /// <summary>Effective clear extent — avoidance extent × hysteresis.</summary>
        public float ClearExtent => AvoidanceExtent * clearHysteresis;

        // Legacy accessor (kept for backward compat in NpcShipController.IsClearOfConflict path)
        public float AvoidanceRadius => avoidanceRadius;
        public float ClearRadius => avoidanceRadius * clearHysteresis;

        private NpcShipController _self;
        private Collider _shipCollider;

        private void Awake()
        {
            _self = GetComponent<NpcShipController>();
            if (_self == null)
                Debug.LogError($"[NpcProximityZone:{gameObject.name}] no NpcShipController on root!", this);
            _shipCollider = FindLargestCollider();
        }

        private Collider FindLargestCollider()
        {
            var all = GetComponentsInChildren<Collider>();
            Collider best = null;
            float bestVol = 0f;
            foreach (var c in all)
            {
                if (c.isTrigger) continue;
                var sz = c.bounds.size;
                float vol = sz.x * sz.y * sz.z;
                if (vol > bestVol) { bestVol = vol; best = c; }
            }
            if (best == null && all.Length > 0) best = all[0]; // fallback: any collider
            return best;
        }

        // ══════════════════════════════════════════════════════════
        // Shape-aware geometry
        // ══════════════════════════════════════════════════════════

        /// <summary>Padded bounds around ship collider (box mode only).</summary>
        public Bounds GetAvoidanceBounds()
        {
            if (_shipCollider == null) return new Bounds(transform.position, Vector3.zero);
            Bounds b = _shipCollider.bounds;
            b.Expand(avoidancePadding);
            return b;
        }

        /// <summary>Padded bounds for clear check (box mode, with hysteresis).</summary>
        private Bounds GetClearBounds()
        {
            if (_shipCollider == null) return new Bounds(transform.position, Vector3.zero);
            Bounds b = _shipCollider.bounds;
            b.Expand(ClearExtent);
            return b;
        }

        /// <summary>Closest point on MY avoidance zone to a world position.</summary>
        public Vector3 ClosestPoint(Vector3 worldPos)
        {
            if (zoneShape == ZoneShape.Sphere)
            {
                Vector3 toPos = worldPos - transform.position;
                float dist = toPos.magnitude;
                if (dist <= avoidanceRadius) return worldPos;
                return transform.position + toPos.normalized * avoidanceRadius;
            }
            return GetAvoidanceBounds().ClosestPoint(worldPos);
        }

        /// <summary>Closest point on MY clear zone (with hysteresis).</summary>
        private Vector3 ClosestClearPoint(Vector3 worldPos)
        {
            if (zoneShape == ZoneShape.Sphere)
            {
                float r = ClearExtent;
                Vector3 toPos = worldPos - transform.position;
                float dist = toPos.magnitude;
                if (dist <= r) return worldPos;
                return transform.position + toPos.normalized * r;
            }
            return GetClearBounds().ClosestPoint(worldPos);
        }

        /// <summary>True если avoidance-зоны пересекаются (без гистерезиса).</summary>
        public bool AvoidanceIntersects(NpcProximityZone other)
        {
            return IntersectsWith(other, false);
        }

        /// <summary>True если clear-зоны пересекаются (с гистерезисом).</summary>
        public bool ClearIntersects(NpcProximityZone other)
        {
            return IntersectsWith(other, true);
        }

        private bool IntersectsWith(NpcProximityZone other, bool useClear)
        {
            float myExt = useClear ? ClearExtent : AvoidanceExtent;
            float otherExt = useClear ? other.ClearExtent : other.AvoidanceExtent;

            if (zoneShape == ZoneShape.Sphere && other.zoneShape == ZoneShape.Sphere)
            {
                float d = Vector3.Distance(transform.position, other.transform.position);
                return d < myExt + otherExt;
            }
            if (zoneShape == ZoneShape.Sphere)
            {
                // My sphere vs other box
                Vector3 otherClosest = useClear ? other.ClosestClearPoint(transform.position)
                                                : other.ClosestPoint(transform.position);
                return Vector3.Distance(transform.position, otherClosest) < myExt;
            }
            if (other.zoneShape == ZoneShape.Sphere)
            {
                // My box vs other sphere
                Vector3 myClosest = useClear ? ClosestClearPoint(other.transform.position)
                                             : ClosestPoint(other.transform.position);
                return Vector3.Distance(other.transform.position, myClosest) < otherExt;
            }
            // Box vs Box: check if AABBs overlap
            Bounds a = useClear ? GetClearBounds() : GetAvoidanceBounds();
            Bounds b = useClear ? other.GetClearBounds() : other.GetAvoidanceBounds();
            float dx = Mathf.Max(0, a.min.x - b.max.x, b.min.x - a.max.x);
            float dy = Mathf.Max(0, a.min.y - b.max.y, b.min.y - a.max.y);
            float dz = Mathf.Max(0, a.min.z - b.max.z, b.min.z - a.max.z);
            return (dx * dx + dy * dy + dz * dz) < 0.0001f;
        }

        /// <summary>Метрика дистанции до конфликта (меньше = ближе).</summary>
        public float DistanceToConflict(NpcProximityZone other)
        {
            if (zoneShape == ZoneShape.Sphere && other.zoneShape == ZoneShape.Sphere)
                return Vector3.Distance(transform.position, other.transform.position);

            if (zoneShape == ZoneShape.Sphere)
                return Vector3.Distance(transform.position, other.ClosestPoint(transform.position));

            if (other.zoneShape == ZoneShape.Sphere)
                return Vector3.Distance(other.transform.position, ClosestPoint(other.transform.position));

            // Box vs Box
            Bounds a = GetAvoidanceBounds();
            Bounds b = other.GetAvoidanceBounds();
            float dx = Mathf.Max(0, a.min.x - b.max.x, b.min.x - a.max.x);
            float dy = Mathf.Max(0, a.min.y - b.max.y, b.min.y - a.max.y);
            float dz = Mathf.Max(0, a.min.z - b.max.z, b.min.z - a.max.z);
            return Mathf.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        // ══════════════════════════════════════════════════════════
        // Conflict detection
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Server-only: ближайший NPC-корабль, чья avoidance-зона пересекается с нашей.
        /// Awareness — всегда по сфере (центр-центр). Avoidance — shape-aware.
        /// </summary>
        public NpcShipController FindClosestConflict(out float dist)
        {
            dist = float.MaxValue;
            NpcShipController closest = null;
            Vector3 myPos = transform.position;

            foreach (var kv in NpcShipZoneRegistry.All)
            {
                var other = kv.Value;
                if (other == null || other == _self) continue;
                if (!IsAvoidable(other)) continue;

                var oz = other.ProximityZone;
                if (oz == null) continue;

                // Awareness: always sphere (center distance)
                float centerDist = Vector3.Distance(myPos, other.transform.position);
                if (centerDist > awarenessRadius) continue;

                // Avoidance: shape-aware intersection
                if (!oz.AvoidanceIntersects(this)) continue;

                float conflictDist = oz.DistanceToConflict(this);
                if (conflictDist < dist)
                {
                    dist = conflictDist;
                    closest = other;
                }
            }

            return closest;
        }

        private static bool IsAvoidable(NpcShipController c)
        {
            var m = c.CurrentMode;
            return m == NpcShipController.NavMode.Lifting
                || m == NpcShipController.NavMode.Yawing
                || m == NpcShipController.NavMode.Cruising
                || m == NpcShipController.NavMode.Avoiding
                || m == NpcShipController.NavMode.AvoidYield;
        }

        /// <summary>
        /// Server-only: ближайшая building-зона, чьи padded bounds пересекаются с нашей avoidance-зоной.
        /// </summary>
        public NpcProximityZoneBuilds FindClosestBuildConflict(out float dist)
        {
            dist = float.MaxValue;
            if (!considerBuildings) return null;

            NpcProximityZoneBuilds closest = null;
            Vector3 myPos = transform.position;
            float myAvoid = AvoidanceExtent;

            if (verboseBuildLogging)
                Debug.Log($"[NpcProximityZone:{gameObject.name}] scanning {NpcBuildZoneRegistry.All.Count} build zones...");

            foreach (var build in NpcBuildZoneRegistry.All)
            {
                if (build == null || !build.isActiveAndEnabled) continue;
                if (!build.IsIntruding(myPos, myAvoid)) continue;

                float d = Vector3.Distance(myPos, build.ClosestPoint(myPos));
                if (verboseBuildLogging)
                    Debug.Log($"[NpcProximityZone:{gameObject.name}] build conflict: {build.gameObject.name} d={d:F1}");

                if (d < dist)
                {
                    dist = d;
                    closest = build;
                }
            }

            if (verboseBuildLogging && closest != null)
                Debug.Log($"[NpcProximityZone:{gameObject.name}] → closest build: {closest.gameObject.name} dist={dist:F1}");

            return closest;
        }

        // ══════════════════════════════════════════════════════════
        // Clear check (for IsClearOfConflict in NpcShipController)
        // ══════════════════════════════════════════════════════════

        /// <summary>True если clear-зоны (с гистерезисом) больше не пересекаются.</summary>
        public bool IsClearOf(NpcProximityZone other)
        {
            return !ClearIntersects(other);
        }

        /// <summary>True если clear-зона больше не пересекается с building-зоной.</summary>
        public bool IsClearOf(NpcProximityZoneBuilds build)
        {
            return !build.IsIntruding(transform.position, ClearExtent);
        }

        // ══════════════════════════════════════════════════════════
        // Gizmos
        // ══════════════════════════════════════════════════════════

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!drawGizmos) return;

            // Awareness: always sphere
            Gizmos.color = new Color(0.3f, 0.7f, 1f, 0.20f);
            Gizmos.DrawWireSphere(transform.position, awarenessRadius);

            if (zoneShape == ZoneShape.Sphere)
            {
                Gizmos.color = new Color(1f, 0.35f, 0.25f, 0.35f);
                Gizmos.DrawWireSphere(transform.position, avoidanceRadius);
            }
            else
            {
                if (_shipCollider == null) _shipCollider = FindLargestCollider();
                if (_shipCollider != null)
                {
                    Bounds b = GetAvoidanceBounds();
                    Gizmos.color = new Color(1f, 0.35f, 0.25f, 0.30f);
                    Gizmos.DrawWireCube(b.center, b.size);

                    // Original collider for reference
                    Gizmos.color = new Color(1f, 0.5f, 0f, 0.15f);
                    Gizmos.DrawWireCube(_shipCollider.bounds.center, _shipCollider.bounds.size);
                }
            }
        }
#endif
    }
}
