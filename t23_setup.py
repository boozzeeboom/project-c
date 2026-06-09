import json, subprocess
code = r'''
var sb = new System.Text.StringBuilder();

// Open BootstrapScene
string scenePath = "Assets/_Project/Scenes/BootstrapScene.unity";
UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath, UnityEditor.SceneManagement.OpenSceneMode.Single);
sb.AppendLine("Scene: " + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);

// === Create [ToastService] GameObject (additive — not removing any) ===
string goName = "[ToastService]";
var existing = GameObject.Find(goName);
GameObject go;
if (existing == null)
{
    go = new GameObject(goName);
    sb.AppendLine("Created GameObject: " + goName);
}
else
{
    go = existing;
    sb.AppendLine("Reusing GameObject: " + goName);
}

// Add UIDocument first (ToastUI has [RequireComponent(typeof(UIDocument))])
var uiDoc = go.GetComponent<UnityEngine.UIElements.UIDocument>();
if (uiDoc == null) {
    uiDoc = go.AddComponent<UnityEngine.UIElements.UIDocument>();
    sb.AppendLine("Added UIDocument");
}

// Assign PanelSettings — find existing в проекте
var panelSettingsPath = "Assets/_Project/Quests/Resources/UI/QuestTrackerPanelSettings.asset";
var ps = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.UIElements.PanelSettings>(panelSettingsPath);
if (ps != null) {
    uiDoc.panelSettings = ps;
    sb.AppendLine("Assigned PanelSettings: " + panelSettingsPath);
} else {
    sb.AppendLine("WARN: PanelSettings not found at " + panelSettingsPath);
}

// Add ToastUI component via reflection (Roslyn не видит свеже-скомпилированные типы)
var tType = System.Type.GetType("ProjectC.UI.Toast.ToastUI, Assembly-CSharp");
if (tType == null) {
    foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies()) {
        tType = asm.GetType("ProjectC.UI.Toast.ToastUI");
        if (tType != null) { sb.AppendLine("Found in: " + asm.GetName().Name); break; }
    }
}
if (tType == null) {
    sb.AppendLine("ERROR: ToastUI type not found");
    return sb.ToString();
}
var comp = go.GetComponent(tType);
if (comp == null) {
    comp = go.AddComponent(tType);
    sb.AppendLine("Added ToastUI component");
} else {
    sb.AppendLine("ToastUI already attached");
}

UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
sb.AppendLine("Scene saved");

// Final state
var finalComp = go.GetComponent(tType);
sb.AppendLine("Final: " + goName + " components=" + (finalComp != null ? "ToastUI+UIDocument" : "ToastUI MISSING"));
return sb.ToString();
'''
payload = json.dumps({"action":"execute","code":code})
r = subprocess.run(["python","C:/Users/leon7/AppData/Local/hermes/profiles/project-c/skills/unity-mcp-orchestrator/scripts/mcp_unity_client.py","tool","execute_code",payload],capture_output=True,text=True,timeout=120)
print(r.stdout[:2000])
