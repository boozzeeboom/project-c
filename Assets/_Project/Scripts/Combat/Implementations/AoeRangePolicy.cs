// Project C: Real-Time Combat Engine
// AoeRangePolicy — guaranteed hit for thrown/AOE skills (grenades, bombs).
// Always returns IsInRange=true, CalculateHitChance=1.0.

using UnityEngine;
using ProjectC.Combat.Core;

namespace ProjectC.Combat
{
    public sealed class AoeRangePolicy : IRangePolicy
    {
        public bool RequiresLineOfSight => false;

        public float Distance(IAttacker a, IDamageTarget t) =>
            Vector3.Distance(a.GetPosition(), t.GetPosition());

        public bool IsInRange(IAttacker a, IDamageTarget t, IDamageSource s) => true;

        public float CalculateHitChance(IAttacker a, IDamageTarget t, IDamageSource s) => 1.0f;
    }
}