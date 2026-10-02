"""Voxel warrior skin, authored in T pose. Blender --background --python this_file.
All sculpture uses a shared cubic lattice; the cape has three four-bone strips.
"""
import bpy, math, json, sys, zipfile
from pathlib import Path
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[5]
OUT=ROOT/'output/ashen-warrior'
SOURCE=ROOT/'Assets/Art/Source/Characters/Warrior'
FBX=ROOT/'Assets/Art/FBX/Characters/AshenWarrior.fbx'
if '--staging' in sys.argv:
 OUT=OUT/'staging';SOURCE=OUT/'source';FBX=OUT/'AshenWarrior.fbx'
for p in (OUT,SOURCE,FBX.parent):p.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
scene=bpy.context.scene;S=.028
PALETTE={
 'Steel':('#606879',.55,.65),'SteelDark':('#3f4655',.67,.55),'Edge':('#959aa5',.47,.70),
 'Chain':('#282d35',.72,.45),'ChainLight':('#3c414b',.67,.55),
 'Cloth':('#743039',.93,0),'ClothLight':('#8d4246',.9,0),'ClothDark':('#49212c',.96,0),
 'Crimson':('#a72b35',.9,0),'CrimsonLight':('#cb4546',.88,0),'CrimsonDark':('#661e2a',.95,0),
 'Leather':('#473327',.86,0),'LeatherEdge':('#765742',.8,0),
 'Brass':('#b2905e',.5,.50),'Rust':('#72503b',.85,.2),
 'Sole':('#23252a',.95,0),'Face':('#080c18',1,0),'Eyes':('#eadbff',.8,0)}
def linear(c):return c/12.92 if c<=.04045 else ((c+.055)/1.055)**2.4
MATS={};colors={}
for key,(hx,rough,metal) in PALETTE.items():
 rgb=tuple(int(hx[i:i+2],16)/255 for i in (1,3,5))
 for variant in range(3):
  factor=(.94,1,1.065)[variant] if key not in ('Face','Eyes') else 1
  color=tuple(min(1,c*factor) for c in rgb);name=f'Warrior_{key}_{variant}'
  m=bpy.data.materials.new(name);m.diffuse_color=tuple(linear(c) for c in color)+(1,);m.use_nodes=True
  n=m.node_tree.nodes.get('Principled BSDF');n.inputs['Base Color'].default_value=m.diffuse_color
  n.inputs['Roughness'].default_value=rough;n.inputs['Metallic'].default_value=metal
  if key=='Eyes':n.inputs['Emission Color'].default_value=m.diffuse_color;n.inputs['Emission Strength'].default_value=.6
  MATS[key,variant]=m;colors[name]={'color':color,'roughness':rough,'metallic':metal,'emission':key=='Eyes'}

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
data=bpy.data.armatures.new('AshenWarriorRig');arm=bpy.data.objects.new('Armature_Humanoid',data)
scene.collection.objects.link(arm);bpy.context.view_layer.objects.active=arm;arm.select_set(True)
bpy.ops.object.mode_set(mode='EDIT')
for name,parent in parents.items():
 e=data.edit_bones.new(name);e.head,e.tail=positions[name]
 if parent:e.parent=data.edit_bones[parent]
 axis=(0,-1,0) if '.' not in name else (0,0,1)
 if name.startswith(('UpperLeg','LowerLeg')):axis=(0,1,0)
 if name.startswith('Foot'):axis=(0,.5145,-.8575)
 e.align_roll(Vector(axis))
cape_tips={}
for col,sign in [('L',1),('C',0),('R',-1)]:
 pts=[(sign*x,y,z) for x,y,z in [(.16,.25,1.40),(.19,.28,1.15),(.22,.32,.90),(.26,.36,.65),(.29,.40,.40)]]
 for i in range(4):
  name=f'Cape.{col}_{i+1}';e=data.edit_bones.new(name);e.head=pts[i];e.tail=pts[i+1]
  e.parent=data.edit_bones['Chest' if i==0 else f'Cape.{col}_{i}'];e.align_roll(Vector((0,1,0)))
 cape_tips[col]=pts[-1]
