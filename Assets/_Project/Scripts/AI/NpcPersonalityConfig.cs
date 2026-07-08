// Project C: Real-Time Combat Engine — T-NPC-S07
// NpcPersonalityConfig: ScriptableObject с 5 чертами личности NPC.
// Design: docs/Character/Skills/real-time-combat/npc-enemy/04_UNIFIED_BEHAVIOR_ARCHITECTURE.md §2.2.

using UnityEngine;

namespace ProjectC.AI
{
    /// <summary>
    /// Конфигурация личности NPC.
    /// Traits влияют на пороги эмоций, скорость переходов и боевые решения.
    /// Если не задан — используются дефолты (courage=0.7, aggression=0.6, ...).
    /// </summary>
    [CreateAssetMenu(fileName = "NpcPersonality_", menuName = "Project C/AI/Npc Personality")]
    public class NpcPersonalityConfig : ScriptableObject
    {
        [Header("Core Traits (0..1)")]
        [Tooltip("0 = трус (бежит при HP<50%), 1 = храбрец (бежит только при HP<15%).")]
        [Range(0f, 1f)] public float courage = 0.7f;

        [Tooltip("0 = избегает боя, 1 = ищет бой. При >0.7 пропускает Warning phase.")]
        [Range(0f, 1f)] public float aggression = 0.6f;

        [Tooltip("0 = бросит группу при опасности, 1 = умрёт за группу.")]
        [Range(0f, 1f)] public float loyalty = 0.8f;

        [Tooltip("0 = осторожен, 1 = лезет на рожон (игнорирует численное меньшинство).")]
        [Range(0f, 1f)] public float recklessness = 0.3f;

        [Tooltip("0 = добивает surrendered врага, 1 = принимает сдачу.")]
        [Range(0f, 1f)] public float mercy = 0.2f;

        /// <summary>Дефолтный personality (courage=0.7, aggression=0.6, loyalty=0.8, recklessness=0.3, mercy=0.2).</summary>
        public static NpcPersonalityConfig Default
        {
            get
            {
                if (_default == null)
                {
                    _default = CreateInstance<NpcPersonalityConfig>();
                    _default.name = "NpcPersonality_Default";
                    _default.courage = 0.7f;
                    _default.aggression = 0.6f;
                    _default.loyalty = 0.8f;
                    _default.recklessness = 0.3f;
                    _default.mercy = 0.2f;
                }
                return _default;
            }
        }
        private static NpcPersonalityConfig _default;
    }
}
