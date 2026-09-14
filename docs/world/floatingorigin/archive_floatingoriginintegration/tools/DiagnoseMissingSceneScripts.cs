using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEngine;

/// <summary>
/// T-FO06G diagnostics: identifies missing MonoBehaviour scripts in a scene by reading the saved YAML.
/// Read-only. Reports the exact script GUID/fileID and owning GameObject so the identity can be decided
/// deliberately instead of guessed from a GameObject name. Deletes and repairs nothing.
/// </summary>
public static class DiagnoseMissingSceneScripts
{
    private const string ScenePath = "Assets/_Project/Scenes/BootstrapScene.unity";

    [Serializable] public sealed class MissingRow
    {
        public string behaviourFileId, gameObjectFileId, gameObjectName, scriptGuid, scriptFileId, resolvedAssetPath, guidFoundInMetaFile;
    }
    [Serializable] public sealed class Report
    {
        public string date, scenePath, verdict;
        public bool sceneDirtyInEditor, scenePathIsSavedFile;
        public int monoBehaviours, scriptReferences, nonScriptReferences, distinctScriptGuids, unresolvedScriptGuids, missingBehaviours;
        public List<MissingRow> missing = new List<MissingRow>();
        public List<string> notes = new List<string>();
    }
    // A MonoScript reference always uses this local file id. Built-in object ids such as
    // {fileID: 19102, guid: 0000000000000000e000000000000000} are engine resources, not missing scripts.
    private const string MonoScriptFileId = "11500000";

    public static string Execute()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stable Edit Mode required.");
        if (!File.Exists(ScenePath)) throw new InvalidOperationException("Scene file missing: " + ScenePath);

        string[] lines = File.ReadAllLines(ScenePath);
        var objectNames = new Dictionary<string, string>();     // GameObject fileID -> m_Name
        var behaviourOwner = new Dictionary<string, string>();  // MonoBehaviour fileID -> GameObject fileID
        var behaviourScript = new Dictionary<string, string[]>(); // MonoBehaviour fileID -> [guid, fileID]

