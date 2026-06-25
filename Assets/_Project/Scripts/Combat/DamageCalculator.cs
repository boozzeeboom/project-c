// Project C: Real-Time Combat Engine — T-RTC05
// DamageCalculator — static class, server-authoritative ERPR damage formula.
// Design: docs/Character/Skills/real-time-combat/10_DESIGN.md §4,
//         docs/Character/Skills/Battle/10_DESIGN.md §7.1.
//
// ERPR-формула (server rolls dice, client NEVER calculates):
//   final = max(0, (1dN + base + STR) × locMult × critMult × skillMult) − effectiveDefense
// где:
//   - locMult = 1.0 (real-time, отключён per 2.17)
//   - critMult = 2.0 если (1d100 + critMod) ≥ 100, иначе 1.0
//   - skillMult = 1.0 (навыки opt-in, после T-CB01..T-CB09)
//   - effectiveDefense = armorDefense × typeMultiplier (Physical/Ballistic=1.0, Antigrav=0.5,
//     Explosive=0.7, Mesium=0.0)

using ProjectC.Combat.Core;
using UnityEngine;

namespace ProjectC.Combat
{
    public static class DamageCalculator
    {
        // === HitChance threshold for crit (ERPR 1d100) ===
        public const int BaseCritThreshold = 100;

        // === Crit multiplier ===
        public const float CritMultiplier = 2.0f;

        /// <summary>
        /// Полный расчёт damage. Вызывать ТОЛЬКО на сервере (rolling dice).
        /// </summary>
        /// <param name="attacker">IAttacker (Player/Npc/Ship), реализация через IAttacker interface.</param>
        /// <param name="defender">IDamageTarget.</param>
        /// <param name="source">IDamageSource (оружие, граната, ...).</param>
        /// <param name="rangePolicy">IRangePolicy (melee/ranged) — используется для hitChance.</param>
        /// <param name="skill">Опциональный SkillNodeConfig (T-CB07, после T-CB01..T-CB09). MVP = null.</param>
        public static DamageResult Calculate(
            IAttacker attacker,
            IDamageTarget defender,
            IDamageSource source,
            IRangePolicy rangePolicy,
            object skill = null)
        {
            // 1. Base attack: roll dN + base + STR
            int roll = source.GetDamageDice().Roll();
            int baseAttack = roll + source.GetBaseDamage() + attacker.GetStrength();

            // 2. Hit chance (from range policy)
            float hitChance = rangePolicy.CalculateHitChance(attacker, defender, source);
            bool isHit = Random.value < hitChance;

            ulong attackerId = attacker.GetAttackerId();
            ulong targetId = defender.GetTargetId();
            ulong sourceId = source.GetSourceId();
            DamageType dmgType = source.GetDamageType();

            if (!isHit)
            {
                return DamageResult.Miss(
                    hitChance, dmgType, attackerId, targetId, sourceId,
                    attacker.GetPosition(), defender.GetPosition());
            }

            // 3. Hit location — ОТКЛЮЧЕН в real-time (per 2.17)
            float locMult = 1.0f;
            byte hitLocation = 1;  // Torso (default)

            // 4. Crit (1d100 + critMod >= 100 → ×2)
            int critRoll = Random.Range(1, 101);
            bool isCrit = (critRoll + source.GetCritModifier()) >= BaseCritThreshold;
            float critMult = isCrit ? CritMultiplier : 1.0f;

            // 5. Skill multiplier (от навыков, opt-in, БЕЗ CAP per 2.18)
            //    MVP: всегда 1.0 (навыки не подключены). После T-CB07 — через source.GetSkillMultiplier.
            float skillMult = source.GetSkillMultiplier(attackerId);

            // 6. Pre-defense damage
            int preDefense = Mathf.RoundToInt(baseAttack * locMult * critMult * skillMult);

            // 7. Defense (armor × typeMultiplier)
            int totalArmor = defender.GetArmorDefense();
            float armorMult = dmgType.ArmorMultiplier();
            int effectiveDefense = Mathf.RoundToInt(totalArmor * armorMult);

            // 8. Final
            int final = Mathf.Max(0, preDefense - effectiveDefense);

            return new DamageResult
            {
                baseAttack = baseAttack,
                locMult = locMult,
                critMult = critMult,
                skillMult = skillMult,
                hitChance = hitChance,
                preDefenseDamage = preDefense,
                effectiveDefense = effectiveDefense,
                finalDamage = final,
                isCrit = isCrit,
                isHit = true,
                hitLocation = hitLocation,
                damageType = dmgType,
                attackerId = attackerId,
                targetId = targetId,
                sourceId = sourceId,
                attackerPosition = attacker.GetPosition(),
                targetPosition = defender.GetPosition(),
            };
        }
    }
}
