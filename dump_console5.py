import json, subprocess
r = subprocess.run(['python','C:/Users/leon7/AppData/Local/hermes/profiles/project-c/skills/unity-mcp-orchestrator/scripts/mcp_unity_client.py','tool','read_console',json.dumps({'action':'get','types':['log','warning','error'],'count':400,'format':'json'})],capture_output=True,text=True,timeout=90)
d = json.loads(r.stdout)
data = d.get('data') or []
print(f"Total: {len(data)}")
# collect_copper_ore or any AddReputation or GuildOfThoughts
for x in data:
    msg = x.get('message','')
    if any(kw in msg for kw in ['collect_copper_ore','AddReputation','GuildOfThoughts','rewards','+25','credits 11','credits 12']):
        print(msg[:300])
