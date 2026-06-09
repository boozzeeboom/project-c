import json, subprocess
# Populate ItemRegistry from Resources.LoadAll (same order as InventoryWorld.RegisterAllItems).
code = r'''
var sb = new System.Text.StringBuilder();
string assetPath = "Assets/_Project/Items/Data/ItemRegistry.asset";
var tType = System.Type.GetType("ProjectC.Items.ItemRegistry, Assembly-CSharp");
var so = UnityEditor.AssetDatabase.LoadAssetAtPath(assetPath, tType) as ScriptableObject;
if (so == null) { sb.AppendLine("ERROR: not found"); return sb.ToString(); }

var entriesField = tType.GetField("entries", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
var entryType = tType.GetNestedType("Entry");
var entryCtor = entryType.GetConstructor(System.Type.EmptyTypes);
var idField = entryType.GetField("id");
var itemField = entryType.GetField("item");

// Load from Resources/Items/ (same as InventoryWorld.RegisterAllItems line 1)
var allItems = Resources.LoadAll<ProjectC.Items.ItemData>("Items");
sb.AppendLine("Found " + allItems.Length + " items in Resources/Items/");
var entriesList = (System.Collections.IList)System.Activator.CreateInstance(typeof(System.Collections.Generic.List<>).MakeGenericType(entryType));
int id = 1;
foreach (var item in allItems) {
    if (item == null) { id++; continue; }
    var entry = entryCtor.Invoke(null);
    idField.SetValue(entry, id);
    itemField.SetValue(entry, item);
    entriesList.Add(entry);
    id++;
}
entriesField.SetValue(so, entriesList);
UnityEditor.EditorUtility.SetDirty(so);
UnityEditor.AssetDatabase.SaveAssets();
sb.AppendLine("Populated " + entriesList.Count + " entries (id 1-" + (id-1) + ")");

// Verify via cache rebuild
var buildCache = tType.GetMethod("BuildCache", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
buildCache.Invoke(so, null);
return sb.ToString();
'''
payload = json.dumps({"action":"execute","code":code})
r = subprocess.run(["python","C:/Users/leon7/AppData/Local/hermes/profiles/project-c/skills/unity-mcp-orchestrator/scripts/mcp_unity_client.py","tool","execute_code",payload],capture_output=True,text=True,timeout=120)
print(r.stdout[:2000])
