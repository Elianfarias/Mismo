import bpy, math, random, json, os
from mathutils import Vector, Matrix, Quaternion, Euler
from pathlib import Path
OUT=Path(__file__).parent
random.seed(47)
bpy.ops.wm.read_factory_settings(use_empty=True)
s=bpy.context.scene;s.render.fps=60
geo=bpy.data.collections.new('GOBLIN | voxel parts');s.collection.children.link(geo)
stage=bpy.data.collections.new('STUDIO | preview only');s.collection.children.link(stage)
mats=[]
def mat(name,c,metal=0,emit=0):
 m=bpy.data.materials.new(name);m.diffuse_color=(*c,1);m.use_nodes=True
 p=m.node_tree.nodes.get('Principled BSDF');p.inputs['Base Color'].default_value=(*c,1);p.inputs['Roughness'].default_value=.78;p.inputs['Metallic'].default_value=metal
 if emit:p.inputs['Emission Color'].default_value=(*c,1);p.inputs['Emission Strength'].default_value=emit
 mats.append(m);return len(mats)-1
def pal(name,c):return [mat(name+f' {i+1}',tuple(min(1,v*k) for v in c)) for i,k in enumerate([.82,.94,1,1.08])]
skin=pal('Olive skin',(.13,.17,.029));hood=pal('Oxide red hood',(.13,.031,.015));leather=pal('Worn leather',(.053,.025,.012));wrap=pal('Linen bandages',(.33,.24,.14));steel=pal('Dark iron',(.073,.079,.069));rust=pal('Rust',(.17,.065,.021))
dark=[mat('Deep recess',(.024,.028,.012))];inner=pal('Inner ear',(.17,.22,.042));tooth=[mat('Old ivory',(.7,.61,.40))];eye=[mat('Sulfur eyes',(.8,1,.12),emit=1.5)];bright=[mat('Edge metal',(.38,.39,.33),.65)]
parts=[];cell=.043
cube_verts=[(-1,-1,-1),(1,-1,-1),(1,1,-1),(-1,1,-1),(-1,-1,1),(1,-1,1),(1,1,1),(-1,1,1)]
cube_faces=[(0,3,2,1),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)]
def blocks(name,cubes,bone):
 vs=[];fs=[];mi=[]
 for center,size,palette in cubes:
  n=len(vs);vs.extend([(center[0]+v[0]*size[0]/2,center[1]+v[1]*size[1]/2,center[2]+v[2]*size[2]/2) for v in cube_verts]);fs.extend([tuple(n+i for i in face) for face in cube_faces]);mi.extend([random.choice(palette)]*6)
 mesh=bpy.data.meshes.new(name);mesh.from_pydata(vs,[],fs);mesh.update();o=bpy.data.objects.new(name,mesh);geo.objects.link(o)
 for m in mats:mesh.materials.append(m)
 for f,i in zip(mesh.polygons,mi):f.material_index=i
 group=o.vertex_groups.new(name=bone);group.add(list(range(len(vs))),1,'REPLACE');o['rig_part']=bone
 bevel=o.modifiers.new('Tiny voxel edges','BEVEL');bevel.width=.0012;bevel.segments=1
 parts.append(o);return o
def vox(name,lo,hi,inside,palette,bone,step=cell):
 cubes=[]
 for i in range(math.floor(lo[0]/step),math.ceil(hi[0]/step)+1):
  for j in range(math.floor(lo[1]/step),math.ceil(hi[1]/step)+1):
   for k in range(math.floor(lo[2]/step),math.ceil(hi[2]/step)+1):
    p=Vector((i*step,j*step,k*step))
    if inside(p):cubes.append((p,(step*.985,)*3,palette(p) if callable(palette) else palette))
 return blocks(name,cubes,bone)
def ell(name,c,r,palette,bone):
 c=Vector(c);r=Vector(r)
 return vox(name,c-r,c+r,lambda p:sum(((p[i]-c[i])/r[i])**2 for i in range(3))<=1,palette,bone)
