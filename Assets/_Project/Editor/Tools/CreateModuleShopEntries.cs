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

            var allModules = new List<ShipModule>();

            foreach (var kv in prices)
            {
                var mod = AssetDatabase.LoadAssetAtPath<ShipModule>($"{dir}/{kv.Key}.asset");
                if (mod == null)
                {
                    Debug.LogWarning($"Module {kv.Key} not found");
                    continue;
                }
                mod.costCredits = kv.Value;
                if (mod.requiredResources == null || mod.requiredResources.Length == 0)
                    mod.requiredResources = new ResourceRequirement[0];
                EditorUtility.SetDirty(mod);
                allModules.Add(mod);
            }

            string dbPath = $"{dir}/ModuleShopDatabase.asset";
            var db = AssetDatabase.LoadAssetAtPath<ModuleShopDatabase>(dbPath);
            if (db != null)
            {
                db.entries = allModules;
                EditorUtility.SetDirty(db);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[CreateModuleShopEntries] Updated {allModules.Count} modules in database");
        }
    }
}
