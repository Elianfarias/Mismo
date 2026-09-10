import bpy
from mathutils import Vector
from pathlib import Path


OUT_DIR = Path(r"C:\Users\elian\Mismo\Docs\Art\Goblins")
OUT_DIR.mkdir(parents=True, exist_ok=True)
OUT_BLEND = OUT_DIR / "Voxel_Goblin_Base.blend"
OUT_FBX = OUT_DIR / "Voxel_Goblin_Base.fbx"
OUT_REPORT = OUT_DIR / "Voxel_Goblin_Base_report.txt"

# Start a clean file so the existing humanoid and arcanist assets stay untouched.
bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
scene.render.fps = 30
scene.frame_start = 1
scene.frame_end = 1

collection = bpy.data.collections.new("Goblin_Voxel_Collection")
scene.collection.children.link(collection)


def make_material(name, color, metallic=0.0, roughness=0.8):
    m = bpy.data.materials.new(name)
    m.diffuse_color = (*color, 1.0)
    m.use_nodes = True
    bsdf = m.node_tree.nodes.get("Principled BSDF")
    if bsdf:
        bsdf.inputs["Base Color"].default_value = (*color, 1.0)
        bsdf.inputs["Metallic"].default_value = metallic
        bsdf.inputs["Roughness"].default_value = roughness
    return m


SKIN = make_material("Goblin_Skin", (0.18, 0.42, 0.105), 0.0, 0.9)
SKIN_DARK = make_material("Goblin_Skin_Dark", (0.08, 0.20, 0.045), 0.0, 0.92)
SKIN_LIGHT = make_material("Goblin_Skin_Light", (0.28, 0.52, 0.14), 0.0, 0.88)
EYE = make_material("Goblin_Eye_Gold", (0.90, 0.58, 0.045), 0.15, 0.3)
PUPIL = make_material("Goblin_Pupil", (0.008, 0.004, 0.002), 0.0, 0.7)
TOOTH = make_material("Goblin_Tusks", (0.82, 0.70, 0.42), 0.0, 0.78)
LEATHER = make_material("Goblin_Leather", (0.18, 0.055, 0.018), 0.0, 0.9)
LEATHER_LIGHT = make_material("Goblin_Leather_Light", (0.34, 0.12, 0.035), 0.0, 0.9)
CLOTH = make_material("Goblin_Cloth", (0.20, 0.095, 0.025), 0.0, 0.92)
METAL = make_material("Goblin_Metal", (0.20, 0.22, 0.20), 0.65, 0.4)
METAL_DARK = make_material("Goblin_Metal_Dark", (0.07, 0.08, 0.07), 0.75, 0.45)


def finish(obj, material=None, bone=None, slot="Body"):
    for c in list(obj.users_collection):
        c.objects.unlink(obj)
    collection.objects.link(obj)
    if material:
        obj.data.materials.append(material)
    for poly in obj.data.polygons:
        poly.use_smooth = False
    obj["asset"] = "Voxel_Goblin_Base"
    obj["slot"] = slot
    if bone:
        obj["parent_bone"] = bone
        world = obj.matrix_world.copy()
        obj.parent = armature
        obj.parent_type = "BONE"
        obj.parent_bone = bone
        obj.matrix_world = world
    return obj


def cube(name, loc, dims, material, bone=None, slot="Body", rot=(0.0, 0.0, 0.0)):
    bpy.ops.mesh.primitive_cube_add(location=loc, rotation=rot)
    obj = bpy.context.object
    obj.name = name
    obj.dimensions = dims
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    return finish(obj, material, bone, slot)


def ico(name, loc, scale, material, bone=None, slot="Body"):
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=1, radius=1.0, location=loc)
    obj = bpy.context.object
    obj.name = name
    obj.scale = scale
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    return finish(obj, material, bone, slot)


def cone(name, loc, r1, r2, depth, material, bone=None, slot="Accessory", vertices=6, rot=(0.0, 0.0, 0.0)):
    bpy.ops.mesh.primitive_cone_add(vertices=vertices, radius1=r1, radius2=r2, depth=depth, location=loc, rotation=rot)
    obj = bpy.context.object
    obj.name = name
    return finish(obj, material, bone, slot)


