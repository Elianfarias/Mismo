"""Dress the existing animated adventurer, preserving object/mesh names and rig."""
import bpy, math, os, json, shutil
from mathutils import Vector
ROOT = r'C:\Users\elian\Mismo'
OUT = os.path.join(ROOT, 'ArtSource/VoxelAdventurer/ReferenceMage')
os.makedirs(OUT, exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=os.path.join(ROOT, 'ArtSource/VoxelAdventurer/AnimationV2/Voxel_Adventurer_Animated.blend'))
s = bpy.context.scene
arm = bpy.data.objects['Armature_Humanoid']
base = bpy.data.objects['Voxel_Base_GameReady']
objects = [arm, base, bpy.data.objects['Sword_E_RightHand']] + list(bpy.data.collections['DETAILS | Faceless adventurer'].objects)
original = {o.name: o.data.name for o in objects if o.type == 'MESH'}
palette = {'Cloth': (.065,.075,.17), 'Leather': (.25,.12,.042), 'Trim': (.8,.43,.075), 'Armor': (.26,.29,.32), 'Scarf': (.36,.22,.105), 'Eyes': (.75,.25,1)}
for m in bpy.data.materials:
 for prefix, color in palette.items():
  if m.name.startswith(prefix):
   m.diffuse_color = (*color,1)
   if m.use_nodes:
    for n in m.node_tree.nodes:
     if n.type == 'BSDF_PRINCIPLED':
      n.inputs['Base Color'].default_value = (*color,1)
      if prefix == 'Eyes':
       n.inputs['Emission Color'].default_value = (*color,1)
       n.inputs['Emission Strength'].default_value = 3

def cube(v, f, center, size):
 i = len(v); x,y,z=center; a,b,c=[d/2 for d in size]
 v.extend([(x-a,y-b,z-c),(x+a,y-b,z-c),(x+a,y+b,z-c),(x-a,y+b,z-c),(x-a,y-b,z+c),(x+a,y-b,z+c),(x+a,y+b,z+c),(x-a,y+b,z+c)])
 f.extend([tuple(i+j for j in q) for q in [(0,3,2,1),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)]])

def replace(name, boxes, bone, material=None):
 o=bpy.data.objects[name]; v=[]; f=[]
 for c,d in boxes: cube(v,f,c,d)
 o.data.clear_geometry(); o.data.from_pydata(v,[],f); o.data.update()
 o.vertex_groups.clear(); g=o.vertex_groups.new(name=bone); g.add(list(range(len(v))),1,'REPLACE')
 if material:
  o.data.materials.clear(); o.data.materials.append(next(m for m in bpy.data.materials if m.name.startswith(material)))

# Stepped elliptical wide brim and bent, tapering crown, each exposed voxel separate.
hat=[]; step=.035
for iz in range(22):
 z=1.82+iz*step
 radius=.245*(1-iz/25)
 cx=max(0,iz-12)*.023
 for ix in range(-10,17):
  for iy in range(-9,10):
   x=ix*step; y=iy*step+.025
   if ((x-cx)/radius)**2+((y-.025)/(radius*.86))**2<=1:
    hat.append(((x,y,z),(step,step,step)))
for iz,rx,ry in [(0,.49,.37),(1,.455,.34),(2,.37,.28)]:
 for ix in range(-14,15):
  for iy in range(-11,12):
   x=ix*step;y=iy*step
   if (x/rx)**2+(y/ry)**2<=1:
    hat.append(((x,y,1.785+iz*.028),(step,step,.028)))
# Drooping tip.
for i in range(5): hat.append(((.23+i*.027,.025,2.53-i*.024),(.06,.065,.06)))
replace('Hood crown',hat,'Head')
band=[]
for ix in range(-7,8):
 for iy in range(-6,7):
  x=ix*step;y=iy*step+.025;r=(x/.25)**2+((y-.025)/.215)**2
  if .67<r<1.1: band.append(((x,y,1.88),(step,step,.085)))
replace('Hood back',band,'Head','Leather')
badge=[]
for i in range(6):
 for sign in [-1,1]:badge.append(((sign*i*.016,-.215,1.99-i*.025),(.026,.035,.03)))
for i in range(-5,6):badge.append(((i*.016,-.218,1.855),(.026,.035,.027)))
replace('Hood upper step',badge,'Head','Trim')
for name in ['Eye violet','Eye violet.001']:
 o=bpy.data.objects[name]; x=sum(v.co.x for v in o.data.vertices)/len(o.data.vertices)
 replace(name,[((x,-.201,1.65),(.064,.026,.034)),((x,-.202,1.65),(.034,.027,.068))],'Head')
