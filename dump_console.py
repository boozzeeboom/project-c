import json, subprocess
r = subprocess.run(['python','C:/Users/leon7/AppData/Local/hermes/profiles/project-c/skills/unity-mcp-orchestrator/scripts/mcp_unity_client.py','tool','read_console',json.dumps({'action':'get','types':['log','warning','error'],'count':150,'format':'json'})],capture_output=True,text=True,timeout=90)
d = json.loads(r.stdout)
data = d.get('data', [])
print(f"Total: {len(data)}")
# Filter for QuestTracker and quest-related messages
relevant = []
for x in data:
    msg = x.get('message','')
    if any(kw in msg for kw in ['QuestTracker','QuestWorld','BuildQuestSnapshot','quest','Quest','Track:']):
        relevant.append(msg)
print("--- QUEST/TRACKER messages ---")
for m in relevant[-60:]:
    print(m[:300])
print("--- ERRORS/WARNINGS ---")
for x in data:
    msg = x.get('message','')
    t = x.get('type','')
    if t in ('error','warning') and 'WebSocket' not in msg and 'LockBox' not in msg and 'toolbar' not in msg:
        print(f"[{t}]", msg[:400])
