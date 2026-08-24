// Project C: data-driven faction identity migration.
// Populates factionKey and wireId from the preserved legacy FactionId enum,
// then migrates authoring-time faction fields to FactionDefinition references.

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using ProjectC.Factions;
using ProjectC.Dialogue;
using ProjectC.Knowledge;

namespace ProjectC.Quests.Editor
{
    public static class FactionIdentityMigration
    {
        private const string FactionsFolder = "Assets/_Project/Resources/Data/Factions";
        private const string ProjectFolder = "Assets/_Project";

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

        [MenuItem("ProjectC/Factions/Tools/Migrate Authoring Faction References")]
        public static void MigrateAuthoringFactionReferences()
        {
            var legacyLookup = BuildLegacyLookup();
            int scanned = 0;
            int migrated = 0;
            int warnings = 0;

            try
            {
                AssetDatabase.StartAssetEditing();
                MigrateAssets<NpcDefinition>("t:NpcDefinition", MigrateNpcDefinition, legacyLookup, ref scanned, ref migrated, ref warnings);
                MigrateAssets<QuestDefinition>("t:QuestDefinition", MigrateQuestDefinition, legacyLookup, ref scanned, ref migrated, ref warnings);
                MigrateAssets<DialogTree>("t:DialogTree", MigrateDialogTree, legacyLookup, ref scanned, ref migrated, ref warnings);
                MigrateAssets<KnowledgeLossConfig>("t:KnowledgeLossConfig", MigrateKnowledgeLossConfig, legacyLookup, ref scanned, ref migrated, ref warnings);
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[FactionIdentityMigration] Authoring references complete: scanned={scanned}, migrated={migrated}, warnings={warnings}.");
        }

        private static Dictionary<int, FactionDefinition> BuildLegacyLookup()
        {
            var lookup = new Dictionary<int, FactionDefinition>();
            var guids = AssetDatabase.FindAssets("t:FactionDefinition", new[] { FactionsFolder });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var definition = AssetDatabase.LoadAssetAtPath<FactionDefinition>(path);
                if (definition == null || definition.factionId == FactionId.None) continue;
                lookup[(int)definition.factionId] = definition;
            }
            return lookup;
        }

        private static void MigrateAssets<T>(
            string filter,
            Func<SerializedObject, Dictionary<int, FactionDefinition>, bool> migrate,
            Dictionary<int, FactionDefinition> legacyLookup,
            ref int scanned,
            ref int migrated,
            ref int warnings) where T : UnityEngine.Object
        {
            var guids = AssetDatabase.FindAssets(filter, new[] { ProjectFolder });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var asset = AssetDatabase.LoadAssetAtPath<T>(path);
                if (asset == null) continue;

                scanned++;
                var serialized = new SerializedObject(asset);
                bool changed = migrate(serialized, legacyLookup);
                if (changed)
                {
                    serialized.ApplyModifiedProperties();
                    EditorUtility.SetDirty(asset);
                    migrated++;
                }
            }
        }

        private static bool MigrateNpcDefinition(SerializedObject serialized, Dictionary<int, FactionDefinition> legacyLookup)
        {
            bool changed = MigrateRootReference(serialized, "factionRef", "faction", legacyLookup);
            changed |= MigrateArrayReferences(serialized.FindProperty("attitudeLinks"), "targetFactionRef", "targetFaction", legacyLookup);
            return changed;
        }

        private static bool MigrateQuestDefinition(SerializedObject serialized, Dictionary<int, FactionDefinition> legacyLookup)
        {
            bool changed = MigrateRootReference(serialized, "factionRef", "faction", legacyLookup);
            changed |= MigrateArrayReferences(serialized.FindProperty("prerequisites"), "factionRef", "factionParam", legacyLookup);

            var rewards = serialized.FindProperty("rewards");
            if (rewards != null)
                changed |= MigrateArrayReferences(rewards.FindPropertyRelative("reputation"), "factionRef", "faction", legacyLookup);

            var stages = serialized.FindProperty("stages");
            changed |= MigrateNestedArrayReferences(stages, "objectives", "targetFactionRef", "targetFaction", legacyLookup);
            changed |= MigrateNestedArrayReferences(stages, "onEnterActions", "factionRef", "factionParam", legacyLookup);
            changed |= MigrateNestedArrayReferences(stages, "onCompleteActions", "factionRef", "factionParam", legacyLookup);
            return changed;
        }

