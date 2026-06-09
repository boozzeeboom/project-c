import json, subprocess
# Simplify: directly use a List<Entry> via List<T>.
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
if (entryType == null) { sb.AppendLine("ERROR: Entry type not found in ItemRegistry"); return sb.ToString(); }
sb.AppendLine("Entry type: " + entryType.FullName);

var entryCtor = entryType.GetConstructor(System.Type.EmptyTypes);
if (entryCtor == null) { sb.AppendLine("ERROR: Entry ctor not found"); return sb.ToString(); }

var idField = entryType.GetField("id");
var itemField = entryType.GetField("item");
if (idField == null || itemField == null) { sb.AppendLine("ERROR: id/item field not found"); return sb.ToString(); }

var entriesField = tType.GetField("entries", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
if (entriesField == null) { sb.AppendLine("ERROR: entries field not found"); return sb.ToString(); }

// Build typed List<Entry>
var listType = typeof(System.Collections.Generic.List<>).MakeGenericType(entryType);
var list = (System.Collections.IList)System.Activator.CreateInstance(listType);

var allItems = Resources.LoadAll<ProjectC.Items.ItemData>("Items");
int id = 1;
foreach (var item in allItems) {
    if (item == null) { id++; continue; }
    var entry = entryCtor.Invoke(null);
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
