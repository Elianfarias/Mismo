import bpy, math, random, os, json
from mathutils import Vector
from math import sin, cos, pi
OUT=os.path.dirname(os.path.abspath(__file__))
bpy.ops.wm.read_factory_settings(use_empty=True)
S=.065
cells={}
def box(center,size,mat,bone):
    for i in range(math.floor((center[0]-size[0]/2)/S),math.ceil((center[0]+size[0]/2)/S)):
      for j in range(math.floor((center[1]-size[1]/2)/S),math.ceil((center[1]+size[1]/2)/S)):
       for k in range(math.floor((center[2]-size[2]/2)/S),math.ceil((center[2]+size[2]/2)/S)):
        cells[i,j,k]=(mat,bone)
def ell(c,r,mat,bone,rough=False):
 for i in range(math.floor((c[0]-r[0])/S),math.ceil((c[0]+r[0])/S)):
  for j in range(math.floor((c[1]-r[1])/S),math.ceil((c[1]+r[1])/S)):
   for k in range(math.floor((c[2]-r[2])/S),math.ceil((c[2]+r[2])/S)):
    p=((i+.5)*S,(j+.5)*S,(k+.5)*S)
    q=sum(((p[a]-c[a])/r[a])**2 for a in range(3))
    wave=.045*sin(i*2+j*.9)*cos(k*1.4) if rough else 0
    if q<1+wave: cells[i,j,k]=(mat,bone)
def tube(a,b,r,mat,bone):
 n=max(2,int((Vector(b)-Vector(a)).length/(S*.4)))
 for t in range(n+1):
  p=Vector(a).lerp(Vector(b),t/n);ell(p,(r,r,r),mat,bone)
# -Y is forward, Z is up; dimensions in meters.
ell((0,.18,.90),(.48,.80,.47),'fur','Body',True)
ell((0,-.24,1.01),(.50,.47,.49),'fur','Body',True)
ell((0,.67,.80),(.38,.36,.39),'fur','Body',True)
# Head hangs in front of the powerful shoulders.
ell((0,-.65,.78),(.37,.46,.38),'fur','Head',True)
ell((0,-.85,.60),(.30,.34,.25),'fur','Head')
box((0,-1.075,.57),(.40,.27,.22),'fur','Head')
box((0,-1.25,.49),(.40,.15,.21),'snout','Head')
for sign in [-1,1]:
 box((sign*.105,-1.335,.495),(.069,.015,.075),'nostril','Head')
 # cheek ridges, brow and eyes
 ell((sign*.305,-.64,.66),(.13,.25,.22),'fur','Head')
 box((sign*.32,-.893,.835),(.09,.14,.13),'shadow','Head')
 box((sign*.346,-.921,.825),(.026,.063,.055),'eye','Head')
 box((sign*.294,-.927,.908),(.16,.19,.072),'fur','Head')
 # Small pointed upright ears, with dark inner blocks.
 for z,w in [(1.04,.16),(1.105,.14),(1.17,.105),(1.235,.065)]:
  box((sign*(.29+(z-1.04)*.26),-.52,z),(w,.13,S),'fur','Ear'+str(sign))
 box((sign*.315,-.593,1.135),(.065,.012,.13),'shadow','Ear'+str(sign))
 # Ivory tusks curving outwards and up.
 for c,sz in [((sign*.28,-1.16,.43),(.13,.13,.13)),((sign*.36,-1.18,.48),(.13,.13,.13)),((sign*.40,-1.18,.565),(.09,.10,.13)),((sign*.425,-1.18,.68),(.065,.075,.12)),((sign*.425,-1.18,.77),(.055,.065,.065))]:
  box(c,sz,'tusk','Head')
