"""Ninja frog sculpted on a cubic grid; Humanoid T pose and two scarf chains.
Run Blender --background --python this_file -- --staging to review before import.
"""
import bpy, math, json, sys, zipfile
from pathlib import Path
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[5]
OUT=ROOT/'output/ninja-frog'
SOURCE=ROOT/'Assets/Art/Source/Characters/NinjaFrog'
FBX=ROOT/'Assets/Art/FBX/Characters/NinjaFrog.fbx'
if '--staging' in sys.argv:
 OUT=OUT/'staging';SOURCE=OUT/'source';FBX=OUT/'NinjaFrog.fbx'
for p in (OUT,SOURCE,FBX.parent):p.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
scene=bpy.context.scene;S=.025
PALETTE={
 'Skin':('#839449',.84,0),'SkinLight':('#9cab60',.84,0),'SkinDark':('#536a36',.9,0),
 'Belly':('#a7b6a2',.9,0),'BellyShade':('#839784',.9,0),
 'Iris':('#c89b4a',.78,0),'IrisLight':('#dfb764',.78,0),'Pupil':('#14201b',.78,0),'Glint':('#eee9d9',.75,0),
 'Jacket':('#aaa999',.94,0),'JacketLight':('#c2bfad',.94,0),'JacketDark':('#798071',.97,0),
 'Pants':('#434c38',.97,0),'PantsLight':('#596146',.97,0),'PantsDark':('#333d2f',.97,0),
 'Orange':('#b96838',.94,0),'OrangeLight':('#d3844e',.93,0),'OrangeDark':('#8b492d',.96,0),
 'Sash':('#8d565b',.95,0),'SashLight':('#a96a6a',.95,0),'SashDark':('#683f47',.97,0),
 'Cord':('#6c5340',.9,0),'CordLight':('#9b8063',.9,0)}
def linear(c):return c/12.92 if c<=.04045 else ((c+.055)/1.055)**2.4
MATS={};colors={}
for key,(hx,rough,metal) in PALETTE.items():
 rgb=tuple(int(hx[i:i+2],16)/255 for i in (1,3,5))
 for variant in range(3):
  factor=(.95,1,1.055)[variant] if key not in ('Pupil','Glint','Iris','IrisLight') else 1
  color=tuple(min(1,c*factor) for c in rgb);name=f'Frog_{key}_{variant}'
  m=bpy.data.materials.new(name);m.diffuse_color=tuple(linear(c) for c in color)+(1,);m.use_nodes=True
  n=m.node_tree.nodes.get('Principled BSDF');n.inputs['Base Color'].default_value=m.diffuse_color
  n.inputs['Roughness'].default_value=rough;n.inputs['Metallic'].default_value=metal
  MATS[key,variant]=m;colors[name]={'color':color,'roughness':rough,'metallic':metal}
# Same body and bone rolls as the mage: skins share humanoid clips and hand mounts.
parents={'Root':None,'Hips':'Root','Spine':'Hips','Chest':'Spine','Neck':'Chest','Head':'Neck'}
positions={'Root':((0,0,0),(0,0,.15)),'Hips':((0,0,.78),(0,0,.96)),
 'Spine':((0,0,.96),(0,0,1.15)),'Chest':((0,0,1.15),(0,0,1.34)),
 'Neck':((0,0,1.34),(0,0,1.46)),'Head':((0,0,1.46),(0,0,1.78))}
for side,sgn in [('L',1),('R',-1)]:
 def p(x,y,z):return(sgn*x,y,z)
 for n,par in [('Clavicle','Chest'),('UpperArm','Clavicle.'+side),('LowerArm','UpperArm.'+side),('Hand','LowerArm.'+side),('UpperLeg','Hips'),('LowerLeg','UpperLeg.'+side),('Foot','LowerLeg.'+side),('Toes','Foot.'+side)]:parents[n+'.'+side]=par
 positions.update({'Clavicle.'+side:(p(.05,0,1.31),p(.29,0,1.30)),
 'UpperArm.'+side:(p(.29,0,1.30),p(.56,-.012,1.30)),
 'LowerArm.'+side:(p(.56,-.012,1.30),p(.82,0,1.30)),
 'Hand.'+side:(p(.82,0,1.30),p(.98,0,1.30)),
 'UpperLeg.'+side:(p(.145,0,.79),p(.18,-.012,.44)),
 'LowerLeg.'+side:(p(.18,-.012,.44),p(.20,0,.17)),
 'Foot.'+side:(p(.20,0,.17),p(.20,-.15,.085)),
 'Toes.'+side:(p(.20,-.15,.085),p(.20,-.24,.085))})
