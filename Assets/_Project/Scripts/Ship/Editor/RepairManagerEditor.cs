#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ProjectC.Ship.Editor
{
    /// <summary>
    /// Кастомный редактор RepairManager — редактирование ModuleShopDatabase INLINE,
    /// без перехода к .asset в Project View.
    /// </summary>
    [CustomEditor(typeof(RepairManager))]
    public class RepairManagerEditor : UnityEditor.Editor
    {
        private string _searchFilter = "";
        private Vector2 _scrollPos;
        private int _bulkCost = 500;

        private static bool _dbFoldout = true;

        public override void OnInspectorGUI()
        {
            var rm = (RepairManager)target;

            // === RepairManager own fields ===
            var dbProp = serializedObject.FindProperty("_shopDatabase");
            EditorGUILayout.PropertyField(dbProp, new GUIContent("Module Shop Database"));

            var db = dbProp.objectReferenceValue as ModuleShopDatabase;
            if (db == null)
            {
                serializedObject.ApplyModifiedProperties();
                EditorGUILayout.HelpBox("Assign a ModuleShopDatabase to edit its entries inline.", MessageType.Info);
                DrawRepairManagerFields();
                return;
            }

            // === Inline Database editor ===
            EditorGUILayout.Space(4);
            _dbFoldout = EditorGUILayout.Foldout(_dbFoldout,
                $"🛠 Module Shop ({db.entries?.Count ?? 0} entries)", true, EditorStyles.foldoutHeader);

            if (_dbFoldout)
            {
                EditorGUI.indentLevel++;

                // --- Stats ---
                if (db.entries != null && db.entries.Count > 0)
                {
                    int valid = db.entries.Count(x => x != null && x.module != null);
                    int broken = db.entries.Count(x => x == null || x.module == null);
                    int totalCost = db.entries.Where(x => x != null).Sum(x => x.costCredits);

                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField($"Valid: {valid} | Total: {totalCost} CR", EditorStyles.miniLabel);
                    if (broken > 0)
                        EditorGUILayout.LabelField($"Broken: {broken}", new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = Color.red } });
                    EditorGUILayout.EndHorizontal();
                }

                // --- Bulk Actions ---
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Scan Modules", GUILayout.Height(22)))
                {
                    ScanModules(db);
                }
                if (GUILayout.Button("Validate", GUILayout.Height(22)))
                {
                    Validate(db);
                }
                if (GUILayout.Button("Clear All", GUILayout.Height(22)))
                {
                    Undo.RecordObject(db, "Clear");
                    db.entries.Clear();
                    EditorUtility.SetDirty(db);
                }
                if (GUILayout.Button("📍 Ping Asset", GUILayout.Height(22)))
                {
                    EditorGUIUtility.PingObject(db);
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Cost:", GUILayout.Width(35));
                _bulkCost = EditorGUILayout.IntField(_bulkCost, GUILayout.Width(60));
                if (GUILayout.Button("Apply to All", GUILayout.Width(80)))
                {
                    Undo.RecordObject(db, "Set All Costs");
                    foreach (var e in db.entries)
                        if (e != null) { Undo.RecordObject(e, "Cost"); e.costCredits = _bulkCost; EditorUtility.SetDirty(e); }
                    EditorUtility.SetDirty(db);
                }
                if (GUILayout.Button("Remove Broken", GUILayout.Width(100)))
                {
                    Undo.RecordObject(db, "Remove Broken");
                    int before = db.entries.Count;
                    db.entries.RemoveAll(x => x == null || x.module == null);
                    EditorUtility.SetDirty(db);
                    Debug.Log($"[RepairManager] Removed {before - db.entries.Count} broken entries");
                }
                EditorGUILayout.EndHorizontal();

                // --- Search ---
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Search:", GUILayout.Width(42));
                _searchFilter = EditorGUILayout.TextField(_searchFilter, GUILayout.Height(18));
                if (GUILayout.Button("✕", GUILayout.Width(22), GUILayout.Height(18)))
                    _searchFilter = "";
                EditorGUILayout.EndHorizontal();

                if (string.IsNullOrEmpty(_searchFilter))
                {
                    EditorGUILayout.BeginHorizontal();
                    if (GUILayout.Button("🚀 Prop", EditorStyles.miniButton, GUILayout.Width(55)))
                        _searchFilter = "Propulsion";
                    if (GUILayout.Button("⚙ Util", EditorStyles.miniButton, GUILayout.Width(55)))
                        _searchFilter = "Utility";
                    if (GUILayout.Button("✨ Spec", EditorStyles.miniButton, GUILayout.Width(55)))
                        _searchFilter = "Special";
                    if (GUILayout.Button("🔥 Eng", EditorStyles.miniButton, GUILayout.Width(55)))
                        _searchFilter = "Engine";
                    EditorGUILayout.EndHorizontal();
                }

                EditorGUILayout.Space(2);

                // --- Entries list ---
                _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos, GUILayout.MinHeight(80), GUILayout.MaxHeight(300));

                for (int i = 0; i < db.entries.Count; i++)
                {
                    var entry = db.entries[i];
                    if (entry == null) continue;
                    var mod = entry.module;

                    // Filter
                    if (!string.IsNullOrEmpty(_searchFilter))
                    {
                        bool match = mod != null && (
                            mod.moduleId.ToLowerInvariant().Contains(_searchFilter.ToLowerInvariant()) ||
                            mod.type.ToString().ToLowerInvariant().Contains(_searchFilter.ToLowerInvariant()));
                        if (!match) continue;
                    }

                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    EditorGUILayout.BeginHorizontal();

                    // Icon
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
                        EditorGUILayout.LabelField(icon, GUILayout.Width(18));
                    }

                    // ID + name
                    string label = mod != null ? mod.moduleId : "(missing)";
                    if (label.Length > 28) label = label.Substring(0, 28) + "..";
                    if (mod == null) GUI.color = Color.red;
                    EditorGUILayout.LabelField(label, GUILayout.MinWidth(80));
                    GUI.color = Color.white;

                    // Cost
                    EditorGUI.BeginChangeCheck();
                    int c = EditorGUILayout.IntField(entry.costCredits, GUILayout.Width(55));
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(entry, "Cost");
                        entry.costCredits = c;
                        EditorUtility.SetDirty(entry);
                    }
                    EditorGUILayout.LabelField("CR", GUILayout.Width(22));

                    // Resources
                    int rc = entry.requiredResources?.Length ?? 0;
                    if (rc > 0)
                    {
                        GUI.color = Color.cyan;
                        EditorGUILayout.LabelField($"📦×{rc}", GUILayout.Width(45));
                        GUI.color = Color.white;
                    }

                    // Ping
                    if (GUILayout.Button("📍", GUILayout.Width(22)))
                        EditorGUIUtility.PingObject(mod != null ? (Object)mod : entry);

                    // Remove
                    if (GUILayout.Button("✕", GUILayout.Width(22)))
                    {
                        Undo.RecordObject(db, "Remove");
                        db.entries.RemoveAt(i);
                        EditorUtility.SetDirty(db);
                        break;
                    }

                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                }

                EditorGUILayout.EndScrollView();

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(4);
            serializedObject.ApplyModifiedProperties();

            DrawRepairManagerFields();
        }

        private void DrawRepairManagerFields()
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Repair Manager Settings", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_repaintCost"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_interactionRadius"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_interactHint"));
        }

        private void ScanModules(ModuleShopDatabase db)
        {
            string modulesPath = "Assets/_Project/Data/Modules";
            var moduleGuids = AssetDatabase.FindAssets("t:ShipModule", new[] { modulesPath });
            var entryGuids = AssetDatabase.FindAssets("t:ModuleShopEntry", new[] { modulesPath });

            Undo.RecordObject(db, "Scan Modules");
            var existingModuleIds = new HashSet<string>(
                db.entries.Where(x => x != null && x.module != null).Select(x => x.module.moduleId));
            int added = 0;

            foreach (var guid in moduleGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var mod = AssetDatabase.LoadAssetAtPath<ShipModule>(path);
                if (mod == null || string.IsNullOrEmpty(mod.moduleId)) continue;
                if (existingModuleIds.Contains(mod.moduleId)) continue;

                ModuleShopEntry existingEntry = null;
                foreach (var eg in entryGuids)
                {
                    var ep = AssetDatabase.GUIDToAssetPath(eg);
                    var e = AssetDatabase.LoadAssetAtPath<ModuleShopEntry>(ep);
                    if (e != null && e.module == mod) { existingEntry = e; break; }
                }

                if (existingEntry == null)
                {
                    string entryPath = Path.Combine(modulesPath, $"ShopEntry_{mod.moduleId}.asset");
                    entryPath = AssetDatabase.GenerateUniqueAssetPath(entryPath);
                    existingEntry = ScriptableObject.CreateInstance<ModuleShopEntry>();
                    existingEntry.module = mod;
                    existingEntry.costCredits = _bulkCost;
                    existingEntry.requiredResources = new ResourceRequirement[0];
                    AssetDatabase.CreateAsset(existingEntry, entryPath);
                }

                db.entries.Add(existingEntry);
                existingModuleIds.Add(mod.moduleId);
                added++;
            }

            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[RepairManager] Scan: added {added} new entries (total: {db.entries.Count})");
        }

        private void Validate(ModuleShopDatabase db)
        {
            var seen = new HashSet<ShipModule>();
            var errors = new List<string>();
            for (int i = 0; i < db.entries.Count; i++)
            {
                var e = db.entries[i];
                if (e == null) { errors.Add($"[{i}]: null"); continue; }
                if (e.module == null) { errors.Add($"[{i}]: module=null"); continue; }
                if (!seen.Add(e.module)) errors.Add($"[{i}]: duplicate '{e.module.moduleId}'");
            }
            if (errors.Count == 0)
                Debug.Log($"[RepairManager] ✅ {db.entries.Count(x => x != null)} entries valid");
            else
            {
                Debug.LogWarning($"[RepairManager] ⚠️ {errors.Count} issues");
                foreach (var e in errors) Debug.LogWarning($"  - {e}");
            }
        }
    }
}
#endif
