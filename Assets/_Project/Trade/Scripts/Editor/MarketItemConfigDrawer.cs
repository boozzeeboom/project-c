#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using ProjectC.Trade.Config;

namespace ProjectC.Trade.Editor
{
    /// <summary>
    /// Компактный PropertyDrawer для MarketItemConfig.
    /// В свёрнутом виде: [buy✓] [sell✓] itemId | Stock:50 | Price:10CR | Regen:0.02
    /// В развёрнутом — все поля как обычно.
    /// </summary>
    [CustomPropertyDrawer(typeof(MarketItemConfig))]
    public class MarketItemConfigDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (!property.isExpanded)
                return EditorGUIUtility.singleLineHeight;

            // Expanded: itemId + definition + 6 fields + restrictions
            return EditorGUIUtility.singleLineHeight * 11 + EditorGUIUtility.standardVerticalSpacing * 10;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var itemIdProp = property.FindPropertyRelative("itemId");
            var definitionProp = property.FindPropertyRelative("definition");
            var basePriceProp = property.FindPropertyRelative("basePrice");
            var initialStockProp = property.FindPropertyRelative("initialStock");
            var regenPerTickProp = property.FindPropertyRelative("regenPerTick");
            var allowBuyProp = property.FindPropertyRelative("allowBuy");
            var allowSellProp = property.FindPropertyRelative("allowSell");
            var factionProp = property.FindPropertyRelative("factionRestriction");

            EditorGUI.BeginProperty(position, label, property);

            if (!property.isExpanded)
            {
                // Single line collapsed view
                var rect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
                var foldoutRect = new Rect(rect.x, rect.y, 14, rect.height);
                property.isExpanded = EditorGUI.Foldout(foldoutRect, property.isExpanded, GUIContent.none);

                float x = rect.x + 16f;

                // Buy/Sell indicators
                var buyRect = new Rect(x, rect.y, 24, rect.height);
                GUI.color = allowBuyProp.boolValue ? Color.green : Color.gray;
                GUI.Label(buyRect, "B", EditorStyles.boldLabel);
                x += 22f;

                // itemId
                string itemId = itemIdProp.stringValue;
                if (string.IsNullOrEmpty(itemId)) itemId = "(empty)";
                string stockInfo = $" | 📦{initialStockProp.intValue}";
                string priceInfo = $" | 💰{basePriceProp.floatValue:F0}CR";
                string regenInfo = $" | ⟳{regenPerTickProp.floatValue:F2}";

                var idRect = new Rect(x, rect.y, position.width - x - 10, rect.height);
                GUI.color = Color.white;
                EditorGUI.LabelField(idRect, $"{itemId}{stockInfo}{priceInfo}{regenInfo}");

                GUI.color = Color.white;
            }
            else
            {
                // Expanded: full fields
                var rect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
                property.isExpanded = EditorGUI.Foldout(new Rect(rect.x, rect.y, 14, rect.height), property.isExpanded, GUIContent.none);

                float y = rect.y + EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

                EditorGUI.PropertyField(new Rect(rect.x, y, position.width, rect.height), itemIdProp);
                y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

                EditorGUI.PropertyField(new Rect(rect.x, y, position.width, rect.height), definitionProp);
                y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

                EditorGUI.PropertyField(new Rect(rect.x, y, position.width, rect.height), basePriceProp);
                y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

                EditorGUI.PropertyField(new Rect(rect.x, y, position.width, rect.height), initialStockProp);
                y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

                EditorGUI.PropertyField(new Rect(rect.x, y, position.width, rect.height), regenPerTickProp);
                y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

                EditorGUI.PropertyField(new Rect(rect.x, y, position.width, rect.height), allowBuyProp);
                y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

                EditorGUI.PropertyField(new Rect(rect.x, y, position.width, rect.height), allowSellProp);
                y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

                EditorGUI.PropertyField(new Rect(rect.x, y, position.width, rect.height), factionProp);
            }

            EditorGUI.EndProperty();
        }
    }
}
#endif
