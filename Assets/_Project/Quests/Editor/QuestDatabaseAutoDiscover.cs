// T-Q09: QuestDatabaseAutoDiscover — auto-populate QuestDatabase via AssetDatabase scan.
// Runs на editor load, save, и manual menu trigger.
// См. docs/NPC_quests/02_V2_ARCHITECTURE.md §2.3.

#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using ProjectC.Quests;
using ProjectC.Factions;
using ProjectC.Dialogue;

namespace ProjectC.Quests.Editor
{
    /// <summary>
    /// Auto-populates QuestDatabase asset со всеми FactionDefinition/NpcDefinition/DialogTree/QuestDefinition
    /// в проекте. Runs:
    ///   • [InitializeOnLoad] на editor startup.
    ///   • AssetPostprocessor на save (post-process all .asset files).
    ///   • [MenuItem("Tools/ProjectC/Re-scan Quest Database")] вручную.
    /// </summary>
    [InitializeOnLoad]
    public static class QuestDatabaseAutoDiscover
    {
        private const string DATABASE_PATH = "Assets/_Project/Quests/Data/QuestDatabase.asset";
        private const string FACTION_GUID = "Assets/_Project/Quests/Data/Factions";
        private const string NPC_GUID = "Assets/_Project/Quests/Data/Npcs";
        private const string DIALOG_GUID = "Assets/_Project/Quests/Data/Dialogs";
        private const string QUEST_GUID = "Assets/_Project/Quests/Data/Quests";

        static QuestDatabaseAutoDiscover()
        {
            // Defer to next editor frame to ensure AssetDatabase is ready.
            EditorApplication.delayCall += () =>
            {
                if (GetOrCreateDatabase() != null)
                {
                    // Silent auto-discovery on startup
                    RescanInternal(verbose: false);
                }
            };
        }

        [MenuItem("Tools/ProjectC/Quests/Re-scan Quest Database", priority = 110)]
        public static void RescanMenu()
        {
            var db = GetOrCreateDatabase();
            if (db == null) return;
            RescanInternal(verbose: true);
        }

        public static void Rescan()
        {
            RescanInternal(verbose: false);
        }

        private static QuestDatabase GetOrCreateDatabase()
        {
            var db = AssetDatabase.LoadAssetAtPath<QuestDatabase>(DATABASE_PATH);
            if (db == null)
            {
                // Create directory if needed
                if (!AssetDatabase.IsValidFolder("Assets/_Project/Quests/Data"))
                {
                    // Should exist (T-Q01 created it). If not — fail silent.
                    return null;
                }
                db = ScriptableObject.CreateInstance<QuestDatabase>();
                AssetDatabase.CreateAsset(db, DATABASE_PATH);
                AssetDatabase.SaveAssets();
                Debug.Log($"[QuestDatabaseAutoDiscover] Created {DATABASE_PATH}");
            }
            return db;
        }

        private static void RescanInternal(bool verbose)
        {
            var db = GetOrCreateDatabase();
            if (db == null) return;

            int factionsCount = ScanFolder<FactionDefinition>(FACTION_GUID, list =>
            {
                System.Array.Resize(ref db.factions, list.Count);
                for (int i = 0; i < list.Count; i++) db.factions[i] = list[i];
            });
            int npcsCount = ScanFolder<NpcDefinition>(NPC_GUID, list =>
            {
                System.Array.Resize(ref db.npcs, list.Count);
                for (int i = 0; i < list.Count; i++) db.npcs[i] = list[i];
            });
            int dialogsCount = ScanFolder<DialogTree>(DIALOG_GUID, list =>
            {
                System.Array.Resize(ref db.dialogTrees, list.Count);
                for (int i = 0; i < list.Count; i++) db.dialogTrees[i] = list[i];
            });
            int questsCount = ScanFolder<QuestDefinition>(QUEST_GUID, list =>
            {
                System.Array.Resize(ref db.quests, list.Count);
                for (int i = 0; i < list.Count; i++) db.quests[i] = list[i];
            });

            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();

            if (verbose)
            {
                Debug.Log($"[QuestDatabaseAutoDiscover] Rescan complete: factions={factionsCount}, npcs={npcsCount}, dialogs={dialogsCount}, quests={questsCount}");
            }
        }

        private static int ScanFolder<T>(string folder, System.Action<List<T>> onLoaded) where T : Object
        {
            if (!AssetDatabase.IsValidFolder(folder)) return 0;
            var guids = AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { folder });
            var list = new List<T>(guids.Length);
            for (int i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var asset = AssetDatabase.LoadAssetAtPath<T>(path);
                if (asset != null) list.Add(asset);
            }
            onLoaded(list);
            return list.Count;
        }
    }
}
#endif
