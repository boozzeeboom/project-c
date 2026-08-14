using UnityEngine;
using UnityEditor;

public static class RevertFemaleMesh
{
    public static void Execute()
    {
        string prefabPath = "Assets/_Project/Prefabs/NetworkPlayer.prefab";
        string humanFPath = "Assets/Kevin Iglesias/Human Animations/Models/HumanF_Model.fbx";

        var humanF = AssetDatabase.LoadAssetAtPath<GameObject>(humanFPath);
        if (humanF == null) { Debug.LogError("HumanF load fail"); return; }
        var smr = humanF.GetComponentInChildren<SkinnedMeshRenderer>(true);
        if (smr == null || smr.sharedMesh == null) { Debug.LogError("HumanF mesh not found"); return; }

        using (var scope = new PrefabUtility.EditPrefabContentsScope(prefabPath))
        {
            var root = scope.prefabContentsRoot;
            var applier = root.GetComponentInChildren<ProjectC.Player.CharacterCustomisationApplier>(true);
            if (applier == null) { Debug.LogError("CharacterCustomisationApplier not found"); return; }

            var so = new SerializedObject(applier);
            var prop = so.FindProperty("_femaleMesh");
            prop.objectReferenceValue = smr.sharedMesh;
            so.ApplyModifiedPropertiesWithoutUndo();
            Debug.Log($"Reverted _femaleMesh = {smr.sharedMesh.name}");
        }
        AssetDatabase.SaveAssets();
    }
}
