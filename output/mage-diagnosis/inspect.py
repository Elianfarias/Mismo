import bpy,json,os
root='C:/Users/elian/Mismo'
reports={}
for path in ['ArtSource/VoxelAdventurer/ReferenceMage/Voxel_Reference_Mage.blend','Assets/Art/FBX/Characters/Voxel_Adventurer_Animated.fbx','Assets/Art/FBX/Characters/Voxel_Reference_Mage.fbx']:
 if path.endswith('.blend'): bpy.ops.wm.open_mainfile(filepath=root+'/'+path)
 else:
  bpy.ops.wm.read_factory_settings(use_empty=True)
  bpy.ops.import_scene.fbx(filepath=root+'/'+path)
 rows=[]
 for o in bpy.data.objects:
  row=dict(name=o.name,type=o.type,hidden=o.hide_get(),render_hidden=o.hide_render,parent=o.parent.name if o.parent else None)
  if o.type=='MESH':
   row.update(vertices=len(o.data.vertices),groups=[g.name for g in o.vertex_groups],unweighted=sum(not v.groups for v in o.data.vertices),modifiers=[dict(type=m.type,target=m.object.name if m.object else None) for m in o.modifiers if m.type=='ARMATURE'])
  rows.append(row)
 reports[path]=rows
json.dump(reports,open(root+'/output/mage-diagnosis/report.json','w'),indent=2)
