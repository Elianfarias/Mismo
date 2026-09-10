import bpy, math, os
from mathutils import Vector

OUT = os.path.dirname(os.path.abspath(__file__))
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
for c in list(bpy.data.collections):
    if c.name != 'Collection': bpy.data.collections.remove(c)
character = bpy.data.collections.get('Collection')
character.name = 'CHARACTER | Rowan'
studio = bpy.data.collections.new('STUDIO | hide for export')
bpy.context.scene.collection.children.link(studio)
def mat(name, color, metallic=0):
    m=bpy.data.materials.new(name); m.diffuse_color=(*color,1); m.use_nodes=True
    p=m.node_tree.nodes.get('Principled BSDF'); p.inputs['Base Color'].default_value=(*color,1); p.inputs['Roughness'].default_value=.78; p.inputs['Metallic'].default_value=metallic
    return m
skin=mat('Skin | warm ochre',(.66,.37,.20)); skinlight=mat('Skin | highlight',(.83,.53,.30))
hair=mat('Hair | midnight brown',(.055,.034,.029)); hairlight=mat('Hair | chestnut',(.14,.073,.039))
teal=mat('Tunic | forest teal',(.035,.25,.23)); light=mat('Tunic | lit teal',(.07,.39,.33)); dark=mat('Tunic | shadow',(.025,.12,.13))
leather=mat('Leather | umber',(.19,.09,.041)); boot=mat('Boot | charcoal',(.065,.057,.052)); gold=mat('Brass',(.75,.45,.12),.45)
white=mat('Eye | ivory',(.94,.89,.73)); black=mat('Eye | ink',(.017,.026,.029)); steel=mat('Steel',(.43,.58,.62),.65)
def cube(name, loc, size, material, parent=None, collection=character):
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc); o=bpy.context.object; o.name=name
    o.dimensions=size; bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    for c in list(o.users_collection): c.objects.unlink(o)
    collection.objects.link(o); o.data.materials.append(material)
    if parent:
        matrix=o.matrix_world.copy(); o.parent=parent; o.matrix_world=matrix
    return o
def pivot(name, location):
    o=bpy.data.objects.new(name,None); character.objects.link(o); o.location=location; o.empty_display_size=.10; return o
root=pivot('ROOT | 1.80 m', (0,0,0))
def part(name,loc):
    p=pivot(name,loc); p.parent=root; return p
head=part('Head pivot',(0,0,1.38)); torso=part('Torso pivot',(0,0,.83))
cube('Tunic torso',(0,0,1.04),(.48,.28,.46),teal,torso)
cube('Tunic hem',(0,0,.805),(.52,.31,.11),dark,torso)
cube('Collar',(0,-.005,1.295),(.30,.30,.07),dark,torso)
cube('Neck',(0,0,1.34),(.20,.19,.12),skin,head)
cube('Head',(0,0,1.56),(.44,.36,.40),skinlight,head)
cube('Hair cap',(0,.005,1.775),(.48,.39,.09),hair,head)
cube('Hair back',(0,.17,1.60),(.47,.075,.30),hair,head)
for x,z,w in [(-.18,1.705,.10),(-.08,1.735,.10),(.02,1.72,.10),(.12,1.68,.10),(.20,1.64,.06)]:
    cube('Stepped fringe',(x,-.19,z),(w,.055,.12),hair,head)
for x in [-.24,.24]: cube('Ear',(x,0,1.535),(.06,.12,.10),skin,head)
for x in [-.105,.105]:
    cube('Eye white',(x,-.184,1.565),(.09,.012,.055),white,head)
    cube('Eye pupil',(x+.013,-.194,1.565),(.035,.012,.052),black,head)
    cube('Brow',(x,-.19,1.615),(.10,.018,.025),hair,head)
cube('Nose',(0,-.205,1.51),(.055,.05,.055),skin,head)
cube('Mouth',(0,-.186,1.435),(.075,.012,.018),leather,head)
for side,x in [('L',-.335),('R',.335)]:
    arm=part('Arm '+side+' pivot',(x,0,1.245))
    cube('Sleeve '+side,(x,0,1.14),(.17,.255,.25),teal,arm)
    cube('Sleeve edge '+side,(x,0,1.015),(.18,.265,.05),light,arm)
    cube('Forearm '+side,(x,0,.91),(.135,.19,.16),skinlight,arm)
    cube('Bracer '+side,(x,-.003,.84),(.155,.205,.08),leather,arm)
    cube('Hand '+side,(x,-.005,.765),(.145,.20,.09),skinlight,arm)
    leg=part('Leg '+side+' pivot',(x*.40,0,.77))
    lx=x*.40
    cube('Trouser '+side,(lx,0,.565),(.205,.245,.38),leather,leg)
    cube('Boot shaft '+side,(lx,0,.265),(.22,.26,.24),boot,leg)
    cube('Boot toe '+side,(lx,-.05,.105),(.23,.36,.15),boot,leg)
    cube('Boot cuff '+side,(lx,0,.375),(.23,.27,.06),gold,leg)
cube('Belt',(0,-.006,.87),(.505,.305,.075),leather,torso)
cube('Buckle',(0,-.172,.87),(.105,.035,.09),gold,torso)
cube('Buckle inset',(0,-.193,.87),(.052,.012,.044),boot,torso)
for i in range(7):
    cube('Diagonal chest strap',(-.185+i*.055,-.152,1.25-i*.05),(.073,.035,.065),leather,torso)
