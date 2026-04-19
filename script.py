import io
from io import StringIO
with open("Assets_Project_Scripts_Player_NetworkPlayer.cs", "r", "utf-8") as f:
    c=f.read()
with close()
s="ApplyWalkingState();"+chr(10)+"            }"+chr(10)+"            else"
n="ApplyWalkingState();"+chr(10)+chr(10)+"                // Find PlayerChunkTracker for chunk streaming"+chr(10)+"            var chunkTrackers = FindObjectsByType<PlayerChunkTracker>();"+chr(10)+"            if (chunkTrackers.Length > 0)"+chr(10)+"            {"+chr(10)+"                _PlayerChunkTracker = chunkTrackers[0];"+chr(10)+"            }"+chr(10)+"            }"+chr(10)+"            else"
c=c.replace(s,n,1)
with open("Assets_Project_Scripts_Player_NetworkPlayer.cs", "w", "utf-8") as f:
    f.write(c)
with close()
print("done")
