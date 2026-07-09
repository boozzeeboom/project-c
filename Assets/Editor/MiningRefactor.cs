using UnityEditor;
using UnityEngine;

public static class MiningRefactor
{
    public static void Execute()
    {
        // Step 1: Ensure prefab has _config=null
        var prefabPath = "Assets/_Project/Prefabs/ResourceNode_Default.prefab";
        using (var scope = new PrefabUtility.EditPrefabContentsScope(prefabPath))
        {
            var rn = scope.prefabContentsRoot.GetComponent<ProjectC.ResourceNode.ResourceNode>();
            if (rn != null)
            {
                var so = new SerializedObject(rn);
                var configProp = so.FindProperty("_config");
                if (configProp.objectReferenceValue != null)
                {
                    configProp.objectReferenceValue = null;
                    so.ApplyModifiedProperties();
                    Debug.Log("[MiningRefactor] Prefab _config → null");
                }
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log("[MiningRefactor] Done.");
    }
}

