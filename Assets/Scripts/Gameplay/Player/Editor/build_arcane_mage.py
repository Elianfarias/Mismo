"""Reproducible, genuinely cubic mage. Run with Blender --background --python.

Rest pose is T; the 22 existing Humanoid bone names/rolls are retained. Shared
voxel corners use matching skin weights so the surface stays closed in motion.
Only visible voxel shells are emitted. Eyes are flush patches in the face.
"""
import bpy, math, json, os, sys
from pathlib import Path
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[5]
OUT = ROOT / 'output/arcane-mage'
SOURCE = ROOT / 'Assets/Art/Source/Characters/Mago'
FBX = ROOT / 'Assets/Art/FBX/Characters/ArcaneMage.fbx'
for p in (OUT, SOURCE, FBX.parent): p.mkdir(parents=True, exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
scene=bpy.context.scene
S=.028
PALETTE={
 'Indigo':('#303b63',.84,0), 'IndigoLight':('#3e4b74',.84,0),
 'IndigoDark':('#222d4a',.86,0), 'Mantle':('#586b85',.88,0),
 'MantleLight':('#697c92',.88,0), 'MantleDark':('#43566f',.88,0),
 'Lining':('#673e50',.92,0), 'Gold':('#b88c48',.4,.52),
 'GoldLight':('#d0a967',.4,.5), 'Leather':('#503a2e',.85,0),
 'LeatherLight':('#6e5038',.8,0), 'Sole':('#292728',.94,0),
 'Glove':('#353442',.87,0), 'Pages':('#ae9973',.95,0),
 'Face':('#080c18',1,0), 'Eyes':('#eadbff',.8,0),
}
def linear(c): return c/12.92 if c<=.04045 else ((c+.055)/1.055)**2.4
MATS={}; manifest_colors={}
for key,(hx,rough,metal) in PALETTE.items():
 rgb=tuple(int(hx[i:i+2],16)/255 for i in (1,3,5))
 for variant in range(3):
  factor=(.94,1,1.065)[variant] if key not in ('Face','Eyes') else 1
  color=tuple(min(1,c*factor) for c in rgb)
  name=f'Mage_{key}_{variant}'
  m=bpy.data.materials.new(name);m.diffuse_color=tuple(linear(c) for c in color)+(1,);m.use_nodes=True
  n=m.node_tree.nodes.get('Principled BSDF');n.inputs['Base Color'].default_value=m.diffuse_color
  n.inputs['Roughness'].default_value=rough;n.inputs['Metallic'].default_value=metal
  if key=='Eyes':
   n.inputs['Emission Color'].default_value=m.diffuse_color;n.inputs['Emission Strength'].default_value=.65
  MATS[(key,variant)]=m
  manifest_colors[name]={'color':list(color),'roughness':rough,'metallic':metal,'emission':key=='Eyes'}

# Preserve roll convention used by Mismo's hand mounts and retargeting.
# Self-contained: the generator does not depend on Unity's disposable Temp folder.
bone_parents={'Root':None,'Hips':'Root','Spine':'Hips','Chest':'Spine','Neck':'Chest','Head':'Neck'}
for side in ['L','R']:
 for name,parent in [('Clavicle','Chest'),('UpperArm','Clavicle.'+side),('LowerArm','UpperArm.'+side),('Hand','LowerArm.'+side),('UpperLeg','Hips'),('LowerLeg','UpperLeg.'+side),('Foot','LowerLeg.'+side),('Toes','Foot.'+side)]:
  bone_parents[name+'.'+side]=parent
data=bpy.data.armatures.new('ArcaneMageRig');arm=bpy.data.objects.new('Armature_Humanoid',data)
scene.collection.objects.link(arm);bpy.context.view_layer.objects.active=arm;arm.select_set(True)
bpy.ops.object.mode_set(mode='EDIT')
positions={
 'Root':((0,0,0),(0,0,.15)), 'Hips':((0,0,.78),(0,0,.96)),
 'Spine':((0,0,.96),(0,0,1.15)), 'Chest':((0,0,1.15),(0,0,1.34)),
 'Neck':((0,0,1.34),(0,0,1.46)), 'Head':((0,0,1.46),(0,0,1.78)),
}
for side,sign in [('L',1),('R',-1)]:
 def pt(x,y,z):return(sign*x,y,z)
 positions.update({
  'Clavicle.'+side:(pt(.05,0,1.31),pt(.29,0,1.30)),
  'UpperArm.'+side:(pt(.29,0,1.30),pt(.56,-.012,1.30)),
  'LowerArm.'+side:(pt(.56,-.012,1.30),pt(.82,0,1.30)),
  'Hand.'+side:(pt(.82,0,1.30),pt(.98,0,1.30)),
  'UpperLeg.'+side:(pt(.145,0,.79),pt(.18,-.012,.44)),
  'LowerLeg.'+side:(pt(.18,-.012,.44),pt(.20,0,.17)),
  'Foot.'+side:(pt(.20,0,.17),pt(.20,-.15,.085)),
  'Toes.'+side:(pt(.20,-.15,.085),pt(.20,-.24,.085)),
 })
for name,parent in bone_parents.items():
 e=data.edit_bones.new(name);e.head,e.tail=positions[name]
 if parent:e.parent=data.edit_bones[parent]
 axis=(0,-1,0) if '.' not in name else (0,0,1)
 if name.startswith(('UpperLeg','LowerLeg')):axis=(0,1,0)
 if name.startswith('Foot'):axis=(0,.5145,-.8575)
 e.align_roll(Vector(axis))
hatpoints=[(0,.018,1.98),(.03,.018,2.18),(.13,.018,2.36),(.28,.018,2.42),(.40,.018,2.31)]
for i in range(4):
 b=data.edit_bones.new('Hat_'+str(i+1));b.head=hatpoints[i];b.tail=hatpoints[i+1]
 b.parent=data.edit_bones['Head' if i==0 else 'Hat_'+str(i)]
bpy.ops.object.mode_set(mode='OBJECT')
arm.show_in_front=True

def variant(x,y,z):
 h=(x*73856093 ^ y*19349663 ^ z*83492791)&255
 return 0 if h<35 else (2 if h>225 else 1)

PARTS=[];COUNT=0;VOXEL_PARTS=[];WORLD_CELLS={}
def smooth_mix(a,b,t):
 t=max(0,min(1,t));t=t*t*(3-2*t)
 return {a:1} if t<=0 else ({b:1} if t>=1 else {a:1-t,b:t})
def vertex_skin(part,coord,fallback):
 x,y,z=coord
 if part.startswith('Mage_SplitRobe'):
  side='L' if x>=0 else 'R';t=max(0,min(.52,(.91-z)*1.2))
  return {'Hips':1-t,'UpperLeg.'+side:t}
 if part.startswith('Mage_Sleeve_'):
  side=part[-1];return smooth_mix('UpperArm.'+side,'LowerArm.'+side,(abs(x)-.50)/.12)
 if part.startswith('Mage_Trousers_'):
  side=part[-1];return smooth_mix('LowerLeg.'+side,'UpperLeg.'+side,(z-.37)/.14)
 if part=='Mage_Tunic':
  return smooth_mix('Hips','Spine',(z-.93)/.13) if z<1.09 else smooth_mix('Spine','Chest',(z-1.09)/.15)
 if part=='Mage_HatCrown':return hw(x,y,z)
 return fallback
class Vox:
 def __init__(self,name,bone):self.name=name;self.bone=bone;self.cells={}
 def shape(self,bounds,predicate,mat,weights=None):
  x0,x1,y0,y1,z0,z1=bounds
  for iz in range(math.floor(z0/S),math.ceil(z1/S)+1):
   z=(iz+.5)*S
   for iy in range(math.floor(y0/S),math.ceil(y1/S)+1):
    y=(iy+.5)*S
    for ix in range(math.floor(x0/S),math.ceil(x1/S)+1):
     x=(ix+.5)*S
     if predicate(x,y,z):
      color=mat(x,y,z) if callable(mat) else mat
      w=weights(x,y,z) if weights else {self.bone:1}
      self.cells[(ix,iy,iz)]=(color,w)
 def box(self,c,d,mat):
  x,y,z=c; a,b,h=[v/2 for v in d]
  self.shape((x-a,x+a,y-b,y+b,z-h,z+h),lambda X,Y,Z:abs(X-x)<=a and abs(Y-y)<=b and abs(Z-z)<=h,mat)
 def finish(self):
  VOXEL_PARTS.append(self)
  for key,value in self.cells.items():WORLD_CELLS[key]=(self.name,value)
  return self
 def build(self):
  global COUNT
  verts=[];faces=[];mi=[];weights=[];keys=list(MATS);index={key:i for i,key in enumerate(keys)}
  normals=[(-1,0,0),(1,0,0),(0,-1,0),(0,1,0),(0,0,-1),(0,0,1)]
  quads=[[(0,0,0),(0,0,1),(0,1,1),(0,1,0)],[(1,0,0),(1,1,0),(1,1,1),(1,0,1)],[(0,0,0),(1,0,0),(1,0,1),(0,0,1)],[(0,1,0),(0,1,1),(1,1,1),(1,1,0)],[(0,0,0),(0,1,0),(1,1,0),(1,0,0)],[(0,0,1),(1,0,1),(1,1,1),(0,1,1)]]
  for (x,y,z),(mat,w) in self.cells.items():
   if WORLD_CELLS[(x,y,z)][0]!=self.name:continue
   # Cull only neighbors of the same rigid weight, retaining articulation caps.
   for n,q in zip(normals,quads):
    entry=WORLD_CELLS.get((x+n[0],y+n[1],z+n[2]))
    neighbor=entry[1] if entry else None
    if neighbor and neighbor[1]==w:continue
    off=len(verts);quad=[((x+a)*S,(y+b)*S,(z+c)*S) for a,b,c in q];verts.extend(quad)
    faces.append(tuple(range(off,off+4)));weights.extend([vertex_skin(self.name,p,w) for p in quad]);mi.append(index[(mat,variant(x,y,z))])
  if not verts:return
  mesh=bpy.data.meshes.new(self.name);mesh.from_pydata(verts,[],faces);mesh.update()
  obj=bpy.data.objects.new(self.name,mesh);scene.collection.objects.link(obj)
  for key in keys:mesh.materials.append(MATS[key])
  for p,m in zip(mesh.polygons,mi):p.material_index=m
  groups={n:obj.vertex_groups.new(name=n) for n in {k for w in weights for k in w}}
  grouped={}
  for i,w in enumerate(weights):
   for name,value in w.items():grouped.setdefault((name,round(value,4)),[]).append(i)
  for (name,value),ids in grouped.items():groups[name].add(ids,value,'REPLACE')
  mod=obj.modifiers.new('Humanoid skin','ARMATURE');mod.object=arm;obj.parent=arm
  PARTS.append(obj);COUNT+=len(self.cells)
  return obj

def ellipse(x,y,rx,ry):return (x/rx)**2+(y/ry)**2

# Tailored torso, slightly scalloped vertical cloth folds.
v=Vox('Mage_Tunic','Spine')
def torso(x,y,z):
 t=(z-.85)/.50;rx=.215+.07*math.sin(t*math.pi*.78);ry=.155+.025*math.sin(t*math.pi)
 a=math.atan2(y/ry,x/rx);fold=.011*math.cos(a*10+z*2)
 return .85<=z<=1.35 and ellipse(x,y,rx+fold,ry+fold)<=1
v.shape((-.31,.31,-.21,.21,.85,1.35),torso,lambda x,y,z:'IndigoLight' if y<-.13 and abs(x)<.13 else 'Indigo',lambda x,y,z:{'Hips':1} if z<.98 else ({'Spine':1} if z<1.16 else {'Chest':1}));v.finish()

# Trouser legs are visibly separate beneath an open, thick robe.
for side,sign in [('L',1),('R',-1)]:
 v=Vox('Mage_Trousers_'+side,'UpperLeg.'+side)
 v.shape((sign*.19-.14,sign*.19+.14,-.15,.15,.24,.85),lambda x,y,z:.25<z<.83 and ellipse(x-sign*(.20-(z-.25)*.07),y,.10+.018*math.sin((z-.25)*5),.12)<=1,lambda x,y,z:'IndigoDark' if z<.45 else 'Indigo',lambda x,y,z:{('LowerLeg.' if z<.44 else 'UpperLeg.')+side:1});v.finish()
 v=Vox('Mage_Boot_'+side,'Foot.'+side)
 v.shape((sign*.20-.13,sign*.20+.13,-.27,.14,0,.30),lambda x,y,z:(.028<=z<=.112 and abs(x-sign*.20)<.114 and -.252<y<.112) or (.112<z<=.20 and ellipse(x-sign*.20,y+.08,.107,.168)<=1) or (.20<z<.30 and ellipse(x-sign*.20,y,.092,.105)<=1),lambda x,y,z:'Sole' if z<.07 else 'Leather');v.finish()
 v=Vox('Mage_BootCuff_'+side,'Foot.'+side)
 v.shape((sign*.20-.14,sign*.20+.14,-.13,.13,.24,.34),lambda x,y,z:.252<z<.322 and .54<ellipse(x-sign*.20,y,.119,.126)<1.12,'LeatherLight')
 for x in [-.035,.035]:v.box((sign*.20+x,-.131,.286),(.028,.03,.084),'Gold')
 for z in [.25,.322]:v.box((sign*.20,-.131,z),(.09,.03,.028),'Gold')
 v.finish()

# A continuous cut cloth shell, not two huge solid thigh cylinders.
v=Vox('Mage_SplitRobe','Hips')
def robe_params(z):
 t=max(0,min(1,(.90-z)/.48));return t,.23+.105*t,.158+.065*t
def robe(x,y,z):
 if not .42<=z<=.92:return False
 t,rx,ry=robe_params(z);a=math.atan2(y/ry,x/rx)
 dr=.012*math.cos(a*12)+.005*math.sin(a*5+z*10)
 outer=ellipse(x,y,rx+dr,ry+dr)
 inner=ellipse(x,y,max(.01,rx+dr-.052),max(.01,ry+dr-.05))
 gap=.018+.078*t
 if abs(x)<gap and abs(y)>.07:return False
 return outer<=1 and inner>=1
def robe_color(x,y,z):
 t,rx,ry=robe_params(z);gap=.018+.078*t
 if z<.462 or (abs(y)>.12 and abs(x)<gap+.033):return 'Gold'
 if ellipse(x,y,rx,ry)<.78:return 'Lining'
 return 'IndigoLight' if y<0 and abs(x)<.20 else 'Indigo'
def robe_weight(x,y,z):
 side='L' if x>0 else 'R';w=max(0,min(.70,(.92-z)*1.7))
 # Discrete bands keep skinning bounded and permit aggressive hidden face culling.
 w=round(w*8)/8
 return {'Hips':1-w,'UpperLeg.'+side:w} if w else {'Hips':1}
v.shape((-.39,.39,-.27,.27,.41,.93),robe,robe_color,robe_weight);v.finish()

# Belt wraps around a real waist; buckle is open, with an inset tongue.
v=Vox('Mage_Belt','Hips')
v.shape((-.28,.28,-.20,.20,.86,.96),lambda x,y,z:.868<z<.952 and .79<ellipse(x,y,.26,.188)<1.18,'Leather')
for x in [-.18,.18]:v.box((x,-.165,.91),(.035,.06,.105),'LeatherLight')
for x in [-.065,.065]:v.box((x,-.215,.91),(.028,.032,.112),'Gold')
for z in [.86,.96]:v.box((0,-.215,z),(.155,.032,.028),'Gold')
v.box((0,-.235,.91),(.025,.028,.07),'GoldLight');v.finish()

# Arms are horizontal in the exported bind pose. Overlapping sleeve volumes
# provide complete elbow coverage when the existing clips bend the arms.
for side,sign in [('L',1),('R',-1)]:
 v=Vox('Mage_Sleeve_'+side,'UpperArm.'+side)
 def sleeve(x,y,z):
  t=abs(x);radius=.113-.022*(t-.3)/.52+.006*math.cos(t*32)
  return .29<t<.825 and ellipse(y,z-1.30,radius,radius)<=1
 v.shape((min(sign*.29,sign*.83),max(sign*.29,sign*.83),-.14,.14,1.15,1.46),sleeve,lambda x,y,z:'IndigoLight' if z>1.33 else 'Indigo',lambda x,y,z:{('UpperArm.' if abs(x)<.56 else 'LowerArm.')+side:1});v.finish()
 v=Vox('Mage_Cuff_'+side,'LowerArm.'+side)
 v.shape((min(sign*.69,sign*.82),max(sign*.69,sign*.82),-.14,.14,1.16,1.44),lambda x,y,z:.69<abs(x)<.82 and .48<ellipse(y,z-1.30,.126,.126)<1.08,lambda x,y,z:'Gold' if abs(x)<.72 or abs(x)>.79 else 'MantleDark');v.finish()
 v=Vox('Mage_Glove_'+side,'Hand.'+side)
 v.box((sign*.895,-.007,1.30),(.158,.112,.15),'Glove')
 # Compact finger silhouette and bent thumb, still editable through the hand.
 for i in range(3):v.box((sign*(.972+(i==1)*.018),-.012+(i-1)*.04,1.296),(.075,.032,.10),'Glove')
 v.box((sign*.876,-.086,1.274),(.073,.048,.065),'Leather');v.finish()

# Mantle over shoulders: three volumetric overlapping rings with a front notch.
v=Vox('Mage_Mantle','Chest')
for layer in range(3):
 rx=.355+layer*.014;ry=.20+layer*.026
 def mantle(x,y,z,layer=layer,rx=rx,ry=ry):
  r=ellipse(x,y,rx,ry);a=math.atan2(y/ry,x/rx)
  top=1.43-layer*.062-.06*r+.012*math.cos(a*7)
  # Split at sternum to show chest emblem and tunic.
  return .25<r<1.25 and top-.060<z<top and not(y<-.05 and abs(x)<.07+layer*.02)
 v.shape((-.43,.43,-.30,.30,1.15,1.48),mantle,('MantleLight','Mantle','MantleDark')[layer])
v.finish()
v=Vox('Mage_MantleLining','Chest')
v.shape((-.42,.42,-.28,.28,1.13,1.23),lambda x,y,z: .70<ellipse(x,y,.36,.25)<1.12 and 1.145<z<1.195 and not(y<0 and abs(x)<.14),'Lining');v.finish()

# Hood shell, with an inset face whose light eye squares are flush surfaces.
v=Vox('Mage_Hood','Head')
v.shape((-.255,.255,-.22,.22,1.40,1.84),lambda x,y,z:1.40<z<1.84 and ellipse(x,y,.24,.207)<1.03 and not(y<-.12 and abs(x)<.173 and 1.505<z<1.766),lambda x,y,z:'IndigoDark' if y>0 else 'Indigo');v.finish()
v=Vox('Mage_Face','Head');v.box((0,-.160,1.637),(.336,.056,.252),'Face');face=v.finish()
# Eye planes lie 0.0007m above actual face front (no floating eye geometry).
eye_front=min(key[1] for key in face.cells)*S-.0007
verts=[];faces=[]
for cx in [-.077,.077]:
 start=len(verts);w=.024;z=1.652;h=.033
 verts += [(cx-w,eye_front,z-h),(cx+w,eye_front,z-h),(cx+w,eye_front,z+h),(cx-w,eye_front,z+h)]
 faces.append(tuple(range(start,start+4)))
m=bpy.data.meshes.new('Flush eye patches');m.from_pydata(verts,[],faces);m.materials.append(MATS['Eyes',1])
o=bpy.data.objects.new('Mage_Eyes_Flush',m);scene.collection.objects.link(o);o.parent=arm
o.vertex_groups.new(name='Head').add(list(range(8)),1,'REPLACE');o.modifiers.new('Head skin','ARMATURE').object=arm;PARTS.append(o)

# Scarf wraps the lower face and extends at one side in sculpted steps.
v=Vox('Mage_Scarf','Head')
for row in range(3):
 v.shape((-.28,.28,-.26,.23,1.38,1.55),lambda x,y,z,row=row:1.386+row*.045<z<1.435+row*.045 and .65<ellipse(x,y+.012,.252-row*.01,.222)<1.12,lambda x,y,z:'MantleLight' if z>1.48 else 'Mantle')
v.finish()
v=Vox('Mage_ScarfTail','Chest')
for row in range(5):v.box((.135+row*.009,-.226,1.40-row*.037),(.07,.056,.056),'MantleDark' if row%2 else 'Mantle')
v.finish()

# Swept, bending crown and a thick brim made from the same voxel grid.
v=Vox('Mage_Hat','Head')
def brim(x,y,z):
 a=math.atan2(y/.40,x/.56);r=ellipse(x,y,.56,.40)
 top=1.85-.044*r+.012*math.sin(a*3)+.008*math.cos(a*5)
 return r<1+.04*math.sin(a*5) and top-.061<z<top
v.shape((-.60,.60,-.44,.44,1.70,1.90),brim,lambda x,y,z:'IndigoLight' if z>1.82 else 'Indigo');v.finish()
v=Vox('Mage_HatCrown','Head')
path=[((0,.014,1.84),.254),((0,.014,1.98),.220),((.023,.014,2.13),.169),((.078,.014,2.28),.128),((.17,.014,2.41),.092),((.28,.014,2.44),.067),((.36,.014,2.39),.047),((.415,.014,2.31),.026)]
def crown_data(x,y,z):
 p=Vector((x,y,z));best=999;at=0
 for i in range(len(path)-1):
  a=Vector(path[i][0]);b=Vector(path[i+1][0]);d=b-a;t=max(0,min(1,(p-a).dot(d)/d.length_squared))
  rad=path[i][1]*(1-t)+path[i+1][1]*t
  delta=p-(a+d*t);delta.y/= .83
  # Fluting removes planar bands from the silhouette.
  rad+=.006*math.cos(math.atan2(delta.y,delta.x)*7+z*6)
  ratio=delta.length/max(.018,rad)
  if ratio<best:best=ratio;at=i+t
 return best,at
def hw(x,y,z):
 _,at=crown_data(x,y,z)
 if at<1.05:return {'Head':1}
 f=min(4,max(0,(at-1)*.68));a=int(f);b=min(4,a+1);t=round((f-a)*4)/4
 names=['Head','Hat_1','Hat_2','Hat_3','Hat_4']
 return {names[a]:1} if a==b or t==0 else ({names[b]:1} if t==1 else {names[a]:1-t,names[b]:t})
v.shape((-.29,.47,-.24,.25,1.81,2.53),lambda x,y,z:z>1.82 and crown_data(x,y,z)[0]<=1,lambda x,y,z:'IndigoLight' if x<-.02 and y<0 else 'Indigo',hw);v.finish()
v=Vox('Mage_HatBand','Head')
v.shape((-.29,.29,-.24,.24,1.88,1.98),lambda x,y,z:1.875<z<1.960 and .84<ellipse(x,y-.014,.257-(z-1.88)*.3,.218-(z-1.88)*.24)<1.12,'LeatherLight');v.finish()

def triangle(name,c,size,bone):
 v=Vox(name,bone);x,y,z=c
 for i in range(7):
  t=i/6
  for sign in [-1,1]:v.box((x+sign*size*.5*t,y,z-size*t),(.025,.031,.03),'GoldLight')
 for i in range(9):v.box((x-size*.5+size*i/8,y,z-size),(.025,.031,.03),'Gold')
 v.finish()
triangle('Mage_HatSigil',(0,-.247,2.064),.151,'Head')
triangle('Mage_ChestSigil',(0,-.225,1.29),.097,'Chest')

# Diagonal leather strap follows the chest surface, with geometric stitches.
v=Vox('Mage_SatchelStrap','Chest')
for i in range(14):
 t=i/13;x=.195-.40*t;z=1.31-.37*t
 v.box((x,-.205,z),(.045,.032,.046),'Leather')
 if i%3==0:v.box((x,-.229,z),(.015,.014,.018),'Gold')
v.finish()
v=Vox('Mage_Spellbook','Hips')
# Hero's right hip (viewer left); spine and covers are visibly thick.
v.box((-.31,-.064,.76),(.12,.228,.29),'Pages')
for x in [-.386,-.245]:v.box((x,-.064,.76),(.034,.262,.326),'Leather')
v.box((-.31,.058,.76),(.158,.042,.326),'LeatherLight')
for z in [.615,.9]:
 for y in [-.17,.05]:v.box((-.401,y,z),(.03,.07,.047),'Gold')
v.box((-.410,-.070,.76),(.026,.05,.10),'Gold')
v.finish()
v=Vox('Mage_BeltPouch','Hips')
v.box((.285,.014,.82),(.13,.20,.17),'Leather')
v.box((.29,-.105,.86),(.145,.032,.085),'LeatherLight')
v.box((.285,-.13,.82),(.03,.025,.06),'Gold');v.finish()

# Resolve voxel ownership globally to remove intersecting/coplanar clothing
# surfaces. Internal articulation caps survive when neighbors have other weights.
for part in VOXEL_PARTS:part.build()
# Purge unused material slots per mesh (and retain named color data for Unity).
for obj in PARTS:
 used=sorted({p.material_index for p in obj.data.polygons});old=list(obj.data.materials)
 remap={x:i for i,x in enumerate(used)};indices=[remap[p.material_index] for p in obj.data.polygons]
 obj.data.materials.clear()
 for i in used:obj.data.materials.append(old[i])
 for p,i in zip(obj.data.polygons,indices):p.material_index=i

# Export production rig at rest. No animations or sword are embedded.
bpy.ops.object.select_all(action='DESELECT');arm.select_set(True)
for o in PARTS:o.select_set(True)
bpy.context.view_layer.objects.active=arm
scene.render.fps=60
bpy.ops.export_scene.fbx(filepath=str(FBX),use_selection=True,object_types={'ARMATURE','MESH'},add_leaf_bones=False,bake_anim=False,axis_forward='-Z',axis_up='Y',apply_scale_options='FBX_SCALE_UNITS',use_custom_props=False,path_mode='AUTO')

# Keep source in T pose. Studio is separate, non-exported collection.
world=bpy.data.worlds.new('Mage studio');scene.world=world;world.use_nodes=True
world.node_tree.nodes['Background'].inputs[0].default_value=(.16,.21,.30,1)
world.node_tree.nodes['Background'].inputs[1].default_value=.4
studio=bpy.data.collections.new('STUDIO - not exported');scene.collection.children.link(studio)
def light(name,loc,power,size,color):
 d=bpy.data.lights.new(name,'AREA');d.energy=power;d.shape='DISK';d.size=size;d.color=color
 o=bpy.data.objects.new(name,d);studio.objects.link(o);o.location=loc;o.rotation_euler=(Vector((0,0,1.2))-o.location).to_track_quat('-Z','Y').to_euler()
light('Key',(-3,-4,6),480,4,(1,.89,.76));light('Fill',(4,-2,3),320,3,(.68,.79,1));light('Rim',(0,4,4),560,3,(.72,.81,1))
floor_mesh=bpy.data.meshes.new('Floor');floor_mesh.from_pydata([(-200,-200,0),(200,-200,0),(200,200,0),(-200,200,0)],[],[(0,1,2,3)])
floor=bpy.data.objects.new('Studio floor',floor_mesh);studio.objects.link(floor)
mat=bpy.data.materials.new('Studio backdrop');mat.diffuse_color=(.036,.059,.09,1);mat.use_nodes=True;mat.node_tree.nodes.get('Principled BSDF').inputs['Base Color'].default_value=mat.diffuse_color;mat.node_tree.nodes.get('Principled BSDF').inputs['Roughness'].default_value=1;floor.data.materials.append(mat)
cam_data=bpy.data.cameras.new('Review');cam=bpy.data.objects.new('Review',cam_data);studio.objects.link(cam);scene.camera=cam;cam.data.type='ORTHO';cam.data.ortho_scale=2.95
scene.render.engine='CYCLES';scene.cycles.samples=24;scene.cycles.use_denoising=True
scene.render.resolution_x=900;scene.render.resolution_y=1050;scene.render.resolution_percentage=100
scene.view_settings.view_transform='AgX'
scene.render.image_settings.file_format='PNG'
# Blender source is packaged to avoid Unity invoking a second Blender importer.
# The editable copy lives next to the exports; the archive belongs in Art/Source.
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'ArcaneMage.blend'))
import zipfile
with zipfile.ZipFile(SOURCE/'ArcaneMage_Source.zip','w',zipfile.ZIP_DEFLATED) as archive:
 archive.write(OUT/'ArcaneMage.blend','ArcaneMage.blend')
 archive.write(__file__,'build_arcane_mage.py')
