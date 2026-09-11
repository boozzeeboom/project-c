using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using ProjectC.World.FloatingOrigin.Network;

namespace ProjectC.EditorTools.FloatingOrigin
{
    /// <summary>
    /// T-FO06R read-only cross-audit of loaded NetworkObjects against the reviewed scene catalog.
    /// No scene, object, catalog or runtime state is mutated.
    /// </summary>
    public static class AuditGlobalMotionRebaseCatalogNetworkPolicies
    {
        private const string BootstrapScenePath = "Assets/_Project/Scenes/BootstrapScene.unity";
        private const string WorldScenePath = "Assets/_Project/Scenes/World/WorldScene_0_0.unity";
        private const string CatalogPath = "Assets/_Project/Prefabs/FloatingOrigin/GlobalMotionPilotSceneCatalog.asset";
        private const string ReportPath = "docs/world/floatingorigin/06R_CATALOG_NETWORK_POLICY_AUDIT.json";

        [Serializable]
        private sealed class AuditReport
        {
            public string generatedAtUtc;
            public string catalogPath;
            public string status;
            public SceneState[] scenes;
            public CatalogSummary catalog;
            public PolicyRecord[] records;
            public string[] inconclusive;
        }

        [Serializable]
        private sealed class SceneState
        {
            public string path;
            public bool isLoaded;
            public bool isDirty;
        }

        [Serializable]
        private sealed class CatalogSummary
        {
            public int entryCount;
            public int sceneCount;
            public int matchedEntries;
            public int missingEntries;
        }

        [Serializable]
        private sealed class PolicyRecord
        {
            public string scenePath;
            public string gameObjectPath;
            public string globalObjectId;
            public string rootPath;
            public string sourceId;
            public string catalogKey;
            public string ownership;
            public string treatment;
            public string poseKind;
            public bool spatial;
            public bool hasRigidbody;
            public bool isShipRoot;
            public string policyClass;
            public bool admitted;
            public string reviewNote;
        }

        [MenuItem("ProjectC/World/Floating Origin/Audit Catalog Network Policies (Read Only)")]
        public static void Execute()
        {
            AuditReport report = Run();
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string absolutePath = Path.Combine(projectRoot, ReportPath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(absolutePath));
            File.WriteAllText(absolutePath, JsonUtility.ToJson(report, true));

            int admitted = report.records.Count(record => record.admitted);
            int blocked = report.records.Length - admitted;
            Debug.Log($"[T-FO06R] Catalog/network audit: records={report.records.Length};matched={report.catalog.matchedEntries};missing={report.catalog.missingEntries};admitted={admitted};blocked={blocked};status={report.status};report={ReportPath}");
        }

