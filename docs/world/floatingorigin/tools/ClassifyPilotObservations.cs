using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Unity.Netcode;
using Unity.Netcode.Components;
using ProjectC.World.FloatingOrigin.Network;

/// <summary>
/// T-FO06K: classifies every catalog observation in the loaded pilot scenes against the executor's actual
/// policy, to establish how many can be catalogued at all in the current slice. Read-only.
/// Mirrors GlobalSceneExecutionPolicy.Validate and GlobalSceneNativeExecutor.UnsupportedOwnedSubtree.
/// </summary>
public static class ClassifyPilotObservations
{
    [Serializable] public sealed class Row
    {
        public string scene, objectPath, verdict, blockingComponents;
        public bool isRoot, isNetworkObject, canPreserveSpatial, canPreserveNonSpatial, canSceneNetworkObject;
    }
    [Serializable] public sealed class Report
    {
        public string date, verdict;
        public int observations, supported, unsupported;
        public int fitPreserveSpatial, fitPreserveNonSpatial, fitSceneNetworkObject;
        public List<Row> rows = new List<Row>();
        public List<string> blockingComponentSummary = new List<string>();
        public List<string> notes = new List<string>();
    }

    public static string Execute()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stable Edit Mode required.");

        var report = new Report
        {
            date = DateTime.Now.ToString("yyyy-MM-dd"),
            verdict = "Feasibility of cataloguing the pilot scenes with the executor as implemented"
        };
        var blockingCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);

        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded) continue;

            var candidates = new HashSet<GameObject>();
            foreach (var root in scene.GetRootGameObjects())
            {
                candidates.Add(root);
                foreach (var no in root.GetComponentsInChildren<NetworkObject>(true)) candidates.Add(no.gameObject);
            }

            foreach (var go in candidates)
            {
                var row = new Row
                {
                    scene = scene.path, objectPath = HierarchyPath(go.transform),
                    isRoot = go.transform.parent == null,
                    isNetworkObject = go.GetComponent<NetworkObject>() != null
                };

                var blockingSpatial = new List<string>();
                var blockingNonSpatial = new List<string>();
                bool unsupportedSpatial = UnsupportedOwnedSubtree(go, true, blockingSpatial);
                bool unsupportedNonSpatial = UnsupportedOwnedSubtree(go, false, blockingNonSpatial);

                // PreserveContent requires a non-network source; SceneNetworkObject requires a network source.
                row.canPreserveSpatial = !row.isNetworkObject && !unsupportedSpatial;
                row.canPreserveNonSpatial = !row.isNetworkObject && !unsupportedNonSpatial;
                // Spatial + NetworkObject is explicitly rejected, so a scene network source must be non-spatial.
                row.canSceneNetworkObject = row.isNetworkObject && !unsupportedNonSpatial;

                if (row.canPreserveSpatial) { report.fitPreserveSpatial++; row.verdict = "PreserveContent(spatial)"; }
                else if (row.canPreserveNonSpatial) { report.fitPreserveNonSpatial++; row.verdict = "PreserveContent(nonSpatial)"; }
                else if (row.canSceneNetworkObject) { report.fitSceneNetworkObject++; row.verdict = "SceneNetworkObject(nonSpatial)"; }
                else
                {
                    row.verdict = "UNSUPPORTED";
                    var reasons = row.isNetworkObject ? blockingNonSpatial : blockingSpatial;
                    var extra = row.isNetworkObject ? new List<string>() : blockingNonSpatial;
                    var all = new SortedSet<string>(StringComparer.Ordinal);
                    foreach (string r in reasons) all.Add(r);
                    foreach (string r in extra) all.Add(r);
                    row.blockingComponents = string.Join(", ", all);
                    foreach (string r in all)
                    {
                        blockingCounts.TryGetValue(r, out int count);
                        blockingCounts[r] = count + 1;
                    }
                }

                if (row.verdict == "UNSUPPORTED") report.unsupported++; else report.supported++;
                report.observations++;
                report.rows.Add(row);
            }
        }

        foreach (var pair in blockingCounts) report.blockingComponentSummary.Add(pair.Value + " x " + pair.Key);

        report.notes.Add("Exclude and ReplaceWithNetworkPrefab are rejected by the policy as needing an extended executor, so they are not options here.");
        report.notes.Add("Spatial sources must additionally be inactive before start and staged in an explicit frame; that is a scene change, not counted as unsupported here.");
        report.notes.Add("Read-only: no scene, prefab, catalog or profile was created or modified.");

        string json = EncodeJson(report);
        Directory.CreateDirectory("docs/world/floatingorigin");
        File.WriteAllText("docs/world/floatingorigin/06K_OBSERVATION_FEASIBILITY.json", json + "\n", new UTF8Encoding(false));

        var summary = new StringBuilder();
        summary.AppendLine("Observation feasibility against the implemented executor");
        summary.AppendLine("  observations=" + report.observations + ", supported=" + report.supported + ", UNSUPPORTED=" + report.unsupported);
        summary.AppendLine("  fits PreserveContent(spatial)=" + report.fitPreserveSpatial +
            ", PreserveContent(nonSpatial)=" + report.fitPreserveNonSpatial +
            ", SceneNetworkObject(nonSpatial)=" + report.fitSceneNetworkObject);
        summary.AppendLine("  blocking component kinds:");
        foreach (string line in report.blockingComponentSummary) summary.AppendLine("    " + line);
        return summary.ToString();
    }

    /// <summary>Mirrors GlobalSceneNativeExecutor.UnsupportedOwnedSubtree and records what blocks it.</summary>
    private static bool UnsupportedOwnedSubtree(GameObject source, bool spatial, List<string> blocking)
    {
        var stack = new Stack<Transform>();
        stack.Push(source.transform);
        bool unsupported = false;
        while (stack.Count > 0)
        {
            var t = stack.Pop();
            foreach (var c in t.GetComponents<Component>())
            {
                if (c == null) { Add(blocking, "MissingScript"); unsupported = true; continue; }
                if (c is Rigidbody || c is Joint || c is CharacterController || c is NavMeshAgent || c is Rigidbody2D || c is Collider2D ||
                    c is NetworkTransform || c is IGlobalMotionActorParticipant || c is GlobalMotionReplicator || c is GlobalMotionPoseAdapter)
                { Add(blocking, c.GetType().Name); unsupported = true; continue; }
                if (spatial && !(c is Transform || c is MeshFilter || c is Renderer || c is Collider || c is GlobalSceneSourceMarker))
                { Add(blocking, c.GetType().Name); unsupported = true; continue; }
                if (!spatial && (c is Renderer || c is Collider || c is Camera || c is Light || c is Animator || c is ParticleSystem))
                { Add(blocking, c.GetType().Name); unsupported = true; }
            }
            for (int i = 0; i < t.childCount; i++) stack.Push(t.GetChild(i));
        }
        return unsupported;
    }
    private static void Add(List<string> list, string value) { if (!list.Contains(value)) list.Add(value); }
    private static string HierarchyPath(Transform t)
    {
        string path = t.name;
        while (t.parent != null) { t = t.parent; path = t.name + "/" + path; }
        return path;
    }
    private static Type FindType(string name)
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (name.StartsWith("Newtonsoft.", StringComparison.Ordinal) && assembly.GetName().Name != "Newtonsoft.Json") continue;
            var type = assembly.GetType(name); if (type != null) return type;
        }
        throw new InvalidOperationException("Type unavailable: " + name);
    }
    private static string EncodeJson(object value)
    {
        var format = FindType("Newtonsoft.Json.Formatting");
        return (string)FindType("Newtonsoft.Json.JsonConvert").GetMethod("SerializeObject", new[] { typeof(object), format })
            .Invoke(null, new[] { value, Enum.ToObject(format, 1) });
    }
}
