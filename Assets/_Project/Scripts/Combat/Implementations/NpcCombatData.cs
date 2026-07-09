// Project C: Real-Time Combat Engine — T-RTC03
// NpcCombatData: ScriptableObject с параметрами NPC-врага (HP, STR/DEX/INT, weapon defaults).
// Design: docs/Character/Skills/real-time-combat/10_DESIGN.md §3.3.
//
// MVP placeholder: один тип NPC-врага (goblin-like). Designer-конфигурируемые значения
// в инспекторе. Реальный NPC-AI (агрессия, flee) — отдельная подсистема (out of scope).

using UnityEngine;
using UnityEngine.Serialization;
using ProjectC.Combat.Core;

namespace ProjectC.Combat
{
    [CreateAssetMenu(fileName = "NpcCombatData_", menuName = "Project C/Combat/NPC Combat Data")]
    public class NpcCombatData : ScriptableObject
    {
        [Header("Identity")]
        public string displayName = "Goblin";
        [Range(1, 1000)] public int maxHp = 30;

        [Header("Stats (tier-based, formula: StatsToFlat(tier) = tier*5+10). P3/P9: unified with Player.")]
        [Tooltip("Tier 0 = STR 10, tier 2 = STR 20, etc. (same formula as PlayerStats.StatsToFlat).")]
        [FormerlySerializedAs("strength")] [Range(0, 20)] public int strengthTier = 0;
        [FormerlySerializedAs("dexterity")] [Range(0, 20)] public int dexterityTier = 0;
        [FormerlySerializedAs("intelligence")] [Range(0, 20)] public int intelligenceTier = 0;

        [Header("Default weapon (до T-CB03 — все NPC используют эти параметры)")]
        public DamageType damageType = DamageType.Physical;
        public DamageDice damageDice = DamageDice.d6;
        [Range(0, 50)] public int baseDamage = 2;
        [Range(-50, 50)] public int critModifier = 0;
        [Range(0.5f, 100f)] public float range = 2.0f;
        [Range(0.1f, 10f)] public float cooldownSeconds = 1.5f;

        [Header("AI stub (Phase 2 — реальный AI)")]
        [Tooltip("Минимальный интервал между атаками (server tick, 30Hz). 30 = 1 сек.")]
        [Range(1, 300)] public int minAttackInterval = 45;  // 1.5s @ 30Hz
    }
}
