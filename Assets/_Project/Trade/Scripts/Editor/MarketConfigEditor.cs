#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using ProjectC.Trade.Config;

namespace ProjectC.Trade.Editor
{
    /// <summary>
    /// Кастомный редактор для MarketConfig.
    /// Возможности:
    ///   - Поиск по itemId/displayName
    ///   - Массовые действия: очистить, добавить, установить stock/цену всем
    ///   - Trade mode: BuyAndSell / BuyOnly / SellOnly
    ///   - BuyAnyItem + GlobalBuyPriceConfig
    ///   - Комиссии, ценовой коридор, затухание
    ///   - Валидация
    ///   - Duplicate
    /// </summary>
    [CustomEditor(typeof(MarketConfig))]
    public class MarketConfigEditor : UnityEditor.Editor
    {
        private string _searchFilter = "";
        private Vector2 _scrollPos;

        public override void OnInspectorGUI()
        {
            var cfg = (MarketConfig)target;
            serializedObject.Update();

            // === HEADER ===
            EditorGUILayout.LabelField($"Market: {cfg.displayName} ({cfg.locationId})", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            // === LOCATION ===
            EditorGUILayout.PropertyField(serializedObject.FindProperty("locationId"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("displayName"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("description"));
            EditorGUILayout.Space(8);

            // === TRADE MODE ===
            EditorGUILayout.LabelField("Trade Mode", EditorStyles.boldLabel);
            var tradeModeProp = serializedObject.FindProperty("tradeMode");
            EditorGUILayout.PropertyField(tradeModeProp);
            EditorGUILayout.Space(4);

            // BuyAnyItem
            var buyAnyItemProp = serializedObject.FindProperty("buyAnyItem");
            EditorGUILayout.PropertyField(buyAnyItemProp);
            if (buyAnyItemProp.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(serializedObject.FindProperty("globalBuyPriceConfig"));
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.Space(8);

            // === COMMISSIONS ===
            EditorGUILayout.LabelField("Commissions", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("sellCommission"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("buyCommission"));
            EditorGUILayout.Space(8);

            // === PRICE CORRIDOR ===
            EditorGUILayout.LabelField("Price Corridor", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("priceFloorRatio"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("priceCeilingRatio"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("decayHalfLifeSeconds"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("regenMultiplier"));
            EditorGUILayout.Space(8);

            // === BULK ACTIONS ===
            EditorGUILayout.LabelField("Bulk Actions", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Clear All Items", GUILayout.Height(24)))
            {
                Undo.RecordObject(cfg, "Clear Market Items");
                cfg.items.Clear();
                EditorUtility.SetDirty(cfg);
            }
            if (GUILayout.Button("Mass Add from Database", GUILayout.Height(24)))
            {
                MassAddWindow.Show(cfg);
            }
            if (GUILayout.Button("Validate", GUILayout.Height(24)))
            {
                ValidateConfig(cfg);
            }
            if (GUILayout.Button("Duplicate Config", GUILayout.Height(24)))
            {
                DuplicateConfig(cfg);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            float pctBuy = 0f;
            float pctSell = 0f;
            int newStock = 50;
            EditorGUILayout.LabelField("% Buy Price:", GUILayout.Width(80));
            pctBuy = EditorGUILayout.FloatField(pctBuy, GUILayout.Width(50));
            if (GUILayout.Button("Apply to All", GUILayout.Width(80)))
            {
                Undo.RecordObject(cfg, "Adjust Buy Prices");
                foreach (var item in cfg.items)
                {
                    if (item != null)
                        item.basePrice *= (1f + pctBuy / 100f);
                }
                EditorUtility.SetDirty(cfg);
            }
            EditorGUILayout.LabelField("% Sell Price:", GUILayout.Width(80));
            pctSell = EditorGUILayout.FloatField(pctSell, GUILayout.Width(50));
            if (GUILayout.Button("Apply to All", GUILayout.Width(80)))
            {
                Undo.RecordObject(cfg, "Adjust Sell Prices");
                foreach (var item in cfg.items)
                {
                    if (item != null)
                        item.basePrice *= (1f + pctSell / 100f);
                }
                EditorUtility.SetDirty(cfg);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Set Stock to All:", GUILayout.Width(100));
            newStock = EditorGUILayout.IntField(newStock, GUILayout.Width(60));
            if (GUILayout.Button("Apply Stock", GUILayout.Width(100)))
            {
                Undo.RecordObject(cfg, "Set All Stock");
                foreach (var item in cfg.items)
                {
                    if (item != null) item.initialStock = newStock;
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

            // === ITEMS LIST ===
            EditorGUILayout.LabelField($"Items ({cfg.items?.Count ?? 0})", EditorStyles.boldLabel);

            var itemsProp = serializedObject.FindProperty("items");
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos, GUILayout.MinHeight(200), GUILayout.MaxHeight(500));

            if (itemsProp != null)
            {
                // Filter items
                var filteredIndices = new List<int>();
                for (int i = 0; i < itemsProp.arraySize; i++)
                {
                    var elem = itemsProp.GetArrayElementAtIndex(i);
                    var idProp = elem.FindPropertyRelative("itemId");
                    var defProp = elem.FindPropertyRelative("definition");

                    string id = idProp?.stringValue ?? "";
                    string display = defProp?.objectReferenceValue != null
                        ? ((TradeItemDefinition)defProp.objectReferenceValue).displayName
                        : "";

                    if (string.IsNullOrEmpty(_searchFilter) ||
                        (id != null && id.ToLowerInvariant().Contains(_searchFilter.ToLowerInvariant())) ||
                        (display != null && display.ToLowerInvariant().Contains(_searchFilter.ToLowerInvariant())))
                    {
                        filteredIndices.Add(i);
                    }
                }

                for (int f = 0; f < filteredIndices.Count; f++)
                {
                    int i = filteredIndices[f];
                    var elem = itemsProp.GetArrayElementAtIndex(i);

                    var idProp = elem.FindPropertyRelative("itemId");
                    var defProp = elem.FindPropertyRelative("definition");
                    var buyProp = elem.FindPropertyRelative("allowBuy");
                    var sellProp = elem.FindPropertyRelative("allowSell");
                    var priceProp = elem.FindPropertyRelative("basePrice");
                    var stockProp = elem.FindPropertyRelative("initialStock");
                    var regenProp = elem.FindPropertyRelative("regenPerTick");

                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                    EditorGUILayout.BeginHorizontal();
                    // Buy/Sell indicators
                    GUI.color = buyProp?.boolValue ?? false ? Color.green : Color.gray;
                    EditorGUILayout.LabelField("B", EditorStyles.boldLabel, GUILayout.Width(16));
                    GUI.color = sellProp?.boolValue ?? false ? Color.green : Color.gray;
                    EditorGUILayout.LabelField("S", EditorStyles.boldLabel, GUILayout.Width(16));
                    GUI.color = Color.white;

                    EditorGUILayout.PropertyField(idProp, GUIContent.none);
                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.PropertyField(defProp, GUIContent.none);
                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("Price:", GUILayout.Width(40));
                    EditorGUILayout.PropertyField(priceProp, GUIContent.none, GUILayout.Width(70));
                    EditorGUILayout.LabelField("Stock:", GUILayout.Width(40));
                    EditorGUILayout.PropertyField(stockProp, GUIContent.none, GUILayout.Width(50));
                    EditorGUILayout.LabelField("Regen:", GUILayout.Width(42));
                    EditorGUILayout.PropertyField(regenProp, GUIContent.none);
                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.PropertyField(buyProp, GUIContent.none, GUILayout.Width(80));
                    EditorGUILayout.PropertyField(sellProp, GUIContent.none, GUILayout.Width(80));

                    if (GUILayout.Button("✕", GUILayout.Width(24)))
                    {
                        Undo.RecordObject(cfg, "Remove Market Item");
                        cfg.items.RemoveAt(i);
                        EditorUtility.SetDirty(cfg);
                        break; // exit loop after removal
                    }
                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.EndVertical();
                    EditorGUILayout.Space(2);
                }

                if (filteredIndices.Count == 0 && !string.IsNullOrEmpty(_searchFilter))
                {
                    EditorGUILayout.HelpBox($"No items match '{_searchFilter}'", MessageType.Info);
                }
            }

            EditorGUILayout.EndScrollView();

            // Add single item button
            if (GUILayout.Button("+ Add Item", GUILayout.Height(22)))
            {
                Undo.RecordObject(cfg, "Add Market Item");
                cfg.items.Add(new MarketItemConfig());
                EditorUtility.SetDirty(cfg);
            }

            EditorGUILayout.Space(8);

            serializedObject.ApplyModifiedProperties();
        }

        private void ValidateConfig(MarketConfig cfg)
        {
            var dbGuid = AssetDatabase.FindAssets("t:TradeDatabase").FirstOrDefault();
            TradeDatabase db = null;
            if (!string.IsNullOrEmpty(dbGuid))
                db = AssetDatabase.LoadAssetAtPath<TradeDatabase>(AssetDatabase.GUIDToAssetPath(dbGuid));

            var existingIds = db != null ? new HashSet<string>(db.allItems.Where(x => x != null).Select(x => x.itemId)) : new HashSet<string>();
            var errors = new List<string>();
            var seenIds = new HashSet<string>();

            for (int i = 0; i < cfg.items.Count; i++)
            {
                var item = cfg.items[i];
                if (item == null)
                {
                    errors.Add($"Item [{i}]: null entry");
                    continue;
                }
                if (string.IsNullOrEmpty(item.itemId))
                {
                    errors.Add($"Item [{i}]: empty itemId");
                    continue;
                }
                if (!seenIds.Add(item.itemId))
                    errors.Add($"Item [{i}]: duplicate itemId '{item.itemId}'");
                if (db != null && existingIds.Count > 0 && !existingIds.Contains(item.itemId))
                    errors.Add($"Item [{i}]: itemId '{item.itemId}' not found in TradeItemDatabase");
            }

            if (string.IsNullOrEmpty(cfg.locationId))
                errors.Add("locationId is empty");

            if (errors.Count == 0)
                Debug.Log($"[MarketConfigEditor] ✅ Validation passed: {cfg.locationId} ({cfg.items.Count} items)");
            else
            {
                Debug.LogWarning($"[MarketConfigEditor] ⚠️ {cfg.locationId}: {errors.Count} issues:");
                foreach (var e in errors) Debug.LogWarning($"  - {e}");
            }
        }

        private void DuplicateConfig(MarketConfig cfg)
        {
            string srcPath = AssetDatabase.GetAssetPath(cfg);
            string dir = System.IO.Path.GetDirectoryName(srcPath);
            string baseName = System.IO.Path.GetFileNameWithoutExtension(srcPath);
            string newPath = System.IO.Path.Combine(dir, $"{baseName}_Copy.asset");
            newPath = AssetDatabase.GenerateUniqueAssetPath(newPath);

            var clone = Instantiate(cfg);
            clone.locationId = cfg.locationId + "_COPY";
            clone.displayName = cfg.displayName + " (Copy)";
            AssetDatabase.CreateAsset(clone, newPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorGUIUtility.PingObject(clone);
            Debug.Log($"[MarketConfigEditor] Duplicated to: {newPath}");
        }
    }

    /// <summary>
    /// Окно массового добавления товаров из TradeItemDatabase.
    /// </summary>
    public class MassAddWindow : EditorWindow
    {
        private MarketConfig _target;
        private List<TradeItemDefinition> _allItems = new();
        private HashSet<int> _selected = new();
        private Vector2 _scroll;
        private string _filter = "";
        private float _defaultPrice = 10f;
        private int _defaultStock = 50;
        private float _defaultRegen = 0.02f;

        public static void Show(MarketConfig target)
        {
            var w = GetWindow<MassAddWindow>(true, "Mass Add Items");
            w._target = target;
            w.minSize = new Vector2(400, 500);
            w.LoadItems();
        }

        private void LoadItems()
        {
            _allItems.Clear();
            _selected.Clear();

            var dbGuid = AssetDatabase.FindAssets("t:TradeDatabase").FirstOrDefault();
            if (string.IsNullOrEmpty(dbGuid)) return;

            var db = AssetDatabase.LoadAssetAtPath<TradeDatabase>(AssetDatabase.GUIDToAssetPath(dbGuid));
            if (db == null) return;

            _allItems = db.allItems.Where(x => x != null).OrderBy(x => x.itemId).ToList();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField($"Add items to: {_target?.displayName ?? "???"}", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            // Defaults
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Default Price:", GUILayout.Width(90));
            _defaultPrice = EditorGUILayout.FloatField(_defaultPrice, GUILayout.Width(60));
            EditorGUILayout.LabelField("Stock:", GUILayout.Width(42));
            _defaultStock = EditorGUILayout.IntField(_defaultStock, GUILayout.Width(50));
            EditorGUILayout.LabelField("Regen:", GUILayout.Width(42));
            _defaultRegen = EditorGUILayout.FloatField(_defaultRegen, GUILayout.Width(50));
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(4);

            // Filter
            EditorGUILayout.BeginHorizontal();
            _filter = EditorGUILayout.TextField("Filter:", _filter);
            if (GUILayout.Button("Select All", GUILayout.Width(80)))
            {
                for (int i = 0; i < _allItems.Count; i++)
                    if (string.IsNullOrEmpty(_filter) || _allItems[i].itemId.ToLowerInvariant().Contains(_filter.ToLowerInvariant()))
                        _selected.Add(i);
            }
            if (GUILayout.Button("Deselect All", GUILayout.Width(80)))
                _selected.Clear();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(4);

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            for (int i = 0; i < _allItems.Count; i++)
            {
                var item = _allItems[i];
                if (!string.IsNullOrEmpty(_filter) && !item.itemId.ToLowerInvariant().Contains(_filter.ToLowerInvariant()))
                    continue;

                bool sel = _selected.Contains(i);
                bool newSel = EditorGUILayout.ToggleLeft($"{item.itemId}  ({item.displayName})", sel);
                if (newSel != sel)
                {
                    if (newSel) _selected.Add(i);
                    else _selected.Remove(i);
                }
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.Space(8);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Add Selected", GUILayout.Height(30)))
            {
                Undo.RecordObject(_target, "Mass Add Items");
                var existingIds = new HashSet<string>(_target.items.Where(x => x != null && !string.IsNullOrEmpty(x.itemId)).Select(x => x.itemId));

                foreach (int idx in _selected.OrderBy(x => x))
                {
                    var def = _allItems[idx];
                    if (existingIds.Contains(def.itemId)) continue; // skip duplicates

                    _target.items.Add(new MarketItemConfig
                    {
                        itemId = def.itemId,
                        definition = def,
                        basePrice = _defaultPrice,
                        initialStock = _defaultStock,
                        regenPerTick = _defaultRegen,
                        allowBuy = true,
                        allowSell = true
                    });
                    existingIds.Add(def.itemId);
                }

                EditorUtility.SetDirty(_target);
                Debug.Log($"[MassAdd] Added {_selected.Count} items to {_target.locationId}");
                Close();
            }
            if (GUILayout.Button("Cancel", GUILayout.Height(30)))
                Close();
            EditorGUILayout.EndHorizontal();
        }
    }
}
#endif
