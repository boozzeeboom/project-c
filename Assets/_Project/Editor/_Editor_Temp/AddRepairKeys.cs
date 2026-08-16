using UnityEngine;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine.Localization.Tables;
using System.Collections.Generic;

public static class AddRepairKeys
{
    public static void Execute()
    {
        var collection = AssetDatabase.LoadAssetAtPath<StringTableCollection>("Assets/_Project/Settings/Localization/UI_Table.asset");
        var ruTable = AssetDatabase.LoadAssetAtPath<StringTable>("Assets/_Project/Settings/Localization/UI_Table_ru.asset");
        if (collection == null || ruTable == null) { Debug.LogError("[AddRepairKeys] Not found"); return; }

        var shared = collection.SharedData;
        var existing = new HashSet<string>();
        foreach (var e in shared.Entries) if (!string.IsNullOrEmpty(e.Key)) existing.Add(e.Key);

        var newKeys = new Dictionary<string, string> {
            { "ui.repair.slot_empty", "Установлено: пусто" },
            { "ui.repair.no_database", "База модулей не задана." },
            { "ui.repair.no_ship", "Корабль не выбран." },
            { "ui.repair.install", "Установить" },
            { "ui.repair.insufficient_power", "Недостаточно энергии" },
            { "ui.repair.no_modules", "Нет совместимых модулей для этого слота." },
            { "ui.repair.available_modules", "Доступные модули:" },
            { "ui.repair.done", "Готово ✓" },
            { "ui.repair.hull_request", "Запрос на ремонт корпуса отправлен..." },
            { "ui.repair.paint_request", "Запрос на покраску отправлен..." },
            { "ui.repair.no_pads", "Нет свободных падов!" },
            { "ui.repair.free", "Бесплатно" },
            { "ui.repair.not_spawned", "Корабль не заспавнен." },
            { "ui.repair.cost_empty", "Стоимость: —" },
        };

        int added = 0;
        foreach (var (key, value) in newKeys)
        {
            if (existing.Contains(key)) continue;
            var entry = shared.AddKey(key);
            ruTable.AddEntry(entry.Id, value);
            added++;
        }

        EditorUtility.SetDirty(ruTable);
        EditorUtility.SetDirty(shared);
        EditorUtility.SetDirty(collection);
        AssetDatabase.SaveAssets();
        Debug.Log($"[AddRepairKeys] Added {added}, total={shared.Entries.Count}");
    }
}
