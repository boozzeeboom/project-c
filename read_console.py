import json, subprocess
r = subprocess.run(["python", "C:/Users/leon7/AppData/Local/hermes/profiles/project-c/skills/unity-mcp-orchestrator/scripts/mcp_unity_client.py", "tool", "read_console", json.dumps({"action":"get","types":["error"],"count":20,"format":"detailed"})],capture_output=True,text=True,timeout=60)
import json as j
d = j.loads(r.stdout)
items = d.get("data", [])
print("errors:", len(items))
for item in items:
    print(item.get("type","?") + ": " + item.get("message","")[:400])
