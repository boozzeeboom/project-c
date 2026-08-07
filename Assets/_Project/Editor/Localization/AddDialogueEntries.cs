// T-Q18: One-shot script to populate Dialogue_Table from NpcDefinition, DialogTree, QuestDefinition assets.
using UnityEngine;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine.Localization.Tables;

namespace ProjectC.Localization.Editor
{
    public static class AddDialogueEntries
    {
        [MenuItem("ProjectC/Localization/Add Dialogue Entries (T-Q18)")]
        public static void Execute()
        {
            var sharedDataPath = "Assets/_Project/Settings/Localization/Dialogue_Table Shared Data.asset";
            var ruTablePath = "Assets/_Project/Settings/Localization/Dialogue_Table_ru.asset";

            var sharedData = AssetDatabase.LoadAssetAtPath<SharedTableData>(sharedDataPath);
            if (sharedData == null)
            {
                Debug.LogError($"[AddDialogueEntries] SharedTableData not found at {sharedDataPath}");
                return;
            }

            var ruTable = AssetDatabase.LoadAssetAtPath<StringTable>(ruTablePath);
            if (ruTable == null)
            {
                Debug.LogError($"[AddDialogueEntries] StringTable (ru) not found at {ruTablePath}");
                return;
            }

            int added = 0;

            // ── NPC: Mira ──
            added += Add(sharedData, ruTable, "dialogue.npc.mira_01.displayName", "Мира Тихоступ");
            added += Add(sharedData, ruTable, "dialogue.npc.mira_01.greeting", "Приветствую, искатель знаний.");

            // ── DialogTree: MiraDefault ──
            added += Add(sharedData, ruTable, "dialogue.tree.mira_default.displayName", "Мира — обычный разговор");
            added += Add(sharedData, ruTable, "dialogue.tree.mira_default.node.greeting", "вааываыва");

            // ── DialogTree: DialogTree_New ──
            added += Add(sharedData, ruTable, "dialogue.tree.DialogTree_New.displayName", "New Dialog");
            added += Add(sharedData, ruTable, "dialogue.tree.DialogTree_New.node.greeting", "Hello!");

            // ── Quest: collect_copper_ore ──
            added += Add(sharedData, ruTable, "dialogue.quest.collect_copper_ore.displayName", "Собрать 3 медных руды");
            added += Add(sharedData, ruTable, "dialogue.quest.collect_copper_ore.description", "Соберите 3 куска медной руды");

            EditorUtility.SetDirty(sharedData);
            EditorUtility.SetDirty(ruTable);
            AssetDatabase.SaveAssets();

            Debug.Log($"[AddDialogueEntries] Done. Added {added} entries to Dialogue_Table.");
        }

        [MenuItem("ProjectC/Localization/Add Missing UI Keys (character)")]
        public static void AddMissingUIKeys()
        {
            var sharedDataPath = "Assets/_Project/Settings/Localization/UI_Table Shared Data.asset";
            var ruTablePath = "Assets/_Project/Settings/Localization/UI_Table_ru.asset";
            var enTablePath = "Assets/_Project/Settings/Localization/UI_Table_en.asset";

            var sharedData = AssetDatabase.LoadAssetAtPath<SharedTableData>(sharedDataPath);
            var ruTable = AssetDatabase.LoadAssetAtPath<StringTable>(ruTablePath);
            var enTable = AssetDatabase.LoadAssetAtPath<StringTable>(enTablePath);

            int added = 0;
            added += AddIfMissing(sharedData, ruTable, enTable, "ui.character.active_available", "Активных: {0}, доступных: {1}", "Active: {0}, available: {1}");
            added += AddIfMissing(sharedData, ruTable, enTable, "ui.character.factions_count", "Фракций: {0}", "Factions: {0}");
            added += AddIfMissing(sharedData, ruTable, enTable, "ui.character.attitudes_count", "Отношений: {0}", "Attitudes: {0}");
            added += AddIfMissing(sharedData, ruTable, enTable, "ui.character.active_contracts_count", "Активных контрактов: {0}", "Active contracts: {0}");

            EditorUtility.SetDirty(sharedData);
            EditorUtility.SetDirty(ruTable);
            EditorUtility.SetDirty(enTable);
            AssetDatabase.SaveAssets();

            Debug.Log($"[AddMissingUIKeys] Done. Added {added} entries.");
        }

