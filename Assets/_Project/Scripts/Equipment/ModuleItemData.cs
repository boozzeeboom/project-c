// Project C: Character Progression — T-P07
// ModuleItemData: персонажные импланты (Q2.5) с per-type эффектами (sensor/processor/weapon/utility).
// Extends ItemData. Design: docs/Character/03_DATA_MODEL.md §3, 05_CLOTHING_AND_MODULES.md §3.
//
// Q2.5: модули = персонажные импланты (как Cyberpunk), НЕ модификации корабля.
// Корабельные модули уже есть через ShipController.modules[] (отдельная подсистема).

using System;
using ProjectC.Equipment;
using ProjectC.Items;
using ProjectC.Skills;
using UnityEngine;

namespace ProjectC.Equipment
{
    [CreateAssetMenu(fileName = "Module_", menuName = "Project C/Equipment/Module", order = 12)]
    public class ModuleItemData : ItemData
    {
        public enum ModuleType : byte
        {
            Sensor    = 0,
            Processor = 1,
            Weapon    = 2,
            Utility   = 3,
        }

        [Header("Equip")]
        [Tooltip("Слот модуля (Module1/Module2/Module3).")]
        public EquipSlot slot = EquipSlot.Module1;

        [Header("Module Type")]
        [Tooltip("Тип модуля. Определяет какие per-type effects активны (sensorRange / craftSpeed / damage).")]
        public ModuleType moduleType;

        [Header("Tier")]
        [Range(1, 10)] public int tier = 1;

        [Header("Effects (per-type)")]
        [Header("Sensor")]
        [Tooltip("Бонус к sensor range (meters). Активен если moduleType=Sensor. " +
                 "Использование в T-P09: пока нет runtime-sensor системы, поле готово для будущего.")]
        public float sensorRangeBonus;

        [Header("Processor")]
        [Tooltip("Множитель crafting speed для Processor модулей. Активен если moduleType=Processor. " +
                 "Использование в T-P09: пока не подключено (нет CraftingStation multiplier API).")]
        [Range(0f, 5f)] public float craftingSpeedMultiplier;

        [Header("Weapon")]
        [Tooltip("Бонус к damage для Weapon модулей. Активен если moduleType=Weapon. " +
                 "Future use — нет weapon system пока.")]
        public float weaponDamageBonus;

        [Header("Utility (общие stat bonuses)")]
        [Tooltip("Flat bonus к Strength (для Utility модулей).")]
        public float strengthBonus;

        [Tooltip("Flat bonus к Dexterity (для Utility модулей).")]
        public float dexterityBonus;

        [Tooltip("Flat bonus к Intelligence (для Utility модулей).")]
        public float intelligenceBonus;

        [Header("Skill Requirements")]
        [Tooltip("Все указанные навыки должны быть изучены для имплантации. " +
                 "Q2.3: hard requirement в MVP (T-P11 добавит soft option).")]
        public SkillNodeConfig[] requiredSkills = Array.Empty<SkillNodeConfig>();

        [Header("Power Consumption (future)")]
        [Tooltip("Watts. Для будущей ship power system. Сейчас поле готово, но не используется.")]
        public float powerConsumption;
    }
}
