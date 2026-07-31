#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using ProjectC.Quests;

public static class QuestDebugVerify
{
    public static void Execute()
    {
        var guids = AssetDatabase.FindAssets("t:QuestDefinition");
        Debug.Log($"=== QuestDebug: {guids.Length} QuestDefinition assets ===");
        foreach (var g in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(g);
            var q = AssetDatabase.LoadAssetAtPath<QuestDefinition>(path);
            if (q == null) continue;
            int sc = q.stages?.Length ?? 0;
            Debug.Log($"  {path}: {sc} stages");
            if (q.stages != null)
                for (int i = 0; i < q.stages.Length; i++)
                {
                    var s = q.stages[i];
                    Debug.Log($"    [{i}] id={s?.stageId} next={s?.nextStageId} objs={s?.objectives?.Length ?? 0}");
                }
        }
        Debug.Log("=== QuestDebug: done ===");
    }
}
#endif
