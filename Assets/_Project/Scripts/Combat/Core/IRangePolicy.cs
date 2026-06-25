// Project C: Real-Time Combat Engine — T-RTC01
// IRangePolicy interface — distance check strategy.
// Design: docs/Character/Skills/real-time-combat/10_DESIGN.md §2.4.
//
// Реализации: MeleeRangePolicy (ближний бой, MVP), RangedRangePolicy (дальний, MVP),
// ShipRangePolicy (Phase 3, 100-1000м + line-of-sight).

namespace ProjectC.Combat.Core
{
    /// <summary>
    /// Стратегия distance check + hit chance расчёт.
    /// </summary>
    public interface IRangePolicy
    {
        /// <summary>Target в радиусе источника?</summary>
        bool IsInRange(IAttacker attacker, IDamageTarget target, IDamageSource source);

        /// <summary>Distance в метрах.</summary>
        float Distance(IAttacker attacker, IDamageTarget target);

        /// <summary>Нужна ли line-of-sight? MVP = false (нет raycast). Phase 2 = true для ranged.</summary>
        bool RequiresLineOfSight { get; }

        /// <summary>
        /// Hit chance (0..1) на данной дистанции. Учитывает DEX, distMod, и т.п.
        /// Per answer 2.1: <c>dexMod = 0.85 + (DEX-10)*0.015</c>.
        /// </summary>
        float CalculateHitChance(IAttacker attacker, IDamageTarget target, IDamageSource source);
    }
}
