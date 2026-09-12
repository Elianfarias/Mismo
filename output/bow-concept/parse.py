import struct,zlib,json
import numpy as np
from pathlib import Path
p=Path(r'C:/Users/elian/Mismo/Assets/Art/FBX/Weapons/Bow/bow_B_withString.fbx')
f=p.open('rb'); f.read(23); ver=struct.unpack('<I',f.read(4))[0]
def node():
 end,n,sz=struct.unpack('<III',f.read(12)); ln=f.read(1)[0]
 if not end:return None
 name=f.read(ln).decode(); props=[]
 for _ in range(n):
  t=f.read(1).decode()
  if t in 'Y C I F D L'.split():
   fmt={'Y':'h','C':'?','I':'i','F':'f','D':'d','L':'q'}[t];props.append(struct.unpack('<'+fmt,f.read(struct.calcsize(fmt)))[0])
  elif t in 'fdlibc':
   count,enc,size=struct.unpack('<III',f.read(12));raw=f.read(size);raw=zlib.decompress(raw) if enc else raw
   props.append(np.frombuffer(raw,dtype={'f':'<f4','d':'<f8','l':'<i8','i':'<i4','b':'u1','c':'u1'}[t]))
  elif t in 'SR':
   size=struct.unpack('<I',f.read(4))[0];raw=f.read(size);props.append(raw.decode(errors='replace') if t=='S' else raw)
 children=[]
 while f.tell()<end:
  c=node()
  if c:children.append(c)
 return (name,props,children)
roots=[]
while True:
 a=node()
 if not a:break
 roots.append(a)
def walk(nodes):
 for n in nodes:
  yield n
  yield from walk(n[2])
for name,p,c in walk(roots):
 if name in ['Geometry','Model','Material','RelativeFilename','P']:print(name,str(p)[:200])
import pickle
pickle.dump(roots,open(r'C:/Users/elian/Mismo/output/bow-concept/parsed.pkl','wb'))
