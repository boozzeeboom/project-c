import json, subprocess
r = subprocess.run(['python','C:/Users/leon7/AppData/Local/hermes/profiles/project-c/skills/unity-mcp-orchestrator/scripts/mcp_unity_client.py','tool','read_console',json.dumps({'action':'get','types':['log','warning'],'count':500,'format':'json'})],capture_output=True,text=True,timeout=90)
d = json.loads(r.stdout)
data = d.get('data') or []
print(f"Total: {len(data)}")
for x in data:
    msg = x.get('message','')
    if any(kw in msg for kw in ['collect_copper_ore','200','GiveCredits','Reward','Stage advanced','turned','completed','Turned','Result','Toast','format']):
        print(msg[:300])
