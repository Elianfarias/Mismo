import bpy, math, random, os, json
from mathutils import Vector
from math import sin, cos, pi
OUT=os.path.dirname(os.path.abspath(__file__))
bpy.ops.wm.read_factory_settings(use_empty=True)
S=.04
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
# Forest spider, eight legs, meter scale; faces -Y.
ell((0,.26,.665),(.345,.405,.355),'shell','Abdomen',True)
ell((0,-.155,.485),(.255,.28,.21),'shell','Body',True)
box((0,-.455,.465),(.395,.28,.255),'shell','Head')
ell((0,-.42,.53),(.25,.23,.16),'shell','Head')
# Chunky layered abdomen with a symmetrical branching crimson marking.
for key,(role,bn) in list(cells.items()):
 i,j,k=key;x,y,z=[(v+.5)*S for v in key]
 if bn=='Abdomen':
  # Patchy shell tone on the lower cap.
  if z<.57 or (i*7+j*3+k)%13==0:cells[key]=('dark',bn)
  spine=abs(x)<.05 and -.045<y<.59 and z>.88
  branch=False
  for row in [.03,.24,.45]:
   branch |= abs(y-(row+abs(x)*.52))<.038 and .04<abs(x)<.285 and z>.69
  frontal=abs(y+.095)<.045 and abs(x)<.18 and .70<z<.81
  if spine or branch or frontal:cells[key]=('mark',bn)
# Face: heavy brow, two large luminous-looking eyes and two small upper eyes.
for sign in [-1,1]:
 box((sign*.15,-.577,.585),(.095,.055,.065),'mark','Head')
 box((sign*.135,-.615,.505),(.13,.065,.12),'dark','Head')
 box((sign*.135,-.65,.51),(.082,.039,.075),'eye','Head')
 # One voxel glint in the red eye; asymmetric local indices mirrored exactly.
 i=3 if sign==1 else -4
 cells[i,-18,13]=('glint','Head')
 # Jointed ivory chelicerae with narrowing tips.
 tag='L' if sign>0 else 'R'
 box((sign*.14,-.626,.38),(.12,.105,.115),'fang','Fang_'+tag)
 box((sign*.145,-.661,.305),(.09,.09,.105),'fang','Fang_'+tag)
 box((sign*.122,-.678,.248),(.055,.067,.067),'fang','Fang_'+tag)
 box((sign*.058,-.63,.34),(.045,.065,.10),'dark','Palp_'+tag)
 box((sign*.052,-.647,.285),(.037,.055,.04),'fang','Palp_'+tag)
# Each leg has a raised knee and two distal sections; eight distinct ground contacts.
legs={}
layout=[(-.31,.48,-.57,.65,.70,-.90),(-.17,.64,-.27,.65,.91,-.43),(-.025,.67,.15,.66,.94,.27),(.12,.51,.48,.65,.72,.80)]
for sign in [-1,1]:
 for idx,(hy,kx,ky,kz,tx,ty) in enumerate(layout):
  tag=('L' if sign>0 else 'R')+str(idx+1)
  h=(sign*.19,hy,.48);k=(sign*kx,ky,kz);a=(sign*(tx-.018),ty*.985,.095);toe=(sign*tx,ty,.012)
  legs[tag]=(h,k,a,toe,sign,idx)
  # Thick upper and tapering lower legs, voxelized along their axes.
  tube(h,k,.056,'dark','Upper_'+tag)
  ell(k,(.068,.068,.068),'dark','Lower_'+tag)
  n=24
  for q in range(n+1):
   t=q/n;p=Vector(k).lerp(Vector(a),t)
   role='leg' if .62<t<.79 else ('mark' if .10<t<.23 or t>.90 else 'dark')
   ell(p,(.049-.014*t,)*3,role,'Lower_'+tag)
  tube(a,toe,.029,'dark','Foot_'+tag)
# Skeleton.
arm=bpy.data.armatures.new('Spider_Skeleton');rig=bpy.data.objects.new('Spider_Rig',arm);bpy.context.collection.objects.link(rig)
bpy.context.view_layer.objects.active=rig;rig.select_set(True);bpy.ops.object.mode_set(mode='EDIT')
def bone(n,h,t,parent=None,deform=True):
 b=arm.edit_bones.new(n);b.head=h;b.tail=t;b.use_deform=deform
 if parent:b.parent=arm.edit_bones[parent]
 return b
