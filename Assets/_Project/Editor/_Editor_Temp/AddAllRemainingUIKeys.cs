using UnityEngine;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine.Localization.Tables;

public static class AddAllRemainingUIKeys
{
    [MenuItem("Tools/ProjectC/Localization/Add All Remaining UI Keys")]
    public static void Execute()
    {
        var sharedPath = "Assets/_Project/Settings/Localization/UI_Table Shared Data.asset";
        var ruPath = "Assets/_Project/Settings/Localization/UI_Table_ru.asset";

        var shared = AssetDatabase.LoadAssetAtPath<SharedTableData>(sharedPath);
        var ruTable = AssetDatabase.LoadAssetAtPath<StringTable>(ruPath);

        if (shared == null) { Debug.LogError("[AddKeys] SharedTableData not found"); return; }
        if (ruTable == null) { Debug.LogError("[AddKeys] RU StringTable not found"); return; }

        var keys = new (string key, string ru)[]
        {
            // MarketWindow remaining
            ("ui.market.btn.pack", "→ УПАКОВАТЬ"),
            ("ui.market.btn.unpack", "← РАСПАКОВАТЬ"),
            ("ui.market.section.items", "Товары на рынке"),
            ("ui.market.section.warehouse", "Ваш склад"),
            ("ui.market.section.cargo", "Груз корабля"),
            ("ui.market.section.contracts", "Контракты НП"),
            ("ui.market.section.exchange", "Ресурсы: конвертация пикаблов ↔ ящики"),
            ("ui.market.label.qty", "Кол-во:"),
            ("ui.market.label.ship", "Корабль:"),
            ("ui.market.exchange.inventory", "Инвентарь (ресурсы)"),
            ("ui.market.exchange.warehouse", "Склад (ящики)"),
            ("ui.market.btn.show_mine", "Показать мои товары"),
            ("ui.market.btn.show_all", "Показать все товары"),

            // EscMenuWindow
            ("ui.esc_menu.button.back", "← НАЗАД"),
            ("ui.esc_menu.button.continue", "ПРОДОЛЖИТЬ"),
            ("ui.esc_menu.button.settings", "НАСТРОЙКИ"),
            ("ui.esc_menu.button.rescue", "СПАСЕНИЕ"),
            ("ui.esc_menu.button.exit", "ВЫХОД В МЕНЮ"),
            ("ui.esc_menu.root_title", "МЕНЮ"),

            // CustomisationWindow
            ("ui.custom.title", "Внешность"),
            ("ui.custom.section.body", "Пол"),
            ("ui.custom.body.male", "МУЖСКОЙ"),
            ("ui.custom.body.male_desc", "Базовая модель HumanM"),
            ("ui.custom.body.female", "ЖЕНСКИЙ"),
            ("ui.custom.body.female_desc", "Базовая модель HumanF"),
            ("ui.custom.section.proportions", "Пропорции"),
            ("ui.custom.label.height", "Рост"),
            ("ui.custom.label.fullness", "Полнота"),
            ("ui.custom.btn.reset_proportions", "СБРОСИТЬ"),
            ("ui.custom.section.skin", "Цвет кожи"),
            ("ui.custom.btn.reset_skin", "СБРОСИТЬ ЦВЕТ"),
            ("ui.custom.btn.close", "ЗАКРЫТЬ"),

            // InventoryWheel
            ("ui.inventory.wheel_title", "ИНВЕНТАРЬ"),
            ("ui.inventory.wheel_hint", "TAB — закрыть • Клик по сектору — список предметов"),
            ("ui.inventory.sector.resources", "РЕСУРСЫ"),
            ("ui.inventory.sector.equipment", "ВЛАДЕНИЕ"),
            ("ui.inventory.sector.food", "ЕДА"),
            ("ui.inventory.sector.fuel", "ТОПЛИВО"),
            ("ui.inventory.sector.antigrav", "АНТИГРАВИЙ"),
            ("ui.inventory.sector.meziy", "МЕЗИЙ"),
            ("ui.inventory.sector.medical", "МЕДИКАМЕНТЫ"),
            ("ui.inventory.sector.tech", "ТЕХНИКА"),
            ("ui.inventory.sublist_placeholder", "Выберите сектор"),
            ("ui.inventory.btn.use", "ИСПОЛЬЗОВАТЬ"),
            ("ui.inventory.btn.drop", "БРОСИТЬ"),
            ("ui.inventory.btn.close", "ЗАКРЫТЬ"),
            ("ui.inventory.welcome", "Откройте инвентарь по TAB"),

            // CharacterWindow
            ("ui.character.player_name_default", "Игрок"),
            ("ui.character.btn.customisation", "ИЗМЕНИТЬ ВНЕШНОСТЬ"),
            ("ui.character.tab.character", "ПЕРСОНАЖ"),
            ("ui.character.tab.ship", "КОРАБЛЬ"),
            ("ui.character.tab.knowledge", "ЗНАНИЯ"),
            ("ui.character.tab.contracts", "КОНТРАКТЫ"),
            ("ui.character.tab.inventory", "ИНВЕНТАРЬ"),
            ("ui.character.tab.quests", "КВЕСТЫ"),
            ("ui.character.section.clothing", "Одежда"),
            ("ui.character.section.modules", "Модули"),
            ("ui.character.section.stats", "Характеристики"),
            ("ui.character.section.combat_skills", "Изученные боевые навыки"),
            ("ui.character.btn.skill_tree", "ИЗУЧИТЬ НАВЫК"),
            ("ui.character.section.social_skills", "Социальные навыки"),
            ("ui.character.section.my_ships", "Мои корабли"),
            ("ui.character.ship_empty", "Нет доступных кораблей. Найдите ключ в мире."),
            ("ui.character.section.cargo", "Груз"),
            ("ui.character.section.installed_modules", "Установленные модули"),
            ("ui.character.section.knowledge", "Знания"),
            ("ui.character.knowledge.factions", "— Фракции"),
            ("ui.character.knowledge.npc", "— Отношения к NPC"),
            ("ui.character.knowledge.skills", "— Навыки"),
            ("ui.character.knowledge.recipes", "— Рецепты"),
            ("ui.character.section.item_desc", "Описание предмета"),
            ("ui.character.section.quests", "Квесты"),
            ("ui.character.quests.active", "— Активные"),
            ("ui.character.quests.completed", "— Завершённые"),
            ("ui.character.quests.failed", "— Провалено"),
            ("ui.character.quests.discovered", "— Найдено"),
            ("ui.character.btn.reject", "ОТКАЗАТЬСЯ"),
            ("ui.character.btn.accept", "ПРИНЯТЬ"),
            ("ui.character.btn.close", "ЗАКРЫТЬ"),
            ("ui.character.welcome", "Откройте меню персонажа"),

            // CraftingWindow
            ("ui.crafting.station_default", "Станция"),
            ("ui.crafting.section.recipes", "Рецепты"),
            ("ui.crafting.recipe_placeholder", "Выберите рецепт"),
            ("ui.crafting.section.ingredients", "Ингредиенты:"),
            ("ui.crafting.section.buffer", "В буфере:"),
            ("ui.crafting.btn.start", "Начать крафт"),
            ("ui.crafting.btn.cancel", "Отменить"),
            ("ui.crafting.btn.collect", "Забрать"),

            // ShipCargoConsoleWindow
            ("ui.cargo.title", "Грузовой отсек"),
            ("ui.cargo.col.inventory", "Инвентарь игрока"),
            ("ui.cargo.col.hold", "Трюм корабля"),
            ("ui.cargo.label.packs", "Паков:"),
            ("ui.cargo.btn.store", "→ В трюм"),
            ("ui.cargo.btn.retrieve", "← Из трюма"),
            ("ui.cargo.status.ready", "Готов"),

            // KeybindingsWindow
            ("ui.keybindings.title", "Настройки клавиш"),
            ("ui.keybindings.section.skills", "Боевые навыки"),
            ("ui.keybindings.section.actions", "Действия"),
            ("ui.keybindings.footer", "Сохранение автоматическое. Кликните строку чтобы изменить клавишу."),

            // SkillBindingWindow
            ("ui.skillbinding.title", "Навыки на слотах"),
            ("ui.skillbinding.subtitle", "Кликните ✎ на слоте чтобы выбрать навык для него. ✕ очищает слот."),
            ("ui.skillbinding.modal_title", "Выберите навык"),

            // SkillTreeWindow
            ("ui.skilltree.title", "Дерево навыков"),
            ("ui.skilltree.chip_all", "Все"),
            ("ui.skilltree.section.slots", "Слоты"),
            ("ui.skilltree.section.skills", "Навыки"),
            ("ui.skilltree.detail_placeholder", "Выберите навык слева"),
            ("ui.skilltree.required", "Требуется:"),
            ("ui.skilltree.unlocks", "Откроет:"),
            ("ui.skilltree.btn.learn", "Изучить"),
            ("ui.skilltree.btn.forget", "Забыть"),
            ("ui.skilltree.btn.close", "Закрыть"),

            // RepairManagerWindow
            ("ui.repair.title", "🛠 Ремонтный Менеджер"),
            ("ui.repair.label.ship", "Корабль:"),
            ("ui.repair.btn.recall", "🚁 Вызвать"),
            ("ui.repair.label.hull", "Прочность: —"),
            ("ui.repair.btn.repair", "🔧 Починить"),
            ("ui.repair.label.slot", "Слот модуля:"),
            ("ui.repair.label.installed", "Установлено: —"),
            ("ui.repair.label.available_modules", "Доступные модули:"),
            ("ui.repair.label.paint", "🎨 Цвет корабля:"),
            ("ui.repair.label.paint_cost", "Стоимость: —"),
            ("ui.repair.btn.paint", "🎨 Покрасить"),
            ("ui.repair.label.credits", "💰 Кредиты: —"),

            // CommPanel
            ("ui.docking.station_dispatcher", "{0} — Диспетчерская"),
            ("ui.docking.btn.request_landing", "Запросить посадку"),
            ("ui.docking.btn.cancel", "Отмена"),

            // QuestTracker
            ("ui.quest.objective_prefix", "Цель: —"),
            ("ui.quest.btn.hide", "Скрыть"),

            // RebindPromptWindow
            ("ui.rebind.title", "Переназначение клавиши:"),
            ("ui.rebind.hint", "нажмите клавишу"),
            ("ui.rebind.cancel_hint", "(Esc — отмена)"),
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
            }
        }

        EditorUtility.SetDirty(shared);
        EditorUtility.SetDirty(ruTable);
        AssetDatabase.SaveAssets();
        Debug.Log($"[AddKeys] Done. Added {added} new keys to UI_Table (total: {shared.Entries.Count}).");
    }
}
