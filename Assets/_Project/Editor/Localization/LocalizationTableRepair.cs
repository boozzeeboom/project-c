// LocalizationTableRepair.cs — Phase A: Rebuild SharedData + tables with proper API
// Clears orphan entries, re-fills using sharedData.AddKey → table.AddEntry(sharedEntry.Id, value)
// Menu: ProjectC → Localization → Rebuild Tables (SharedData fix)
using UnityEngine;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine.Localization.Tables;
using System.Collections.Generic;

namespace ProjectC.Localization.Editor
{
    public static class LocalizationTableRepair
    {
        private const string SETTINGS_PATH = "Assets/_Project/Settings/Localization";

        [MenuItem("ProjectC/Localization/Rebuild Tables (SharedData fix)")]
        public static void Execute()
        {
            Debug.Log("[Repair] ===== Starting SharedData repair =====");
            AssetDatabase.StartAssetEditing();

            try
            {
                RepairStaticTable();
                RepairSystemTable();
                RepairUITable();
                RepairDialogueTable();
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            Debug.Log("[Repair] ===== SharedData repair COMPLETE =====");
        }

        // ============================================================
        // STATIC TABLE — re-run migrator with fixed API
        // ============================================================
        private static void RepairStaticTable()
        {
            var collection = LoadCollection("Static_Table");
            if (collection == null) return;

            var ruTable = LoadTable("Static_Table_ru");
            if (ruTable == null) return;

            ClearAllEntries(collection, ruTable);

            int added = 0;
            added += MigrateTradeItemsFixed(collection, ruTable);
            added += MigrateNpcsFixed(collection, ruTable);
            added += MigrateQuestsFixed(collection, ruTable);
            added += MigrateFactionsFixed(collection, ruTable);
            added += MigrateMarketsFixed(collection, ruTable);
            added += MigrateItemTypesFixed(collection, ruTable);
            added += MigrateSkillsFixed(collection, ruTable);

            MarkDirtyAndSave(collection, ruTable);
            Debug.Log($"[Repair] Static_Table: {added} keys (SharedData.Entries={collection.SharedData.Entries.Count})");
        }

        // ============================================================
        // SYSTEM TABLE — rebuild from enum codes
        // ============================================================
        private static void RepairSystemTable()
        {
            var collection = LoadCollection("System_Table");
            if (collection == null) return;

            var ruTable = LoadTable("System_Table_ru");
            if (ruTable == null) return;

            ClearAllEntries(collection, ruTable);

            int added = 0;
            added += AddSystemEnum(collection, ruTable, "inventory", InventoryResultCodeMap);
            added += AddSystemEnum(collection, ruTable, "contract", ContractResultCodeMap);
            added += AddSystemEnum(collection, ruTable, "market", TradeResultCodeMap);

            MarkDirtyAndSave(collection, ruTable);
            Debug.Log($"[Repair] System_Table: {added} keys (SharedData.Entries={collection.SharedData.Entries.Count})");
        }

        // ============================================================
        // UI TABLE — rebuild from code literals
        // ============================================================
        private static void RepairUITable()
        {
            var collection = LoadCollection("UI_Table");
            if (collection == null) return;

            var ruTable = LoadTable("UI_Table_ru");
            if (ruTable == null) return;

            ClearAllEntries(collection, ruTable);

            int added = 0;
            foreach (var (key, ruValue) in UIKeyMap)
            {
                added += AddEntry(collection, ruTable, key, ruValue);
            }

            MarkDirtyAndSave(collection, ruTable);
            Debug.Log($"[Repair] UI_Table: {added} keys (SharedData.Entries={collection.SharedData.Entries.Count})");
        }

        // ============================================================
        // DIALOGUE TABLE — clear orphan entries, leave empty (T-Q18 skipped)
        // ============================================================
        private static void RepairDialogueTable()
        {
            var collection = LoadCollection("Dialogue_Table");
            if (collection == null) return;

            var ruTable = LoadTable("Dialogue_Table_ru");
            if (ruTable == null) return;

            ClearAllEntries(collection, ruTable);
            MarkDirtyAndSave(collection, ruTable);
            Debug.Log($"[Repair] Dialogue_Table: cleared (0 keys, T-Q18 skipped)");
        }

        // ============================================================
        // HELPERS
        // ============================================================

        private static StringTableCollection LoadCollection(string name)
        {
            var path = $"{SETTINGS_PATH}/{name}.asset";
            var col = AssetDatabase.LoadAssetAtPath<StringTableCollection>(path);
            if (col == null) Debug.LogError($"[Repair] Collection not found: {path}");
            return col;
        }

        private static StringTable LoadTable(string name)
        {
            var path = $"{SETTINGS_PATH}/{name}.asset";
            var table = AssetDatabase.LoadAssetAtPath<StringTable>(path);
            if (table == null) Debug.LogError($"[Repair] Table not found: {path}");
            return table;
        }

        /// <summary>Clear both SharedData entries and table data, then save.</summary>
        private static void ClearAllEntries(StringTableCollection collection, StringTable ruTable)
        {
            // Clear SharedData
            var shared = collection.SharedData;
            if (shared != null)
            {
                // Remove all entries by key
                var keys = new List<string>();
                foreach (var entry in shared.Entries)
                {
                    if (!string.IsNullOrEmpty(entry.Key))
                        keys.Add(entry.Key);
                }
                foreach (var key in keys)
                    shared.RemoveKey(key);

                EditorUtility.SetDirty(shared);
            }

            // Clear table via SerializedObject (m_TableData is managed reference)
            var so = new SerializedObject(ruTable);
            var tableData = so.FindProperty("m_TableData");
            if (tableData != null)
            {
                tableData.ClearArray();
                so.ApplyModifiedProperties();
            }
            EditorUtility.SetDirty(ruTable);
        }

        /// <summary>Add entry using proper API: sharedData.AddKey → table.AddEntry(Id, value).</summary>
        private static int AddEntry(StringTableCollection collection, StringTable ruTable,
            string key, string ruValue)
        {
            if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(ruValue))
                return 0;

            var shared = collection.SharedData;
            var sharedEntry = shared.AddKey(key);
            ruTable.AddEntry(sharedEntry.Id, ruValue);
            return 1;
        }

