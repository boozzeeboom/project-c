import json, subprocess, sys

MCP_SCRIPT = "C:/Users/leon7/AppData/Local/hermes/profiles/project-c/skills/unity-mcp-orchestrator/scripts/mcp_unity_client.py"

code = '''
var t = System.Type.GetType("ProjectC.Trade.Config.ExchangeRateConfig, Assembly-CSharp");
if (t == null) return "FAIL: type not found";
var cfg = UnityEngine.ScriptableObject.CreateInstance(t);
var so = new UnityEditor.SerializedObject(cfg);
var rates = so.FindProperty("rates");
rates.arraySize = 3;
var e0 = rates.GetArrayElementAtIndex(0);
e0.FindPropertyRelative("warehouseItemId").stringValue = "resource_iron_box";
e0.FindPropertyRelative("warehouseQty").intValue = 1;
e0.FindPropertyRelative("inventoryItemName").stringValue = "Железная руда";
e0.FindPropertyRelative("inventoryQty").intValue = 100;
e0.FindPropertyRelative("displayName").stringValue = "Ящик железной руды";
var e1 = rates.GetArrayElementAtIndex(1);
e1.FindPropertyRelative("warehouseItemId").stringValue = "resource_copper_box";
e1.FindPropertyRelative("warehouseQty").intValue = 1;
e1.FindPropertyRelative("inventoryItemName").stringValue = "Медная руда";
e1.FindPropertyRelative("inventoryQty").intValue = 100;
e1.FindPropertyRelative("displayName").stringValue = "Ящик медной руды";
var e2 = rates.GetArrayElementAtIndex(2);
e2.FindPropertyRelative("warehouseItemId").stringValue = "resource_wood_box";
e2.FindPropertyRelative("warehouseQty").intValue = 1;
e2.FindPropertyRelative("inventoryItemName").stringValue = "Древесина";
e2.FindPropertyRelative("inventoryQty").intValue = 100;
e2.FindPropertyRelative("displayName").stringValue = "Ящик древесины";
so.ApplyModifiedPropertiesWithoutUndo();
var path = "Assets/_Project/Resources/Exchange/DefaultExchangeRate.asset";
UnityEditor.AssetDatabase.CreateAsset(cfg, path);
UnityEditor.AssetDatabase.SaveAssets();
UnityEditor.AssetDatabase.Refresh();
return "CREATED: " + path;
'''

payload = json.dumps({"action": "execute", "code": code, "safety_checks": False})
result = subprocess.run(
    [sys.executable, MCP_SCRIPT, "tool", "execute_code", payload],
    capture_output=True, text=True, timeout=60
)
print(result.stdout)
if result.stderr:
    print("STDERR:", result.stderr[:500])
