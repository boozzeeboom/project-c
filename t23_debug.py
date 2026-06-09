import json, subprocess
code = r'''
var sb = new System.Text.StringBuilder();

string scenePath = "Assets/_Project/Scenes/BootstrapScene.unity";
UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath, UnityEditor.SceneManagement.OpenSceneMode.Single);
sb.AppendLine("Scene: " + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);

// === Add [ToastDebugUI] to [ToastService] GameObject (so one UI Document) ===
var go = GameObject.Find("[ToastService]");
if (go == null) { sb.AppendLine("ERROR: [ToastService] not found"); return sb.ToString(); }

// Find ToastDebugUI type
var tType = System.Type.GetType("ProjectC.Testing.ToastDebugUI, Assembly-CSharp");
if (tType == null) {
    foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies()) {
        tType = asm.GetType("ProjectC.Testing.ToastDebugUI");
        if (tType != null) { sb.AppendLine("Found in: " + asm.GetName().Name); break; }
    }
}
if (tType == null) {
    sb.AppendLine("ERROR: ToastDebugUI type not found");
    return sb.ToString();
}

var comp = go.GetComponent(tType);
if (comp == null) {
    comp = go.AddComponent(tType);
    sb.AppendLine("Added ToastDebugUI to [ToastService]");
} else {
    sb.AppendLine("ToastDebugUI already attached");
}

UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
sb.AppendLine("Scene saved");
return sb.ToString();
'''
payload = json.dumps({"action":"execute","code":code})
r = subprocess.run(["python","C:/Users/leon7/AppData/Local/hermes/profiles/project-c/skills/unity-mcp-orchestrator/scripts/mcp_unity_client.py","tool","execute_code",payload],capture_output=True,text=True,timeout=120)
print(r.stdout[:2000])
