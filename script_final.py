f=open('Assets/_Project/Scripts/Player/NetworkPlayer.cs','r',encoding='utf-8')  
c=f.read()  
f.close()  
search='ApplyWalkingState();' + chr(10) + '            }' + chr(10) + '            else'  
print('Search in content:', search in c)  