bone('Root',(0,0,0),(0,0,.15))
bone('Body',(0,.10,.48),(0,-.25,.48),'Root')
bone('Abdomen',(0,.075,.56),(0,.50,.70),'Body')
bone('Head',(0,-.26,.485),(0,-.54,.475),'Body')
for sign in [-1,1]:
 tag='L' if sign>0 else 'R'
 bone('Fang_'+tag,(sign*.14,-.60,.41),(sign*.125,-.67,.25),'Head')
 bone('Palp_'+tag,(sign*.058,-.59,.39),(sign*.052,-.65,.28),'Head')
bone('Web_Origin',(0,-.66,.395),(0,-.80,.395),'Head')
for tag,(h,k,a,toe,sign,idx) in legs.items():
 bone('Upper_'+tag,h,k,'Body');bone('Lower_'+tag,k,a,'Upper_'+tag)
 bone('Foot_'+tag,a,toe,'Lower_'+tag)
 bone('IK_'+tag,a,toe,'Root',False)
bpy.ops.object.mode_set(mode='POSE')
for tag in legs:
 c=rig.pose.bones['Lower_'+tag].constraints.new('IK');c.target=rig;c.subtarget='IK_'+tag;c.chain_count=2;c.use_stretch=False
 c=rig.pose.bones['Foot_'+tag].constraints.new('COPY_ROTATION');c.target=rig;c.subtarget='IK_'+tag;c.target_space='WORLD';c.owner_space='WORLD'
for p in rig.pose.bones:p.rotation_mode='XYZ'
bpy.ops.object.mode_set(mode='OBJECT');rig.show_in_front=True;arm.display_type='OCTAHEDRAL'
roles=['shell','dark','mark','leg','fang','eye','glint']
palettes={
'Standard':['382b29','211c1d','ad262b','704931','e8d3ad','e12a2c','ffe3bb'],
'Forest_Moss':['343529','20251c','78842f','59543b','d3cca3','baca40','f6f6bc'],
'Dark_Cave':['2c2832','191820','82718f','423a4b','bfb8c8','aabdeb','eef3ff'],
'Albino':['d5cbbf','847977','c79095','b49e94','f4ead5','ce3045','ffdddd']}
faces_dir=[((1,0,0),[(1,0,0),(1,1,0),(1,1,1),(1,0,1)]),((-1,0,0),[(0,0,0),(0,0,1),(0,1,1),(0,1,0)]),((0,1,0),[(0,1,0),(0,1,1),(1,1,1),(1,1,0)]),((0,-1,0),[(0,0,0),(1,0,0),(1,0,1),(0,0,1)]),((0,0,1),[(0,0,1),(1,0,1),(1,1,1),(0,1,1)]),((0,0,-1),[(0,0,0),(0,1,0),(1,1,0),(1,0,0)])]
verts=[];faces=[];faceinfo=[];weights={};vid={}
for (i,j,k),(role,bn) in cells.items():
 shade=random.Random(i*11737+j*2731+k*673).randrange(5)
 moss=False
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
mesh=bpy.data.meshes.new('Spider_Voxel_Surface');mesh.from_pydata(verts,[],faces);mesh.update()
variants=[]
for name,colors in palettes.items():
 me=mesh.copy();ob=bpy.data.objects.new('Spider_'+name,me);bpy.context.collection.objects.link(ob);variants.append(ob)
 for bn,indices in weights.items():ob.vertex_groups.new(name=bn).add(indices,1.0,'REPLACE')
 ob.parent=rig;mod=ob.modifiers.new('Spider Skin','ARMATURE');mod.object=rig
 img=bpy.data.images.new(name+'_Palette',width=35,height=1,alpha=True)
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
  u=(roles.index(role)*5+shade+.5)/35
  for li in poly.loop_indices:uv.data[li].uv=(u,.5)
 ob.hide_render=name!='Standard';ob.hide_set(name!='Standard')
scene=bpy.context.scene;scene.render.fps=30;scene.unit_settings.system='METRIC'
rig.animation_data_create()
clips={'Idle':(60,True),'Walk':(36,True),'Run':(24,True),'Jump':(42,False),'Attack_Bite':(30,False),'Spit_Web':(36,False)}
def worldmove(p,xyz):p.location=p.bone.matrix_local.to_3x3().inverted()@Vector(xyz)
def smooth(a,b,x):
 t=max(0,min(1,(x-a)/(b-a)));return t*t*(3-2*t)
