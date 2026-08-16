using UnityEngine;
using UnityEditor;

public static class InspectSkeletons
{
    public static void Execute()
    {
        string riggedPath = "Assets/Generated_Models/Female_Pilot_Rigged/Female_Pilot_Rigged.glb";
        string humanFPath = "Assets/Kevin Iglesias/Human Animations/Models/HumanF_Model.fbx";

        Debug.Log("===== RIGGED GLB =====");
        DumpModel(riggedPath);

        Debug.Log("===== HumanF_Model =====");
        DumpModel(humanFPath);
    }

    static void DumpModel(string path)
    {
        var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (go == null)
        {
            Debug.LogError("Cannot load: " + path);
            return;
        }

        var smrs = go.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        Debug.Log($"SkinnedMeshRenderer count: {smrs.Length}");
        foreach (var smr in smrs)
        {
            Debug.Log($"--- SMR: {smr.name} | mesh: {(smr.sharedMesh != null ? smr.sharedMesh.name : "null")} | verts: {(smr.sharedMesh != null ? smr.sharedMesh.vertexCount : 0)} | bones: {smr.bones.Length}");
            var names = new System.Collections.Generic.List<string>();
            foreach (var b in smr.bones)
            {
                if (b != null) names.Add(b.name);
                else names.Add("<null>");
            }
            Debug.Log("Bones: " + string.Join(", ", names));
        }
    }
}
