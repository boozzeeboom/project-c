import json, subprocess
code = r'''
var sb = new System.Text.StringBuilder();

string scenePath = "Assets/_Project/Scenes/BootstrapScene.unity";
UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath, UnityEditor.SceneManagement.OpenSceneMode.Single);
sb.AppendLine("Scene: " + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);

// === Remove old [ToastService] (T-Q23 superseded by QuestToast) ===
var oldGo = GameObject.Find("[ToastService]");
if (oldGo != null) {
    UnityEngine.Object.DestroyImmediate(oldGo);
    sb.AppendLine("Removed old [ToastService] GameObject (superseded)");
}

// === Create [QuestToast] GameObject (additive — other objects NOT touched) ===
string goName = "[QuestToast]";
var existing = GameObject.Find(goName);
GameObject go;
if (existing == null)
{
    go = new GameObject(goName);
    sb.AppendLine("Created: " + goName);
} else {
    go = existing;
    sb.AppendLine("Reusing: " + goName);
}

// Add UIDocument first ([RequireComponent(typeof(UIDocument))])
var uiDoc = go.GetComponent<UnityEngine.UIElements.UIDocument>();
if (uiDoc == null) {
    uiDoc = go.AddComponent<UnityEngine.UIElements.UIDocument>();
    sb.AppendLine("Added UIDocument");
}

// Assign PanelSettings (reuse same as QuestTracker)
var panelSettingsPath = "Assets/_Project/Quests/Resources/UI/QuestTrackerPanelSettings.asset";
var ps = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.UIElements.PanelSettings>(panelSettingsPath);
if (ps != null) {
    uiDoc.panelSettings = ps;
    sb.AppendLine("PanelSettings: " + panelSettingsPath);
}

// Add QuestToast component via reflection
var tType = System.Type.GetType("ProjectC.Quests.UI.QuestToast, Assembly-CSharp");
if (tType == null) {
    foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies()) {
        tType = asm.GetType("ProjectC.Quests.UI.QuestToast");
        if (tType != null) { sb.AppendLine("Found in: " + asm.GetName().Name); break; }
    }
}
if (tType == null) {
    sb.AppendLine("ERROR: QuestToast type not found");
    return sb.ToString();
}
var comp = go.GetComponent(tType);
if (comp == null) {
    comp = go.AddComponent(tType);
    sb.AppendLine("Added QuestToast component");
} else {
    sb.AppendLine("QuestToast already attached");
}

UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
sb.AppendLine("Scene saved");
return sb.ToString();
'''
payload = json.dumps({"action":"execute","code":code})
r = subprocess.run(["python","C:/Users/leon7/AppData/Local/hermes/profiles/project-c/skills/unity-mcp-orchestrator/scripts/mcp_unity_client.py","tool","execute_code",payload],capture_output=True,text=True,timeout=120)
print(r.stdout[:2000])
