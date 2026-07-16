#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using ProjectC.Trade.Config;

namespace ProjectC.Trade.Editor
{
    /// <summary>
    /// Кастомный редактор MarketZone — показывает MarketConfig INLINE,
    /// без необходимости переходить к .asset в Project View.
    /// </summary>
    [CustomEditor(typeof(Network.MarketZone))]
    public class MarketZoneEditor : UnityEditor.Editor
    {
        private string _searchFilter = "";
        private Vector2 _scrollPos;
        private float _bulkPricePct = 0f;
        private int _bulkStock = 50;

        private static bool _configFoldout = true;

        public override void OnInspectorGUI()
        {
            var zone = (Network.MarketZone)target;

            // === MarketConfig reference ===
            var configProp = serializedObject.FindProperty("_marketConfig");
            EditorGUILayout.PropertyField(configProp, new GUIContent("Market Config"));

            var cfg = configProp.objectReferenceValue as MarketConfig;
            if (cfg == null)
            {
                serializedObject.ApplyModifiedProperties();
                EditorGUILayout.HelpBox("Assign a MarketConfig to edit its items inline.", MessageType.Info);

                // Остальные поля зоны
                DrawZoneFields();
                return;
            }

            // === Inline MarketConfig editor ===
            EditorGUILayout.Space(4);
            _configFoldout = EditorGUILayout.Foldout(_configFoldout,
                $"📋 {cfg.displayName} ({cfg.locationId}) — {cfg.items?.Count ?? 0} items", true, EditorStyles.foldoutHeader);

            if (_configFoldout)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.Space(2);

                // --- Trade Mode ---
                var so = new SerializedObject(cfg);
                so.Update();

                EditorGUILayout.LabelField("Trade Mode", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(so.FindProperty("tradeMode"));
                var buyAnyProp = so.FindProperty("buyAnyItem");
                EditorGUILayout.PropertyField(buyAnyProp);
                if (buyAnyProp.boolValue)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(so.FindProperty("globalBuyPriceConfig"));
                    EditorGUI.indentLevel--;
                }

                // --- Commissions ---
                EditorGUILayout.LabelField("Commissions", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(so.FindProperty("sellCommission"));
                EditorGUILayout.PropertyField(so.FindProperty("buyCommission"));

                // --- Price Corridor ---
                EditorGUILayout.LabelField("Price Corridor", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(so.FindProperty("priceFloorRatio"));
                EditorGUILayout.PropertyField(so.FindProperty("priceCeilingRatio"));
                EditorGUILayout.PropertyField(so.FindProperty("decayHalfLifeSeconds"));
                EditorGUILayout.PropertyField(so.FindProperty("regenMultiplier"));

                // --- Bulk Actions ---
                EditorGUILayout.LabelField("Bulk Actions", EditorStyles.boldLabel);
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Clear All", GUILayout.Height(22)))
                {
                    Undo.RecordObject(cfg, "Clear Items");
                    cfg.items.Clear();
                    EditorUtility.SetDirty(cfg);
                }
                if (GUILayout.Button("Mass Add from DB", GUILayout.Height(22)))
                {
                    MassAddWindow.Show(cfg);
                }
                if (GUILayout.Button("Validate", GUILayout.Height(22)))
                {
                    ValidateConfig(cfg);
                }
                if (GUILayout.Button("📍 Ping Asset", GUILayout.Height(22)))
                {
                    EditorGUIUtility.PingObject(cfg);
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("% Price:", GUILayout.Width(50));
                _bulkPricePct = EditorGUILayout.FloatField(_bulkPricePct, GUILayout.Width(50));
                if (GUILayout.Button("Apply %", GUILayout.Width(60)))
                {
                    Undo.RecordObject(cfg, "Adjust Prices");
                    foreach (var item in cfg.items)
                        if (item != null) item.basePrice *= (1f + _bulkPricePct / 100f);
                    EditorUtility.SetDirty(cfg);
                }
                EditorGUILayout.LabelField("Stock:", GUILayout.Width(40));
                _bulkStock = EditorGUILayout.IntField(_bulkStock, GUILayout.Width(50));
                if (GUILayout.Button("Apply Stock", GUILayout.Width(80)))
                {
                    Undo.RecordObject(cfg, "Set Stock");
                    foreach (var item in cfg.items)
                        if (item != null) item.initialStock = _bulkStock;
                    EditorUtility.SetDirty(cfg);
                }
                EditorGUILayout.EndHorizontal();

                // --- Search ---
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Search:", GUILayout.Width(45));
                _searchFilter = EditorGUILayout.TextField(_searchFilter, GUILayout.Height(18));
                if (GUILayout.Button("✕", GUILayout.Width(24), GUILayout.Height(18)))
                    _searchFilter = "";
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.Space(2);

                // --- Items list ---
                _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos, GUILayout.MinHeight(100), GUILayout.MaxHeight(350));

                for (int i = 0; i < cfg.items.Count; i++)
                {
                    var item = cfg.items[i];
                    if (item == null) continue;

                    // Filter
                    if (!string.IsNullOrEmpty(_searchFilter))
                    {
                        string defName = item.definition != null ? item.definition.displayName : "";
                        if (!(item.itemId != null && item.itemId.ToLowerInvariant().Contains(_searchFilter.ToLowerInvariant())) &&
                            !(defName != null && defName.ToLowerInvariant().Contains(_searchFilter.ToLowerInvariant())))
                            continue;
                    }

                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    EditorGUILayout.BeginHorizontal();

                    // Buy/Sell indicators
                    GUI.color = item.allowBuy ? Color.green : Color.gray;
                    EditorGUILayout.LabelField("B", EditorStyles.boldLabel, GUILayout.Width(14));
                    GUI.color = item.allowSell ? Color.green : Color.gray;
                    EditorGUILayout.LabelField("S", EditorStyles.boldLabel, GUILayout.Width(14));
                    GUI.color = Color.white;

                    // itemId
                    string label = item.itemId;
                    if (item.definition != null && !string.IsNullOrEmpty(item.definition.displayName))
                        label = $"{item.itemId} ({item.definition.displayName})";
                    if (label.Length > 40) label = label.Substring(0, 40) + "...";
                    EditorGUILayout.LabelField(label, GUILayout.MinWidth(100));

                    // Price
                    EditorGUI.BeginChangeCheck();
                    float newPrice = EditorGUILayout.FloatField(item.basePrice, GUILayout.Width(55));
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(cfg, "Change Price");
                        item.basePrice = newPrice;
                        EditorUtility.SetDirty(cfg);
                    }

                    // Stock
                    EditorGUI.BeginChangeCheck();
                    int newStock = EditorGUILayout.IntField(item.initialStock, GUILayout.Width(40));
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(cfg, "Change Stock");
                        item.initialStock = newStock;
                        EditorUtility.SetDirty(cfg);
                    }

                    // Regen
                    EditorGUI.BeginChangeCheck();
                    float newRegen = EditorGUILayout.FloatField(item.regenPerTick, GUILayout.Width(38));
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(cfg, "Change Regen");
                        item.regenPerTick = newRegen;
                        EditorUtility.SetDirty(cfg);
                    }

                    // Buy/Sell toggles
                    EditorGUI.BeginChangeCheck();
                    bool nb = EditorGUILayout.ToggleLeft("Buy", item.allowBuy, GUILayout.Width(48));
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(cfg, "Toggle Buy");
                        item.allowBuy = nb;
                        EditorUtility.SetDirty(cfg);
                    }
                    EditorGUI.BeginChangeCheck();
                    bool ns = EditorGUILayout.ToggleLeft("Sell", item.allowSell, GUILayout.Width(50));
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(cfg, "Toggle Sell");
                        item.allowSell = ns;
                        EditorUtility.SetDirty(cfg);
                    }

