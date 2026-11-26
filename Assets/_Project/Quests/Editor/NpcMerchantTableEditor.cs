// T-MERCH01: Кастомный инспектор для NpcMerchantTable — удобное редактирование личного прайс-листа NPC
// с превью автоматических цен, категорий, требований репутации и быстрым наполнением.

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using ProjectC.Items;

namespace ProjectC.Quests.Editor
{
    [CustomEditor(typeof(NpcMerchantTable))]
    public class NpcMerchantTableEditor : UnityEditor.Editor
    {
        private ItemData _quickAddItem;

        public override void OnInspectorGUI()
        {
            var table = (NpcMerchantTable)target;
            if (table == null) return;

            serializedObject.Update();

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("🛒 NPC Merchant Table (Личный прайс-лист NPC)", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "Задаёт индивидуальный набор Pickable Items для торговли через правый блок DialogWindow (клавиша E).",
                EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(4);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("shopTitle"), new GUIContent("Shop Title"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("priceMultiplier"), new GUIContent("Price Multiplier"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("allowPlayerSellToNpc"), new GUIContent("Allow Player Sell To NPC"));
            if (table.allowPlayerSellToNpc)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("sellBackPriceMultiplier"), new GUIContent("Sell-Back Multiplier"));
            }
            EditorGUILayout.PropertyField(serializedObject.FindProperty("enableReputationDiscount"), new GUIContent("Enable Reputation Discount"));
            if (table.enableReputationDiscount)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("maxReputationDiscountRatio"), new GUIContent("Max Discount Ratio"));
            }

            EditorGUILayout.Space(8);
            var itemsProp = serializedObject.FindProperty("items");
            EditorGUILayout.LabelField($"📦 Товары в ассортименте ({itemsProp.arraySize})", EditorStyles.boldLabel);

            // Быстрое добавление перетаскиванием предмета
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            _quickAddItem = (ItemData)EditorGUILayout.ObjectField("Быстро добавить предмет:", _quickAddItem, typeof(ItemData), false);
            if (_quickAddItem != null && GUILayout.Button("+ Добавить в список", GUILayout.Width(150)))
            {
                int idx = itemsProp.arraySize;
                itemsProp.arraySize++;
                var el = itemsProp.GetArrayElementAtIndex(idx);
                el.FindPropertyRelative("item").objectReferenceValue = _quickAddItem;
                el.FindPropertyRelative("customBuyPrice").intValue = 0;
                el.FindPropertyRelative("customSellPrice").intValue = 0;
                el.FindPropertyRelative("stock").intValue = -1;
                el.FindPropertyRelative("minNpcAttitude").intValue = -100;
                el.FindPropertyRelative("minFactionReputation").intValue = -100;
                _quickAddItem = null;
            }
            EditorGUILayout.EndHorizontal();

            for (int i = 0; i < itemsProp.arraySize; i++)
            {
                var el = itemsProp.GetArrayElementAtIndex(i);
                if (el == null) continue;

                var itemProp = el.FindPropertyRelative("item");
                var buyProp = el.FindPropertyRelative("customBuyPrice");
                var sellProp = el.FindPropertyRelative("customSellPrice");
                var stockProp = el.FindPropertyRelative("stock");
                var minAttProp = el.FindPropertyRelative("minNpcAttitude");
                var minRepProp = el.FindPropertyRelative("minFactionReputation");
                var reqQuestProp = el.FindPropertyRelative("requiredQuest");

                var itemRef = itemProp.objectReferenceValue as ItemData;
                int effBuy = MerchantPriceResolver.ResolveBuyPrice(
                    itemRef, buyProp.intValue, table.priceMultiplier, 25, 0f, 0f, false);
                int effSell = MerchantPriceResolver.ResolveSellPrice(
                    itemRef, buyProp.intValue, sellProp.intValue, table.sellBackPriceMultiplier, 25, effBuy);

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();
                string title = itemRef != null
                    ? $"#{i + 1}  {itemRef.itemName}  [{ItemTypeNames.GetDisplayName(itemRef.itemType)}]  —  Купить: {effBuy} CR / Скупка: {effSell} CR"
                    : $"#{i + 1}  (Пустой слот)";
                EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
                if (i > 0 && GUILayout.Button("▲", GUILayout.Width(24)))
                {
                    itemsProp.MoveArrayElement(i, i - 1);
                    break;
                }
                if (i < itemsProp.arraySize - 1 && GUILayout.Button("▼", GUILayout.Width(24)))
                {
                    itemsProp.MoveArrayElement(i, i + 1);
                    break;
                }
                if (GUILayout.Button("×", GUILayout.Width(24)))
                {
                    itemsProp.DeleteArrayElementAtIndex(i);
                    break;
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.PropertyField(itemProp, new GUIContent("Item (Pickable)"));
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PropertyField(buyProp, new GUIContent("Buy CR (0=auto)"));
                EditorGUILayout.PropertyField(sellProp, new GUIContent("Sell CR (0=auto)"));
                EditorGUILayout.PropertyField(stockProp, new GUIContent("Stock (-1=∞)"));
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PropertyField(minAttProp, new GUIContent("Min Attitude ❤"));
                EditorGUILayout.PropertyField(minRepProp, new GUIContent("Min Faction Rep ⚑"));
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.PropertyField(reqQuestProp, new GUIContent("Required Quest (optional)"));

                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("+ Add Empty Item Slot", GUILayout.Height(24)))
            {
                int idx = itemsProp.arraySize;
                itemsProp.arraySize++;
                var el = itemsProp.GetArrayElementAtIndex(idx);
                el.FindPropertyRelative("stock").intValue = -1;
                el.FindPropertyRelative("minNpcAttitude").intValue = -100;
                el.FindPropertyRelative("minFactionReputation").intValue = -100;
            }
            EditorGUILayout.EndHorizontal();

            serializedObject.ApplyModifiedProperties();
            if (GUI.changed)
                EditorUtility.SetDirty(table);
        }
    }
}
#endif