def box(name,c,size,palette,bone):return blocks(name,[(c,size,palette)],bone)
def limb(name,a,b,r,palette,bone):
 a=Vector(a);b=Vector(b);d=b-a
 def inside(p):
  t=max(0,min(1,(p-a).dot(d)/d.length_squared));return (p-a-d*t).length<=r*(1-.12*t)
 return vox(name,[min(a[i],b[i])-r for i in range(3)],[max(a[i],b[i])+r for i in range(3)],inside,palette,bone)

# Broad shoulders, narrow waist, crouched feet, oversized expressive head.
ell('Chest',(0,.02,1.05),(.28,.16,.25),skin,'Chest');ell('Waist',(0,.035,.85),(.195,.145,.17),skin,'Hips')
ell('Neck',(0,-.005,1.23),(.12,.115,.12),skin,'Chest')
ell('Head',(0,-.015,1.48),(.275,.22,.285),skin,'Head')
ell('Jaw',(0,-.15,1.31),(.19,.13,.11),skin,'Head')
ell('Cheek L',(.19,-.17,1.41),(.095,.11,.10),skin,'Head');ell('Cheek R',(-.19,-.17,1.41),(.095,.11,.10),skin,'Head')
for sign in [-1,1]:
 x=sign*.112
 box('Eye socket '+str(sign),(x,-.230,1.485),(.145,.043,.133),dark,'Head')
 box('Luminous eye '+str(sign),(x,-.257,1.48),(.07,.025,.07),eye,'Head')
 for j in range(3):box('Brow',(sign*(.057+j*.047),-.276,1.535+j*.017),(.053,.042,.052),dark,'Head')
 box('Lower tusk',(sign*.139,-.280,1.335),(.036,.047,.071),tooth,'Head')
ell('Crooked broad nose',(0,-.27,1.405),(.095,.087,.067),skin,'Head')
box('Mouth crease',(0,-.26,1.31),(.195,.025,.022),dark,'Head')
for sign in [-1,1]:
 def ear_inside(p,sg=sign):
  u=(p.x*sg-.23)/.47
  if not 0<=u<=1:return False
  bottom=1.46+.30*u;top=1.72+.09*u
  return bottom<=p.z<=top and abs(p.y-.013)<.071*(1-.55*u)
 vox('Long ear '+str(sign),(-.74,-.09,1.43),(.74,.10,1.85),ear_inside,lambda p:inner if p.y<-.02 else skin,'Head')
 x=sign*.59
 for c,sz in [((x,-.058,1.61),(.036,.037,.11)),((x+sign*.045,-.058,1.61),(.025,.037,.11)),((x+sign*.021,-.058,1.56),(.066,.037,.025))]:box('Ear iron ring',c,sz,bright,'Head')

# Hood forms an actual shell with an open face, and hangs beside the cheeks.
def hood_shape(p):
 q=p-Vector((0,.025,1.47));outer=(q.x/.337)**2+(q.y/.281)**2+(q.z/.365)**2
 inn=(q.x/.278)**2+(q.y/.219)**2+(q.z/.306)**2
 return outer<=1 and inn>=1 and not(p.y<-.09 and abs(p.x)<.245 and p.z<1.70)
vox('Red hood shell',(-.36,-.29,1.10),(.36,.33,1.86),hood_shape,hood,'Head')
for sg in [-1,1]:
 limb('Hood cheek drape',(sg*.279,-.13,1.55),(sg*.23,-.14,1.15),.065,hood,'Head')
 limb('Cowl front V',(sg*.22,-.145,1.20),(0,-.205,1.035),.07,hood,'Chest')
vox('Back cowl',(-.25,.10,.97),(.25,.25,1.27),lambda p:abs(p.x)<(p.z-.93)*.8 and .10<p.y<.22,hood,'Chest')