        string currentFileId = null;
        int currentClass = 0;
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];
            if (line.StartsWith("--- !u!", StringComparison.Ordinal))
            {
                int amp = line.IndexOf('&');
                string classPart = line.Substring(7, amp - 8);
                currentClass = int.TryParse(classPart, NumberStyles.None, CultureInfo.InvariantCulture, out int parsed) ? parsed : 0;
                currentFileId = amp >= 0 ? line.Substring(amp + 1).Trim() : null;
                continue;
            }
            if (currentFileId == null) continue;

            if (currentClass == 1 && line.StartsWith("  m_Name: ", StringComparison.Ordinal))
            {
                if (!objectNames.ContainsKey(currentFileId)) objectNames.Add(currentFileId, line.Substring(10).Trim());
            }
            else if (currentClass == 114)
            {
                if (line.StartsWith("  m_GameObject: ", StringComparison.Ordinal))
                {
                    string owner = Extract(line, "fileID:");
                    if (owner != null && !behaviourOwner.ContainsKey(currentFileId)) behaviourOwner.Add(currentFileId, owner);
                }
                else if (line.StartsWith("  m_Script: ", StringComparison.Ordinal))
                {
                    string guid = Extract(line, "guid:");
                    string scriptFileId = Extract(line, "fileID:");
                    if (!behaviourScript.ContainsKey(currentFileId)) behaviourScript.Add(currentFileId, new[] { guid ?? "", scriptFileId ?? "" });
                }
            }
        }

        var report = new Report
        {
            date = DateTime.Now.ToString("yyyy-MM-dd"), scenePath = ScenePath, scenePathIsSavedFile = true,
            monoBehaviours = behaviourScript.Count,
            verdict = "Read-only identification of missing scripts in the saved scene file"
        };
        for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
            if (scene.path == ScenePath) report.sceneDirtyInEditor = scene.isDirty;
        }

        var distinct = new HashSet<string>(StringComparer.Ordinal);
        var unresolved = new HashSet<string>(StringComparer.Ordinal);
        foreach (var pair in behaviourScript)
        {
            string guid = pair.Value[0];
            if (string.IsNullOrEmpty(guid)) continue;
            if (pair.Value[1] != MonoScriptFileId) { report.nonScriptReferences++; continue; }
            report.scriptReferences++;
            distinct.Add(guid);
            if (unresolved.Contains(guid)) { AddRow(report, pair.Key, pair.Value, behaviourOwner, objectNames); continue; }
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            bool resolved = !string.IsNullOrEmpty(assetPath) && (File.Exists(assetPath) || Directory.Exists(assetPath));
            if (resolved) continue;
            unresolved.Add(guid);
            AddRow(report, pair.Key, pair.Value, behaviourOwner, objectNames);
        }
        report.distinctScriptGuids = distinct.Count;
        report.unresolvedScriptGuids = unresolved.Count;
        report.missingBehaviours = report.missing.Count;

        // A GUID can still exist in an orphaned .meta even when Unity cannot resolve the asset.
        foreach (var row in report.missing) row.guidFoundInMetaFile = FindGuidInMetaFiles(row.scriptGuid);

        report.notes.Add("GameObject names do not establish a C# class. Only the script GUID identifies the missing asset.");
        report.notes.Add("Only MonoScript references (fileID " + MonoScriptFileId + ") are considered; engine built-in object references are counted separately and are not missing scripts.");
        report.notes.Add("Nothing was deleted, replaced or repaired; the scene was neither loaded nor saved by this tool.");
        if (report.sceneDirtyInEditor)
            report.notes.Add("The editor holds unsaved changes for this scene, so live components may differ from this saved-file analysis.");

        string json = EncodeJson(report);
        File.WriteAllText("docs/world/floatingorigin/06G_MISSING_SCENE_SCRIPTS.json", json + "\n", new UTF8Encoding(false));

        var summary = new StringBuilder();
        summary.AppendLine("Missing script diagnosis for " + ScenePath);
        summary.AppendLine("  MonoBehaviours in saved file: " + report.monoBehaviours + ", script refs: " + report.scriptReferences +
            ", non-script refs skipped: " + report.nonScriptReferences + ", distinct script GUIDs: " + report.distinctScriptGuids);
        summary.AppendLine("  Unresolved GUIDs: " + report.unresolvedScriptGuids + ", affected components: " + report.missingBehaviours);
        summary.AppendLine("  Scene dirty in editor: " + report.sceneDirtyInEditor);
        foreach (var row in report.missing)
            summary.AppendLine("  guid=" + row.scriptGuid + " fileID=" + row.scriptFileId + " on '" + row.gameObjectName +
                "' (GO " + row.gameObjectFileId + ", behaviour " + row.behaviourFileId + ") metaHit=" + row.guidFoundInMetaFile);
        return summary.ToString();
    }

    private static void AddRow(Report report, string behaviourId, string[] script,
        Dictionary<string, string> owners, Dictionary<string, string> names)
    {
        owners.TryGetValue(behaviourId, out string ownerId);
        string name = ownerId != null && names.TryGetValue(ownerId, out string found) ? found : "UNRESOLVED_GAMEOBJECT";
        report.missing.Add(new MissingRow
        {
            behaviourFileId = behaviourId, gameObjectFileId = ownerId ?? "", gameObjectName = name,
            scriptGuid = script[0], scriptFileId = script[1],
            resolvedAssetPath = AssetDatabase.GUIDToAssetPath(script[0]) ?? ""
        });
    }

    private static string FindGuidInMetaFiles(string guid)
    {
        if (string.IsNullOrEmpty(guid)) return "";
        foreach (string root in new[] { "Assets", "Packages" })
        {
            if (!Directory.Exists(root)) continue;
            foreach (string file in Directory.EnumerateFiles(root, "*.meta", SearchOption.AllDirectories))
            {
                string text;
                try { text = File.ReadAllText(file); } catch (IOException) { continue; }
                if (text.IndexOf(guid, StringComparison.Ordinal) >= 0) return file;
            }
        }
        return "";
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
