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
    /// entries — прямые ссылки на ShipModule (цена и ресурсы внутри модуля).
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

            EditorGUILayout.LabelField($"Module Shop Database ({db.entries?.Count ?? 0} modules)", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            // === STATS ===
            if (db.entries != null && db.entries.Count > 0)
            {
                int valid = db.entries.Count(x => x != null);
                int broken = db.entries.Count(x => x == null);
                int totalCost = db.entries.Where(x => x != null).Sum(x => x.costCredits);

                var byType = db.entries
                    .Where(x => x != null)
                    .GroupBy(x => x.type)
                    .OrderByDescending(g => g.Count());

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"Valid: {valid} | Total: {totalCost} CR", EditorStyles.miniLabel);
                if (broken > 0)
                    EditorGUILayout.LabelField($"Null: {broken}", new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = Color.red } });
                EditorGUILayout.EndHorizontal();

                string typeStr = string.Join(" | ", byType.Select(g => $"{g.Key}:{g.Count()}"));
                EditorGUILayout.LabelField($"Types: {typeStr}", EditorStyles.miniLabel);
            }
            EditorGUILayout.Space(4);

            // === BULK ACTIONS ===
            EditorGUILayout.LabelField("Bulk Actions", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Scan Modules Folder", GUILayout.Height(24)))
                ScanModulesFolder(db);
            if (GUILayout.Button("Validate", GUILayout.Height(24)))
                ValidateDatabase(db);
            if (GUILayout.Button("Clear All", GUILayout.Height(24)))
            {
                Undo.RecordObject(db, "Clear");
                db.entries.Clear();
                EditorUtility.SetDirty(db);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Cost:", GUILayout.Width(35));
            _bulkCost = EditorGUILayout.IntField(_bulkCost, GUILayout.Width(60));
            if (GUILayout.Button("Apply to All", GUILayout.Width(80)))
            {
                Undo.RecordObject(db, "Set Costs");
                foreach (var m in db.entries)
                    if (m != null) { Undo.RecordObject(m, "Cost"); m.costCredits = _bulkCost; EditorUtility.SetDirty(m); }
                EditorUtility.SetDirty(db);
            }
            if (GUILayout.Button("Sort by Cost", GUILayout.Height(22)))
            {
                Undo.RecordObject(db, "Sort");
                db.entries = db.entries.Where(x => x != null).OrderBy(x => x.costCredits).ThenBy(x => x.moduleId).ToList();
                EditorUtility.SetDirty(db);
            }
            if (GUILayout.Button("Sort by Name", GUILayout.Height(22)))
            {
                Undo.RecordObject(db, "Sort");
                db.entries = db.entries.Where(x => x != null).OrderBy(x => x.moduleId).ToList();
                EditorUtility.SetDirty(db);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Remove Nulls", GUILayout.Height(22)))
            {
                Undo.RecordObject(db, "Remove Nulls");
                int before = db.entries.Count;
                db.entries.RemoveAll(x => x == null);
                EditorUtility.SetDirty(db);
                Debug.Log($"[ModuleShopDB] Removed {before - db.entries.Count} nulls");
            }
            if (GUILayout.Button("Select All in Project", GUILayout.Height(22)))
            {
                var paths = db.entries.Where(x => x != null).Select(x => AssetDatabase.GetAssetPath(x)).Where(p => !string.IsNullOrEmpty(p)).ToArray();
                Selection.objects = paths.Select(p => AssetDatabase.LoadAssetAtPath<Object>(p)).Where(o => o != null).ToArray();
                Debug.Log($"[ModuleShopDB] Selected {Selection.objects.Length} modules");
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(8);

            // === SEARCH ===
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Search:", GUILayout.Width(50));
            _searchFilter = EditorGUILayout.TextField(_searchFilter, GUILayout.Height(18));
            if (GUILayout.Button("✕", GUILayout.Width(24), GUILayout.Height(18))) _searchFilter = "";
            EditorGUILayout.EndHorizontal();

            if (string.IsNullOrEmpty(_searchFilter))
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("🚀 Prop", EditorStyles.miniButton, GUILayout.Width(55))) _searchFilter = "Propulsion";
                if (GUILayout.Button("⚙ Util", EditorStyles.miniButton, GUILayout.Width(55))) _searchFilter = "Utility";
                if (GUILayout.Button("✨ Spec", EditorStyles.miniButton, GUILayout.Width(55))) _searchFilter = "Special";
                if (GUILayout.Button("🔥 Eng", EditorStyles.miniButton, GUILayout.Width(55))) _searchFilter = "Engine";
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.Space(4);

            // === ENTRIES ===
            var entriesProp = serializedObject.FindProperty("entries");
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos, GUILayout.MinHeight(150), GUILayout.MaxHeight(500));

            if (entriesProp != null)
            {
                for (int i = 0; i < entriesProp.arraySize; i++)
                {
                    var elem = entriesProp.GetArrayElementAtIndex(i);
                    if (elem.objectReferenceValue == null) continue;
                    var mod = (ShipModule)elem.objectReferenceValue;

                    if (!string.IsNullOrEmpty(_searchFilter))
                    {
                        if (!mod.moduleId.ToLowerInvariant().Contains(_searchFilter.ToLowerInvariant()) &&
                            !mod.type.ToString().ToLowerInvariant().Contains(_searchFilter.ToLowerInvariant()))
                            continue;
                    }

                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    EditorGUILayout.BeginHorizontal();

                    string icon = mod.type switch { ModuleType.Propulsion => "🚀", ModuleType.Utility => "⚙", ModuleType.Special => "✨", ModuleType.Engine => "🔥", _ => "📦" };
                    EditorGUILayout.LabelField(icon, GUILayout.Width(18));

                    string label = mod.moduleId.Length > 28 ? mod.moduleId.Substring(0, 28) + ".." : mod.moduleId;
                    EditorGUILayout.LabelField(label, GUILayout.MinWidth(90));

                    EditorGUI.BeginChangeCheck();
                    int c = EditorGUILayout.IntField(mod.costCredits, GUILayout.Width(55));
                    if (EditorGUI.EndChangeCheck()) { Undo.RecordObject(mod, "Cost"); mod.costCredits = c; EditorUtility.SetDirty(mod); }
                    EditorGUILayout.LabelField("CR", GUILayout.Width(22));

                    int rc = mod.requiredResources?.Length ?? 0;
                    if (rc > 0) { GUI.color = Color.cyan; EditorGUILayout.LabelField($"📦×{rc}", GUILayout.Width(45)); GUI.color = Color.white; }

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
                    EditorGUILayout.Space(1);
                }
            }
            EditorGUILayout.EndScrollView();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("+ Add Module", GUILayout.Height(22)))
                AddModuleWindow.Show(db, _bulkCost);
            if (GUILayout.Button("+ Mass Add from Catalog", GUILayout.Height(22)))
                MassAddModulesWindow.Show(db, _bulkCost);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(8);
            serializedObject.ApplyModifiedProperties();
        }

        private void ScanModulesFolder(ModuleShopDatabase db)
        {
            string modulesPath = "Assets/_Project/Data/Modules";
            var guids = AssetDatabase.FindAssets("t:ShipModule", new[] { modulesPath });
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
            Debug.Log($"[ModuleShopDB] Scan: +{added}, total={db.entries.Count}");
        }

        private void ValidateDatabase(ModuleShopDatabase db)
        {
            var seen = new HashSet<ShipModule>();
            var errors = new List<string>();
            for (int i = 0; i < db.entries.Count; i++)
            {
                var m = db.entries[i];
                if (m == null) { errors.Add($"[{i}]: null"); continue; }
                if (string.IsNullOrEmpty(m.moduleId)) errors.Add($"[{i}]: empty moduleId");
                if (!seen.Add(m)) errors.Add($"[{i}]: duplicate '{m.moduleId}'");
            }
            if (errors.Count == 0) Debug.Log($"[ModuleShopDB] ✅ {db.entries.Count(x => x != null)} valid");
            else { Debug.LogWarning($"[ModuleShopDB] ⚠️ {errors.Count} issues"); foreach (var e in errors) Debug.LogWarning($"  - {e}"); }
        }
    }

    public class AddModuleWindow : EditorWindow
    {
        private ModuleShopDatabase _db;
        private ShipModule _selected;
        private int _cost = 500;

        public static void Show(ModuleShopDatabase db, int defaultCost)
        {
            var w = GetWindow<AddModuleWindow>(true, "Add Module");
            w._db = db; w._cost = defaultCost;
            w.minSize = new Vector2(350, 120); w.maxSize = new Vector2(500, 140);
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Select ShipModule:", EditorStyles.boldLabel);
            _selected = (ShipModule)EditorGUILayout.ObjectField("Module", _selected, typeof(ShipModule), false);
            _cost = EditorGUILayout.IntField("Cost (CR)", _cost);
            EditorGUILayout.Space(8);
            EditorGUILayout.BeginHorizontal();
            GUI.enabled = _selected != null && !string.IsNullOrEmpty(_selected.moduleId);
            if (GUILayout.Button("Add", GUILayout.Height(28)))
            {
                if (_db.entries.Any(x => x == _selected)) { EditorUtility.DisplayDialog("Duplicate", $"'{_selected.moduleId}' already exists.", "OK"); return; }
                Undo.RecordObject(_db, "Add");
                Undo.RecordObject(_selected, "Set Cost");
                _selected.costCredits = _cost;
                EditorUtility.SetDirty(_selected);
                _db.entries.Add(_selected);
                EditorUtility.SetDirty(_db);
                Debug.Log($"[AddModule] Added: {_selected.moduleId} ({_cost} CR)");
                Close();
            }
            GUI.enabled = true;
            if (GUILayout.Button("Cancel", GUILayout.Height(28))) Close();
            EditorGUILayout.EndHorizontal();
        }
    }

    public class MassAddModulesWindow : EditorWindow
    {
        private ModuleShopDatabase _db;
        private List<ShipModule> _available = new();
        private HashSet<int> _selected = new();
        private Vector2 _scroll;
        private string _filter = "";
        private int _cost = 500;

        public static void Show(ModuleShopDatabase db, int defaultCost)
        {
            var w = GetWindow<MassAddModulesWindow>(true, "Mass Add Modules");
            w._db = db; w._cost = defaultCost;
            w.minSize = new Vector2(400, 500);
            w.LoadAvailable();
        }

        private void LoadAvailable()
        {
            _available.Clear(); _selected.Clear();
            var existing = new HashSet<ShipModule>(_db.entries.Where(x => x != null));
            foreach (var g in AssetDatabase.FindAssets("t:ShipModule"))
            {
                var mod = AssetDatabase.LoadAssetAtPath<ShipModule>(AssetDatabase.GUIDToAssetPath(g));
                if (mod != null && !string.IsNullOrEmpty(mod.moduleId) && !existing.Contains(mod))
                    _available.Add(mod);
            }
            _available = _available.OrderBy(m => m.type).ThenBy(m => m.moduleId).ToList();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField($"Available ({_available.Count})", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Cost:", GUILayout.Width(35));
            _cost = EditorGUILayout.IntField(_cost, GUILayout.Width(60));
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            _filter = EditorGUILayout.TextField("Filter:", _filter);
            if (GUILayout.Button("All", GUILayout.Width(50)))
                for (int i = 0; i < _available.Count; i++)
                    if (string.IsNullOrEmpty(_filter) || _available[i].moduleId.ToLowerInvariant().Contains(_filter.ToLowerInvariant())) _selected.Add(i);
            if (GUILayout.Button("None", GUILayout.Width(50))) _selected.Clear();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(4);
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            for (int i = 0; i < _available.Count; i++)
            {
                if (!string.IsNullOrEmpty(_filter) && !_available[i].moduleId.ToLowerInvariant().Contains(_filter.ToLowerInvariant())) continue;
                string icon = _available[i].type switch { ModuleType.Propulsion => "🚀", ModuleType.Utility => "⚙", ModuleType.Special => "✨", ModuleType.Engine => "🔥", _ => "📦" };
                bool sel = _selected.Contains(i);
                if (EditorGUILayout.ToggleLeft($"{icon} {_available[i].moduleId} (T{_available[i].tier})", sel) != sel)
                { if (sel) _selected.Remove(i); else _selected.Add(i); }
            }
            EditorGUILayout.EndScrollView();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button($"Add {_selected.Count}", GUILayout.Height(30)))
            {
                Undo.RecordObject(_db, "Mass Add");
                foreach (int idx in _selected.OrderBy(x => x))
                {
                    var mod = _available[idx];
                    Undo.RecordObject(mod, "Cost");
                    mod.costCredits = _cost;
                    EditorUtility.SetDirty(mod);
                    _db.entries.Add(mod);
                }
                EditorUtility.SetDirty(_db);
                Debug.Log($"[MassAdd] Added {_selected.Count} modules");
                Close();
            }
            if (GUILayout.Button("Cancel", GUILayout.Height(30))) Close();
            EditorGUILayout.EndHorizontal();
        }
    }
}
#endif
