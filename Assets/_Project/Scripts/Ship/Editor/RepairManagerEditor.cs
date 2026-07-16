#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ProjectC.Ship.Editor
{
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
            var dbProp = serializedObject.FindProperty("_shopDatabase");
            EditorGUILayout.PropertyField(dbProp, new GUIContent("Module Shop Database"));
            var db = dbProp.objectReferenceValue as ModuleShopDatabase;

            if (db == null)
            {
                serializedObject.ApplyModifiedProperties();
                EditorGUILayout.HelpBox("Assign ModuleShopDatabase to edit modules inline.", MessageType.Info);
                DrawRepairFields();
                return;
            }

            EditorGUILayout.Space(4);
            _dbFoldout = EditorGUILayout.Foldout(_dbFoldout, $"🛠 Modules ({db.entries?.Count ?? 0})", true, EditorStyles.foldoutHeader);

            if (_dbFoldout)
            {
                EditorGUI.indentLevel++;

                // Stats
                if (db.entries != null && db.entries.Count > 0)
                {
                    int valid = db.entries.Count(x => x != null);
                    int totalCost = db.entries.Where(x => x != null).Sum(x => x.costCredits);
                    EditorGUILayout.LabelField($"Valid: {valid} | Total cost: {totalCost} CR", EditorStyles.miniLabel);
                }

                // Bulk
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Scan", GUILayout.Height(22))) ScanModules(db);
                if (GUILayout.Button("Validate", GUILayout.Height(22))) Validate(db);
                if (GUILayout.Button("Clear", GUILayout.Height(22))) { Undo.RecordObject(db, "Clear"); db.entries.Clear(); EditorUtility.SetDirty(db); }
                if (GUILayout.Button("📍 Ping", GUILayout.Height(22))) EditorGUIUtility.PingObject(db);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Cost:", GUILayout.Width(35));
                _bulkCost = EditorGUILayout.IntField(_bulkCost, GUILayout.Width(60));
                if (GUILayout.Button("Apply All", GUILayout.Width(70)))
                {
                    Undo.RecordObject(db, "Costs");
                    foreach (var m in db.entries) if (m != null) { Undo.RecordObject(m, "Cost"); m.costCredits = _bulkCost; EditorUtility.SetDirty(m); }
                    EditorUtility.SetDirty(db);
                }
                if (GUILayout.Button("Remove Nulls", GUILayout.Width(90)))
                {
                    Undo.RecordObject(db, "Remove Nulls");
                    db.entries.RemoveAll(x => x == null);
                    EditorUtility.SetDirty(db);
                }
                EditorGUILayout.EndHorizontal();

                // Search
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Search:", GUILayout.Width(42));
                _searchFilter = EditorGUILayout.TextField(_searchFilter, GUILayout.Height(18));
                if (GUILayout.Button("✕", GUILayout.Width(22), GUILayout.Height(18))) _searchFilter = "";
                EditorGUILayout.EndHorizontal();
                if (string.IsNullOrEmpty(_searchFilter))
                {
                    EditorGUILayout.BeginHorizontal();
                    if (GUILayout.Button("🚀", EditorStyles.miniButton, GUILayout.Width(28))) _searchFilter = "Propulsion";
                    if (GUILayout.Button("⚙", EditorStyles.miniButton, GUILayout.Width(28))) _searchFilter = "Utility";
                    if (GUILayout.Button("✨", EditorStyles.miniButton, GUILayout.Width(28))) _searchFilter = "Special";
                    if (GUILayout.Button("🔥", EditorStyles.miniButton, GUILayout.Width(28))) _searchFilter = "Engine";
                    EditorGUILayout.EndHorizontal();
                }
                EditorGUILayout.Space(2);

                // List
                _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos, GUILayout.MinHeight(80), GUILayout.MaxHeight(300));
                for (int i = 0; i < db.entries.Count; i++)
                {
                    var mod = db.entries[i];
                    if (mod == null) continue;
                    if (!string.IsNullOrEmpty(_searchFilter) &&
                        !mod.moduleId.ToLowerInvariant().Contains(_searchFilter.ToLowerInvariant()) &&
                        !mod.type.ToString().ToLowerInvariant().Contains(_searchFilter.ToLowerInvariant())) continue;

                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    EditorGUILayout.BeginHorizontal();

                    string icon = mod.type switch { ModuleType.Propulsion => "🚀", ModuleType.Utility => "⚙", ModuleType.Special => "✨", ModuleType.Engine => "🔥", _ => "📦" };
                    EditorGUILayout.LabelField(icon, GUILayout.Width(18));
                    string lbl = mod.moduleId.Length > 26 ? mod.moduleId.Substring(0, 26) + ".." : mod.moduleId;
                    EditorGUILayout.LabelField(lbl, GUILayout.MinWidth(75));

                    EditorGUI.BeginChangeCheck();
                    int c = EditorGUILayout.IntField(mod.costCredits, GUILayout.Width(55));
                    if (EditorGUI.EndChangeCheck()) { Undo.RecordObject(mod, "Cost"); mod.costCredits = c; EditorUtility.SetDirty(mod); }
                    EditorGUILayout.LabelField("CR", GUILayout.Width(22));

                    int rc = mod.requiredResources?.Length ?? 0;
                    if (rc > 0) { GUI.color = Color.cyan; EditorGUILayout.LabelField($"📦×{rc}", GUILayout.Width(42)); GUI.color = Color.white; }

                    if (GUILayout.Button("📍", GUILayout.Width(22))) EditorGUIUtility.PingObject(mod);
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

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("+ Add Module", GUILayout.Height(22)))
                    AddModuleWindow.Show(db, _bulkCost);
                if (GUILayout.Button("+ Mass Add", GUILayout.Height(22)))
                    MassAddModulesWindow.Show(db, _bulkCost);
                EditorGUILayout.EndHorizontal();

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(4);
            serializedObject.ApplyModifiedProperties();
            DrawRepairFields();
        }

        private void DrawRepairFields()
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Repair Manager Settings", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_repaintCost"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_interactionRadius"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_interactHint"));
        }

        private void ScanModules(ModuleShopDatabase db)
        {
            var guids = AssetDatabase.FindAssets("t:ShipModule", new[] { "Assets/_Project/Data/Modules" });
            Undo.RecordObject(db, "Scan");
            var existing = new HashSet<ShipModule>(db.entries.Where(x => x != null));
            int added = 0;
            foreach (var g in guids)
            {
                var mod = AssetDatabase.LoadAssetAtPath<ShipModule>(AssetDatabase.GUIDToAssetPath(g));
                if (mod == null || string.IsNullOrEmpty(mod.moduleId) || existing.Contains(mod)) continue;
                db.entries.Add(mod); existing.Add(mod); added++;
            }
            EditorUtility.SetDirty(db);
            Debug.Log($"[RepairManager] Scan: +{added}, total={db.entries.Count}");
        }

        private void Validate(ModuleShopDatabase db)
        {
            var seen = new HashSet<ShipModule>();
            var errors = new List<string>();
            for (int i = 0; i < db.entries.Count; i++)
            {
                var m = db.entries[i];
                if (m == null) { errors.Add($"[{i}]: null"); continue; }
                if (!seen.Add(m)) errors.Add($"[{i}]: dup '{m.moduleId}'");
            }
            if (errors.Count == 0) Debug.Log($"[RepairManager] ✅ {db.entries.Count(x => x != null)} valid");
            else { Debug.LogWarning($"[RepairManager] ⚠️ {errors.Count} issues"); foreach (var e in errors) Debug.LogWarning($"  - {e}"); }
        }
    }
}
#endif
