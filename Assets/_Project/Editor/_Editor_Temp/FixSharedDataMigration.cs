using UnityEngine;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine.Localization.Tables;
using UnityEngine.Localization;
using System.Text;

public static class FixSharedDataMigration
{
    public static void Execute()
    {
        string[] tables = { "System_Table", "UI_Table", "Static_Table", "Dialogue_Table" };
        var sb = new StringBuilder();

        foreach (var tableName in tables)
        {
            var ruPath = $"Assets/_Project/Settings/Localization/{tableName}_ru.asset";
            var ruTable = AssetDatabase.LoadAssetAtPath<StringTable>(ruPath);
            if (ruTable == null) { sb.AppendLine($"{tableName}: RU table not found"); continue; }

            var collPath = $"Assets/_Project/Settings/Localization/{tableName}.asset";
            var coll = AssetDatabase.LoadAssetAtPath<StringTableCollection>(collPath);
            if (coll == null) { sb.AppendLine($"{tableName}: Collection not found"); continue; }

            var shared = coll.SharedData;

            // Collect all existing values: (id, value) pairs from RU StringTable
            var idValues = new System.Collections.Generic.List<(long id, string value)>();
            foreach (var kv in ruTable)
            {
                var entry = kv.Value;
                if (entry != null && !string.IsNullOrEmpty(entry.Value))
                    idValues.Add((entry.KeyId, entry.Value));
            }

            sb.AppendLine($"{tableName}: {idValues.Count} values, SharedData.Entries was {shared.Entries.Count}");

            // Remove all existing entries from SharedData and recreate
            // First, clear SharedData
            while (shared.Entries.Count > 0)
                shared.RemoveKey(shared.Entries[0].Key);

            // Also remove from all locale tables
            foreach (var loc in LocalizationEditorSettings.GetLocales())
            {
                var table = coll.GetTable(loc.Identifier.Code) as StringTable;
                if (table != null) table.Clear();
            }

            // Re-add to SharedData + RU table using proper API
            // The issue: we don't know the KEYS — only the values!
            // We need to reconstruct keys from our Populate scripts
            // For now, let's just ensure SharedData has proper entries
            // and rebuild from the known data
            sb.AppendLine($"  Cleared. Need to re-populate from populate scripts.");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log(sb.ToString());
        Debug.LogWarning("[FixSharedData] Tables cleared. Run Populate scripts again to refill with proper keys.");
    }
}
