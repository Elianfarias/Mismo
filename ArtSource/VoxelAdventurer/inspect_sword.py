import bpy
from mathutils import Vector
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=r'C:\Users\elian\Mismo\Assets\Art\FBX\Weapons\fbx(unity)\sword_E.fbx')
for o in bpy.data.objects:
 print(o.name,o.type,list(o.dimensions),list(o.rotation_euler),list(o.scale))
 if o.type=='MESH':
  vs=[o.matrix_world@v.co for v in o.data.vertices]
  print('BOUNDS',[(min(v[i] for v in vs),max(v[i] for v in vs)) for i in range(3)])
  print('MATS',[(m.name,[(n.type,n.image.filepath if n.type=='TEX_IMAGE' and n.image else '') for n in m.node_tree.nodes]) for m in o.data.materials])
  for i in range(10):
   lo=min(v.z for v in vs); hi=max(v.z for v in vs); points=[v for v in vs if lo+(hi-lo)*i/10<=v.z<=lo+(hi-lo)*(i+1)/10]
   if points: print('SLICE',i, min(v.x for v in points),max(v.x for v in points),min(v.y for v in points),max(v.y for v in points))