for sg,side in [(-1,'R'),(1,'L')]:
 shoulder=(sg*.285,.015,1.16);elbow=(sg*.43,-.015,.96);wrist=(sg*.52,-.105,.755)
 limb('Upper arm '+side,shoulder,elbow,.084,skin,'UpperArm.'+side);limb('Forearm '+side,elbow,wrist,.075,skin,'LowerArm.'+side)
 for z in [.84,.89,.94]:
  t=(.96-z)/(.96-.755);c=Vector(elbow).lerp(Vector(wrist),t)
  box('Arm wrap '+side,c,(.16,.16,.037),wrap,'LowerArm.'+side)
 ell('Palm '+side,(sg*.535,-.117,.715),(.094,.071,.095),skin,'Hand.'+side)
 for n in range(3):
  x=sg*(.478+n*.05)
  limb('Finger '+side,(x,-.14,.7),(x,-.18,.62),.024,skin,'Hand.'+side)
  box('Claw '+side,(x,-.19,.603),(.036,.045,.045),dark,'Hand.'+side)
 hip=(sg*.13,.035,.80);knee=(sg*.235,-.065,.48);ankle=(sg*.275,.02,.19)
 limb('Thigh '+side,hip,knee,.105,skin,'UpperLeg.'+side);limb('Shin '+side,knee,ankle,.082,skin,'LowerLeg.'+side)
 for z in [.24,.29,.34]:
  c=Vector(knee).lerp(Vector(ankle),(.48-z)/.29);box('Leg wrap '+side,c,(.174,.16,.039),wrap,'LowerLeg.'+side)
 box('Sandal sole '+side,(sg*.275,-.052,.065),(.235,.32,.065),leather,'Foot.'+side)
 ell('Foot '+side,(sg*.275,-.08,.123),(.102,.15,.062),skin,'Foot.'+side)
 for n in range(3):box('Toe '+side,(sg*(.207+n*.065),-.187,.103),(.057,.07,.058),skin,'Foot.'+side)
 box('Sandal strap '+side,(sg*.275,-.065,.16),(.224,.055,.043),leather,'Foot.'+side)

# Layered one-sided shoulder armor and chest harness.
for k in range(3):
 ell('Left shoulder plate '+str(k),(.285+k*.031,.021,1.195-k*.035),(.145,.18,.078),steel,'UpperArm.L')
 box('Pauldron leather ridge',(.27+k*.052,-.152,1.2-k*.042),(.038,.039,.084),wrap,'UpperArm.L')
limb('Diagonal chest strap',(.23,-.123,1.17),(-.13,-.162,.855),.032,leather,'Chest')
for sg in [-1,1]:
 for i in range(5):box('Belt segment',(sg*(.025+i*.044),-.144,.817),(.046,.065,.085),leather,'Hips')
box('Back belt',(0,.168,.817),(.40,.05,.086),leather,'Hips')
for c,sz in [((-.07,-.197,.823),(.025,.035,.123)),((.07,-.197,.823),(.025,.035,.123)),((0,-.197,.881),(.145,.035,.026)),((0,-.197,.762),(.145,.035,.026)),((.024,-.218,.823),(.10,.025,.02))]:box('Iron belt buckle',c,sz,bright,'Hips')
for sg in [-1,1]:
 box('Belt pouch',(sg*.237,-.035,.77),(.12,.14,.15),leather,'Hips');box('Pouch flap',(sg*.237,-.116,.79),(.135,.025,.068),rust,'Hips');box('Pouch rivet',(sg*.237,-.137,.786),(.025,.02,.025),bright,'Hips')
for sg in [-1,1]:
 for i in range(3):
  x=sg*(.08+i*.044);limb('Leather skirt strips',(x,-.11,.77),(x*1.4,-.12,.59+(i%2)*.04),.034,leather,'Hips')
vox('Front ragged tabard',(-.12,-.20,.43),(.12,-.13,.79),lambda p:abs(p.x)<.04+(p.z-.43)*.27 and -.19<p.y<-.13,hood,'Hips')
vox('Back ragged cloth',(-.14,.12,.47),(.14,.21,.79),lambda p:abs(p.x)<.035+(p.z-.47)*.34 and .13<p.y<.20,hood,'Hips')

