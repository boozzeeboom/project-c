#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using ProjectC.PeacefulShip.Core;
using ProjectC.Trade;

namespace ProjectC.PeacefulShip.EditorTools
{
    /// <summary>
    /// Inspector drawer for NpcCargoTradeConfig.
    /// New configurations use a TradeItemDefinition asset reference;
    /// legacy string itemId remains available as a fallback for old assets.
    /// </summary>
    [CustomPropertyDrawer(typeof(NpcCargoTradeConfig))]
    public sealed class NpcCargoTradeConfigDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float lineHeight = EditorGUIUtility.singleLineHeight;
            float spacing = EditorGUIUtility.standardVerticalSpacing;
            return lineHeight * 4f + spacing * 3f;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var tradeItemProp = property.FindPropertyRelative("tradeItem");
            var itemIdProp = property.FindPropertyRelative("itemId");
            var quantityProp = property.FindPropertyRelative("desiredQuantity");
            var sellProp = property.FindPropertyRelative("sellOnArrival");
            var keepProp = property.FindPropertyRelative("maxKeepQuantity");

            float lineHeight = EditorGUIUtility.singleLineHeight;
            float spacing = EditorGUIUtility.standardVerticalSpacing;
            float y = position.y;

            if (tradeItemProp != null && itemIdProp != null && tradeItemProp.objectReferenceValue is TradeItemDefinition definition)
            {
                string resolvedId = definition != null ? definition.itemId ?? string.Empty : string.Empty;
                if (itemIdProp.stringValue != resolvedId)
                    itemIdProp.stringValue = resolvedId;
            }

            if (tradeItemProp != null)
            {
                EditorGUI.PropertyField(
                    new Rect(position.x, y, position.width, lineHeight),
                    tradeItemProp,
                    new GUIContent("Trade Item"));
                y += lineHeight + spacing;
            }

            if (itemIdProp != null)
            {
                bool hasTradeItem = tradeItemProp != null && tradeItemProp.objectReferenceValue != null;
                using (new EditorGUI.DisabledScope(hasTradeItem))
                {
                    EditorGUI.PropertyField(
                        new Rect(position.x, y, position.width, lineHeight),
                        itemIdProp,
                        new GUIContent(hasTradeItem ? "Resolved Item ID" : "Legacy Item ID"));
                }
                y += lineHeight + spacing;
            }

            if (quantityProp != null)
            {
                EditorGUI.PropertyField(
                    new Rect(position.x, y, position.width, lineHeight),
                    quantityProp,
                    new GUIContent("Desired Quantity"));
                y += lineHeight + spacing;
            }

            float halfWidth = (position.width - spacing) * 0.5f;
            if (sellProp != null)
            {
                EditorGUI.PropertyField(
                    new Rect(position.x, y, halfWidth, lineHeight),
                    sellProp,
                    new GUIContent("Sell on Arrival"));
            }

            if (keepProp != null)
            {
                EditorGUI.PropertyField(
                    new Rect(position.x + halfWidth + spacing, y, halfWidth, lineHeight),
                    keepProp,
                    new GUIContent("Max Keep"));
            }

            EditorGUI.EndProperty();
        }
    }
}
#endif
