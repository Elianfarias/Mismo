import bpy,json
from pathlib import Path
out=Path(r'C:\Users\elian\Mismo\Assets\Art\FBX\Goblins');out.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=r'C:\Users\elian\Mismo\ArtSource\GoblinConcept\Goblin_Concept_Rigged.blend')
a=bpy.data.objects['Goblin_Rig'];parts=[o for o in bpy.data.collections['GOBLIN | voxel parts'].objects if o.type=='MESH']
for o in parts:
 for mod in list(o.modifiers):
  if mod.type=='BEVEL':o.modifiers.remove(mod)
bpy.ops.object.select_all(action='DESELECT')
for o in parts:o.select_set(True)
bpy.context.view_layer.objects.active=parts[0];bpy.ops.object.join();mesh=bpy.context.object;mesh.name='Goblin_Concept_Mesh'
a.select_set(True);bpy.context.view_layer.objects.active=a
bpy.context.scene.render.fps=60;bpy.context.scene.render.fps_base=1
bpy.ops.export_scene.fbx(filepath=str(out/'Goblin_Concept_Animated.fbx'),use_selection=True,object_types={'ARMATURE','MESH'},add_leaf_bones=False,bake_anim=True,bake_anim_use_all_actions=True,bake_anim_use_nla_strips=False,bake_anim_simplify_factor=0,axis_forward='-Z',axis_up='Y',path_mode='COPY')
colors={m.name:list(m.diffuse_color) for m in mesh.data.materials if m}
(out/'GoblinPalette.json').write_text(json.dumps(colors,indent=2))
print('GOBLIN_FBX_READY',len(mesh.data.vertices),[x.name for x in bpy.data.actions])
