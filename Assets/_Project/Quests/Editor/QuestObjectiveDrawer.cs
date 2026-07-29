// QuestObjectiveDrawer — контекстно-зависимый PropertyDrawer для QuestObjective.
// Показывает только поля, релевантные выбранному objectiveType.
// Паттерн: как DialogueConditionDrawer.
// T-QUEDIT v2: SceneAsset для ReachLocation, NpcDefinition для KillEntity.

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using ProjectC.Quests;
using ProjectC.Factions;
using ProjectC.Items;

namespace ProjectC.Quests.Editor
{
    [CustomPropertyDrawer(typeof(QuestObjective))]
    public class QuestObjectiveDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var typeProp = property.FindPropertyRelative("objectiveType");
            var type = (QuestObjectiveType)typeProp.enumValueIndex;

            float lineH = EditorGUIUtility.singleLineHeight;
            float y = position.y;
            float w = position.width;
            float halfW = w * 0.5f;

            // Always: objectiveId + objectiveType
            var idProp = property.FindPropertyRelative("objectiveId");
            var idRect = new Rect(position.x, y, halfW - 4, lineH);
            var typeRect = new Rect(position.x + halfW + 4, y, halfW - 4, lineH);
            EditorGUI.PropertyField(idRect, idProp, new GUIContent("ID"));
            EditorGUI.PropertyField(typeRect, typeProp, GUIContent.none);
            y += lineH + 2;

            // Description
            var descProp = property.FindPropertyRelative("description");
            EditorGUI.PropertyField(new Rect(position.x, y, w, lineH), descProp, new GUIContent("Desc"));
            y += lineH + 2;

            // Context-sensitive fields
            switch (type)
            {
                case QuestObjectiveType.TalkToNpc:
                    DrawNpcField(property, position, ref y, w, lineH, "targetNpc", "targetNpcId", "NPC");
                    DrawOptionalRequired(property, position, ref y, w, lineH);
                    break;

                case QuestObjectiveType.HaveItem:
                    DrawPickupItemField(property, position, ref y, w, lineH);
                    DrawIntField(property, "requiredQuantity", position, ref y, w, lineH, "Qty");
                    DrawOptionalRequired(property, position, ref y, w, lineH);
                    break;

                case QuestObjectiveType.DeliverItem:
                    DrawPickupItemField(property, position, ref y, w, lineH);
                    DrawNpcField(property, position, ref y, w, lineH, "targetNpc", "targetNpcId", "NPC");
                    DrawIntField(property, "requiredQuantity", position, ref y, w, lineH, "Qty");
                    DrawOptionalRequired(property, position, ref y, w, lineH);
                    break;

                case QuestObjectiveType.ReachLocation:
                    DrawSceneField(property, position, ref y, w, lineH);
                    EditorGUI.PropertyField(new Rect(position.x, y, w, lineH),
                        property.FindPropertyRelative("targetPosition"), new GUIContent("Position"));
                    y += lineH + 2;
                    EditorGUI.PropertyField(new Rect(position.x, y, w, lineH),
                        property.FindPropertyRelative("targetRadius"), new GUIContent("Radius (m)"));
                    y += lineH + 2;
                    DrawOptionalRequired(property, position, ref y, w, lineH);
                    break;

                case QuestObjectiveType.ReputationAtLeast:
                    EditorGUI.PropertyField(new Rect(position.x, y, w, lineH),
                        property.FindPropertyRelative("targetFaction"), new GUIContent("Faction"));
                    y += lineH + 2;
                    DrawIntField(property, "reputationValue", position, ref y, w, lineH, "Min Value");
                    DrawOptionalRequired(property, position, ref y, w, lineH);
                    break;

                case QuestObjectiveType.EventDriven:
                case QuestObjectiveType.WaitForEvent:
                    DrawStringField(property, "eventId", position, ref y, w, lineH, "Event ID");
                    DrawOptionalRequired(property, position, ref y, w, lineH);
                    break;

                case QuestObjectiveType.KillEntity:
                    DrawNpcField(property, position, ref y, w, lineH, "targetEntity", "targetEntityType", "Entity");
                    DrawIntField(property, "requiredQuantity", position, ref y, w, lineH, "Qty");
                    DrawOptionalRequired(property, position, ref y, w, lineH);
                    break;
            }

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var typeProp = property.FindPropertyRelative("objectiveType");
            var type = (QuestObjectiveType)typeProp.enumValueIndex;
            int lines = 2; // ID + description