data=bpy.data.armatures.new('NinjaFrogRig');arm=bpy.data.objects.new('Armature_Humanoid',data)
scene.collection.objects.link(arm);bpy.context.view_layer.objects.active=arm;arm.select_set(True)
bpy.ops.object.mode_set(mode='EDIT')
for name,parent in parents.items():
 e=data.edit_bones.new(name);e.head,e.tail=positions[name]
 if parent:e.parent=data.edit_bones[parent]
 axis=(0,-1,0) if '.' not in name else (0,0,1)
 if name.startswith(('UpperLeg','LowerLeg')):axis=(0,1,0)
 if name.startswith('Foot'):axis=(0,.5145,-.8575)
 e.align_roll(Vector(axis))

scarf_paths={
 'L':[(-.11,.17,1.41),(-.14,.29,1.39),(-.18,.36,1.22),(-.21,.40,1.04),(-.27,.43,.88)],
 'R':[(.045,.17,1.41),(.09,.30,1.38),(.15,.365,1.22),(.20,.42,1.03),(.24,.455,.92)]}
for side,pts in scarf_paths.items():
 for i in range(4):
  e=data.edit_bones.new(f'Scarf.{side}_{i+1}');e.head=pts[i];e.tail=pts[i+1]
  e.parent=data.edit_bones['Chest' if i==0 else f'Scarf.{side}_{i}'];e.align_roll(Vector((0,1,0)))
 e=data.edit_bones.new(f'Scarf.{side}_tip');e.head=pts[-1];e.tail=Vector(pts[-1])+Vector((0,0,-.025));e.parent=data.edit_bones[f'Scarf.{side}_4']
bpy.ops.object.mode_set(mode='OBJECT');arm.show_in_front=True
PARTS=[];VOXEL_PARTS=[];WORLD_CELLS={};DETAIL_CELLS={};COUNT=0
def variant(x,y,z):
 h=(x*73856093 ^ y*19349663 ^ z*83492791)&255
 return 0 if h<22 else (2 if h>235 else 1)
def ellipse(x,y,rx,ry):return (x/rx)**2+(y/ry)**2
def ellipsoid(x,y,z,rx,ry,rz):return ellipse(x,y,rx,ry)+(z/rz)**2
def mix(a,b,t):
 t=max(0,min(1,t));t=t*t*(3-2*t)
 return {a:1} if t==0 else ({b:1} if t==1 else {a:1-t,b:t})
def scarf_segment(side,p):
 pts=scarf_paths[side];best=(100,0,0,Vector(pts[0]))
 for i in range(4):
  a=Vector(pts[i]);delta=Vector(pts[i+1])-a;t=max(0,min(1,(p-a).dot(delta)/delta.length_squared));q=a+delta*t
  distance=(p-q).length
  if distance<best[0]:best=(distance,i,t,q)
 return best
def scarf_weights(side,x,y,z):
 _,i,t,_=scarf_segment(side,Vector((x,y,z)))
 return mix('Chest' if i==0 else f'Scarf.{side}_{i}',f'Scarf.{side}_{i+1}',t)
def torso_weights(z):
 return mix('Hips','Spine',(z-.93)/.15) if z<1.1 else mix('Spine','Chest',(z-1.1)/.16)
def vertex_skin(part,p,w):
 x,y,z=p
 if part.startswith('Frog_ScarfTail_'):return scarf_weights(part[-1],x,y,z)
 if part in ('Frog_Tunic','Frog_Jacket','Frog_Lapels','Frog_BackPatch'):return torso_weights(z)
 if part=='Frog_Neck':return mix('Chest','Neck',(z-1.30)/.07) if z<1.37 else mix('Neck','Head',(z-1.37)/.10)
 if part.startswith(('Frog_Arm_','Frog_Sleeve_')):return mix('UpperArm.'+part[-1],'LowerArm.'+part[-1],(abs(x)-.50)/.13)
 if part.startswith('Frog_Pants_'):
  side=part[-1]
  return mix('LowerLeg.'+side,'UpperLeg.'+side,(z-.36)/.19) if z<.75 else mix('UpperLeg.'+side,'Hips',(z-.75)/.15)
 if part.startswith('Frog_Shin_'):return mix('Foot.'+part[-1],'LowerLeg.'+part[-1],(z-.16)/.11)
 if part.startswith('Frog_JacketTail_'):return mix('Hips','UpperLeg.'+part[-1],max(0,min(.55,(.92-z)*1.8)))
 if part.startswith('Frog_SashEnd_'):return mix('Hips','UpperLeg.'+part[-1],max(0,min(.55,(.90-z)*1.8)))
 return w
