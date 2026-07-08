// Project C: Real-Time Combat Engine — T-NPC-S09 (Phase 2 prep)
// SocialTrigger: enum + evaluation data для 7 социальных триггеров.
// Design: docs/Character/Skills/real-time-combat/npc-enemy/04_UNIFIED_BEHAVIOR_ARCHITECTURE.md §4.

using ProjectC.Combat.Core;

namespace ProjectC.AI
{
    /// <summary>
    /// 7 социальных триггеров, отсортированных по приоритету.
    /// </summary>
    public enum SocialTriggerType
    {
        /// <summary>T1 (приоритет 10): союзник умер в allyDeathRadius.</summary>
        AllyKilled = 10,
        /// <summary>T2 (приоритет 9): лидер группы в Chase/Attack.</summary>
        LeaderAggrod = 9,
        /// <summary>T3 (приоритет 8): союзник в 15м в Chase/Attack.</summary>
        AllyInCombat = 8,
        /// <summary>T4 (приоритет 7): игрок ранее атаковал этого NPC (Grudge).</summary>
        GrudgeTrigger = 7,
        /// <summary>T5 (приоритет 6): игрок вошёл в trigger-зону.</summary>
        TerritoryViolation = 6,
        /// <summary>T6 (приоритет 5, модификатор): врагов > союзников × 1.5.</summary>
        Outnumbered = 5,
        /// <summary>T7 (приоритет 4, модификатор): союзники в 30м.</summary>
        ReinforcementNearby = 4,
    }

    /// <summary>
    /// Данные активного социального триггера.
    /// </summary>
    [System.Serializable]
    public struct SocialTriggerData
    {
        public SocialTriggerType Type;
        /// <summary>IDamageTarget, связанный с триггером (killer, target, etc).</summary>
        public IDamageTarget Target;
        /// <summary>ClientId игрока-обидчика (для GrudgeTrigger).</summary>
        public ulong PlayerClientId;

        public SocialTriggerData(SocialTriggerType type, IDamageTarget target = null, ulong playerClientId = 0)
        {
            Type = type;
            Target = target;
            PlayerClientId = playerClientId;
        }
    }
}