        private static AuditReport Run()
        {
            if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stable Edit Mode required.");

            SceneState[] scenes = new[]
            {
                ReadSceneState(BootstrapScenePath),
                ReadSceneState(WorldScenePath)
            };
            if (scenes.Any(scene => !scene.isLoaded || scene.isDirty))
                throw new InvalidOperationException("Both canonical scenes must be loaded and clean.");

            GlobalMotionSceneCatalog catalogAsset = AssetDatabase.LoadAssetAtPath<GlobalMotionSceneCatalog>(CatalogPath);
            if (catalogAsset == null)
                throw new InvalidOperationException($"Catalog asset not found: {CatalogPath}");

            GlobalSceneCatalogData catalogData = catalogAsset.Data;
            if (catalogData == null || catalogData.entries == null)
                throw new InvalidOperationException("Catalog data or entries are missing.");

            var catalogByKey = catalogData.entries
                .Where(entry => entry != null)
                .GroupBy(entry => entry.sceneGuid + "|" + entry.sourceId, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Single(), StringComparer.Ordinal);

            var records = new List<PolicyRecord>();
            var shipRootPaths = new HashSet<string>(StringComparer.Ordinal);
            foreach (Scene scene in EnumerateCanonicalScenes())
            {
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
                    bool rootHasRigidbody = transforms.Any(transform => HasComponent(transform.gameObject, "Rigidbody"));
                    bool rootHasNetworkObject = transforms.Any(transform => HasComponent(transform.gameObject, "NetworkObject"));
                    if (rootHasRigidbody && rootHasNetworkObject && root.transform == root.transform.root)
                        shipRootPaths.Add(GetPath(root));

                    foreach (Transform transform in transforms)
                    {
                        GameObject gameObject = transform.gameObject;
                        if (!HasComponent(gameObject, "NetworkObject"))
                            continue;

                        GlobalSceneSourceMarker marker = gameObject.GetComponent<GlobalSceneSourceMarker>();
                        string scenePath = scene.path;
                        string gameObjectPath = GetPath(gameObject);
                        string globalObjectId = GlobalObjectId.GetGlobalObjectIdSlow(gameObject).ToString();
                        string rootPath = GetPath(root);
                        string sourceId = marker != null ? marker.SourceId : string.Empty;
                        string sceneGuid = AssetDatabase.AssetPathToGUID(scenePath);
                        string catalogKey = sceneGuid + "|" + sourceId;
                        GlobalSceneEntry entry = null;
                        catalogByKey.TryGetValue(catalogKey, out entry);

                        bool isShipRoot = transform == root.transform && rootHasRigidbody;
                        if (isShipRoot)
                            shipRootPaths.Add(rootPath);

                        bool hasRigidbody = HasComponent(gameObject, "Rigidbody");
                        string policyClass;
                        bool admitted = false;
                        if (marker == null)
                            policyClass = "BLOCKED_NO_SOURCE_MARKER";
                        else if (entry == null)
                            policyClass = "BLOCKED_CATALOG_ENTRY_MISSING";
                        else if (isShipRoot)
                            policyClass = entry.ownership == GlobalSceneOwnership.ShipOrRigidbodyRoot
                                ? "SHIP_ROOT_CATALOG_MATCH_REVIEW_REQUIRED"
                                : "BLOCKED_SHIP_ROOT_OWNERSHIP_MISMATCH";
                        else if (entry.ownership == GlobalSceneOwnership.BootstrapService ||
                                 entry.ownership == GlobalSceneOwnership.PersistentBootstrapService)
                            policyClass = "BOOTSTRAP_SERVICE_EXPLICIT_NONSPATIAL_REVIEW";
                        else if (entry.ownership == GlobalSceneOwnership.SceneOwnedNetworkGameplay)
                            policyClass = "NETWORK_GAMEPLAY_ROOT_EXPLICIT_REVIEW_REQUIRED";
                        else
                            policyClass = "BLOCKED_UNSPECIFIED_OWNERSHIP";

                        records.Add(new PolicyRecord
                        {
                            scenePath = scenePath,
                            gameObjectPath = gameObjectPath,
                            globalObjectId = globalObjectId,
                            rootPath = rootPath,
                            sourceId = sourceId,
                            catalogKey = catalogKey,
                            ownership = entry != null ? entry.ownership.ToString() : string.Empty,
                            treatment = entry != null ? entry.treatment.ToString() : string.Empty,
                            poseKind = entry != null ? entry.poseKind.ToString() : string.Empty,
                            spatial = entry != null && entry.spatial,
                            hasRigidbody = hasRigidbody,
                            isShipRoot = isShipRoot,
                            policyClass = policyClass,
                            admitted = admitted,
                            reviewNote = entry != null ? entry.reviewNote : string.Empty
                        });
                    }
                }
            }

            records = records
                .OrderBy(record => record.scenePath, StringComparer.Ordinal)
                .ThenBy(record => record.gameObjectPath, StringComparer.Ordinal)
                .ToList();

            int matchedEntries = records.Count(record => !string.IsNullOrEmpty(record.sourceId) && !string.IsNullOrEmpty(record.ownership));
            return new AuditReport
            {
                generatedAtUtc = DateTime.UtcNow.ToString("O"),
                catalogPath = CatalogPath,
                status = "READ_ONLY_POLICY_CROSS_AUDIT;LIVE_NETWORK_ADMISSION_NOT_CREATED",
                scenes = scenes,
                catalog = new CatalogSummary
                {
                    entryCount = catalogData.entries.Length,
                    sceneCount = catalogData.scenes != null ? catalogData.scenes.Length : 0,
                    matchedEntries = matchedEntries,
                    missingEntries = records.Count - matchedEntries
                },
                records = records.ToArray(),
                inconclusive = new[]
                {
                    "Catalog ownership/treatment is reviewed source data, not proof of runtime ownership or lifetime.",
                    "No NetworkObject is admitted automatically into NETWORK_GAMEPLAY_ROOT.",
                    "ShipDeckNav runtime registration, passenger provenance, camera ownership and NGO ordering remain outside this audit.",
                    "A catalog match does not prove Apply/Rebuild/Validate/Publish or native rollback readiness."
                }
            };
        }

        private static SceneState ReadSceneState(string path)
        {
            Scene scene = SceneManager.GetSceneByPath(path);
            return new SceneState
            {
                path = path,
                isLoaded = scene.IsValid() && scene.isLoaded,
                isDirty = scene.IsValid() && scene.isDirty
            };
        }

        private static IEnumerable<Scene> EnumerateCanonicalScenes()
        {
            Scene bootstrap = SceneManager.GetSceneByPath(BootstrapScenePath);
            Scene world = SceneManager.GetSceneByPath(WorldScenePath);
            yield return bootstrap;
            yield return world;
        }

        private static bool HasComponent(GameObject gameObject, string typeName)
        {
            return gameObject.GetComponents<Component>().Any(component => component != null && component.GetType().Name == typeName);
        }

        private static string GetPath(GameObject gameObject)
        {
            var names = new List<string>();
            Transform current = gameObject.transform;
            while (current != null)
            {
                names.Add(current.name);
                current = current.parent;
            }
            names.Reverse();
            return string.Join("/", names);
        }
    }
}
