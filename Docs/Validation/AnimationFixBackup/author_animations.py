import bpy, math, os, json
from mathutils import Vector, Matrix, Quaternion, Euler
OUT=os.path.dirname(os.path.abspath(__file__))
bpy.ops.wm.open_mainfile(filepath=os.path.join(os.path.dirname(OUT),'Voxel_Adventurer_Sword_E.blend'))
s=bpy.context.scene;arm=bpy.data.objects['Armature_Humanoid'];base=bpy.data.objects['Voxel_Base_GameReady']; sword=bpy.data.objects['Sword_E_RightHand']
arm.animation_data_clear()
for a in list(bpy.data.actions):bpy.data.actions.remove(a)
P=arm.pose.bones; R=arm.data.bones
for p in P:p.rotation_mode='QUATERNION'
def reset():
 for p in P:p.matrix_basis=Matrix.Identity(4)
def rot(name,x=0,y=0,z=0):
 P[name].rotation_quaternion=Euler(tuple(math.radians(v) for v in (x,y,z)),'XYZ').to_quaternion()
def update():bpy.context.view_layer.update()
def aim(name, direction):
 p=P[name];head=p.head.copy(); b=R[name];q=(b.tail_local-b.head_local).rotation_difference(Vector(direction).normalized())@b.matrix_local.to_quaternion()
 p.matrix=Matrix.Translation(head)@q.to_matrix().to_4x4();update()
def ik(upper,lower,target,pole):
 update();h=P[upper].head.copy();t=Vector(target);a=R[upper].length;b=R[lower].length;v=t-h;d=max(.001,min(v.length,a+b-.003));v.normalize();t=h+v*d
 along=(a*a-b*b+d*d)/(2*d);height=math.sqrt(max(.00001,a*a-along*along))
 bend=Vector(pole);bend=(bend-v*bend.dot(v)).normalized();k=h+v*along+bend*height
 aim(upper,k-h);aim(lower,t-k)
def foot(side,target,pitch=0):
 ik('UpperLeg.'+side,'LowerLeg.'+side,target,(0,-1,0))
 p=P['Foot.'+side];q=Quaternion((1,0,0),math.radians(pitch))@R['Foot.'+side].matrix_local.to_quaternion();p.matrix=Matrix.Translation(p.head)@q.to_matrix().to_4x4();update()
def hands(right=(-.30,-.10,.84),left=(.30,-.02,.82)):
 for side,target,sign in [('R',right,-1),('L',left,1)]:
  ik('UpperArm.'+side,'LowerArm.'+side,target,(sign,-.1,-.1))
  aim('Hand.'+side,P['LowerArm.'+side].tail-P['LowerArm.'+side].head)
def body(bob=-.025,lean=0,twist=0,sway=0):
 reset();P['Hips'].location=(sway,0,bob);rot('Spine',lean*.40,0,twist*.35);rot('Chest',lean*.60,0,twist*.65);rot('Head',-lean*.35,0,-twist*.6);update()
def idle(t):
 wave=math.sin(t*math.tau);body(-.028+.006*wave,1,1.4*wave,.003*wave)
 foot('L',(.15,0,.17));foot('R',(-.15,-.025,.17));hands(right=(-.31,-.075,.85+.003*wave))
def gait(t,run=False):
 wave=math.sin(t*math.tau);bob=(-.055+.018*math.cos(t*4*math.pi)) if not run else (-.095+.04*math.cos(t*4*math.pi))
 body(bob,6 if not run else 15,(-4 if not run else -8)*wave,.009*wave)
 for side,sign in [('L',1),('R',-1)]:
  u=(t+(0 if sign==1 else .5))%1
  # Stance occupies 60% of the walk; swing clears the ground with a bent knee.
  stance=.60 if not run else .40;stride=.38 if not run else .56
  if u<stance: y=-stride/2+stride*u/stance;z=.17;pitch=0
  else:
   v=(u-stance)/(1-stance);y=stride/2-stride*v;z=.17+(.115 if not run else .22)*math.sin(v*math.pi);pitch=12*math.sin(v*math.tau)
  foot(side,(sign*.15,y,z),pitch)
 # Weapon arm swings less, free arm counterbalances the legs.
 hands(right=(-.32,-.07+(.07 if not run else .15)*wave,.85+(.035 if not run else .12)),left=(.32,-.03-(.12 if not run else .25)*wave,.84+(.00 if not run else .16)))
