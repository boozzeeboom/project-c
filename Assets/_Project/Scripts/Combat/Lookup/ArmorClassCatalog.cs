// Project C: Real-Time Combat Engine — T-CB05
// ArmorClassCatalog: SO-справочник для дизайнера: ArmorClass → required proficiency skill.
// Phase 2 stub: ArmorClass enum ещё не введён (см. IMPLEMENTATION_PLAN_2026.md T-CB06 Phase 2).
// Пока — placeholder структура, чтобы дизайнер видел hook в Inspector.

using UnityEngine;
using ProjectC.Skills;

namespace ProjectC.Combat.Lookup
{
    /// <summary>
    /// T-CB05 stub: ArmorClass enum ещё не введён (Phase 2).
    /// Структура SO готова — дизайнер наполнит когда enum появится.
    /// </summary>
    [CreateAssetMenu(fileName = "ArmorClassCatalog", menuName = "Project C/Combat/Armor Class Catalog", order = 17)]
    public class ArmorClassCatalog : ScriptableObject
    {
        [System.Serializable]
        public struct Entry
        {
            [Tooltip("Stub: armor class пока как string (Light/Medium/Heavy/Antigrav). Phase 2 заменим на enum.")]
            public string armorClass;

            [Tooltip("SkillNodeConfig — proficiency gate. null = нет gate.")]
            public SkillNodeConfig requiredProficiency;
        }

        [Tooltip("Phase 2 stub: маппинг armorClass → required proficiency. " +
                 "Заполнится когда дизайнер введёт ArmorClass enum.")]
        public Entry[] entries = System.Array.Empty<Entry>();

        public SkillNodeConfig GetRequiredProficiency(string armorClass)
        {
            if (entries == null) return null;
            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i].armorClass == armorClass) return entries[i].requiredProficiency;
            }
            return null;
        }

        public bool IsUnlocked(string armorClass, System.Collections.Generic.HashSet<string> learnedSkillIds)
        {
            var req = GetRequiredProficiency(armorClass);
            if (req == null) return true;
            if (learnedSkillIds == null) return false;
            return learnedSkillIds.Contains(req.skillId);
        }
    }
}