for name,(duration,loop) in clips.items():
 action=bpy.data.actions.new(name);action.use_fake_user=True;rig.animation_data.action=action
 for f in range(duration+1):
  t=f/duration;w=2*pi*t
  for p in rig.pose.bones:p.location=(0,0,0);p.rotation_euler=(0,0,0);p.scale=(1,1,1)
  body=rig.pose.bones['Body'];head=rig.pose.bones['Head'];abdomen=rig.pose.bones['Abdomen']
  z=.006*sin(w);dy=0;jump=0;fold=0;strike=0
  abdomen.rotation_euler.x=.012*sin(w)
  if name in ['Walk','Run']:
   z=(.006 if name=='Walk' else .012)*sin(2*w)
   body.rotation_euler.y=.012*sin(w)
  elif name=='Jump':
   crouch=smooth(0,.20,t)*(1-smooth(.20,.29,t))
   jump=.29*sin(pi*(t-.24)/.49) if .24<t<.73 else 0
   landing=smooth(.69,.76,t)*(1-smooth(.76,.92,t))
   z=jump-.072*crouch-.045*landing
   fold=sin(pi*(t-.24)/.49) if .24<t<.73 else 0
   head.rotation_euler.x=-.065*fold;abdomen.rotation_euler.x=.08*fold
  elif name=='Attack_Bite':
   wind=smooth(0,.27,t)*(1-smooth(.27,.39,t))
   strike=smooth(.27,.43,t)*(1-smooth(.50,.80,t))
   z=.035*wind-.015*strike;dy=.025*wind-.095*strike
   head.rotation_euler.x=-.07*wind+.11*strike
   for sign in [-1,1]:
    p=rig.pose.bones['Fang_'+('L' if sign>0 else 'R')]
    p.rotation_euler.y=sign*(.22*wind-.18*strike)
  elif name=='Spit_Web':
   wind=smooth(0,.25,t)*(1-smooth(.25,.45,t))
   spit=smooth(.30,.42,t)*(1-smooth(.47,.72,t))
   z=.014*wind;dy=.025*wind+.03*spit
   head.rotation_euler.x=-.09*wind+.07*spit
   abdomen.rotation_euler.x=.07*wind-.04*spit
   for sign in [-1,1]:
    rig.pose.bones['Fang_'+('L' if sign>0 else 'R')].rotation_euler.y=sign*.14*spit
  worldmove(body,(0,dy,z))
  for tag,(h,k,a,toe,sign,idx) in legs.items():
   target=rig.pose.bones['IK_'+tag]
   move=(0,0,0)
   if name in ['Walk','Run']:
    # Alternating tetrapod gait: four support contacts at a time.
    phase=(idx%2)*.5+(0 if sign==1 else .5)
    u=(t+phase)%1;stance=.62 if name=='Walk' else .54
    stride=.15 if name=='Walk' else .24;lift=.055 if name=='Walk' else .085
    if u<stance:fy=-stride/2+stride*u/stance;fz=0
    else:v=(u-stance)/(1-stance);fy=stride/2-stride*(v*v*(3-2*v));fz=lift*sin(pi*v)
    move=(0,fy,fz)
   elif name=='Jump':move=(-sign*.095*fold,-a[1]*.065*fold,jump+.065*fold)
   elif name=='Attack_Bite' and idx==0:move=(0,-.045*strike,.05*sin(pi*t)**2)
   worldmove(target,move)
  for sign in [-1,1]:
   rig.pose.bones['Palp_'+('L' if sign>0 else 'R')].rotation_euler.x=.05*sin(w+sign*.25)
  for p in rig.pose.bones:
   p.keyframe_insert('location',frame=f+1,group=p.name);p.keyframe_insert('rotation_euler',frame=f+1,group=p.name)
 for fc in action.fcurves:
  for kp in fc.keyframe_points:kp.interpolation='LINEAR'
 action['loop']=loop;action['description']='30 FPS, stationary Root. '+('Loop.' if loop else 'One shot.')
 if name=='Spit_Web':action.pose_markers.new('Emit_Web').frame=14
 if name=='Attack_Bite':action.pose_markers.new('Bite_Impact').frame=14
 if name=='Jump':
  action.pose_markers.new('Takeoff').frame=11;action.pose_markers.new('Land').frame=32
