import json, subprocess
# T-Q26: Create ItemRegistry asset populated from current InventoryWorld._itemDatabase order.
# Use the same order that InventoryWorld uses, so ids stay identical.
code = r'''
var sb = new System.Text.StringBuilder();

// Find existing ItemRegistry type
var tType = System.Type.GetType("ProjectC.Items.ItemRegistry, Assembly-CSharp");
if (tType == null) {
    foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies()) {
        tType = asm.GetType("ProjectC.Items.ItemRegistry");
        if (tType != null) break;
    }
}
if (tType == null) { sb.AppendLine("ERROR: ItemRegistry type not found"); return sb.ToString(); }

// Create the asset
string assetPath = "Assets/_Project/Items/Data/ItemRegistry.asset";
var existing = UnityEditor.AssetDatabase.LoadAssetAtPath(assetPath, tType);
ScriptableObject so;
if (existing == null) {
    so = (ScriptableObject)ScriptableObject.CreateInstance(tType);
    UnityEditor.AssetDatabase.CreateAsset(so, assetPath);
    sb.AppendLine("Created: " + assetPath);
} else {
    so = (ScriptableObject)existing;
    sb.AppendLine("Reusing: " + assetPath);
}

// Get entries field
var entriesField = tType.GetField("entries", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
if (entriesField == null) { sb.AppendLine("ERROR: entries field not found"); return sb.ToString(); }

// Get current registered items from InventoryWorld
var iw = ProjectC.Items.InventoryWorld.Instance;
if (iw == null) {
    sb.AppendLine("WARN: InventoryWorld.Instance == null (start host first to register items)");
} else {
    var dbField = typeof(ProjectC.Items.InventoryWorld).GetField("_itemDatabase", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
    var db = dbField.GetValue(iw) as System.Collections.Generic.Dictionary<int, ProjectC.Items.ItemData>;
    if (db != null) {
        // Entry type is nested: ProjectC.Items.ItemRegistry+Entry
        var entryType = tType.GetNestedType("Entry");
        if (entryType == null) { sb.AppendLine("ERROR: Entry nested type not found"); return sb.ToString(); }
        var entryCtor = entryType.GetConstructor(System.Type.EmptyTypes);
        var idField = entryType.GetField("id");
        var itemField = entryType.GetField("item");
        var entriesList = (System.Collections.IList)System.Activator.CreateInstance(typeof(System.Collections.Generic.List<>).MakeGenericType(entryType));
        var sortedKeys = new System.Collections.Generic.List<int>(db.Keys);
        sortedKeys.Sort();
        foreach (var k in sortedKeys) {
            var item = db[k];
            if (item == null) continue;
            var entry = entryCtor.Invoke(null);
            idField.SetValue(entry, k);
            itemField.SetValue(entry, item);
            entriesList.Add(entry);
        }
        entriesField.SetValue(so, entriesList);
        sb.AppendLine("Populated " + entriesList.Count + " entries from InventoryWorld");
    } else {
        sb.AppendLine("ERROR: _itemDatabase not found");
    }
}

UnityEditor.EditorUtility.SetDirty(so);
UnityEditor.AssetDatabase.SaveAssets();
UnityEditor.AssetDatabase.Refresh();
sb.AppendLine("Asset saved");

// Set as Instance for runtime
var setInstMethod = tType.GetMethod("SetInstance", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
if (setInstMethod != null) {
    setInstMethod.Invoke(null, new object[] { so });
    sb.AppendLine("Set as ItemRegistry.Instance");
}
return sb.ToString();
'''
payload = json.dumps({"action":"execute","code":code})
r = subprocess.run(["python","C:/Users/leon7/AppData/Local/hermes/profiles/project-c/skills/unity-mcp-orchestrator/scripts/mcp_unity_client.py","tool","execute_code",payload],capture_output=True,text=True,timeout=120)
print(r.stdout[:2000])
