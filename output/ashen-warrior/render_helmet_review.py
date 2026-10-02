"""Close views of the actual editable mesh, without image post-processing."""
import bpy, math
from pathlib import Path
from mathutils import Vector

root=Path(__file__).resolve().parent
bpy.ops.wm.open_mainfile(filepath=str(root/'staging/AshenWarrior.blend'))
scene=bpy.context.scene
arm=bpy.data.objects['Armature_Humanoid']
for side,sign in [('L',1),('R',-1)]:
    bone=arm.pose.bones['UpperArm.'+side];bone.rotation_mode='XYZ';bone.rotation_euler.x=math.radians(-67)
    bone=arm.pose.bones['LowerArm.'+side];bone.rotation_mode='XYZ';bone.rotation_euler.z=math.radians(-10*sign)
bpy.context.view_layer.update()
cam=scene.camera
cam.data.ortho_scale=1.06
scene.render.resolution_x=1000
scene.render.resolution_y=1000
scene.cycles.samples=32
target=Vector((0,-.045,1.79))
for name,offset in [('Helmet_ThreeQuarter',(4,-7,1.1)),('Helmet_Front',(0,-7,.06)),('Helmet_Profile',(7,0,.03))]:
    cam.location=target+Vector(offset)
    cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler()
    scene.render.filepath=str(root/'staging'/(name+'.png'))
    bpy.ops.render.render(write_still=True)
