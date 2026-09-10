import bpy
import math
from pathlib import Path


INPUT = Path(r"C:\Users\elian\Mismo\Docs\Art\VoxelBase\Humanoid_Voxel_Study_20260907_055328_rigged_animated.blend")
OUT_BLEND = INPUT.with_name("Humanoid_Voxel_Study_20260907_055328_rigged_animated_fixed.blend")
OUT_FBX = INPUT.with_name("Humanoid_Voxel_Study_20260907_055328_rigged_Medium_Rigged_Animated_Fixed.fbx")
OUT_REPORT = INPUT.with_name("Humanoid_Voxel_Study_20260907_055328_rigged_animation_fixed_report.txt")


bpy.ops.wm.open_mainfile(filepath=str(INPUT))

arm = bpy.data.objects["Armature_Humanoid"]
mesh = bpy.data.objects["Voxel_Base_GameReady"]

if arm.animation_data:
    arm.animation_data_clear()
for name in ("Idle", "Walk"):
    action = bpy.data.actions.get(name)
    if action:
        bpy.data.actions.remove(action)

for pb in arm.pose.bones:
    pb.rotation_mode = "XYZ"
    pb.rotation_euler = (0.0, 0.0, 0.0)
    pb.location = (0.0, 0.0, 0.0)


def set_rot(name, x=0.0, y=0.0, z=0.0):
    pb = arm.pose.bones[name]
    pb.rotation_mode = "XYZ"
    pb.rotation_euler = (x, y, z)


def set_loc(name, x=0.0, y=0.0, z=0.0):
    arm.pose.bones[name].location = (x, y, z)


def key_all(frame):
    for pb in arm.pose.bones:
        pb.keyframe_insert(data_path="rotation_euler", index=-1, frame=frame, group=pb.name)
        pb.keyframe_insert(data_path="location", index=-1, frame=frame, group=pb.name)


def make_idle():
    act = bpy.data.actions.new("Idle")
    act.use_fake_user = True
    arm.animation_data_create()
    arm.animation_data.action = act
    for frame, sway in ((1, 0.0), (16, 0.012), (31, 0.0), (46, -0.012), (61, 0.0)):
        for pb in arm.pose.bones:
            pb.rotation_euler = (0.0, 0.0, 0.0)
            pb.location = (0.0, 0.0, 0.0)
        # Relaxed arms: the rest pose is a T-pose, so lower both upper arms.
        for side in ("L", "R"):
            set_rot(f"UpperArm.{side}", x=-1.0)
            set_rot(f"LowerArm.{side}", x=0.22)
        set_rot("Spine", y=sway)
        set_rot("Chest", y=sway * 1.4)
        set_rot("Neck", y=sway * 0.8)
        set_rot("Head", y=sway * 0.5)
        set_loc("Hips", z=0.008 if frame in (16, 46) else 0.0)
        key_all(frame)
    return act


def make_walk():
    act = bpy.data.actions.new("Walk")
    act.use_fake_user = True
    arm.animation_data_create()
    arm.animation_data.action = act
    frames = (1, 9, 17, 25, 31)
    # Existing leg cycle, preserved from the previous version.
    legs = {
        1:  (-0.384, 0.524, -0.14,  0.384, -0.175, 0.07),
        9:  (-0.07,  0.14,  -0.07,   0.07, -0.035, 0.035),
        17: (0.384, -0.175, 0.07,   -0.384, 0.524, -0.14),
        25: (0.07,  -0.035, 0.035,  -0.07, 0.14,  -0.07),
        31: (-0.384, 0.524, -0.14,  0.384, -0.175, 0.07),
    }
    # Arms are now relaxed instead of horizontal. Local Z adds a gentle
    # forward/back swing while local X keeps the arms pointed down.
    arm_swing = {1: -0.30, 9: 0.0, 17: 0.30, 25: 0.0, 31: -0.30}
    for frame in frames:
        for pb in arm.pose.bones:
            pb.rotation_euler = (0.0, 0.0, 0.0)
            pb.location = (0.0, 0.0, 0.0)
        swing = arm_swing[frame]
        for side in ("L", "R"):
            set_rot(f"UpperArm.{side}", x=-1.0, z=swing)
            set_rot(f"LowerArm.{side}", x=0.22, z=swing * 0.35)
        ul, ll, fl, ur, lr, fr = legs[frame]
        set_rot("UpperLeg.L", x=ul)
        set_rot("LowerLeg.L", x=ll)
        set_rot("Foot.L", x=fl)
        set_rot("UpperLeg.R", x=ur)
        set_rot("LowerLeg.R", x=lr)
        set_rot("Foot.R", x=fr)
        set_loc("Hips", z=0.012 if frame in (9, 25) else 0.0)
        key_all(frame)
    return act


idle = make_idle()
walk = make_walk()
arm.animation_data.action = idle
bpy.context.scene.frame_start = 1
bpy.context.scene.frame_end = 61
bpy.context.scene.render.fps = 30
bpy.context.scene.frame_set(1)

bpy.ops.object.select_all(action="DESELECT")
mesh.select_set(True)
arm.select_set(True)
bpy.context.view_layer.objects.active = arm

bpy.ops.wm.save_as_mainfile(filepath=str(OUT_BLEND))
bpy.ops.export_scene.fbx(
    filepath=str(OUT_FBX),
    use_selection=True,
    object_types={"ARMATURE", "MESH"},
    add_leaf_bones=False,
    bake_anim=True,
    bake_anim_use_all_actions=True,
    bake_anim_use_nla_strips=False,
    bake_anim_simplify_factor=0.0,
    apply_unit_scale=True,
)

OUT_REPORT.write_text(
    "Humanoid Voxel Animation Fixed Report\n"
    f"Mesh: {mesh.name} / {len(mesh.data.vertices)} verts / {len(mesh.data.polygons)} faces / {len(mesh.data.loop_triangles)} tris\n"
    f"Armature: {arm.name} / {len(arm.data.bones)} bones\n"
    "FPS: 30\n"
    "Actions: Idle frames 1-61 cyclic; Walk frames 1-31 cyclic in-place\n"
    "Fix: arms lowered from T-pose; local X=-1.0 upper arm, local X=0.22 forearm\n"
    "Walk: gentle opposite forward/back arm swing, preserved leg cycle, no root translation\n",
    encoding="utf-8",
)
print("FIXED_OUTPUT", OUT_BLEND, OUT_FBX, OUT_REPORT)
