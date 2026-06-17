"""
T-CARGO-01: Создание Assets/Resources/ShipClassMapping.asset
через Roslyn-string-based CreateInstance (pitfall #27a).
Запускается через `python` который дергает mcp_unity_client.py.
"""
import json
import subprocess
import sys
import os

# ВАЖНО: Roslyn snippet — одна строка, без `using`, fully-qualified types.
# Pitfall #9: bash heredoc ломает длинные payload'ы — пишем напрямую через subprocess.
CODE_MKDIR = (
    "var dir = \"Assets/Resources\"; "
    "if (!System.IO.Directory.Exists(dir)) { "
    "UnityEditor.AssetDatabase.CreateFolder(\"Assets\", \"Resources\"); "
    "return \"created\"; } "
    "return \"exists\";"
)

# Создание asset через string-based CreateInstance (Roslyn cache bypass).
# Заполняем 4 пары: (Light,Light),(Medium,Medium),(Heavy,HeavyI),(HeavyII,HeavyII)
# 0=Light, 1=Medium, 2=Heavy, 3=HeavyII для обоих enum'ов.
CODE_CREATE = (
    "var t = System.Type.GetType(\"ProjectC.Ship.ShipClassMappingConfig, Assembly-CSharp\"); "
    "if (t == null) { return \"TYPE_NOT_FOUND: ShipClassMappingConfig\"; } "
    "var so = UnityEngine.ScriptableObject.CreateInstance(t) as UnityEngine.ScriptableObject; "
    "if (so == null) { return \"CREATE_INSTANCE_FAILED\"; } "
    "var mappingsField = t.GetField(\"mappings\", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance); "
    "if (mappingsField == null) { return \"FIELD_NOT_FOUND: mappings\"; } "
    "var entryType = System.Type.GetType(\"ProjectC.Ship.ShipClassMapping, Assembly-CSharp\"); "
    "if (entryType == null) { return \"TYPE_NOT_FOUND: ShipClassMapping\"; } "
    "var listType = typeof(System.Collections.Generic.List<>).MakeGenericType(entryType); "
    "var list = System.Activator.CreateInstance(listType); "
    "var addMethod = listType.GetMethod(\"Add\"); "
    "var fcField = entryType.GetField(\"flightClass\"); "
    "var ccField = entryType.GetField(\"cargoClass\"); "
    "var fcType = System.Type.GetType(\"ProjectC.Player.ShipFlightClass, Assembly-CSharp\"); "
    "var ccType = System.Type.GetType(\"ProjectC.Trade.Core.ShipClass, Assembly-CSharp\"); "
    "if (fcType == null) { return \"TYPE_NOT_FOUND: ShipFlightClass\"; } "
    "if (ccType == null) { return \"TYPE_NOT_FOUND: ShipClass\"; } "
    # pairs: [flightEnum, cargoEnum]
    "int[] fcVals = new int[]{ 0, 1, 2, 3 }; "
    "int[] ccVals = new int[]{ 0, 1, 2, 3 }; "
    "for (int i = 0; i < 4; i++) { "
    "object entry = System.Activator.CreateInstance(entryType); "
    "fcField.SetValue(entry, System.Enum.ToObject(fcType, fcVals[i])); "
    "ccField.SetValue(entry, System.Enum.ToObject(ccType, ccVals[i])); "
    "addMethod.Invoke(list, new object[] { entry }); "
    "} "
    "mappingsField.SetValue(so, list); "
    "UnityEditor.AssetDatabase.CreateAsset(so, \"Assets/Resources/ShipClassMapping.asset\"); "
    "UnityEditor.AssetDatabase.SaveAssets(); "
    "UnityEditor.AssetDatabase.Refresh(); "
    "return \"OK mappings=\" + ((System.Collections.ICollection)list).Count;"
)

def call_unity(tool, payload_obj):
    payload_str = json.dumps(payload_obj)
    cmd = [
        "python",
        "C:/Users/leon7/AppData/Local/hermes/profiles/project-c/skills/unity-mcp-orchestrator/scripts/mcp_unity_client.py",
        "tool", tool, payload_str,
    ]
    r = subprocess.run(cmd, capture_output=True, text=True, timeout=90)
    return r.returncode, r.stdout, r.stderr

# Step 1: ensure Resources dir
print("=== MKDIR ===")
rc, out, err = call_unity("execute_code", {"action": "execute", "code": CODE_MKDIR})
print(f"rc={rc}")
print("STDOUT:", out[:600])
print("STDERR:", err[:300])

# Step 2: create asset
print("\n=== CREATE ASSET ===")
rc, out, err = call_unity("execute_code", {"action": "execute", "code": CODE_CREATE})
print(f"rc={rc}")
print("STDOUT:", out[:800])
print("STDERR:", err[:300])
