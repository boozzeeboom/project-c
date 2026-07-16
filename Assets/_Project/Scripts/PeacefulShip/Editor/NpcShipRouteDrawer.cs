#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using ProjectC.Docking.Network; // DockStationController
using ProjectC.PeacefulShip.Core;

namespace ProjectC.PeacefulShip.EditorTools
{
    /// <summary>
    /// PropertyDrawer for NpcShipRoute — replaces raw string IDs with dropdown
    /// of DockStationController.LocationId values from the open scene.
    /// Shows validation warning when an ID doesn't match any station in the scene.
    /// </summary>
    [CustomPropertyDrawer(typeof(NpcShipRoute))]
    public class NpcShipRouteDrawer : PropertyDrawer
    {
        private static List<DockStationController> _cachedStations;
        private static double _lastCacheTime;
        private const double CacheTTL = 3.0;

        private static List<DockStationController> GetSceneStations()
        {
            double now = EditorApplication.timeSinceStartup;
            if (_cachedStations != null && (now - _lastCacheTime) < CacheTTL)
                return _cachedStations;

            _cachedStations = Object.FindObjectsByType<DockStationController>(
                FindObjectsInactive.Include).ToList();
            _lastCacheTime = now;
            return _cachedStations;
        }

        private static string[] GetLocationOptions()
        {
            var stations = GetSceneStations();
            var ids = stations
                .Select(s => s.LocationId)
                .Where(id => !string.IsNullOrEmpty(id))
                .Distinct()
                .OrderBy(id => id)
                .ToList();
            ids.Insert(0, "(none)");
            return ids.ToArray();
        }

        private static HashSet<string> GetValidIds()
        {
            return new HashSet<string>(
                GetSceneStations()
                    .Select(s => s.LocationId)
                    .Where(id => !string.IsNullOrEmpty(id)));
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            // 6 rows + optional warning row
            var fromProp = property.FindPropertyRelative("fromLocationId");
            var toProp = property.FindPropertyRelative("toLocationId");
            var validIds = GetValidIds();

            bool hasWarning = false;
            if (!string.IsNullOrEmpty(fromProp.stringValue) && !validIds.Contains(fromProp.stringValue))
                hasWarning = true;
            if (!string.IsNullOrEmpty(toProp.stringValue) && !validIds.Contains(toProp.stringValue))
                hasWarning = true;

            int rows = 6 + (hasWarning ? 1 : 0);
            return (EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing) * rows
                   + EditorGUIUtility.standardVerticalSpacing;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var fromProp = property.FindPropertyRelative("fromLocationId");
            var toProp = property.FindPropertyRelative("toLocationId");
            var dwellProp = property.FindPropertyRelative("dwellTimeSec");
            var flightProp = property.FindPropertyRelative("flightDurationSec");
            var dwellRandMinProp = property.FindPropertyRelative("dwellRandomAddMinSec");
            var dwellRandMaxProp = property.FindPropertyRelative("dwellRandomAddMaxSec");
            var classProp = property.FindPropertyRelative("preferredShipClass");
            var demandProp = property.FindPropertyRelative("demandCategory");

            string[] locationOptions = GetLocationOptions();
            var validIds = GetValidIds();

            float lineH = EditorGUIUtility.singleLineHeight;
            float sp = EditorGUIUtility.standardVerticalSpacing;
            float y = position.y;

            // ── Route header ──
            int idx = GetArrayIndex(property);
            EditorGUI.LabelField(new Rect(position.x, y, position.width, lineH),
                $"◆ Route [{idx}]", EditorStyles.boldLabel);
            y += lineH + sp;

            // ── From / To dropdowns ──
            float colW = (position.width - 12f) / 2f;

            int fromIdx = System.Array.IndexOf(locationOptions, fromProp.stringValue);
            if (fromIdx < 0) fromIdx = 0;
            var fromRect = new Rect(position.x, y, 38, lineH);
            EditorGUI.LabelField(fromRect, "From");
            var fromPopupRect = new Rect(position.x + 40, y, colW - 40, lineH);
            int newFrom = EditorGUI.Popup(fromPopupRect, fromIdx, locationOptions);
            if (newFrom != fromIdx)
                fromProp.stringValue = newFrom == 0 ? "" : locationOptions[newFrom];

            int toIdx = System.Array.IndexOf(locationOptions, toProp.stringValue);
            if (toIdx < 0) toIdx = 0;
            var toLabelRect = new Rect(position.x + colW + 12, y, 26, lineH);
            EditorGUI.LabelField(toLabelRect, "To");
            var toPopupRect = new Rect(position.x + colW + 40, y, colW - 40, lineH);
            int newTo = EditorGUI.Popup(toPopupRect, toIdx, locationOptions);
            if (newTo != toIdx)
                toProp.stringValue = newTo == 0 ? "" : locationOptions[newTo];
            y += lineH + sp;

            // ── Dwell / Flight ──
            EditorGUI.PropertyField(new Rect(position.x, y, colW, lineH),
                dwellProp, new GUIContent("Dwell base (s)"));
            EditorGUI.PropertyField(new Rect(position.x + colW + 12, y, colW, lineH),
                flightProp, new GUIContent("Flight (s)"));
            y += lineH + sp;

            // ── Random dwell add: +[min]..[max] s ──
            var randLabelRect = new Rect(position.x, y, 60, lineH);
            EditorGUI.LabelField(randLabelRect, "Random +");
            var randMinRect = new Rect(position.x + 62, y, (colW - 82) / 2f, lineH);
            EditorGUI.PropertyField(randMinRect, dwellRandMinProp, GUIContent.none);
            var dashRect = new Rect(randMinRect.xMax + 2, y, 12, lineH);
            EditorGUI.LabelField(dashRect, "..");
            var randMaxRect = new Rect(dashRect.xMax + 2, y, (colW - 82) / 2f + colW - 82 - (colW - 82) / 2f, lineH);
            EditorGUI.PropertyField(randMaxRect, dwellRandMaxProp, GUIContent.none);
            EditorGUI.LabelField(new Rect(randMaxRect.xMax + 6, y, 14, lineH), "s");
            y += lineH + sp;

            // ── Ship Class / Demand ──
            EditorGUI.PropertyField(new Rect(position.x, y, colW, lineH),
                classProp, new GUIContent("Ship Class"));
            EditorGUI.PropertyField(new Rect(position.x + colW + 12, y, colW, lineH),
                demandProp, new GUIContent("Demand"));
            y += lineH + sp;

            // ── Warning: ID not in scene ──
            if (!string.IsNullOrEmpty(fromProp.stringValue) && !validIds.Contains(fromProp.stringValue))
            {
                EditorGUI.HelpBox(new Rect(position.x, y, position.width, lineH),
                    $"⚠ '{fromProp.stringValue}' not found in scene stations", MessageType.Warning);
            }
            else if (!string.IsNullOrEmpty(toProp.stringValue) && !validIds.Contains(toProp.stringValue))
            {
                EditorGUI.HelpBox(new Rect(position.x, y, position.width, lineH),
                    $"⚠ '{toProp.stringValue}' not found in scene stations", MessageType.Warning);
            }

            EditorGUI.EndProperty();
        }

        private static int GetArrayIndex(SerializedProperty property)
        {
            string path = property.propertyPath;
            int br = path.LastIndexOf('[');
            if (br < 0) return 0;
            int er = path.LastIndexOf(']');
            if (er > br && int.TryParse(path.Substring(br + 1, er - br - 1), out int i))
                return i;
            return 0;
        }
    }
}
#endif