        private static int AddIfMissing(SharedTableData shared, StringTable ru, StringTable en, string key, string ruVal, string enVal)
        {
            if (shared.GetEntry(key) != null)
            {
                Debug.Log($"[AddMissingUIKeys] SKIP (exists): {key}");
                return 0;
            }
            shared.AddKey(key);
            ru.AddEntry(key, ruVal);
            en.AddEntry(key, enVal);
            Debug.Log($"[AddMissingUIKeys] ADD: {key}");
            return 1;
        }

        private static int Add(SharedTableData sharedData, StringTable ruTable, string key, string ruValue)
        {
            // Skip if key already exists
            if (sharedData.GetEntry(key) != null)
            {
                Debug.Log($"[AddDialogueEntries] SKIP (exists): {key}");
                return 0;
            }

            sharedData.AddKey(key);
            ruTable.AddEntry(key, ruValue);
            Debug.Log($"[AddDialogueEntries] ADD: {key} = {ruValue}");

            return 1;
        }

        [MenuItem("ProjectC/Localization/Add CharacterWindow Tab Keys")]
        public static void AddCharacterUITabKeys()
        {
            var sharedPath = "Assets/_Project/Settings/Localization/UI_Table Shared Data.asset";
            var ruPath = "Assets/_Project/Settings/Localization/UI_Table_ru.asset";
            var enPath = "Assets/_Project/Settings/Localization/UI_Table_en.asset";

            var shared = AssetDatabase.LoadAssetAtPath<SharedTableData>(sharedPath);
            var ru = AssetDatabase.LoadAssetAtPath<StringTable>(ruPath);
            var en = AssetDatabase.LoadAssetAtPath<StringTable>(enPath);

            int added = 0;
            added += AddIfMissing(shared, ru, en, "ui.character.tab.character", "Персонаж", "Character");
            added += AddIfMissing(shared, ru, en, "ui.character.tab.ship", "Корабль", "Ship");
            added += AddIfMissing(shared, ru, en, "ui.character.tab.knowledge", "Знания", "Knowledge");
            added += AddIfMissing(shared, ru, en, "ui.character.tab.contracts", "Контракты", "Contracts");
            added += AddIfMissing(shared, ru, en, "ui.character.tab.inventory", "Инвентарь", "Inventory");
            added += AddIfMissing(shared, ru, en, "ui.character.tab.quests", "Квесты", "Quests");
            added += AddIfMissing(shared, ru, en, "ui.character.label.player", "Игрок", "Player");
            added += AddIfMissing(shared, ru, en, "ui.character.btn.customisation", "Изменить внешность", "Change Appearance");
            added += AddIfMissing(shared, ru, en, "ui.character.section.knowledge", "Знания", "Knowledge");
            added += AddIfMissing(shared, ru, en, "ui.character.section.quests", "Квесты", "Quests");
            added += AddIfMissing(shared, ru, en, "ui.character.section.clothing", "Одежда", "Clothing");
            added += AddIfMissing(shared, ru, en, "ui.character.section.modules", "Модули", "Modules");
            added += AddIfMissing(shared, ru, en, "ui.character.section.stats", "Характеристики", "Stats");
            added += AddIfMissing(shared, ru, en, "ui.character.section.combat", "Боевые навыки", "Combat Skills");
            added += AddIfMissing(shared, ru, en, "ui.character.section.social", "Социальные навыки", "Social Skills");
            added += AddIfMissing(shared, ru, en, "ui.character.knowledge.factions", "Фракции", "Factions");
            added += AddIfMissing(shared, ru, en, "ui.character.knowledge.npc", "NPC", "NPCs");
            added += AddIfMissing(shared, ru, en, "ui.character.knowledge.skills", "Навыки", "Skills");
            added += AddIfMissing(shared, ru, en, "ui.character.knowledge.recipes", "Рецепты", "Recipes");
            added += AddIfMissing(shared, ru, en, "ui.character.quests.active", "Активные", "Active");
            added += AddIfMissing(shared, ru, en, "ui.character.quests.completed", "Завершённые", "Completed");
            added += AddIfMissing(shared, ru, en, "ui.character.quests.failed", "Проваленные", "Failed");
            added += AddIfMissing(shared, ru, en, "ui.character.quests.discovered", "Найденные", "Discovered");
            added += AddIfMissing(shared, ru, en, "ui.character.location", "Локация: —", "Location: —");

            EditorUtility.SetDirty(shared);
            EditorUtility.SetDirty(ru);
            EditorUtility.SetDirty(en);
            AssetDatabase.SaveAssets();
            Debug.Log($"[AddCharacterUITabKeys] Done. Added {added} entries.");
        }