            switch (type)
            {
                case QuestObjectiveType.TalkToNpc:
                    lines += 2; // Npc + optional/required (+maybe fallback)
                    if (property.FindPropertyRelative("targetNpc").objectReferenceValue == null) lines += 1;
                    break;
                case QuestObjectiveType.HaveItem:
                    lines += 3; // pickupItem + qty + optional/required
                    if (property.FindPropertyRelative("pickupItem").objectReferenceValue == null) lines += 1;
                    break;
                case QuestObjectiveType.DeliverItem:
                    lines += 4;
                    if (property.FindPropertyRelative("pickupItem").objectReferenceValue == null) lines += 1;
                    if (property.FindPropertyRelative("targetNpc").objectReferenceValue == null) lines += 1;
                    break;
                case QuestObjectiveType.ReachLocation:
                    lines += 4; // scene + position + radius + optional/required
                    if (property.FindPropertyRelative("targetSceneId").stringValue == "") lines += 0; // SceneAsset always fills string
                    break;
                case QuestObjectiveType.ReputationAtLeast:
                    lines += 3;
                    break;
                case QuestObjectiveType.EventDriven:
                case QuestObjectiveType.WaitForEvent:
                    lines += 2;
                    break;
                case QuestObjectiveType.KillEntity:
                    lines += 3; // entity + qty + optional/required
                    if (property.FindPropertyRelative("targetEntity").objectReferenceValue == null) lines += 1;
                    break;
                default:
                    lines += 1;
                    break;
            }

            return (EditorGUIUtility.singleLineHeight + 2) * lines;
        }

        // ── general helpers ──

        private static void DrawStringField(SerializedProperty property, string propName, Rect position, ref float y, float w, float h, string label)
        {
            EditorGUI.PropertyField(new Rect(position.x, y, w, h),
                property.FindPropertyRelative(propName), new GUIContent(label));
            y += h + 2;
        }

        private static void DrawIntField(SerializedProperty property, string propName, Rect position, ref float y, float w, float h, string label)
        {
            EditorGUI.PropertyField(new Rect(position.x, y, w, h),
                property.FindPropertyRelative(propName), new GUIContent(label));
            y += h + 2;
        }

        private static void DrawOptionalRequired(SerializedProperty property, Rect position, ref float y, float w, float h)
        {
            float halfW = w * 0.5f;
            EditorGUI.PropertyField(new Rect(position.x, y, halfW - 4, h),
                property.FindPropertyRelative("required"), new GUIContent("Required"));
            EditorGUI.PropertyField(new Rect(position.x + halfW + 4, y, halfW - 4, h),
                property.FindPropertyRelative("optional"), new GUIContent("Optional"));
            y += h + 2;
        }

        // ── drag-and-drop helpers ──

        private static void DrawPickupItemField(SerializedProperty property, Rect position, ref float y, float w, float h)
        {
            var pickupProp = property.FindPropertyRelative("pickupItem");
            EditorGUI.PropertyField(new Rect(position.x, y, w, h), pickupProp, new GUIContent("Item (drag .asset)"));
            y += h + 2;

            if (pickupProp.objectReferenceValue == null)
            {
                var fallbackProp = property.FindPropertyRelative("itemTradeItemId");
                EditorGUI.PropertyField(new Rect(position.x, y, w, h), fallbackProp, new GUIContent("  └ Item ID (string)"));
                y += h + 2;
            }
        }

        private static void DrawNpcField(SerializedProperty property, Rect position, ref float y, float w, float h,
            string refPropName, string stringPropName, string label)
        {
            var refProp = property.FindPropertyRelative(refPropName);
            EditorGUI.PropertyField(new Rect(position.x, y, w, h), refProp, new GUIContent($"{label} (drag .asset)"));
            y += h + 2;

            if (refProp.objectReferenceValue == null)
            {
                var fallbackProp = property.FindPropertyRelative(stringPropName);
                EditorGUI.PropertyField(new Rect(position.x, y, w, h), fallbackProp, new GUIContent($"  └ {label} ID (string)"));
                y += h + 2;
            }
        }

        private static void DrawSceneField(SerializedProperty property, Rect position, ref float y, float w, float h)
        {
            var sceneIdProp = property.FindPropertyRelative("targetSceneId");

            // Show SceneAsset ObjectField — writes scene name to targetSceneId
            SceneAsset currentScene = null;
            if (!string.IsNullOrEmpty(sceneIdProp.stringValue))
            {
                // Find scene asset by name
                var guids = AssetDatabase.FindAssets("t:SceneAsset");
                foreach (var g in guids)
                {
                    var path = AssetDatabase.GUIDToAssetPath(g);
                    var name = System.IO.Path.GetFileNameWithoutExtension(path);
                    if (name == sceneIdProp.stringValue)
                    {
                        currentScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
                        break;
                    }
                }
            }

            var newScene = (SceneAsset)EditorGUI.ObjectField(
                new Rect(position.x, y, w, h), new GUIContent("Scene (drag .unity)"), currentScene, typeof(SceneAsset), false);
            y += h + 2;

            if (newScene != null)
                sceneIdProp.stringValue = newScene.name;
            else if (currentScene == null && !string.IsNullOrEmpty(sceneIdProp.stringValue))
                sceneIdProp.stringValue = "";

            // Show fallback string
            if (newScene == null)
            {
                EditorGUI.PropertyField(new Rect(position.x, y, w, h), sceneIdProp, new GUIContent("  └ Scene ID (string)"));
                y += h + 2;
            }
        }
    }
}
#endif
