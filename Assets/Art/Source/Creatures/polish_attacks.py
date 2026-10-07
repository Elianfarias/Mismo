"""Bake only combat animation, preserving the original meshes, rigs and locomotion.

Run with Blender --background --python this_file -- <project root>.
Sources: ArtSource/Creatures/{Boar,Spider}/*Rigged.blend (original authoring files).
Output FBXs contain the deform skeleton and four one-shot clips, with IK baked.
Every clip is 60 frames / 1 second; Unity maps its three segments to gameplay time.
"""
import math
import os
import sys
import bpy
from mathutils import Vector

ROOT = os.path.abspath(sys.argv[sys.argv.index('--') + 1])
OUT = os.path.join(ROOT, 'Assets/Art/Animations/ForestCreatures/Combat')


def smooth(a, b, value):
    t = max(0.0, min(1.0, (value - a) / (b - a)))
    return t * t * (3.0 - 2.0 * t)


def move(bone, xyz):
    bone.location = bone.bone.matrix_local.to_3x3().inverted() @ Vector(xyz)


def pose_boar(rig, name, t):
    bones = rig.pose.bones
    body, head = bones['Body'], bones['Head']
    # Windup reaches a definite held silhouette before the committed movement.
    wind = smooth(0, .42, t)
    if name == 'Boar_TuskStrike':
        release = smooth(.60, .72, t)
        settle = 1 - smooth(.88, 1, t)
        wind *= (1 - release) * settle
        strike = release * settle
        move(body, (0, .16 * wind - .23 * strike, -.075 * wind + .025 * strike))
        body.rotation_euler.x = .075 * wind - .06 * strike
        head.rotation_euler.x = .30 * wind - .43 * strike
        for tag in ('FL', 'FR'):
            move(bones['IK_' + tag], (0, -.09 * strike, .055 * wind))
    else:
        settle = 1 - smooth(.90, 1, t)
        drive = smooth(.60, .64, t)
        load = smooth(0, .36, t) * settle
        wind *= (1 - drive) * settle
        move(body, (0, .13 * wind, -.085 * load + .025 * drive * settle * (1-math.cos(8*math.pi*(t-.60)/.30))))
        body.rotation_euler.x = .06 * load
        head.rotation_euler.x = .28 * load
        # Two visible forehoof scrapes differentiate the charge from the tusk strike.
        if t < .60:
            scrape = smooth(.08, .16, t) * (1-smooth(.50, .58, t))
            cycle = ((t-.08) / .22) % 1
            move(bones['IK_FL'], (0, -.17 * math.sin(2*math.pi*cycle)*scrape,
                                  .12 * max(0, math.sin(2*math.pi*cycle))*scrape))
        else:
            u = min(1, (t-.60)/.30)
            for tag, phase in {'FL':0, 'HR':.5, 'FR':.5, 'HL':0}.items():
                cycle = (u*2 + phase) % 1
                move(bones['IK_' + tag], (0, .23 * math.sin(2*math.pi*cycle)*drive*settle,
                                         .15 * max(0, math.sin(2*math.pi*cycle))*drive*settle))
    for sign in (-1, 1):
        bones['Ear' + str(sign)].rotation_euler.y = sign * .17 * wind
    bones['Tail'].rotation_euler.x = -.16 * wind


