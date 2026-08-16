using UnityEngine;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine.Localization.Tables;
using System.Collections.Generic;

public static class AddMissingUIKeys
{
    private const string SETTINGS_PATH = "Assets/_Project/Settings/Localization";

    public static void Execute()
    {
        var collection = AssetDatabase.LoadAssetAtPath<StringTableCollection>($"{SETTINGS_PATH}/UI_Table.asset");
        var ruTable = AssetDatabase.LoadAssetAtPath<StringTable>($"{SETTINGS_PATH}/UI_Table_ru.asset");
        if (collection == null || ruTable == null)
        {
            Debug.LogError("[AddUIKeys] Collection or RU table not found");
            return;
        }

        var shared = collection.SharedData;
        var existing = new HashSet<string>();
        foreach (var e in shared.Entries)
            if (!string.IsNullOrEmpty(e.Key)) existing.Add(e.Key);

        // New keys to add (only those not already in SharedData)
        var newKeys = new Dictionary<string, string>
        {
            // EscMenu main buttons
            { "ui.esc_menu.button.continue", "ПРОДОЛЖИТЬ" },
            { "ui.esc_menu.button.settings", "НАСТРОЙКИ" },
            { "ui.esc_menu.button.rescue", "СПАСЕНИЕ" },
            { "ui.esc_menu.button.exit", "ВЫХОД В МЕНЮ" },
            { "ui.esc_menu.root_title", "МЕНЮ" },
            // CharacterWindow filters
            { "ui.character.filter.all", "Все" },
            { "ui.character.filter.contracts", "Контракты" },
            { "ui.character.filter.quests", "Квесты" },
            { "ui.character.filter.active", "Активные" },
            { "ui.character.filter.available", "Доступные" },
            { "ui.character.filter.completed", "Завершённые" },
            { "ui.character.filter.all_types", "Все типы" },
            // CharacterWindow labels
            { "ui.character.player_owner", "Игрок (Owner)" },
            { "ui.character.player", "Игрок" },
            { "ui.character.no_data", "—" },
            { "ui.character.bonuses", "Бонусы: " },
            { "ui.character.equip", "НАДЕТЬ" },
            { "ui.character.unequip", "СНЯТЬ" },
            { "ui.character.drop", "БРОСИТЬ" },
            { "ui.character.no_reputation", "Нет данных о репутации" },
            { "ui.character.no_attitude", "Нет данных об отношениях" },
            { "ui.character.no_contracts", "Нет активных или доступных контрактов" },
            { "ui.character.select_item_left", "Выберите предмет слева" },
            { "ui.character.select_contract", "Выберите контракт из списка" },
            { "ui.character.contract_unavailable", "Этот контракт уже не доступен для принятия" },
            { "ui.character.contract_not_active", "Этот контракт не активен" },
            { "ui.character.request_sent", "Запрос отправлен..." },
            { "ui.character.no_active_contracts", "Нет активных контрактов" },
            { "ui.character.loading_contracts", "Загрузка контрактов..." },
            // Quest states
            { "ui.quest.state.discovered", "ОБНАРУЖЕН" },
            { "ui.quest.state.offered", "ПРЕДЛОЖЕН" },
            { "ui.quest.state.active", "АКТИВЕН" },
            { "ui.quest.state.completed", "ВЫПОЛНЕН" },
            { "ui.quest.state.turned_in", "СДАН" },
            { "ui.quest.state.failed", "ПРОВАЛЕН" },
            { "ui.quest.track", "Следить" },
            { "ui.quest.untrack", "Не следить" },
            { "ui.quest.discovered_unavailable", "Список найденных квестов недоступен" },
            { "ui.quest.reject_unavailable", "Отказ от квеста пока не реализован (ждёт серверную часть)" },
            // Skills
            { "ui.skill.learn", "Изучить" },
            { "ui.skill.forget", "Забыть" },
            // Contract types + ranks
            { "ui.contract.type.standard", "Обычный" },
            { "ui.contract.type.urgent", "Срочный" },
            { "ui.contract.type.receipt", "Квитанция" },
            { "ui.contract.rank.primium", "Примум" },
            { "ui.contract.rank.secundus", "Секундус" },
            { "ui.contract.rank.tertius", "Терциус" },
            { "ui.contract.rank.quartus", "Квартус" },
            // MyShipsTab
            { "ui.ship.no_ships", "Нет доступных кораблей. Найдите ключ в мире." },
            { "ui.ship.hull_broken", "Прочность: СЛОМАН" },
            { "ui.ship.hull_empty", "Прочность: —" },
            { "ui.ship.fuel_empty", "Топливо: —" },
            { "ui.ship.cargo_empty", "Груз: — (нет данных)" },
            { "ui.ship.modules_zero", "Модулей: 0" },
            { "ui.ship.hold_empty", "Трюм пуст" },
            // MarketWindow
            { "ui.market.loading", "Загрузка рынка..." },
            { "ui.market.no_data", "Нет данных о рынке" },
            { "ui.market.server_unavailable", "Сервер обменника не доступен" },
            { "ui.market.server_not_ready", "Сервер обменника не инициализирован. Подождите пару секунд." },
            { "ui.market.select_left", "Выберите предмет в левом списке" },
            { "ui.market.select_right", "Выберите товар в правом списке" },
            { "ui.market.show_all", "Показать все товары" },
            { "ui.market.show_mine", "Показать мои товары" },
            { "ui.market.select_ship_first", "Сначала выберите корабль" },
            { "ui.market.no_contracts_here", "Нет контрактов на этой локации" },
            { "ui.market.op.buy", "Куплено" },
            { "ui.market.op.sell", "Продано" },
            { "ui.market.op.load", "Погрузка" },
            { "ui.market.op.unload", "Разгрузка" },
            // ShipCargoConsoleWindow
            { "ui.cargo.select_inventory", "Выберите предмет в инвентаре" },
            { "ui.cargo.select_hold", "Выберите ящик в трюме" },
            { "ui.cargo.server_unavailable", "Сервер грузового отсека не доступен" },
            { "ui.cargo.unpack_unavailable", "Распаковка недоступна: нет курса обмена для этого товара" },
        };

        int added = 0;
        foreach (var (key, value) in newKeys)
        {
            if (existing.Contains(key))
            {
                Debug.Log($"[AddUIKeys] SKIP (exists): {key}");
                continue;
            }
            var sharedEntry = shared.AddKey(key);
            ruTable.AddEntry(sharedEntry.Id, value);
            added++;
            Debug.Log($"[AddUIKeys] ADDED: {key} = \"{value}\"");
        }

        EditorUtility.SetDirty(ruTable);
        EditorUtility.SetDirty(shared);
        EditorUtility.SetDirty(collection);
        AssetDatabase.SaveAssets();
        Debug.Log($"[AddUIKeys] Done! Added {added} new keys. Total SharedData.Entries = {shared.Entries.Count}");
    }
}
