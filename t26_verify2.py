import json, subprocess
code = r'''
var sb = new System.Text.StringBuilder();
var regType = System.Type.GetType("ProjectC.Items.ItemRegistry, Assembly-CSharp");
if (regType == null) {
    foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies()) {
        regType = asm.GetType("ProjectC.Items.ItemRegistry");
        if (regType != null) break;
    }
}
var reg = UnityEditor.AssetDatabase.LoadAssetAtPath("Assets/_Project/Items/Data/ItemRegistry.asset", regType);
if (reg == null) { sb.AppendLine("ERROR: ItemRegistry asset not found"); return sb.ToString(); }
var setInstance = regType.GetMethod("SetInstance", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
setInstance.Invoke(null, new object[] { reg });
sb.AppendLine("ItemRegistry.Instance set");
int count = 0;
var getEntries = regType.GetMethod("GetEntries");
var entries = (IEnumerable)getEntries.Invoke(reg, null);
foreach (var e in entries) {
    var et = e.GetType();
    int id = (int)et.GetField("id").GetValue(e);
    var item = (ProjectC.Items.ItemData)et.GetField("item").GetValue(e);
    string name = item != null ? item.itemName : "null";
    sb.AppendLine("  id=" + id + " -> " + name);
    count++;
}
sb.AppendLine("Total: " + count);
sb.AppendLine("ResolveItemId('Медная руда') = " + ProjectC.Quests.QuestWorld.ResolveItemId("Медная руда"));
sb.AppendLine("ResolveItemId('999') = " + ProjectC.Quests.QuestWorld.ResolveItemId("999"));
return sb.ToString();
'''
payload = json.dumps({"action":"execute","code":code})
r = subprocess.run(["python","C:/Users/leon7/AppData/Local/hermes/profiles/project-c/skills/unity-mcp-orchestrator/scripts/mcp_unity_client.py","tool","execute_code",payload],capture_output=True,text=True,timeout=120)
print(r.stdout[:5000])
