using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEngine;

/// <summary>
/// T-FO06I sizing: counts catalog-relevant observations (roots and NetworkObjects) in the pilot scenes by
/// parsing the saved YAML. Read-only, loads no scene. Establishes the real authoring volume before any
/// catalog is drafted, so the effort is measured rather than guessed.
/// </summary>
public static class SizePilotSceneCatalog
{
    private static readonly string[] Scenes =
    {
        "Assets/_Project/Scenes/BootstrapScene.unity",
        "Assets/_Project/Scenes/World/WorldScene_0_0.unity",
    };

    [Serializable] public sealed class SceneRow
    {
        public string path, sceneGuid;
        public int gameObjects, roots, networkObjects, rootNetworkObjects, observations;
        public int monoBehaviours, missingScripts;
        public List<string> missingScriptGuids = new List<string>();
    }
    [Serializable] public sealed class Report
    {
        public string date, verdict, networkObjectScriptGuid;
        public int totalObservations, totalEntriesRequired;
        public List<SceneRow> scenes = new List<SceneRow>();
        public List<string> notes = new List<string>();
    }

    public static string Execute()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stable Edit Mode required.");

        string networkObjectGuid = AssetDatabase.AssetPathToGUID("Packages/com.unity.netcode.gameobjects/Runtime/Core/NetworkObject.cs");
        if (string.IsNullOrEmpty(networkObjectGuid)) throw new InvalidOperationException("Could not resolve the NetworkObject script GUID.");

        var report = new Report
        {
            date = DateTime.Now.ToString("yyyy-MM-dd"),
            networkObjectScriptGuid = networkObjectGuid,
            verdict = "Read-only observation sizing for the declared pilot scene set"
        };

        foreach (string path in Scenes)
        {
            if (!File.Exists(path)) throw new InvalidOperationException("Scene file missing: " + path);
            var row = new SceneRow { path = path, sceneGuid = AssetDatabase.AssetPathToGUID(path) };

            string[] lines = File.ReadAllLines(path);
            int currentClass = 0;
            string currentFileId = null;
            var transformFathers = new Dictionary<string, string>();   // Transform owner GO fileID -> father Transform fileID
            var transformOwner = new Dictionary<string, string>();     // Transform fileID -> GO fileID
            var networkObjectOwners = new HashSet<string>();           // GO fileIDs carrying a NetworkObject
            string pendingOwner = null, pendingFather = null;

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                if (line.StartsWith("--- !u!", StringComparison.Ordinal))
                {
                    Flush(ref pendingOwner, ref pendingFather, transformFathers);
                    int amp = line.IndexOf('&');
                    currentClass = int.TryParse(line.Substring(7, amp - 8), NumberStyles.None, CultureInfo.InvariantCulture, out int parsed) ? parsed : 0;
                    currentFileId = amp >= 0 ? line.Substring(amp + 1).Trim() : null;
                    if (currentClass == 1) row.gameObjects++;
                    else if (currentClass == 114) row.monoBehaviours++;
                    continue;
                }
                if (currentFileId == null) continue;

                if (currentClass == 4) // Transform
                {
                    if (line.StartsWith("  m_GameObject: ", StringComparison.Ordinal))
                    {
                        pendingOwner = Extract(line, "fileID:");
                        if (pendingOwner != null) transformOwner[currentFileId] = pendingOwner;
                    }
                    else if (line.StartsWith("  m_Father: ", StringComparison.Ordinal)) pendingFather = Extract(line, "fileID:");
                }
                else if (currentClass == 114 && line.StartsWith("  m_Script: ", StringComparison.Ordinal))
                {
                    string guid = Extract(line, "guid:");
                    string scriptFileId = Extract(line, "fileID:");
                    if (scriptFileId == "11500000" && !string.IsNullOrEmpty(guid))
                    {
                        string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                        if (string.IsNullOrEmpty(assetPath) || !(File.Exists(assetPath) || Directory.Exists(assetPath)))
                        {
                            row.missingScripts++;
                            if (!row.missingScriptGuids.Contains(guid)) row.missingScriptGuids.Add(guid);
                        }
                        if (guid == networkObjectGuid)
                        {
                            // Find the owning GameObject of this behaviour.
                            for (int j = i; j >= 0 && j > i - 12; j--)
                                if (lines[j].StartsWith("  m_GameObject: ", StringComparison.Ordinal))
                                { string owner = Extract(lines[j], "fileID:"); if (owner != null) networkObjectOwners.Add(owner); break; }
                        }
                    }
                }
            }
            Flush(ref pendingOwner, ref pendingFather, transformFathers);

            foreach (var pair in transformFathers) if (pair.Value == "0") row.roots++;
            row.networkObjects = networkObjectOwners.Count;
            foreach (string owner in networkObjectOwners)
                if (transformFathers.TryGetValue(owner, out string father) && father == "0") row.rootNetworkObjects++;

            // An observation is a root or a NetworkObject; objects that are both are counted once.
            row.observations = row.roots + row.networkObjects - row.rootNetworkObjects;
            report.scenes.Add(row);
            report.totalObservations += row.observations;
        }
        report.totalEntriesRequired = report.totalObservations;

        report.notes.Add("The compiler requires exactly one reviewed entry per observation, each with a non-empty review note.");
        report.notes.Add("Counts come from the saved YAML. Prefab-instance objects can expose additional identity, so the project's own audit remains the authoring source.");
        report.notes.Add("No scene was loaded, modified or saved; no catalog was drafted.");

        string json = EncodeJson(report);
        File.WriteAllText("docs/world/floatingorigin/06I_PILOT_CATALOG_SIZING.json", json + "\n", new UTF8Encoding(false));

        var summary = new StringBuilder();
        summary.AppendLine("Pilot catalog sizing (read-only, no scene loaded)");
        foreach (var row in report.scenes)
            summary.AppendLine("  " + Path.GetFileName(row.path) + ": GO=" + row.gameObjects + ", roots=" + row.roots +
                ", networkObjects=" + row.networkObjects + " (root NO=" + row.rootNetworkObjects + ")" +
                ", observations=" + row.observations + ", MB=" + row.monoBehaviours + ", missingScripts=" + row.missingScripts);
        summary.AppendLine("  TOTAL observations = reviewed entries required: " + report.totalEntriesRequired);
        return summary.ToString();
    }

    private static void Flush(ref string owner, ref string father, Dictionary<string, string> map)
    {
        if (owner != null && father != null && !map.ContainsKey(owner)) map.Add(owner, father);
        owner = null; father = null;
    }
    private static string Extract(string line, string key)
    {
        int start = line.IndexOf(key, StringComparison.Ordinal);
        if (start < 0) return null;
        start += key.Length;
        while (start < line.Length && line[start] == ' ') start++;
        int end = start;
        while (end < line.Length && line[end] != ',' && line[end] != '}' && line[end] != ' ') end++;
        return end > start ? line.Substring(start, end - start) : null;
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