        private static int AddSystemEnum(StringTableCollection collection, StringTable ruTable,
            string domain, Dictionary<string, string> map)
        {
            int added = 0;
            foreach (var (code, ruValue) in map)
            {
                var key = $"sys.{domain}.{code}";
                added += AddEntry(collection, ruTable, key, ruValue);
            }
            return added;
        }

        private static void MarkDirtyAndSave(StringTableCollection collection, StringTable ruTable)
        {
            EditorUtility.SetDirty(ruTable);
            EditorUtility.SetDirty(collection.SharedData);
            EditorUtility.SetDirty(collection);
        }

        // ============================================================
        // SO MIGRATOR METHODS (fixed API: sharedData.AddKey → table.AddEntry)
        // ============================================================

        private static int MigrateTradeItemsFixed(StringTableCollection collection, StringTable table)
        {
            var guids = AssetDatabase.FindAssets("t:TradeItemDefinition", new[] { "Assets/_Project/Trade/Data/Items" });
            int added = 0;
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var so = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                if (so == null) continue;

                var itemId = GetField(so, "itemId");
                var displayName = GetField(so, "displayName");
                var description = GetField(so, "description");

                if (!string.IsNullOrEmpty(itemId) && !string.IsNullOrEmpty(displayName))
                    added += AddEntry(collection, table, $"static.item.{itemId}.displayName", displayName);
                if (!string.IsNullOrEmpty(itemId) && !string.IsNullOrEmpty(description))
                    added += AddEntry(collection, table, $"static.item.{itemId}.description", description);
            }
            Debug.Log($"[Repair] TradeItems: {added} keys ({guids.Length} assets)");
            return added;
        }

