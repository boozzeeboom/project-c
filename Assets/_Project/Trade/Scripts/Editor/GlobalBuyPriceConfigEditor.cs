#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using ProjectC.Trade.Config;

namespace ProjectC.Trade.Editor
{
    /// <summary>
    /// Кастомный редактор для GlobalBuyPriceConfig.
    /// Возможности:
    ///   - Поиск по itemId
    ///   - Импорт всех товаров из TradeItemDatabase
    ///   - Массовая установка buyPrice
    ///   - Очистить все / Удалить отсутствующие в БД
    /// </summary>
    [CustomEditor(typeof(GlobalBuyPriceConfig))]
    public class GlobalBuyPriceConfigEditor : UnityEditor.Editor
    {
        private string _searchFilter = "";
        private Vector2 _scrollPos;
        private float _bulkPrice = 1f;

        public override void OnInspectorGUI()
        {
            var cfg = (GlobalBuyPriceConfig)target;
            serializedObject.Update();

            EditorGUILayout.LabelField($"Global Buy Price Config ({cfg.entries?.Count ?? 0} entries)", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            // === BULK ACTIONS ===
            EditorGUILayout.LabelField("Bulk Actions", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Import All from Database", GUILayout.Height(24)))
            {
                ImportFromDatabase(cfg);
            }
            if (GUILayout.Button("Clear All", GUILayout.Height(24)))
            {
                Undo.RecordObject(cfg, "Clear All Buy Prices");
                cfg.entries.Clear();
                EditorUtility.SetDirty(cfg);
            }
            if (GUILayout.Button("Remove Missing", GUILayout.Height(24)))
            {
                RemoveMissing(cfg);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Set Price to All:", GUILayout.Width(100));
            _bulkPrice = EditorGUILayout.FloatField(_bulkPrice, GUILayout.Width(60));
            if (GUILayout.Button("Apply", GUILayout.Width(60)))
            {
                Undo.RecordObject(cfg, "Set All Buy Prices");
                foreach (var e in cfg.entries)
                {
                    if (e != null) e.buyPrice = _bulkPrice;
                }
                EditorUtility.SetDirty(cfg);
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
            EditorGUILayout.Space(4);

            // === ENTRIES LIST ===
            var entriesProp = serializedObject.FindProperty("entries");
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos, GUILayout.MinHeight(150), GUILayout.MaxHeight(500));

            if (entriesProp != null)
            {
                for (int i = 0; i < entriesProp.arraySize; i++)
                {
                    var elem = entriesProp.GetArrayElementAtIndex(i);
                    var idProp = elem.FindPropertyRelative("itemId");
                    var defProp = elem.FindPropertyRelative("definition");
                    var priceProp = elem.FindPropertyRelative("buyPrice");

                    string id = idProp?.stringValue ?? "";
                    string display = defProp?.objectReferenceValue != null
                        ? ((TradeItemDefinition)defProp.objectReferenceValue).displayName
                        : "";

                    // Filter
                    if (!string.IsNullOrEmpty(_searchFilter))
                    {
                        bool match = (id != null && id.ToLowerInvariant().Contains(_searchFilter.ToLowerInvariant())) ||
                                     (display != null && display.ToLowerInvariant().Contains(_searchFilter.ToLowerInvariant()));
                        if (!match) continue;
                    }

                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    EditorGUILayout.BeginHorizontal();

                    // itemId
                    if (defProp?.objectReferenceValue != null)
                    {
                        string label = display ?? id;
                        if (label.Length > 35) label = label.Substring(0, 35) + "...";
                        EditorGUILayout.LabelField(label, GUILayout.MinWidth(120));
                    }
                    else
                    {
                        EditorGUILayout.PropertyField(idProp, GUIContent.none, GUILayout.MinWidth(120));
                    }

                    // Price
                    EditorGUILayout.LabelField("Buy Price:", GUILayout.Width(60));
                    EditorGUILayout.PropertyField(priceProp, GUIContent.none, GUILayout.Width(70));

                    // Delete button
                    if (GUILayout.Button("✕", GUILayout.Width(24)))
                    {
                        Undo.RecordObject(cfg, "Remove Entry");
                        cfg.entries.RemoveAt(i);
                        EditorUtility.SetDirty(cfg);
                        break;
                    }

                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                    EditorGUILayout.Space(1);
                }
            }

            EditorGUILayout.EndScrollView();

            // Add single
            if (GUILayout.Button("+ Add Entry", GUILayout.Height(22)))
            {
                Undo.RecordObject(cfg, "Add Entry");
                cfg.entries.Add(new GlobalBuyPriceEntry { buyPrice = _bulkPrice });
                EditorUtility.SetDirty(cfg);
            }

            EditorGUILayout.Space(8);
            serializedObject.ApplyModifiedProperties();
        }

        private void ImportFromDatabase(GlobalBuyPriceConfig cfg)
        {
            var dbGuid = AssetDatabase.FindAssets("t:TradeDatabase").FirstOrDefault();
            if (string.IsNullOrEmpty(dbGuid))
            {
                Debug.LogWarning("[GlobalBuyPriceEditor] TradeItemDatabase not found");
                return;
            }

            var db = AssetDatabase.LoadAssetAtPath<TradeDatabase>(AssetDatabase.GUIDToAssetPath(dbGuid));
            if (db == null || db.allItems == null) return;

            Undo.RecordObject(cfg, "Import from Database");
            var existingIds = new HashSet<string>(cfg.entries.Where(x => x != null && !string.IsNullOrEmpty(x.itemId)).Select(x => x.itemId));
            int added = 0;

            foreach (var item in db.allItems)
            {
                if (item == null || string.IsNullOrEmpty(item.itemId)) continue;
                if (existingIds.Contains(item.itemId)) continue;

                cfg.entries.Add(new GlobalBuyPriceEntry
                {
                    itemId = item.itemId,
                    definition = item,
                    buyPrice = _bulkPrice
                });
                existingIds.Add(item.itemId);
                added++;
            }

            EditorUtility.SetDirty(cfg);
            Debug.Log($"[GlobalBuyPriceEditor] Imported {added} new entries from TradeItemDatabase (total: {cfg.entries.Count})");
        }

        private void RemoveMissing(GlobalBuyPriceConfig cfg)
        {
            var dbGuid = AssetDatabase.FindAssets("t:TradeDatabase").FirstOrDefault();
            TradeDatabase db = null;
            if (!string.IsNullOrEmpty(dbGuid))
                db = AssetDatabase.LoadAssetAtPath<TradeDatabase>(AssetDatabase.GUIDToAssetPath(dbGuid));

            var validIds = db != null
                ? new HashSet<string>(db.allItems.Where(x => x != null && !string.IsNullOrEmpty(x.itemId)).Select(x => x.itemId))
                : new HashSet<string>();

            Undo.RecordObject(cfg, "Remove Missing");
            int removed = cfg.entries.RemoveAll(e => e == null || string.IsNullOrEmpty(e.itemId) || (validIds.Count > 0 && !validIds.Contains(e.itemId)));
            EditorUtility.SetDirty(cfg);
            Debug.Log($"[GlobalBuyPriceEditor] Removed {removed} entries not in TradeItemDatabase");
        }
    }
}
#endif
