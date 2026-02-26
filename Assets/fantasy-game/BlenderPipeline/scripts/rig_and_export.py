"""
rig_and_export.py
=================
Blender Python script — run headless:
    blender --background --python BlenderPipeline/scripts/rig_and_export.py

Loads the unrigged humanoid.glb, creates a humanoid armature with manually
defined bones (no Rigify), skins the mesh via existing vertex groups, and
exports the rigged model as humanoid_rigged.glb.

Bone names match the vertex groups created by generate_humanoid.py and also
follow Unity's Humanoid convention so Mecanim auto-mapping works.
"""

import bpy
import os
import sys
from mathutils import Vector

# ---------------------------------------------------------------------------
# Config
# ---------------------------------------------------------------------------
SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
EXPORT_DIR = os.path.join(SCRIPT_DIR, "..", "exports")
INPUT_PATH = os.path.join(EXPORT_DIR, "humanoid.glb")
OUTPUT_PATH = os.path.join(EXPORT_DIR, "humanoid_rigged.glb")

# Bone definitions: (name, head_xyz, tail_xyz, parent_name)
# Positions match the body parts in generate_humanoid.py
# Head = joint start, Tail = joint end (child direction)
BONES = [
    # --- Spine chain ---
    ("Root",         (0, 0, 0.00),    (0, 0, 0.10),    None),
    ("Hips",         (0, 0, 0.90),    (0, 0, 0.95),    "Root"),
    ("Spine",        (0, 0, 0.95),    (0, 0, 1.10),    "Hips"),
    ("Chest",        (0, 0, 1.10),    (0, 0, 1.30),    "Spine"),
    ("Neck",         (0, 0, 1.46),    (0, 0, 1.52),    "Chest"),
    ("Head",         (0, 0, 1.52),    (0, 0, 1.75),    "Neck"),

    # --- Left arm ---
    ("Shoulder_L",   (0.15, 0, 1.42), (0.30, 0, 1.40), "Chest"),
    ("UpperArm_L",   (0.30, 0, 1.40), (0.56, 0, 1.38), "Shoulder_L"),
    ("LowerArm_L",   (0.56, 0, 1.38), (0.84, 0, 1.38), "UpperArm_L"),
    ("Hand_L",       (0.84, 0, 1.38), (0.98, 0, 1.38), "LowerArm_L"),

    # --- Right arm ---
    ("Shoulder_R",   (-0.15, 0, 1.42),(-0.30, 0, 1.40),"Chest"),
    ("UpperArm_R",   (-0.30, 0, 1.40),(-0.56, 0, 1.38),"Shoulder_R"),
    ("LowerArm_R",   (-0.56, 0, 1.38),(-0.84, 0, 1.38),"UpperArm_R"),
    ("Hand_R",       (-0.84, 0, 1.38),(-0.98, 0, 1.38),"LowerArm_R"),

    # --- Left leg ---
    ("UpperLeg_L",   (0.12, 0, 0.88), (0.12, 0, 0.50), "Hips"),
    ("LowerLeg_L",   (0.12, 0, 0.50), (0.12, 0, 0.08), "UpperLeg_L"),
    ("Foot_L",       (0.12, 0, 0.08), (0.12, 0.15, 0.02),"LowerLeg_L"),

    # --- Right leg ---
    ("UpperLeg_R",   (-0.12, 0, 0.88),(-0.12, 0, 0.50),"Hips"),
    ("LowerLeg_R",   (-0.12, 0, 0.50),(-0.12, 0, 0.08),"UpperLeg_R"),
    ("Foot_R",       (-0.12, 0, 0.08),(-0.12, 0.15, 0.02),"LowerLeg_R"),
]


# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------
def clear_scene():
    """Remove everything."""
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)
    for block in bpy.data.meshes:
        if block.users == 0:
            bpy.data.meshes.remove(block)
    for block in bpy.data.armatures:
        if block.users == 0:
            bpy.data.armatures.remove(block)


def import_humanoid() -> bpy.types.Object:
    """Import the unrigged glb and return the mesh object."""
    if not os.path.exists(INPUT_PATH):
        print(f"ERROR: {INPUT_PATH} not found. Run generate_humanoid.py first.")
        sys.exit(1)

    bpy.ops.import_scene.gltf(filepath=INPUT_PATH)

    # Find the mesh object (might be nested under an empty)
    mesh_obj = None
    for obj in bpy.context.selected_objects:
        if obj.type == 'MESH':
            mesh_obj = obj
            break

    if mesh_obj is None:
        # Check all scene objects
        for obj in bpy.data.objects:
            if obj.type == 'MESH':
                mesh_obj = obj
                break

    if mesh_obj is None:
        print("ERROR: No mesh found in imported glb.")
        sys.exit(1)

    # Clear parent transform if glTF importer added an empty
    if mesh_obj.parent:
        parent = mesh_obj.parent
        mesh_obj.parent = None
        mesh_obj.matrix_world = mesh_obj.matrix_world  # keep world transform
        bpy.data.objects.remove(parent, do_unlink=True)

    print(f"  Imported mesh: {mesh_obj.name}")
    print(f"  Vertices: {len(mesh_obj.data.vertices)}")
    print(f"  Vertex groups: {[vg.name for vg in mesh_obj.vertex_groups]}")

    return mesh_obj


