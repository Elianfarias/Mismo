import bpy, os
from mathutils import Vector
OUT=r'C:\Users\elian\Mismo\ArtSource\VoxelAdventurer'
bpy.ops.wm.open_mainfile(filepath=r'C:\Users\elian\Mismo\Docs\Art\VoxelBase\Humanoid_Voxel_Study_20260907_055328_rigged_animated_fixed.blend')
s=bpy.context.scene; arm=bpy.data.objects['Armature_Humanoid']; base=bpy.data.objects['Voxel_Base_GameReady']
for o in bpy.data.objects:
 if o.type=='MESH' and o!=base: o.hide_render=True; o.hide_set(True)
base.hide_render=False; base.hide_set(False)
col=bpy.data.collections.new('DETAILS | Faceless adventurer'); s.collection.children.link(col)
def mat(name,c):
 m=bpy.data.materials.new(name); m.diffuse_color=(*c,1); return m
navy=mat('Cloth | slate blue',(.09,.15,.23)); dark=mat('Face | shadow',(.008,.01,.022)); leather=mat('Leather | warm brown',(.24,.13,.075)); brass=mat('Trim | muted brass',(.60,.39,.15)); metal=mat('Armor | charcoal steel',(.16,.20,.24)); scarf=mat('Scarf | sand',(.43,.31,.21)); eyes=mat('Eyes | pale violet',(.72,.43,1))
base.data.materials.clear()
for m in [navy,dark,leather,metal,scarf]: base.data.materials.append(m)
for p in base.data.polygons:
 z=p.center.z
 p.material_index=1 if z>1.46 else 2 if z<.38 else 0
# Accessories are built in the existing rig rest coordinates and fully weighted to its bones.
def box(name,loc,dims,material,bone):
 bpy.ops.mesh.primitive_cube_add(size=1,location=loc); o=bpy.context.object; o.name=name; o.dimensions=dims
 bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
 for c in list(o.users_collection): c.objects.unlink(o)
 col.objects.link(o); o.data.materials.append(material)
 bpy.ops.object.transform_apply(location=True,rotation=False,scale=False)
 vg=o.vertex_groups.new(name=bone); vg.add(list(range(len(o.data.vertices))),1,'REPLACE')
 mod=o.modifiers.new('Follow original rig','ARMATURE'); mod.object=arm; o.parent=arm
 return o
# Hood frames the existing head; no replacement body, nose or mouth.
box('Hood crown',(0,.015,1.79),(.43,.37,.08),navy,'Head')
box('Hood upper step',(0,.03,1.845),(.33,.29,.03),navy,'Head')
box('Hood back',(0,.165,1.63),(.40,.07,.28),navy,'Head')
for x in [-.195,.195]: box('Hood side',(x,0,1.635),(.055,.35,.27),navy,'Head')
box('Face shadow',(0,-.177,1.635),(.335,.025,.225),dark,'Head')
for x in [-.085,.085]: box('Eye violet',(x,-.195,1.65),(.055,.02,.055),eyes,'Head')
box('Scarf upper',(0,-.008,1.48),(.385,.355,.07),scarf,'Head')
box('Scarf lower',(0,-.005,1.415),(.34,.32,.06),leather,'Neck')
box('Scarf hanging end',(.095,-.165,1.28),(.10,.035,.22),scarf,'Chest')
box('Chest tabard',(0,-.164,1.155),(.245,.03,.32),navy,'Chest')
for x in [-.13,.13]: box('Tabard trim',(x,-.185,1.15),(.025,.022,.29),brass,'Chest')
box('Chest clasp',(0,-.19,1.285),(.065,.035,.065),brass,'Chest')
box('Belt front',(0,-.163,.965),(.43,.04,.07),leather,'Hips')
box('Belt back',(0,.15,.965),(.43,.04,.07),leather,'Hips')
box('Buckle',(0,-.193,.965),(.085,.025,.085),brass,'Hips')
box('Buckle center',(0,-.209,.965),(.042,.012,.04),dark,'Hips')
box('Utility pouch',(-.235,-.08,.905),(.105,.16,.135),leather,'Hips')
box('Pouch flap',(-.235,-.168,.94),(.115,.025,.05),scarf,'Hips')
for side,sign in [('L',1),('R',-1)]:
 box('Shoulder plate '+side,(sign*.28,0,1.375),(.16,.27,.07),metal,'UpperArm.'+side)
 box('Shoulder edge '+side,(sign*.35,0,1.35),(.035,.28,.10),brass,'UpperArm.'+side)
 box('Bracer '+side,(sign*.565,0,1.30),(.17,.19,.19),metal,'LowerArm.'+side)
 for x in [.50,.63]: box('Bracer band '+side,(sign*x,0,1.30),(.025,.20,.20),leather,'LowerArm.'+side)
 box('Boot cuff '+side,(sign*.15,0,.32),(.19,.23,.055),leather,'LowerLeg.'+side)
 box('Boot strap '+side,(sign*.15,-.118,.22),(.16,.024,.035),brass,'LowerLeg.'+side)
# Preserve both source actions and rig; default to the existing idle.
arm.animation_data.action=bpy.data.actions['Idle']; s.frame_set(1); bpy.context.view_layer.update()
s.render.engine='BLENDER_WORKBENCH'; s.render.resolution_x=900; s.render.resolution_y=1000; s.render.resolution_percentage=100
sh=s.display.shading; sh.light='STUDIO'; sh.studiolight_rotate_z=.4; sh.color_type='MATERIAL'; sh.show_shadows=True; sh.show_cavity=True; sh.cavity_type='BOTH'; sh.background_type='WORLD'; s.world.color=(.045,.055,.075)
cam=bpy.data.objects['Camera']; s.camera=cam; cam.location=(2.7,-5.5,2.4); cam.rotation_euler=(Vector((0,0,.96))-cam.location).to_track_quat('-Z','Y').to_euler(); cam.data.type='ORTHO'; cam.data.ortho_scale=2.35
bpy.ops.object.select_all(action='DESELECT'); base.select_set(True); bpy.context.view_layer.objects.active=base
for screen in bpy.data.screens:
 for area in screen.areas:
  if area.type=='VIEW_3D':
   area.spaces.active.shading.color_type='MATERIAL'; area.spaces.active.region_3d.view_distance=3; area.spaces.active.region_3d.view_location=Vector((0,0,1))
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT,'Voxel_Base_Faceless_Detailed.blend'))
s.render.image_settings.file_format='PNG'; s.render.filepath=os.path.join(OUT,'Base_Detailed_Idle.png'); bpy.ops.render.render(write_still=True)
arm.animation_data.action=bpy.data.actions['Walk']; s.frame_set(9); s.render.filepath=os.path.join(OUT,'Base_Detailed_Walk.png'); bpy.ops.render.render(write_still=True)
print('VERIFIED',len(base.data.vertices),'base vertices',len(arm.data.bones),'bones', [a.name for a in bpy.data.actions],len(col.objects),'details')