        [MenuItem("ProjectC/Localization/Add MarketWindow Keys")]
        public static void AddMarketWindowKeys()
        {
            var sharedPath = "Assets/_Project/Settings/Localization/UI_Table Shared Data.asset";
            var ruPath = "Assets/_Project/Settings/Localization/UI_Table_ru.asset";
            var enPath = "Assets/_Project/Settings/Localization/UI_Table_en.asset";

            var shared = AssetDatabase.LoadAssetAtPath<SharedTableData>(sharedPath);
            var ru = AssetDatabase.LoadAssetAtPath<StringTable>(ruPath);
            var en = AssetDatabase.LoadAssetAtPath<StringTable>(enPath);

            int added = 0;
            // Tabs
            added += AddIfMissing(shared, ru, en, "ui.market.tab.market", "Рынок", "Market");
            added += AddIfMissing(shared, ru, en, "ui.market.tab.warehouse", "Склад / Трюм", "Warehouse / Hold");
            added += AddIfMissing(shared, ru, en, "ui.market.tab.contracts", "Контракты", "Contracts");
            added += AddIfMissing(shared, ru, en, "ui.market.tab.exchanger", "Обменник", "Exchanger");
            // Action buttons
            added += AddIfMissing(shared, ru, en, "ui.market.btn.buy", "Купить", "Buy");
            added += AddIfMissing(shared, ru, en, "ui.market.btn.sell", "Продать", "Sell");
            added += AddIfMissing(shared, ru, en, "ui.market.btn.load", "Погрузить", "Load");
            added += AddIfMissing(shared, ru, en, "ui.market.btn.unload", "Разгрузить", "Unload");
            added += AddIfMissing(shared, ru, en, "ui.market.btn.accept", "Взять", "Accept");
            added += AddIfMissing(shared, ru, en, "ui.market.btn.complete", "Сдать", "Complete");
            added += AddIfMissing(shared, ru, en, "ui.market.btn.fail", "Провалить", "Fail");
            added += AddIfMissing(shared, ru, en, "ui.market.btn.close", "Закрыть", "Close");
            // Labels
            added += AddIfMissing(shared, ru, en, "ui.market.label.welcome", "Откройте рынок, чтобы торговать", "Open the market to trade");

            EditorUtility.SetDirty(shared);
            EditorUtility.SetDirty(ru);
            EditorUtility.SetDirty(en);
            AssetDatabase.SaveAssets();
            Debug.Log($"[AddMarketWindowKeys] Done. Added {added} entries.");
        }

