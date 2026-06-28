// Project C: Real-Time Combat Engine — T-CB05
// WeaponTechniqueCatalog: SO-справочник для дизайнера: techniqueId → required proficiency skill.
// Примеры техник: "parry", "riposte", "feint", "double_strike", "power_attack".
// Phase 2: combat-движок читает список разблокированных техник игрока и применяет их.

using UnityEngine;
using ProjectC.Skills;

namespace ProjectC.Combat.Lookup
{
    /// <summary>
    /// T-CB05 stub: список боевых техник + proficiency gate.
    /// Combat-движок (T-RTC) прочитает PlayerData.unlockedTechniques и начнёт применять.
    /// </summary>
    [CreateAssetMenu(fileName = "WeaponTechniqueCatalog", menuName = "Project C/Combat/Weapon Technique Catalog", order = 18)]
    public class WeaponTechniqueCatalog : ScriptableObject
    {
        [System.Serializable]
        public struct Entry
        {
            [Tooltip("Уникальный ID техники (например: \"parry\", \"riposte\", \"double_strike\").")]
            public string techniqueId;

            [Tooltip("Display name для UI (\"Парирование\", \"Рипост\", ...).")]
            public string displayName;

            [Tooltip("SkillNodeConfig — proficiency gate (Phase 2: навык разблокирует технику).")]
            public SkillNodeConfig requiredProficiency;

            [Tooltip("Phase 2: логика техники (ScriptableObject ссылка или enum). Пока — флаг.")]
            public bool isPassive;
        }

        [Tooltip("Phase 2 stub: список техник. Наполнится когда дизайнер сделает tree техник.")]
        public Entry[] entries = System.Array.Empty<Entry>();

        public SkillNodeConfig GetRequiredProficiency(string techniqueId)
        {
            if (entries == null) return null;
            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i].techniqueId == techniqueId) return entries[i].requiredProficiency;
            }
            return null;
        }

        public bool IsUnlocked(string techniqueId, System.Collections.Generic.HashSet<string> learnedSkillIds)
        {
            var req = GetRequiredProficiency(techniqueId);
            if (req == null) return true;
            if (learnedSkillIds == null) return false;
            return learnedSkillIds.Contains(req.skillId);
        }
    }
}