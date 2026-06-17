"""T-CARGO-01 verify: загрузить asset и проверить Resolve()."""
import json, subprocess

CODE = (
    "var t = System.Type.GetType(\"ProjectC.Ship.ShipClassMappingConfig, Assembly-CSharp\"); "
    "if (t == null) { return \"TYPE_NOT_FOUND\"; } "
    "var asset = UnityEditor.AssetDatabase.LoadAssetAtPath(\"Assets/Resources/ShipClassMapping.asset\", t); "
    "if (asset == null) { return \"ASSET_NOT_FOUND\"; } "
    "var mappingsField = t.GetField(\"mappings\"); "
    "var list = mappingsField.GetValue(asset) as System.Collections.IList; "
    "var sb = new System.Text.StringBuilder(); "
    "sb.Append(\"asset=ok count=\" + list.Count + \" \"); "
    "var resolveMethod = t.GetMethod(\"Resolve\"); "
    "var fcType = System.Type.GetType(\"ProjectC.Player.ShipFlightClass, Assembly-CSharp\"); "
    "var ccType = System.Type.GetType(\"ProjectC.Trade.Core.ShipClass, Assembly-CSharp\"); "
    "for (int i = 0; i < 4; i++) { "
    "object fc = System.Enum.ToObject(fcType, i); "
    "object cc = resolveMethod.Invoke(asset, new object[] { fc }); "
    "sb.Append(fc.ToString() + \"->\" + (cc == null ? \"null\" : cc.ToString()) + \" \"); "
    "} "
    # Default loader test
    "var defaultProp = t.GetProperty(\"Default\", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static); "
    "var def = defaultProp.GetValue(null); "
    "sb.Append(\"default=ok\"); "
    "return sb.ToString();"
)

cmd = ["python",
       "C:/Users/leon7/AppData/Local/hermes/profiles/project-c/skills/unity-mcp-orchestrator/scripts/mcp_unity_client.py",
       "tool", "execute_code", json.dumps({"action":"execute","code":CODE})]
r = subprocess.run(cmd, capture_output=True, text=True, timeout=60)
print("STDOUT:", r.stdout[:800])
print("STDERR:", r.stderr[:300])
