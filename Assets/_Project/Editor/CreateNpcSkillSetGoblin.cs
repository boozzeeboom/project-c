#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using ProjectC.AI;
using ProjectC.Skills;
using ProjectC.Combat.Core;

public static class CreateNpcSkillSetGoblin
{
    public static void Execute()
    {
        // 1. Создать NpcSkillSet asset
        var skillSet = ScriptableObject.CreateInstance<NpcSkillSet>();
        skillSet.selectionMode = NpcSkillSet.SelectionMode.RandomWeighted;

        // Найти SkillNodeConfig assets для скилов
        var basicSword = AssetDatabase.LoadAssetAtPath<SkillNodeConfig>(
            "Assets/_Project/Resources/Skills/Skill_Melee_BasicSword.asset");
        var heavySwing = AssetDatabase.LoadAssetAtPath<SkillNodeConfig>(
            "Assets/_Project/Resources/Skills/Skill_Melee_HeavySwing.asset");

        // Загрузить анимации из HumanM_Model.fbx
        var humanModel = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Kevin Iglesias/Human Animations/Models/HumanM_Model.fbx");
        AnimationClip attack02 = null;
        AnimationClip attack03 = null;
        if (humanModel != null)
        {
            var allAssets = AssetDatabase.LoadAllAssetsAtPath(
                "Assets/Kevin Iglesias/Human Animations/Models/HumanM_Model.fbx");
            foreach (var a in allAssets)
            {
                if (a is AnimationClip clip)
                {
                    if (clip.name.Contains("Attack02")) attack02 = clip;
                    if (clip.name.Contains("Attack03")) attack03 = clip;
                }
            }
        }

        skillSet.skills = new NpcSkillOverride[2];

        // Skill 1: Basic Sword (частая атака) с overrideAnimation
        skillSet.skills[0] = new NpcSkillOverride
        {
            skillConfig = basicSword,
            overrideCooldown = 0f,        // использовать из skill
            overrideAnimation = attack02,
            overrideAnimationSpeed = 0f,   // использовать из skill
            overrideDamageDice = DamageDice.d6,
            overrideBaseDamage = 0,
            overrideRange = 0f,
            priority = 70,                // часто
            minHpPercent = 0f,
            maxHpPercent = 1f,
        };

        // Skill 2: Heavy Swing (редкая, мощная) с overrideAnimation
        skillSet.skills[1] = new NpcSkillOverride
        {
            skillConfig = heavySwing,
            overrideCooldown = 4.0f,       // медленнее чем у игрока
            overrideAnimation = attack03,
            overrideAnimationSpeed = 0.7f,  // медленнее
            overrideDamageDice = DamageDice.d8,
            overrideBaseDamage = 5,
            overrideRange = 0f,
            priority = 30,                 // реже
            minHpPercent = 0f,
            maxHpPercent = 1f,
        };

        // Сохранить
        string path = "Assets/_Project/Resources/AI/NpcSkillSet_Goblin.asset";
        AssetDatabase.CreateAsset(skillSet, path);
        AssetDatabase.SaveAssets();

        Debug.Log($"[CreateNpcSkillSetGoblin] Created {path} with {skillSet.skills.Length} skills.");

        // 2. Привязать к NpcSpawner_Default
        var spawnerConfig = AssetDatabase.LoadAssetAtPath<NpcSpawnerConfig>(
            "Assets/_Project/Resources/AI/NpcSpawner_Default.asset");
        if (spawnerConfig != null)
        {
            var so = new SerializedObject(spawnerConfig);
            so.FindProperty("npcSkillSet").objectReferenceValue = skillSet;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(spawnerConfig);
            AssetDatabase.SaveAssets();
            Debug.Log("[CreateNpcSkillSetGoblin] Assigned NpcSkillSet_Goblin to NpcSpawner_Default.npcSkillSet");
        }
        else
        {
            Debug.LogWarning("[CreateNpcSkillSetGoblin] NpcSpawner_Default.asset not found — skip wiring");
        }
    }
}
#endif
