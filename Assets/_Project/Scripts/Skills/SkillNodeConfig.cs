// Project C: Character Progression — T-P07 (stub-forward-declare) → полная версия T-P11
// SkillNodeConfig stub. Нужен в T-P07 потому что ClothingItemData.requiredSkills[]
// и ModuleItemData.requiredSkills[] ссылаются на SkillNodeConfig. Полная реализация (effects,
// prerequisites, cycle detection) придёт в T-P11.

using UnityEngine;

namespace ProjectC.Skills
{
    /// <summary>
    /// T-P07 STUB — полная версия в T-P11. Один SkillNodeConfig = один навык.
    /// </summary>
    /// <remarks>
    /// Stub содержит только те поля, которые ClothingItemData/ModuleItemData могут reference
    /// в requiredSkills[]: skillId (для comparison) + displayName (для Tooltip UI).
    /// T-P11 добавит: category, prerequisites[], effects[], LearnXpCost, RequiredIntelligenceTier,
    /// treeX/treeY, OnValidate cycle detection.
    /// </remarks>
    [CreateAssetMenu(fileName = "Skill_", menuName = "Project C/Skill Node", order = 13)]
    public class SkillNodeConfig : ScriptableObject
    {
        [Header("Identity")]
        public string skillId;          // "social_basic_talk" — stable key (persisted across sessions)
        public string displayName;      // "Базовый разговор"
        [TextArea(2, 4)] public string description;
        public Sprite icon;
    }
}