rig.animation_data.action=bpy.data.actions['Idle'];scene.frame_start=1;scene.frame_end=61;scene.frame_set(1)
# Each FBX includes exactly one color variant and all six baked skeletal clips.
for ob in variants:
 for o in bpy.context.selected_objects:o.select_set(False)
 ob.hide_set(False);ob.select_set(True);rig.select_set(True);bpy.context.view_layer.objects.active=rig
 bpy.ops.export_scene.fbx(filepath=os.path.join(OUT,ob.name+'.fbx'),use_selection=True,object_types={'ARMATURE','MESH'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,add_leaf_bones=False,use_armature_deform_only=True,bake_anim=True,bake_anim_use_all_actions=True,bake_anim_use_nla_strips=False,bake_anim_force_startend_keying=True,bake_anim_step=1,bake_anim_simplify_factor=0,path_mode='COPY',embed_textures=True)
 ob.select_set(False);ob.hide_set(ob!=variants[0])
rig.animation_data.action=bpy.data.actions['Idle'];scene.frame_set(1)
# Reusable render studio; excluded from the FBX.
def track(ob,p):ob.rotation_euler=(Vector(p)-ob.location).to_track_quat('-Z','Y').to_euler()
bpy.ops.object.camera_add(location=(2.8,-4.4,2.30));cam=bpy.context.object;track(cam,(0,-.03,.49));cam.data.type='ORTHO';cam.data.ortho_scale=2.75;scene.camera=cam
for name,loc,power,size in [('Key',(-1,-3,4),400,3),('Fill',(3,-1,2),150,3),('Rim',(0,3,3),600,2.5)]:
 bpy.ops.object.light_add(type='AREA',location=loc);o=bpy.context.object;o.name=name;o.data.energy=power;o.data.shape='DISK';o.data.size=size;track(o,(0,0,.5))
bpy.ops.mesh.primitive_plane_add(size=200);floor=bpy.context.object;floor.name='Studio_Floor'
mat=bpy.data.materials.new('Backdrop');mat.diffuse_color=(.12,.14,.15,1);floor.data.materials.append(mat)
scene.world=bpy.data.worlds.new('Studio_World');scene.world.use_nodes=True;scene.world.node_tree.nodes['Background'].inputs[0].default_value=(.18,.20,.24,1);scene.world.node_tree.nodes['Background'].inputs[1].default_value=.25
scene.render.engine='CYCLES';scene.cycles.samples=24;scene.cycles.use_denoising=True
scene.render.resolution_x=900;scene.render.resolution_y=900;scene.render.resolution_percentage=100;scene.view_settings.view_transform='AgX'
for o in bpy.context.selected_objects:o.select_set(False)
rig.select_set(True);bpy.context.view_layer.objects.active=rig
for screen in bpy.data.screens:
 for area in screen.areas:
  if area.type=='VIEW_3D':
   area.spaces.active.region_3d.view_distance=3.2;area.spaces.active.region_3d.view_location=(0,0,.5);area.spaces.active.region_3d.view_rotation=cam.rotation_euler.to_quaternion();area.spaces.active.shading.type='MATERIAL'
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT,'Forest_Spider_Rigged.blend'))
for ob in variants:
 for other in variants:other.hide_render=other!=ob
 scene.render.filepath=os.path.join(OUT,ob.name+'_Preview.png');bpy.ops.render.render(write_still=True)
for other in variants:other.hide_render=other!=variants[0]
scene.render.resolution_x=420;scene.render.resolution_y=420;scene.cycles.samples=12
for name,(dur,loop) in clips.items():
 rig.animation_data.action=bpy.data.actions[name]
 for idx in range(12):
  scene.frame_set(1+round(idx*dur/12));scene.render.filepath=os.path.join(OUT,'frame_'+name+'_'+str(idx)+'.png');bpy.ops.render.render(write_still=True)
with open(os.path.join(OUT,'model_stats.json'),'w') as f:json.dump({'vertices':len(verts),'quads':len(faces),'triangles':len(faces)*2,'bones_blender':len(arm.bones),'clips':clips,'height_m':max(v[2] for v in verts)-min(v[2] for v in verts),'leg_count':8},f,indent=2)
print('SPIDER_BUILD_COMPLETE',flush=True)