plume_points=[Vector(p) for p in [(0,.095,2.012),(0,.29,2.15),(0,.44,2.08),(0,.49,1.90),(0,.50,1.69),(0,.52,1.47)]]
for i in range(5):
 e=data.edit_bones.new(f'Plume_{i+1}');e.head=plume_points[i];e.tail=plume_points[i+1]
 e.parent=data.edit_bones['Head' if i==0 else f'Plume_{i}'];e.align_roll(Vector((1,0,0)))
bpy.ops.object.mode_set(mode='OBJECT');arm.show_in_front=True

PARTS=[];VOXEL_PARTS=[];WORLD_CELLS={};DETAIL_CELLS={};COUNT=0
def variant(x,y,z):
 h=(x*73856093 ^ y*19349663 ^ z*83492791)&255
 return 0 if h<35 else (2 if h>225 else 1)
def ellipse(x,y,rx,ry):return (x/rx)**2+(y/ry)**2
def mix(a,b,t):
 t=max(0,min(1,t));t=t*t*(3-2*t)
 return {a:1} if t==0 else ({b:1} if t==1 else {a:1-t,b:t})
def cape_weights(x,y,z):
 level=max(0,min(4,(1.36-z)/.25));row=int(level);frac=level-row
 width=.16+max(0,min(1,(1.40-z)))*.13
 t=max(-1,min(1,x/width));columns=[('C',1-abs(t)),('L' if t>=0 else 'R',abs(t))];result={}
 for col,cw in columns:
  for r,rw in [(row,1-frac),(min(4,row+1),frac)]:
   name='Chest' if r==0 else f'Cape.{col}_{r}'
   if cw*rw>.000001:result[name]=result.get(name,0)+cw*rw
 return result
def plume_nearest(x,y,z):
 p=Vector((x,y,z));best=(100,0,0)
 for i in range(5):
  a=plume_points[i];d=plume_points[i+1]-a;t=max(0,min(1,(p-a).dot(d)/d.length_squared))
  dist=(p-a-d*t).length
  if dist<best[0]:best=(dist,i,t)
 return best
def plume_weights(x,y,z):
 _,i,t=plume_nearest(x,y,z)
 return mix('Head' if i==0 else f'Plume_{i}',f'Plume_{i+1}',t)
def vertex_skin(part,p,w):
 x,y,z=p
 if part=='Warrior_Cape':return cape_weights(x,y,z)
 if part=='Warrior_Plume':return plume_weights(x,y,z)
 if part=='Warrior_Aventail':
  return mix('Chest','Neck',(z-1.28)/.075) if z<1.355 else mix('Neck','Head',(z-1.355)/.105)
 if part=='Warrior_MailTorso':return mix('Hips','Spine',(z-.92)/.15) if z<1.1 else mix('Spine','Chest',(z-1.1)/.12)
 if part.startswith('Warrior_MailArm_'):return mix('UpperArm.'+part[-1],'LowerArm.'+part[-1],(abs(x)-.50)/.12)
 if part.startswith('Warrior_MailLeg_'):return mix('LowerLeg.'+part[-1],'UpperLeg.'+part[-1],(z-.37)/.14)
 if part.startswith(('Warrior_Tasset_','Warrior_Tabard_')):
  return mix('Hips','UpperLeg.'+part[-1],min(.7,max(0,(.90-z)*1.65)))
 return w
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
    if entry and (entry[1][1]==w or entry[0]==self.name and self.name in ('Warrior_Cape','Warrior_Plume')):continue
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

def mail(x,y,z):return 'ChainLight' if (math.floor(x/S)+math.floor(y/S)+math.floor(z/S))%3==0 else 'Chain'
def steel(x,y,z):
 h=(int(x/S)*83 ^ int(y/S)*97 ^ int(z/S)*47)&127
 return 'Rust' if h<5 else ('SteelDark' if h<15 else 'Steel')

