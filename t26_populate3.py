import json, subprocess
code = r'''
var sb = new System.Text.StringBuilder();
string assetPath = "Assets/_Project/Items/Data/ItemRegistry.asset";
var tType = System.Type.GetType("ProjectC.Items.ItemRegistry, Assembly-CSharp");
if (tType == null) {
    foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies()) {
        tType = asm.GetType("ProjectC.Items.ItemRegistry");
        if (tType != null) break;
    }
}
if (tType == null) { sb.AppendLine("ERROR: ItemRegistry type not found"); return sb.ToString(); }
var so = UnityEditor.AssetDatabase.LoadAssetAtPath(assetPath, tType) as ScriptableObject;
if (so == null) { sb.AppendLine("ERROR: asset not found"); return sb.ToString(); }
var entryType = tType.GetNestedType("Entry");
sb.AppendLine("Entry type: " + entryType.FullName + " (IsValueType=" + entryType.IsValueType + ")");
var idField = entryType.GetField("id");
var itemField = entryType.GetField("item");
var entriesField = tType.GetField("entries", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
var listType = typeof(System.Collections.Generic.List<>).MakeGenericType(entryType);
var list = (System.Collections.IList)System.Activator.CreateInstance(listType);
var allItems = Resources.LoadAll<ProjectC.Items.ItemData>("Items");
int id = 1;
foreach (var item in allItems) {
    if (item == null) { id++; continue; }
    // Create struct instance using default + reflection
    object entry = System.Activator.CreateInstance(entryType);  // works for struct
    idField.SetValue(entry, id);
    itemField.SetValue(entry, item);
    list.Add(entry);
    id++;
}
entriesField.SetValue(so, list);
UnityEditor.EditorUtility.SetDirty(so);
UnityEditor.AssetDatabase.SaveAssets();
sb.AppendLine("Populated " + list.Count + " entries (id 1-" + (id-1) + ")");
return sb.ToString();
'''
payload = json.dumps({"action":"execute","code":code})
r = subprocess.run(["python","C:/Users/leon7/AppData/Local/hermes/profiles/project-c/skills/unity-mcp-orchestrator/scripts/mcp_unity_client.py","tool","execute_code",payload],capture_output=True,text=True,timeout=120)
print(r.stdout[:2000])
