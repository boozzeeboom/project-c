// T-FACTION-UNIFY Stage C: Create FactionDefinition assets for new factions + update Pirates.
// Run via: Tools → ProjectC → Migration → Create Faction Assets (Stage C)

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using ProjectC.Factions;

public static class CreateFactionAssets
{
    [MenuItem("Tools/ProjectC/Migration/Create Faction Assets (Stage C)")]
    public static void CreateAssets()
    {
        string dir = "Assets/_Project/Quests/Data/Factions";

        // ---- C1: Bandits ----
        CreateFaction(dir, "Faction_Bandits", FactionId.Bandits, "Бандиты",
            new Color(0.85f, 0.3f, 0.2f), "Разбойники, грабящие караваны и одиноких путников.",
            FactionAttitude.Hostile, FactionRelation.Hostile);

        // ---- C2: Cultists ----
        CreateFaction(dir, "Faction_Cultists", FactionId.Cultists, "Культисты",
            new Color(0.6f, 0.1f, 0.6f), "Последователи тёмных культов, практикующие запретные ритуалы.",
            FactionAttitude.Hostile, FactionRelation.Hostile);

        // ---- C3: Guards ----
        CreateFaction(dir, "Faction_Guards", FactionId.Guards, "Стража",
            new Color(0.2f, 0.4f, 0.85f), "Городская стража, поддерживающая порядок.",
            FactionAttitude.Neutral, FactionRelation.Neutral);

        // ---- C4: Villagers ----
        CreateFaction(dir, "Faction_Villagers", FactionId.Villagers, "Жители",
            new Color(0.3f, 0.7f, 0.3f), "Мирные жители городов и деревень.",
            FactionAttitude.Neutral, FactionRelation.Neutral);

        // ---- C5: Update Pirates ----
        UpdatePirates(dir);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[T-FACTION-UNIFY Stage C] Created 4 new FactionDefinition assets + updated Pirates.");
    }

    private static void CreateFaction(string dir, string name, FactionId id, string displayName,
        Color color, string lore, FactionAttitude attitude, FactionRelation combatRelation)
    {
        string path = $"{dir}/{name}.asset";
        var existing = AssetDatabase.LoadAssetAtPath<FactionDefinition>(path);
        if (existing != null)
        {
            Debug.Log($"  [SKIP] {name} already exists at {path}");
            return;
        }

        var asset = ScriptableObject.CreateInstance<FactionDefinition>();
        asset.factionId = id;
        asset.displayName = displayName;
        asset.color = color;
        asset.loreDescription = lore;
        asset.defaultAttitude = attitude;
        asset.defaultCombatRelation = combatRelation;
        asset.combatRelations = System.Array.Empty<FactionCombatRelation>();

        AssetDatabase.CreateAsset(asset, path);
        Debug.Log($"  [OK] Created {name} at {path}");
    }

    private static void UpdatePirates(string dir)
    {
        string path = $"{dir}/Pirates.asset";
        var pirates = AssetDatabase.LoadAssetAtPath<FactionDefinition>(path);
        if (pirates == null)
        {
            Debug.LogError($"[T-FACTION-UNIFY] Pirates.asset not found at {path}");
            return;
        }

        var so = new SerializedObject(pirates);

        // defaultCombatRelation = Hostile
        var combatRelProp = so.FindProperty("defaultCombatRelation");
        combatRelProp.enumValueIndex = (int)FactionRelation.Hostile;

        // defaultAttitude = Hostile
        var attitudeProp = so.FindProperty("defaultAttitude");
        attitudeProp.enumValueIndex = (int)FactionAttitude.Hostile;

        // combatRelations: Bandits=Allied, Cultists=Allied, Guards=Hostile, Villagers=Hostile
        var arrProp = so.FindProperty("combatRelations");
        arrProp.arraySize = 4;

        SetRelationEntry(arrProp, 0, FactionId.Bandits, FactionRelation.Allied);
        SetRelationEntry(arrProp, 1, FactionId.Cultists, FactionRelation.Allied);
        SetRelationEntry(arrProp, 2, FactionId.Guards, FactionRelation.Hostile);
        SetRelationEntry(arrProp, 3, FactionId.Villagers, FactionRelation.Hostile);

        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(pirates);
        Debug.Log("  [OK] Updated Pirates.asset: defaultCombatRelation=Hostile, defaultAttitude=Hostile, +4 combatRelations");
    }

    private static void SetRelationEntry(SerializedProperty arrProp, int index, FactionId target, FactionRelation rel)
    {
        var elem = arrProp.GetArrayElementAtIndex(index);
        elem.FindPropertyRelative("targetFaction").enumValueIndex = (int)target;
        elem.FindPropertyRelative("relation").enumValueIndex = (int)rel;
    }
}
#endif
