#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using ProjectC.Ship;

namespace ProjectC.Ship.Editor
{
    /// <summary>
    /// Миграция T-MOD03: переносит costCredits и requiredResources из
    /// старых ModuleShopEntry.asset в ShipModule, обновляет ModuleShopDatabase.
    /// После миграции ShopEntry_*.asset можно удалить.
    /// </summary>
    public static class ModuleShopMigration
    {
        [MenuItem("Tools/ProjectC/Ship/Migrate ShopEntry → ShipModule")]
        public static void Migrate()
        {
            const string dir = "Assets/_Project/Data/Modules";

            // 1. Найти все ShopEntry
            var entryGuids = AssetDatabase.FindAssets("t:ModuleShopEntry", new[] { dir });
            int migrated = 0;

            foreach (var guid in entryGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var entry = AssetDatabase.LoadAssetAtPath<ModuleShopEntry>(path);
                if (entry == null || entry.module == null) continue;

                var mod = entry.module;
                Undo.RecordObject(mod, "Migrate ShopEntry");
                mod.costCredits = entry.costCredits;
                if (entry.requiredResources != null && entry.requiredResources.Length > 0)
                    mod.requiredResources = entry.requiredResources;
                else
                    mod.requiredResources = new ResourceRequirement[0];
                EditorUtility.SetDirty(mod);
                migrated++;
            }

            // 2. Обновить ModuleShopDatabase
            string dbPath = $"{dir}/ModuleShopDatabase.asset";
            var db = AssetDatabase.LoadAssetAtPath<ModuleShopDatabase>(dbPath);
            if (db != null)
            {
                Undo.RecordObject(db, "Migrate Database");
                db.entries.Clear();
                foreach (var guid in AssetDatabase.FindAssets("t:ShipModule", new[] { dir }))
                {
                    var mod = AssetDatabase.LoadAssetAtPath<ShipModule>(AssetDatabase.GUIDToAssetPath(guid));
                    if (mod != null && !string.IsNullOrEmpty(mod.moduleId))
                        db.entries.Add(mod);
                }
                EditorUtility.SetDirty(db);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[ModuleShopMigration] Migrated {migrated} ShopEntry → ShipModule. " +
                      $"Database updated ({db?.entries.Count ?? 0} modules). " +
                      $"Old ShopEntry_*.asset files can now be deleted manually.");
        }
    }
}
#endif
