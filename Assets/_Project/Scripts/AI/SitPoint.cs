// Project C: Real-Time Combat Engine — T-NPC-S21
// SitPoint: маркер места для сидения (ручная расстановка дизайнером).
// Используется NpcSocialBrain при idle-активности Sit.
// Design: docs/Character/Skills/real-time-combat/npc-enemy/04_UNIFIED_BEHAVIOR_ARCHITECTURE.md §4 T-NPC-S21

using UnityEngine;

namespace ProjectC.AI
{
    /// <summary>
    /// Маркер точки для сидения (Sit idle-активность).
    /// Размещается дизайнером на сцене. NPC с idleActivity=Sit ищет ближайший SitPoint.
    /// </summary>
    public class SitPoint : MonoBehaviour
    {
        [Tooltip("Занято ли это место другим NPC.")]
        public bool IsOccupied
        {
            get
            {
                var cols = Physics.OverlapSphere(transform.position, occupyRadius);
                foreach (var c in cols)
                {
                    var brain = c.GetComponentInParent<NpcSocialBrain>();
                    if (brain != null && brain != _currentOccupant)
                        return true;
                }
                return false;
            }
        }

        [Tooltip("Радиус проверки занятости.")]
        [Range(0.3f, 3f)] public float occupyRadius = 1f;

        [Tooltip("Направление лица при сидении (локальное).")]
        public Vector3 sitForward = Vector3.forward;

        [System.NonSerialized] public NpcSocialBrain _currentOccupant;

        public Vector3 SitPosition => transform.position;
        public Quaternion SitRotation => transform.rotation * Quaternion.LookRotation(sitForward.normalized);

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.8f, 0.5f, 0.2f, 0.7f);
            Gizmos.DrawWireSphere(transform.position, occupyRadius * 0.3f);
            Gizmos.DrawWireCube(transform.position, new Vector3(0.6f, 0.4f, 0.6f));
            Gizmos.color = Color.yellow;
            Gizmos.DrawRay(transform.position, SitRotation * Vector3.forward * 0.5f);
        }
#endif
    }
}
