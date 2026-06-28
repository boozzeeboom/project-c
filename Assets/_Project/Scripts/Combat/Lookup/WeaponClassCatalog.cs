// Project C: Real-Time Combat Engine — T-CB05
// WeaponClassCatalog: SO-справочник для дизайнера: WeaponClass → required proficiency skill.
// Дизайнер редактирует в инспекторе, без кода.
// Phase 2: используется ApplySkillEffects для разблокировки экипировки.

using UnityEngine;
using ProjectC.Equipment;
using ProjectC.Skills;

namespace ProjectC.Combat.Lookup
{
    /// <summary>
    /// T-CB05: справочник WeaponClass → SkillNodeConfig (proficiency gate).
    /// Phase 2: при learn навыка с WeaponProficiencyUnlock → unlock соответствующего WeaponClass.
    /// При forget навыка → lock + force-unequip.
    /// </summary>
    [CreateAssetMenu(fileName = "WeaponClassCatalog", menuName = "Project C/Combat/Weapon Class Catalog", order = 16)]
    public class WeaponClassCatalog : ScriptableObject
    {
        [System.Serializable]
        public struct Entry
        {
            [Tooltip("Класс оружия из WeaponItemData.weaponClass.")]
            public WeaponClass weaponClass;

            [Tooltip("SkillNodeConfig — proficiency gate. null = нет gate (любой игрок может использовать).")]
            public SkillNodeConfig requiredProficiency;
        }

        [Tooltip("Маппинг weaponClass → required proficiency skill. " +
                 "Дизайнер перетаскивает SkillNodeConfig в Inspector.")]
        public Entry[] entries = new Entry[]
        {
            new Entry { weaponClass = WeaponClass.Sword,          requiredProficiency = null },
            new Entry { weaponClass = WeaponClass.Dagger,         requiredProficiency = null },
            new Entry { weaponClass = WeaponClass.Spear,          requiredProficiency = null },
            new Entry { weaponClass = WeaponClass.Mace,           requiredProficiency = null },
            new Entry { weaponClass = WeaponClass.Crossbow,       requiredProficiency = null },
            new Entry { weaponClass = WeaponClass.Pneumatic,      requiredProficiency = null },
            new Entry { weaponClass = WeaponClass.AntigravBlade,  requiredProficiency = null },
            new Entry { weaponClass = WeaponClass.MesiumRifle,    requiredProficiency = null },
        };

        /// <summary>Получить required proficiency для данного WeaponClass. null = нет gate.</summary>
        public SkillNodeConfig GetRequiredProficiency(WeaponClass wc)
        {
            if (entries == null) return null;
            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i].weaponClass == wc) return entries[i].requiredProficiency;
            }
            return null;
        }

        /// <summary>Phase 2: список всех locked классов (для UI "заблокировано").</summary>
        public bool IsUnlocked(WeaponClass wc, System.Collections.Generic.HashSet<string> learnedSkillIds)
        {
            var req = GetRequiredProficiency(wc);
            if (req == null) return true;
            if (learnedSkillIds == null) return false;
            return learnedSkillIds.Contains(req.skillId);
        }
    }
}