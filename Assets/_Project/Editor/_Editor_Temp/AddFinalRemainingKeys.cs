using UnityEngine;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine.Localization.Tables;

public static class AddFinalRemainingKeys
{
    [MenuItem("Tools/ProjectC/Localization/Add Final Remaining Keys")]
    public static void Execute()
    {
        var sharedPath = "Assets/_Project/Settings/Localization/UI_Table Shared Data.asset";
        var ruPath = "Assets/_Project/Settings/Localization/UI_Table_ru.asset";
        var shared = AssetDatabase.LoadAssetAtPath<SharedTableData>(sharedPath);
        var ruTable = AssetDatabase.LoadAssetAtPath<StringTable>(ruPath);
        if (shared == null || ruTable == null) { Debug.LogError("[AddKeys] Tables not found"); return; }

        var keys = new (string key, string ru)[]
        {
            ("ui.cargo.title_format", "Грузовой отсек: {0}"),
            ("ui.cargo.title", "Грузовой отсек"),
            ("ui.cargo.col.inventory", "Инвентарь игрока"),
            ("ui.cargo.col.hold", "Трюм корабля"),
            ("ui.cargo.label.packs", "Паков:"),
            ("ui.cargo.btn.store", "→ В трюм"),
            ("ui.cargo.btn.retrieve", "← Из трюма"),
            ("ui.cargo.status.ready", "Готов"),
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