def tapered_box(name, z0, z1, w0, w1, d0, d1, cx, cy, material, bone=None, slot="Body"):
    verts = [
        (cx - w0 / 2, cy - d0 / 2, z0), (cx + w0 / 2, cy - d0 / 2, z0),
        (cx + w0 / 2, cy + d0 / 2, z0), (cx - w0 / 2, cy + d0 / 2, z0),
        (cx - w1 / 2, cy - d1 / 2, z1), (cx + w1 / 2, cy - d1 / 2, z1),
        (cx + w1 / 2, cy + d1 / 2, z1), (cx - w1 / 2, cy + d1 / 2, z1),
    ]
    faces = [(0, 1, 2, 3), (4, 7, 6, 5), (0, 4, 5, 1), (1, 5, 6, 2), (2, 6, 7, 3), (4, 0, 3, 7)]
    mesh = bpy.data.meshes.new(name + "_Mesh")
    mesh.from_pydata(verts, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    scene.collection.objects.link(obj)
    return finish(obj, material, bone, slot)


def pointed_panel(name, top_z, bottom_z, tip_z, top_w, bottom_w, depth, cx, cy, material, bone=None, slot="Clothing"):
    outline = [(-top_w / 2, top_z), (top_w / 2, top_z), (bottom_w / 2, bottom_z), (0.0, tip_z), (-bottom_w / 2, bottom_z)]
    verts = [(cx + x, cy - depth / 2, z) for x, z in outline] + [(cx + x, cy + depth / 2, z) for x, z in outline]
    faces = [(0, 1, 2, 3, 4), (9, 8, 7, 6, 5), (0, 5, 6, 1), (1, 6, 7, 2), (2, 7, 8, 3), (3, 8, 9, 4), (4, 9, 5, 0)]
    mesh = bpy.data.meshes.new(name + "_Mesh")
    mesh.from_pydata(verts, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    scene.collection.objects.link(obj)
    return finish(obj, material, bone, slot)


def bone_segment(name, bone_name, radius, material, slot="Body", start=0.0, end=1.0):
    pb = armature.pose.bones[bone_name]
    head = pb.head.copy()
    tail = pb.tail.copy()
    direction = tail - head
    p0 = head + direction * start
    p1 = head + direction * end
    vec = p1 - p0
    bpy.ops.mesh.primitive_cylinder_add(vertices=6, radius=radius, depth=vec.length, location=(p0 + p1) * 0.5)
    obj = bpy.context.object
    obj.name = name
    obj.rotation_mode = "QUATERNION"
    obj.rotation_quaternion = vec.to_track_quat("Z", "Y")
    return finish(obj, material, bone_name, slot)


# Build a compact, slightly hunched goblin skeleton in its relaxed pose.
bpy.ops.object.armature_add(enter_editmode=True, location=(0.0, 0.0, 0.0))
armature = bpy.context.object
armature.name = "Armature_Goblin"
armature.data.name = "Armature_Goblin_Data"
edit = armature.data.edit_bones
edit[0].name = "Root"
edit[0].head = (0.0, 0.0, 0.0)
edit[0].tail = (0.0, 0.0, 0.12)


def ebone(name, head, tail, parent="Root"):
    b = edit.new(name)
    b.head = head
    b.tail = tail
    b.parent = edit.get(parent) if parent else None
    return b


ebone("Hips", (0.0, 0.0, 0.52), (0.0, 0.0, 0.70))
ebone("Spine", (0.0, 0.0, 0.69), (0.0, 0.0, 0.88), "Hips")
ebone("Neck", (0.0, 0.0, 0.88), (0.0, 0.0, 1.06), "Spine")
ebone("Head", (0.0, 0.0, 1.04), (0.0, 0.0, 1.48), "Neck")
for side, s in (("L", 1.0), ("R", -1.0)):
    ebone(f"UpperArm.{side}", (0.24 * s, 0.0, 0.88), (0.38 * s, 0.0, 0.73), "Spine")
    ebone(f"LowerArm.{side}", (0.38 * s, 0.0, 0.73), (0.49 * s, 0.0, 0.60), f"UpperArm.{side}")
    ebone(f"Hand.{side}", (0.49 * s, 0.0, 0.60), (0.57 * s, 0.0, 0.58), f"LowerArm.{side}")
    ebone(f"UpperLeg.{side}", (0.13 * s, 0.0, 0.57), (0.14 * s, 0.0, 0.34), "Hips")
    ebone(f"LowerLeg.{side}", (0.14 * s, 0.0, 0.34), (0.17 * s, 0.0, 0.13), f"UpperLeg.{side}")
    ebone(f"Foot.{side}", (0.17 * s, 0.0, 0.13), (0.17 * s, -0.18, 0.08), f"LowerLeg.{side}")
bpy.ops.object.mode_set(mode="OBJECT")
armature.show_in_front = True

# Body and face.
tapered_box("Goblin_Torso", 0.60, 0.96, 0.46, 0.56, 0.34, 0.40, 0.0, 0.0, SKIN, "Hips", "Body")
ico("Goblin_Belly", (0.0, -0.19, 0.78), (0.22, 0.055, 0.25), SKIN_LIGHT, "Spine", "Body")
bone_segment("Goblin_Neck", "Neck", 0.115, SKIN_DARK, "Body", 0.15, 0.92)
ico("Goblin_Head", (0.0, 0.0, 1.27), (0.34, 0.30, 0.30), SKIN, "Head", "Head")
cube("Goblin_Brow_L", (-0.115, -0.255, 1.40), (0.12, 0.045, 0.055), SKIN_DARK, "Head", "Face")
cube("Goblin_Brow_R", (0.115, -0.255, 1.40), (0.12, 0.045, 0.055), SKIN_DARK, "Head", "Face")
cube("Goblin_Eye_L", (-0.115, -0.276, 1.34), (0.09, 0.045, 0.085), EYE, "Head", "Face")
cube("Goblin_Eye_R", (0.115, -0.276, 1.34), (0.09, 0.045, 0.085), EYE, "Head", "Face")
cube("Goblin_Pupil_L", (-0.115, -0.302, 1.34), (0.035, 0.018, 0.055), PUPIL, "Head", "Face")
cube("Goblin_Pupil_R", (0.115, -0.302, 1.34), (0.035, 0.018, 0.055), PUPIL, "Head", "Face")
cube("Goblin_Nose", (0.0, -0.285, 1.25), (0.18, 0.16, 0.16), SKIN_DARK, "Head", "Face")
cube("Goblin_Mouth", (0.0, -0.274, 1.15), (0.20, 0.035, 0.065), SKIN_DARK, "Head", "Face")
cone("Goblin_Tusk_L", (-0.105, -0.30, 1.17), 0.028, 0.065, 0.16, TOOTH, "Head", "Face", vertices=6)
cone("Goblin_Tusk_R", (0.105, -0.30, 1.17), 0.028, 0.065, 0.16, TOOTH, "Head", "Face", vertices=6)
cone("Goblin_Ear_L", (0.30, 0.0, 1.37), 0.125, 0.015, 0.27, SKIN_DARK, "Head", "Head", vertices=5, rot=(0.0, 0.85, 0.0))
cone("Goblin_Ear_R", (-0.30, 0.0, 1.37), 0.125, 0.015, 0.27, SKIN_DARK, "Head", "Head", vertices=5, rot=(0.0, -0.85, 0.0))

# Arms: exposed green skin with leather cuffs and oversized hands.
for side, s in (("L", 1.0), ("R", -1.0)):
    bone_segment(f"Goblin_UpperArm_{side}", f"UpperArm.{side}", 0.105, SKIN, "Arms", 0.05, 0.96)
    bone_segment(f"Goblin_LowerArm_{side}", f"LowerArm.{side}", 0.095, SKIN_LIGHT, "Arms", 0.04, 0.88)
    bone_segment(f"Goblin_Cuff_{side}", f"LowerArm.{side}", 0.115, LEATHER, "Armor", 0.70, 0.98)
    pb = armature.pose.bones[f"Hand.{side}"]
    ico(f"Goblin_Hand_{side}", tuple(pb.head), (0.13, 0.12, 0.11), SKIN, f"Hand.{side}", "Arms")

# Short legs, leather shin guards and broad feet.
for side, s in (("L", 1.0), ("R", -1.0)):
    bone_segment(f"Goblin_UpperLeg_{side}", f"UpperLeg.{side}", 0.14, SKIN_DARK, "Legs", 0.03, 0.96)
    bone_segment(f"Goblin_LowerLeg_{side}", f"LowerLeg.{side}", 0.115, SKIN, "Legs", 0.04, 0.93)
    cube(f"Goblin_Boot_{side}", (0.17 * s, -0.12, 0.10), (0.28, 0.34, 0.19), LEATHER, f"Foot.{side}", "Boots")
    cube(f"Goblin_BootToe_{side}", (0.17 * s, -0.28, 0.10), (0.29, 0.16, 0.16), LEATHER_LIGHT, f"Foot.{side}", "Boots")
    cube(f"Goblin_ShinGuard_{side}", (0.16 * s, -0.01, 0.28), (0.25, 0.25, 0.11), METAL_DARK, f"LowerLeg.{side}", "Armor")

# Simple goblin clothing and equipment.
pointed_panel("Goblin_Loincloth", 0.69, 0.40, 0.27, 0.30, 0.42, 0.08, 0.0, -0.22, CLOTH, "Hips", "Clothing")
cube("Goblin_BeltFront", (0.0, -0.205, 0.66), (0.55, 0.06, 0.085), LEATHER, "Hips", "Belt")
cube("Goblin_BeltBack", (0.0, 0.205, 0.66), (0.55, 0.06, 0.085), LEATHER, "Hips", "Belt")
cube("Goblin_Buckle", (0.0, -0.245, 0.66), (0.12, 0.04, 0.12), METAL, "Hips", "Belt")
cube("Goblin_Pouch_L", (-0.29, -0.16, 0.58), (0.14, 0.14, 0.16), LEATHER_LIGHT, "Hips", "Belt")
cube("Goblin_Pouch_R", (0.29, -0.16, 0.58), (0.14, 0.14, 0.16), LEATHER_LIGHT, "Hips", "Belt")
cube("Goblin_ShoulderStrap_L", (0.25, 0.0, 0.88), (0.11, 0.22, 0.10), LEATHER, "UpperArm.L", "Armor")
cube("Goblin_ShoulderStrap_R", (-0.25, 0.0, 0.88), (0.11, 0.22, 0.10), LEATHER, "UpperArm.R", "Armor")

# Metadata and export.
armature["asset_type"] = "Voxel Goblin"
armature["scale_reference"] = "1.48 Blender units tall"
armature["animation_ready"] = True
base_count = 0
for obj in collection.objects:
    if obj.type == "MESH":
        base_count += len(obj.data.polygons)

bpy.ops.object.select_all(action="DESELECT")
armature.select_set(True)
for obj in collection.objects:
    obj.select_set(True)
bpy.context.view_layer.objects.active = armature

bpy.ops.wm.save_as_mainfile(filepath=str(OUT_BLEND))
bpy.ops.export_scene.fbx(
    filepath=str(OUT_FBX), use_selection=True, object_types={"ARMATURE", "MESH"},
    add_leaf_bones=False, bake_anim=False, apply_unit_scale=True,
)

OUT_REPORT.write_text(
    "Voxel Goblin Base Report\n"
    f"Mesh objects: {sum(1 for o in collection.objects if o.type == 'MESH')}\n"
    f"Total mesh faces: {base_count}\n"
    f"Armature: {armature.name} / {len(armature.data.bones)} bones\n"
    "Style: flat-shaded voxel/blocky low-poly primitives\n"
    "Features: large head, pointed ears, snout, eyes, tusks, compact body, loincloth, belt, pouches, cuffs and boots\n"
    "Original humanoid and arcanist files were not modified.\n",
    encoding="utf-8",
)
print("GOBLIN_OUTPUT", OUT_BLEND, OUT_FBX, OUT_REPORT, "MESHES", sum(1 for o in collection.objects if o.type == "MESH"))
