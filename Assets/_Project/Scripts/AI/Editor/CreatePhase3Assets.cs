// Project C: Phase 3 asset creator — SocialRole presets
// Run in Unity Editor via execute_script

using UnityEditor;
using UnityEngine;
using ProjectC.AI;

public static class CreatePhase3Assets
{
    public static void Execute()
    {
        string folder = "Assets/_Project/Resources/AI";
        if (!AssetDatabase.IsValidFolder(folder))
            AssetDatabase.CreateFolder("Assets/_Project/Resources", "AI");

        CreateRole("SocialRole_Guard", SocialRoleConfig.CreateGuardPreset(), folder);
        CreateRole("SocialRole_Civilian", SocialRoleConfig.CreateCivilianPreset(), folder);
        CreateRole("SocialRole_Merchant", SocialRoleConfig.CreateMerchantPreset(), folder);
        CreateRole("SocialRole_Thug", SocialRoleConfig.CreateThugPreset(), folder);
        CreateRole("SocialRole_Leader", SocialRoleConfig.CreateLeaderPreset(), folder);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[Phase3] Created 5 SocialRole assets in Resources/AI/");
    }

    private static void CreateRole(string name, SocialRoleConfig preset, string folder)
    {
        string path = $"{folder}/{name}.asset";
        if (AssetDatabase.LoadAssetAtPath<SocialRoleConfig>(path) != null)
        {
            Debug.Log($"[Phase3] {name} already exists, skipping.");
            return;
        }
        AssetDatabase.CreateAsset(preset, path);
    }
}
