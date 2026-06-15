// Project C: Character Progression — T-P11
// SkillEffect: atomic effect struct (applied when skill learned).
// Design: docs/Character/03_DATA_MODEL.md §4.2, docs/Character/06_SKILL_TREE.md §1.2
//
// Atomic design (per skill pitfall #33): один SkillEffect = один тип параметров.
// Избегаем composite struct (depth limit Unity serializer).

using System;
using ProjectC.Stats;

namespace ProjectC.Skills
{
    [Serializable]
    public struct SkillEffect
    {
        public enum Type : byte
        {
            StatMod        = 0,  // +X к STR/DEX/INT (additive и/или multiplicative)
            AbilityUnlock  = 1,  // открывает ability ID (для будущего оружия)
            PassiveEffect  = 2,  // generic passive (future use, e.g. "+10% dialog XP")
        }

        public Type type;
        /// <summary>Только для StatMod.</summary>
        public StatType statType;
        /// <summary>Additive bonus (StatMod) или duration (PassiveEffect). 0 = no additive.</summary>
        public float floatValue;
        /// <summary>Multiplicative bonus (StatMod), 0 = no multiplier. Range [0..5].</summary>
        public float multiplier;
        /// <summary>Ability id (AbilityUnlock) или passive id (PassiveEffect). "" = none.</summary>
        public string stringParam;

        // === Factory methods ===

        public static SkillEffect StatModAdd(StatType stat, float add) => new SkillEffect
        {
            type = Type.StatMod, statType = stat, floatValue = add, multiplier = 0f, stringParam = null,
        };

        public static SkillEffect StatModAddMul(StatType stat, float add, float mul) => new SkillEffect
        {
            type = Type.StatMod, statType = stat, floatValue = add, multiplier = mul, stringParam = null,
        };

        public static SkillEffect Ability(string abilityId) => new SkillEffect
        {
            type = Type.AbilityUnlock, statType = StatType.Strength, floatValue = 0f, multiplier = 0f,
            stringParam = abilityId ?? string.Empty,
        };

        public static SkillEffect Passive(string passiveId, float duration) => new SkillEffect
        {
            type = Type.PassiveEffect, statType = StatType.Strength, floatValue = duration,
            multiplier = 0f, stringParam = passiveId ?? string.Empty,
        };
    }
}
