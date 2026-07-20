// T-FACTION-UNIFY Stage D: Migrate NpcFaction references → FactionDefinition.
// Menu: Tools → ProjectC → Migration → Migrate NpcFaction → FactionDefinition
//
// Replaces all NpcFaction ScriptableObject references with FactionDefinition
// equivalents in NpcSpawnerConfig assets and NpcSocialBrain components
// across prefabs and scenes.

#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using ProjectC.Factions;
using ProjectC.AI;

namespace ProjectC.Editor.Migration
{
    public static class FactionMigrationTool
    {
        [MenuItem("Tools/ProjectC/Migration/Migrate NpcFaction → FactionDefinition")]
        public static void Migrate()
        {
            int updatedSpawners = 0;
            int updatedBrains = 0;
            int skipped = 0;

            // ---- Step 1: Build mapping (NpcFaction string factionId → FactionDefinition) ----
            var mapping = BuildMapping();

            if (mapping.Count == 0)
            {
                Debug.LogWarning("[T-FACTION-UNIFY Migration] No FactionDefinition assets found. Run Stage C first.");
                return;
            }

            // ---- Step 2: Migrate NpcSpawnerConfig assets ----
            var spawnerGuids = AssetDatabase.FindAssets("t:NpcSpawnerConfig");
            foreach (var guid in spawnerGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var config = AssetDatabase.LoadAssetAtPath<NpcSpawnerConfig>(path);
                if (config == null) continue;

                var so = new SerializedObject(config);
                var factionProp = so.FindProperty("faction");
                if (factionProp == null) continue;

                var oldRef = factionProp.objectReferenceValue;
                if (oldRef == null) { skipped++; continue; }

                // If already a FactionDefinition, skip.
                if (oldRef is FactionDefinition) { skipped++; continue; }

                // Try to resolve via name matching.
                string oldName = oldRef.name; // e.g. "NpcFaction_bandits"
                FactionDefinition newFaction = null;

                // Direct name match
                foreach (var kvp in mapping)
                {
                    if (oldName.Contains(kvp.Key))
                    {
                        newFaction = kvp.Value;
                        break;
                    }
                }

                // Fallback: try matching by lowercased name
                if (newFaction == null)
                {
                    string lower = oldName.ToLowerInvariant();
                    foreach (var kvp in mapping)
                    {
                        if (lower.Contains(kvp.Key.ToLowerInvariant()))
                        {
                            newFaction = kvp.Value;
                            break;
                        }
                    }
                }

                if (newFaction != null)
                {
                    factionProp.objectReferenceValue = newFaction;
                    so.ApplyModifiedProperties();
                    EditorUtility.SetDirty(config);
                    updatedSpawners++;
                    Debug.Log($"[T-FACTION-UNIFY Migration] Spawner '{path}': {oldName} → {newFaction.name}");
                }
                else
                {
                    skipped++;
                    Debug.LogWarning($"[T-FACTION-UNIFY Migration] Spawner '{path}': no FactionDefinition match for '{oldName}'");
                }
            }

            // ---- Step 3: Migrate NpcSocialBrain in prefabs ----
            var prefabGuids = AssetDatabase.FindAssets("t:Prefab");
            foreach (var guid in prefabGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                // Only process project prefabs (not package content)
                if (!path.StartsWith("Assets/")) continue;

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;

                bool prefabModified = false;
                var brains = prefab.GetComponentsInChildren<NpcSocialBrain>(true);
                foreach (var brain in brains)
                {
                    var so = new SerializedObject(brain);
                    var factionProp = so.FindProperty("faction");
                    if (factionProp == null) continue;

                    var oldRef = factionProp.objectReferenceValue;
                    if (oldRef == null) continue;
                    if (oldRef is FactionDefinition) continue;

                    string oldName = oldRef.name;
                    FactionDefinition newFaction = FindMatch(mapping, oldName);
                    if (newFaction != null)
                    {
                        factionProp.objectReferenceValue = newFaction;
                        so.ApplyModifiedProperties();
                        prefabModified = true;
                        updatedBrains++;
                        Debug.Log($"[T-FACTION-UNIFY Migration] Prefab '{path}'/{brain.name}: {oldName} → {newFaction.name}");
                    }
                }

                if (prefabModified)
                    EditorUtility.SetDirty(prefab);
            }

            // ---- Step 4: Migrate NpcSocialBrain in open scenes ----
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded) continue;

                bool sceneModified = false;
                var roots = scene.GetRootGameObjects();
                foreach (var root in roots)
                {
                    var brains = root.GetComponentsInChildren<NpcSocialBrain>(true);
                    foreach (var brain in brains)
                    {
                        var so = new SerializedObject(brain);
                        var factionProp = so.FindProperty("faction");
                        if (factionProp == null) continue;

                        var oldRef = factionProp.objectReferenceValue;
                        if (oldRef == null) continue;
                        if (oldRef is FactionDefinition) continue;

                        string oldName = oldRef.name;
                        FactionDefinition newFaction = FindMatch(mapping, oldName);
                        if (newFaction != null)
                        {
                            factionProp.objectReferenceValue = newFaction;
                            so.ApplyModifiedProperties();
                            sceneModified = true;
                            updatedBrains++;
                            Debug.Log($"[T-FACTION-UNIFY Migration] Scene '{scene.name}'/{brain.name}: {oldName} → {newFaction.name}");
                        }
                    }
                }

                if (sceneModified)
                    EditorSceneManager.MarkSceneDirty(scene);
            }

            // ---- Save ----
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[T-FACTION-UNIFY Migration] Done. Updated: {updatedSpawners} spawner configs, {updatedBrains} brains. Skipped: {skipped}.");
        }

        private static Dictionary<string, FactionDefinition> BuildMapping()
        {
            var mapping = new Dictionary<string, FactionDefinition>();

            // Map from lowercase factionId strings (from old NpcFaction) to FactionDefinition assets.
            var guids = AssetDatabase.FindAssets("t:FactionDefinition");
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var fd = AssetDatabase.LoadAssetAtPath<FactionDefinition>(path);
                if (fd == null) continue;

                // Map by FactionId enum name (lowercase)
                string key = fd.factionId.ToString().ToLowerInvariant();
                mapping[key] = fd;

                // Also map by asset name (without "Faction_" prefix)
                string assetName = fd.name;
                if (assetName.StartsWith("Faction_"))
                    mapping[assetName.Substring(8).ToLowerInvariant()] = fd;
            }

            return mapping;
        }

        private static FactionDefinition FindMatch(Dictionary<string, FactionDefinition> mapping, string oldName)
        {
            string lower = oldName.ToLowerInvariant();

            // Try exact match first.
            foreach (var kvp in mapping)
            {
                if (lower == kvp.Key) return kvp.Value;
            }

            // Try substring match.
            foreach (var kvp in mapping)
            {
                if (lower.Contains(kvp.Key) || kvp.Key.Contains(lower))
                    return kvp.Value;
            }

            return null;
        }
    }
}
#endif