# A jagged dorsal crest broad at shoulders, tapering towards the tail.
for j in range(-9,14):
 y=(j+.5)*S
 top=1.40+.26*math.exp(-((y+.15)/.40)**2)-.19*max(0,y)
 width=.20+.12*math.exp(-((y+.12)/.45)**2)
 for i in range(-math.ceil(width/S),math.ceil(width/S)):
  x=(i+.5)*S
  height=top-abs(x)*1.12 + (.03 if j%3==0 else 0)
  for k in range(int(1.14/S),int(height/S)):
   cells[i,j,k]=('mane','Head' if y<-.48 else 'Body')
# Dark mantle under the crest, following the curved shoulders.
for key,(role,bn) in list(cells.items()):
 x,y,z=[(v+.5)*S for v in key]
 if role=='fur' and -.50<y<.86 and abs(x)<.33 and z>1.20+.12*abs(x)/.33-.08*y:
  cells[key]=('mane',bn)
# Four articulated legs: thigh, shin and split hoof.
legs={}
for sign in [-1,1]:
 for front in [True,False]:
  tag=('F' if front else 'H')+('L' if sign>0 else 'R')
  x=sign*.34; y=-.36 if front else .65
  hip=(x,y,.82 if front else .74)
  knee=(x,y+(.055 if front else -.10),.39)
  ankle=(x,y,.12)
  legs[tag]=(hip,knee,ankle)
  ell((x,y,.63),(.155,.21,.27),'fur','Upper_'+tag,True)
  tube(hip,knee,.11,'fur','Upper_'+tag)
  tube(knee,ankle,.082,'fur','Lower_'+tag)
  for side in [-1,1]:
   box((x+side*.055,y-.045,.065),(.083,.20,.13),'hoof','Foot_'+tag)
# Stiff curled tail, separate two-bone chain.
tube((0,.83,.88),(0,1.12,.75),.052,'fur','Tail')
tube((0,1.12,.75),(0,1.25,.61),.045,'mane','TailTip')
# Armature
arm=bpy.data.armatures.new('Boar_Skeleton'); rig=bpy.data.objects.new('Boar_Rig',arm);bpy.context.collection.objects.link(rig)
bpy.context.view_layer.objects.active=rig;rig.select_set(True);bpy.ops.object.mode_set(mode='EDIT')
def bone(n,h,t,parent=None,deform=True):
 b=arm.edit_bones.new(n);b.head=h;b.tail=t;b.use_deform=deform
 if parent:b.parent=arm.edit_bones[parent]
 return b
bone('Root',(0,0,0),(0,0,.2))
bone('Body',(0,.40,.86),(0,-.32,.98),'Root')
bone('Head',(0,-.40,.90),(0,-1.05,.62),'Body')
for sign in [-1,1]:bone('Ear'+str(sign),(sign*.29,-.52,1.02),(sign*.34,-.52,1.25),'Head')
bone('Tail',(0,.83,.88),(0,1.12,.75),'Body');bone('TailTip',(0,1.12,.75),(0,1.25,.61),'Tail')
for tag,(h,k,a) in legs.items():
 bone('Upper_'+tag,h,k,'Body');bone('Lower_'+tag,k,a,'Upper_'+tag)
 bone('Foot_'+tag,a,(a[0],a[1]-.18,a[2]),'Lower_'+tag)
 bone('IK_'+tag,a,(a[0],a[1]-.18,a[2]),'Root',False)
bpy.ops.object.mode_set(mode='POSE')
for tag in legs:
 c=rig.pose.bones['Lower_'+tag].constraints.new('IK');c.target=rig;c.subtarget='IK_'+tag;c.chain_count=2;c.use_stretch=False
 c=rig.pose.bones['Foot_'+tag].constraints.new('COPY_ROTATION');c.target=rig;c.subtarget='IK_'+tag;c.target_space='WORLD';c.owner_space='WORLD'
