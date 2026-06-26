// Project C: Character Progression — T-P11
// SkillEffect: atomic effect struct (applied when skill learned).
// Design: docs/Character/03_DATA_MODEL.md §4.2, docs/Character/06_SKILL_TREE.md §1.2
//
// Atomic design (per skill pitfall #33): один SkillEffect = один тип параметров.
// Избегаем composite struct (depth limit Unity serializer).
//
// T-CB01 expansion (2026-06-26): enum Type расширен с 3 до 8 значений (WeaponProficiency/Armor/Technique/ExplosiveRecipe/AntigravTechnique).
// Backward-compat: 8 существующих .asset используют StatMod=0 и AbilityUnlock=1 — новые code 3..7 не задевают их.

using System;
using ProjectC.Stats;

namespace ProjectC.Skills
{
    [Serializable]
    public struct SkillEffect
    {
        public enum Type : byte
        {
            // === Existing (T-P11, 0..2) — НЕ ТРОГАЕМ ===
            StatMod        = 0,  // +X к STR/DEX/INT (additive и/или multiplicative)
            AbilityUnlock  = 1,  // открывает ability ID (для будущего оружия)
            PassiveEffect  = 2,  // generic passive (future use, e.g. "+10% dialog XP")

            // === NEW (T-CB01, 3..7) — runtime-handler добавится в T-CB07 ===
            // Enum-only expansion. Существующие .asset используют 0..2 — backward-compat сохранён.
            // Семантика stringParam по type:
            //   WeaponProficiencyUnlock → WeaponClass (e.g. "sword") — открывает экипировку этого класса
            //   ArmorProficiencyUnlock  → ArmorClass (e.g. "heavy") — открывает экипировку этого класса
            //   WeaponTechniqueUnlock   → TechniqueId (e.g. "parry") — открывает технику (combat engine использует)
            //   ExplosiveRecipeUnlock   → RecipeId (e.g. "recipe_grenade_basic") — открывает крафт-рецепт
            //   AntigravTechniqueUnlock → TechniqueId (e.g. "grav_pull") — открывает antigrav-приём
            WeaponProficiencyUnlock = 3,
            ArmorProficiencyUnlock  = 4,
            WeaponTechniqueUnlock   = 5,
            ExplosiveRecipeUnlock   = 6,
            AntigravTechniqueUnlock = 7,
        }

        public Type type;
        /// <summary>Только для StatMod.</summary>
        public StatType statType;
        /// <summary>Additive bonus (StatMod) или duration (PassiveEffect). 0 = no additive.</summary>
        public float floatValue;
        /// <summary>Multiplicative bonus (StatMod), 0 = no multiplier. Range [0..5].</summary>
        public float multiplier;
        /// <summary>Ability id (AbilityUnlock) или passive id (PassiveEffect) или weaponClass/armorClass/techniqueId/recipeId. "" = none.</summary>
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

        // T-CB01: factory methods для новых 5 типов.
        public static SkillEffect WeaponProficiency(string weaponClass) => new SkillEffect
        {
            type = Type.WeaponProficiencyUnlock, statType = StatType.Strength,
            floatValue = 0f, multiplier = 0f, stringParam = weaponClass ?? string.Empty,
        };

        public static SkillEffect ArmorProficiency(string armorClass) => new SkillEffect
        {
            type = Type.ArmorProficiencyUnlock, statType = StatType.Strength,
            floatValue = 0f, multiplier = 0f, stringParam = armorClass ?? string.Empty,
        };

        public static SkillEffect WeaponTechnique(string techniqueId) => new SkillEffect
        {
            type = Type.WeaponTechniqueUnlock, statType = StatType.Strength,
            floatValue = 0f, multiplier = 0f, stringParam = techniqueId ?? string.Empty,
        };

        public static SkillEffect ExplosiveRecipe(string recipeId) => new SkillEffect
        {
            type = Type.ExplosiveRecipeUnlock, statType = StatType.Strength,
            floatValue = 0f, multiplier = 0f, stringParam = recipeId ?? string.Empty,
        };

        public static SkillEffect AntigravTechnique(string techniqueId) => new SkillEffect
        {
            type = Type.AntigravTechniqueUnlock, statType = StatType.Strength,
            floatValue = 0f, multiplier = 0f, stringParam = techniqueId ?? string.Empty,
        };
    }
}