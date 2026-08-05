using UnityEngine;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine.Localization.Tables;
using System.Collections.Generic;
using System.IO;

/// <summary>
/// LOC-05: Scans ScriptableObject assets and populates Static_Table_ru with derive-key entries.
/// Does NOT modify the source assets — keys are derived from IDs at runtime.
/// </summary>
public static class LocalizationStringMigrator
{
    [MenuItem("ProjectC/Localization/Migrate SO Strings to Static_Table")]
    public static void Execute()
    {
        var tablePath = "Assets/_Project/Settings/Localization/Static_Table_ru.asset";
        var table = AssetDatabase.LoadAssetAtPath<StringTable>(tablePath);
        if (table == null) { Debug.LogError($"[Migrator] Table not found: {tablePath}"); return; }

        int totalAdded = 0;

        // --- TradeItemDefinition (113 items) ---
        totalAdded += MigrateTradeItems(table);

        // --- NpcDefinition ---
        totalAdded += MigrateNpcs(table);

        // --- QuestDefinition ---
        totalAdded += MigrateQuests(table);

        // --- FactionDefinition ---
        totalAdded += MigrateFactions(table);

        // --- MarketConfig ---
        totalAdded += MigrateMarkets(table);

        // --- ItemTypeNames ---
        totalAdded += MigrateItemTypes(table);

        EditorUtility.SetDirty(table);
        AssetDatabase.SaveAssets();
        Debug.Log($"[Migrator] Total keys added to Static_Table_ru: {totalAdded}");
    }

    private static int MigrateTradeItems(StringTable table)
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
            {
                var key = $"static.item.{itemId}.displayName";
                if (table.GetEntry(key) == null) { table.AddEntry(key, displayName); added++; }
            }
            if (!string.IsNullOrEmpty(itemId) && !string.IsNullOrEmpty(description))
            {
                var key = $"static.item.{itemId}.description";
                if (table.GetEntry(key) == null) { table.AddEntry(key, description); added++; }
            }
        }
        Debug.Log($"[Migrator] TradeItems: {added} keys ({guids.Length} assets)");
        return added;
    }

    private static int MigrateNpcs(StringTable table)
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
            {
                var key = $"static.npc.{npcId}.displayName";
                if (table.GetEntry(key) == null) { table.AddEntry(key, displayName); added++; }
            }
            if (!string.IsNullOrEmpty(npcId) && !string.IsNullOrEmpty(greetingText))
            {
                var key = $"static.npc.{npcId}.greetingText";
                if (table.GetEntry(key) == null) { table.AddEntry(key, greetingText); added++; }
            }
        }
        Debug.Log($"[Migrator] NPCs: {added} keys ({guids.Length} assets)");
        return added;
    }

    private static int MigrateQuests(StringTable table)
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
            {
                var key = $"static.quest.{questId}.displayName";
                if (table.GetEntry(key) == null) { table.AddEntry(key, displayName); added++; }
            }
            if (!string.IsNullOrEmpty(questId) && !string.IsNullOrEmpty(description))
            {
                var key = $"static.quest.{questId}.description";
                if (table.GetEntry(key) == null) { table.AddEntry(key, description); added++; }
            }

            // Stages and objectives via SerializedProperty
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
                    {
                        var skey = $"static.quest.{questId}.stage.{stageId}.description";
                        if (table.GetEntry(skey) == null) { table.AddEntry(skey, stageDesc); added++; }
                    }

                    var objectives = stage.FindPropertyRelative("objectives");
                    if (objectives != null && objectives.isArray)
                    {
                        for (int o = 0; o < objectives.arraySize; o++)
                        {
                            var obj = objectives.GetArrayElementAtIndex(o);
                            var objId = obj.FindPropertyRelative("objectiveId")?.stringValue;
                            var objDesc = obj.FindPropertyRelative("description")?.stringValue;

                            if (!string.IsNullOrEmpty(questId) && !string.IsNullOrEmpty(stageId) && !string.IsNullOrEmpty(objId) && !string.IsNullOrEmpty(objDesc))
                            {
                                var okey = $"static.quest.{questId}.stage.{stageId}.obj.{objId}";
                                if (table.GetEntry(okey) == null) { table.AddEntry(okey, objDesc); added++; }
                            }
                        }
                    }
                }
            }
        }
        Debug.Log($"[Migrator] Quests: {added} keys ({guids.Length} assets)");
        return added;
    }

    private static int MigrateFactions(StringTable table)
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
            {
                var key = $"static.faction.{factionId}.displayName";
                if (table.GetEntry(key) == null) { table.AddEntry(key, displayName); added++; }
            }
            if (!string.IsNullOrEmpty(factionId) && !string.IsNullOrEmpty(loreDescription))
            {
                var key = $"static.faction.{factionId}.loreDescription";
                if (table.GetEntry(key) == null) { table.AddEntry(key, loreDescription); added++; }
            }

            // Reputation tiers
            var sso = new SerializedObject(so);
            var tiers = sso.FindProperty("reputationTiers");
            if (tiers != null && tiers.isArray)
            {
                for (int t = 0; t < tiers.arraySize; t++)
                {
                    var tier = tiers.GetArrayElementAtIndex(t);
                    var tierName = tier.FindPropertyRelative("tier")?.stringValue;
                    if (!string.IsNullOrEmpty(factionId) && !string.IsNullOrEmpty(tierName))
                    {
                        var tkey = $"static.faction.{factionId}.tier.{t}";
                        if (table.GetEntry(tkey) == null) { table.AddEntry(tkey, tierName); added++; }
                    }
                }
            }
        }
        Debug.Log($"[Migrator] Factions: {added} keys ({guids.Length} assets)");
        return added;
    }

    private static int MigrateMarkets(StringTable table)
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
            {
                var key = $"static.market.{marketId}.displayName";
                if (table.GetEntry(key) == null) { table.AddEntry(key, displayName); added++; }
            }
        }
        Debug.Log($"[Migrator] Markets: {added} keys ({guids.Length} assets)");
        return added;
    }

    private static int MigrateItemTypes(StringTable table)
    {
        // ItemTypeNames is a static class with _names[] array — migrate via reflection
        try
        {
            var type = System.Type.GetType("ProjectC.Core.ItemTypeNames, Assembly-CSharp");
            if (type == null) { Debug.LogWarning("[Migrator] ItemTypeNames not found"); return 0; }

            var namesField = type.GetField("_names", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            if (namesField == null) { Debug.LogWarning("[Migrator] _names field not found"); return 0; }

            var names = namesField.GetValue(null) as string[];
            if (names == null) return 0;

            int added = 0;
            for (int i = 0; i < names.Length; i++)
            {
                var key = $"static.item_type.{i}";
                if (table.GetEntry(key) == null && !string.IsNullOrEmpty(names[i]))
                {
                    table.AddEntry(key, names[i]);
                    added++;
                }
            }
            Debug.Log($"[Migrator] ItemTypes: {added} keys");
            return added;
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[Migrator] ItemTypes error: {ex.Message}");
            return 0;
        }
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
}
