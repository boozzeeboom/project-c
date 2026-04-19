f=open('Assets/_Project/Scripts/Player/NetworkPlayer.cs','r',encoding='utf-8')  
c=f.read()  
f.close()  
lines = c.split('\\n')  
print('Total lines:', len(lines))  
for i, l in enumerate(lines):  
    if 'chunkTrackers[0]' in l: print(i+1, l)  
