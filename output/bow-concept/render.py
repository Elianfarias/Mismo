import bpy
from mathutils import Vector
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=r'C:\Users\elian\Mismo\Assets\Art\FBX\Weapons\Bow\bow_B_withString.fbx')
for o in bpy.context.scene.objects:
 print('OBJECT',o.name,o.type,tuple(o.dimensions))
for m in bpy.data.materials:
 print('MAT',m.name,tuple(m.diffuse_color))
for im in bpy.data.images:
 print('IMAGE',im.name,im.filepath)