def create_armature() -> bpy.types.Object:
    """
    Create the humanoid armature from the BONES definition.
    Returns the armature object.
    """
    arm_data = bpy.data.armatures.new("HumanoidArmature")
    arm_data.display_type = 'STICK'
    arm_obj = bpy.data.objects.new("Armature", arm_data)
    bpy.context.collection.objects.link(arm_obj)

    # Must be active and in edit mode to add bones
    bpy.context.view_layer.objects.active = arm_obj
    bpy.ops.object.mode_set(mode='EDIT')

    bone_map = {}

    for name, head, tail, parent_name in BONES:
        bone = arm_data.edit_bones.new(name)
        bone.head = Vector(head)
        bone.tail = Vector(tail)
        bone.use_connect = False

        if parent_name and parent_name in bone_map:
            bone.parent = bone_map[parent_name]
            # Connect if the child head matches parent tail (within tolerance)
            if (bone.head - bone.parent.tail).length < 0.01:
                bone.use_connect = True

        bone_map[name] = bone

    bpy.ops.object.mode_set(mode='OBJECT')

    print(f"  Created armature with {len(BONES)} bones")
    return arm_obj


def assign_weights_by_bones(mesh_obj: bpy.types.Object, arm_obj: bpy.types.Object):
    """
    Assign each vertex to the nearest bone via vertex groups.
    glTF export strips vertex groups that aren't tied to an armature,
    so we reassign them here based on proximity to bone segments.
    """
    # Collect bone midpoints from the armature's rest pose
    bone_data = []  # list of (bone_name, head, tail)
    for bone in arm_obj.data.bones:
        if bone.name in ("Root",):
            continue  # Skip root — it's a utility bone
        head = arm_obj.matrix_world @ bone.head_local
        tail = arm_obj.matrix_world @ bone.tail_local
        bone_data.append((bone.name, head, tail))

    # Clear any existing vertex groups
    mesh_obj.vertex_groups.clear()

    # Create a vertex group for each bone
    for bone_name, _, _ in bone_data:
        mesh_obj.vertex_groups.new(name=bone_name)

    def closest_point_on_segment(p, a, b):
        """Return the closest point on segment a-b to point p."""
        ab = b - a
        length_sq = ab.length_squared
        if length_sq < 1e-8:
            return a
        t = max(0.0, min(1.0, (p - a).dot(ab) / length_sq))
        return a + ab * t

    # Assign each vertex to the nearest bone
    for v in mesh_obj.data.vertices:
        co = mesh_obj.matrix_world @ v.co
        best_bone = None
        best_dist = float('inf')
        for bone_name, head, tail in bone_data:
            pt = closest_point_on_segment(co, head, tail)
            dist = (co - pt).length
            if dist < best_dist:
                best_dist = dist
                best_bone = bone_name
        if best_bone:
            vg = mesh_obj.vertex_groups[best_bone]
            vg.add([v.index], 1.0, 'REPLACE')

    # Report
    for vg in mesh_obj.vertex_groups:
        count = sum(1 for v in mesh_obj.data.vertices
                    for g in v.groups
                    if g.group == vg.index and g.weight > 0.01)
        print(f"    {vg.name}: {count} vertices")


def skin_mesh(mesh_obj: bpy.types.Object, arm_obj: bpy.types.Object):
    """
    Parent the mesh to the armature with Armature deform.
    Reassigns vertex weights by bone proximity since glTF
    doesn't preserve vertex groups without an armature.
    """
    # Reassign weights based on bone positions
    assign_weights_by_bones(mesh_obj, arm_obj)

    # Parent mesh to armature
    mesh_obj.parent = arm_obj
    mesh_obj.matrix_parent_inverse = arm_obj.matrix_world.inverted()

    # Add armature modifier
    mod = mesh_obj.modifiers.new(name="Armature", type='ARMATURE')
    mod.object = arm_obj
    mod.use_vertex_groups = True

    print("  Skinned mesh to armature via vertex groups")


def set_rest_pose(arm_obj: bpy.types.Object):
    """Ensure the armature is in its rest pose (T-pose)."""
    bpy.context.view_layer.objects.active = arm_obj
    bpy.ops.object.mode_set(mode='POSE')
    bpy.ops.pose.select_all(action='SELECT')
    bpy.ops.pose.transforms_clear()
    bpy.ops.object.mode_set(mode='OBJECT')
    print("  Rest pose set (T-pose)")


# ---------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------
def main():
    print("=" * 60)
    print("  Rigging humanoid")
    print("=" * 60)

    clear_scene()

    # Step 1: Import the mesh
    mesh_obj = import_humanoid()

    # Step 2: Create skeleton
    arm_obj = create_armature()

    # Step 3: Skin mesh to skeleton
    skin_mesh(mesh_obj, arm_obj)

    # Step 4: Verify rest pose
    set_rest_pose(arm_obj)

    # Step 5: Export rigged model
    os.makedirs(EXPORT_DIR, exist_ok=True)

    # Select both armature and mesh for export
    bpy.ops.object.select_all(action='DESELECT')
    arm_obj.select_set(True)
    mesh_obj.select_set(True)
    bpy.context.view_layer.objects.active = arm_obj

    bpy.ops.export_scene.gltf(
        filepath=OUTPUT_PATH,
        export_format='GLB',
        use_selection=True,
        export_apply=False,          # Keep armature modifier live
        export_yup=True,
        export_materials='EXPORT',
        export_skins=True,           # Include skinning data
        export_all_influences=True,
        export_animations=False,     # No anims yet
    )

    file_size = os.path.getsize(OUTPUT_PATH)
    print(f"  Exported rigged model: {OUTPUT_PATH}")
    print(f"  File size: {file_size / 1024:.1f} KB")
    print("=" * 60)
    print("  Done! Copy to Assets/StreamingAssets/Models/ and hit Play.")
    print("=" * 60)


if __name__ == "__main__":
    main()
