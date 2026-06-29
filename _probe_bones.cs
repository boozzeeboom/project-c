var sb = new System.Text.StringBuilder();

void PrintH(UnityEngine.Transform t, int depth) {
    string indent = new string(' ', depth * 2);
    sb.AppendLine(indent + t.name);
    foreach (UnityEngine.Transform c in t)
        PrintH(c, depth + 1);
}

var go = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>("Assets/_Project/Prefabs/NetworkPlayer.prefab");
if (go == null) { sb.AppendLine("prefab null"); return sb.ToString(); }
var animator = go.GetComponentInChildren<UnityEngine.Animator>(true);
sb.AppendLine("Animator on: " + animator.gameObject.name);
PrintH(animator.transform, 0);

sb.AppendLine("=== Mixamo clip bindings ===");
var clip = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.AnimationClip>("Assets/_Project/Animations/Combat/Male/StandingMeleeAttack.anim");
if (clip == null) { sb.AppendLine("no .anim"); return sb.ToString(); }
var bindings = UnityEditor.AnimationUtility.GetCurveBindings(clip);
sb.AppendLine("Clip curves: " + bindings.Length + ", checking first 5 paths:");
int matches = 0;
for (int i = 0; i < bindings.Length; i++)
{
    var p = bindings[i].path;
    var found = animator.transform.Find(p);
    if (found != null) matches++;
}
sb.AppendLine("Total path-matches: " + matches + " / " + bindings.Length);
for (int i = 0; i < 5 && i < bindings.Length; i++)
{
    var found = animator.transform.Find(bindings[i].path);
    sb.AppendLine("  '" + bindings[i].path + "' prop='" + bindings[i].propertyName + "' found=" + (found != null));
}
return sb.ToString();