        private static bool MigrateDialogTree(SerializedObject serialized, Dictionary<int, FactionDefinition> legacyLookup)
        {
            bool changed = false;
            var nodes = serialized.FindProperty("nodes");
            if (nodes == null || !nodes.isArray) return false;

            for (int i = 0; i < nodes.arraySize; i++)
            {
                var node = nodes.GetArrayElementAtIndex(i);
                changed |= MigrateArrayReferences(node.FindPropertyRelative("onEnterActions"), "factionRef", "factionParam", legacyLookup);

                var edges = node.FindPropertyRelative("edges");
                if (edges == null || !edges.isArray) continue;
                for (int e = 0; e < edges.arraySize; e++)
                {
                    var edge = edges.GetArrayElementAtIndex(e);
                    changed |= MigrateInlineReference(edge.FindPropertyRelative("action"), "factionRef", "factionParam", legacyLookup);
                    changed |= MigrateInlineReference(edge.FindPropertyRelative("condition"), "factionRef", "factionParam", legacyLookup);
                    changed |= MigrateArrayReferences(edge.FindPropertyRelative("conditions"), "factionRef", "factionParam", legacyLookup);
                }
            }
            return changed;
        }

        private static bool MigrateKnowledgeLossConfig(SerializedObject serialized, Dictionary<int, FactionDefinition> legacyLookup)
        {
            var references = serialized.FindProperty("neverForgetFactionRefs");
            var legacy = serialized.FindProperty("neverForgetFactions");
            if (references == null || legacy == null || !references.isArray || !legacy.isArray) return false;

            bool changed = false;
            if (references.arraySize < legacy.arraySize)
            {
                references.arraySize = legacy.arraySize;
                changed = true;
            }

            for (int i = 0; i < legacy.arraySize; i++)
            {
                var reference = references.GetArrayElementAtIndex(i);
                if (reference.objectReferenceValue != null) continue;
                var legacyEntry = legacy.GetArrayElementAtIndex(i);
                var definition = ResolveLegacy(legacyEntry, legacyLookup);
                if (definition == null) continue;
                reference.objectReferenceValue = definition;
                changed = true;
            }
            return changed;
        }

        private static bool MigrateRootReference(
            SerializedObject serialized,
            string referenceName,
            string legacyName,
            Dictionary<int, FactionDefinition> legacyLookup)
        {
            return AssignReference(
                serialized.FindProperty(referenceName),
                serialized.FindProperty(legacyName),
                legacyLookup);
        }

        private static bool MigrateInlineReference(
            SerializedProperty container,
            string referenceName,
            string legacyName,
            Dictionary<int, FactionDefinition> legacyLookup)
        {
            if (container == null) return false;
            return AssignReference(
                container.FindPropertyRelative(referenceName),
                container.FindPropertyRelative(legacyName),
                legacyLookup);
        }

        private static bool MigrateArrayReferences(
            SerializedProperty array,
            string referenceName,
            string legacyName,
            Dictionary<int, FactionDefinition> legacyLookup)
        {
            if (array == null || !array.isArray) return false;
            bool changed = false;
            for (int i = 0; i < array.arraySize; i++)
            {
                changed |= MigrateInlineReference(array.GetArrayElementAtIndex(i), referenceName, legacyName, legacyLookup);
            }
            return changed;
        }

        private static bool MigrateNestedArrayReferences(
            SerializedProperty outerArray,
            string nestedArrayName,
            string referenceName,
            string legacyName,
            Dictionary<int, FactionDefinition> legacyLookup)
        {
            if (outerArray == null || !outerArray.isArray) return false;
            bool changed = false;
            for (int i = 0; i < outerArray.arraySize; i++)
            {
                var outerElement = outerArray.GetArrayElementAtIndex(i);
                changed |= MigrateArrayReferences(
                    outerElement.FindPropertyRelative(nestedArrayName),
                    referenceName,
                    legacyName,
                    legacyLookup);
            }
            return changed;
        }

        private static bool AssignReference(
            SerializedProperty reference,
            SerializedProperty legacy,
            Dictionary<int, FactionDefinition> legacyLookup)
        {
            if (reference == null || legacy == null || reference.objectReferenceValue != null)
                return false;
            var definition = ResolveLegacy(legacy, legacyLookup);
            if (definition == null) return false;
            reference.objectReferenceValue = definition;
            return true;
        }

        private static FactionDefinition ResolveLegacy(
            SerializedProperty legacy,
            Dictionary<int, FactionDefinition> legacyLookup)
        {
            if (legacy == null || legacy.propertyType != SerializedPropertyType.Enum)
                return null;
            int legacyId = legacy.enumValueIndex;
            return legacyLookup.TryGetValue(legacyId, out var definition) ? definition : null;
        }
    }
}
#endif