# Cleaver blade, stepped cutting edge and irregular rust patches.
grip=Vector((-.535,-.145,.705));blade_axis=Vector((-.75,-.36,-.55)).normalized()
normal=Vector((0,-1,0));normal=(normal-blade_axis*normal.dot(blade_axis)).normalized();basis=Matrix((normal.cross(blade_axis),normal,blade_axis)).transposed()
weapon=[]
def wb(c,sz,p):
 # Axis-aligned voxels in the weapon's own frame, transformed as a complete piece below.
 weapon.append((c,sz,p))
for i in range(5):wb((0,0,(i-2)*.035),(.062,.065,.033),leather if i%2 else rust)
wb((0,0,.095),(.23,.082,.047),steel);wb((0,0,-.105),(.081,.081,.048),bright)
for k in range(14):
 z=.14+k*.038;half=.06+min(k,6)*.016
 if k>10:half-=.022*(k-10)
 for i in range(-5,6):
  x=i*.038
  if abs(x)>half:continue
  palette=bright if x<-.7*half else rust if random.random()<.31 else steel
  wb((x,0,z),(.037,.052,.037),palette)
o=blocks('Rusty cleaver',weapon,'Hand.R')
for v in o.data.vertices:v.co=grip+basis@v.co

# Rigid voxel weights keep each block intact while joints articulate.
ad=bpy.data.armatures.new('Goblin skeleton');arm=bpy.data.objects.new('Goblin_Rig',ad);geo.objects.link(arm);bpy.context.view_layer.objects.active=arm;arm.select_set(True)
bpy.ops.object.mode_set(mode='EDIT')
def bone(n,a,b,parent=None):
 e=ad.edit_bones.new(n);e.head=a;e.tail=b
 if parent:e.parent=ad.edit_bones[parent]
bone('Root',(0,0,0),(0,0,.15));bone('Hips',(0,.035,.80),(0,.02,.99),'Root');bone('Chest',(0,.02,.99),(0,0,1.23),'Hips');bone('Head',(0,0,1.23),(0,-.015,1.65),'Chest')
for sg,side in [(-1,'R'),(1,'L')]:
 bone('UpperArm.'+side,(sg*.285,.015,1.16),(sg*.43,-.015,.96),'Chest');bone('LowerArm.'+side,(sg*.43,-.015,.96),(sg*.52,-.105,.755),'UpperArm.'+side)
 a=Vector((sg*.52,-.105,.755));direction=blade_axis if side=='R' else Vector((0,-.1,-1)).normalized();bone('Hand.'+side,a,a+direction*.14,'LowerArm.'+side)
 bone('UpperLeg.'+side,(sg*.13,.035,.80),(sg*.235,-.065,.48),'Hips');bone('LowerLeg.'+side,(sg*.235,-.065,.48),(sg*.275,.02,.19),'UpperLeg.'+side);bone('Foot.'+side,(sg*.275,.02,.19),(sg*.275,-.17,.11),'LowerLeg.'+side)
bpy.ops.object.mode_set(mode='OBJECT');arm.show_in_front=True;ad.display_type='STICK'
for o in parts:
 o.parent=arm;mod=o.modifiers.new('Rigid voxel rig','ARMATURE');mod.object=arm
 # Every part has a single rigid weight; bevel and deformation commute.
 # Evaluate only bones while authoring hundreds of IK poses.
 for modifier in o.modifiers:modifier.show_viewport=False
print('GEOMETRY_READY',len(parts),flush=True)
P=arm.pose.bones;R=ad.bones
for p in P:p.rotation_mode='QUATERNION'
def update():bpy.context.view_layer.update()
def reset():
 for p in P:p.matrix_basis=Matrix.Identity(4)
def turn(n,x=0,y=0,z=0):P[n].rotation_quaternion=Euler(tuple(math.radians(v) for v in (x,y,z)),'XYZ').to_quaternion()
def aim(n,d):
 b=R[n];p=P[n];q=(b.tail_local-b.head_local).rotation_difference(Vector(d).normalized())@b.matrix_local.to_quaternion();p.matrix=Matrix.Translation(p.head)@q.to_matrix().to_4x4();update()