                    GUILayout.Space(4);

                    // Remove
                    if (GUILayout.Button("✕", GUILayout.Width(24)))
                    {
                        Undo.RecordObject(cfg, "Remove Item");
                        cfg.items.RemoveAt(i);
                        EditorUtility.SetDirty(cfg);
                        break;
                    }

                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                }

                EditorGUILayout.EndScrollView();

                // Add single
                if (GUILayout.Button("+ Add Item", GUILayout.Height(20)))
                {
                    Undo.RecordObject(cfg, "Add Item");
                    cfg.items.Add(new MarketItemConfig());
                    EditorUtility.SetDirty(cfg);
                }

                EditorGUI.indentLevel--;
                so.ApplyModifiedProperties();
            }

            EditorGUILayout.Space(4);
            serializedObject.ApplyModifiedProperties();

            // Остальные поля зоны
            DrawZoneFields();
        }

        private void DrawZoneFields()
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Zone Settings", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_debugLog"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("tradeRadius"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("shipDockRadius"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("drawGizmos"));
        }

        private void ValidateConfig(MarketConfig cfg)
        {
            var dbGuid = AssetDatabase.FindAssets("t:TradeDatabase").FirstOrDefault();
            TradeDatabase db = null;
            if (!string.IsNullOrEmpty(dbGuid))
                db = AssetDatabase.LoadAssetAtPath<TradeDatabase>(AssetDatabase.GUIDToAssetPath(dbGuid));

            var existingIds = db != null
                ? new HashSet<string>(db.allItems.Where(x => x != null).Select(x => x.itemId))
                : new HashSet<string>();
            var seenIds = new HashSet<string>();
            var errors = new List<string>();

            for (int i = 0; i < cfg.items.Count; i++)
            {
                var item = cfg.items[i];
                if (item == null) { errors.Add($"[{i}]: null"); continue; }
                if (string.IsNullOrEmpty(item.itemId)) { errors.Add($"[{i}]: empty id"); continue; }
                if (!seenIds.Add(item.itemId)) errors.Add($"[{i}]: duplicate '{item.itemId}'");
                if (existingIds.Count > 0 && !existingIds.Contains(item.itemId))
                    errors.Add($"[{i}]: '{item.itemId}' not in DB");
            }

            if (string.IsNullOrEmpty(cfg.locationId))
                errors.Add("locationId is empty");

            if (errors.Count == 0)
                Debug.Log($"[MarketZoneEditor] ✅ {cfg.locationId}: {cfg.items.Count} items valid");
            else
            {
                Debug.LogWarning($"[MarketZoneEditor] ⚠️ {cfg.locationId}: {errors.Count} issues");
                foreach (var e in errors) Debug.LogWarning($"  - {e}");
            }
        }
    }
}
#endif
