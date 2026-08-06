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
    }
}


