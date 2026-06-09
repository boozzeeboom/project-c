import json, subprocess, os

# Delete broken asset
asset_path = "C:/UNITY_PROJECTS/ProjectC_client/Assets/_Project/Resources/Items/Item_Resource_TestStageItem.asset"
meta_path = asset_path + ".meta"
if os.path.exists(asset_path):
    os.remove(asset_path)
    print("Removed:", asset_path)
if os.path.exists(meta_path):
    os.remove(meta_path)
    print("Removed:", meta_path)

# Create properly via Roslyn (gets correct script guid)
code = r'''
var sb = new System.Text.StringBuilder();
string itemPath = "Assets/_Project/Resources/Items/Item_Resource_TestStageItem.asset";
var item = UnityEngine.ScriptableObject.CreateInstance<ProjectC.Items.ItemData>();
item.itemName = "TestStageItem";
item.itemType = ProjectC.Items.ItemType.Resources;
item.maxStack = 99;
UnityEditor.AssetDatabase.CreateAsset(item, itemPath);
UnityEditor.AssetDatabase.SaveAssets();
sb.AppendLine("Created: " + itemPath + " name=" + item.itemName + " type=" + item.itemType);
return sb.ToString();
'''
payload = json.dumps({"action":"execute","code":code})
r = subprocess.run(["python","C:/Users/leon7/AppData/Local/hermes/profiles/project-c/skills/unity-mcp-orchestrator/scripts/mcp_unity_client.py","tool","execute_code",payload],capture_output=True,text=True,timeout=60)
print(r.stdout[:1500])
