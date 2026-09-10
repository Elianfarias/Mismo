import bpy
from mathutils import Vector

path = r"C:\Users\elian\Mismo\Docs\Art\Goblins\Voxel_Goblin_Base.blend"
out = r"C:\Users\elian\Mismo\Docs\Art\Goblins\Voxel_Goblin_preview.png"
bpy.ops.wm.open_mainfile(filepath=path)
scene = bpy.context.scene
scene.render.engine = "BLENDER_WORKBENCH"
scene.render.resolution_x = 640
scene.render.resolution_y = 800
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = "PNG"
scene.render.filepath = out
scene.display.shading.light = "STUDIO"
scene.display.shading.studio_light = "paint.sl"
scene.display.shading.color_type = "MATERIAL"
scene.display.shading.show_shadows = True
scene.display.shading.show_cavity = True
scene.display.shading.cavity_type = "BOTH"
cam_data = bpy.data.cameras.new("Goblin_Camera")
cam = bpy.data.objects.new("Goblin_Camera", cam_data)
scene.collection.objects.link(cam)
scene.camera = cam
cam.location = (2.4, -4.4, 1.35)
cam.rotation_euler = (Vector((0.0, 0.0, 0.78)) - cam.location).to_track_quat("-Z", "Y").to_euler()
cam.data.lens = 58
bpy.ops.render.render(write_still=True)
print("PREVIEW", out)
