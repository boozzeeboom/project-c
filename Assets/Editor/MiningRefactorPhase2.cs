using UnityEditor;
using UnityEngine;
using UnityEditor.SceneManagement;

public static class MiningRefactorPhase2
{
    public static void Execute()
    {
        var prefabPath = "Assets/_Project/Prefabs/ResourceNode_Default.prefab";
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null)
        {
            Debug.LogError("[MiningPhase2] Prefab not found.");
            return;
        }

        var scene = EditorSceneManager.GetActiveScene();
        var rootObjects = scene.GetRootGameObjects();

        var ironVeinConfig = AssetDatabase.LoadAssetAtPath<ProjectC.ResourceNode.ResourceNodeConfig>(
            "Assets/_Project/Resources/ResourceNodes/ResourceNode_IronVein.asset");
        var copperVeinConfig = AssetDatabase.LoadAssetAtPath<ProjectC.ResourceNode.ResourceNodeConfig>(
            "Assets/_Project/Resources/ResourceNodes/ResourceNode_CopperVein.asset");
        var plantHerbConfig = AssetDatabase.LoadAssetAtPath<ProjectC.ResourceNode.ResourceNodeConfig>(
            "Assets/_Project/Resources/ResourceNodes/ResourceNode_PlantHerb.asset");

        // Find existing nodes
        GameObject ironVein = null, copperVein = null, plantHerb = null;
        foreach (var obj in rootObjects)
        {
            var rn = obj.GetComponent<ProjectC.ResourceNode.ResourceNode>();
            if (rn == null) continue;
            if (obj.name.Contains("IronVein")) ironVein = obj;
            else if (obj.name.Contains("CopperVein")) copperVein = obj;
            else if (obj.name.Contains("PlantHerb")) plantHerb = obj;
        }

        // Fix IronVein config (lost during prefab null-clearing)
        if (ironVein != null)
        {
            var rn = ironVein.GetComponent<ProjectC.ResourceNode.ResourceNode>();
            var so = new SerializedObject(rn);
            var configProp = so.FindProperty("_config");
            if (configProp.objectReferenceValue == null)
            {
                configProp.objectReferenceValue = ironVeinConfig;
                so.ApplyModifiedProperties();
                PrefabUtility.RecordPrefabInstancePropertyModifications(rn);
                Debug.Log("[MiningPhase2] IronVein _config restored.");
            }
        }

        // Helper: replace a raw GO with prefab instance
        System.Func<GameObject, Vector3, ProjectC.ResourceNode.ResourceNodeConfig, string, string, GameObject> replaceNode =
            (oldGO, pos, conf, displayName, configName) =>
        {
            if (oldGO != null) Object.DestroyImmediate(oldGO);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.transform.position = pos;
            go.name = $"[ResourceNode_{configName}]";

            var rn = go.GetComponent<ProjectC.ResourceNode.ResourceNode>();
            var so = new SerializedObject(rn);
            so.FindProperty("_config").objectReferenceValue = conf;
            so.ApplyModifiedProperties();

            var mr = go.GetComponent<ProjectC.MetaRequirement.MetaRequirement>();
            if (mr != null)
            {
                var mrSo = new SerializedObject(mr);
                mrSo.FindProperty("_interactableDisplayName").stringValue = displayName;
                mrSo.ApplyModifiedProperties();
            }

            PrefabUtility.RecordPrefabInstancePropertyModifications(rn);
            if (mr != null) PrefabUtility.RecordPrefabInstancePropertyModifications(mr);
            return go;
        };

        // Replace CopperVein and PlantHerb with prefab instances
        if (copperVein != null)
            replaceNode(copperVein, new Vector3(40051.2f, 2502.77f, 40026.6f), copperVeinConfig, "CopperVein", "CopperVein");
        if (plantHerb != null)
            replaceNode(plantHerb, new Vector3(40047.0977f, 2502.77f, 40019.5039f), plantHerbConfig, "PlantHerb", "PlantHerb");

        EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[MiningPhase2] All 3 ResourceNodes are now prefab instances of ResourceNode_Default.prefab.");
    }
}