# Padded mail foundation; matching corner weights preserve elbow and knee seams.
v=Vox('Warrior_MailTorso','Spine')
v.shape((-.29,.29,-.19,.19,.79,1.39),lambda x,y,z:.79<z<1.38 and ellipse(x,y,.235+.035*math.sin((z-.8)*5),.16)<1,mail);v.finish()
for side,sgn in [('L',1),('R',-1)]:
 v=Vox('Warrior_MailArm_'+side,'UpperArm.'+side)
 v.shape((min(sgn*.29,sgn*.84),max(sgn*.29,sgn*.84),-.12,.12,1.18,1.42),lambda x,y,z:.29<abs(x)<.83 and ellipse(y,z-1.30,.09+.008*(int(abs(x)/S)%2),.099)<1,mail);v.finish()
 v=Vox('Warrior_MailLeg_'+side,'UpperLeg.'+side)
 v.shape((sgn*.18-.12,sgn*.18+.12,-.13,.13,.17,.86),lambda x,y,z:.17<z<.85 and ellipse(x-sgn*(.20-(z-.25)*.07),y,.09,.112)<1,mail);v.finish()

# Raised breastplate, separate abdominal lames, and a smaller back plate.
v=Vox('Warrior_Breastplate','Chest')
def breast(x,y,z):
 if not 1.085<z<1.345:return False
 rx=.265+.014*math.sin((z-1.08)*10);ry=.189+.023*math.sin((z-1.08)*8)
 return y<.04 and .60<ellipse(x,y,rx,ry)<1.06
v.shape((-.30,.30,-.25,.07,1.07,1.35),breast,lambda x,y,z:'Edge' if z<1.122 or abs(x)>.246 else steel(x,y,z))
for row in range(7):
 z=1.13+row*.028;v.box((0,-.217-(3-abs(row-3))*.005,z),(.042,.04,.035),'Edge' if row>4 else 'Steel')
v.finish()
for layer in range(3):
 v=Vox('Warrior_Abdomen_'+str(layer),'Spine')
 z0=.952+layer*.055
 v.shape((-.26,.26,-.22,.1,z0,z0+.087),lambda x,y,z,z0=z0:.65<ellipse(x,y,.245,.185)<1.05 and y<.045 and z0<z<z0+.081,lambda x,y,z,z0=z0:'Edge' if z<z0+.028 else steel(x,y,z));v.finish()
v=Vox('Warrior_Backplate','Chest');v.shape((-.28,.28,.05,.21,1.08,1.35),lambda x,y,z:1.1<z<1.33 and .68<ellipse(x,y,.27,.19)<1.03,steel);v.finish()

