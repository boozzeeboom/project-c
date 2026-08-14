using UnityEngine;
using UnityEditor;

public static class AssignFemaleMesh
{
    public static void Execute()
    {
        string prefabPath = "Assets/_Project/Prefabs/NetworkPlayer.prefab";
        string meshPath = "Assets/Generated_Models/Female_Pilot/Female_Pilot_BodyMesh.asset";

        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
        if (mesh == null) { Debug.LogError("Mesh not found: " + meshPath); return; }

        using (var scope = new PrefabUtility.EditPrefabContentsScope(prefabPath))
        {
            var root = scope.prefabContentsRoot;
            var applier = root.GetComponentInChildren<ProjectC.Player.CharacterCustomisationApplier>(true);
            if (applier == null) { Debug.LogError("CharacterCustomisationApplier not found in prefab"); return; }

            var so = new SerializedObject(applier);
            var prop = so.FindProperty("_femaleMesh");
            prop.objectReferenceValue = mesh;
            so.ApplyModifiedPropertiesWithoutUndo();
            Debug.Log($"Assigned _femaleMesh = {mesh.name} on NetworkPlayer.prefab");
        }

        AssetDatabase.SaveAssets();
    }
}