def mottled(x,y,z):
 h=(int(x/.07)*31 ^ int(y/.07)*73 ^ int(z/.075)*13)&63
 return 'SkinDark' if h<2 else ('SkinLight' if z>1.72 and h>50 else 'Skin')
class Vox:
 def __init__(self,name,bone,step=S):
  self.name=name;self.bone=bone;self.cells={};self.step=step;self.world=WORLD_CELLS if step==S else DETAIL_CELLS
 def shape(self,bounds,predicate,mat,weights=None):
  S=self.step
  x0,x1,y0,y1,z0,z1=bounds
  for iz in range(math.floor(z0/S),math.ceil(z1/S)+1):
   z=(iz+.5)*S
   for iy in range(math.floor(y0/S),math.ceil(y1/S)+1):
    y=(iy+.5)*S
    for ix in range(math.floor(x0/S),math.ceil(x1/S)+1):
     x=(ix+.5)*S
     if predicate(x,y,z):self.cells[ix,iy,iz]=(mat(x,y,z) if callable(mat) else mat,weights(x,y,z) if weights else {self.bone:1})
 def box(self,c,d,mat):
  x,y,z=c;a,b,h=[v/2 for v in d]
  self.shape((x-a,x+a,y-b,y+b,z-h,z+h),lambda X,Y,Z:abs(X-x)<=a and abs(Y-y)<=b and abs(Z-z)<=h,mat)
 def finish(self):
  VOXEL_PARTS.append(self)
  for k,v in self.cells.items():self.world[k]=(self.name,v)
  return self
 def build(self):
  global COUNT
  S=self.step;WORLD_CELLS=self.world
  verts=[];faces=[];mi=[];weights=[];keys=list(MATS);index={key:i for i,key in enumerate(keys)}
  normals=[(-1,0,0),(1,0,0),(0,-1,0),(0,1,0),(0,0,-1),(0,0,1)]
  quads=[[(0,0,0),(0,0,1),(0,1,1),(0,1,0)],[(1,0,0),(1,1,0),(1,1,1),(1,0,1)],[(0,0,0),(1,0,0),(1,0,1),(0,0,1)],[(0,1,0),(0,1,1),(1,1,1),(1,1,0)],[(0,0,0),(0,1,0),(1,1,0),(1,0,0)],[(0,0,1),(1,0,1),(1,1,1),(0,1,1)]]
  for (x,y,z),(mat,w) in self.cells.items():
   if WORLD_CELLS[x,y,z][0]!=self.name:continue
   for n,q in zip(normals,quads):
    entry=WORLD_CELLS.get((x+n[0],y+n[1],z+n[2]))
    if entry and (entry[1][1]==w or entry[0]==self.name and True):continue
    off=len(verts);quad=[((x+a)*S,(y+b)*S,(z+c)*S) for a,b,c in q];verts.extend(quad)
    faces.append(tuple(range(off,off+4)));weights.extend([vertex_skin(self.name,p,w) for p in quad]);mi.append(index[mat,1 if self.step<.02 else variant(x,y,z)])
  if not verts:return
  mesh=bpy.data.meshes.new(self.name);mesh.from_pydata(verts,[],faces);mesh.update()
  obj=bpy.data.objects.new(self.name,mesh);scene.collection.objects.link(obj)
  for k in keys:mesh.materials.append(MATS[k])
  for p,m in zip(mesh.polygons,mi):p.material_index=m
  groups={n:obj.vertex_groups.new(name=n) for n in {k for w in weights for k in w}}
  grouped={}
  for i,w in enumerate(weights):
   for name,value in w.items():grouped.setdefault((name,round(value,5)),[]).append(i)
  for (name,value),ids in grouped.items():groups[name].add(ids,value,'REPLACE')
  obj.modifiers.new('Humanoid skin','ARMATURE').object=arm;obj.parent=arm;PARTS.append(obj);COUNT+=len(self.cells)