# Compact armored sabatons and shaped greaves; all sole vertices bind to Foot.
for side,sgn in [('L',1),('R',-1)]:
 v=Vox('Warrior_Boot_'+side,'Foot.'+side)
 v.shape((sgn*.20-.13,sgn*.20+.13,-.27,.13,0,.24),lambda x,y,z:(.028<z<.09 and abs(x-sgn*.20)<.11 and -.25<y<.10) or (.09<=z<.18 and ellipse(x-sgn*.20,y+.075,.104,.158)<1) or (.18<=z<.235 and ellipse(x-sgn*.20,y,.087,.087)<1),lambda x,y,z:'Sole' if z<.062 else steel(x,y,z))
 for row in range(3):
  v.box((sgn*.20,-.20+row*.065,.13+row*.016),(.196,.032,.035),'Edge')
 v.finish()
 v=Vox('Warrior_Greave_'+side,'LowerLeg.'+side)
 v.shape((sgn*.19-.14,sgn*.19+.14,-.16,.13,.22,.43),lambda x,y,z:.225<z<.421 and .45<ellipse(x-sgn*.19,y,.10+(z-.22)*.08,.116)<1.13,lambda x,y,z:'Edge' if y<-.095 and abs(x-sgn*.19)<.035 or z>.39 else steel(x,y,z));v.finish()
 v=Vox('Warrior_Knee_'+side,'LowerLeg.'+side)
 v.shape((sgn*.18-.16,sgn*.18+.16,-.22,-.065,.386,.55),lambda x,y,z:ellipse(x-sgn*.18,z-.464,.126,.087)<1.05 and -.188<y<-.083,lambda x,y,z:'Edge' if z>.51 or abs(x-sgn*.18)>.1 else steel(x,y,z));v.finish()
 v=Vox('Warrior_ThighPlate_'+side,'UpperLeg.'+side)
 v.shape((sgn*.16-.12,sgn*.16+.12,-.16,-.09,.57,.83),lambda x,y,z:.57<z<.83 and abs(x-sgn*.16)<.09+.018*math.sin((z-.57)*11) and -.154<y<-.096,lambda x,y,z:'Edge' if z<.598 else steel(x,y,z));v.finish()
 for layer in range(3):
  v=Vox(f'Warrior_Tasset_{layer}_{side}','Hips');z0=.70+layer*.065
  v.shape((sgn*.24-.12,sgn*.24+.12,-.235,.10,z0,z0+.10),lambda x,y,z,z0=z0:sgn*x>.14 and .72<ellipse(x,y,.325-(z-.7)*.19,.215)<1.1 and y<.06 and z0<z<z0+.093,lambda x,y,z,z0=z0:'Edge' if z<z0+.028 else steel(x,y,z));v.finish()
 v=Vox('Warrior_Tabard_'+side,'Hips')
 v.shape((min(sgn*.016,sgn*.12),max(sgn*.016,sgn*.12),-.23,-.14,.48,.87),lambda x,y,z:.48+.04*math.sin(x*97)<z<.87 and .018<abs(x)<.107 and abs(y-(-.193-.025*math.cos(z*16)))<.022,lambda x,y,z:'ClothLight' if abs(x)>.075 else 'Cloth');v.finish()

# Articulated shoulder plates and segmented gauntlets; avoid oversized shoulders.
for side,sgn in [('L',1),('R',-1)]:
 for layer in range(3):
  v=Vox(f'Warrior_Pauldron_{layer}_{side}','UpperArm.'+side);cx=sgn*(.315+layer*.054)
  rz=.148-layer*.024+(side=='R')*.012
  v.shape((cx-.14,cx+.14,-.16,.16,1.22,1.51),lambda x,y,z,cx=cx,rz=rz,layer=layer:1.255<z<1.30+rz and abs(x-cx)<.083 and .45<ellipse(y,z-1.30,.15-layer*.008,rz)<1.1,lambda x,y,z,cx=cx:'Edge' if abs(x-cx)>.060 or y<-.12 else steel(x,y,z));v.finish()
 v=Vox('Warrior_Elbow_'+side,'LowerArm.'+side)
 v.box((sgn*.558,0,1.408),(.14,.16,.075),'SteelDark');v.box((sgn*.558,-.087,1.36),(.13,.055,.12),'Steel');v.finish()
 for layer in range(3):
  v=Vox(f'Warrior_Vambrace_{layer}_{side}','LowerArm.'+side);cx=sgn*(.635+layer*.062)
  v.shape((cx-.065,cx+.065,-.127,.127,1.19,1.43),lambda x,y,z,cx=cx:.03<abs(x-cx)<.060 and .46<ellipse(y,z-1.30,.113,.118)<1.08,lambda x,y,z,cx=cx:'Edge' if abs(x-cx)>.04 else steel(x,y,z));v.finish()
 v=Vox('Warrior_Gauntlet_'+side,'Hand.'+side)
 v.box((sgn*.89,-.005,1.30),(.16,.13,.135),'Chain')
 for row in range(3):v.box((sgn*(.85+row*.046),-.011,1.371),(.038,.14,.055),'Steel' if row%2 else 'Edge')
 for i in range(3):v.box((sgn*(.97+(i==1)*.014),-.045+i*.036,1.291),(.084,.03,.099),'SteelDark')
 v.box((sgn*.874,-.09,1.268),(.07,.055,.07),'Steel');v.finish()

