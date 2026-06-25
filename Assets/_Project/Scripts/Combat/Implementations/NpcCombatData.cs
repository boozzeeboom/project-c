// Project C: Real-Time Combat Engine — T-RTC03
// NpcCombatData: ScriptableObject с параметрами NPC-врага (HP, STR/DEX/INT, weapon defaults).
// Design: docs/Character/Skills/real-time-combat/10_DESIGN.md §3.3.
//
// MVP placeholder: один тип NPC-врага (goblin-like). Designer-конфигурируемые значения
// в инспекторе. Реальный NPC-AI (агрессия, flee) — отдельная подсистема (out of scope).

using UnityEngine;
using ProjectC.Combat.Core;

namespace ProjectC.Combat
{
    [CreateAssetMenu(fileName = "NpcCombatData_", menuName = "Project C/Combat/NPC Combat Data")]
    public class NpcCombatData : ScriptableObject
    {
        [Header("Identity")]
        public string displayName = "Goblin";
        [Range(1, 1000)] public int maxHp = 30;

        [Header("Stats (default 10, как у PlayerStats tier0)")]
        [Range(1, 30)] public int strength = 10;
        [Range(1, 30)] public int dexterity = 10;
        [Range(1, 30)] public int intelligence = 10;

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
