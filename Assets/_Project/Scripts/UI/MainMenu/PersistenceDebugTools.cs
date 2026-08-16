using System;
using System.IO;
using UnityEngine;

namespace ProjectC.UI.MainMenu
{
    /// <summary>
    /// Debug-only persistence cleanup used by the MainMenu debug panel.
    /// It targets the save locations currently used by Project C and keeps
    /// graphics/gameplay settings and input bindings intact.
    /// </summary>
    public static class PersistenceDebugTools
    {
        private const string InputBindingsPrefsKey = "ProjectC.InputBindings.v1";

        private static string SaveRoot => Application.persistentDataPath;

        public static string DeleteAllSaves()
        {
            int deletedFiles = 0;
            deletedFiles += DeleteCharacterPositionSavesInternal();
            deletedFiles += DeleteCharacterInventoryInternal();
            deletedFiles += DeleteCharacterProgressionInternal();
            deletedFiles += DeleteCharacterCustomisationInternal();
            deletedFiles += DeleteQuestSavesInternal();
            deletedFiles += DeleteSkillBindingSavesInternal();
            deletedFiles += DeleteKeyInstanceSavesInternal();
            deletedFiles += DeleteWorldTimeSavesInternal();
            deletedFiles += DeleteTradeFileSavesInternal();
            ClearTradePlayerPrefs();

            return $"Delete All Saves: removed {deletedFiles} file(s). Trade PlayerPrefs cleared; settings and input bindings preserved.";
        }

        public static string DeleteCharacterPositionSaves()
        {
            int count = DeleteCharacterPositionSavesInternal();
            return $"Delete Character Position Saves: removed {count} file(s).";
        }

        public static string DeleteCharacterInventory()
        {
            int count = DeleteCharacterInventoryInternal();
            return $"Delete Character Inventory: removed {count} file(s).";
        }

        public static string DeleteCharacterProgression()
        {
            int count = DeleteCharacterProgressionInternal();
            return $"Delete Character Progression: removed {count} file(s).";
        }

        public static string DeleteCharacterCustomisation()
        {
            int count = DeleteCharacterCustomisationInternal();
            return $"Delete Character Customisation: removed {count} file(s).";
        }

        public static string DeleteQuestSaves()
        {
            int count = DeleteQuestSavesInternal();
            return $"Delete Quests: removed {count} file(s).";
        }

        public static string DeleteSkillBindingSaves()
        {
            int count = DeleteSkillBindingSavesInternal();
            return $"Delete Skill Slot Bindings: removed {count} file(s).";
        }

        public static string DeleteKeyInstanceSaves()
        {
            int count = DeleteKeyInstanceSavesInternal();
            return $"Delete Key Instance Saves: removed {count} file(s).";
        }

        public static string DeleteWorldTimeSaves()
        {
            int count = DeleteWorldTimeSavesInternal();
            return $"Delete World Time: removed {count} file(s).";
        }

        public static string DeleteTradeSaves()
        {
            int count = DeleteTradeFileSavesInternal();
            ClearTradePlayerPrefs();
            return $"Delete Trade / Cargo Saves: removed {count} file(s) and cleared trade PlayerPrefs.";
        }

        private static int DeleteCharacterPositionSavesInternal()
        {
            return DeleteFiles(SaveRoot, "ShipPositions.json", "ShipPositions.json.tmp");
        }

        private static int DeleteCharacterInventoryInternal()
        {
            return DeleteFiles(SaveRoot, "inventory_*.json", "inventory_*.json.tmp");
        }

        private static int DeleteCharacterProgressionInternal()
        {
            string directory = Path.Combine(SaveRoot, "Character");
            return DeleteFiles(directory, "character_*.json", "character_*.json.tmp");
        }

        private static int DeleteCharacterCustomisationInternal()
        {
            string directory = Path.Combine(SaveRoot, "Customisation");
            return DeleteFiles(directory, "customisation_*.json", "customisation_*.json.tmp");
        }

        private static int DeleteQuestSavesInternal()
        {
            return DeleteFiles(SaveRoot, "quest_state_*.json", "quest_state_*.json.tmp");
        }

        private static int DeleteSkillBindingSavesInternal()
        {
            string directory = Path.Combine(SaveRoot, "Skills");
            return DeleteFiles(directory, "slot_bindings_*.json", "slot_bindings_*.json.tmp");
        }

        private static int DeleteKeyInstanceSavesInternal()
        {
            return DeleteFiles(SaveRoot, "KeyRodInstances.json", "KeyRodInstances.json.tmp");
        }

        private static int DeleteWorldTimeSavesInternal()
        {
            return DeleteFiles(SaveRoot, "time_state.json", "time_state.json.tmp");
        }

        private static int DeleteTradeFileSavesInternal()
        {
            string directory = Path.Combine(SaveRoot, "ServerData");
            return DeleteFiles(directory, "*");
        }

        private static int DeleteFiles(string directory, params string[] searchPatterns)
        {
            if (string.IsNullOrEmpty(directory) || searchPatterns == null || !Directory.Exists(directory))
                return 0;

            int deleted = 0;
            foreach (string pattern in searchPatterns)
            {
                string[] paths;
                try
                {
                    paths = Directory.GetFiles(directory, pattern, SearchOption.TopDirectoryOnly);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[PersistenceDebugTools] Failed to enumerate '{directory}' with '{pattern}': {ex.Message}");
                    continue;
                }

                foreach (string path in paths)
                {
                    try
                    {
                        File.Delete(path);
                        deleted++;
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[PersistenceDebugTools] Failed to delete '{path}': {ex.Message}");
                    }
                }
            }

            return deleted;
        }

        private static void ClearTradePlayerPrefs()
        {
            string inputBindingsJson = null;
            bool hadInputBindings = PlayerPrefs.HasKey(InputBindingsPrefsKey);
            if (hadInputBindings)
                inputBindingsJson = PlayerPrefs.GetString(InputBindingsPrefsKey, string.Empty);

            // The current project stores trade persistence in PlayerPrefs, while
            // SettingsManager and InputBindingsRuntime use the same storage for
            // non-save preferences. Clear everything, then restore those two categories.
            PlayerPrefs.DeleteAll();
            ProjectC.Core.SettingsManager.Save();

            if (hadInputBindings)
                PlayerPrefs.SetString(InputBindingsPrefsKey, inputBindingsJson ?? string.Empty);

            PlayerPrefs.Save();
        }
    }
}