for p in rig.pose.bones:p.rotation_mode='XYZ'
bpy.ops.object.mode_set(mode='OBJECT');rig.show_in_front=True;arm.display_type='OCTAHEDRAL'
# Palette texture keeps FBX material portable and permits easy palette swapping.
roles=['fur','mane','snout','nostril','shadow','eye','tusk','hoof','moss']
palettes={'Standard':['95572f','35221b','893d36','291b19','30201a','af2023','ead8b1','252526','667236'], 'Dark_Fur':['484242','211e21','764342','22181a','221b1e','c12a2c','d6cdb5','202126','4e5934'], 'Forest_Moss':['765032','34291c','824236','281b19','302119','b82c23','d8d0ac','282b21','62743a']}
def linear(v):return ((v+.055)/1.055)**2.4 if v>.04045 else v/12.92
faces_dir=[((1,0,0),[(1,0,0),(1,1,0),(1,1,1),(1,0,1)]),((-1,0,0),[(0,0,0),(0,0,1),(0,1,1),(0,1,0)]),((0,1,0),[(0,1,0),(0,1,1),(1,1,1),(1,1,0)]),((0,-1,0),[(0,0,0),(1,0,0),(1,0,1),(0,0,1)]),((0,0,1),[(0,0,1),(1,0,1),(1,1,1),(0,1,1)]),((0,0,-1),[(0,0,0),(0,1,0),(1,1,0),(1,0,0)])]
verts=[];faces=[];faceinfo=[];weights={};vid={}
for (i,j,k),(role,bn) in cells.items():
 shade=random.Random(i*11737+j*2731+k*673).randrange(5)
 moss=(role in ['fur','mane'] and k*S>.78 and sin(i*.75+j*.52)+cos(j*.63-k*.65)>.9)
 for d,corners in faces_dir:
  neighbor=cells.get((i+d[0],j+d[1],k+d[2]))
  if neighbor and neighbor[1]==bn:continue
  ids=[]
  for a,b,c in corners:
   key=(i+a,j+b,k+c,bn)
   if key not in vid:
    vid[key]=len(verts);verts.append(((i+a)*S,(j+b)*S,(k+c)*S));weights.setdefault(bn,[]).append(vid[key])
   ids.append(vid[key])
  faces.append(ids);faceinfo.append((role,shade,moss))
mesh=bpy.data.meshes.new('Boar_Voxel_Surface');mesh.from_pydata(verts,[],faces);mesh.update()
variants=[]
for name,colors in palettes.items():
 me=mesh.copy();ob=bpy.data.objects.new('Boar_'+name,me);bpy.context.collection.objects.link(ob);variants.append(ob)
 for bn,indices in weights.items():ob.vertex_groups.new(name=bn).add(indices,1.0,'REPLACE')
 ob.parent=rig;mod=ob.modifiers.new('Boar Skin','ARMATURE');mod.object=rig
 img=bpy.data.images.new(name+'_Palette',width=45,height=1,alpha=True)
 pix=[]
 for col in colors:
  rgb=[int(col[p:p+2],16)/255 for p in (0,2,4)]
  for sh in range(5):pix += [min(1,v*(.82+sh*.09)) for v in rgb]+[1]
 img.pixels=pix;img.filepath_raw=os.path.join(OUT,name+'_Palette.png');img.file_format='PNG';img.save();img=bpy.data.images.load(img.filepath_raw,check_existing=False);img.pack()
 mat=bpy.data.materials.new(name+'_Material');mat.use_nodes=True
 nt=mat.node_tree;bs=nt.nodes.get('Principled BSDF');bs.inputs['Roughness'].default_value=.9
 tex=nt.nodes.new('ShaderNodeTexImage');tex.image=img;tex.interpolation='Closest';nt.links.new(tex.outputs['Color'],bs.inputs['Base Color']);me.materials.append(mat)
 uv=me.uv_layers.new(name='PaletteUV')
 for poly,(role,shade,moss) in zip(me.polygons,faceinfo):
  if name=='Forest_Moss' and moss:role='moss'
  u=(roles.index(role)*5+shade+.5)/45
  for li in poly.loop_indices:uv.data[li].uv=(u,.5)
 ob.hide_render=name!='Standard';ob.hide_set(name!='Standard')