        [MenuItem("ProjectC/Localization/Add Quest & Exchange Keys")]
        public static void AddQuestExchangeKeys()
        {
            var sharedPath = "Assets/_Project/Settings/Localization/UI_Table Shared Data.asset";
            var ruPath = "Assets/_Project/Settings/Localization/UI_Table_ru.asset";
            var enPath = "Assets/_Project/Settings/Localization/UI_Table_en.asset";

            var shared = AssetDatabase.LoadAssetAtPath<SharedTableData>(sharedPath);
            var ru = AssetDatabase.LoadAssetAtPath<StringTable>(ruPath);
            var en = AssetDatabase.LoadAssetAtPath<StringTable>(enPath);

            int added = 0;
            // Quest toasts
            added += AddIfMissing(shared, ru, en, "ui.quest.toast_discovered", "✨ Найден квест: {0}", "✨ Quest discovered: {0}");
            added += AddIfMissing(shared, ru, en, "ui.quest.cant_accept", "Не удалось взять квест", "Could not accept quest");
            added += AddIfMissing(shared, ru, en, "ui.quest.give_item", "📦 +1 предмет", "📦 +1 item");
            added += AddIfMissing(shared, ru, en, "ui.quest.give_item_named", "📦 +1 {0}", "📦 +1 {0}");
            added += AddIfMissing(shared, ru, en, "ui.quest.take_item", "📦 -1 предмет", "📦 -1 item");
            added += AddIfMissing(shared, ru, en, "ui.quest.take_item_named", "📦 -1 {0}", "📦 -1 {0}");
            added += AddIfMissing(shared, ru, en, "ui.quest.credits_gained", "💰 +{0} CR", "💰 +{0} CR");
            added += AddIfMissing(shared, ru, en, "ui.quest.reputation_gained", "📈 Репутация +{0}", "📈 Reputation +{0}");
            added += AddIfMissing(shared, ru, en, "ui.quest.reputation_faction_gained", "📈 {0} +{1}", "📈 {0} +{1}");
            added += AddIfMissing(shared, ru, en, "ui.quest.attitude_gained", "💚 Отношение +{0}", "💚 Attitude +{0}");
            added += AddIfMissing(shared, ru, en, "ui.quest.attitude_npc_gained", "💚 {0} +{1}", "💚 {0} +{1}");
            added += AddIfMissing(shared, ru, en, "ui.quest.objective_done", "✅ Цель выполнена", "✅ Objective completed");
            added += AddIfMissing(shared, ru, en, "ui.quest.objective_done_named", "✅ {0}", "✅ {0}");
            // Exchange market
            added += AddIfMissing(shared, ru, en, "ui.market.packs_suffix", "пач.", "packs");
            added += AddIfMissing(shared, ru, en, "ui.market.boxed_suffix", "ящ.", "boxes");
            // CharacterWindow buttons
            added += AddIfMissing(shared, ru, en, "ui.character.btn.learn_skill", "Изучить навык", "Learn Skill");

            EditorUtility.SetDirty(shared);
            EditorUtility.SetDirty(ru);
            EditorUtility.SetDirty(en);
            AssetDatabase.SaveAssets();
            Debug.Log($"[AddQuestExchangeKeys] Done. Added {added} entries.");
        }

