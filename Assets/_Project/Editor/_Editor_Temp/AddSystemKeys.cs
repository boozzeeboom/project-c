using UnityEngine;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine.Localization.Tables;

public static class AddSystemKeys
{
    [MenuItem("Tools/ProjectC/Localization/Add System Keys")]
    public static void Execute()
    {
        var sharedPath = "Assets/_Project/Settings/Localization/UI_Table Shared Data.asset";
        var ruPath = "Assets/_Project/Settings/Localization/UI_Table_ru.asset";
        var shared = AssetDatabase.LoadAssetAtPath<SharedTableData>(sharedPath);
        var ruTable = AssetDatabase.LoadAssetAtPath<StringTable>(ruPath);
        if (shared == null || ruTable == null) { Debug.LogError("[AddKeys] Tables not found"); return; }

        var keys = new (string key, string ru)[]
        {
            ("ui.system.questtracker_unavailable", "QuestTracker недоступен"),
            ("ui.system.queststate_unavailable", "QuestClientState недоступен"),
            ("ui.system.contractstate_unavailable", "ContractClientState недоступен"),
            ("ui.character.btn.accept_contract", "Взять"),
            ("ui.character.btn.complete_contract", "Сдать"),
            ("ui.character.btn.fail_contract", "Провалить"),
        };

        int added = 0;
        foreach (var (key, ru) in keys)
        {
            if (shared.GetEntry(key) == null)
            {
                shared.AddKey(key);
                ruTable.AddEntry(key, ru);
                added++;
            }
        }
        EditorUtility.SetDirty(shared);
        EditorUtility.SetDirty(ruTable);
        AssetDatabase.SaveAssets();
        Debug.Log($"[AddKeys] Done. Added {added} keys (total: {shared.Entries.Count}).");
    }
}