def jump(t):
 body(-.08+.045*t,10-3*t)
 foot('L',(.16,-.08,.23+.13*t),-12*t);foot('R',(-.16,.075,.21+.09*t),-10*t)
 hands(right=(-.37,-.12,1.0+.07*t),left=(.38,-.08,1.03+.06*t))
def fall(t):
 body(-.025,4)
 foot('L',(.17,-.03,.205),-5);foot('R',(-.17,.035,.22),-6)
 hands(right=(-.38,-.06,.98),left=(.38,-.04,1.0))
def land(t):
 w=math.sin(math.pi*t);body(-.025-.13*w,14*w)
 foot('L',(.16,0,.17));foot('R',(-.16,-.025,.17));hands(right=(-.33,-.12,.85-.03*w),left=(.33,-.10,.88-.03*w))
def lerp(a,b,t):return tuple(x+(y-x)*t for x,y in zip(a,b))
def attack(t,kind=1):
 # Anticipation, fast contact sweep, then recovery; contact times match existing hitbox windows.
 start=.16 if kind<3 else .23;end=.53 if kind<3 else .61
 if kind==1: wind=(-.46,.12,1.26);finish=(.18,-.48,1.03);tw0=-26;tw1=28
 elif kind==2:wind=(.12,-.40,1.25);finish=(-.50,-.12,1.03);tw0=25;tw1=-28
 else:wind=(-.18,.02,1.64);finish=(-.22,-.51,.97);tw0=-8;tw1=9
 home=(-.31,-.075,.85)
 if t<start: u=t/start;right=lerp(home,wind,u);tw=tw0*u
 elif t<end:u=(t-start)/(end-start);right=lerp(wind,finish,u);tw=tw0+(tw1-tw0)*u
 else:u=(t-end)/(1-end);right=lerp(finish,home,u*u*(3-2*u));tw=tw1*(1-u)
 body(-.05,5+10*math.sin(t*math.pi),tw)
 foot('L',(.18,.08,.17));foot('R',(-.18,-.13,.17));hands(right=right,left=(.31,.035,1.02))
def dash(t):
 body(-.11,23);foot('L',(.16,-.19,.22),10);foot('R',(-.16,.21,.20),-15);hands(right=(-.32,.15,.94),left=(.32,.15,1.02))
def parry(t):
 body(-.07,6,-9);foot('L',(.18,.08,.17));foot('R',(-.18,-.13,.17));hands(right=(-.18,-.36,1.15),left=(.20,-.30,1.12))
def lunge(t):
 body(-.08,18,-15);foot('L',(.16,.20,.19),-10);foot('R',(-.16,-.27,.17));hands(right=(-.22,-.46,1.14),left=(.33,.12,1.03))
def spin(t):
 body(-.06,4,0);P['Root'].rotation_quaternion=Quaternion((0,0,1),-math.tau*t);update()
 # Local rotations maintain an extended attacking arm through the full turn.
 rot('UpperArm.R',-25,0,0);rot('LowerArm.R',10,0,0);rot('UpperArm.L',-48,0,0);rot('LowerArm.L',35,0,0)
 rot('UpperLeg.L',-8,0,0);rot('LowerLeg.L',18,0,0);rot('UpperLeg.R',8,0,0);rot('LowerLeg.R',14,0,0)
# Bake dense keys so the exact authored poses survive FBX interpolation.
specs=[('Idle',2.4,idle),('Walk',.88,lambda t:gait(t)),('Run',.62,lambda t:gait(t,True)),('Jump',.30,jump),('Fall',.40,fall),('Land',.18,land),('Attack1',.38,lambda t:attack(t,1)),('Attack2',.38,lambda t:attack(t,2)),('Attack3',.52,lambda t:attack(t,3)),('Dash',.24,dash),('Parry',.16,parry),('Lunge',.24,lunge),('Spin',.55,spin)]
s.render.fps=60
for name,duration,pose in specs:
 act=bpy.data.actions.new(name);act.use_fake_user=True;arm.animation_data_create();arm.animation_data.action=act
 frames=round(duration*60)
 for f in range(frames+1):
  pose(f/frames)
  for p in P:
   p.keyframe_insert(data_path='rotation_euler' if p.rotation_mode=='XYZ' else 'rotation_quaternion',frame=f+1,group=p.name)
   p.keyframe_insert(data_path='location',frame=f+1,group=p.name)
 print('AUTHORED',name,frames+1,flush=True)
