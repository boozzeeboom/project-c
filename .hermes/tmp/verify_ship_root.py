"""T-CARGO-01: найти Ship_Light_root в WorldScene_0_0 и валидировать Resolve()."""
import json, subprocess

CODE = (
    "var scenePath = \"Assets/_Project/Scenes/World/WorldScene_0_0.unity\"; "
    "var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath, UnityEditor.SceneManagement.OpenSceneMode.Additive); "
    "var root = UnityEngine.GameObject.Find(\"Ship_Light_root\"); "
    "if (root == null) { return \"Ship_Light_root NOT_FOUND in additive open of \" + scenePath; } "
    "var sc = root.GetComponent(System.Type.GetType(\"ProjectC.Player.ShipController, Assembly-CSharp\")) as UnityEngine.MonoBehaviour; "
    "if (sc == null) { return \"ShipController NOT on Ship_Light_root\"; } "
    "var cargoComp = root.GetComponent(System.Type.GetType(\"ProjectC.Player.CargoSystem, Assembly-CSharp\")) as UnityEngine.MonoBehaviour; "
    "var fcType = System.Type.GetType(\"ProjectC.Player.ShipFlightClass, Assembly-CSharp\"); "
    "var fcField = sc.GetType().GetField(\"shipFlightClass\", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance); "
    "object fc = fcField.GetValue(sc); "
    "var cfg = ProjectC.Ship.ShipClassMappingConfig.Default; "
    "var cc = cfg.Resolve((ProjectC.Player.ShipFlightClass)fc); "
    "var sb = new System.Text.StringBuilder(); "
    "sb.Append(\"name=\" + root.name + \" \"); "
    "sb.Append(\"flight=\" + fc + \" \"); "
    "sb.Append(\"cargoResolved=\" + (cc.HasValue ? cc.Value.ToString() : \"null\") + \" \"); "
    "sb.Append(\"hasCargoSystem=\" + (cargoComp != null) + \" \"); "
    "if (cargoComp != null) { "
    "var csType = cargoComp.GetType(); "
    "var shipClassField = csType.GetField(\"shipClass\"); "
    "sb.Append(\"legacyCargoSystem.shipClass=\" + shipClassField.GetValue(cargoComp) + \" \"); "
    "} "
    "return sb.ToString();"
)

cmd = ["python",
       "C:/Users/leon7/AppData/Local/hermes/profiles/project-c/skills/unity-mcp-orchestrator/scripts/mcp_unity_client.py",
       "tool", "execute_code", json.dumps({"action":"execute","code":CODE})]
r = subprocess.run(cmd, capture_output=True, text=True, timeout=60)
print("STDOUT:", r.stdout[:1000])
print("STDERR:", r.stderr[:300])
