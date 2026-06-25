// Project C: Real-Time Combat Engine — T-RTC04
// MeleeRangePolicy — distance check + hit chance для ближнего боя (меч, копьё, кулак).
// Design: docs/Character/Skills/real-time-combat/10_DESIGN.md §3.6,
//         docs/Character/Skills/real-time-combat/30_PITFALLS.md §1.2 (hitChance tuning).

using UnityEngine;
using ProjectC.Combat.Core;

namespace ProjectC.Combat
{
    /// <summary>
    /// Melee range: 0..3м. Hit chance = base * distMod * dexMod.
    /// Per answer 2.1: <c>dexMod = 0.85 + (DEX-10) * 0.015</c> (clamp 0..1).
    /// </summary>
    public sealed class MeleeRangePolicy : IRangePolicy
    {
        public bool RequiresLineOfSight => false;  // MVP: без raycast

        public float Distance(IAttacker a, IDamageTarget t) =>
            Vector3.Distance(a.GetPosition(), t.GetPosition());

        public bool IsInRange(IAttacker a, IDamageTarget t, IDamageSource s) =>
            Distance(a, t) <= s.GetRange() + 0.5f;  // 0.5м tolerance

        public float CalculateHitChance(IAttacker a, IDamageTarget t, IDamageSource s)
        {
            float dist = Distance(a, t);

            // distMod: 1.0 на dist ≤ 1.5м, 0 на dist ≥ 3.5м
            float distMod = Mathf.Clamp01(1f - (dist - 1.5f) / 2f);

            // dexMod: 0.85 на DEX 10, 0.925 на DEX 20 (per 2.1)
            float dex = a.GetDexterity();
            float dexMod = Mathf.Clamp01(0.85f + (dex - 10) * 0.015f);

            const float baseMelee = 0.85f;  // CombatConfig.baseMeleeHitChance (hardcoded MVP)
            return Mathf.Clamp01(baseMelee * distMod * dexMod);
        }
    }
}
