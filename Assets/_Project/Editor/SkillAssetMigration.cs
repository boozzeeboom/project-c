// Project C: Skill System Refactor — Phase B
// SkillAssetMigration: Editor-скрипт для миграции всех SkillNodeConfig .asset
// со старых дисциплин (Explosives=4, Antigrav=5) на новые (Melee/Ranged/Defense/Placed).
//
// Usage: меню Project C > Skills > Migrate All Skill Assets to New Disciplines
// Перед запуском убедиться что Phase A (enum refactor) уже скомпилирован.

#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using ProjectC.Skills;

namespace ProjectC.Editor.Skills
{
    public static class SkillAssetMigration
    {
        private struct MigrationEntry
        {
            public string skillIdPrefix;
            public CombatDiscipline targetDiscipline;
            public CombatSubtype targetSubtype;
        }

        private static readonly MigrationEntry[] MigrationMap = new MigrationEntry[]
        {
            // Combat (generic roots) — без изменений
            new() { skillIdPrefix = "combat_", targetDiscipline = CombatDiscipline.Combat, targetSubtype = CombatSubtype.None },

            // Melee — без изменений
            new() { skillIdPrefix = "melee_", targetDiscipline = CombatDiscipline.Melee, targetSubtype = CombatSubtype.None },

            // Ranged — без изменений (кроме throwables — они в отдельном правиле ниже)
            new() { skillIdPrefix = "ranged_", targetDiscipline = CombatDiscipline.Ranged, targetSubtype = CombatSubtype.None },

            // Defense — без изменений
            new() { skillIdPrefix = "defense_", targetDiscipline = CombatDiscipline.Defense, targetSubtype = CombatSubtype.None },

            // Social — без изменений
            new() { skillIdPrefix = "social_", targetDiscipline = CombatDiscipline.None, targetSubtype = CombatSubtype.None },

            // === Бывшие Explosives → новые дисциплины ===

            // Гранаты → Ranged + Throwables
            new() { skillIdPrefix = "expl_grenade", targetDiscipline = CombatDiscipline.Ranged, targetSubtype = CombatSubtype.Throwables },

            // Бомбы → Ranged + Throwables
            new() { skillIdPrefix = "expl_basic_bomb", targetDiscipline = CombatDiscipline.Ranged, targetSubtype = CombatSubtype.Throwables },

            // Мины → Placed + Traps
            new() { skillIdPrefix = "expl_mine", targetDiscipline = CombatDiscipline.Placed, targetSubtype = CombatSubtype.Traps },

            // === Бывшие Antigrav → новые дисциплины ===

            // Аура → Defense
            new() { skillIdPrefix = "antigrav_aura", targetDiscipline = CombatDiscipline.Defense, targetSubtype = CombatSubtype.None },

            // Щит → Defense
            new() { skillIdPrefix = "antigrav_shield", targetDiscipline = CombatDiscipline.Defense, targetSubtype = CombatSubtype.None },

            // Пульс → Defense (по решению пользователя). skillId = "antigrav_basic" (без _pulse)
            new() { skillIdPrefix = "antigrav_basic", targetDiscipline = CombatDiscipline.Defense, targetSubtype = CombatSubtype.None },
        };

        [MenuItem("Project C/Skills/Migrate All Skill Assets to New Disciplines")]
        public static void MigrateAllSkills()
        {
            var allSkills = Resources.LoadAll<SkillNodeConfig>("Skills");
            if (allSkills == null || allSkills.Length == 0)
            {
                Debug.LogWarning("[SkillAssetMigration] No SkillNodeConfig assets found in Resources/Skills/");
                return;
            }

            int migrated = 0;
            int skipped = 0;
            var logLines = new List<string>();

            foreach (var skill in allSkills)
            {
                if (skill == null || string.IsNullOrEmpty(skill.skillId)) continue;

                bool matched = false;
                foreach (var entry in MigrationMap)
                {
                    if (skill.skillId.StartsWith(entry.skillIdPrefix))
                    {
                        var oldDiscipline = skill.discipline;
                        var oldSubtype = skill.subtype;

                        // Сериализуем через SerializedObject (чтобы Undo работал и asset пометился dirty)
                        var so = new SerializedObject(skill);
                        var disciplineProp = so.FindProperty("discipline");
                        var subtypeProp = so.FindProperty("subtype");

                        if (disciplineProp != null)
                            disciplineProp.enumValueIndex = (int)entry.targetDiscipline;
                        if (subtypeProp != null)
                            subtypeProp.enumValueIndex = (int)entry.targetSubtype;

                        so.ApplyModifiedProperties();
                        EditorUtility.SetDirty(skill);

                        logLines.Add($"[Migrated] {skill.skillId}: discipline {oldDiscipline}→{entry.targetDiscipline}, subtype {oldSubtype}→{entry.targetSubtype}");
                        migrated++;
                        matched = true;
                        break;
                    }
                }

                if (!matched)
                {
                    logLines.Add($"[Skipped] {skill.skillId}: no migration rule for prefix (discipline={skill.discipline})");
                    skipped++;
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[SkillAssetMigration] Done. Migrated={migrated}, Skipped={skipped}, Total={allSkills.Length}\n" +
                      string.Join("\n", logLines));
            EditorUtility.DisplayDialog("Skill Asset Migration",
                $"Migrated: {migrated}\nSkipped: {skipped}\n\nDetails in Console.",
                "OK");
        }
    }
}
#endif