        private static int MigrateNpcsFixed(StringTableCollection collection, StringTable table)
        {
            var guids = AssetDatabase.FindAssets("t:NpcDefinition", new[] { "Assets/_Project" });
            int added = 0;
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var so = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                if (so == null) continue;

                var npcId = GetField(so, "npcId");
                var displayName = GetField(so, "displayName");
                var greetingText = GetField(so, "greetingText");

                if (!string.IsNullOrEmpty(npcId) && !string.IsNullOrEmpty(displayName))
                    added += AddEntry(collection, table, $"static.npc.{npcId}.displayName", displayName);
                if (!string.IsNullOrEmpty(npcId) && !string.IsNullOrEmpty(greetingText))
                    added += AddEntry(collection, table, $"static.npc.{npcId}.greetingText", greetingText);
            }
            Debug.Log($"[Repair] NPCs: {added} keys ({guids.Length} assets)");
            return added;
        }

        private static int MigrateQuestsFixed(StringTableCollection collection, StringTable table)
        {
            var guids = AssetDatabase.FindAssets("t:QuestDefinition", new[] { "Assets/_Project" });
            int added = 0;
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var so = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                if (so == null) continue;

                var questId = GetField(so, "questId");
                var displayName = GetField(so, "displayName");
                var description = GetField(so, "description");

                if (!string.IsNullOrEmpty(questId) && !string.IsNullOrEmpty(displayName))
                    added += AddEntry(collection, table, $"static.quest.{questId}.displayName", displayName);
                if (!string.IsNullOrEmpty(questId) && !string.IsNullOrEmpty(description))
                    added += AddEntry(collection, table, $"static.quest.{questId}.description", description);

                var sso = new SerializedObject(so);
                var stages = sso.FindProperty("stages");
                if (stages != null && stages.isArray)
                {
                    for (int s = 0; s < stages.arraySize; s++)
                    {
                        var stage = stages.GetArrayElementAtIndex(s);
                        var stageId = stage.FindPropertyRelative("stageId")?.stringValue;
                        var stageDesc = stage.FindPropertyRelative("description")?.stringValue;

                        if (!string.IsNullOrEmpty(questId) && !string.IsNullOrEmpty(stageId) && !string.IsNullOrEmpty(stageDesc))
                            added += AddEntry(collection, table, $"static.quest.{questId}.stage.{stageId}.description", stageDesc);

                        var objectives = stage.FindPropertyRelative("objectives");
                        if (objectives != null && objectives.isArray)
                        {
                            for (int o = 0; o < objectives.arraySize; o++)
                            {
                                var obj = objectives.GetArrayElementAtIndex(o);
                                var objId = obj.FindPropertyRelative("objectiveId")?.stringValue;
                                var objDesc = obj.FindPropertyRelative("description")?.stringValue;

                                if (!string.IsNullOrEmpty(questId) && !string.IsNullOrEmpty(stageId) && !string.IsNullOrEmpty(objId) && !string.IsNullOrEmpty(objDesc))
                                    added += AddEntry(collection, table, $"static.quest.{questId}.stage.{stageId}.obj.{objId}", objDesc);
                            }
                        }
                    }
                }
            }
            Debug.Log($"[Repair] Quests: {added} keys ({guids.Length} assets)");
            return added;
        }

        private static int MigrateFactionsFixed(StringTableCollection collection, StringTable table)
        {
            var guids = AssetDatabase.FindAssets("t:FactionDefinition", new[] { "Assets/_Project" });
            int added = 0;
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var so = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                if (so == null) continue;

                var factionId = GetField(so, "factionId");
                var displayName = GetField(so, "displayName");
                var loreDescription = GetField(so, "loreDescription");

                if (!string.IsNullOrEmpty(factionId) && !string.IsNullOrEmpty(displayName))
                    added += AddEntry(collection, table, $"static.faction.{factionId}.displayName", displayName);
                if (!string.IsNullOrEmpty(factionId) && !string.IsNullOrEmpty(loreDescription))
                    added += AddEntry(collection, table, $"static.faction.{factionId}.loreDescription", loreDescription);

                var sso = new SerializedObject(so);
                var tiers = sso.FindProperty("reputationTiers");
                if (tiers != null && tiers.isArray)
                {
                    for (int t = 0; t < tiers.arraySize; t++)
                    {
                        var tier = tiers.GetArrayElementAtIndex(t);
                        var tierName = tier.FindPropertyRelative("tier")?.stringValue;
                        if (!string.IsNullOrEmpty(factionId) && !string.IsNullOrEmpty(tierName))
                            added += AddEntry(collection, table, $"static.faction.{factionId}.tier.{t}", tierName);
                    }
                }
            }
            Debug.Log($"[Repair] Factions: {added} keys ({guids.Length} assets)");
            return added;
        }

        private static int MigrateMarketsFixed(StringTableCollection collection, StringTable table)
        {
            var guids = AssetDatabase.FindAssets("t:MarketConfig", new[] { "Assets/_Project" });
            int added = 0;
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var so = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                if (so == null) continue;

                var marketId = GetField(so, "marketId");
                var displayName = GetField(so, "displayName");

                if (!string.IsNullOrEmpty(marketId) && !string.IsNullOrEmpty(displayName))
                    added += AddEntry(collection, table, $"static.market.{marketId}.displayName", displayName);
            }
            Debug.Log($"[Repair] Markets: {added} keys ({guids.Length} assets)");
            return added;
        }

        private static int MigrateItemTypesFixed(StringTableCollection collection, StringTable table)
        {
            try
            {
                var type = System.Type.GetType("ProjectC.Core.ItemTypeNames, Assembly-CSharp");
                if (type == null) { Debug.LogWarning("[Repair] ItemTypeNames not found"); return 0; }

                var namesField = type.GetField("_names",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                if (namesField == null) { Debug.LogWarning("[Repair] _names field not found"); return 0; }

                var names = namesField.GetValue(null) as string[];
                if (names == null) return 0;

                int added = 0;
                for (int i = 0; i < names.Length; i++)
                {
                    if (!string.IsNullOrEmpty(names[i]))
                        added += AddEntry(collection, table, $"static.item_type.{i}", names[i]);
                }
                Debug.Log($"[Repair] ItemTypes: {added} keys");
                return added;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[Repair] ItemTypes error: {ex.Message}");
                return 0;
            }
        }

        // R2-xxx / LOC-13: навыки — static.skill.{skillId}.displayName / .description
        // Источник: Assets/_Project/Resources/Skills/*.asset (SkillNodeConfig)
        private static int MigrateSkillsFixed(StringTableCollection collection, StringTable table)
        {
            var guids = AssetDatabase.FindAssets("t:SkillNodeConfig", new[] { "Assets/_Project/Resources/Skills" });
            int added = 0;
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var so = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                if (so == null) continue;

                var skillId = GetField(so, "skillId");
                var displayName = GetField(so, "displayName");
                var description = GetField(so, "description");

                if (!string.IsNullOrEmpty(skillId) && !string.IsNullOrEmpty(displayName))
                    added += AddEntry(collection, table, $"static.skill.{skillId}.displayName", displayName);
                if (!string.IsNullOrEmpty(skillId) && !string.IsNullOrEmpty(description))
                    added += AddEntry(collection, table, $"static.skill.{skillId}.description", description);
            }
            Debug.Log($"[Repair] Skills: {added} keys ({guids.Length} assets)");
            return added;
        }

        private static string GetField(ScriptableObject so, string fieldName)
        {
            var sso = new SerializedObject(so);
            var prop = sso.FindProperty(fieldName);
            if (prop == null) return null;
            switch (prop.propertyType)
            {
                case SerializedPropertyType.String:
                    return prop.stringValue;
                case SerializedPropertyType.Integer:
                    return prop.intValue.ToString();
                default:
                    return null;
            }
        }

        // ============================================================
        // SYSTEM ENUM VALUE MAPS (RU values from existing YAML / git history)
        // ============================================================

        private static readonly Dictionary<string, string> InventoryResultCodeMap = new()
        {
            { "ok", "OK" },
            { "not_in_zone", "Слишком далеко от предмета" },
            { "inventory_full", "Инвентарь полон" },
            { "item_not_found", "Предмет не найден" },
            { "not_enough_quantity", "Недостаточно предметов" },
            { "invalid_slot", "Неверный слот" },
            { "rate_limited", "Слишком много запросов" },
            { "internal_error", "Внутренняя ошибка" },
            { "no_permission", "Нет прав на операцию" },
            { "item_not_owned", "Этого предмета нет в инвентаре" },
            { "stack_overflow", "Стек переполнен" },
        };

        private static readonly Dictionary<string, string> ContractResultCodeMap = new()
        {
            { "ok", "OK" },
            { "not_in_zone", "Вы должны быть в зоне NPC-агента" },
            { "contract_not_found", "Контракт не найден" },
            { "contract_not_pending", "Контракт уже принят или истёк" },
            { "contract_not_active", "Контракт не активен" },
            { "contract_not_assigned", "Это не ваш контракт" },
            { "max_active_reached", "Слишком много активных контрактов" },
            { "too_much_debt", "Слишком большой долг" },
            { "timer_expired", "Время контракта истекло" },
            { "wrong_destination", "Вы не в целевой локации" },
            { "cargo_missing", "Нет нужного груза" },
            { "warehouse_full", "Нет места на складе" },
            { "item_not_found", "Товар не найден" },
            { "rate_limited", "Слишком много запросов" },
            { "internal_error", "Внутренняя ошибка" },
        };

        private static readonly Dictionary<string, string> TradeResultCodeMap = new()
        {
            { "ok", "OK" },
            { "invalid_args", "Некорректный запрос" },
            { "internal_error", "Внутренняя ошибка" },
            { "not_in_zone", "Вы должны быть в зоне рынка" },
            { "rate_limited", "Слишком много запросов" },
            { "market_not_found", "Рынок не найден" },
            { "item_not_in_market", "Товар не продаётся здесь" },
            { "insufficient_stock", "Нет в наличии" },
            { "item_buy_disabled", "Здесь нельзя купить" },
            { "item_sell_disabled", "Здесь нельзя продать" },
            { "price_invalid", "Ошибка цены" },
            { "faction_restricted", "Торговля для вашей фракции закрыта" },
            { "item_not_in_warehouse", "Товара нет на складе" },
            { "warehouse_full_weight", "Склад переполнен по весу" },
            { "warehouse_full_volume", "Склад переполнен по объёму" },
            { "warehouse_full_types", "Склад переполнен по типам" },
            { "ship_not_found", "Корабль не найден" },
            { "ship_not_in_zone", "Корабль не в зоне причала" },
            { "item_not_in_cargo", "Товара нет в трюме" },
            { "cargo_full_weight", "Трюм переполнен по весу" },
            { "cargo_full_volume", "Трюм переполнен по объёму" },
            { "cargo_full_slots", "Трюм переполнен по слотам" },
            { "not_owner", "Недостаточно кредитов" },
            { "insufficient_credits", "Недостаточно кредитов" },
            { "not_allowed", "Слишком много запросов" },
        };

        // ============================================================
        // UI KEY MAP (~130 keys from code literals → RU fallback values)
        // ============================================================

        private static readonly Dictionary<string, string> UIKeyMap = new()
        {
            // EscMenu title
            { "ui.esc_menu.title", "Настройки" },
            { "ui.esc_menu.settings", "Настройки" },
            // EscMenu main buttons (UXML)
            { "ui.esc_menu.button.continue", "ПРОДОЛЖИТЬ" },
            { "ui.esc_menu.button.settings", "НАСТРОЙКИ" },
            { "ui.esc_menu.button.rescue", "СПАСЕНИЕ" },
            { "ui.esc_menu.button.exit", "ВЫХОД В МЕНЮ" },
            { "ui.esc_menu.root_title", "МЕНЮ" },
            // EscMenu section buttons
            { "ui.esc_menu.button.controls", "Управление" },
            { "ui.esc_menu.button.graphics", "Графика" },
            { "ui.esc_menu.button.audio", "Звук" },
            { "ui.esc_menu.button.gameplay", "Геймплей" },
            { "ui.esc_menu.button.confirm_exit", "Выйти в меню" },
            { "ui.esc_menu.button.cancel", "Отмена" },
            // EscMenu sub-page titles
            { "ui.esc_menu.controls", "Управление" },
            { "ui.esc_menu.graphics", "Графика" },
            { "ui.esc_menu.audio", "Звук" },
            { "ui.esc_menu.gameplay", "Геймплей" },
            { "ui.esc_menu.exit", "Выход" },
            { "ui.esc_menu.exit_confirm", "Вы уверены, что хотите выйти в главное меню?" },
            // Section headers
            { "ui.esc_menu.section.quality", "Качество" },
            { "ui.esc_menu.section.screen", "Экран" },
            { "ui.esc_menu.section.gameplay", "Геймплей" },
            { "ui.esc_menu.section.accessibility", "Доступность" },
            { "ui.esc_menu.section.language", "Язык" },
            { "ui.esc_menu.section.volume", "Громкость" },
            { "ui.esc_menu.section.channels", "Каналы" },
            // Labels — graphics
            { "ui.esc_menu.label.quality", "Качество" },
            { "ui.esc_menu.label.resolution", "Разрешение" },
            { "ui.esc_menu.label.fullscreen", "Полный экран" },
            { "ui.esc_menu.label.vsync", "VSync" },
            { "ui.esc_menu.label.antialiasing", "Сглаживание" },
            // Labels — gameplay
            { "ui.esc_menu.label.mouse_sens", "Чувствительность мыши" },
            { "ui.esc_menu.label.invert_y", "Инвертировать Y" },
            { "ui.esc_menu.label.zoom_sens", "Чувствительность зума" },
            { "ui.esc_menu.label.subtitles", "Субтитры" },
            { "ui.esc_menu.label.language", "Язык" },
            // Labels — audio
            { "ui.esc_menu.label.master_volume", "Общая громкость" },
            { "ui.esc_menu.label.music", "Музыка" },
            { "ui.esc_menu.label.effects", "Эффекты" },
            { "ui.esc_menu.label.voice", "Голос" },
            { "ui.esc_menu.label.ui", "Интерфейс" },
            { "ui.esc_menu.audio_mixer_note", "Микширование будет доступно после внедрения Audio Mixer" },
            // Anti-aliasing options
            { "ui.esc_menu.aa.off", "Выкл" },
            { "ui.esc_menu.aa.2x", "2x" },
            { "ui.esc_menu.aa.4x", "4x" },
            { "ui.esc_menu.aa.8x", "8x" },
            // Keybindings
            { "ui.keybindings.title", "Настройка клавиш" },
            { "ui.keybindings.save", "Сохранить" },
            { "ui.keybindings.load", "Загрузить" },
            { "ui.keybindings.reset", "Сброс" },
            { "ui.keybindings.lmb", "ЛКМ" },
            { "ui.keybindings.rmb", "ПКМ" },
            { "ui.keybindings.mmb", "СКМ" },
            { "ui.keybindings.combat_skills", "Боевые навыки" },
            { "ui.keybindings.actions", "Действия" },
            { "ui.keybindings.footer", "Нажмите на кнопку чтобы переназначить. Esc — отмена." },
            // Dialog
            { "ui.dialog.end", "Закончить разговор" },

            // === NEW: CharacterWindow filters ===
            { "ui.character.filter.all", "Все" },
            { "ui.character.filter.contracts", "Контракты" },
            { "ui.character.filter.quests", "Квесты" },
            { "ui.character.filter.active", "Активные" },
            { "ui.character.filter.available", "Доступные" },
            { "ui.character.filter.completed", "Завершённые" },
            { "ui.character.filter.all_types", "Все типы" },
            // CharacterWindow — labels
            { "ui.character.player_owner", "Игрок (Owner)" },
            { "ui.character.player", "Игрок" },
            { "ui.character.no_data", "—" },
            { "ui.character.bonuses", "Бонусы: " },
            { "ui.character.equip", "НАДЕТЬ" },
            { "ui.character.unequip", "СНЯТЬ" },
            { "ui.character.drop", "БРОСИТЬ" },
            { "ui.character.no_reputation", "Нет данных о репутации" },
            { "ui.character.no_attitude", "Нет данных об отношениях" },
            { "ui.character.factions_count", "Фракций: {0}" },
            { "ui.character.attitudes_count", "Отношений: {0}" },
            { "ui.character.no_contracts", "Нет активных или доступных контрактов" },
            { "ui.character.active_available", "Активных: {0} | Доступно: {1}" },
            { "ui.character.select_item_left", "Выберите предмет слева" },
            { "ui.character.select_contract", "Выберите контракт из списка" },
            { "ui.character.contract_unavailable", "Этот контракт уже не доступен для принятия" },
            { "ui.character.contract_not_active", "Этот контракт не активен" },
            { "ui.character.request_sent", "Запрос отправлен..." },
            { "ui.character.no_active_contracts", "Нет активных контрактов" },
            { "ui.character.active_contracts_count", "Активных: {0}" },
            { "ui.character.loading_contracts", "Загрузка контрактов..." },

            // === NEW: Quest states ===
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

            // === NEW: Skills ===
            { "ui.skill.learn", "Изучить" },
            { "ui.skill.forget", "Забыть" },

            // === NEW: Contract types + ranks ===
            { "ui.contract.type.standard", "Обычный" },
            { "ui.contract.type.urgent", "Срочный" },
            { "ui.contract.type.receipt", "Квитанция" },
            { "ui.contract.rank.primium", "Примум" },
            { "ui.contract.rank.secundus", "Секундус" },
            { "ui.contract.rank.tertius", "Терциус" },
            { "ui.contract.rank.quartus", "Квартус" },

            // === NEW: MyShipsTab ===
            { "ui.ship.no_ships", "Нет доступных кораблей. Найдите ключ в мире." },
            { "ui.ship.hull_broken", "Прочность: СЛОМАН" },
            { "ui.ship.hull_empty", "Прочность: —" },
            { "ui.ship.fuel_empty", "Топливо: —" },
            { "ui.ship.cargo_empty", "Груз: — (нет данных)" },
            { "ui.ship.modules_zero", "Модулей: 0" },
            { "ui.ship.hold_empty", "Трюм пуст" },

            // === NEW: MarketWindow ===
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

            // === NEW: ShipCargoConsoleWindow ===
            { "ui.cargo.select_inventory", "Выберите предмет в инвентаре" },
            { "ui.cargo.select_hold", "Выберите ящик в трюме" },
            { "ui.cargo.server_unavailable", "Сервер грузового отсека не доступен" },
            { "ui.cargo.unpack_unavailable", "Распаковка недоступна: нет курса обмена для этого товара" },

            // === LOC-13: CharacterWindow tabs/labels (были только в таблицах из temp-скриптов) ===
            { "ui.character.tab.character", "Персонаж" },
            { "ui.character.tab.inventory", "Инвентарь" },
            { "ui.character.tab.knowledge", "Знания" },
            { "ui.character.tab.quests", "Квесты" },
            { "ui.character.tab.contracts", "Контракты" },
            { "ui.character.tab.ship", "Корабль" },
            { "ui.character.label.player", "Игрок" },
            { "ui.character.location", "Локация: —" },

            // === LOC-13: MarketWindow buttons/tabs/sections ===
            { "ui.market.tab.market", "Рынок" },
            { "ui.market.tab.warehouse", "Склад / Трюм" },
            { "ui.market.tab.contracts", "Контракты" },
            { "ui.market.tab.exchanger", "Обменник" },
            { "ui.market.label.welcome", "Откройте рынок, чтобы торговать" },
            { "ui.market.btn.buy", "Купить" },
            { "ui.market.btn.sell", "Продать" },
            { "ui.market.btn.load", "Погрузить" },
            { "ui.market.btn.unload", "Разгрузить" },
            { "ui.market.btn.accept", "Взять" },
            { "ui.market.btn.complete", "Сдать" },
            { "ui.market.btn.fail", "Провалить" },
            { "ui.market.btn.close", "Закрыть" },
            { "ui.market.section.items", "Товары на рынке" },
            { "ui.market.section.warehouse", "Ваш склад" },
            { "ui.market.section.cargo", "Груз корабля" },
            { "ui.market.section.contracts", "Контракты НП" },
            { "ui.market.section.exchange", "Ресурсы: конвертация пикаблов ↔ ящики" },
            { "ui.market.exchange.inv", "Инвентарь (ресурсы)" },
            { "ui.market.exchange.wh", "Склад (ящики)" },
            { "ui.market.ship_selector", "Корабль:" },
            { "ui.market.quantity", "Кол-во:" },
            { "ui.market.pack", "→ УПАКОВАТЬ" },
            { "ui.market.unpack", "← РАСПАКОВАТЬ" },
            { "ui.market.location", "Рынок: {0}" },
            { "ui.market.credits", "Кредиты: {0:F0} CR" },
            { "ui.market.warehouse_info", "Склад: {0} типов / {1}" },
            { "ui.market.speed_info", "Скорость рынка: x{0:F1} | Тик через: {1}с" },
            { "ui.market.error", "Ошибка: {0}" },
            { "ui.market.op_ok", "{0}: OK ({1} x{2})" },
            { "ui.market.taken", " [ВЗЯТ]" },
            { "ui.market.type.standard", "[Стандарт]" },
            { "ui.market.type.urgent", "[Срочный]" },
            { "ui.market.type.receipt", "[Расписка]" },
            { "ui.market.weight", "{0:F1} / {1:F0} кг" },
            { "ui.market.slots", "{0} / {1} слотов" },
            { "ui.market.weight_empty", "— / — кг" },
            { "ui.market.slots_empty", "— / — слотов" },
            { "ui.market.packs", "{0} → {1} пач." },
            { "ui.market.boxed", "{0} (ящ.)" },
            { "ui.market.exchange_ok", "Обмен: OK (Δ склад={0}, инвентарь={1})" },
            { "ui.market.pack_request", "Отправлен запрос на упаковку {0}..." },
            { "ui.market.unpack_request", "Отправлен запрос на распаковку {0}..." },
            { "ui.market.row.item", "{0}  —  {1:F0} CR  (сток: {2})  (у вас: {3})" },
            { "ui.market.row.qty", "{0}  —  {1} ед." },
            { "ui.market.row.qty_ship", "{0}  —  {1} ед.  ({2})" },

            // === LOC-13: RepairManagerWindow (значения из temp-скрипта AddRepairKeys) ===
            { "ui.repair.available_modules", "Доступные модули:" },
            { "ui.repair.done", "Готово ✓" },
            { "ui.repair.free", "Бесплатно" },
            { "ui.repair.hull_request", "Запрос на ремонт корпуса отправлен..." },
            { "ui.repair.install", "Установить" },
            { "ui.repair.insufficient_power", "Недостаточно энергии" },
            { "ui.repair.no_database", "База модулей не задана." },
            { "ui.repair.no_modules", "Нет совместимых модулей для этого слота." },
            { "ui.repair.no_pads", "Нет свободных падов!" },
            { "ui.repair.no_ship", "Корабль не выбран." },
            { "ui.repair.not_spawned", "Корабль не заспавнен." },
            { "ui.repair.paint_request", "Запрос на покраску отправлен..." },
            { "ui.repair.slot_empty", "Установлено: пусто" },
            { "ui.repair.title", "🛠 Ремонтный Менеджер" },
            { "ui.repair.call", "🚁 Вызвать" },
            { "ui.repair.credits", "💰 Кредиты: {0:F0}" },
            { "ui.repair.recall_cost", "{0} кр." },
            { "ui.repair.key_fallback", "🔑 Key #{0}" },
            { "ui.repair.slot_empty_suffix", " [пусто]" },
            { "ui.repair.slot_occupied_suffix", " [✓ {0}]" },
            { "ui.repair.ship_class", "Класс: {0}" },
            { "ui.repair.ship_class_empty", "Класс: —" },
            { "ui.repair.power", "Энергия: {0}/{1}" },
            { "ui.repair.power_empty", "Энергия: —" },
            { "ui.repair.hull_broken_fmt", "Прочность: СЛОМАН ({0}/{1})" },
            { "ui.repair.hull_fmt", "Прочность: {0}/{1}" },
            { "ui.repair.hull_btn", "🔧 Починить ({0} кр.)" },
            { "ui.repair.hull_ok", "✓ Целый" },
            { "ui.repair.dock_required", "Корабль должен быть в доке" },
            { "ui.repair.not_enough_credits", "Недостаточно кредитов! Нужно {0}, есть {1:F0}" },
            { "ui.repair.recalled_to_pad", "Корабль вызван на пад {0}..." },
            { "ui.repair.installed_no", "Установлено: —" },
            { "ui.repair.installed_fmt", "Установлено: {0} (★{1})" },
            { "ui.repair.sell_btn", "💰 Продать (+{0} кр.)" },
            { "ui.repair.sell_request", "Продажа модуля из '{0}' (+{1} кр.)..." },
            { "ui.repair.modules_for_slot", "Модули для слота '{0}':" },
            { "ui.repair.price_credits", "💰 {0} кр." },
            { "ui.repair.power_avail", " ⚡ {0} (свободно {1})" },
            { "ui.repair.install_request", "Запрос на установку '{0}' в '{1}' отправлен..." },
            { "ui.repair.paint_cost", "Стоимость: {0} кр." },
            { "ui.repair.paint_btn", "🎨 Покрасить ({0} кр.)" },
            { "ui.repair.paint_choose", "🎨 Выберите цвет" },
            { "ui.repair.section.ship", "Корабль:" },
            { "ui.repair.section.slot", "Слот модуля:" },
            { "ui.repair.section.paint", "🎨 Цвет корабля:" },
            { "ui.repair.color.white", "⚪ Белый" },
            { "ui.repair.color.gray", "🔘 Серый" },
            { "ui.repair.color.black", "⚫ Чёрный" },
            { "ui.repair.color.red", "🔴 Красный" },
            { "ui.repair.color.blue", "🔵 Синий" },
            { "ui.repair.color.green", "🟢 Зелёный" },
            { "ui.repair.color.yellow", "🟡 Жёлтый" },
            { "ui.repair.color.orange", "🟠 Оранжевый" },
            { "ui.repair.color.purple", "🟣 Фиолетовый" },
            { "ui.repair.color.turquoise", "🔷 Бирюзовый" },

            // === LOC-13: SkillTreeWindow ===
            { "ui.skill.tree_title", "Дерево навыков" },
            { "ui.skill.filter.melee", "⚔ Melee" },
            { "ui.skill.filter.ranged", "🏹 Ranged" },
            { "ui.skill.filter.defense", "🛡 Defense" },
            { "ui.skill.filter.placed", "📍 Placed" },
            { "ui.skill.search_placeholder", "Поиск по имени или эффекту (STR, +2)..." },
            { "ui.skill.slots", "Слоты" },
            { "ui.skill.list", "Навыки" },
            { "ui.skill.select_hint", "Выберите навык слева" },
            { "ui.skill.required", "Требуется:" },
            { "ui.skill.unlocks", "Откроет:" },
            { "ui.skill.close", "Закрыть" },
            { "ui.skill.type_active", "Активный (биндится на слот)" },
            { "ui.skill.type_passive", "Пассивный (применяется автоматически)" },
            { "ui.skill.no_description", "(нет описания)" },
            { "ui.skill.type_line", "Тип: {0}" },
            { "ui.skill.animation", "Анимация: {0}" },
            { "ui.skill.effects_line", "Эффекты: {0}" },
            { "ui.skill.cost", "Стоимость: {0}" },
            { "ui.skill.free", "Free" },
            { "ui.skill.requirements", "Требования: {0}" },
            { "ui.skill.requirements_none", "Требования: нет" },
            { "ui.skill.aoe_cone", "⚔ Зона: Конус {0}° × {1}м вперёд" },
            { "ui.skill.aoe_sphere", "💥 Зона: Сфера {0}м радиус (вокруг персонажа)" },
            { "ui.skill.aoe_line", "➤ Зона: Линия {0}м × {1}м (древко)" },
            { "ui.skill.aoe_box", "▣ Зона: Бокс {0}м × {1}м" },
            { "ui.skill.none", "(нет)" },
            { "ui.skill.nothing", "(ничего)" },
            { "ui.skill.empty", "(пусто)" },

            // === LOC-13: Toasts ===
            { "ui.toast.knowledge_opened", "📖 Открыто знание — {0}: {1}" },
            { "ui.toast.category.skill", "Навык" },
            { "ui.toast.category.recipe", "Рецепт" },
            { "ui.toast.category.faction", "Фракция" },
            { "ui.toast.category.npc", "NPC" },
            { "ui.toast.and_more", " и ещё {0}" },
            { "ui.gather.progress", "Сбор ресурса…" },
            { "ui.gather.completed", "✅ Добыто: {0} × {1}" },
            { "ui.gather.resource", "Ресурс" },
            { "ui.gather.depleted", " (узел истощён)" },
            { "ui.gather.denied", "Отказано в доступе" },
            { "ui.quest.toast.discovered", "✨ Найден квест: {0}" },
            { "ui.quest.toast.fail_take", "Не удалось взять квест" },
            { "ui.quest.toast.item_give", "📦 +1 предмет" },
            { "ui.quest.toast.item_give_named", "📦 +1 {0}" },
            { "ui.quest.toast.item_take", "📦 -1 предмет" },
            { "ui.quest.toast.item_take_named", "📦 -1 {0}" },
            { "ui.quest.toast.credits", "💰 +{0} CR" },
            { "ui.quest.toast.reputation", "📈 Репутация +{0}" },
            { "ui.quest.toast.reputation_named", "📈 {0} +{1}" },
            { "ui.quest.toast.attitude", "💚 Отношение +{0}" },
            { "ui.quest.toast.attitude_named", "💚 {0} +{1}" },
            { "ui.quest.toast.objective", "✅ Цель выполнена" },
            { "ui.quest.toast.objective_named", "✅ {0}" },

            // === LOC-13: MetaRequirement (клиентские fallback'и) ===
            { "ui.metareq.server_unavailable", "Сервер требований недоступен" },
            { "ui.metareq.no_access", "Нет доступа" },
        };
    }
}
