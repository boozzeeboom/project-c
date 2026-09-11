using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectC.EditorTools.FloatingOrigin
{
    /// <summary>
    /// T-FO06O read-only identity census for loaded canonical scenes.
    /// It records stable GlobalObjectId + hierarchy path facts and never mutates scene state.
    /// </summary>
    public static class AuditGlobalMotionRebaseParticipantIdentities
    {
        private const string BootstrapScenePath = "Assets/_Project/Scenes/BootstrapScene.unity";
        private const string WorldScenePath = "Assets/_Project/Scenes/World/WorldScene_0_0.unity";
        private const string ReportPath = "docs/world/floatingorigin/06O_PARTICIPANT_IDENTITY_CENSUS.json";

        [Serializable]
        public sealed class Report
        {
            public string generatedAtUtc;
            public string scope;
            public string[] requiredScenes;
            public SceneRecord[] scenes;
            public ObjectRecord[] markers;
            public ObjectRecord[] shipRoots;
            public ObjectRecord[] shipDeckNav;
            public ObjectRecord[] cameras;
            public ObjectRecord[] networkObjects;
            public string[] inconclusive;
        }

        [Serializable]
        public sealed class SceneRecord
        {
            public string name;
            public string path;
            public bool isLoaded;
            public bool isDirty;
            public int rootCount;
            public string[] rootPaths;
        }

        [Serializable]
        public sealed class ObjectRecord
        {
            public string scenePath;
            public string gameObjectPath;
            public string globalObjectId;
            public string rootPath;
            public string rootGlobalObjectId;
            public string[] componentTypes;
            public string[] matchedRoles;
        }

        [MenuItem("ProjectC/World/Floating Origin/Audit Rebase Participant Identities (Read Only)")]
        public static void Execute()
        {
            Report report = Run();
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string absolutePath = Path.Combine(projectRoot, ReportPath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(absolutePath));
            File.WriteAllText(absolutePath, JsonUtility.ToJson(report, true));

            int loaded = report.scenes.Count(scene => scene.isLoaded);
            Debug.Log($"[T-FO06O] Identity census: scenes={loaded}/{report.requiredScenes.Length}; shipRoots={report.shipRoots.Length}; shipDeckNav={report.shipDeckNav.Length}; cameras={report.cameras.Length}; networkObjects={report.networkObjects.Length}; report={ReportPath}");
        }

        public static Report Run()
        {
            if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stable Edit Mode required.");

            var report = new Report
            {
                generatedAtUtc = DateTime.UtcNow.ToString("O"),
                scope = "Loaded canonical scenes only. Read-only identity census; no scene discovery, mutation, runtime or Play Mode.",
                requiredScenes = new[] { BootstrapScenePath, WorldScenePath },
                scenes = new SceneRecord[0],
                markers = new ObjectRecord[0],
                shipRoots = new ObjectRecord[0],
                shipDeckNav = new ObjectRecord[0],
                cameras = new ObjectRecord[0],
                networkObjects = new ObjectRecord[0],
                inconclusive = new[]
                {
                    "Source identity is GlobalObjectId + loaded hierarchy path, not a published manifest asset.",
                    "Camera ownership/history binding is not inferred from Camera component presence.",
                    "ShipDeckNav runtime registration and physics relation are not inferred from component presence.",
                    "NetworkObject ownership, spawn lifetime and runtime participant policy are not inferred from scene census."
                }
            };

            var sceneRecords = new List<SceneRecord>();
            var markers = new List<ObjectRecord>();
            var shipRoots = new List<ObjectRecord>();
            var shipDeckNav = new List<ObjectRecord>();
            var cameras = new List<ObjectRecord>();
            var networkObjects = new List<ObjectRecord>();

            for (int i = 0; i < report.requiredScenes.Length; i++)
            {
                string requiredPath = report.requiredScenes[i];
                Scene scene = SceneManager.GetSceneByPath(requiredPath);
                if (!scene.IsValid())
                {
                    sceneRecords.Add(new SceneRecord
                    {
                        name = Path.GetFileNameWithoutExtension(requiredPath),
                        path = requiredPath,
                        isLoaded = false,
                        isDirty = false,
                        rootCount = 0,
                        rootPaths = new string[0]
                    });
                    continue;
                }

                GameObject[] roots = scene.GetRootGameObjects();
                sceneRecords.Add(new SceneRecord
                {
                    name = scene.name,
                    path = requiredPath,
                    isLoaded = scene.isLoaded,
                    isDirty = scene.isDirty,
                    rootCount = roots.Length,
                    rootPaths = roots.Select(GetPath).OrderBy(value => value, StringComparer.Ordinal).ToArray()
                });

                foreach (GameObject root in roots)
                {
                    Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
                    bool rootHasRigidbody = false;
                    bool rootHasNetworkObject = false;
                    bool rootHasShipDeckNav = false;
                    foreach (Transform transform in transforms)
                    {
                        Component[] components = transform.GetComponents<Component>();
                        foreach (Component component in components)
                        {
                            if (component == null) continue;
                            string typeName = component.GetType().Name;
                            rootHasRigidbody |= typeName == "Rigidbody";
                            rootHasNetworkObject |= typeName == "NetworkObject";
                            rootHasShipDeckNav |= typeName == "ShipDeckNav";
                        }
                    }

                    foreach (Transform transform in transforms)
                    {
                        GameObject gameObject = transform.gameObject;
                        Component[] components = gameObject.GetComponents<Component>();
                        var types = components.Where(component => component != null)
                            .Select(component => component.GetType().FullName)
                            .Distinct(StringComparer.Ordinal)
                            .OrderBy(value => value, StringComparer.Ordinal)
                            .ToArray();
                        var roles = new List<string>();
                        string name = gameObject.name;
                        if (name == "WorldRoot_0_0" || name == "Respawn_Default") roles.Add("WORLD_ANCHOR_OR_ROOT");
                        bool hasRigidbody = HasComponent(components, "Rigidbody");
                        bool hasNetworkObject = HasComponent(components, "NetworkObject");
                        bool hasDeckNav = HasComponent(components, "ShipDeckNav");
                        bool hasCamera = HasComponent(components, "Camera");
                        if (hasRigidbody && hasNetworkObject && transform == root.transform) roles.Add("SHIP_ROOT_CANDIDATE");
                        if (hasDeckNav) roles.Add("SHIP_DECK_NAV_CANDIDATE");
                        if (hasCamera) roles.Add("CAMERA_CANDIDATE");
                        if (hasNetworkObject) roles.Add("NETWORK_OBJECT_CANDIDATE");

                        if (name == "WorldRoot_0_0" || name == "Respawn_Default") markers.Add(CreateRecord(scene, gameObject, types, roles));
                        if (hasCamera) cameras.Add(CreateRecord(scene, gameObject, types, roles));
                        if (hasDeckNav) shipDeckNav.Add(CreateRecord(scene, gameObject, types, roles));
                        if (hasNetworkObject) networkObjects.Add(CreateRecord(scene, gameObject, types, roles));

                        if (transform == root.transform && rootHasRigidbody && rootHasNetworkObject)
                        {
                            if (rootHasShipDeckNav) roles.Add("SHIP_ROOT_HAS_DECK_NAV_IN_SUBTREE");
                            shipRoots.Add(CreateRecord(scene, root, types, roles));
                        }
                    }
                }
            }

            report.scenes = sceneRecords.ToArray();
            report.markers = DistinctRecords(markers);
            report.shipRoots = DistinctRecords(shipRoots);
            report.shipDeckNav = DistinctRecords(shipDeckNav);
            report.cameras = DistinctRecords(cameras);
            report.networkObjects = DistinctRecords(networkObjects);
            return report;
        }

        private static bool HasComponent(Component[] components, string typeName)
        {
            return components.Any(component => component != null && component.GetType().Name == typeName);
        }

        private static ObjectRecord CreateRecord(Scene scene, GameObject gameObject, string[] types, List<string> roles)
        {
            GameObject root = gameObject.transform.root.gameObject;
            return new ObjectRecord
            {
                scenePath = scene.path,
                gameObjectPath = GetPath(gameObject),
                globalObjectId = GlobalObjectId.GetGlobalObjectIdSlow(gameObject).ToString(),
                rootPath = GetPath(root),
                rootGlobalObjectId = GlobalObjectId.GetGlobalObjectIdSlow(root).ToString(),
                componentTypes = types,
                matchedRoles = roles.Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray()
            };
        }

        private static ObjectRecord[] DistinctRecords(List<ObjectRecord> records)
        {
            return records.GroupBy(record => record.scenePath + "|" + record.gameObjectPath, StringComparer.Ordinal)
                .Select(group => group.First())
                .OrderBy(record => record.scenePath, StringComparer.Ordinal)
                .ThenBy(record => record.gameObjectPath, StringComparer.Ordinal)
                .ToArray();
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
