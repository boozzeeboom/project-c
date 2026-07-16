#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ProjectC.Ship.Editor
{
    /// <summary>
    /// Кастомный редактор для ModuleShopDatabase.
    /// Возможности:
    ///   - Поиск по moduleId / displayName
    ///   - Скан папки Modules — авто-добавление ShipModule + ShopEntry
    ///   - Очистить все / Валидация / Сортировка
    ///   - Массовая установка costCredits
    ///   - Статистика: по типам модулей, общая стоимость
    /// </summary>
    [CustomEditor(typeof(ModuleShopDatabase))]
    public class ModuleShopDatabaseEditor : UnityEditor.Editor
    {
        private string _searchFilter = "";
        private Vector2 _scrollPos;
        private int _bulkCost = 500;

        public override void OnInspectorGUI()
        {
            var db = (ModuleShopDatabase)target;
            serializedObject.Update();

            EditorGUILayout.LabelField($"Module Shop Database ({db.entries?.Count ?? 0} entries)", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            // === STATS ===
            if (db.entries != null && db.entries.Count > 0)
            {
                int valid = db.entries.Count(x => x != null && x.module != null);
                int broken = db.entries.Count(x => x == null || x.module == null);
                int totalCost = db.entries.Where(x => x != null).Sum(x => x.costCredits);

                var byType = db.entries
                    .Where(x => x != null && x.module != null)
                    .GroupBy(x => x.module.type)
                    .OrderByDescending(g => g.Count());

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"Valid: {valid}", EditorStyles.miniLabel);
                if (broken > 0)
                    EditorGUILayout.LabelField($"Broken: {broken}", new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = Color.red } });
                EditorGUILayout.LabelField($"Total cost: {totalCost} CR", EditorStyles.miniLabel);
                EditorGUILayout.EndHorizontal();

                string typeStr = string.Join(" | ", byType.Select(g => $"{g.Key}:{g.Count()}"));
                EditorGUILayout.LabelField($"Types: {typeStr}", EditorStyles.miniLabel);
            }
            EditorGUILayout.Space(4);

            // === BULK ACTIONS ===
            EditorGUILayout.LabelField("Bulk Actions", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Scan Modules Folder", GUILayout.Height(24)))
            {
                ScanModulesFolder(db);
            }
            if (GUILayout.Button("Validate", GUILayout.Height(24)))
            {
                ValidateDatabase(db);
            }
            if (GUILayout.Button("Clear All", GUILayout.Height(24)))
            {
                Undo.RecordObject(db, "Clear Database");
                db.entries.Clear();
                EditorUtility.SetDirty(db);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Set Cost to All:", GUILayout.Width(100));
            _bulkCost = EditorGUILayout.IntField(_bulkCost, GUILayout.Width(70));
            if (GUILayout.Button("Apply", GUILayout.Width(60)))
            {
                Undo.RecordObject(db, "Set All Costs");
                foreach (var e in db.entries)
                {
                    if (e != null) e.costCredits = _bulkCost;
                }
                EditorUtility.SetDirty(db);
            }
            if (GUILayout.Button("Sort by Cost", GUILayout.Height(22)))
            {
                Undo.RecordObject(db, "Sort by Cost");
                db.entries = db.entries
                    .Where(x => x != null)
                    .OrderBy(x => x.costCredits)
                    .ThenBy(x => x.module != null ? x.module.moduleId : "")
                    .ToList();
                EditorUtility.SetDirty(db);
            }
            if (GUILayout.Button("Sort by Name", GUILayout.Height(22)))
            {
                Undo.RecordObject(db, "Sort by Name");
                db.entries = db.entries
                    .Where(x => x != null)
                    .OrderBy(x => x.module != null ? x.module.moduleId : "zzz")
                    .ToList();
                EditorUtility.SetDirty(db);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Remove Nulls/Broken", GUILayout.Height(22)))
            {
                Undo.RecordObject(db, "Remove Broken");
                int before = db.entries.Count;
                db.entries.RemoveAll(x => x == null || x.module == null);
                EditorUtility.SetDirty(db);
                Debug.Log($"[ModuleShopDB] Removed {before - db.entries.Count} broken entries");
            }
            if (GUILayout.Button("Select All Modules", GUILayout.Height(22)))
            {
                var paths = db.entries
                    .Where(x => x != null && x.module != null)
                    .Select(x => AssetDatabase.GetAssetPath(x.module))
                    .Where(p => !string.IsNullOrEmpty(p))
                    .ToArray();
                Selection.objects = paths.Select(p => AssetDatabase.LoadAssetAtPath<Object>(p)).Where(o => o != null).ToArray();
                Debug.Log($"[ModuleShopDB] Selected {Selection.objects.Length} modules in Project View");
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(8);

            // === SEARCH ===
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Search:", GUILayout.Width(50));
            _searchFilter = EditorGUILayout.TextField(_searchFilter, GUILayout.Height(18));
            if (GUILayout.Button("✕", GUILayout.Width(24), GUILayout.Height(18)))
                _searchFilter = "";
            EditorGUILayout.EndHorizontal();

            // Filter chips
            if (string.IsNullOrEmpty(_searchFilter))
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Propulsion", EditorStyles.miniButton, GUILayout.Width(80)))
                    _searchFilter = "Propulsion";
                if (GUILayout.Button("Utility", EditorStyles.miniButton, GUILayout.Width(60)))
                    _searchFilter = "Utility";
                if (GUILayout.Button("Special", EditorStyles.miniButton, GUILayout.Width(60)))
                    _searchFilter = "Special";
                if (GUILayout.Button("Engine", EditorStyles.miniButton, GUILayout.Width(60)))
                    _searchFilter = "Engine";
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.Space(4);

            // === ENTRIES LIST ===
            var entriesProp = serializedObject.FindProperty("entries");
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos, GUILayout.MinHeight(150), GUILayout.MaxHeight(500));

            if (entriesProp != null)
            {
                for (int i = 0; i < entriesProp.arraySize; i++)
                {
                    var elem = entriesProp.GetArrayElementAtIndex(i);
                    if (elem.objectReferenceValue == null) continue;

                    var entry = (ModuleShopEntry)elem.objectReferenceValue;
                    var mod = entry.module;

                    // Filter
                    if (!string.IsNullOrEmpty(_searchFilter))
                    {
                        bool match = false;
                        if (mod != null)
                        {
                            match = mod.moduleId.ToLowerInvariant().Contains(_searchFilter.ToLowerInvariant()) ||
                                    mod.type.ToString().ToLowerInvariant().Contains(_searchFilter.ToLowerInvariant());
                        }
                        if (!match) continue;
                    }

                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    EditorGUILayout.BeginHorizontal();

                    // Module type icon
                    if (mod != null)
                    {
                        string icon = mod.type switch
                        {
                            ModuleType.Propulsion => "🚀",
                            ModuleType.Utility => "⚙",
                            ModuleType.Special => "✨",
                            ModuleType.Engine => "🔥",
                            _ => "📦"
                        };
                        GUI.color = Color.white;
                        EditorGUILayout.LabelField(icon, GUILayout.Width(20));
                    }

                    // moduleId
                    string id = mod != null ? mod.moduleId : "(missing)";
                    if (id.Length > 30) id = id.Substring(0, 30) + "...";
                    EditorGUILayout.LabelField(id, mod == null ? new GUIStyle(EditorStyles.label) { normal = { textColor = Color.red } } : EditorStyles.label, GUILayout.MinWidth(100));

                    // Cost
                    EditorGUI.BeginChangeCheck();
                    int newCost = EditorGUILayout.IntField(entry.costCredits, GUILayout.Width(60));
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(entry, "Change Cost");
                        entry.costCredits = newCost;
                        EditorUtility.SetDirty(entry);
                    }
                    EditorGUILayout.LabelField("CR", GUILayout.Width(25));

                    // Resources count
                    int resCount = entry.requiredResources?.Length ?? 0;
                    if (resCount > 0)
                    {
                        GUI.color = Color.cyan;
                        EditorGUILayout.LabelField($"📦×{resCount}", GUILayout.Width(50));
                        GUI.color = Color.white;
                    }

                    // Ping module
                    if (GUILayout.Button("📍", GUILayout.Width(24)))
                    {
                        if (mod != null) EditorGUIUtility.PingObject(mod);
                        else EditorGUIUtility.PingObject(entry);
                    }

                    // Remove
                    if (GUILayout.Button("✕", GUILayout.Width(24)))
                    {
                        Undo.RecordObject(db, "Remove Entry");
                        db.entries.RemoveAt(i);
                        EditorUtility.SetDirty(db);
                        break;
                    }

                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                    EditorGUILayout.Space(1);
                }
            }

            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(8);
            serializedObject.ApplyModifiedProperties();
        }

        private void ScanModulesFolder(ModuleShopDatabase db)
        {
            string modulesPath = "Assets/_Project/Data/Modules";
            var moduleGuids = AssetDatabase.FindAssets("t:ShipModule", new[] { modulesPath });
            var entryGuids = AssetDatabase.FindAssets("t:ModuleShopEntry", new[] { modulesPath });

            Undo.RecordObject(db, "Scan Modules Folder");
            var existingModuleIds = new HashSet<string>(
                db.entries.Where(x => x != null && x.module != null).Select(x => x.module.moduleId));
            int added = 0;

            foreach (var guid in moduleGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var mod = AssetDatabase.LoadAssetAtPath<ShipModule>(path);
                if (mod == null || string.IsNullOrEmpty(mod.moduleId)) continue;
                if (existingModuleIds.Contains(mod.moduleId)) continue;

                // Look for existing ShopEntry for this module
                ModuleShopEntry existingEntry = null;
                foreach (var eg in entryGuids)
                {
                    var ep = AssetDatabase.GUIDToAssetPath(eg);
                    var e = AssetDatabase.LoadAssetAtPath<ModuleShopEntry>(ep);
                    if (e != null && e.module == mod) { existingEntry = e; break; }
                }

                if (existingEntry == null)
                {
                    // Create new ShopEntry
                    string entryPath = Path.Combine(modulesPath, $"ShopEntry_{mod.moduleId}.asset");
                    entryPath = AssetDatabase.GenerateUniqueAssetPath(entryPath);
                    existingEntry = ScriptableObject.CreateInstance<ModuleShopEntry>();
                    existingEntry.module = mod;
                    existingEntry.costCredits = _bulkCost;
                    existingEntry.requiredResources = new ResourceRequirement[0];
                    AssetDatabase.CreateAsset(existingEntry, entryPath);
                    Debug.Log($"[ModuleShopDB] Created ShopEntry: {entryPath}");
                }

                db.entries.Add(existingEntry);
                existingModuleIds.Add(mod.moduleId);
                added++;
            }

            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[ModuleShopDB] Scan complete: {moduleGuids.Length} modules found, added {added} new (total: {db.entries.Count})");
        }

        private void ValidateDatabase(ModuleShopDatabase db)
        {
            var errors = new List<string>();
            var seenModules = new HashSet<ShipModule>();

            for (int i = 0; i < db.entries.Count; i++)
            {
                var e = db.entries[i];
                if (e == null) { errors.Add($"[{i}]: null entry"); continue; }
                if (e.module == null) { errors.Add($"[{i}]: module is null"); continue; }
                if (string.IsNullOrEmpty(e.module.moduleId)) errors.Add($"[{i}]: module has empty moduleId");
                if (!seenModules.Add(e.module)) errors.Add($"[{i}]: duplicate module '{e.module.moduleId}'");
            }

            if (errors.Count == 0)
                Debug.Log($"[ModuleShopDB] ✅ Validation passed: {db.entries.Count(x => x != null)} valid entries");
            else
            {
                Debug.LogWarning($"[ModuleShopDB] ⚠️ {errors.Count} issues:");
                foreach (var err in errors) Debug.LogWarning($"  - {err}");
            }
        }
    }
}
#endif
