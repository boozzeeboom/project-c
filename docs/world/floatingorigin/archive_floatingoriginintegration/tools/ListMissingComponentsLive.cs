using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Lists every missing MonoBehaviour in the loaded pilot scenes, including those coming from prefab
/// instances, and reports where the authoritative fix belongs (scene object or prefab asset). Read-only.
/// </summary>
public static class ListMissingComponentsLive
{
    [Serializable] public sealed class Row
    {
        public string scene, objectPath, rootName;
        public int componentIndex;
        public bool isPartOfPrefabInstance, isAddedGameObjectOverride;
        public string prefabAssetPath, nearestPrefabRootPath, fixBelongsTo;
    }
    [Serializable] public sealed class Report
    {
        public string date, verdict;
        public int scenesInspected, totalMissing;
        public List<Row> rows = new List<Row>();
        public List<string> notes = new List<string>();
    }

    public static string Execute()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stable Edit Mode required.");

        var report = new Report
        {
            date = DateTime.Now.ToString("yyyy-MM-dd"),
            verdict = "Live inventory of missing MonoBehaviour references in the loaded scenes"
        };

        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded) continue;
            report.scenesInspected++;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                {
                    var go = transform.gameObject;
                    var components = go.GetComponents<Component>();
                    for (int index = 0; index < components.Length; index++)
                    {
                        if (components[index] != null) continue;
                        var row = new Row
                        {
                            scene = scene.path, objectPath = HierarchyPath(transform), rootName = root.name, componentIndex = index,
                            isPartOfPrefabInstance = PrefabUtility.IsPartOfPrefabInstance(go),
                            isAddedGameObjectOverride = PrefabUtility.IsAddedGameObjectOverride(go)
                        };
                        if (row.isPartOfPrefabInstance)
                        {
                            var prefabRoot = PrefabUtility.GetNearestPrefabInstanceRoot(go);
                            row.nearestPrefabRootPath = prefabRoot != null ? HierarchyPath(prefabRoot.transform) : "";
                            row.prefabAssetPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go) ?? "";
                            // An added override lives in the scene; otherwise the dead reference comes from the asset.
                            row.fixBelongsTo = row.isAddedGameObjectOverride ? "scene_override" : "prefab_asset";
                        }
                        else { row.prefabAssetPath = ""; row.nearestPrefabRootPath = ""; row.fixBelongsTo = "scene_object"; }
                        report.rows.Add(row);
                        report.totalMissing++;
                    }
                }
        }

        report.notes.Add("Missing components inside a prefab instance originate in the prefab asset; removing them per-scene would only create overrides.");
        report.notes.Add("Read-only: nothing was removed, no scene or prefab was modified or saved.");

        string json = EncodeJson(report);
        Directory.CreateDirectory("docs/world/floatingorigin");
        File.WriteAllText("docs/world/floatingorigin/06J_LIVE_MISSING_COMPONENTS.json", json + "\n", new UTF8Encoding(false));

        var summary = new StringBuilder();
        summary.AppendLine("Live missing-component inventory: scenes=" + report.scenesInspected + ", missing=" + report.totalMissing);
        foreach (var row in report.rows)
            summary.AppendLine("  [" + row.fixBelongsTo + "] " + System.IO.Path.GetFileName(row.scene) + " :: " + row.objectPath +
                " (index " + row.componentIndex + ")" +
                (string.IsNullOrEmpty(row.prefabAssetPath) ? "" : " <- " + row.prefabAssetPath));
        return summary.ToString();
    }

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
