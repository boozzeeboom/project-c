import json, subprocess
code = r'''
var sb = new System.Text.StringBuilder();

// 1. Load ItemRegistry
var regType = System.Type.GetType("ProjectC.Items.ItemRegistry, Assembly-CSharp");
if (regType == null) {
    foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies()) {
        regType = asm.GetType("ProjectC.Items.ItemRegistry");
        if (regType != null) break;
    }
}
var reg = UnityEditor.AssetDatabase.LoadAssetAtPath("Assets/_Project/Items/Data/ItemRegistry.asset", regType);
if (reg == null) { sb.AppendLine("ERROR: ItemRegistry asset not found"); return sb.ToString(); }

// 2. Set as Instance + BuildCache
var setInstance = regType.GetMethod("SetInstance", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
setInstance.Invoke(null, new object[] { reg });
sb.AppendLine("ItemRegistry.Instance set");

// 3. Verify each entry resolves correctly
int count = 0;
foreach (var entry in ((IEnumerable<dynamic>)null)) { } // noop
var getEntries = regType.GetMethod("GetEntries");
var entries = (IEnumerable)getEntries.Invoke(reg, null);
sb.AppendLine("=== ItemRegistry entries ===");
foreach (var e in entries) {
    var entryType = e.GetType();
    var idF = entryType.GetField("id");
    var itemF = entryType.GetField("item");
    int id = (int)idF.GetValue(e);
    var item = (ProjectC.Items.ItemData)itemF.GetValue(e);
    string name = item != null ? item.itemName : "null";
    sb.AppendLine("  id=" + id + " -> " + name);
    count++;
}
sb.AppendLine("Total entries: " + count);

// 4. Verify QuestWorld.ResolveItemId returns same ids
sb.AppendLine("=== QuestWorld.ResolveItemId test ===");
sb.AppendLine("'Медная руда' -> " + ProjectC.Quests.QuestWorld.ResolveItemId("Медная руда") + " (expected 26)");
sb.AppendLine("'TimeCrystal' -> " + ProjectC.Quests.QuestWorld.ResolveItemId("TimeCrystal"));
sb.AppendLine("'999' (int) -> " + ProjectC.Quests.QuestWorld.ResolveItemId("999") + " (expected 999)");
return sb.ToString();
'''
payload = json.dumps({"action":"execute","code":code})
r = subprocess.run(["python","C:/Users/leon7/AppData/Local/hermes/profiles/project-c/skills/unity-mcp-orchestrator/scripts/mcp_unity_client.py","tool","execute_code",payload],capture_output=True,text=True,timeout=120)
print(r.stdout[:5000])