# Continuous undershirt with shared corner weights across the waist and chest.
v=Vox('Frog_Tunic','Spine')
v.shape((-.28,.28,-.19,.19,.82,1.37),lambda x,y,z:.83<z<1.365 and ellipse(x,y,.218+.033*math.sin((z-.83)*5),.15)<1,lambda x,y,z:'PantsLight' if y<-.10 and abs(x)<.14 else 'Pants');v.finish()
for side,sgn in [('L',1),('R',-1)]:
 # Green articulated arms remain connected under the short, open sleeves.
 v=Vox('Frog_Arm_'+side,'UpperArm.'+side)
 v.shape((min(sgn*.26,sgn*.87),max(sgn*.26,sgn*.87),-.12,.12,1.19,1.42),lambda x,y,z:.26<abs(x)<.865 and ellipse(y,z-1.30,.073+.012*math.sin(abs(x)*15),.08)<1,mottled);v.finish()
 v=Vox('Frog_Shin_'+side,'LowerLeg.'+side)
 v.shape((sgn*.20-.085,sgn*.20+.085,-.08,.085,.135,.46),lambda x,y,z:.14<z<.445 and ellipse(x-sgn*(.2-(z-.17)*.03),y,.046+.022*max(0,(z-.17)/.26),.053)<1,mottled);v.finish()
 # Broad forefoot with three separate, connected amphibian toes. No boots.
 v=Vox('Frog_Foot_'+side,'Foot.'+side)
 v.shape((sgn*.20-.13,sgn*.20+.13,-.205,.07,.018,.20),lambda x,y,z:(.025<z<.10 and ellipse(x-sgn*.20,y+.07,.101,.136)<1) or (.075<z<.19 and ellipse(x-sgn*.20,y,.052,.059)<1),mottled)
 for index in [-1,0,1]:
  cx=sgn*.20+index*.084;end=-.345 if index==0 else -.305
  v.shape((cx-.05,cx+.05,end-.04,-.10,.02,.085),lambda x,y,z,cx=cx,end=end:.025<z<.074 and end<y<-.10 and abs(x-cx)<(.034 if y<end+.06 else .027),'SkinLight' if index==0 else 'Skin')
 v.finish()
 # Bulky trouser folds taper into small wraps below the knees.
 v=Vox('Frog_Pants_'+side,'UpperLeg.'+side)
 def pants(x,y,z,sgn=sgn):
  if not .295<z<.924:return False
  t=(z-.295)/.629;cx=sgn*(.205-.057*t);r=.102+.060*math.sin(math.pi*t*.88)
  a=math.atan2(y,x-cx);fold=.006*math.cos(a*6+z)
  return ellipse(x-cx,y,r+fold,.125+.054*math.sin(math.pi*t)+fold)<1
 v.shape((sgn*.19-.19,sgn*.19+.19,-.205,.205,.28,.94),pants,lambda x,y,z:'PantsLight' if math.cos(math.atan2(y,x-sgn*.18)*6+z)>.55 else ('PantsDark' if y>.11 else 'Pants'));v.finish()
 v=Vox('Frog_LegWrap_'+side,'LowerLeg.'+side)
 for row in range(3):
  z=.265+row*.028
  v.shape((sgn*.20-.09,sgn*.20+.09,-.09,.09,z-.02,z+.03),lambda x,y,Z,z=z:.0<Z-z+.018<.044 and ellipse(x-sgn*.194,y,.064+row*.005,.067+row*.004)<1,'JacketLight' if row%2==0 else 'JacketDark')
 v.finish()
 # Amphibian palm, three long fingers, and a shorter opposed thumb.
 v=Vox('Frog_Hand_'+side,'Hand.'+side)
 v.shape((min(sgn*.815,sgn*.956),max(sgn*.815,sgn*.956),-.099,.095,1.242,1.367),lambda x,y,z:.815<abs(x)<.953 and ellipse(y,z-1.301,.083,.05)<1,'Skin')
 for i in [-1,0,1]:
  length=.127 if i==0 else .103;cx=sgn*(.92+length/2)
  v.box((cx,i*.056,1.294),(length,.039,.053),'SkinLight')
  v.box((sgn*(.92+length),i*.062,1.294),(.043,.051,.051),'Skin')
 v.box((sgn*.88,-.105,1.281),(.058,.094,.061),'Skin');v.finish()
 v=Vox('Frog_WristWrap_'+side,'LowerArm.'+side)
 for row in range(2):v.box((sgn*(.754+row*.031),0,1.30),(.024,.158,.158),'CordLight' if row==0 else 'JacketDark')
 v.finish()

