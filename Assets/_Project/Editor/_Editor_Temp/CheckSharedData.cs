using UnityEngine;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine.Localization.Tables;
using System.Text;

public static class CheckSharedData
{
    public static void Execute()
    {
        var sb = new StringBuilder();
        string[] tables = { "Static_Table", "System_Table", "UI_Table", "Dialogue_Table" };

        foreach (var name in tables)
        {
            var collPath = $"Assets/_Project/Settings/Localization/{name}.asset";
            var coll = AssetDatabase.LoadAssetAtPath<StringTableCollection>(collPath);
            if (coll == null) { Debug.LogError($"{name}: COLLECTION NULL"); continue; }
            
            var shared = coll.SharedData;
            int entryCount = shared != null ? shared.Entries.Count : -1;
            Debug.Log($"{name}: SharedData.Entries = {entryCount}");
            
            var ruPath = $"Assets/_Project/Settings/Localization/{name}_ru.asset";
            var t = AssetDatabase.LoadAssetAtPath<StringTable>(ruPath);
            int tableCount = t != null ? t.Count : -1;
            Debug.Log($"{name}_ru: table.Count = {tableCount}");
        }
    }
}
