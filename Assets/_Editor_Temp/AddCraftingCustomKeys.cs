using UnityEngine;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine.Localization.Tables;

public static class AddCraftingCustomKeys
{
    [MenuItem("Tools/ProjectC/Localization/Add Crafting+Custom Keys")]
    public static void Execute()
    {
        var sharedPath = "Assets/_Project/Settings/Localization/UI_Table Shared Data.asset";
        var ruPath = "Assets/_Project/Settings/Localization/UI_Table_ru.asset";
        var shared = AssetDatabase.LoadAssetAtPath<SharedTableData>(sharedPath);
        var ruTable = AssetDatabase.LoadAssetAtPath<StringTable>(ruPath);
        if (shared == null || ruTable == null) { Debug.LogError("[AddKeys] Tables not found"); return; }

        var keys = new (string key, string ru)[]
        {
            ("ui.crafting.section.recipes", "Рецепты"),
            ("ui.crafting.section.ingredients", "Ингредиенты:"),
            ("ui.crafting.section.buffer", "В буфере:"),
            ("ui.custom.status.female", "Текущий выбор: Женский. Изменения применяются сразу."),
            ("ui.custom.status.male", "Текущий выбор: Мужской. Изменения применяются сразу."),
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