def aim(loc,target=(0,0,1.24)):
 cam.location=loc;cam.rotation_euler=(Vector(target)-cam.location).to_track_quat('-Z','Y').to_euler()
def render(name,loc):
 aim(loc);scene.render.filepath=str(OUT/(name+'.png'));bpy.ops.render.render(write_still=True)

if '--no-render' not in sys.argv:
 render('T_Pose',(0,-7,2.4))
 for side,sign in [('L',1),('R',-1)]:
  b=arm.pose.bones['UpperArm.'+side];b.rotation_mode='XYZ';b.rotation_euler.x=math.radians(-67)
  arm.pose.bones['LowerArm.'+side].rotation_mode='XYZ';arm.pose.bones['LowerArm.'+side].rotation_euler.z=math.radians(-10*sign)
 bpy.context.view_layer.update()
 render('Hero',(3,-7,3.0));render('Side',(7,-.1,2.35));render('Back',(-3,7,2.9))

report={'voxel_size':S,'occupied_voxels':COUNT,'mesh_count':len(PARTS),'vertices':sum(len(o.data.vertices) for o in PARTS),'triangles':sum(len(o.data.polygons)*2 for o in PARTS),'bones':list(data.bones.keys()),'eye_surface_gap':.0007,'colors':manifest_colors}
(OUT/'model-manifest.json').write_text(json.dumps(report,indent=2))
print('ARCANE_MAGE_EXPORT_OK',COUNT,report['vertices'],report['triangles'],flush=True)
