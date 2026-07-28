#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

public static class InvestigateAnimator
{
    public static void Execute()
    {
        string[] prefabPaths = {
            "Assets/_Project/Prefabs/AI/Npc_Goblin.prefab",
            "Assets/_Project/Prefabs/NetworkPlayer.prefab"
        };

        foreach (var path in prefabPaths)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) { Debug.LogError($"Prefab not found: {path}"); continue; }

            var animators = prefab.GetComponentsInChildren<Animator>(true);
            Debug.Log($"=== {path} ===");
            foreach (var a in animators)
            {
                Debug.Log($"  Animator on: '{a.gameObject.name}' (path: {GetGameObjectPath(a.gameObject, prefab.transform)})");
                Debug.Log($"    applyRootMotion: {a.applyRootMotion}");
                Debug.Log($"    updateMode: {a.updateMode}");
                Debug.Log($"    cullingMode: {a.cullingMode}");
                Debug.Log($"    hasController: {a.runtimeAnimatorController != null}");
                if (a.runtimeAnimatorController != null)
                    Debug.Log($"    controller: {a.runtimeAnimatorController.name}");
                Debug.Log($"    avatar: {(a.avatar != null ? a.avatar.name : "null")}");
                Debug.Log($"    isHuman: {(a.avatar != null ? a.avatar.isHuman.ToString() : "N/A")}");
            }

            var smrs = prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            Debug.Log($"  SkinnedMeshRenderers: {smrs.Length}");
            foreach (var s in smrs)
            {
                Debug.Log($"    SMR on: '{s.gameObject.name}', updateWhenOffscreen={s.updateWhenOffscreen}, skinnedMotionVectors={s.skinnedMotionVectors}");
            }

            var nts = prefab.GetComponentsInChildren<Unity.Netcode.Components.NetworkTransform>(true);
            Debug.Log($"  NetworkTransforms: {nts.Length}");
            foreach (var nt in nts)
            {
                Debug.Log($"    NT on: '{nt.gameObject.name}', Interpolate={nt.Interpolate}, InLocalSpace={nt.InLocalSpace}");
            }
        }

        Debug.Log("=== DONE ===");
    }

    public static void FullTree()
    {
        string path = "Assets/_Project/Prefabs/NetworkPlayer.prefab";
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        DumpTree(prefab.transform, "");
    }

    private static void DumpTree(Transform t, string indent)
    {
        var a = t.GetComponent<Animator>();
        var smr = t.GetComponent<SkinnedMeshRenderer>();
        string extras = "";
        if (a != null) extras += $" [Animator: applyRootMotion={a.applyRootMotion}, hasCtrl={a.runtimeAnimatorController!=null}]";
        if (smr != null) extras += $" [SMR]";
        Debug.Log($"{indent}{t.name}{extras}");
        for (int i = 0; i < t.childCount; i++)
            DumpTree(t.GetChild(i), indent + "  ");
    }

    private static string GetGameObjectPath(GameObject go, Transform root)
    {
        string path = go.name;
        Transform t = go.transform;
        while (t.parent != null && t.parent != root)
        {
            t = t.parent;
            path = t.name + "/" + path;
        }
        return path;
    }
}
#endif