# Short weathered jacket, open in front with a dark V under the scarf.
v=Vox('Frog_Jacket','Spine')
def jacket(x,y,z):
 if not .905<z<1.38:return False
 rx=.247+.035*math.sin((z-.90)*5);ry=.184
 if y<-.08 and abs(x)<.047+max(0,z-1.06)*.22:return False
 return .68<ellipse(x,y,rx,ry)<1
v.shape((-.30,.30,-.205,.205,.9,1.39),jacket,lambda x,y,z:'JacketLight' if y<-.12 else ('JacketDark' if y>.12 else 'Jacket'));v.finish()
v=Vox('Frog_Lapels','Spine')
for sign in [-1,1]:
 for row in range(12):
  z=.95+row*.033;x=sign*(.06+max(0,z-1.06)*.22)
  v.box((x,-.174,z),(.054,.047,.043),'JacketLight')
v.finish()
for side,sgn in [('L',1),('R',-1)]:
 v=Vox('Frog_Sleeve_'+side,'UpperArm.'+side)
 def sleeve(x,y,z):
  t=(abs(x)-.27)/.385
  if not 0<t<1:return False
  ry=.115+.035*t;rz=.112+.075*t
  if t>.88 and z<1.28 and int((y+.18)/.053)%3==1:return False
  return .52<ellipse(y,z-1.30,ry,rz)<1.07
 v.shape((min(sgn*.26,sgn*.67),max(sgn*.26,sgn*.67),-.17,.17,1.07,1.52),sleeve,lambda x,y,z:'JacketLight' if y<-.06 or z>1.40 else ('JacketDark' if z<1.22 else 'Jacket'));v.finish()
 v=Vox('Frog_ShoulderCord_'+side,'UpperArm.'+side)
 for j in range(11):
  a=math.pi*j/10;y=.122*math.cos(a);z=1.30+.121*math.sin(a)
  v.box((sgn*.365,y,z),(.029,.037,.034),'Cord')
 v.box((sgn*.365,-.115,1.355),(.071,.034,.047),'CordLight')
 for row in range(3):v.box((sgn*(.325+row*.038),-.139,1.39),(.023,.031,.051),'CordLight')
 v.finish()
 v=Vox('Frog_JacketTail_'+side,'Hips')
 def jacket_tail(x,y,z,sgn=sgn):
  if not .695<z<.93 or x*sgn<.066:return False
  t=(.93-z)/.24;rx=.263+.025*t;ry=.177+.025*t
  if z<.748 and int((abs(x)+y)/.05)%3==0:return False
  return .67<ellipse(x,y,rx,ry)<1.1
 v.shape((min(sgn*.055,sgn*.325),max(sgn*.055,sgn*.325),-.215,.215,.685,.94),jacket_tail,lambda x,y,z:'JacketLight' if y<0 and x*sgn<.17 else 'Jacket');v.finish()

# Waist cloth has real thickness; hanging ends are skinned to the nearest thigh.
v=Vox('Frog_Sash','Hips')
for row in range(3):
 z=.883+row*.026
 v.shape((-.29,.29,-.222,.222,z-.017,z+.026),lambda x,y,Z,z=z:abs(Z-z)<.025 and .72<ellipse(x,y,.273,.206)<1.07,'SashLight' if row==2 else ('SashDark' if row==0 else 'Sash'))
v.box((-.07,-.23,.91),(.133,.083,.10),'SashLight');v.box((-.087,-.274,.91),(.049,.023,.069),'Sash')
v.finish()
for side,sgn in [('L',1),('R',-1)]:
 v=Vox('Frog_SashEnd_'+side,'Hips')
 for row in range(11 if sgn==1 else 8):
  z=.89-row*.028;x=-.055+sgn*(.025+row*.006)
  v.box((x,-.239-row*.001,z),(.071+(row%3)*.008,.032,.035),'SashLight' if row%4==1 else 'Sash')
 v.finish()
v=Vox('Frog_BackPatch','Chest')
v.box((-.13,.19,1.157),(.096,.025,.119),'JacketDark')
for z in [1.12,1.19]:v.box((-.13,.207,z),(.10,.022,.016),'CordLight')
v.finish()