# Five in-place clips. IK controls keep the feet horizontal during support.
scene=bpy.context.scene;scene.render.fps=30;scene.unit_settings.system='METRIC'
rig.animation_data_create()
clips={'Idle':(60,True),'Walk':(36,True),'Run':(24,True),'Charge':(20,True),'Attack':(36,False)}
for name,(duration,loop) in clips.items():
 action=bpy.data.actions.new(name);action.use_fake_user=True;rig.animation_data.action=action
 for f in range(duration+1):
  t=f/duration;w=2*pi*t
  for p in rig.pose.bones:p.location=(0,0,0);p.rotation_euler=(0,0,0);p.scale=(1,1,1)
  body=rig.pose.bones['Body'];head=rig.pose.bones['Head']
  # Body local Y points towards the head, so translation is converted from world space.
  bounce=.009*sin(w) if name=='Idle' else (.016 if name=='Walk' else .035)*(1-cos(2*w))
  pitch=0
  if name=='Charge':pitch=.04;head.rotation_euler.x=.12
  if name=='Attack':
   wind=sin(pi*min(t/.38,1)) if t<.38 else 0
   strike=sin(pi*(t-.38)/.32) if .38<=t<=.70 else 0
   head.rotation_euler.x=.10*wind-.25*strike
   body.location=body.bone.matrix_local.to_3x3().inverted()@Vector((0,-.13*strike,.035*wind));pitch=.03*wind-.04*strike
  else:body.location=body.bone.matrix_local.to_3x3().inverted()@Vector((0,0,bounce))
  body.rotation_euler.x=pitch
  if name=='Idle':head.rotation_euler.x=.02*sin(w);head.rotation_euler.z=.018*sin(w)
  elif name in ['Walk','Run']:head.rotation_euler.x=.025*sin(w)
  for tag in legs:
   p=rig.pose.bones['IK_'+tag]
   if name in ['Walk','Run','Charge']:
    phase={'FL':0,'HR':.5,'FR':.5,'HL':0}[tag] if name!='Walk' else {'FL':0,'HR':.25,'FR':.5,'HL':.75}[tag]
    u=(t+phase)%1;stance=.62 if name=='Walk' else .48
    stride=.24 if name=='Walk' else (.38 if name=='Run' else .44)
    lift=.08 if name=='Walk' else .13
    if u<stance:dy=-stride/2+stride*u/stance;dz=0
    else:v=(u-stance)/(1-stance);dy=stride/2-stride*(v*v*(3-2*v));dz=lift*sin(pi*v)
    p.location=p.bone.matrix_local.to_3x3().inverted()@Vector((0,dy,dz))
  rig.pose.bones['Tail'].rotation_euler.y=.11*sin(w)
  rig.pose.bones['TailTip'].rotation_euler.x=.07*sin(w+.4)
  for sign in [-1,1]:rig.pose.bones['Ear'+str(sign)].rotation_euler.y=sign*.04*sin(w)
  for p in rig.pose.bones:
   p.keyframe_insert('location',frame=f+1,group=p.name);p.keyframe_insert('rotation_euler',frame=f+1,group=p.name)
 for fc in action.fcurves:
  for kp in fc.keyframe_points:kp.interpolation='LINEAR'
 action['loop']=loop;action['description']='In-place, 30 FPS. '+('Loop.' if loop else 'One-shot attack.')
