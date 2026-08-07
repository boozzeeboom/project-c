using UnityEngine;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine.Localization.Tables;

public static class AddKnowledgeInventoryKeys
{
    [MenuItem("Tools/ProjectC/Localization/Add Knowledge+Inventory+Dialog Keys")]
    public static void Execute()
    {
        var sharedPath = "Assets/_Project/Settings/Localization/UI_Table Shared Data.asset";
        var ruPath = "Assets/_Project/Settings/Localization/UI_Table_ru.asset";

        var shared = AssetDatabase.LoadAssetAtPath<SharedTableData>(sharedPath);
        var ruTable = AssetDatabase.LoadAssetAtPath<StringTable>(ruPath);

        if (shared == null) { Debug.LogError("[AddKeys] SharedTableData not found at " + sharedPath); return; }
        if (ruTable == null) { Debug.LogError("[AddKeys] RU StringTable not found at " + ruPath); return; }

        var keys = new (string key, string ru)[]
        {
            ("ui.knowledge.toast_format", "📖 Открыто знание — {0}: {1}"),
            ("ui.knowledge.toast_and_more", "и ещё {0}"),
            ("ui.inventory.select_item_to_use", "Выберите предмет для использования"),
            ("ui.inventory.use_todo", "Использование предметов — TODO (Phase 8+)"),
            ("ui.inventory.select_item_to_drop", "Выберите предмет для броска"),
            ("ui.inventory.network_unavailable", "Сеть не запущена"),
            ("ui.inventory.player_not_found", "Игрок не найден"),
            ("ui.inventory.dropping", "Бросаю..."),
            ("ui.dialog.action.add_item", "+1 предмет"),
            ("ui.dialog.action.remove_item", "-1 предмет"),
            ("ui.dialog.action.reputation", "Репутация"),
            ("ui.dialog.action.attitude", "Отношение"),
            ("ui.dialog.action.objective_complete", "Цель выполнена"),
            ("ui.dialog.option_unavailable", "Недоступно"),
        };

        int added = 0;
        foreach (var (key, ru) in keys)
        {
            var entry = shared.GetEntry(key);
            if (entry == null)
            {
                shared.AddKey(key);
                ruTable.AddEntry(key, ru);
                added++;
                Debug.Log($"[AddKeys] + {key}");
            }
            else
            {
                Debug.Log($"[AddKeys] skip (exists): {key}");
            }
        }

        EditorUtility.SetDirty(shared);
        EditorUtility.SetDirty(ruTable);
        AssetDatabase.SaveAssets();
        Debug.Log($"[AddKeys] Done. Added {added} new keys to UI_Table.");
    }
}