# Flexible neck is continuous with the head and covered by a fitted scarf.
v=Vox('Frog_Neck','Neck')
v.shape((-.15,.15,-.125,.125,1.29,1.59),lambda x,y,z:1.295<z<1.585 and ellipse(x,y,.115+.018*max(0,(z-1.4)/.18),.105)<1,'Skin');v.finish()
v=Vox('Frog_Head','Head')
def head_shape(x,y,z):
 return ellipsoid(x,y+.045,z-1.674,.332,.260,.164)<1 or (1.60<z<1.71 and abs(x)<.277 and -.310<y<-.19 and ellipse(x,z-1.656,.306,.083)<1)
def head_color(x,y,z):
 if abs(abs(x)-.0875)<.017 and abs(z-1.6875)<.017 and y<-.29:return 'SkinDark'
 if 1.58<z<1.61 and y<-.13:return 'SkinDark'
 return 'SkinLight' if z>1.74 and y<-.04 else ('SkinDark' if y>.06 and abs(x)>.23 else 'Skin')
v.shape((-.34,.34,-.345,.225,1.49,1.875),head_shape,head_color);v.finish()
v=Vox('Frog_Jaw','Head')
v.shape((-.315,.315,-.338,.19,1.465,1.61),lambda x,y,z:1.475<z<1.59 and ellipsoid(x,y+.067,z-1.575,.302,.246,.104)<1,lambda x,y,z:'Belly' if y<-.08 else 'BellyShade');v.finish()
for side,sgn in [('L',1),('R',-1)]:
 v=Vox('Frog_EyeTower_'+side,'Head')
 v.shape((sgn*.23-.111,sgn*.23+.111,-.225,.095,1.72,1.985),lambda x,y,z:ellipsoid(x-sgn*.23,y+.061,z-1.826,.098,.128,.118)<1,mottled);v.finish()
 v=Vox('Frog_Iris_'+side,'Head')
 def eye_patch(x,y,z,sgn=sgn):
  dx=x-sgn*.2375;dz=z-1.8375
  return abs(dx)<.068 and abs(dz)<.084 and -.217<y<-.174 and not (abs(dx)>.048 and abs(dz)>.061)
 def eye_color(x,y,z,sgn=sgn):
  dx=x-sgn*.2375;dz=z-1.8375
  if abs(dx)<.041 and abs(dz)<.058:
   return 'Glint' if -.041<dx<-.009 and .023<dz<.058 else 'Pupil'
  return 'IrisLight' if dz>.055 else 'Iris'
 v.shape((sgn*.2375-.08,sgn*.2375+.08,-.225,-.17,1.75,1.93),eye_patch,eye_color);v.finish()

# A wrapped orange scarf attaches to the chest; two tails emerge from its back knot.
v=Vox('Frog_ScarfCollar','Chest')
for row in range(3):
 v.shape((-.26,.26,-.23,.22,1.335,1.49),lambda x,y,z,row=row:1.345+row*.037+.02*abs(x)/.24<z<1.393+row*.037+.023*abs(x)/.24 and .34<ellipse(x,y,.245-row*.024,.213-row*.019)<1.07,lambda x,y,z:'OrangeLight' if z>1.43 else ('OrangeDark' if z<1.38 else 'Orange'))
v.box((-.04,.187,1.425),(.18,.10,.125),'Orange');v.box((-.04,.243,1.434),(.087,.035,.081),'OrangeLight');v.finish()
for side in ['L','R']:
 v=Vox('Frog_ScarfTail_'+side,'Chest')
 def scarf(x,y,z,side=side):
  _,i,t,q=scarf_segment(side,Vector((x,y,z)))
  width=.055 if side=='L' else .047
  # Ribbon cross-section: wide across X, narrow through depth.
  return abs(x-q.x)<width and abs(y-q.y)<.022 and abs(z-q.z)<.038
 v.shape((-.39,.35,.145,.49,.85,1.47),scarf,lambda x,y,z:'OrangeLight' if int(x/S)%3==0 else 'Orange',lambda x,y,z,side=side:scarf_weights(side,x,y,z));v.finish()

for part in VOXEL_PARTS:part.build()
for obj in PARTS:
 used=sorted({p.material_index for p in obj.data.polygons});old=list(obj.data.materials);remap={x:i for i,x in enumerate(used)};indices=[remap[p.material_index] for p in obj.data.polygons]
 obj.data.materials.clear()
 for i in used:obj.data.materials.append(old[i])
 for p,i in zip(obj.data.polygons,indices):p.material_index=i
