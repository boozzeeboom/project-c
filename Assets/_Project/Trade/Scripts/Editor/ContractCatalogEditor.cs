#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using ProjectC.Trade.Config;
using UnityEditor;
using UnityEngine;

namespace ProjectC.Trade.Editor
{
    /// <summary>
    /// Custom editor for ContractCatalog.
    /// Keeps catalog locations synchronized with MarketConfig assets without manual ID copying.
    /// </summary>
    [CustomEditor(typeof(ContractCatalog))]
    public sealed class ContractCatalogEditor : UnityEditor.Editor
    {
        private const string MarketConfigSearchPath = "Assets/_Project/Trade/Data/Markets";

        public override void OnInspectorGUI()
        {
            var catalog = (ContractCatalog)target;
            serializedObject.Update();
            DrawPropertiesExcluding(serializedObject, "m_Script");

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("MarketConfig synchronization", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                $"Scans MarketConfig assets under {MarketConfigSearchPath}. " +
                "Missing canonical location IDs are appended as disabled entries. " +
                "Enable a new location only after its route distances are configured.",
                MessageType.Info);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Scan MarketConfigs and add missing locations", GUILayout.Height(26)))
            {
                serializedObject.ApplyModifiedProperties();
                ScanMarketConfigs(catalog);
                serializedObject.Update();
            }

            if (GUILayout.Button("Validate Catalog", GUILayout.Height(26)))
            {
                serializedObject.ApplyModifiedProperties();
                ValidateCatalog(catalog);
                serializedObject.Update();
            }
            EditorGUILayout.EndHorizontal();

            serializedObject.ApplyModifiedProperties();
        }

        private static void ScanMarketConfigs(ContractCatalog catalog)
        {
            Undo.RecordObject(catalog, "Sync ContractCatalog locations");

            var guids = AssetDatabase.FindAssets("t:MarketConfig", new[] { MarketConfigSearchPath });
            var existingIds = new HashSet<string>();
            bool normalizedExistingIds = false;

            if (catalog.locations == null)
                catalog.locations = new List<ContractCatalog.LocationDefinition>();

            for (int i = 0; i < catalog.locations.Count; i++)
            {
                var location = catalog.locations[i];
                if (location == null) continue;

                string normalized = MarketConfigCollector.NormalizeLocationId(location.locationId);
                if (location.locationId != normalized)
                {
                    location.locationId = normalized;
                    normalizedExistingIds = true;
                }

                if (!string.IsNullOrEmpty(normalized))
                    existingIds.Add(normalized);
            }

            var discovered = new List<(string id, string path)>();
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var marketConfig = AssetDatabase.LoadAssetAtPath<MarketConfig>(path);
                if (marketConfig == null) continue;

                string locationId = MarketConfigCollector.NormalizeLocationId(marketConfig.locationId);
                if (!string.IsNullOrEmpty(locationId))
                    discovered.Add((locationId, path));
                else
                    Debug.LogWarning($"[ContractCatalogEditor] Skipped MarketConfig with empty locationId: {path}");
            }

            discovered.Sort((a, b) => string.CompareOrdinal(a.id, b.id));

            var missing = new List<string>();
            for (int i = 0; i < discovered.Count; i++)
            {
                var entry = discovered[i];
                if (!existingIds.Add(entry.id)) continue;

                catalog.locations.Add(new ContractCatalog.LocationDefinition
                {
                    locationId = entry.id,
                    enabled = false
                });
                missing.Add(entry.id);
            }

            if (missing.Count == 0 && !normalizedExistingIds)
            {
                EditorUtility.DisplayDialog(
                    "ContractCatalog",
                    $"No new MarketConfig locations found. Scanned {discovered.Count} assets.",
                    "OK");
                return;
            }

            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();

            string message = missing.Count == 0
                ? "Existing location IDs were normalized."
                : $"Added {missing.Count} location(s): {string.Join(", ", missing)}.\n\nNew entries are disabled until route distances are configured.";

            Debug.Log($"[ContractCatalogEditor] Scanned {discovered.Count} MarketConfig assets; added {missing.Count} locations to '{catalog.name}'.");
            EditorUtility.DisplayDialog("ContractCatalog synchronized", message, "OK");
        }

        private static void ValidateCatalog(ContractCatalog catalog)
        {
            if (catalog.Validate(out var errors))
            {
                Debug.Log($"[ContractCatalogEditor] '{catalog.name}' is valid.");
                EditorUtility.DisplayDialog("ContractCatalog", "Catalog is valid.", "OK");
                return;
            }

            string message = string.Join("\n", errors.Select(error => "• " + error));
            Debug.LogError($"[ContractCatalogEditor] '{catalog.name}' is invalid:\n{message}");
            EditorUtility.DisplayDialog("ContractCatalog validation failed", message, "OK");
        }
    }
}
#endif
