import bpy,os,math
from mathutils import Vector,Matrix
out=r'C:\Users\elian\Mismo\ArtSource\VoxelAdventurer'
bpy.ops.wm.open_mainfile(filepath=os.path.join(out,'Voxel_Base_Faceless_Detailed.blend'))
s=bpy.context.scene; arm=bpy.data.objects['Armature_Humanoid']; base=bpy.data.objects['Voxel_Base_GameReady']; arm.animation_data.action=bpy.data.actions['Idle']; s.frame_set(1); bpy.context.view_layer.update()
objects=[arm,base]+list(bpy.data.collections['DETAILS | Faceless adventurer'].objects)
for m in {m for o in objects if o.type=='MESH' for m in o.data.materials if m}:
 color=tuple(m.diffuse_color); m.use_nodes=True; nodes=m.node_tree.nodes
 bs=next((n for n in nodes if n.type=='BSDF_PRINCIPLED'),None) or nodes.new('ShaderNodeBsdfPrincipled')
 op=next((n for n in nodes if n.type=='OUTPUT_MATERIAL'),None) or nodes.new('ShaderNodeOutputMaterial')
 m.node_tree.links.new(bs.outputs['BSDF'],op.inputs['Surface']); bs.inputs['Base Color'].default_value=color; bs.inputs['Roughness'].default_value=.8
 if m.name.startswith('Eyes'):
  bs.inputs['Emission Color'].default_value=color; bs.inputs['Emission Strength'].default_value=.5
bpy.ops.import_scene.fbx(filepath=r'C:\Users\elian\Mismo\Assets\Art\FBX\Weapons\fbx(unity)\sword_E.fbx')
sword=next(o for o in bpy.context.selected_objects if o.type=='MESH'); sword.name='Sword_E_RightHand'
# Keep original mesh and UVs. Place the grip at the hand midpoint in the idle pose.
pb=arm.pose.bones['Hand.R']; hand_group=base.vertex_groups['Hand.R'].index
hand_vertices=[v.co for v in base.data.vertices if any(g.group==hand_group and g.weight>.5 for g in v.groups)]
rest_grip=sum(hand_vertices,Vector())/len(hand_vertices)
grip=(pb.matrix@arm.data.bones['Hand.R'].matrix_local.inverted())@rest_grip
world=sword.matrix_world.copy(); verts=[world@v.co for v in sword.data.vertices]
scale=1.0/(max(v.z for v in verts)-min(v.z for v in verts))
rotation=Vector((-.70,-.35,.62)).normalized().to_track_quat('Z','Y').to_matrix() @ Matrix.Rotation(math.radians(70),3,'Z')
deform=pb.matrix@arm.data.bones['Hand.R'].matrix_local.inverted()
for v,co in zip(sword.data.vertices,verts): v.co=deform.inverted()@(grip+rotation@(co*scale))
sword.matrix_world=Matrix.Identity(4); sword.parent=arm
vg=sword.vertex_groups.new(name='Hand.R'); vg.add(list(range(len(sword.data.vertices))),1,'REPLACE')
mod=sword.modifiers.new('Follow right hand','ARMATURE'); mod.object=arm
sword['source_asset']='sword_E.fbx'; sword['grip_bone']='Hand.R'
for m in sword.data.materials:
 bs=next(n for n in m.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
 for link in list(m.node_tree.links):
  if link.to_node==bs and link.to_socket==bs.inputs['Normal']: m.node_tree.links.remove(link)
 for n in m.node_tree.nodes:
  if n.type=='TEX_IMAGE' and n.image:
   n.image.filepath=r'C:\Users\elian\Mismo\Assets\Art\FBX\Weapons\Textures\weapons_bits_texture.png'; n.image.reload(); n.image.pack()
objects.append(sword)
# Render with materials so the source weapon palette is visible.
s.render.engine='CYCLES'; s.cycles.samples=24
for name,loc,power,size in [('SwordPreview_Key',(-3,-4,6),500,4),('SwordPreview_Fill',(4,-2,3),300,3),('SwordPreview_Rim',(0,3,4),500,3)]:
 d=bpy.data.lights.new(name,'AREA'); d.energy=power; d.shape='DISK'; d.size=size; o=bpy.data.objects.new(name,d); s.collection.objects.link(o); o.location=loc; o.rotation_euler=(Vector((0,0,1))-o.location).to_track_quat('-Z','Y').to_euler()
s.camera.location=(-3.4,-5.8,2.6); s.camera.rotation_euler=(Vector((0,-.22,1))-s.camera.location).to_track_quat('-Z','Y').to_euler(); s.camera.data.ortho_scale=2.6
s.render.resolution_x=950;s.render.resolution_y=1000;s.render.resolution_percentage=100
bpy.ops.object.select_all(action='DESELECT')
for o in objects: o.hide_set(False);o.select_set(True)
bpy.context.view_layer.objects.active=arm
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(out,'Voxel_Adventurer_Sword_E.blend'))
fbx=r'C:\Users\elian\Mismo\Assets\Art\FBX\Voxel_Adventurer_Sword_E.fbx'
bpy.ops.export_scene.fbx(filepath=fbx,use_selection=True,object_types={'ARMATURE','MESH'},add_leaf_bones=False,bake_anim=True,bake_anim_use_all_actions=True,bake_anim_use_nla_strips=False,bake_anim_simplify_factor=0,path_mode='COPY',embed_textures=True,axis_forward='-Z',axis_up='Y')
s.render.filepath=os.path.join(out,'Sword_E_Idle.png');bpy.ops.render.render(write_still=True)
arm.animation_data.action=bpy.data.actions['Walk'];s.frame_set(17);s.render.filepath=os.path.join(out,'Sword_E_Walk.png');bpy.ops.render.render(write_still=True)
# Reimport exported artifact and verify weapon skinning and clips.
bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.fbx(filepath=fbx)
w=bpy.data.objects['Sword_E_RightHand'];assert w.vertex_groups.get('Hand.R');assert any(m.type=='ARMATURE' for m in w.modifiers);assert len(bpy.data.actions)==2
assert any(n.type=='TEX_IMAGE' and n.image and n.image.has_data for m in w.data.materials for n in m.node_tree.nodes)
print('VERIFIED sword rig, texture and two animation clips',os.path.getsize(fbx))
