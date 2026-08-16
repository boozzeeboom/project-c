using UnityEngine;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine.Localization.Tables;
using UnityEngine.Localization;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Linq;

public static class ExportFixer
{
    public static void Execute()
    {
        string[] tables = { "System_Table", "UI_Table", "Static_Table", "Dialogue_Table" };
        var locales = LocalizationEditorSettings.GetLocales();
        var exportDir = "Assets/_Project/Localization/Export";
        EnsureDir(exportDir);

        foreach (var tableName in tables)
        {
            var collection = AssetDatabase.LoadAssetAtPath<StringTableCollection>(
                $"Assets/_Project/Settings/Localization/{tableName}.asset");
            var ruTable = AssetDatabase.LoadAssetAtPath<StringTable>(
                $"Assets/_Project/Settings/Localization/{tableName}_ru.asset");

            if (collection == null) { Debug.LogError($"{tableName}: collection null"); continue; }
            if (ruTable == null) { Debug.LogWarning($"{tableName}: ru table null"); }

            var shared = collection.SharedData;

            // Collect all keys from SharedData (id -> key)
            var idToKey = new Dictionary<long, string>();
            foreach (var se in shared.Entries)
            {
                if (!string.IsNullOrEmpty(se.Key) && !idToKey.ContainsKey(se.Id))
                    idToKey[se.Id] = se.Key;
            }

            Debug.Log($"{tableName}: SharedData entries={shared.Entries.Count}, idToKey={idToKey.Count}, ruTable={ruTable?.Count ?? 0}");

            var csvPath = $"{exportDir}/{tableName}.csv";
            using (var sw = new StreamWriter(csvPath, false, Encoding.UTF8))
            {
                // Header
                var header = "Key";
                foreach (var loc in locales)
                    header += "," + loc.Identifier.Code;
                sw.WriteLine(header);

                // Rows from SharedData
                foreach (var kv in idToKey)
                {
                    var key = kv.Value;
                    var id = kv.Key;
                    var row = CsvEscape(key);
                    foreach (var loc in locales)
                    {
                        var table = collection.GetTable(loc.Identifier.Code) as StringTable;
                        if (table != null)
                        {
                            var entry = table.GetEntry(id);
                            row += "," + CsvEscape(entry?.GetLocalizedString() ?? "");
                        }
                        else row += ",";
                    }
                    sw.WriteLine(row);
                }
            }

            Debug.Log($"{tableName}: exported {idToKey.Count} keys to {csvPath}");
        }

        AssetDatabase.Refresh();
        Debug.Log("[ExportFixer] Done! Open folder: " + exportDir);
        EditorUtility.RevealInFinder(exportDir);
    }

    static string CsvEscape(string v)
    {
        if (string.IsNullOrEmpty(v)) return "";
        if (v.Contains(",") || v.Contains("\"") || v.Contains("\n"))
            return "\"" + v.Replace("\"", "\"\"") + "\"";
        return v;
    }

    static void EnsureDir(string dir)
    {
        var abs = Path.Combine(Application.dataPath.Replace("/Assets", ""), dir);
        if (!Directory.Exists(abs)) Directory.CreateDirectory(abs);
    }
}
