import json, subprocess
r = subprocess.run(['python','C:/Users/leon7/AppData/Local/hermes/profiles/project-c/skills/unity-mcp-orchestrator/scripts/mcp_unity_client.py','tool','read_console',json.dumps({'action':'get','types':['log','warning','error'],'count':200,'format':'json'})],capture_output=True,text=True,timeout=90)
d = json.loads(r.stdout)
data = d.get('data', [])
print(f"Total: {len(data)}")
# quest-related lines
for x in data:
    msg = x.get('message','')
    if any(kw in msg for kw in ['QuestWorld','QuestServer','QuestClient','Objective','Talk','talk','tick','3 sec','Acc','onEnter','nNpcAttitude','quest','Quest']):
        print(msg[:250])
        if 'locked' in msg.lower() or 'rate' in msg.lower(): print("!!RATE LIMIT!!")
print("--- ERRORS ---")
for x in data:
    msg = x.get('message','')
    t = x.get('type','')
    if t in ('error','warning') and 'WebSocket' not in msg and 'LockBox' not in msg and 'toolbar' not in msg:
        print(f"[{t}]", msg[:300])
