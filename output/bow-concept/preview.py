import pickle,numpy as np
from PIL import Image, ImageDraw
roots=pickle.load(open('output/bow-concept/parsed.pkl','rb'))
def walk(ns):
 for n in ns:
  yield n
  yield from walk(n[2])
g=next(n for n in walk(roots) if n[0]=='Geometry' and n[1][-1]=='Mesh')
d={n[0]:n[1] for n in walk(g[2])}
v=d['Vertices'][0].reshape(-1,3); idx=d['PolygonVertexIndex'][0];uv=d['UV'][0].reshape(-1,2);ui=d['UVIndex'][0]
print('BOUNDS',v.min(0),v.max(0))
tex=np.array(Image.open('Assets/Art/FBX/Weapons/Textures/weapons_bits_texture.png').convert('RGB'))/255
faces=[];colors=[];face=[];uis=[]
for k,i in enumerate(idx):
 face.append(-i-1 if i<0 else i);uis.append(ui[k])
 if i<0:
  faces.append(v[face]); coord=uv[uis].mean(0);colors.append(tex[int((1-coord[1])*(tex.shape[0]-1)),int(coord[0]*(tex.shape[1]-1))]);face=[];uis=[]
canvas=Image.new('RGB',(1400,1500),'#e9e5df');draw=ImageDraw.Draw(canvas)
ext=np.ptp(v,axis=0);vert=np.argmax(ext);dep=np.argmin(ext);hor=next(i for i in range(3) if i not in [vert,dep])
for j,angle in enumerate([0,0.45]):
 order=np.argsort([f[:,dep].mean() for f in faces]); scale=1250/ext[vert]
 for i in order:
  f=faces[i];x=f[:,hor]*np.cos(angle)+f[:,dep]*np.sin(angle);y=f[:,vert]
  center=(v[:,hor].min()+v[:,hor].max())/2*np.cos(angle)+(v[:,dep].min()+v[:,dep].max())/2*np.sin(angle)
  points=list(zip((x-center)*scale+350+j*700,750-(y-(v[:,vert].min()+v[:,vert].max())/2)*scale))
  normal=np.cross(f[1]-f[0],f[2]-f[0]);normal=normal/(np.linalg.norm(normal)+1e-10);shade=.75+.25*abs(normal@np.array([.3,.5,.8]));color=tuple((np.clip(colors[i]*shade,0,1)*255).astype(int))
  draw.polygon(points,fill=color)
canvas.save('output/bow-concept/original-reference.png')