cube('Shoulder armor',(-.335,0,1.275),(.23,.31,.10),steel,bpy.data.objects['Arm L pivot'])
cube('Shoulder brass edge',(-.435,-.005,1.25),(.045,.32,.13),gold,bpy.data.objects['Arm L pivot'])
cube('Belt pouch',(.255,-.08,.825),(.13,.19,.15),leather,torso)
cube('Pouch clasp',(.255,-.181,.85),(.045,.022,.04),gold,torso)
cube('Backpack',(0,.225,1.065),(.33,.19,.32),leather,torso)
cube('Bedroll',(0,.235,1.285),(.39,.18,.12),light,torso)
for x in [-.12,.12]: cube('Bedroll strap',(x,.235,1.29),(.045,.19,.135),dark,torso)
# Separate editable sword, stowed behind the right shoulder.
sword=part('Sword pivot',(.23,.29,.93))
cube('Sword grip',(.23,.29,1.43),(.065,.07,.21),leather,sword)
cube('Sword pommel',(.23,.29,1.55),(.09,.085,.065),gold,sword)
cube('Sword guard',(.23,.29,1.31),(.25,.075,.055),gold,sword)
cube('Sword blade',(.23,.29,1.035),(.105,.045,.49),steel,sword)
cube('Sword tip',(.23,.29,.765),(.055,.045,.05),steel,sword)
sword.rotation_euler[1]=math.radians(17)
# Faceless direction: dark hood opening and two luminous eyes only.
for o in list(character.objects):
    if o.name.startswith(('Hair','Stepped fringe','Ear','Eye','Brow','Nose','Mouth','Head.')) or o.name == 'Head':
        bpy.data.objects.remove(o, do_unlink=True)
hood=mat('Hood | blue slate',(.065,.10,.17))
void=mat('Face | darkness',(.006,.009,.018))
eye=mat('Eyes | lavender glow',(.58,.30,.95))
eye.node_tree.nodes.get('Principled BSDF').inputs['Emission Color'].default_value=(.52,.22,1,1)
eye.node_tree.nodes.get('Principled BSDF').inputs['Emission Strength'].default_value=2.5
cube('Face void',(0,0,1.55),(.40,.32,.34),void,head)
cube('Hood top',(0,.015,1.755),(.50,.43,.09),hood,head)
cube('Hood crown',(0,.025,1.815),(.38,.35,.04),hood,head)
cube('Hood back',(0,.17,1.555),(.48,.09,.37),hood,head)
for x in [-.23,.23]:
    cube('Hood side',(x,0,1.555),(.08,.40,.35),hood,head)
for x in [-.095,.095]:
    cube('Luminous eye',(x,-.17,1.585),(.065,.025,.075),eye,head)
cube('Scarf face cover',(0,-.175,1.425),(.42,.09,.115),dark,head)
cube('Scarf fold',(0,-.205,1.385),(.34,.065,.055),light,head)
cube('Scarf tail',(.14,-.185,1.19),(.105,.065,.29),dark,torso)
floor=mat('Studio | slate',(.035,.053,.068))
cube('Display plinth',(0,0,-.065),(1.65,1.65,.12),floor,collection=studio)
cube('Ground',(0,0,-.15),(200,200,.05),floor,collection=studio)
def target(o,p): o.rotation_euler=(Vector(p)-o.location).to_track_quat('-Z','Y').to_euler()
def area(name,loc,power,size,color):
    d=bpy.data.lights.new(name,'AREA'); d.energy=power; d.shape='DISK'; d.size=size; d.color=color
    o=bpy.data.objects.new(name,d); studio.objects.link(o); o.location=loc; target(o,(0,0,.9))
area('Key',(-3,-4,6),450,4,(1,.84,.67)); area('Fill',(4,-2,3),260,3,(.60,.79,1)); area('Rim',(1,3,4),500,3,(.64,1,.9))
d=bpy.data.cameras.new('Portrait'); cam=bpy.data.objects.new('Portrait',d); studio.objects.link(cam)
cam.location=(3,-5,2.8); target(cam,(0,0,.9)); d.type='ORTHO'; d.ortho_scale=2.65
s=bpy.context.scene; s.camera=cam; s.render.engine='CYCLES'; s.cycles.samples=32
s.render.resolution_x=1000; s.render.resolution_y=1000; s.render.resolution_percentage=100
s.world.color=(.18,.18,.18); s.view_settings.view_transform='AgX'
s.render.image_settings.file_format='PNG'; s.render.filepath=os.path.join(OUT,'Rowan_preview.png')
bpy.ops.object.select_all(action='DESELECT')
for o in character.objects:
    if o.type=='MESH': o.select_set(True)
bpy.context.view_layer.objects.active=bpy.data.objects.get('Tunic torso')
for screen in bpy.data.screens:
    for a in screen.areas:
        if a.type=='VIEW_3D':
            a.spaces.active.region_3d.view_distance=3.5
            a.spaces.active.region_3d.view_location=Vector((0,0,.9))
            a.spaces.active.shading.color_type='MATERIAL'
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT,'Rowan_voxel.blend'))
bpy.ops.render.render(write_still=True)
