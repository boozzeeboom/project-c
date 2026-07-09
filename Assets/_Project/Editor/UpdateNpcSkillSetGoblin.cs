// One-shot Editor script: обновить NpcSkillSet_Goblin.asset —
// добавить Skill_Explosives_Grenade (Throwables) и Skill_Ranged_BasicBow (Bows).
using UnityEditor;
using UnityEngine;
using ProjectC.AI;
using ProjectC.Skills;

public static class UpdateNpcSkillSetGoblin
{
    [MenuItem("Project C/Dev/Update NpcSkillSet_Goblin")]
    public static void Execute()
    {
        var assetPath = "Assets/_Project/Resources/AI/NpcSkillSet_Goblin.asset";
        var skillSet = AssetDatabase.LoadAssetAtPath<NpcSkillSet>(assetPath);
        if (skillSet == null)
        {
            Debug.LogError($"NpcSkillSet_Goblin not found at {assetPath}");
            return;
        }

        var grenade = AssetDatabase.LoadAssetAtPath<SkillNodeConfig>(
            "Assets/_Project/Resources/Skills/Skill_Explosives_Grenade.asset");
        var bow = AssetDatabase.LoadAssetAtPath<SkillNodeConfig>(
            "Assets/_Project/Resources/Skills/Skill_Ranged_BasicBow.asset");
        var heavySwing = AssetDatabase.LoadAssetAtPath<SkillNodeConfig>(
            "Assets/_Project/Resources/Skills/Skill_Melee_HeavySwing.asset");
        var basicSword = AssetDatabase.LoadAssetAtPath<SkillNodeConfig>(
            "Assets/_Project/Resources/Skills/Skill_Melee_BasicSword.asset");

        // Keep existing entries + add new ones
        var existing = skillSet.skills;
        var newSkills = new NpcSkillOverride[4];

        // 0: BasicSword (keep existing if present, else create)
        newSkills[0] = new NpcSkillOverride
        {
            skillConfig = basicSword,
            priority = 50,
            overrideCooldown = 1.2f,
            minHpPercent = 0f,
            maxHpPercent = 1f,
        };

        // 1: HeavySwing — AOE Cone (enable debug viz on skill config)
        newSkills[1] = new NpcSkillOverride
        {
            skillConfig = heavySwing,
            priority = 30,
            overrideCooldown = 3.0f,
            minHpPercent = 0.3f, // only available above 30% HP
            maxHpPercent = 1f,
        };
        // Enable debugVisualizeAoe on HeavySwing skill config
        if (heavySwing != null)
        {
            var so = new SerializedObject(heavySwing);
            so.FindProperty("debugVisualizeAoe").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // 2: Grenade — Throwable, Sphere AOE 8m
        newSkills[2] = new NpcSkillOverride
        {
            skillConfig = grenade,
            priority = 25,
            overrideCooldown = 4.0f,
            overrideBaseDamage = 8,
            overrideDamageDice = ProjectC.Combat.Core.DamageDice.d8,
            overrideRange = 50f,
            minHpPercent = 0.5f,
            maxHpPercent = 1f,
        };

        // 3: BasicBow — Ranged, D100 hit/damage
        newSkills[3] = new NpcSkillOverride
        {
            skillConfig = bow,
            priority = 35,
            overrideCooldown = 2.0f,
            overrideBaseDamage = 5,
            overrideDamageDice = ProjectC.Combat.Core.DamageDice.d6,
            overrideRange = 200f,
            minHpPercent = 0f,
            maxHpPercent = 1f,
        };

        skillSet.skills = newSkills;

        EditorUtility.SetDirty(skillSet);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"Updated NpcSkillSet_Goblin: {newSkills.Length} skills " +
                  $"(BasicSword, HeavySwing[Cone], Grenade[Throwable], BasicBow[Ranged])");
    }
}