# Flexible mail closes the neck continuously, including under the helmet.
# The upper ring follows Head completely before it meets the helmet edge;
# the lower rings blend down into Neck and Chest rather than leaving a gap.
v=Vox('Warrior_Aventail','Neck')
v.shape((-.17,.17,-.16,.16,1.265,1.60),lambda x,y,z:1.27<z<1.59 and ellipse(x,y,.132+.016*max(0,min(1,(z-1.35)/.15)),.116+.020*max(0,min(1,(z-1.35)/.15)))<1,mail)
v.finish()

# Closed armet rebuilt around one continuous tall visor. Fine cubic sampling
# resolves narrow ventilation slots without turning the metal plate into a grille.
H=.014
# Keep the neck housing on the body grid: it shares the existing Head-weighted
# seam with the aventail, completely covered by the outer shell.
v=Vox('Warrior_Helmet','Head')
v.shape((-.18,.18,-.16,.16,1.47,1.59),lambda x,y,z:1.475<z<1.585 and ellipse(x,y,.175-.020*max(0,(1.60-z)/.125),.150)<1.07,'Steel');v.finish()

def crown_radius(z):
 t=max(0,(z-1.80)/.262);q=math.sqrt(max(.006,1-t*t))
 return .249*q,.228*q
v=Vox('Warrior_HelmetShell','Head',H)
def helmet_shell(x,y,z):
 if not 1.51<z<2.061:return False
 rx,ry=crown_radius(z)
 if z<1.80:
  taper=max(0,min(1,(1.61-z)/.11));rx=.244-.069*taper;ry=.214-.056*taper
 return ellipse(x,y-.018,rx,ry)<1.04 and (z>1.846 or y>-.043 or abs(x)>.218)
v.shape((-.27,.27,-.25,.265,1.50,2.07),helmet_shell,'Steel');v.finish()

# One shell wraps from the forward keel around both cheeks to the pivots.
def visor_radius(z):return .255-.050*max(0,min(1,(1.61-z)/.12))
def visor_depth(z):return .390-.070*max(0,min(1,(1.60-z)/.11))
def wrap_y(x,rx,depth):
 # Two broad planar cheeks meet at a keel; short wings return to the temples.
 a=min(1,abs(x)/rx)
 return -depth+.115*(a/.72) if a<=.72 else -depth+.115+(depth-.134)*((a-.72)/.28)
def visor_y(x,z):return wrap_y(x,visor_radius(z),visor_depth(z))
def visor_top(x):return 1.833-.043*(min(1,abs(x)/.255))**1.5
def visor_bottom(x,y):return 1.494+.065*(abs(x)/.255)**2+.105*max(0,min(1,(y+.135)/.115))**1.4
def vent(x,z):
 # Eight thin rectangular slots in each row; broad metal between and around them.
 slot=any(abs(x-c)<.0095 for c in [-.203,-.147,-.091,-.035,.035,.091,.147,.203])
 return slot and (abs(z-(1.741-.026*abs(x)/.255))<.026 or abs(z-(1.631+.023*abs(x)/.255))<.022)
v=Vox('Warrior_Faceguard','Head',H)
def faceguard(x,y,z):
 if abs(x)>=visor_radius(z) or y>-.019:return False
 if not visor_bottom(x,y)<z<visor_top(x):return False
 # A closed volume prevents cracks where steep cheek wings meet adjacent cubes.
 # Ventilation recesses retain a dark back, so no torso or scarf shows through.
 return y>visor_y(x,z) and not (vent(x,z) and y<visor_y(x,z)+.042)
def guard_color(x,y,z):
 if vent(x,z):return 'Face'
 return 'Edge' if z>visor_top(x)-.015 or z<visor_bottom(x,y)+.016 else 'Steel'
v.shape((-.265,.265,-.425,.0,1.485,1.846),faceguard,guard_color);v.finish()

# Recessed darkness follows the shell, never overwriting its outer surface.
v=Vox('Warrior_Visor','Head',H)
v.shape((-.258,.258,-.40,.025,1.78,1.884),lambda x,y,z:abs(x)<visor_radius(z)-.01 and visor_top(x)<z<visor_top(x)+.034 and .038<y-visor_y(x,min(z,visor_top(x)))<.10,'Face')
face=v.finish()