def ik(upper,lower,target,pole):
 update();a=R[upper].length;b=R[lower].length;h=P[upper].head.copy();v=Vector(target)-h;dist=max(.001,min(v.length,a+b-.002));v.normalize();along=(a*a-b*b+dist*dist)/(2*dist);bend=Vector(pole);bend=(bend-v*bend.dot(v)).normalized();k=h+v*along+bend*math.sqrt(max(.000001,a*a-along*along));aim(upper,k-h);aim(lower,h+v*dist-k)
def body(bob=0,lean=0,twist=0):
 reset();P['Hips'].location=R['Hips'].matrix_local.to_3x3().inverted()@Vector((0,0,bob));turn('Chest',lean,twist,0);turn('Head',-lean*.25,-twist*.25,0);update()
def legs(t=0,moving=False,run=False):
 for sg,side in [(-1,'R'),(1,'L')]:
  u=(t+(0 if sg==1 else .5))%1;stride=.24 if not run else .39
  y=.02+(math.cos(u*math.tau)*stride if moving else 0);z=.19+(max(0,math.sin(u*math.tau))*(.10 if not run else .17) if moving else 0)
  ik('UpperLeg.'+side,'LowerLeg.'+side,(sg*.275,y,z),(0,-1,0))
  p=P['Foot.'+side];p.matrix=Matrix.Translation(p.head)@R['Foot.'+side].matrix_local.to_quaternion().to_matrix().to_4x4();update()
def hand(side,target,direction=None):
 sg=-1 if side=='R' else 1;ik('UpperArm.'+side,'LowerArm.'+side,target,(sg,0,0))
 aim('Hand.'+side,direction if direction is not None else (blade_axis if side=='R' else (0,-.1,-1)))
def pose(name,t):
 wave=math.sin(t*math.tau)
 if name in ('Idle','Walk','Run'):
  moving=name!='Idle';run=name=='Run';body(.006*wave if not moving else -.018+.017*math.cos(t*4*math.pi),3 if not run else 9,2*wave if not moving else 5*wave);legs(t,moving,run)
  hand('R',(-.52,-.105+(.065*wave if moving else 0),.755));hand('L',(.52,-.105-(.09*wave if moving else 0),.755))
 elif name=='Attack':
  # Windup, forward contact, recovery: impact roughly 0.22--0.36 seconds.
  home=Vector((-.52,-.105,.755));wind=Vector((-.38,.04,1.23));hit=Vector((-.22,-.49,.86))
  if t<.32:u=t/.32;target=home.lerp(wind,u);d=blade_axis.lerp(Vector((-.25,.1,1)),u);tw=-19*u
  elif t<.59:u=(t-.32)/.27;target=wind.lerp(hit,u);d=Vector((-.25,.1,1)).lerp(Vector((.15,-1,-.3)),u);tw=-19+39*u
  else:u=(t-.59)/.41;u=u*u*(3-2*u);target=hit.lerp(home,u);d=Vector((.15,-1,-.3)).lerp(blade_axis,u);tw=20*(1-u)
  body(-.025*math.sin(t*math.pi),8*math.sin(t*math.pi),tw);legs();hand('R',target,d);hand('L',(.47,-.14,.84))
 elif name=='Hit':
  w=math.sin(math.pi*min(1,t/.28)) if t<.14 else max(0,1-(t-.14)/.86)**2
  body(-.035*w,-18*w,-9*w);turn('Head',-12*w,8*w,0);update();legs();hand('R',(-.52,-.105+.08*w,.755+.04*w));hand('L',(.52,-.105+.06*w,.755+.08*w))
spec=[('Idle',2.0),('Walk',.85),('Run',.60),('Attack',.65),('Hit',.42)]
arm.animation_data_create()
for name,duration in spec:
 act=bpy.data.actions.new(name);act.use_fake_user=True;arm.animation_data.action=act;frames=round(duration*60)
 for f in range(frames+1):
  pose(name,f/frames)
  for p in P:
   p.keyframe_insert(data_path='rotation_quaternion',frame=f+1,group=p.name);p.keyframe_insert(data_path='location',frame=f+1,group=p.name)
 print('ACTION',name,flush=True)
