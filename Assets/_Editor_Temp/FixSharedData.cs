using UnityEngine;
using UnityEditor;
using UnityEngine.Localization.Tables;
using System.Collections.Generic;
using System.Text;

public static class FixSharedData
{
    public static void Execute()
    {
        string[] tables = { "System_Table", "UI_Table", "Static_Table", "Dialogue_Table" };
        var sb = new StringBuilder();

        foreach (var tableName in tables)
        {
            var ruPath = $"Assets/_Project/Settings/Localization/{tableName}_ru.asset";
            var ruTable = AssetDatabase.LoadAssetAtPath<StringTable>(ruPath);
            if (ruTable == null) { sb.AppendLine($"{tableName}: ru table not found"); continue; }

            var collPath = $"Assets/_Project/Settings/Localization/{tableName}.asset";
            var collSo = new SerializedObject(AssetDatabase.LoadAssetAtPath<Object>(collPath));
            var sharedProp = collSo.FindProperty("m_SharedData");
            if (sharedProp == null) { sb.AppendLine($"{tableName}: SharedData prop not found"); continue; }

            var sharedObj = sharedProp.managedReferenceValue;
            if (sharedObj == null) { sb.AppendLine($"{tableName}: SharedData is null"); continue; }

            var sharedType = sharedObj.GetType();
            var entriesField = sharedType.GetField("m_Entries", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
            var tableEntriesField = sharedType.GetField("m_TableEntries", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
            var addKeyMethod = sharedType.GetMethod("AddKey", new[] { typeof(string) });

            var entries = entriesField?.GetValue(sharedObj) as System.Collections.IList;
            var tableEntries = tableEntriesField?.GetValue(sharedObj) as System.Collections.IDictionary;

            // Build existing key→id map from SharedData
            var existingKeys = new Dictionary<string, long>();
            if (entries != null)
            {
                foreach (var e in entries)
                {
                    var eType = e.GetType();
                    var idField = eType.GetField("m_Id", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
                    var keyField = eType.GetField("m_Key", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
                    var id = (long)(idField?.GetValue(e) ?? 0L);
                    var key = (string)(keyField?.GetValue(e) ?? "");
                    if (!string.IsNullOrEmpty(key) && !existingKeys.ContainsKey(key))
                        existingKeys[key] = id;
                }
            }

            sb.AppendLine($"{tableName}: SharedData had {existingKeys.Count} keys, RU table has {ruTable.Count} entries");

            // Iterate RU StringTable entries
            var ruSo = new SerializedObject(ruTable);
            var tableDataProp = ruSo.FindProperty("m_TableData");
            if (tableDataProp == null) { sb.AppendLine("  m_TableData null"); continue; }

            // m_TableData → iterate children (Array of entries)
            // Each entry: id (long) + value (string)
            var valuesProp = tableDataProp.FindPropertyRelative("m_Values");
            var keysProp = tableDataProp.FindPropertyRelative("m_Keys");

            int added = 0;
            if (valuesProp != null && valuesProp.isArray)
            {
                // Go through each value entry
                var dictType = tableDataProp.managedReferenceValue?.GetType();
                if (dictType != null)
                {
                    var getEntryMethod = dictType.GetMethod("GetEnumerator");
                    // Use .NET reflection to get key-value pairs
                    var dictRef = tableDataProp.managedReferenceValue;
                    var asDict = dictRef as System.Collections.IDictionary;
                    if (asDict != null)
                    {
                        foreach (System.Collections.DictionaryEntry kv in asDict)
                        {
                            var id = (long)kv.Key;
                            var value = (string)kv.Value;
                            // Check SharedData
                            if (entries != null && addKeyMethod != null)
                            {
                                bool found = false;
                                foreach (var ek in existingKeys)
                                    if (ek.Value == id) { found = true; break; }
                                if (!found)
                                {
                                    // Add to SharedData — but we need the key string
                                    // The issue: we don't have the key, only the id
                                    sb.AppendLine($"  orphan id={id} value='{value?.Substring(0, Mathf.Min(30, value?.Length ?? 0))}...' - no key mapping");
                                }
                            }
                        }
                    }
                }
                sb.AppendLine($"  Values: {valuesProp.arraySize}");
            }

            if (keysProp != null && keysProp.isArray)
                sb.AppendLine($"  Keys: {keysProp.arraySize}");

            ruSo.ApplyModifiedProperties();
            collSo.ApplyModifiedProperties();

            if (added > 0)
                sb.AppendLine($"  Added {added} new SharedData entries");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log(sb.ToString());
    }
}