# A single swept forehead overhang forms the narrow slit above the visor.
def brow_radius(z):return .256-max(0,z-1.88)*.48
def brow_depth(z):return .404-max(0,z-1.854)*1.25
def brow_y(x,z):return wrap_y(x,brow_radius(z),brow_depth(z))
v=Vox('Warrior_BrowPlate','Head',H)
v.shape((-.265,.265,-.43,.01,1.82,2.04),lambda x,y,z:abs(x)<brow_radius(z) and visor_top(x)+.027<z<2.03 and y>brow_y(x,z),lambda x,y,z:'Edge' if z<visor_top(x)+.043 else 'Steel');v.finish()

# The projecting chin returns into the neck guard underneath, closing the bottom.
v=Vox('Warrior_Bevor','Head',H)
def bevor(x,y,z):
 rx=.172+max(0,min(1,(z-1.463)/.147))*.075
 depth=.277+max(0,min(1,(z-1.463)/.137))*.112
 return abs(x)<rx and 1.463<z<1.510+.088*(abs(x)/.255)**2 and (y<.018 and y>wrap_y(x,rx,depth) or y>=.018 and ellipse(x,y-.018,rx,.157+max(0,z-1.463)*.40)<1.04)
v.shape((-.26,.26,-.40,.23,1.46,1.63),bevor,'Steel');v.finish()

# Restrained crown ridge and two shallow swept ribs, not stacked brow blocks.
v=Vox('Warrior_HelmetRidge','Head',H)
for j in range(14):
 z=1.89+j*.012
 if z<2.032:
  y=min(brow_y(0,z),-crown_radius(z)[1]+.018)
  v.box((0,y-.004,z),(.020,.023,.021),'Steel')
for j in range(20):
 y=-.145+j*.018;z=1.80+.266*math.sqrt(max(0,1-((y-.018)/.235)**2))
 v.box((0,y,z),(.023,.024,.020),'Edge' if j>8 else 'Steel')
for side in [-1,1]:
 for j in range(17):
  y=-.085+j*.019
  z=1.80+.263*math.sqrt(max(0,1-(.102/.249)**2-((y-.018)/.238)**2))
  v.box((side*.102,y,z),(.018,.021,.016),'SteelDark')
 # Hinge disks translated into a small stepped voxel rosette.
 v.box((side*.247,-.040,1.786),(.024,.056,.056),'SteelDark')
 v.box((side*.260,-.040,1.786),(.016,.028,.028),'Brass')
v.finish()

# Tiny eye glints remain flush with the recessed dark material inside the slit.
vertices=[];faces=[]
for cx in [-.091,.091]:
 z=visor_top(cx)+.010;i=len(vertices);w=.010;h=.006
 candidates=[k[1] for k in face.cells if abs((k[0]+.5)*H-cx)<H and abs((k[2]+.5)*H-z)<H]
 front=min(candidates)*H-.0007
 vertices.extend([(cx-w,front,z-h),(cx+w,front,z-h),(cx+w,front,z+h),(cx-w,front,z+h)]);faces.append(tuple(range(i,i+4)))
m=bpy.data.meshes.new('Flush visor eyes');m.from_pydata(vertices,[],faces);m.materials.append(MATS['Eyes',1]);o=bpy.data.objects.new('Warrior_Eyes_Flush',m);scene.collection.objects.link(o);o.parent=arm;o.vertex_groups.new(name='Head').add(list(range(8)),1,'REPLACE');o.modifiers.new('Head skin','ARMATURE').object=arm;PARTS.append(o)

