using UnityEngine;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine.Localization.Tables;
using UnityEngine.Localization;
using System.Text;

public static class DebugTableContents
{
    public static void Execute()
    {
        string[] tables = { "System_Table", "UI_Table", "Static_Table", "Dialogue_Table" };
        var sb = new StringBuilder();

        foreach (var name in tables)
        {
            var ruPath = $"Assets/_Project/Settings/Localization/{name}_ru.asset";
            var t = AssetDatabase.LoadAssetAtPath<StringTable>(ruPath);
            if (t == null) { sb.AppendLine($"{name}: RU table NULL"); continue; }

            sb.AppendLine($"{name}: {t.Count} rows");
            int n = 0;
            foreach (var kv in t)
            {
                var e = kv.Value;
                sb.AppendLine($"  [{e.KeyId}] key='{e.Key}' val='{Lim(e.Value, 50)}'");
                if (++n >= 3) { sb.AppendLine($"  ... +{t.Count - 3} more"); break; }
            }

            // SharedData check
            var collPath = $"Assets/_Project/Settings/Localization/{name}.asset";
            var coll = AssetDatabase.LoadAssetAtPath<StringTableCollection>(collPath);
            if (coll != null)
            {
                var sd = coll.SharedData;
                sb.AppendLine($"  SharedData.Entries: {sd.Entries.Count}");
                int m = 0;
                foreach (var se in sd.Entries)
                {
                    sb.AppendLine($"    SD: id={se.Id} key='{se.Key}'");
                    if (++m >= 3) { sb.AppendLine($"    ... +{sd.Entries.Count - 3}"); break; }
                }
            }
            else
            {
                sb.AppendLine($"  Collection NULL at {collPath}");
            }
        }

        Debug.Log(sb.ToString());
    }

    static string Lim(string s, int max)
    {
        if (string.IsNullOrEmpty(s)) return "(empty)";
        return s.Length <= max ? s : s.Substring(0, max) + "...";
    }
}