        [MenuItem("ProjectC/Localization/Add Repair Manager Keys")]
        public static void AddRepairManagerKeys()
        {
            var sharedPath = "Assets/_Project/Settings/Localization/UI_Table Shared Data.asset";
            var ruPath = "Assets/_Project/Settings/Localization/UI_Table_ru.asset";
            var enPath = "Assets/_Project/Settings/Localization/UI_Table_en.asset";

            var shared = AssetDatabase.LoadAssetAtPath<SharedTableData>(sharedPath);
            var ru = AssetDatabase.LoadAssetAtPath<StringTable>(ruPath);
            var en = AssetDatabase.LoadAssetAtPath<StringTable>(enPath);

            int added = 0;
            added += AddIfMissing(shared, ru, en, "ui.common.credits", "кр.", "cr.");
            added += AddIfMissing(shared, ru, en, "ui.repair.credits_label", "💰 Кредиты: {0}", "💰 Credits: {0}");
            added += AddIfMissing(shared, ru, en, "ui.repair.ship_class", "Класс: {0}", "Class: {0}");
            added += AddIfMissing(shared, ru, en, "ui.repair.ship_power", "Энергия: {0}/{1}", "Power: {0}/{1}");
            added += AddIfMissing(shared, ru, en, "ui.repair.hull_broken", "Прочность: СЛОМАН ({0}/{1})", "Hull: BROKEN ({0}/{1})");
            added += AddIfMissing(shared, ru, en, "ui.repair.hull_normal", "Прочность: {0}/{1}", "Hull: {0}/{1}");
            added += AddIfMissing(shared, ru, en, "ui.repair.hull_repair_btn", "🔧 Починить ({0} кр.)", "🔧 Repair ({0} cr.)");
            added += AddIfMissing(shared, ru, en, "ui.repair.hull_ok_btn", "✓ Целый", "✓ Intact");
            added += AddIfMissing(shared, ru, en, "ui.repair.insufficient_funds", "Недостаточно кредитов! Нужно {0}, есть {1}", "Insufficient credits! Need {0}, have {1}");
            added += AddIfMissing(shared, ru, en, "ui.repair.recalled", "Корабль вызван на пад {0}...", "Ship recalled to pad {0}...");
            added += AddIfMissing(shared, ru, en, "ui.repair.installed_label", "Установлено: {0}", "Installed: {0}");
            added += AddIfMissing(shared, ru, en, "ui.repair.sell_btn", "💰 Продать (+{0} кр.)", "💰 Sell (+{0} cr.)");
            added += AddIfMissing(shared, ru, en, "ui.repair.sell_status", "Продажа модуля из '{0}' (+{1} кр.)...", "Selling module from '{0}' (+{1} cr.)...");
            added += AddIfMissing(shared, ru, en, "ui.repair.modules_for_slot", "Модули для слота '{0}':", "Modules for slot '{0}':");
            added += AddIfMissing(shared, ru, en, "ui.repair.install_request", "Запрос на установку '{0}' в '{1}' отправлен...", "Install request for '{0}' in '{1}' sent...");
            added += AddIfMissing(shared, ru, en, "ui.repair.paint_cost", "Стоимость: {0} кр.", "Cost: {0} cr.");
            added += AddIfMissing(shared, ru, en, "ui.repair.paint_btn", "🎨 Покрасить ({0} кр.)", "🎨 Paint ({0} cr.)");
            added += AddIfMissing(shared, ru, en, "ui.repair.paint_select", "🎨 Выберите цвет", "🎨 Select color");
            // Knowledge toast
            added += AddIfMissing(shared, ru, en, "ui.knowledge.category_skill", "Навык", "Skill");
            added += AddIfMissing(shared, ru, en, "ui.knowledge.category_recipe", "Рецепт", "Recipe");
            added += AddIfMissing(shared, ru, en, "ui.knowledge.category_faction", "Фракция", "Faction");
            // Docking CommPanel
            added += AddIfMissing(shared, ru, en, "ui.docking.dispatcher", "Диспетчерская", "Dispatcher");
            added += AddIfMissing(shared, ru, en, "ui.docking.station_dispatcher", "{0} — Диспетчерская", "{0} — Dispatcher");
            added += AddIfMissing(shared, ru, en, "ui.docking.btn.request_landing", "Запросить посадку", "Request Landing");
            added += AddIfMissing(shared, ru, en, "ui.docking.btn.cancel", "Отмена", "Cancel");
            added += AddIfMissing(shared, ru, en, "ui.docking.btn.cancel_request", "Отменить запрос", "Cancel Request");
            added += AddIfMissing(shared, ru, en, "ui.docking.btn.undock", "Отстыковка", "Undock");
            added += AddIfMissing(shared, ru, en, "ui.docking.btn.close", "Закрыть", "Close");
            added += AddIfMissing(shared, ru, en, "ui.docking.btn.repark", "Перепарковаться", "Repark");
            added += AddIfMissing(shared, ru, en, "ui.docking.btn.ok", "Хорошо", "OK");
            added += AddIfMissing(shared, ru, en, "ui.docking.btn.abort", "Отбой", "Abort");
            added += AddIfMissing(shared, ru, en, "ui.docking.assignment", "Диспетчер: «{0} Подход: высота {1}, курс {2}. Окно: {3} сек. Подтверждаете?»", "Dispatcher: «{0} Approach: altitude {1}, heading {2}. Window: {3}s. Confirm?»");
            added += AddIfMissing(shared, ru, en, "ui.docking.wrong_pad", "Диспетчер: «Борт, вы на чужом pad'е (#{0}). Перепаркуйтесь».", "Dispatcher: «Ship, you are on the wrong pad (#{0}). Repark.»");
            added += AddIfMissing(shared, ru, en, "ui.docking.msg.idle", "Диспетчер: «На связи, жду ваших распоряжений».", "Dispatcher: «On standby, waiting for your orders.»");
            added += AddIfMissing(shared, ru, en, "ui.docking.msg.assigned", "Диспетчер: «Борт, добро. Следуйте к pad #{0}».", "Dispatcher: «Ship, good. Proceed to pad #{0}.»");
            added += AddIfMissing(shared, ru, en, "ui.docking.msg.docked", "Диспетчер: «Стыковка зафиксирована. Двигатели заблокированы. Удачной торговли».", "Dispatcher: «Docking confirmed. Engines locked. Happy trading.»");
            added += AddIfMissing(shared, ru, en, "ui.docking.msg.wrong_pad", "Диспетчер: «Борт, вы на чужом pad'е (#{0}). Перепаркуйтесь».", "Dispatcher: «Ship, you are on the wrong pad (#{0}). Repark.»");
            added += AddIfMissing(shared, ru, en, "ui.docking.msg.cancelled", "Диспетчер: «Окно посадки истекло. Повторите запрос».", "Dispatcher: «Landing window expired. Repeat request.»");
            added += AddIfMissing(shared, ru, en, "ui.docking.fail.no_suitable_pad", "Диспетчер: «Свободных pad'ов нет, попробуйте позже».", "Dispatcher: «No free pads, try later.»");
            added += AddIfMissing(shared, ru, en, "ui.docking.fail.rate_limited", "Диспетчер: «Слишком частые запросы, подождите».", "Dispatcher: «Too many requests, wait.»");
            added += AddIfMissing(shared, ru, en, "ui.docking.fail.station_full", "Диспетчер: «Станция переполнена, попробуйте позже».", "Dispatcher: «Station full, try later.»");
            added += AddIfMissing(shared, ru, en, "ui.docking.fail.station_not_found", "Диспетчер: «Связь потеряна, повторите».", "Dispatcher: «Connection lost, repeat.»");
            added += AddIfMissing(shared, ru, en, "ui.docking.fail.ship_not_found", "Диспетчер: «Корабль не найден, подойдите ближе».", "Dispatcher: «Ship not found, come closer.»");
            added += AddIfMissing(shared, ru, en, "ui.docking.fail.not_your_ship", "Диспетчер: «Это не ваш корабль».", "Dispatcher: «This is not your ship.»");
            added += AddIfMissing(shared, ru, en, "ui.docking.fail.unknown", "Диспетчер: «Ошибка: {0}».", "Dispatcher: «Error: {0}».");
            // QuestTracker objectives
            added += AddIfMissing(shared, ru, en, "ui.quest.objective_none", "Цель: (нет целей)", "Objective: (none)");
            added += AddIfMissing(shared, ru, en, "ui.quest.objective_counter", "Цель: {0} ({1}/{2})", "Objective: {0} ({1}/{2})");
            added += AddIfMissing(shared, ru, en, "ui.quest.objective_simple", "Цель: {0}", "Objective: {0}");
            added += AddIfMissing(shared, ru, en, "ui.quest.objective_completed", "Цель: ({0}/{1}) выполнено", "Objective: ({0}/{1}) done");
            // QuestWorld prerequisites
            added += AddIfMissing(shared, ru, en, "ui.quest.prereq.complete_first", "Сначала выполните квест «{0}»", "Complete quest «{0}» first");
            added += AddIfMissing(shared, ru, en, "ui.quest.prereq.activate_first", "Сначала активируйте квест «{0}»", "Activate quest «{0}» first");
            added += AddIfMissing(shared, ru, en, "ui.quest.prereq.reputation", "Нужна репутация {0} ≥ {1}", "Need reputation {0} ≥ {1}");
            added += AddIfMissing(shared, ru, en, "ui.quest.prereq.npc_attitude", "Нужно отношение с NPC «{0}» ≥ {1}", "Need attitude with NPC «{0}» ≥ {1}");
            added += AddIfMissing(shared, ru, en, "ui.quest.prereq.have_item", "Нужен предмет «{0}» ×{1}", "Need item «{0}» ×{1}");
            added += AddIfMissing(shared, ru, en, "ui.quest.prereq.flag", "Не выполнено условие «{0}»", "Condition «{0}» not met");
            added += AddIfMissing(shared, ru, en, "ui.quest.prereq.unknown", "Не выполнено условие #{0}", "Prerequisite #{0} not met");
            // Crafting
            added += AddIfMissing(shared, ru, en, "ui.crafting.progress_label", "Крафт…", "Crafting…");
            added += AddIfMissing(shared, ru, en, "ui.crafting.crafting_with_name", "Крафт: {0}", "Crafting: {0}");
            added += AddIfMissing(shared, ru, en, "ui.crafting.completed", "✅ Готово: {0}", "✅ Done: {0}");
            added += AddIfMissing(shared, ru, en, "ui.crafting.item_fallback", "Предмет", "Item");
            added += AddIfMissing(shared, ru, en, "ui.crafting.denied", "❌ {0}", "❌ {0}");
            added += AddIfMissing(shared, ru, en, "ui.crafting.denied_fallback", "Отказано", "Denied");
            added += AddIfMissing(shared, ru, en, "ui.crafting.cancelled", "Крафт отменён", "Crafting cancelled");
            added += AddIfMissing(shared, ru, en, "ui.crafting.interrupted", "⚠ {0}", "⚠ {0}");
            added += AddIfMissing(shared, ru, en, "ui.crafting.interrupted_fallback", "Прервано", "Interrupted");
            added += AddIfMissing(shared, ru, en, "ui.crafting.started", "Крафт запущен…", "Crafting started…");
            added += AddIfMissing(shared, ru, en, "ui.crafting.collecting", "Забираете результат…", "Collecting result…");
            added += AddIfMissing(shared, ru, en, "ui.crafting.station_default", "Станция", "Station");
            added += AddIfMissing(shared, ru, en, "ui.crafting.select_recipe", "Выберите рецепт", "Select recipe");
            added += AddIfMissing(shared, ru, en, "ui.crafting.station_switched", "Станция переключена", "Station switched");
            added += AddIfMissing(shared, ru, en, "ui.crafting.btn.all", "Все", "All");
            added += AddIfMissing(shared, ru, en, "ui.crafting.added_item", "Добавлено: {0} × {1}", "Added: {0} × {1}");
            added += AddIfMissing(shared, ru, en, "ui.crafting.select_recipe_hint", "Выберите рецепт и добавьте ингредиенты", "Select a recipe and add ingredients");
            // CharacterWindow extra
            added += AddIfMissing(shared, ru, en, "ui.character.credits_label", "Кредиты: {0:F0} CR", "Credits: {0:F0} CR");
            added += AddIfMissing(shared, ru, en, "ui.character.inv_type", "Тип: {0}", "Type: {0}");
            added += AddIfMissing(shared, ru, en, "ui.character.inv_weight", "Вес: {0:F1} кг", "Weight: {0:F1} kg");
            added += AddIfMissing(shared, ru, en, "ui.character.skill_prereq", "Нужно: {0}", "Requires: {0}");
            added += AddIfMissing(shared, ru, en, "ui.character.quests_not_implemented", "Квесты ещё не реализованы", "Quests not yet implemented");
            added += AddIfMissing(shared, ru, en, "ui.character.no_quests", "Нет квестов в журнале", "No quests in journal");
            added += AddIfMissing(shared, ru, en, "ui.character.quests_summary", "Активных: {0} | Завершённых: {1} | Провалено: {2} | Найдено: {3}", "Active: {0} | Completed: {1} | Failed: {2} | Discovered: {3}");
            added += AddIfMissing(shared, ru, en, "ui.character.new_quest", "Новый квест: {0}", "New quest: {0}");
            added += AddIfMissing(shared, ru, en, "ui.character.select_discovered", "Выберите квест в секции 'Найденные' для принятия", "Select a quest in 'Discovered' section to accept");
            added += AddIfMissing(shared, ru, en, "ui.character.accept_request", "Запрос на принятие '{0}' отправлен...", "Accept request for '{0}' sent...");
            // Keybindings
            added += AddIfMissing(shared, ru, en, "ui.keybindings.rebind_skill_title", "Переназначение навыка:", "Remap skill:");
            added += AddIfMissing(shared, ru, en, "ui.keybindings.rebind_key_title", "Переназначение клавиши:", "Remap key:");
            added += AddIfMissing(shared, ru, en, "ui.keybindings.rebind_hint", "«{0}» — нажмите клавишу", "«{0}» — press a key");
            added += AddIfMissing(shared, ru, en, "ui.keybindings.select_skill_for_slot", "Выберите навык для слота {0}", "Select a skill for slot {0}");
            // NetworkUI
            added += AddIfMissing(shared, ru, en, "ui.network.players_count", "Игроков: {0}", "Players: {0}");
            added += AddIfMissing(shared, ru, en, "ui.network.disconnected", "Отключено", "Disconnected");

            EditorUtility.SetDirty(shared);
            EditorUtility.SetDirty(ru);
            EditorUtility.SetDirty(en);
            AssetDatabase.SaveAssets();
            Debug.Log($"[AddRepairManagerKeys] Done. Added {added} entries.");
        }
    }
}




