f=open('Assets/_Project/Scripts/Player/NetworkPlayer.cs','r',encoding='utf-8')  
c=f.read()  
f.close()  
lines=c.split(chr(10))  
result=[]  
changes=0  
for i,l in enumerate(lines):  
    result.append(l)  
    if l.strip()=='private NetworkObject networkObject;' and changes==0:  
        result.append(''')  
        result.append('        // Chunk tracking')  
        result.append('        private PlayerChunkTracker _playerChunkTracker;')  
        result.append(''')  
        changes=1  
    if l.strip()=='ApplyWalkingState(');' and changes==1:  
        result.append(''')  
        result.append('                // Find PlayerChunkTracker for chunk streaming')  
        result.append('                var chunkTrackers = FindObjectsByType<PlayerChunkTracker>(\);')  
        result.append('                if (chunkTrackers.Length > 0)')  
        result.append('                {')  
        result.append('                    _playerChunkTracker = chunkTrackers[0];')  
        result.append('                }')  
        result.append(''')  
        changes=2  
    if l.strip()=='if (\!IsOwner) return;' and changes==2 and i>260:  
        result.append(''')  
        result.append('            // Update PlayerChunkTracker for server-side chunk streaming')  
        result.append('            if (_playerChunkTracker != null)')  
        result.append('            {')  
        result.append('                _playerChunkTracker.ForceUpdatePlayerChunk(OwnerClientId, transform.position);')  
        result.append('            }')  
        result.append(''')  
        changes=3  
open('Assets/_Project/Scripts/Player/NetworkPlayer.cs','w',encoding='utf-8').write(chr(10).join(result))  
print('Changes:', changes)  
