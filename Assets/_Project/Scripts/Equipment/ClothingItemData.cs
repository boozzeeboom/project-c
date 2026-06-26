// Project C: Character Progression — T-P07
// ClothingItemData: одежда с характеристиками (STR/DEX/INT бонусы), слот, skill-requirements.
// Extends ItemData (ProjectC.Items). Design: docs/Character/03_DATA_MODEL.md §2, 05_CLOTHING_AND_MODULES.md §2.
//
// Стиль: НЕ использует [SerializeField] private (как базовый ItemData в этом проекте — поля public).
// Это контраст со StatsConfig, но соответствует существующему стилю Items subsystem.

using System;
using ProjectC.Equipment;
using ProjectC.Items;
using ProjectC.Skills;
using UnityEngine;

namespace ProjectC.Equipment
{
    [CreateAssetMenu(fileName = "Clothing_", menuName = "Project C/Equipment/Clothing", order = 11)]
    public class ClothingItemData : ItemData
    {
        [Header("Equip")]
        [Tooltip("Слот, в который надевается предмет (Head/Chest/.../Accessory2). Модули в Module1..3 " +
                 "(для одежды обычно Module1..3 = никогда, но Enum позволяет).")]
        public EquipSlot slot = EquipSlot.None;

        [Header("Tier (visual + scaling)")]
        [Tooltip("Tier предмета. Higher = лучше stats. Используется в T-P09 (TryEquip validation) " +
                 "и в UI (visual styling).")]
        [Range(1, 10)] public int tier = 1;

        [Header("Stat Bonuses (additive, base)")]
        [Tooltip("Flat bonus к Strength (additive to base stat).")]
        public float strengthBonus;

        [Tooltip("Flat bonus к Dexterity (additive to base stat).")]
        public float dexterityBonus;

        [Tooltip("Flat bonus к Intelligence (additive to base stat).")]
        public float intelligenceBonus;

        [Header("Stat Bonuses (multiplicative, applied after additive)")]
        [Tooltip("Множитель Strength (1.0 = +100% = ×2.0 итого). Применяется в RecomputeEffectiveStat (T-P09).")]
        [Range(0f, 5f)] public float strengthMultiplier;

        [Tooltip("Множитель Dexterity. [Range(0,5)] = ×1..×6 final value.")]
        [Range(0f, 5f)] public float dexterityMultiplier;

        [Tooltip("Множитель Intelligence. [Range(0,5)] = ×1..×6 final value.")]
        [Range(0f, 5f)] public float intelligenceMultiplier;

        [Header("Armor (T-CB06)")]
        [Tooltip("Физическая защита. Combat-движок: armor × typeMultiplier = effectiveDefense. " +
                 "Antigrav: ×0.5 (g-волна частично игнорирует). Mesium: ×0.0 (токсин не блокируется).")]
        [Range(0, 50)] public int armorDefense = 0;

        [Header("Skill Requirements")]
        [Tooltip("Все указанные навыки должны быть изучены для надевания. " +
                 "Q2.3: hard requirement — нельзя надеть без навыка. (T-P11 добавит RequirementType.RequirementType.Soft).")]
        public SkillNodeConfig[] requiredSkills = Array.Empty<SkillNodeConfig>();

        // Свой базовый RequirementType добавим в T-P11, когда SkillNodeConfig дозреет до полной версии.
        // В T-P07 всё hard (default), по roadmap Q2.3.

        /// <summary>
        /// Helper для UI/EquipmentServer: есть ли хоть один stat bonus (additive или multiplicative).
        /// </summary>
        public bool HasAnyStatBonus =>
            strengthBonus != 0f || dexterityBonus != 0f || intelligenceBonus != 0f
         || strengthMultiplier != 0f || dexterityMultiplier != 0f || intelligenceMultiplier != 0f;
    }
}
