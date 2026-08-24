// Project C: FactionDefinition authoring inspector.
// New faction identity is authored as factionKey + wireId; factionId is legacy read-only.

#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
using ProjectC.Factions;

namespace ProjectC.Quests.Editor
{
    [CustomEditor(typeof(FactionDefinition))]
    public class FactionDefinitionEditor : UnityEditor.Editor
    {
        private const string FactionsFolder = "Assets/_Project/Resources/Data/Factions";

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.LabelField("Identity", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("factionId"),
                    new GUIContent("Legacy Faction ID"));
            }
            EditorGUILayout.PropertyField(serializedObject.FindProperty("factionKey"),
                new GUIContent("Faction Key"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("wireId"),
                new GUIContent("Wire ID"));
            DrawIdentityValidation();

            EditorGUILayout.Space(4);
            DrawPropertiesExcluding(serializedObject, "m_Script", "factionId", "factionKey", "wireId");
            serializedObject.ApplyModifiedProperties();
        }

        private void DrawIdentityValidation()
        {
            var definition = target as FactionDefinition;
            if (definition == null) return;

            if (string.IsNullOrWhiteSpace(definition.factionKey))
                EditorGUILayout.HelpBox("Faction Key is required for a data-driven faction.", MessageType.Error);

            if (definition.wireId <= 0 || definition.wireId > byte.MaxValue)
                EditorGUILayout.HelpBox("Wire ID must be in the range 1..255.", MessageType.Error);
            else if (definition.factionId == FactionId.None && definition.wireId < 16)
                EditorGUILayout.HelpBox("New factions must use wire ID 16..255. IDs 1..15 are reserved for legacy factions.", MessageType.Error);

            if (definition.factionId != FactionId.None && definition.wireId > 0 &&
                definition.wireId != (int)definition.factionId)
                EditorGUILayout.HelpBox("Wire ID must match the legacy Faction ID on legacy assets.", MessageType.Error);

            if (!string.IsNullOrWhiteSpace(definition.factionKey))
            {
                var guids = AssetDatabase.FindAssets("t:FactionDefinition", new[] { FactionsFolder });
                for (int i = 0; i < guids.Length; i++)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                    var other = AssetDatabase.LoadAssetAtPath<FactionDefinition>(path);
                    if (other == null || other == definition) continue;
                    if (string.Equals(other.EffectiveFactionKey, definition.factionKey.Trim(), StringComparison.Ordinal))
                    {
                        EditorGUILayout.HelpBox($"Duplicate factionKey with '{other.name}'.", MessageType.Error);
                        break;
                    }
                    if (definition.wireId > 0 && other.EffectiveWireId == definition.wireId)
                    {
                        EditorGUILayout.HelpBox($"Duplicate wireId with '{other.name}'.", MessageType.Error);
                        break;
                    }
                }
            }
        }
    }
}
#endif
