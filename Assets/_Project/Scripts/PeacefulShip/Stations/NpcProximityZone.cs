// T-NS-AV01: NpcProximityZone — две зоны вокруг NPC-корабля для расхождения (см.
// docs/NPC_others_peacfull/npc_ship/07_SHIP_PROXIMITY_AVOIDANCE.md).
//
// Паттерн: OuterCommZone (Docking/Zones/OuterCommZone.cs) — зона+радиус в инспекторе.
// Отличие: детекция целей — по дистанции через NpcShipZoneRegistry, БЕЗ физ-триггеров
// (коллизии между NPC намеренно приглушены — см. M2_FSM_DIAGNOSIS.md §7).
//
// Компонент опционален: если его нет на корабле — расхождение выключено для него.

using ProjectC.PeacefulShip.Network; // NpcShipZoneRegistry
using UnityEngine;

namespace ProjectC.PeacefulShip.Stations
{
    /// <summary>
    /// Две сферические зоны вокруг NPC-корабля:
    ///  • awarenessRadius (большая) — «кого я знаю» рядом;
    ///  • avoidanceRadius (малая) — пересечение запускает манёвр расхождения.
    /// Радиусы настраиваются в инспекторе на каждом корабле индивидуально.
    /// </summary>
    public class NpcProximityZone : MonoBehaviour
    {
        [Header("Zones (per-ship, tune in Inspector)")]
        [Tooltip("Большая зона связи: в её радиусе корабль 'знает' о других NPC-кораблях.")]
        [Min(1f)] [SerializeField] private float awarenessRadius = 400f;

        [Tooltip("Малая зона расхождения: пересечение с чужой малой зоной запускает манёвр.")]
        [Min(1f)] [SerializeField] private float avoidanceRadius = 120f;

        [Tooltip("Гистерезис выхода: зона считается 'разошедшейся' на avoidanceRadius * этот множитель.")]
        [Range(1f, 3f)] [SerializeField] private float clearHysteresis = 1.5f;

        [Header("Buildings")]
        [Tooltip("Учитывать NpcProximityZoneBuilds (здания/препятствия) при поиске конфликтов.")]
        [SerializeField] private bool considerBuildings = true;

        [Header("Debug")]
        [SerializeField] private bool drawGizmos = true;
        [SerializeField] private bool verboseBuildLogging = false;

        public float AwarenessRadius => awarenessRadius;
        public float AvoidanceRadius => avoidanceRadius;
        public bool ConsiderBuildings => considerBuildings;
        /// <summary>Радиус, с которого считаем что конфликт исчерпан (гистерезис).</summary>
        public float ClearRadius => avoidanceRadius * clearHysteresis;

        private NpcShipController _self;

        private void Awake()
        {
            _self = GetComponent<NpcShipController>();
            if (_self == null)
                Debug.LogError($"[NpcProximityZone:{gameObject.name}] no NpcShipController on root!", this);
        }

        /// <summary>
        /// Server-only: ближайший NPC-корабль, чья малая зона пересекается с нашей и
        /// который находится в пределах нашей зоны связи. null — если конфликтов нет.
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

                float d = Vector3.Distance(myPos, other.transform.position);
                if (d > awarenessRadius) continue;                 // вне зоны связи — не учитываем
                if (d >= avoidanceRadius + oz.AvoidanceRadius) continue; // малые зоны не пересеклись

                if (d < dist)
                {
                    dist = d;
                    closest = other;
                }
            }

            return closest;
        }

        /// <summary>
        /// Расходимся только от кораблей в свободном полёте. Корабли на паде/финальном
        /// подходе (Docked/Berthing) игнорируем — там очередь держит pad-contention.
        /// </summary>
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
        /// Server-only: ближайшая building-зона (NpcProximityZoneBuilds),
        /// чьи padded bounds пересекаются с нашей avoidance-сферой.
        /// null — если конфликтов нет или considerBuildings = false.
        /// </summary>
        public NpcProximityZoneBuilds FindClosestBuildConflict(out float dist)
        {
            dist = float.MaxValue;
            if (!considerBuildings) return null;

            NpcProximityZoneBuilds closest = null;
            Vector3 myPos = transform.position;

            if (verboseBuildLogging)
                Debug.Log($"[NpcProximityZone:{gameObject.name}] scanning {NpcBuildZoneRegistry.All.Count} build zones...");

            foreach (var build in NpcBuildZoneRegistry.All)
            {
                if (build == null || !build.isActiveAndEnabled) continue;
                if (!build.IsIntruding(myPos, avoidanceRadius)) continue;

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

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!drawGizmos) return;
            // Большая зона связи
            Gizmos.color = new Color(0.3f, 0.7f, 1f, 0.20f);
            Gizmos.DrawWireSphere(transform.position, awarenessRadius);
            // Малая зона расхождения
            Gizmos.color = new Color(1f, 0.35f, 0.25f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, avoidanceRadius);
        }
#endif
    }
}
