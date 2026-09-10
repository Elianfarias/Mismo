import bpy,json
p=r'C:\Users\elian\Mismo\Docs\Art\VoxelBase\Humanoid_Voxel_Study_20260907_055328_rigged_animated_fixed.blend1'
bpy.ops.wm.open_mainfile(filepath=p)
for o in bpy.data.objects:
 print('OBJECT',o.name,o.type,'loc',list(o.location),'dims',list(o.dimensions),'parent',o.parent.name if o.parent else None,'materials',[m.name for m in o.data.materials] if o.type=='MESH' else '', 'modifiers',[(m.name,m.type) for m in o.modifiers])
 if o.type=='ARMATURE': print('BONES',[(b.name,list(b.head_local),list(b.tail_local)) for b in o.data.bones])
print('ACTIONS',[a.name for a in bpy.data.actions])
print('CAMERA',bpy.context.scene.camera)
