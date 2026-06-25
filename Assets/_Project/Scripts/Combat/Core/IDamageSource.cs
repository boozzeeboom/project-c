// Project C: Real-Time Combat Engine — T-RTC01
// IDamageSource interface — generic abstraction for ANY source of damage.
// Design: docs/Character/Skills/real-time-combat/10_DESIGN.md §2.3.
//
// Реализации: WeaponDamageSource (меч, после T-CB03), DefaultDamageSource (fallback
// до T-CB03), ExplosionDamageSource (Phase 2), TurretDamageSource (Phase 3, ship),
// AntigravPulseDamageSource (Phase 3).

using UnityEngine;

namespace ProjectC.Combat.Core
{
    /// <summary>
    /// Что угодно, что наносит урон: оружие, граната, мина, турель, g-волна.
    /// </summary>
    public interface IDamageSource
    {
        /// <summary>Stable id (для RPC: "use source #N").</summary>
        ulong GetSourceId();

        /// <summary>Damage type (Physical/Ballistic/Antigrav/Explosive/Mesium).</summary>
        DamageType GetDamageType();

        /// <summary>Damage dice (d4-d20, ERPR).</summary>
        DamageDice GetDamageDice();

        /// <summary>Базовый урон (без dice, без модификаторов).</summary>
        int GetBaseDamage();

        /// <summary>Crit modifier: 1d100 + critMod &gt;= 100 → crit ×2.</summary>
        int GetCritModifier();

        /// <summary>Range в метрах (для IRangePolicy.IsInRange).</summary>
        float GetRange();

        /// <summary>Cooldown между атаками (seconds).</summary>
        float GetCooldownSeconds();

        /// <summary>
        /// Skill multiplier (от навыков, opt-in). MVP = 1.0.
        /// После T-CB01..T-CB09 (MVP+1): читает SkillsWorld.GetLearnedSkills.
        /// </summary>
        float GetSkillMultiplier(ulong attackerId);

        /// <summary>Display name (для UI/log).</summary>
        string GetDisplayName();
    }
}
