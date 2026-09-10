import bpy,os
p=r'C:\Users\elian\Mismo\Assets\Art\FBX\Voxel_Adventurer_Animated.fbx'
bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.fbx(filepath=p)
assert len(bpy.data.actions)==13,len(bpy.data.actions)
a=next(o for o in bpy.data.objects if o.type=='ARMATURE')
assert len(a.data.bones)==22
for action in bpy.data.actions:
 a.animation_data.action=action
 for f in [action.frame_range[0],sum(action.frame_range)/2,action.frame_range[1]]:
  bpy.context.scene.frame_set(round(f));bpy.context.view_layer.update()
  assert a.pose.bones['Root'].location.length<.0001,(action.name,'root translation')
w=bpy.data.objects['Sword_E_RightHand'];assert any(m.type=='ARMATURE' for m in w.modifiers)
assert w.vertex_groups.get('Hand.R')
print('VERIFIED 13 animation clips, 22 bones, hand-bound sword, in-place root')