def pose_spider(rig, name, t):
    bones = rig.pose.bones
    body, head, abdomen = bones['Body'], bones['Head'], bones['Abdomen']
    wind = smooth(0, .40, t)
    if name == 'Spider_Pounce':
        u = max(0, min(1, (t-.50)/.42))
        launch = smooth(0, .12, u)
        # Ground contact at 90% of the active window, matching the damage gate.
        air = max(0, math.sin(math.pi*min(1, u/.90)))
        height = .64 * air
        crouch = wind * (1-launch)
        landing = smooth(.82, .90, u) * (1-smooth(.92, 1, t))
        move(body, (0, .10*crouch, height-.17*crouch-.055*landing))
        head.rotation_euler.x = -.16*crouch-.16*air
        abdomen.rotation_euler.x = .20*crouch+.18*air
        for sign, side in ((1, 'L'), (-1, 'R')):
            for index in range(1, 5):
                target = bones['IK_' + side + str(index)]
                front = 1 if index == 1 else 0
                move(target, (sign*(.10*crouch-.15*air),
                              -.12*front*crouch-target.bone.head_local.y*.10*air,
                              height+.11*air+.12*front*crouch))
    else:
        strike = smooth(.60, .74, t)
        settle = 1-smooth(.88, 1, t)
        wind *= (1-strike)*settle
        strike *= settle
        move(body, (0, .10*wind-.20*strike, .065*wind-.025*strike))
        head.rotation_euler.x = -.26*wind+.26*strike
        abdomen.rotation_euler.x = .10*wind-.07*strike
        for sign, side in ((1, 'L'), (-1, 'R')):
            move(bones['IK_' + side + '1'], (sign*.08*wind, -.12*strike, .23*wind+.07*strike))
            move(bones['IK_' + side + '2'], (sign*.04*wind, -.035*strike, .04*wind))
    for sign, side in ((1, 'L'), (-1, 'R')):
        jaw = wind if name == 'Spider_Bite' else crouch
        closing = strike if name == 'Spider_Bite' else air
        bones['Fang_' + side].rotation_euler.y = sign*(.44*jaw-.22*closing)
        bones['Palp_' + side].rotation_euler.x = -.20*jaw


def bake(species, filename, names, pose):
    bpy.ops.wm.open_mainfile(filepath=os.path.join(ROOT, 'ArtSource/Creatures', species, filename))
    rig = bpy.data.objects[species + '_Rig']
    rig.animation_data_clear()
    for action in list(bpy.data.actions):
        bpy.data.actions.remove(action)
    rig.animation_data_create()
    scene = bpy.context.scene
    scene.render.fps = 60
    for name in names:
        action = bpy.data.actions.new(name)
        action.use_fake_user = True
        rig.animation_data.action = action
        for f in range(61):
            scene.frame_set(f+1)
            for bone in rig.pose.bones:
                bone.rotation_mode = 'XYZ'
                bone.location = (0, 0, 0)
                bone.rotation_euler = (0, 0, 0)
                bone.scale = (1, 1, 1)
            pose(rig, name, f/60)
            for bone in rig.pose.bones:
                bone.keyframe_insert('location', frame=f+1, group=bone.name)
                bone.keyframe_insert('rotation_euler', frame=f+1, group=bone.name)
        # Blender 5 layered actions expose curves on their channel bag.
        for layer in action.layers:
            for strip in layer.strips:
                for bag in strip.channelbags:
                    for curve in bag.fcurves:
                        for key in curve.keyframe_points:
                            key.interpolation = 'LINEAR'
    scene.frame_start, scene.frame_end = 1, 61
    scene.frame_set(1)
    bpy.ops.object.select_all(action='DESELECT')
    rig.hide_set(False)
    rig.select_set(True)
    bpy.context.view_layer.objects.active = rig
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, species + '_Combat.fbx'),
        use_selection=True, object_types={'ARMATURE'}, axis_forward='-Z', axis_up='Y',
        apply_unit_scale=True, add_leaf_bones=False, use_armature_deform_only=True,
        bake_anim=True, bake_anim_use_all_actions=True, bake_anim_use_nla_strips=False,
        bake_anim_force_startend_keying=True, bake_anim_step=1, bake_anim_simplify_factor=0)


os.makedirs(OUT, exist_ok=True)
bake('Boar', 'Wild_Boar_Rigged.blend', ('Boar_TuskStrike', 'Boar_Charge'), pose_boar)
bake('Spider', 'Forest_Spider_Rigged.blend', ('Spider_Bite', 'Spider_Pounce'), pose_spider)
