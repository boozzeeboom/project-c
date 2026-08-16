using UnityEngine;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine.Localization.Tables;
using UnityEngine.Localization;
using System.Text;

public static class DebugTableExport
{
    public static void Execute()
    {
        string[] tables = { "System_Table", "UI_Table", "Static_Table", "Dialogue_Table" };
        var sb = new StringBuilder();

        foreach (var name in tables)
        {
            var path = $"Assets/_Project/Settings/Localization/{name}.asset";
            var coll = AssetDatabase.LoadAssetAtPath<StringTableCollection>(path);
            if (coll == null) { sb.AppendLine($"{name}: COLLECTION NULL"); continue; }

            var shared = coll.SharedData;
            sb.AppendLine($"{name}: SharedData entries={shared.Entries.Count}");

            var ruTable = coll.GetTable("ru") as StringTable;
            if (ruTable != null)
            {
                sb.AppendLine($"  RU table values: {ruTable.Count}");
                int shown = 0;
                foreach (var kv in ruTable)
                {
                    var entry = shared.GetEntry(kv.Key);
                    var key = entry?.Key ?? $"id={kv.Key}";
                    sb.AppendLine($"    '{key}' = '{kv.Value.Value}'");
                    if (++shown >= 5) { sb.AppendLine($"    ... and {ruTable.Count - 5} more"); break; }
                }
            }
            else
            {
                sb.AppendLine("  RU table: NULL");
            }
        }

        Debug.Log(sb.ToString());
    }
}