# Tied horsehair plume emerging from the rear crown, with a curved five-bone tail.
v=Vox('Warrior_PlumeSocket','Head')
v.box((0,.112,1.991),(.118,.10,.069),'SteelDark');v.box((0,.131,2.023),(.091,.055,.037),'Brass');v.finish()
v=Vox('Warrior_Plume','Head')
def plume_shape(x,y,z):
 distance,i,t=plume_nearest(x,y,z);radii=[.045,.090,.103,.080,.056,.024]
 radius=radii[i]*(1-t)+radii[i+1]*t
 radius*=.95+.07*math.cos(x*110)+.045*math.sin(z*65)
 return distance<radius
v.shape((-.13,.13,.04,.64,1.425,2.265),plume_shape,lambda x,y,z:'CrimsonLight' if int(abs(x)/S)%3==1 else ('CrimsonDark' if int(abs(x)/S)%3==2 else 'Crimson'),plume_weights);v.finish()

# The collar and shoulder yoke share the chest anchor with the top of the cape.
# Their intersecting volumes give a continuous cloth attachment even during turns.
v=Vox('Warrior_Scarf','Chest')
for row in range(3):
 v.shape((-.24,.24,-.21,.22,1.30,1.47),lambda x,y,z,row=row:1.322+row*.040+.023*abs(x)/.23<z<1.376+row*.040+.012*abs(x)/.23 and .40<ellipse(x,y,.225-row*.017,.197-row*.011)<1.08,lambda x,y,z:'ClothLight' if z>1.42 else ('ClothDark' if z<1.36 else 'Cloth'))
# A sloping mantle runs from the collar over the shoulders into the cape.
v.shape((-.31,.31,.13,.34,1.255,1.455),lambda x,y,z:abs(x)<.30 and 1.27+.022*abs(x)/.30<z<1.443-.155*abs(x)/.30 and abs(y-(.197+(1.40-z)*.75))<.035,lambda x,y,z:'Cloth' if z<1.41 else 'ClothLight')
v.finish()
v=Vox('Warrior_Belt','Hips')
v.shape((-.28,.28,-.207,.207,.865,.975),lambda x,y,z:.865<z<.963 and .77<ellipse(x,y,.255,.19)<1.16,'Leather')
for x in [-.19,.16]:v.box((x,-.17,.914),(.035,.06,.10),'LeatherEdge')
for x in [-.055,.055]:v.box((x,-.211,.918),(.025,.033,.096),'Brass')
for z in [.878,.958]:v.box((0,-.211,z),(.13,.033,.025),'Brass')
v.box((0,-.228,.918),(.018,.024,.061),'Brass');v.finish()
v=Vox('Warrior_Harness','Chest')
for side in [-1,1]:
 for row in range(8):
  t=row/7;x=side*(.195-.07*t);z=1.325-.22*t
  v.box((x,-.171-.032*t,z),(.035,.035,.043),'Leather')
  if row in (1,5):v.box((x,-.198-.032*t,z),(.045,.021,.031),'Brass')
v.finish()

# One continuous thick cloth surface. Three strips share weights across the width.
v=Vox('Warrior_Cape','Chest')
def cape_shape(x,y,z):
 if not .34<z<1.405:return False
 t=(1.40-z)/1.0;width=.305+.096*t
 hem=.397+.043*math.sin(x*42)+.028*math.cos(x*91)
 if abs(x)>width or z<hem:return False
 # Small cutouts and torn stepped hem, never large through-holes in the shoulders.
 if z<.70 and (abs(x-.055)<.027 and z<.61 or abs(x+.14)<.018 and z<.54):return False
 surface=.25+.15*t+.018*math.sin(x*34)*(0.3+t)+.009*math.cos(z*15+x*8)
 return abs(y-surface)<.025
