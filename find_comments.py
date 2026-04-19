f=open('Assets/_Project/Scripts/Player/NetworkPlayer.cs','r',encoding='utf-8')  
c=f.read()  
f.close()  
idx=0  
i=1  
while True:  
    idx=c.find('// Find PlayerChunkTracker', idx)  
    print(i, idx)  
    if idx==0: break  
    idx=idx+1  
    i=i+1  
