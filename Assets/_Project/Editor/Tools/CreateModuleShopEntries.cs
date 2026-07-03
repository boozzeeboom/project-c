using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using ProjectC.Ship;

namespace ProjectC.Editor.Tools
{
    public static class CreateModuleShopEntries
    {
        [MenuItem("ProjectC/Tools/Create Module Shop Entries")]
        public static void Execute()
        {
            const string dir = "Assets/_Project/Data/Modules";

            var prices = new Dictionary<string, int>
            {
                {"MODULE_LIFT_ENH", 500}, {"MODULE_PITCH_ENH", 500},
                {"MODULE_YAW_ENH", 500}, {"MODULE_ROLL", 800},
                {"MODULE_MEZIY_PITCH", 1200}, {"MODULE_MEZIY_ROLL", 1200},
                {"MODULE_MEZIY_YAW", 1500}, {"MODULE_MEZIY_THRUST", 2000},
            };

            int created = 0;
            var allEntries = new List<ModuleShopEntry>();

            foreach (var kv in prices)
            {
                string id = kv.Key;
                var mod = AssetDatabase.LoadAssetAtPath<ShipModule>($"{dir}/{id}.asset");
                if (mod == null)
                {
                    Debug.LogWarning($"Module {id} not found");
                    continue;
                }

                string entryPath = $"{dir}/ShopEntry_{id}.asset";
                var entry = AssetDatabase.LoadAssetAtPath<ModuleShopEntry>(entryPath);
                if (entry == null)
                {
                    entry = ScriptableObject.CreateInstance<ModuleShopEntry>();
                    AssetDatabase.CreateAsset(entry, entryPath);
                }
                entry.module = mod;
                entry.costCredits = kv.Value;
                entry.requiredResources = new ResourceRequirement[0];
                EditorUtility.SetDirty(entry);
                allEntries.Add(entry);
                created++;
            }

            // Update database
            string dbPath = $"{dir}/ModuleShopDatabase.asset";
            var db = AssetDatabase.LoadAssetAtPath<ModuleShopDatabase>(dbPath);
            if (db != null)
            {
                db.entries = allEntries;
                EditorUtility.SetDirty(db);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[CreateModuleShopEntries] Created {created} entries");
        }
    }
}