v.shape((-.43,.43,.19,.48,.33,1.42),cape_shape,lambda x,y,z:'ClothLight' if math.sin(x*34)>.45 else ('ClothDark' if math.sin(x*34)<-.55 else 'Cloth'),cape_weights)
# Folded cowl drapes over the attachment, tapering down the spine.
v.shape((-.29,.29,.255,.345,1.115,1.43),lambda x,y,z:1.115<z<1.425 and abs(x)<min(.272,(z-1.115)*1.0) and abs(y-(.285+(1.40-z)*.08))<.03,lambda x,y,z:'ClothLight' if abs(x)>(z-1.115)*.78 else 'Cloth',cape_weights)
v.finish()

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
report={'voxel_size':S,'helmet_voxel_size':H,'mesh_count':len(PARTS),'occupied_voxels':COUNT,'vertices':sum(len(o.data.vertices) for o in PARTS),'triangles':sum(len(o.data.polygons)*2 for o in PARTS),'bones':list(data.bones.keys()),'cape_tips_blender':cape_tips,'plume_tip_blender':list(plume_points[-1]),'colors':colors}
(OUT/'model-manifest.json').write_text(json.dumps(report,indent=2))

# Non-exported studio, retained in the editable source in T pose.
studio=bpy.data.collections.new('STUDIO - not exported');scene.collection.children.link(studio)
world=bpy.data.worlds.new('Warrior studio');scene.world=world;world.use_nodes=True;world.node_tree.nodes['Background'].inputs[0].default_value=(.16,.21,.30,1);world.node_tree.nodes['Background'].inputs[1].default_value=.4
def light(name,loc,power,size,color):
 d=bpy.data.lights.new(name,'AREA');d.energy=power;d.shape='DISK';d.size=size;d.color=color
 o=bpy.data.objects.new(name,d);studio.objects.link(o);o.location=loc;o.rotation_euler=(Vector((0,0,1.1))-o.location).to_track_quat('-Z','Y').to_euler()
light('Key',(-3,-4,6),480,4,(1,.89,.76));light('Fill',(4,-2,3),320,3,(.68,.79,1));light('Rim',(0,4,4),560,3,(.72,.81,1))
mesh=bpy.data.meshes.new('Studio floor');mesh.from_pydata([(-200,-200,0),(200,-200,0),(200,200,0),(-200,200,0)],[],[(0,1,2,3)])
floor=bpy.data.objects.new('Floor',mesh);studio.objects.link(floor)
mat=bpy.data.materials.new('Backdrop');mat.use_nodes=True;mat.node_tree.nodes.get('Principled BSDF').inputs['Base Color'].default_value=(.036,.059,.09,1);mat.node_tree.nodes.get('Principled BSDF').inputs['Roughness'].default_value=1;floor.data.materials.append(mat)
cam=bpy.data.objects.new('Review',bpy.data.cameras.new('Review'));studio.objects.link(cam);scene.camera=cam;cam.data.type='ORTHO';cam.data.ortho_scale=2.53
scene.render.engine='CYCLES';scene.cycles.samples=24;scene.cycles.use_denoising=True;scene.render.resolution_x=900;scene.render.resolution_y=1050;scene.render.resolution_percentage=100;scene.render.image_settings.file_format='PNG';scene.view_settings.view_transform='AgX';scene.render.fps=60
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'AshenWarrior.blend'))
with zipfile.ZipFile(SOURCE/'AshenWarrior_Source.zip','w',zipfile.ZIP_DEFLATED) as z:z.write(OUT/'AshenWarrior.blend','AshenWarrior.blend');z.write(__file__,'build_ashen_warrior.py')
print('ASHEN_WARRIOR_EXPORT_OK',report['vertices'],report['triangles'],flush=True)
def render(name,loc):
 cam.location=loc;cam.rotation_euler=(Vector((0,0,1.04))-cam.location).to_track_quat('-Z','Y').to_euler();scene.render.filepath=str(OUT/(name+'.png'));bpy.ops.render.render(write_still=True)
if '--no-render' not in sys.argv:
 render('T_Pose',(0,-7,2.05))
 for side,sgn in [('L',1),('R',-1)]:
  b=arm.pose.bones['UpperArm.'+side];b.rotation_mode='XYZ';b.rotation_euler.x=math.radians(-67)
  b=arm.pose.bones['LowerArm.'+side];b.rotation_mode='XYZ';b.rotation_euler.z=math.radians(-10*sgn)
 bpy.context.view_layer.update()
 render('Hero',(3,-7,2.8));render('Side',(7,-.1,2.05));render('Back',(-3,7,2.5))
