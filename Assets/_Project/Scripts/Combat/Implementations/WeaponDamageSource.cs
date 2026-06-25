// Project C: Real-Time Combat Engine — T-RTC04 + T-CB03
// WeaponDamageSource: IDamageSource adapter для WeaponItemData (T-CB03).
// Design: docs/Character/Skills/real-time-combat/60_NEXT_STEPS_T-CB01.md §2.3.
//
// До T-CB07 — GetSkillMultiplier возвращает 1.0 (навыки не подключены).
// После T-CB07 — читает SkillsWorld.GetLearnedSkillIds, накапливает mult из
// SkillEffect (type=StatMod, multiplier>0) — без cap per 2.18.
//
// До T-CB06 — GetArmorMult (в DamageCalculator) = 1.0 (default Physical/Ballistic).
// TODO (post T-CB06): GetArmorMult в PlayerTarget читает armorDefense.

using ProjectC.Combat.Core;

namespace ProjectC.Combat
{
    /// <summary>
    /// IDamageSource adapter для <see cref="ProjectC.Equipment.WeaponItemData"/>.
    /// Создаётся в <see cref="PlayerAttacker.RebuildSources"/> при equip оружия.
    /// </summary>
    public sealed class WeaponDamageSource : IDamageSource
    {
        private readonly ProjectC.Equipment.WeaponItemData _weapon;
        private readonly ulong _sourceId;

        public WeaponDamageSource(ProjectC.Equipment.WeaponItemData weapon, ulong sourceId)
        {
            _weapon = weapon;
            _sourceId = sourceId;
        }

        public ulong GetSourceId() => _sourceId;
        public DamageType GetDamageType() => _weapon.damageType;
        public DamageDice GetDamageDice() => _weapon.damageDice;
        public int GetBaseDamage() => _weapon.baseDamage;
        public int GetCritModifier() => _weapon.critModifier;
        public float GetRange() => _weapon.range;
        public string GetDisplayName() => _weapon.itemName;

        /// <summary>
        /// Cooldown per damage dice tier (per design):
        ///   d4/d6 → 1.0s
        ///   d8/d10 → 1.5s
        ///   d12/d20 → 2.5s
        /// </summary>
        public float GetCooldownSeconds() => _weapon.damageDice switch
        {
            DamageDice.d4 or DamageDice.d6 => 1.0f,
            DamageDice.d8 or DamageDice.d10 => 1.5f,
            DamageDice.d12 or DamageDice.d20 => 2.5f,
            _ => 1.0f,
        };

        /// <summary>
        /// T-CB07 hook: skillMult = product(1.0 + eff.multiplier) для всех StatMod-эффектов
        /// изученных навыков атакующего. Без cap per answer 2.18.
        ///
        /// v0.1 (T-CB03): возвращает 1.0. Реальная интеграция — в T-CB07 (следующий этап).
        /// </summary>
        public float GetSkillMultiplier(ulong attackerId)
        {
            // TODO T-CB07:
            // var learned = ProjectC.Skills.SkillsWorld.Instance?.GetLearnedSkillIds(attackerId);
            // if (learned == null) return 1.0f;
            // float mult = 1.0f;
            // foreach (var skillId in learned) {
            //     if (!ProjectC.Skills.SkillsWorld.Instance.TryGetSkill(skillId, out var skill)) continue;
            //     if (skill.effects == null) continue;
            //     foreach (var eff in skill.effects) {
            //         if (eff.type == ProjectC.Skills.SkillEffect.Type.StatMod && eff.multiplier > 0f) {
            //             mult *= (1.0f + eff.multiplier);
            //         }
            //     }
            // }
            // return mult;
            return 1.0f;
        }
    }
}