bpy.ops.object.select_all(action='DESELECT');arm.select_set(True)
for obj in PARTS:obj.select_set(True)
bpy.context.view_layer.objects.active=arm
bpy.ops.export_scene.fbx(filepath=str(FBX),use_selection=True,object_types={'ARMATURE','MESH'},add_leaf_bones=False,bake_anim=False,axis_forward='-Z',axis_up='Y',apply_scale_options='FBX_SCALE_UNITS',path_mode='AUTO')
report={'voxel_size':S,'mesh_count':len(PARTS),'occupied_voxels':COUNT,'vertices':sum(len(o.data.vertices) for o in PARTS),'triangles':sum(len(o.data.polygons)*2 for o in PARTS),'bones':list(data.bones.keys()),'scarf_paths_blender':scarf_paths,'colors':colors}
(OUT/'model-manifest.json').write_text(json.dumps(report,indent=2))
studio=bpy.data.collections.new('STUDIO - not exported');scene.collection.children.link(studio)
world=bpy.data.worlds.new('Ninja frog studio');scene.world=world;world.use_nodes=True;world.node_tree.nodes['Background'].inputs[0].default_value=(.16,.21,.30,1);world.node_tree.nodes['Background'].inputs[1].default_value=.4
def light(name,loc,power,size,color):
 d=bpy.data.lights.new(name,'AREA');d.energy=power;d.shape='DISK';d.size=size;d.color=color
 o=bpy.data.objects.new(name,d);studio.objects.link(o);o.location=loc;o.rotation_euler=(Vector((0,0,1.1))-o.location).to_track_quat('-Z','Y').to_euler()
light('Key',(-3,-4,6),480,4,(1,.89,.76));light('Fill',(4,-2,3),320,3,(.68,.79,1));light('Rim',(0,4,4),560,3,(.72,.81,1))
mesh=bpy.data.meshes.new('Studio floor');mesh.from_pydata([(-200,-200,0),(200,-200,0),(200,200,0),(-200,200,0)],[],[(0,1,2,3)])
floor=bpy.data.objects.new('Floor',mesh);studio.objects.link(floor);floor.location.z=S
mat=bpy.data.materials.new('Backdrop');mat.use_nodes=True;mat.node_tree.nodes.get('Principled BSDF').inputs['Base Color'].default_value=(.036,.059,.09,1);mat.node_tree.nodes.get('Principled BSDF').inputs['Roughness'].default_value=1;floor.data.materials.append(mat)
cam=bpy.data.objects.new('Review',bpy.data.cameras.new('Review'));studio.objects.link(cam);scene.camera=cam;cam.data.type='ORTHO';cam.data.ortho_scale=2.30
scene.render.engine='CYCLES';scene.cycles.samples=24;scene.cycles.use_denoising=True;scene.render.resolution_x=900;scene.render.resolution_y=1050;scene.render.resolution_percentage=100;scene.render.image_settings.file_format='PNG';scene.view_settings.view_transform='AgX';scene.render.fps=60
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'NinjaFrog.blend'))
with zipfile.ZipFile(SOURCE/'NinjaFrog_Source.zip','w',zipfile.ZIP_DEFLATED) as z:z.write(OUT/'NinjaFrog.blend','NinjaFrog.blend');z.write(__file__,'build_ninja_frog.py')
print('NINJA_FROG_EXPORT_OK',report['vertices'],report['triangles'],flush=True)
def render(name,loc):
 cam.location=loc;cam.rotation_euler=(Vector((0,0,1.015))-cam.location).to_track_quat('-Z','Y').to_euler();scene.render.filepath=str(OUT/(name+'.png'));bpy.ops.render.render(write_still=True)
if '--no-render' not in sys.argv:
 cam.data.ortho_scale=2.65;render('T_Pose',(0,-7,1.8));cam.data.ortho_scale=2.30
 for side,sgn in [('L',1),('R',-1)]:
  b=arm.pose.bones['UpperArm.'+side];b.rotation_mode='XYZ';b.rotation_euler.x=math.radians(-67)
  b=arm.pose.bones['LowerArm.'+side];b.rotation_mode='XYZ';b.rotation_euler.z=math.radians(-10*sgn)
 bpy.context.view_layer.update()
 render('Hero',(3,-7,2.6));render('Side',(7,-.1,1.8));render('Back',(-3,7,2.3))
