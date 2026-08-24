// Project C: data-driven faction identity migration.
// Populates factionKey and wireId from the preserved legacy FactionId enum.

#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
using ProjectC.Factions;

namespace ProjectC.Quests.Editor
{
    public static class FactionIdentityMigration
    {
        private const string FactionsFolder = "Assets/_Project/Resources/Data/Factions";

        [MenuItem("ProjectC/Factions/Tools/Migrate Faction Assets")]
        public static void Execute()
        {
            string[] guids = AssetDatabase.FindAssets("t:FactionDefinition", new[] { FactionsFolder });
            int migrated = 0;
            int unchanged = 0;
            int warnings = 0;

            try
            {
                AssetDatabase.StartAssetEditing();

                for (int i = 0; i < guids.Length; i++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                    var definition = AssetDatabase.LoadAssetAtPath<FactionDefinition>(path);
                    if (definition == null) continue;

                    if (definition.factionId == FactionId.None)
                    {
                        Debug.LogWarning($"[FactionIdentityMigration] Skipping '{path}': legacy factionId is None.");
                        warnings++;
                        continue;
                    }

                    string expectedKey = definition.factionId.ToString();
                    int expectedWireId = (int)definition.factionId;
                    var serialized = new SerializedObject(definition);
                    var keyProperty = serialized.FindProperty("factionKey");
                    var wireIdProperty = serialized.FindProperty("wireId");

                    if (keyProperty == null || wireIdProperty == null)
                    {
                        Debug.LogError($"[FactionIdentityMigration] Missing identity fields on '{path}'.");
                        warnings++;
                        continue;
                    }

                    bool changed = false;

                    if (string.IsNullOrWhiteSpace(keyProperty.stringValue))
                    {
                        keyProperty.stringValue = expectedKey;
                        changed = true;
                    }
                    else if (!string.Equals(keyProperty.stringValue.Trim(), expectedKey, StringComparison.Ordinal))
                    {
                        Debug.LogError($"[FactionIdentityMigration] Key mismatch in '{path}': existing='{keyProperty.stringValue}', expected='{expectedKey}'.");
                        warnings++;
                    }

                    if (wireIdProperty.intValue == 0)
                    {
                        wireIdProperty.intValue = expectedWireId;
                        changed = true;
                    }
                    else if (wireIdProperty.intValue != expectedWireId)
                    {
                        Debug.LogError($"[FactionIdentityMigration] wireId mismatch in '{path}': existing={wireIdProperty.intValue}, expected={expectedWireId}.");
                        warnings++;
                    }

                    if (changed)
                    {
                        serialized.ApplyModifiedProperties();
                        EditorUtility.SetDirty(definition);
                        migrated++;
                    }
                    else
                    {
                        unchanged++;
                    }
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[FactionIdentityMigration] Complete: scanned={guids.Length}, migrated={migrated}, unchanged={unchanged}, warnings={warnings}.");
        }
    }
}
#endif
