// Project C: Real-Time Combat Engine — T-RTC04
// RangedRangePolicy — distance check + hit chance для дальнего боя (арбалет, пневматика).
// Design: docs/Character/Skills/real-time-combat/10_DESIGN.md §3.7.

using UnityEngine;
using ProjectC.Combat.Core;

namespace ProjectC.Combat
{
    /// <summary>
    /// Ranged range: 5..100м (зависит от source.GetRange()). Hit chance ниже melee.
    /// Per answer 2.1: <c>dexMod = 0.85 + (DEX-10) * 0.015</c> (clamp 0..1).
    /// </summary>
    public sealed class RangedRangePolicy : IRangePolicy
    {
        public bool RequiresLineOfSight => false;  // MVP: без raycast; Phase 2 — true

        public float Distance(IAttacker a, IDamageTarget t) =>
            Vector3.Distance(a.GetPosition(), t.GetPosition());

        public bool IsInRange(IAttacker a, IDamageTarget t, IDamageSource s) =>
            Distance(a, t) <= s.GetRange();

        public float CalculateHitChance(IAttacker a, IDamageTarget t, IDamageSource s)
        {
            float dist = Distance(a, t);
            float maxRange = Mathf.Max(0.01f, s.GetRange());

            // distMod: 1.0 на dist=0, 0 на dist=maxRange (линейно)
            float distMod = Mathf.Clamp01(1f - dist / maxRange);

            // dexMod: 0.85 на DEX 10, 0.925 на DEX 20 (per 2.1)
            float dex = a.GetDexterity();
            float dexMod = Mathf.Clamp01(0.85f + (dex - 10) * 0.015f);

            const float baseRanged = 0.75f;  // CombatConfig.baseRangedHitChance (hardcoded MVP)
            return Mathf.Clamp01(baseRanged * distMod * dexMod);
        }
    }
}