replace('Chest tabard',[((0,-.185,1.16),(.27,.045,.37))],'Chest')
replace('Scarf hanging end',[((.12,-.178,1.34),(.085,.035,.18))],'Chest')
clasp=[]
for i in range(4):
 for sign in [-1,1]:clasp.append(((sign*i*.015,-.222,1.32-i*.022),(.024,.033,.027)))
for i in range(-3,4):clasp.append(((i*.015,-.222,1.251),(.022,.033,.022)))
replace('Chest clasp',clasp,'Chest')
for side,sign in [('L',1),('R',-1)]:
 plates=[]
 for row in range(3):
  for col in range(5):plates.append(((sign*(.275+row*.046), (col-2)*.048, 1.415-row*.036),(.08,.047,.045)))
 replace('Shoulder plate '+side,plates,'UpperArm.'+side)
 replace('Shoulder edge '+side,[((sign*.397,0,1.31),(.05,.28,.055))],'UpperArm.'+side,'Armor')
 # Articulated robe halves are weighted to the original thigh bones.
 # Append to the existing body mesh, retaining its names/material slots and all original skin weights.
v=[tuple(p.co) for p in base.data.vertices];f=[tuple(p.vertices) for p in base.data.polygons]
mi=[p.material_index for p in base.data.polygons]
group_names=[g.name for g in base.vertex_groups]
weights=[[(base.vertex_groups[g.group].name,g.weight) for g in p.groups] for p in base.data.vertices]
for side,sign in [('L',1),('R',-1)]:
 for row in range(13):
  z=.94-row*.039; rx=.135+row*.003; ry=.155+row*.003
  for ix in range(-6,7):
   for iy in range(-7,8):
    dx=ix*.03;dy=iy*.03;r=(dx/rx)**2+(dy/ry)**2
    if r<1.12:
     cube(v,f,(sign*.15+dx,dy,z),(.03,.03,.039))
     mi.extend([2 if row==12 or (abs(ix)==3 and iy<0) else 0]*6)
     weights.extend([[(f'UpperLeg.{side}',1)]]*8)
base.data.clear_geometry();base.data.from_pydata(v,[],f);base.data.update()
base.vertex_groups.clear()
for name in group_names:base.vertex_groups.new(name=name)
for p,m in zip(base.data.polygons,mi):p.material_index=m
for i,groups in enumerate(weights):
 for name,w in groups:
  g=base.vertex_groups.get(name) or base.vertex_groups.new(name=name);g.add([i],w,'REPLACE')
arm.animation_data.action=bpy.data.actions['Idle'];s.frame_set(1)
bpy.ops.object.select_all(action='DESELECT')
for o in objects:o.hide_set(False);o.hide_render=False;o.select_set(True)
bpy.context.view_layer.objects.active=arm
assert all(bpy.data.objects[n].data.name==data for n,data in original.items())
s.render.fps=60;s.render.fps_base=1
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT,'Voxel_Reference_Mage.blend'))
target=os.path.join(ROOT,'Assets/Art/FBX/Characters/Voxel_Adventurer_Animated.fbx')
backup=os.path.join(OUT,'Before_Mage.fbx')
if not os.path.exists(backup):shutil.copy2(target,backup)
bpy.ops.export_scene.fbx(filepath=target,use_selection=True,object_types={'ARMATURE','MESH'},add_leaf_bones=False,bake_anim=True,bake_anim_use_all_actions=True,bake_anim_use_nla_strips=False,bake_anim_simplify_factor=0,path_mode='COPY',embed_textures=True,axis_forward='-Z',axis_up='Y')
s.render.engine='BLENDER_WORKBENCH';s.display.shading.color_type='MATERIAL';s.display.shading.light='STUDIO';s.display.shading.show_shadows=True;s.display.shading.show_cavity=True
s.render.resolution_x=850;s.render.resolution_y=1050;s.render.resolution_percentage=100
s.camera.location=(3,-6,2.8);s.camera.rotation_euler=(Vector((0,0,1.27))-s.camera.location).to_track_quat('-Z','Y').to_euler();s.camera.data.ortho_scale=3.05
for action,frame in [('Idle',1),('Run',9),('Attack1',12)]:
 arm.animation_data.action=bpy.data.actions[action];s.frame_set(frame);s.render.filepath=os.path.join(OUT,action+'.png');bpy.ops.render.render(write_still=True)
json.dump({'meshes':original,'bones':[b.name for b in arm.data.bones],'actions':[a.name for a in bpy.data.actions]},open(os.path.join(OUT,'manifest.json'),'w'),indent=2)
print('MAGE_EXPORT_OK',flush=True)

