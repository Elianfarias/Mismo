import bpy, os, json
out=r'C:\Users\elian\Mismo\ArtSource\VoxelAdventurer'
bpy.ops.wm.open_mainfile(filepath=os.path.join(out,'Voxel_Base_Faceless_Detailed.blend'))
arm=bpy.data.objects['Armature_Humanoid']; base=bpy.data.objects['Voxel_Base_GameReady']
objects=[arm,base]+list(bpy.data.collections['DETAILS | Faceless adventurer'].objects)
materials={m for o in objects if o.type=='MESH' for m in o.data.materials if m}
for m in materials:
 color=tuple(m.diffuse_color)
 m.use_nodes=True
 nodes=m.node_tree.nodes
 bs=next((n for n in nodes if n.type=='BSDF_PRINCIPLED'),None)
 if bs is None: bs=nodes.new('ShaderNodeBsdfPrincipled')
 output=next((n for n in nodes if n.type=='OUTPUT_MATERIAL'),None)
 if output is None: output=nodes.new('ShaderNodeOutputMaterial')
 m.node_tree.links.new(bs.outputs['BSDF'],output.inputs['Surface'])
 bs.inputs['Base Color'].default_value=color
 bs.inputs['Roughness'].default_value=.8
 if m.name.startswith('Eyes'):
  bs.inputs['Emission Color'].default_value=color
  bs.inputs['Emission Strength'].default_value=.4
arm.animation_data.action=bpy.data.actions['Idle']; bpy.context.scene.frame_set(1)
bpy.ops.object.select_all(action='DESELECT')
for o in objects: o.hide_set(False); o.select_set(True)
bpy.context.view_layer.objects.active=arm
path=os.path.join(out,'Voxel_Base_Faceless_Detailed_Colored.fbx')
bpy.ops.export_scene.fbx(filepath=path,use_selection=True,object_types={'ARMATURE','MESH'},add_leaf_bones=False,bake_anim=True,bake_anim_use_all_actions=True,bake_anim_use_nla_strips=False,bake_anim_simplify_factor=0.0,path_mode='COPY',embed_textures=True,axis_forward='-Z',axis_up='Y')
expected={m.name:tuple(m.diffuse_color) for m in materials}
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=path)
meshes=[o for o in bpy.data.objects if o.type=='MESH']
assert len(meshes)==36, len(meshes)
assert any(o.type=='ARMATURE' for o in bpy.data.objects)
assert len(bpy.data.actions)>=2
for name,color in expected.items():
 m=bpy.data.materials.get(name)
 assert m is not None, name
 bs=next(n for n in m.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
 actual=bs.inputs['Base Color'].default_value
 assert all(abs(actual[i]-color[i])<.02 for i in range(3)), (name,list(actual),color)
print('VERIFIED',len(meshes),'meshes',len(expected),'colored materials',len(bpy.data.actions),'actions',os.path.getsize(path),'bytes')
