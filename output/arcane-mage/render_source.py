import bpy, math
from pathlib import Path
from mathutils import Vector
out=Path(bpy.data.filepath).parent
scene=bpy.context.scene
camera=scene.camera
arm=bpy.data.objects['Armature_Humanoid']
def render(name,loc):
    camera.location=loc
    camera.rotation_euler=(Vector((0,0,1.24))-camera.location).to_track_quat('-Z','Y').to_euler()
    scene.render.filepath=str(out/(name+'.png'))
    bpy.ops.render.render(write_still=True)
render('T_Pose',(0,-7,2.4))
for side,sign in [('L',1),('R',-1)]:
    bone=arm.pose.bones['UpperArm.'+side]
    bone.rotation_mode='XYZ';bone.rotation_euler.x=math.radians(-67)
    bone=arm.pose.bones['LowerArm.'+side]
    bone.rotation_mode='XYZ';bone.rotation_euler.z=math.radians(-10*sign)
bpy.context.view_layer.update()
render('Hero',(3,-7,3.0))
render('Side',(7,-.1,2.35))
render('Back',(-3,7,2.9))