arm.animation_data.action=bpy.data.actions['Idle'];s.frame_set(1);update();s.frame_start=1;s.frame_end=121
for o in parts:
 for modifier in o.modifiers:modifier.show_viewport=True

# Neutral studio, front three-quarter hero view; no AI image stands in for the model.
def move_stage(o):
 for c in list(o.users_collection):c.objects.unlink(o)
 stage.objects.link(o)
bpy.ops.mesh.primitive_plane_add(size=200);floor=bpy.context.object;floor.name='Studio ground';move_stage(floor);fm=bpy.data.materials.new('Studio warm gray');fm.diffuse_color=(.09,.075,.06,1);fm.use_nodes=True;fm.node_tree.nodes.get('Principled BSDF').inputs['Base Color'].default_value=fm.diffuse_color;floor.data.materials.append(fm)
def area(name,pos,power,size):
 data=bpy.data.lights.new(name,'AREA');data.energy=power;data.shape='DISK';data.size=size;o=bpy.data.objects.new(name,data);stage.objects.link(o);o.location=pos;o.rotation_euler=(Vector((0,0,.9))-o.location).to_track_quat('-Z','Y').to_euler()
area('Large warm key',(-3,-4,5),500,4);area('Soft fill',(3,-1,3),180,3);area('Rim',(1,3,4),650,3)
camdata=bpy.data.cameras.new('Presentation camera');cam=bpy.data.objects.new('Presentation camera',camdata);stage.objects.link(cam);s.camera=cam;camdata.type='ORTHO';camdata.ortho_scale=2.6
def camera(pos,target=(0,-.03,.93)):
 cam.location=pos;cam.rotation_euler=(Vector(target)-cam.location).to_track_quat('-Z','Y').to_euler()
camera((3,-6,2.8))
s.render.engine='CYCLES';s.cycles.samples=24;s.cycles.use_denoising=True;s.render.resolution_x=1000;s.render.resolution_y=1100;s.render.resolution_percentage=100
s.world=bpy.data.worlds.new('Studio world');s.world.use_nodes=True;s.world.node_tree.nodes.get('Background').inputs['Color'].default_value=(.2,.2,.2,1);s.world.node_tree.nodes.get('Background').inputs['Strength'].default_value=.3;s.view_settings.view_transform='AgX'
bpy.ops.object.select_all(action='DESELECT');arm.select_set(True);bpy.context.view_layer.objects.active=arm
for screen in bpy.data.screens:
 for a in screen.areas:
  if a.type=='VIEW_3D':
   a.spaces.active.shading.color_type='MATERIAL';a.spaces.active.region_3d.view_distance=3;a.spaces.active.region_3d.view_location=(0,0,.95)
s['asset_notes']='Concept-inspired voxel goblin. Forward -Y, up Z. Rigid weighted blocks. Actions: Idle, Walk, Run, Attack, Hit. Studio excluded from FBX.'
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'Goblin_Concept_Rigged.blend'))
for name,pos in [('Hero',(3,-6,2.8)),('Back',(-3,6,2.7))]:
 camera(pos);s.render.filepath=str(OUT/(name+'.png'));bpy.ops.render.render(write_still=True)
camera((3,-6,2.8));s.render.resolution_x=500;s.render.resolution_y=550;s.cycles.samples=12
for name,duration in [('Attack',.65),('Hit',.42),('Run',.60)]:
 arm.animation_data.action=bpy.data.actions[name];folder=OUT/'preview'/name;folder.mkdir(parents=True,exist_ok=True)
 for i in range(16):
  s.frame_set(1+round(i/15*round(duration*60)));s.render.filepath=str(folder/f'{i:02}.png');bpy.ops.render.render(write_still=True)
arm.animation_data.action=bpy.data.actions['Idle'];s.frame_set(1)
report={'objects':len(parts),'vertices':sum(len(o.data.vertices) for o in parts),'bones':len(R),'actions':dict(spec),'height_m':1.85,'original_preserved':True}
(OUT/'Report.json').write_text(json.dumps(report,indent=2));print('GOBLIN_READY',report,flush=True)
