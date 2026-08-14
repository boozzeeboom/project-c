using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public static class ReskinFemalePilot
{
    public static void Execute()
    {
        string srcPath = "Assets/Generated_Models/Female_Pilot_Rigged/Female_Pilot_Rigged.glb";
        // Целевой скелет = тот, что реально стоит на _bodyRenderer в NetworkPlayer (HumanM_BodyMesh).
        string tgtPath = "Assets/Kevin Iglesias/Human Animations/Models/HumanM_Model.fbx";
        string outPath = "Assets/Generated_Models/Female_Pilot/Female_Pilot_BodyMesh.asset";

        var srcPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(srcPath);
        var tgtPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(tgtPath);
        if (srcPrefab == null) { Debug.LogError("SRC load fail: " + srcPath); return; }
        if (tgtPrefab == null) { Debug.LogError("TGT load fail: " + tgtPath); return; }

        var srcGO = Object.Instantiate(srcPrefab);
        var tgtGO = Object.Instantiate(tgtPrefab);
        try
        {
            var srcSMR = srcGO.GetComponentInChildren<SkinnedMeshRenderer>(true);
            var tgtSMR = tgtGO.GetComponentInChildren<SkinnedMeshRenderer>(true);
            var srcMesh = srcSMR.sharedMesh;
            var tgtMesh = tgtSMR.sharedMesh;

            Debug.Log($"SRC bones={srcSMR.bones.Length}, verts={srcMesh.vertexCount}");
            Debug.Log($"TGT bones={tgtSMR.bones.Length}, verts={tgtMesh.vertexCount}");

            // 1. Scale source to match target height (world).
            float srcH = srcSMR.bounds.size.y;
            float tgtH = tgtSMR.bounds.size.y;
            float scale = tgtH / Mathf.Max(srcH, 0.0001f);
            srcGO.transform.localScale = Vector3.one * scale;
            Debug.Log($"Scale {srcH:F3} -> {tgtH:F3} = {scale:F3}");

            // 2. Source bone index -> target bone index.
            int[] map = BuildBoneMap(srcSMR.bones, tgtSMR.bones);

            // 3. Re-skin.
            var srcVerts = srcMesh.vertices;
            var srcWeights = srcMesh.boneWeights;
            var tgtBindposes = tgtMesh.bindposes;

            var newVerts = new Vector3[srcVerts.Length];
            var newWeights = new BoneWeight[srcWeights.Length];

            Matrix4x4 srcLocalToWorld = srcSMR.transform.localToWorldMatrix;
            Matrix4x4 tgtWorldToLocal = tgtSMR.transform.worldToLocalMatrix;

            int unmapped = 0;
            for (int i = 0; i < srcVerts.Length; i++)
            {
                Vector3 vWorld = srcLocalToWorld.MultiplyPoint3x4(srcVerts[i]);
                newVerts[i] = tgtWorldToLocal.MultiplyPoint3x4(vWorld);
                newWeights[i] = Remap(srcWeights[i], map, ref unmapped);
            }
            if (unmapped > 0) Debug.LogWarning($"Unmapped weight entries: {unmapped}");

            var newMesh = new Mesh { name = "Female_Pilot_BodyMesh" };
            newMesh.vertices = newVerts;
            newMesh.normals = srcMesh.normals;
            newMesh.tangents = srcMesh.tangents;
            newMesh.uv = srcMesh.uv;
            newMesh.uv2 = srcMesh.uv2;
            newMesh.colors = srcMesh.colors;
            newMesh.triangles = srcMesh.triangles;
            newMesh.boneWeights = newWeights;
            newMesh.bindposes = tgtBindposes; // тот же порядок, что и у tgtSMR.bones
            newMesh.RecalculateBounds();

            if (AssetDatabase.LoadAssetAtPath<Mesh>(outPath) != null)
                AssetDatabase.DeleteAsset(outPath);
            AssetDatabase.CreateAsset(newMesh, outPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"DONE: {outPath} | verts={newMesh.vertexCount} | bindposes={newMesh.bindposes.Length} | bounds={newMesh.bounds}");
        }
        finally
        {
            Object.DestroyImmediate(srcGO);
            Object.DestroyImmediate(tgtGO);
        }
    }

    static int[] BuildBoneMap(Transform[] srcBones, Transform[] tgtBones)
    {
        var dict = new Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase);
        for (int t = 0; t < tgtBones.Length; t++)
            if (tgtBones[t] != null) dict[tgtBones[t].name] = t;

        var map = new int[srcBones.Length];
        for (int s = 0; s < srcBones.Length; s++)
        {
            string src = srcBones[s] != null ? srcBones[s].name : "";
            string tgt = MapName(src);
            map[s] = dict.TryGetValue(tgt, out int idx) ? idx : -1;
        }
        return map;
    }

    static string MapName(string src)
    {
        switch (src)
        {
            case "Root":
            case "Hip":
            case "Pelvis": return "B-hips";
            case "Waist":
            case "Spine01": return "B-spine";
            case "Spine02": return "B-chest";
            case "NeckTwist01":
            case "NeckTwist02": return "B-neck";
            case "Head": return "B-head";

            case "L_Clavicle": return "B-shoulder.L";
            case "L_Upperarm":
            case "L_UpperarmTwist01":
            case "L_UpperarmTwist02": return "B-upperArm.L";
            case "L_Forearm":
            case "L_ForearmTwist01":
            case "L_ForearmTwist02": return "B-forearm.L";
            case "L_Hand": return "B-hand.L";
            case "L_Thigh":
            case "L_ThighTwist01":
            case "L_ThighTwist02": return "B-thigh.L";
            case "L_Calf":
            case "L_CalfTwist01":
            case "L_CalfTwist02": return "B-shin.L";
            case "L_Foot": return "B-foot.L";
            case "L_ToeBase": return "B-toe.L";

            case "R_Clavicle": return "B-shoulder.R";
            case "R_Upperarm":
            case "R_UpperarmTwist01":
            case "R_UpperarmTwist02": return "B-upperArm.R";
            case "R_Forearm":
            case "R_ForearmTwist01":
            case "R_ForearmTwist02": return "B-forearm.R";
            case "R_Hand": return "B-hand.R";
            case "R_Thigh":
            case "R_ThighTwist01":
            case "R_ThighTwist02": return "B-thigh.R";
            case "R_Calf":
            case "R_CalfTwist01":
            case "R_CalfTwist02": return "B-shin.R";
            case "R_Foot": return "B-foot.R";
            case "R_ToeBase": return "B-toe.R";
        }
        return src;
    }

    static BoneWeight Remap(BoneWeight bw, int[] map, ref int unmapped)
    {
        // Собираем веса по целевому индексу (мёрджим твисты в родителя).
        var acc = new Dictionary<int, float>();
        Add(acc, bw.boneIndex0, bw.weight0, map, ref unmapped);
        Add(acc, bw.boneIndex1, bw.weight1, map, ref unmapped);
        Add(acc, bw.boneIndex2, bw.weight2, map, ref unmapped);
        Add(acc, bw.boneIndex3, bw.weight3, map, ref unmapped);

        // Сортируем по убыванию веса, берём 4 топовых.
        var list = new List<KeyValuePair<int, float>>(acc);
        list.Sort((a, b) => b.Value.CompareTo(a.Value));
        while (list.Count > 4) list.RemoveAt(list.Count - 1);

        float sum = 0f;
        foreach (var kv in list) sum += kv.Value;
        if (sum <= 0f) sum = 1f;

        var r = new BoneWeight();
        void Set(int slot, int idx, float w)
        {
            switch (slot)
            {
                case 0: r.boneIndex0 = idx; r.weight0 = w / sum; break;
                case 1: r.boneIndex1 = idx; r.weight1 = w / sum; break;
                case 2: r.boneIndex2 = idx; r.weight2 = w / sum; break;
                case 3: r.boneIndex3 = idx; r.weight3 = w / sum; break;
            }
        }
        for (int i = 0; i < list.Count; i++)
        {
            int idx = list[i].Key;
            if (idx < 0) { unmapped++; continue; }
            Set(i, idx, list[i].Value);
        }
        return r;
    }

    static void Add(Dictionary<int, float> acc, int boneIdx, float w, int[] map, ref int unmapped)
    {
        if (w <= 0.0001f) return;
        int target = (boneIdx >= 0 && boneIdx < map.Length) ? map[boneIdx] : -1;
        if (target < 0) { unmapped++; return; }
        if (acc.TryGetValue(target, out float old)) acc[target] = old + w;
        else acc[target] = w;
    }
}
