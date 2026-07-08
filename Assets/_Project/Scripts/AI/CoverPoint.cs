// Project C: Real-Time Combat Engine — T-NPC-S14
// CoverPoint: маркер укрытия (ручная расстановка дизайнером).
// Design: docs/Character/Skills/real-time-combat/npc-enemy/04_UNIFIED_BEHAVIOR_ARCHITECTURE.md §4 T-NPC-S14
//          + 02_SOCIAL_HUMAN_BEHAVIOR.md §2.3.2

using System.Collections.Generic;
using UnityEngine;

namespace ProjectC.AI

{
    /// <summary>
    /// Ручной маркер точки укрытия.
    /// Размещается дизайнером на сцене как Empty GameObject.
    /// CoverSeeker ищет ближайший CoverPoint при необходимости укрыться.
    /// </summary>
    public class CoverPoint : MonoBehaviour
    {
        // T-NPC-S00 P2 fix: статический реестр.
        public static readonly List<CoverPoint> AllCoverPoints = new List<CoverPoint>();

        [Header("Cover Properties")]

        [Tooltip("Приоритет укрытия (0=низкий, 10=высокий). Высокий приоритет выбирается первым.")]
        [Range(0, 10)] public int priority = 5;

        [Tooltip("Тип укрытия: Wall (полное), HalfWall (полурост), Pillar (узкое).")]
        public CoverType coverType = CoverType.Wall;

        [Tooltip("Радиус, в котором это укрытие считается «занятым» другим NPC.")]
        [Range(0.5f, 5f)] public float occupyRadius = 1.5f;

        [Tooltip("Смещение, куда встаёт NPC относительно pivot точки.")]
        public Vector3 standOffset = Vector3.zero;

        [Tooltip("Направление, куда NPC смотрит из укрытия (локальное).")]
        public Vector3 lookDirection = Vector3.forward;

        private void Awake() { AllCoverPoints.Add(this); }
        private void OnDestroy() { AllCoverPoints.Remove(this); }

        /// <summary>Занято ли это укрытие другим NPC?</summary>

        public bool IsOccupied
        {
            get
            {
                var cols = Physics.OverlapSphere(transform.position + standOffset, occupyRadius);
                foreach (var c in cols)
                {
                    var brain = c.GetComponentInParent<NpcSocialBrain>();
                    if (brain != null && brain != _currentOccupant)
                        return true;
                }
                return false;
            }
        }

        [System.NonSerialized] public NpcSocialBrain _currentOccupant;

        /// <summary>Мировая позиция, где стоит NPC (pivot + standOffset).</summary>
        public Vector3 StandPosition => transform.position + transform.TransformDirection(standOffset);

        /// <summary>Мировое направление взгляда из укрытия.</summary>
        public Vector3 LookDirectionWorld => transform.TransformDirection(lookDirection.normalized);

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            // Cover type color
            Color c = coverType switch
            {
                CoverType.Wall => new Color(0.2f, 0.8f, 0.2f, 0.7f),
                CoverType.HalfWall => new Color(0.8f, 0.8f, 0.2f, 0.7f),
                CoverType.Pillar => new Color(0.2f, 0.5f, 0.8f, 0.7f),
                _ => Color.gray,
            };

            Gizmos.color = c;
            Vector3 standPos = transform.position + transform.TransformDirection(standOffset);
            Gizmos.DrawWireSphere(standPos, occupyRadius * 0.3f);

            // Stand position
            Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.position, standPos);
            Gizmos.DrawWireCube(standPos, Vector3.one * 0.3f);

            // Look direction
            Gizmos.color = Color.cyan;
            Gizmos.DrawRay(standPos, LookDirectionWorld * 2f);

            // Icon
            Gizmos.color = c;
            Gizmos.DrawSphere(transform.position, 0.15f);
        }
#endif
    }

    /// <summary>
    /// Тип укрытия.
    /// </summary>
    public enum CoverType
    {
        /// <summary>Полное укрытие (стена). Защищает от прямого огня.</summary>
        Wall,
        /// <summary>Полурост (баррикада). Частичная защита.</summary>
        HalfWall,
        /// <summary>Узкое укрытие (колонна). Защищает с одной стороны.</summary>
        Pillar,
    }
}
