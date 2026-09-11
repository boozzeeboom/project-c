using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ProjectC.EditorTools.FloatingOrigin
{
    /// <summary>
    /// T-FO06P read-only mapping from the T-FO06O census to reviewed candidate manifest IDs.
    /// This tool never discovers scenes, mutates Unity objects, or creates a live manifest.
    /// </summary>
    public static class BuildGlobalMotionRebaseParticipantMapping
    {
        private const string CensusPath = "docs/world/floatingorigin/06O_PARTICIPANT_IDENTITY_CENSUS.json";
        private const string ReportPath = "docs/world/floatingorigin/06P_PARTICIPANT_IDENTITY_MAPPING.json";
        private const string BootstrapScenePath = "Assets/_Project/Scenes/BootstrapScene.unity";
        private const string WorldScenePath = "Assets/_Project/Scenes/World/WorldScene_0_0.unity";

        [Serializable]
        private sealed class CensusReport
        {
            public SceneRecord[] scenes;
            public ObjectRecord[] markers;
            public ObjectRecord[] shipRoots;
            public ObjectRecord[] shipDeckNav;
            public ObjectRecord[] cameras;
            public ObjectRecord[] networkObjects;
        }

        [Serializable]
        private sealed class SceneRecord
        {
            public string path;
            public bool isLoaded;
            public bool isDirty;
        }

        [Serializable]
        private sealed class ObjectRecord
        {
            public string scenePath;
            public string gameObjectPath;
            public string globalObjectId;
            public string rootPath;
            public string rootGlobalObjectId;
            public string[] componentTypes;
            public string[] matchedRoles;
        }

        [Serializable]
        private sealed class MappingReport
        {
            public string generatedAtUtc;
            public string sourceCensusPath;
            public string status;
            public SceneSummary[] scenes;
            public BoundaryRecord[] boundaries;
            public ParticipantMapping[] fixedParticipants;
            public NetworkPolicyRecord[] networkPolicy;
            public string[] inconclusive;
        }

        [Serializable]
        private sealed class SceneSummary
        {
            public string path;
            public bool isLoaded;
            public bool isDirty;
        }

        [Serializable]
        private sealed class BoundaryRecord
        {
            public string participantId;
            public string kind;
            public string policy;
            public ObjectRecord source;
        }

        [Serializable]
        private sealed class ParticipantMapping
        {
            public string participantId;
            public string kind;
            public int ordinal;
            public string policy;
            public ObjectRecord source;
        }

        [Serializable]
        private sealed class NetworkPolicyRecord
        {
            public string policyClass;
            public bool admitted;
            public ObjectRecord source;
        }

        [MenuItem("ProjectC/World/Floating Origin/Build Reviewed Participant Mapping (Read Only)")]
        public static void Execute()
        {
            MappingReport report = Build();
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string absolutePath = Path.Combine(projectRoot, ReportPath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(absolutePath));
            File.WriteAllText(absolutePath, JsonUtility.ToJson(report, true));

            int fixedCount = report.fixedParticipants.Length;
            int networkCount = report.networkPolicy.Length;
            int admittedNetwork = report.networkPolicy.Count(item => item.admitted);
            Debug.Log($"[T-FO06P] Reviewed mapping: fixed={fixedCount};networkCandidates={networkCount};networkAdmitted={admittedNetwork};status={report.status};report={ReportPath}");
        }

        private static MappingReport Build()
        {
            if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stable Edit Mode required.");

            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string censusAbsolutePath = Path.Combine(projectRoot, CensusPath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(censusAbsolutePath))
                throw new FileNotFoundException("T-FO06O census report is missing.", censusAbsolutePath);

            CensusReport census = JsonUtility.FromJson<CensusReport>(File.ReadAllText(censusAbsolutePath));
            if (census == null)
                throw new InvalidOperationException("T-FO06O census report could not be parsed.");

            SceneRecord[] scenes = census.scenes ?? Array.Empty<SceneRecord>();
            ObjectRecord[] markers = census.markers ?? Array.Empty<ObjectRecord>();
            ObjectRecord[] shipRoots = (census.shipRoots ?? Array.Empty<ObjectRecord>())
                .OrderBy(record => record.gameObjectPath, StringComparer.Ordinal)
                .ToArray();
            ObjectRecord[] deckNav = (census.shipDeckNav ?? Array.Empty<ObjectRecord>())
                .OrderBy(record => record.gameObjectPath, StringComparer.Ordinal)
                .ToArray();
            ObjectRecord[] cameras = census.cameras ?? Array.Empty<ObjectRecord>();
            ObjectRecord[] networkObjects = (census.networkObjects ?? Array.Empty<ObjectRecord>())
                .OrderBy(record => record.scenePath, StringComparer.Ordinal)
                .ThenBy(record => record.gameObjectPath, StringComparer.Ordinal)
                .ToArray();

            RequireSceneState(scenes, BootstrapScenePath);
            RequireSceneState(scenes, WorldScenePath);
            RequireCount(shipRoots, 22, "ship roots");
            RequireCount(deckNav, 20, "ShipDeckNav candidates");
            RequireCount(cameras, 1, "camera candidates");

            ObjectRecord worldRoot = FindUnique(markers, WorldScenePath, "WorldRoot_0_0");
            ObjectRecord respawn = FindUnique(markers, WorldScenePath, "Respawn_Default");
            ObjectRecord camera = cameras[0];
            if (!string.Equals(camera.gameObjectPath, "MainCamera", StringComparison.Ordinal) ||
                !string.Equals(camera.scenePath, BootstrapScenePath, StringComparison.Ordinal))
                throw new InvalidOperationException("The census camera candidate is not BootstrapScene/MainCamera.");

            var fixedParticipants = new List<ParticipantMapping>();
            for (int i = 0; i < shipRoots.Length; i++)
            {
                fixedParticipants.Add(new ParticipantMapping
                {
                    participantId = $"SHIP_ROOT/{i + 1:00}",
                    kind = "SHIP_ROOT",
                    ordinal = i + 1,
                    policy = "REVIEWED_IDENTITY_CANDIDATE;NOT_LIVE_MANIFEST_ADMISSION",
                    source = shipRoots[i]
                });
            }

            for (int i = 0; i < deckNav.Length; i++)
            {
                int shipIndex = i + 2;
                if (!string.Equals(deckNav[i].gameObjectPath, shipRoots[shipIndex].gameObjectPath, StringComparison.Ordinal))
                    throw new InvalidOperationException($"ShipDeckNav pairing mismatch at ordinal {i + 1}: {deckNav[i].gameObjectPath} != {shipRoots[shipIndex].gameObjectPath}.");

                fixedParticipants.Add(new ParticipantMapping
                {
                    participantId = $"SHIP_DECK_NAV/{i + 1:00}",
                    kind = "SHIP_DECK_NAV",
                    ordinal = i + 1,
                    policy = "REVIEWED_IDENTITY_CANDIDATE;RUNTIME_REGISTRATION_UNVERIFIED",
                    source = deckNav[i]
                });
            }

            var boundaries = new[]
            {
                new BoundaryRecord
                {
                    participantId = "CITY_STATIC",
                    kind = "CITY_STATIC",
                    policy = "BOUNDARY_ONLY;DESCENDANT_ADMISSION_REQUIRES_REVIEWED_ENTRY",
                    source = worldRoot
                },
                new BoundaryRecord
                {
                    participantId = "WORLD_ANCHORS",
                    kind = "WORLD_ANCHORS",
                    policy = "EXPLICIT_ANCHOR_CANDIDATE;POSE_AND_RUNTIME_ROLE_UNVERIFIED",
                    source = respawn
                },
                new BoundaryRecord
                {
                    participantId = "CAMERA",
                    kind = "CAMERA",
                    policy = "IDENTITY_CANDIDATE;ACTIVE_OWNER_AND_HISTORY_UNVERIFIED",
                    source = camera
                }
            };

            string[] shipRootPaths = shipRoots.Select(record => record.gameObjectPath).ToArray();
            var networkPolicy = networkObjects.Select(record =>
            {
                bool isShipRoot = record.rootPath == record.gameObjectPath && shipRootPaths.Contains(record.gameObjectPath, StringComparer.Ordinal);
                bool isShipSubtree = shipRootPaths.Contains(record.rootPath, StringComparer.Ordinal) && !isShipRoot;
                string policyClass = isShipRoot
                    ? "SHIP_ROOT_COVERED_BY_FIXED_MAPPING"
                    : isShipSubtree
                        ? "SHIP_SUBTREE_REVIEW_REQUIRED"
                        : record.scenePath == BootstrapScenePath
                            ? "BOOTSTRAP_NETWORK_REVIEW_REQUIRED"
                            : "WORLD_NETWORK_REVIEW_REQUIRED";

                return new NetworkPolicyRecord
                {
                    policyClass = policyClass,
                    admitted = false,
                    source = record
                };
            }).ToArray();

            return new MappingReport
            {
                generatedAtUtc = DateTime.UtcNow.ToString("O"),
                sourceCensusPath = CensusPath,
                status = "REVIEWED_CANDIDATE_MAPPING;LIVE_MANIFEST_NOT_CREATED",
                scenes = scenes
                    .OrderBy(scene => scene.path, StringComparer.Ordinal)
                    .Select(scene => new SceneSummary { path = scene.path, isLoaded = scene.isLoaded, isDirty = scene.isDirty })
                    .ToArray(),
                boundaries = boundaries,
                fixedParticipants = fixedParticipants.ToArray(),
                networkPolicy = networkPolicy,
                inconclusive = new[]
                {
                    "Ordinal mapping is deterministic census-order mapping and still requires owner review before live manifest publication.",
                    "CITY_STATIC is a reviewed boundary candidate only; generic WorldRoot parenting is not participant admission.",
                    "WORLD_ANCHORS contains the reviewed Respawn_Default candidate, but runtime pose/ownership policy is not proven.",
                    "CAMERA identity is recorded, but active ownership, target binding and history continuity are not proven.",
                    "No NETWORK_GAMEPLAY_ROOT entry is admitted automatically; every non-ship NetworkObject remains policy-review required.",
                    "ShipDeckNav runtime registration, NavMesh origin and passenger provenance remain unverified."
                }
            };
        }

        private static void RequireSceneState(SceneRecord[] scenes, string path)
        {
            SceneRecord scene = scenes.SingleOrDefault(item => item.path == path);
            if (scene == null || !scene.isLoaded || scene.isDirty)
                throw new InvalidOperationException($"Required scene is missing, unloaded or dirty: {path}.");
        }

        private static void RequireCount<T>(T[] values, int expected, string label)
        {
            if (values.Length != expected)
                throw new InvalidOperationException($"Unexpected {label} count: {values.Length}; expected {expected}.");
        }

        private static ObjectRecord FindUnique(ObjectRecord[] records, string scenePath, string gameObjectPath)
        {
            ObjectRecord[] matches = records.Where(record => record.scenePath == scenePath && record.gameObjectPath == gameObjectPath).ToArray();
            if (matches.Length != 1)
                throw new InvalidOperationException($"Expected exactly one census record for {scenePath}:{gameObjectPath}, got {matches.Length}.");
            return matches[0];
        }
    }
}
