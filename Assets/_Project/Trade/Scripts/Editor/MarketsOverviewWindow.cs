#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using ProjectC.Trade.Config;

namespace ProjectC.Trade.Editor
{
    /// <summary>
    /// Окно обзора всех рынков: таблица «itemId × locationId» с ценами.
    /// Цветовая шкала: зелёный = дёшево, красный = дорого.
    /// </summary>
    public class MarketsOverviewWindow : EditorWindow
    {
        private Vector2 _scroll;
        private List<MarketConfig> _markets = new();
        private List<string> _allItemIds = new();

        [MenuItem("Tools/ProjectC/Trade/Markets Overview")]
        public static void ShowWindow()
        {
            var w = GetWindow<MarketsOverviewWindow>("Markets Overview");
            w.minSize = new Vector2(800, 400);
            w.RefreshData();
        }

        private void RefreshData()
        {
            _markets.Clear();
            _allItemIds.Clear();

            var guids = AssetDatabase.FindAssets("t:MarketConfig");
            var itemIdSet = new HashSet<string>();

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var cfg = AssetDatabase.LoadAssetAtPath<MarketConfig>(path);
                if (cfg == null) continue;
                _markets.Add(cfg);

                foreach (var item in cfg.items)
                {
                    if (item != null && !string.IsNullOrEmpty(item.itemId))
                        itemIdSet.Add(item.itemId);
                }
            }

            _allItemIds = itemIdSet.OrderBy(x => x).ToList();
        }

        private void OnGUI()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Markets Overview ({_markets.Count} markets, {_allItemIds.Count} items)", EditorStyles.boldLabel);
            if (GUILayout.Button("Refresh", GUILayout.Width(80)))
                RefreshData();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(4);

            if (_markets.Count == 0)
            {
                EditorGUILayout.HelpBox("No MarketConfig assets found in project.", MessageType.Info);
                return;
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            // Header row: empty + market names
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            EditorGUILayout.LabelField("itemId", GUILayout.Width(200));
            foreach (var m in _markets)
            {
                string label = string.IsNullOrEmpty(m.displayName) ? m.locationId : m.displayName;
                EditorGUILayout.LabelField(label, GUILayout.Width(100));
            }
            EditorGUILayout.EndHorizontal();

            // Data rows
            foreach (var itemId in _allItemIds)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(itemId, GUILayout.Width(200));

                // Find min/max for color scale
                float minPrice = float.MaxValue;
                float maxPrice = float.MinValue;
                foreach (var m in _markets)
                {
                    var cfg = m.GetItemConfig(itemId);
                    if (cfg != null)
                    {
                        if (cfg.basePrice < minPrice) minPrice = cfg.basePrice;
                        if (cfg.basePrice > maxPrice) maxPrice = cfg.basePrice;
                    }
                }

                foreach (var m in _markets)
                {
                    var cfg = m.GetItemConfig(itemId);
                    var rect = EditorGUILayout.GetControlRect(GUILayout.Width(100));

                    if (cfg != null)
                    {
                        float t = (maxPrice > minPrice) ? (cfg.basePrice - minPrice) / (maxPrice - minPrice) : 0f;
                        Color bg = Color.Lerp(Color.green, Color.red, t);
                        var bgPrev = GUI.backgroundColor;
                        GUI.backgroundColor = bg;
                        EditorGUI.LabelField(rect, $"{cfg.basePrice:F0} CR");
                        GUI.backgroundColor = bgPrev;
                    }
                    else
                    {
                        GUI.color = Color.gray;
                        EditorGUI.LabelField(rect, "—");
                        GUI.color = Color.white;
                    }
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();
        }
    }
}
#endif