arm.animation_data.action=bpy.data.actions['Idle'];s.frame_set(1);update()
# Rebind the existing sword into the relaxed right-hand grip of the new Idle.
bpy.ops.import_scene.fbx(filepath=r'C:\Users\elian\Mismo\Assets\Art\FBX\Weapons\fbx(unity)\sword_E.fbx')
source=next(o for o in bpy.context.selected_objects if o.type=='MESH');verts=[source.matrix_world@v.co for v in source.data.vertices]
assert len(verts)==len(sword.data.vertices)
group=base.vertex_groups['Hand.R'].index;points=[v.co for v in base.data.vertices if any(g.group==group and g.weight>.5 for g in v.groups)];rest_grip=sum(points,Vector())/len(points)
arm.animation_data.action=bpy.data.actions['Idle'];s.frame_set(1);update();deform=P['Hand.R'].matrix@R['Hand.R'].matrix_local.inverted();grip=deform@rest_grip
scale=.92/(max(v.z for v in verts)-min(v.z for v in verts));rotation=Vector((-.10,-.73,-.68)).normalized().to_track_quat('Z','Y').to_matrix()@Matrix.Rotation(math.radians(60),3,'Z')
for v,co in zip(sword.data.vertices,verts):v.co=deform.inverted()@(grip+rotation@(co*scale))
bpy.data.objects.remove(source,do_unlink=True)
# Preserve all original scene/model parts; export only the actual character.
objects=[arm,base,sword]+list(bpy.data.collections['DETAILS | Faceless adventurer'].objects)
bpy.ops.object.select_all(action='DESELECT')
for o in objects:o.hide_set(False);o.select_set(True)
bpy.context.view_layer.objects.active=arm;s.frame_start=1;s.frame_end=145
for screen in bpy.data.screens:
 for a in screen.areas:
  if a.type=='VIEW_3D':a.spaces.active.shading.color_type='MATERIAL'
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT,'Voxel_Adventurer_Animated.blend'))
fbx=r'C:\Users\elian\Mismo\Assets\Art\FBX\Voxel_Adventurer_Animated.fbx'
bpy.ops.export_scene.fbx(filepath=fbx,use_selection=True,object_types={'ARMATURE','MESH'},add_leaf_bones=False,bake_anim=True,bake_anim_use_all_actions=True,bake_anim_use_nla_strips=False,bake_anim_simplify_factor=0,path_mode='COPY',embed_textures=True,axis_forward='-Z',axis_up='Y')
json.dump({n:d for n,d,_ in specs},open(os.path.join(OUT,'clips.json'),'w'),indent=2)
print('ANIMATION_ASSETS_READY',flush=True)
# Lightweight actual motion previews, 24 frames per action.
s.render.engine='BLENDER_WORKBENCH';s.display.shading.color_type='MATERIAL';s.display.shading.light='STUDIO';s.display.shading.show_shadows=True;s.display.shading.show_cavity=True
s.render.resolution_x=480;s.render.resolution_y=520;s.render.resolution_percentage=100
s.camera.location=(-3.8,-5.8,2.6);s.camera.rotation_euler=(Vector((0,-.1,.95))-s.camera.location).to_track_quat('-Z','Y').to_euler();s.camera.data.ortho_scale=2.5
for name,duration,_ in specs[:9]:
 folder=os.path.join(OUT,'preview',name);os.makedirs(folder,exist_ok=True);arm.animation_data.action=bpy.data.actions[name]
 for i in range(24):
  s.frame_set(1+round(i/23*round(duration*60)));s.render.filepath=os.path.join(folder,f'{i:02}.png');bpy.ops.render.render(write_still=True)
print('PREVIEWS_READY',flush=True)