rig.animation_data.action=bpy.data.actions['Idle'];scene.frame_start=1;scene.frame_end=61;scene.frame_set(1)
# Export each variant with every action baked, including IK results.
for ob in variants:
 for o in bpy.context.selected_objects:o.select_set(False)
 ob.hide_set(False);ob.select_set(True);rig.select_set(True);bpy.context.view_layer.objects.active=rig
 bpy.ops.export_scene.fbx(filepath=os.path.join(OUT,ob.name+'.fbx'),use_selection=True,object_types={'ARMATURE','MESH'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,add_leaf_bones=False,use_armature_deform_only=True,bake_anim=True,bake_anim_use_all_actions=True,bake_anim_use_nla_strips=False,bake_anim_force_startend_keying=True,bake_anim_step=1,bake_anim_simplify_factor=0,path_mode='COPY',embed_textures=True)
 ob.select_set(False);ob.hide_set(ob!=variants[0])
# Studio scene, excluded from FBX exports.
rig.animation_data.action=bpy.data.actions['Idle'];scene.frame_set(1)
def track(ob,p):ob.rotation_euler=(Vector(p)-ob.location).to_track_quat('-Z','Y').to_euler()
bpy.ops.object.camera_add(location=(3.1,-4.3,2.5));cam=bpy.context.object;track(cam,(0,-.04,.8));cam.data.type='ORTHO';cam.data.ortho_scale=3.25;scene.camera=cam
for name,loc,power,size in [('Key',(0,-3,5),550,4),('Fill',(-3,-1,2.5),240,3),('Rim',(1,3,4),700,3)]:
 bpy.ops.object.light_add(type='AREA',location=loc);o=bpy.context.object;o.name=name;o.data.energy=power;o.data.shape='DISK';o.data.size=size;track(o,(0,0,.8))
bpy.ops.mesh.primitive_plane_add(size=200);floor=bpy.context.object;floor.name='Studio_Floor'
mat=bpy.data.materials.new('Backdrop');mat.diffuse_color=(.13,.15,.17,1);floor.data.materials.append(mat)
scene.world=bpy.data.worlds.new('Studio_World');scene.world.use_nodes=True;scene.world.node_tree.nodes['Background'].inputs[0].default_value=(.18,.20,.24,1)
scene.world.node_tree.nodes['Background'].inputs[1].default_value=.35
scene.render.engine='CYCLES';scene.cycles.samples=24;scene.cycles.use_denoising=True
scene.render.resolution_x=900;scene.render.resolution_y=900;scene.render.resolution_percentage=100
scene.view_settings.view_transform='AgX'
for o in bpy.context.selected_objects:o.select_set(False)
rig.select_set(True);bpy.context.view_layer.objects.active=rig
# Default viewport faces the character.
for screen in bpy.data.screens:
 for area in screen.areas:
  if area.type=='VIEW_3D':
   area.spaces.active.region_3d.view_distance=3.8;area.spaces.active.region_3d.view_location=(0,0,.8)
   area.spaces.active.region_3d.view_rotation=cam.rotation_euler.to_quaternion();area.spaces.active.shading.type='MATERIAL'
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT,'Wild_Boar_Rigged.blend'))
for ob in variants:
 for other in variants:other.hide_render=other!=ob
 scene.render.filepath=os.path.join(OUT,ob.name+'_Preview.png');bpy.ops.render.render(write_still=True)
for other in variants:other.hide_render=other!=variants[0]
# Small animation contact sheet source frames, using the actual rig.
scene.render.resolution_x=400;scene.render.resolution_y=400;scene.cycles.samples=12
for name,(dur,loop) in clips.items():
 rig.animation_data.action=bpy.data.actions[name]
 for idx in range(6):
  scene.frame_set(1+round(idx*dur/6));scene.render.filepath=os.path.join(OUT,'frame_'+name+'_'+str(idx)+'.png');bpy.ops.render.render(write_still=True)
with open(os.path.join(OUT,'model_stats.json'),'w') as f:json.dump({'vertices':len(verts),'quads':len(faces),'triangles':len(faces)*2,'bones':len(arm.bones),'clips':clips},f,indent=2)
print('BOAR_BUILD_COMPLETE',flush=True)
