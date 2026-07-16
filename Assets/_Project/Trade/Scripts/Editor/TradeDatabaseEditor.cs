#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ProjectC.Trade.Editor
{
    /// <summary>
    /// Кастомный редактор для TradeDatabase.
    /// Возможности:
    ///   - Поиск по itemId / displayName
    ///   - Скан папки Items — авто-добавление TradeItemDefinition
    ///   - Очистить все / Валидация / Сортировка
    ///   - Статистика: сколько товаров, по фракциям, контрабанда
    /// </summary>
    [CustomEditor(typeof(TradeDatabase))]
    public class TradeDatabaseEditor : UnityEditor.Editor
    {
        private string _searchFilter = "";
        private Vector2 _scrollPos;

        public override void OnInspectorGUI()
        {
            var db = (TradeDatabase)target;
            serializedObject.Update();

            EditorGUILayout.LabelField($"Trade Item Database ({db.allItems?.Count ?? 0} items)", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            // === STATS ===
            if (db.allItems != null && db.allItems.Count > 0)
            {
                int valid = db.allItems.Count(x => x != null);
                int noId = db.allItems.Count(x => x != null && string.IsNullOrEmpty(x.itemId));
                int contraband = db.allItems.Count(x => x != null && x.isContraband);
                int dangerous = db.allItems.Count(x => x != null && x.isDangerous);
                int fragile = db.allItems.Count(x => x != null && x.isFragile);

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"Valid: {valid}", EditorStyles.miniLabel);
                EditorGUILayout.LabelField($"No ID: {noId}", noId > 0 ? new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = Color.red } } : EditorStyles.miniLabel);
                EditorGUILayout.LabelField($"Contraband: {contraband}", EditorStyles.miniLabel);
                EditorGUILayout.LabelField($"Dangerous: {dangerous}", EditorStyles.miniLabel);
                EditorGUILayout.LabelField($"Fragile: {fragile}", EditorStyles.miniLabel);
                EditorGUILayout.EndHorizontal();

                // Faction breakdown
                var factions = db.allItems
                    .Where(x => x != null)
                    .GroupBy(x => x.requiredFaction)
                    .OrderByDescending(g => g.Count());
                string factionStr = string.Join(" | ", factions.Select(g => $"{g.Key}:{g.Count()}"));
                EditorGUILayout.LabelField($"Factions: {factionStr}", EditorStyles.miniLabel);
            }
            EditorGUILayout.Space(4);

            // === BULK ACTIONS ===
            EditorGUILayout.LabelField("Bulk Actions", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Scan Items Folder", GUILayout.Height(24)))
            {
                ScanItemsFolder(db);
            }
            if (GUILayout.Button("Validate", GUILayout.Height(24)))
            {
                ValidateDatabase(db);
            }
            if (GUILayout.Button("Clear All", GUILayout.Height(24)))
            {
                Undo.RecordObject(db, "Clear Database");
                db.allItems.Clear();
                EditorUtility.SetDirty(db);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Sort by ID", GUILayout.Height(22)))
            {
                Undo.RecordObject(db, "Sort by ID");
                db.allItems = db.allItems
                    .Where(x => x != null)
                    .OrderBy(x => x.itemId)
                    .ToList();
                EditorUtility.SetDirty(db);
            }
            if (GUILayout.Button("Sort by Name", GUILayout.Height(22)))
            {
                Undo.RecordObject(db, "Sort by Name");
                db.allItems = db.allItems
                    .Where(x => x != null)
                    .OrderBy(x => x.displayName)
                    .ToList();
                EditorUtility.SetDirty(db);
            }
            if (GUILayout.Button("Remove Nulls", GUILayout.Height(22)))
            {
                Undo.RecordObject(db, "Remove Nulls");
                int before = db.allItems.Count;
                db.allItems.RemoveAll(x => x == null);
                EditorUtility.SetDirty(db);
                Debug.Log($"[TradeDB] Removed {before - db.allItems.Count} null entries");
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Select All in Project", GUILayout.Height(22)))
            {
                var paths = db.allItems
                    .Where(x => x != null)
                    .Select(x => AssetDatabase.GetAssetPath(x))
                    .Where(p => !string.IsNullOrEmpty(p))
                    .ToArray();
                Selection.objects = paths.Select(p => AssetDatabase.LoadAssetAtPath<Object>(p)).Where(o => o != null).ToArray();
                Debug.Log($"[TradeDB] Selected {Selection.objects.Length} items in Project View");
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
                if (GUILayout.Button("Contraband", EditorStyles.miniButtonLeft, GUILayout.Width(80)))
                    _searchFilter = "isContraband:true";
                if (GUILayout.Button("Dangerous", EditorStyles.miniButtonMid, GUILayout.Width(80)))
                    _searchFilter = "isDangerous:true";
                if (GUILayout.Button("Fragile", EditorStyles.miniButtonRight, GUILayout.Width(80)))
                    _searchFilter = "isFragile:true";
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.Space(4);

            // === ITEMS LIST ===
            var itemsProp = serializedObject.FindProperty("allItems");
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos, GUILayout.MinHeight(150), GUILayout.MaxHeight(500));

            if (itemsProp != null)
            {
                for (int i = 0; i < itemsProp.arraySize; i++)
                {
                    var elem = itemsProp.GetArrayElementAtIndex(i);
                    if (elem.objectReferenceValue == null) continue;

                    var item = (TradeItemDefinition)elem.objectReferenceValue;

                    // Search filter
                    if (!string.IsNullOrEmpty(_searchFilter))
                    {
                        if (_searchFilter == "isContraband:true" && !item.isContraband) continue;
                        if (_searchFilter == "isDangerous:true" && !item.isDangerous) continue;
                        if (_searchFilter == "isFragile:true" && !item.isFragile) continue;

                        if (!_searchFilter.StartsWith("is"))
                        {
                            bool match = (item.itemId != null && item.itemId.ToLowerInvariant().Contains(_searchFilter.ToLowerInvariant())) ||
                                         (item.displayName != null && item.displayName.ToLowerInvariant().Contains(_searchFilter.ToLowerInvariant()));
                            if (!match) continue;
                        }
                    }

                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    EditorGUILayout.BeginHorizontal();

                    // Icons
                    if (item.isContraband) { GUI.color = Color.red; EditorGUILayout.LabelField("🚫", GUILayout.Width(18)); }
                    if (item.isDangerous) { GUI.color = Color.yellow; EditorGUILayout.LabelField("⚠", GUILayout.Width(18)); }
                    if (item.isFragile) { GUI.color = Color.cyan; EditorGUILayout.LabelField("💔", GUILayout.Width(18)); }
                    GUI.color = Color.white;

                    // itemId and displayName
                    EditorGUILayout.LabelField(item.itemId ?? "(no id)", GUILayout.MinWidth(120));
                    EditorGUILayout.LabelField(item.displayName ?? "", EditorStyles.miniLabel);

                    // Price
                    EditorGUILayout.LabelField($"{item.basePrice:F0} CR", GUILayout.Width(60));

                    // Weight/Volume
                    GUI.color = Color.gray;
                    EditorGUILayout.LabelField($"⚖{item.weight:F1}", GUILayout.Width(50));
                    EditorGUILayout.LabelField($"📦{item.volume:F2}", GUILayout.Width(50));
                    EditorGUILayout.LabelField($"🎰{item.slots}", GUILayout.Width(30));
                    GUI.color = Color.white;

                    // Faction
                    if (item.requiredFaction != Faction.None)
                        EditorGUILayout.LabelField($"[{item.requiredFaction}]", GUILayout.Width(80));

                    // Ping & Remove
                    if (GUILayout.Button("📍", GUILayout.Width(24)))
                        EditorGUIUtility.PingObject(item);
                    if (GUILayout.Button("✕", GUILayout.Width(24)))
                    {
                        Undo.RecordObject(db, "Remove Item");
                        db.allItems.RemoveAt(i);
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

        private void ScanItemsFolder(TradeDatabase db)
        {
            string itemsPath = "Assets/_Project/Trade/Data/Items";
            var guids = AssetDatabase.FindAssets("t:TradeItemDefinition", new[] { itemsPath });
            Undo.RecordObject(db, "Scan Items Folder");
            var existing = new HashSet<TradeItemDefinition>(db.allItems.Where(x => x != null));
            int added = 0;

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var item = AssetDatabase.LoadAssetAtPath<TradeItemDefinition>(path);
                if (item == null || existing.Contains(item)) continue;

                db.allItems.Add(item);
                existing.Add(item);
                added++;
            }

            EditorUtility.SetDirty(db);
            Debug.Log($"[TradeDB] Scan complete: found {guids.Length} .asset files, added {added} new items (total: {db.allItems.Count})");
        }

        private void ValidateDatabase(TradeDatabase db)
        {
            var errors = new List<string>();
            var seenIds = new HashSet<string>();

            for (int i = 0; i < db.allItems.Count; i++)
            {
                var item = db.allItems[i];
                if (item == null)
                {
                    errors.Add($"[{i}]: null reference");
                    continue;
                }
                if (string.IsNullOrEmpty(item.itemId))
                    errors.Add($"[{i}]: '{item.displayName ?? "???"}' — empty itemId");

                if (!string.IsNullOrEmpty(item.itemId) && !seenIds.Add(item.itemId))
                    errors.Add($"[{i}]: duplicate itemId '{item.itemId}'");
            }

            if (errors.Count == 0)
                Debug.Log($"[TradeDB] ✅ Validation passed: {db.allItems.Count(x => x != null)} valid items");
            else
            {
                Debug.LogWarning($"[TradeDB] ⚠️ {errors.Count} issues:");
                foreach (var e in errors) Debug.LogWarning($"  - {e}");
            }
        }
    }
}
#endif
