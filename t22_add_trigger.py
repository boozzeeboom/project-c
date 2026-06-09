import json, subprocess
# T-Q22: Add trigger zone for stage_intro_demo. Same pattern as M13 trigger, but questId="stage_intro_demo".
# Place в стороне от других triggers (Mira+3m N уже занят TestStageItem pickup → +3m E будет нормально).

code = r'''
var sb = new System.Text.StringBuilder();

// Ensure scene open
string scenePath = "Assets/_Project/Scenes/World/WorldScene_0_0.unity";
UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath, UnityEditor.SceneManagement.OpenSceneMode.Single);
sb.AppendLine("Scene: " + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);

string triggerName = "TriggerZone_StageIntro";
var existing = GameObject.Find(triggerName);
if (existing != null) {
    sb.AppendLine("Trigger already exists: " + triggerName);
    return sb.ToString();
}

var mira = GameObject.Find("[Mira]");
UnityEngine.Vector3 miraPos = UnityEngine.Vector3.zero;
if (mira != null) miraPos = mira.transform.position;

// Place trigger в 8m E от Mira (existing trigger — 12m W; TestStageItem pickup — 3m N; copper — 5m E/S/W)
UnityEngine.Vector3 trigPos = new UnityEngine.Vector3(miraPos.x + 8f, miraPos.y, miraPos.z + 0f);

var trig = new GameObject(triggerName);
trig.transform.position = trigPos;

// T-Q22 fix: BoxCollider ДО AddComponent (M13QuestTriggerZone требует [RequireComponent(typeof(Collider))])
var col = trig.AddComponent<UnityEngine.BoxCollider>();
col.isTrigger = true;
col.size = new UnityEngine.Vector3(4f, 3f, 4f);
col.center = new UnityEngine.Vector3(0f, 1.5f, 0f);

// Add M13QuestTriggerZone component via reflection
var trgType = System.Type.GetType("ProjectC.Quests.Testing.M13QuestTriggerZone, Assembly-CSharp");
if (trgType == null) {
    foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies()) {
        trgType = asm.GetType("ProjectC.Quests.Testing.M13QuestTriggerZone");
        if (trgType != null) break;
    }
}
if (trgType == null) {
    sb.AppendLine("ERROR: M13QuestTriggerZone type not found");
    return sb.ToString();
}
var comp = trig.AddComponent(trgType);
if (comp == null) {
    sb.AppendLine("ERROR: AddComponent returned null");
    return sb.ToString();
}

// Set questId = "stage_intro_demo" via SerializedObject
var so = new UnityEditor.SerializedObject(comp);
var questIdProp = so.FindProperty("questId");
if (questIdProp != null) {
    questIdProp.stringValue = "stage_intro_demo";
    so.ApplyModifiedProperties();
}
sb.AppendLine("Set questId=" + questIdProp.stringValue);

// Visual marker — зелёный полупрозрачный куб (как у M13 trigger)
var visual = GameObject.CreatePrimitive(UnityEngine.PrimitiveType.Cube);
visual.name = "VisualMarker";
visual.transform.SetParent(trig.transform, false);
visual.transform.localPosition = new UnityEngine.Vector3(0f, 0.1f, 0f);
visual.transform.localScale = new UnityEngine.Vector3(3.5f, 0.05f, 3.5f);
var rend = visual.GetComponent<UnityEngine.Renderer>();
if (rend != null) {
    rend.material.color = new UnityEngine.Color(0.3f, 0.8f, 0.3f, 0.5f);
}
// remove collider from visual (trigger does the work)
var vcol = visual.GetComponent<UnityEngine.BoxCollider>();
if (vcol != null) UnityEngine.Object.DestroyImmediate(vcol);

sb.AppendLine("Trigger created @ " + trigPos);

UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
sb.AppendLine("Scene saved");
return sb.ToString();
'''
payload = json.dumps({"action":"execute","code":code})
r = subprocess.run(["python","C:/Users/leon7/AppData/Local/hermes/profiles/project-c/skills/unity-mcp-orchestrator/scripts/mcp_unity_client.py","tool","execute_code",payload],capture_output=True,text=True,timeout=90)
print(r.stdout[:2000])
