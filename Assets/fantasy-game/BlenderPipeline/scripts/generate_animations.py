"""
generate_animations.py
======================
Blender Python script — run headless:
    blender --background --python BlenderPipeline/scripts/generate_animations.py

Loads humanoid_rigged.glb, creates procedural keyframe animations
(Idle, Walk, Run, Jump) as NLA actions, and re-exports with animations.

Must be run AFTER rig_and_export.py.
"""

import bpy
import math
import os
import sys
from mathutils import Euler

# ---------------------------------------------------------------------------
# Config
# ---------------------------------------------------------------------------
SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
EXPORT_DIR = os.path.join(SCRIPT_DIR, "..", "exports")
INPUT_PATH = os.path.join(EXPORT_DIR, "humanoid_rigged.glb")
OUTPUT_PATH = os.path.join(EXPORT_DIR, "humanoid_rigged.glb")  # overwrite

FPS = 30


# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------
def clear_scene():
    """Remove all objects from the scene."""
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)
    for block in bpy.data.meshes:
        if block.users == 0:
            bpy.data.meshes.remove(block)
    for block in bpy.data.armatures:
        if block.users == 0:
            bpy.data.armatures.remove(block)
    for block in bpy.data.actions:
        if block.users == 0:
            bpy.data.actions.remove(block)


def assign_weights_by_bones(mesh_obj, arm_obj):
    """
    Assign each vertex to the nearest bone via vertex groups.
    Duplicated from rig_and_export.py because glTF round-trip strips
    vertex groups, so we need to rebuild them.
    """
    bone_data = []
    for bone in arm_obj.data.bones:
        if bone.name == "Root":
            continue
        head = arm_obj.matrix_world @ bone.head_local
        tail = arm_obj.matrix_world @ bone.tail_local
        bone_data.append((bone.name, head, tail))

    mesh_obj.vertex_groups.clear()
    for bone_name, _, _ in bone_data:
        mesh_obj.vertex_groups.new(name=bone_name)

    def closest_point_on_segment(p, a, b):
        ab = b - a
        length_sq = ab.length_squared
        if length_sq < 1e-8:
            return a
        t = max(0.0, min(1.0, (p - a).dot(ab) / length_sq))
        return a + ab * t

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

    total = sum(1 for vg in mesh_obj.vertex_groups
                for v in mesh_obj.data.vertices
                for g in v.groups
                if g.group == vg.index and g.weight > 0.01)
    print(f"  Reassigned {total} vertex-bone weights across {len(mesh_obj.vertex_groups)} groups")


def import_rigged_model():
    """Import the rigged GLB and return the armature and mesh objects."""
    if not os.path.exists(INPUT_PATH):
        print(f"ERROR: {INPUT_PATH} not found. Run rig_and_export.py first.")
        sys.exit(1)

    bpy.ops.import_scene.gltf(filepath=INPUT_PATH)

    arm_obj = None
    mesh_obj = None
    for obj in bpy.data.objects:
        if obj.type == 'ARMATURE':
            arm_obj = obj
        elif obj.type == 'MESH':
            # Pick the mesh with the most vertices (skip stray primitives)
            if mesh_obj is None or len(obj.data.vertices) > len(mesh_obj.data.vertices):
                mesh_obj = obj

    # Delete any stray mesh objects that aren't our main mesh
    for obj in list(bpy.data.objects):
        if obj.type == 'MESH' and obj != mesh_obj:
            print(f"  Removing stray mesh: {obj.name} ({len(obj.data.vertices)} verts)")
            bpy.data.objects.remove(obj, do_unlink=True)

    if arm_obj is None:
        print("ERROR: No armature found in imported GLB.")
        sys.exit(1)

    print(f"  Imported armature: {arm_obj.name}")
    print(f"  Bones: {[b.name for b in arm_obj.data.bones]}")

    if mesh_obj is None:
        print("ERROR: No mesh found in imported GLB.")
        sys.exit(1)

    print(f"  Imported mesh: {mesh_obj.name}, verts: {len(mesh_obj.data.vertices)}")

    # --- Fix mesh-armature relationship (glTF round-trip breaks these) ---

    # 1. Parent mesh to armature
    mesh_obj.parent = arm_obj
    mesh_obj.matrix_parent_inverse = arm_obj.matrix_world.inverted()

    # 2. Ensure Armature modifier exists
    has_armature_mod = False
    for mod in mesh_obj.modifiers:
        if mod.type == 'ARMATURE':
            mod.object = arm_obj
            has_armature_mod = True
            break
    if not has_armature_mod:
        mod = mesh_obj.modifiers.new(name="Armature", type='ARMATURE')
        mod.object = arm_obj
    print("  Armature modifier set up")

    # 3. Reassign vertex weights (lost in glTF round-trip)
    assign_weights_by_bones(mesh_obj, arm_obj)

    # 4. Clear any imported animation data / NLA tracks (we'll create fresh ones)
    if arm_obj.animation_data:
        arm_obj.animation_data.action = None
        for track in list(arm_obj.animation_data.nla_tracks):
            arm_obj.animation_data.nla_tracks.remove(track)
    # Also purge orphan actions from the imported file
    for action in list(bpy.data.actions):
        if action.users == 0:
            bpy.data.actions.remove(action)
        else:
            action.user_clear()
            bpy.data.actions.remove(action)
    print("  Cleared imported animation data")

    return arm_obj, mesh_obj


def rad(degrees):
    """Convert degrees to radians."""
    return math.radians(degrees)


def set_bone_rotation(arm_obj, bone_name, rx_deg, ry_deg, rz_deg, frame):
    """Set rotation keyframe on a pose bone (euler XYZ, in degrees)."""
    pose_bone = arm_obj.pose.bones.get(bone_name)
    if pose_bone is None:
        return
    pose_bone.rotation_mode = 'XYZ'
    pose_bone.rotation_euler = Euler((rad(rx_deg), rad(ry_deg), rad(rz_deg)))
    pose_bone.keyframe_insert(data_path="rotation_euler", frame=frame)


def set_bone_location(arm_obj, bone_name, x, y, z, frame):
    """Set location keyframe on a pose bone (local offset from rest)."""
    pose_bone = arm_obj.pose.bones.get(bone_name)
    if pose_bone is None:
        return
    pose_bone.location = (x, y, z)
    pose_bone.keyframe_insert(data_path="location", frame=frame)


def create_action(arm_obj, name):
    """Create a new action and assign it to the armature."""
    action = bpy.data.actions.new(name=name)
    if arm_obj.animation_data is None:
        arm_obj.animation_data_create()
    arm_obj.animation_data.action = action
    return action


def push_to_nla(arm_obj, action, name):
    """Push the current action to an NLA track."""
    track = arm_obj.animation_data.nla_tracks.new()
    track.name = name
    strip = track.strips.new(name, 1, action)
    strip.action = action
    arm_obj.animation_data.action = None


def reset_pose(arm_obj):
    """Reset all pose bones to rest position."""
    for pb in arm_obj.pose.bones:
        pb.rotation_mode = 'XYZ'
        pb.rotation_euler = Euler((0, 0, 0))
        pb.location = (0, 0, 0)


# ---------------------------------------------------------------------------
# Animation: Idle (60 frames = 2.0s, looping)
# ---------------------------------------------------------------------------
def create_idle(arm_obj):
    """Subtle breathing / sway animation."""
    action = create_action(arm_obj, "Idle")
    reset_pose(arm_obj)

    # Keyframe pattern: rest → peak → rest (looping)
    # Frames: 1, 30, 60
    for frame in [1, 60]:  # rest frames (loop boundary)
        set_bone_rotation(arm_obj, "Spine", 0, 0, 0, frame)
        set_bone_rotation(arm_obj, "Chest", 0, 0, 0, frame)
        set_bone_rotation(arm_obj, "Head", 0, 0, 0, frame)
        set_bone_rotation(arm_obj, "UpperArm_L", 0, 0, 0, frame)
        set_bone_rotation(arm_obj, "UpperArm_R", 0, 0, 0, frame)

    # Breathing peak at frame 30
    set_bone_rotation(arm_obj, "Spine", 2, 0, 0, 30)
    set_bone_rotation(arm_obj, "Chest", -1.5, 0, 0, 30)
    set_bone_rotation(arm_obj, "Head", -1, 0, 0, 30)
    set_bone_rotation(arm_obj, "UpperArm_L", 0, 0, 1, 30)
    set_bone_rotation(arm_obj, "UpperArm_R", 0, 0, -1, 30)

    push_to_nla(arm_obj, action, "Idle")
    print("  Created: Idle (60 frames)")


# ---------------------------------------------------------------------------
# Animation: Walk (30 frames = 1.0s, looping)
# ---------------------------------------------------------------------------
def create_walk(arm_obj):
    """Bipedal walk cycle."""
    action = create_action(arm_obj, "Walk")
    reset_pose(arm_obj)

    # Walk cycle keyframes:
    # Frame 1:  Left contact (left foot forward)
    # Frame 8:  Left passing (left foot under body)
    # Frame 15: Right contact (right foot forward)
    # Frame 23: Right passing
    # Frame 30: Left contact again (loop)

    # --- Frame 1 & 30: Left contact ---
    for f in [1, 30]:
        # Legs
        set_bone_rotation(arm_obj, "UpperLeg_L", -25, 0, 0, f)
        set_bone_rotation(arm_obj, "LowerLeg_L", 5, 0, 0, f)
        set_bone_rotation(arm_obj, "Foot_L", 10, 0, 0, f)
        set_bone_rotation(arm_obj, "UpperLeg_R", 20, 0, 0, f)
        set_bone_rotation(arm_obj, "LowerLeg_R", 35, 0, 0, f)
        set_bone_rotation(arm_obj, "Foot_R", -5, 0, 0, f)
        # Arms (opposite to legs)
        set_bone_rotation(arm_obj, "UpperArm_L", 15, 0, 0, f)
        set_bone_rotation(arm_obj, "LowerArm_L", -10, 0, 0, f)
        set_bone_rotation(arm_obj, "UpperArm_R", -15, 0, 0, f)
        set_bone_rotation(arm_obj, "LowerArm_R", -15, 0, 0, f)
        # Spine twist
        set_bone_rotation(arm_obj, "Spine", 2, 3, 0, f)
        set_bone_rotation(arm_obj, "Chest", 0, -3, 0, f)
        # Hips bob (lowest at contact)
        set_bone_location(arm_obj, "Hips", 0, 0, -0.02, f)

    # --- Frame 8: Left passing ---
    set_bone_rotation(arm_obj, "UpperLeg_L", 0, 0, 0, 8)
    set_bone_rotation(arm_obj, "LowerLeg_L", 30, 0, 0, 8)
    set_bone_rotation(arm_obj, "Foot_L", -15, 0, 0, 8)
    set_bone_rotation(arm_obj, "UpperLeg_R", 0, 0, 0, 8)
    set_bone_rotation(arm_obj, "LowerLeg_R", 5, 0, 0, 8)
    set_bone_rotation(arm_obj, "Foot_R", 0, 0, 0, 8)
    set_bone_rotation(arm_obj, "UpperArm_L", 0, 0, 0, 8)
    set_bone_rotation(arm_obj, "LowerArm_L", -5, 0, 0, 8)
    set_bone_rotation(arm_obj, "UpperArm_R", 0, 0, 0, 8)
    set_bone_rotation(arm_obj, "LowerArm_R", -5, 0, 0, 8)
    set_bone_rotation(arm_obj, "Spine", 2, 0, 0, 8)
    set_bone_rotation(arm_obj, "Chest", 0, 0, 0, 8)
    set_bone_location(arm_obj, "Hips", 0, 0, 0.01, 8)

    # --- Frame 15: Right contact (mirror of frame 1) ---
    set_bone_rotation(arm_obj, "UpperLeg_L", 20, 0, 0, 15)
    set_bone_rotation(arm_obj, "LowerLeg_L", 35, 0, 0, 15)
    set_bone_rotation(arm_obj, "Foot_L", -5, 0, 0, 15)
    set_bone_rotation(arm_obj, "UpperLeg_R", -25, 0, 0, 15)
    set_bone_rotation(arm_obj, "LowerLeg_R", 5, 0, 0, 15)
    set_bone_rotation(arm_obj, "Foot_R", 10, 0, 0, 15)
    set_bone_rotation(arm_obj, "UpperArm_L", -15, 0, 0, 15)
    set_bone_rotation(arm_obj, "LowerArm_L", -15, 0, 0, 15)
    set_bone_rotation(arm_obj, "UpperArm_R", 15, 0, 0, 15)
    set_bone_rotation(arm_obj, "LowerArm_R", -10, 0, 0, 15)
    set_bone_rotation(arm_obj, "Spine", 2, -3, 0, 15)
    set_bone_rotation(arm_obj, "Chest", 0, 3, 0, 15)
    set_bone_location(arm_obj, "Hips", 0, 0, -0.02, 15)

    # --- Frame 23: Right passing (mirror of frame 8) ---
    set_bone_rotation(arm_obj, "UpperLeg_L", 0, 0, 0, 23)
    set_bone_rotation(arm_obj, "LowerLeg_L", 5, 0, 0, 23)
    set_bone_rotation(arm_obj, "Foot_L", 0, 0, 0, 23)
    set_bone_rotation(arm_obj, "UpperLeg_R", 0, 0, 0, 23)
    set_bone_rotation(arm_obj, "LowerLeg_R", 30, 0, 0, 23)
    set_bone_rotation(arm_obj, "Foot_R", -15, 0, 0, 23)
    set_bone_rotation(arm_obj, "UpperArm_L", 0, 0, 0, 23)
    set_bone_rotation(arm_obj, "LowerArm_L", -5, 0, 0, 23)
    set_bone_rotation(arm_obj, "UpperArm_R", 0, 0, 0, 23)
    set_bone_rotation(arm_obj, "LowerArm_R", -5, 0, 0, 23)
    set_bone_rotation(arm_obj, "Spine", 2, 0, 0, 23)
    set_bone_rotation(arm_obj, "Chest", 0, 0, 0, 23)
    set_bone_location(arm_obj, "Hips", 0, 0, 0.01, 23)

    push_to_nla(arm_obj, action, "Walk")
    print("  Created: Walk (30 frames)")


# ---------------------------------------------------------------------------
# Animation: Run (20 frames = 0.667s, looping)
# ---------------------------------------------------------------------------
def create_run(arm_obj):
    """Amplified walk cycle for running."""
    action = create_action(arm_obj, "Run")
    reset_pose(arm_obj)

    # Run cycle: same structure as walk but more extreme, faster
    # Frame 1 & 20: Left contact
    # Frame 5: Left passing
    # Frame 10: Right contact
    # Frame 15: Right passing

    # --- Frame 1 & 20: Left contact ---
    for f in [1, 20]:
        set_bone_rotation(arm_obj, "UpperLeg_L", -40, 0, 0, f)
        set_bone_rotation(arm_obj, "LowerLeg_L", 10, 0, 0, f)
        set_bone_rotation(arm_obj, "Foot_L", 15, 0, 0, f)
        set_bone_rotation(arm_obj, "UpperLeg_R", 30, 0, 0, f)
        set_bone_rotation(arm_obj, "LowerLeg_R", 50, 0, 0, f)
        set_bone_rotation(arm_obj, "Foot_R", -10, 0, 0, f)
        set_bone_rotation(arm_obj, "UpperArm_L", 25, 0, 0, f)
        set_bone_rotation(arm_obj, "LowerArm_L", -20, 0, 0, f)
        set_bone_rotation(arm_obj, "UpperArm_R", -30, 0, 0, f)
        set_bone_rotation(arm_obj, "LowerArm_R", -25, 0, 0, f)
        set_bone_rotation(arm_obj, "Spine", 5, 5, 0, f)
        set_bone_rotation(arm_obj, "Chest", -3, -5, 0, f)
        set_bone_location(arm_obj, "Hips", 0, 0, -0.03, f)

    # --- Frame 5: Left passing ---
    set_bone_rotation(arm_obj, "UpperLeg_L", 5, 0, 0, 5)
    set_bone_rotation(arm_obj, "LowerLeg_L", 45, 0, 0, 5)
    set_bone_rotation(arm_obj, "Foot_L", -20, 0, 0, 5)
    set_bone_rotation(arm_obj, "UpperLeg_R", -5, 0, 0, 5)
    set_bone_rotation(arm_obj, "LowerLeg_R", 10, 0, 0, 5)
    set_bone_rotation(arm_obj, "Foot_R", 0, 0, 0, 5)
    set_bone_rotation(arm_obj, "UpperArm_L", 0, 0, 0, 5)
    set_bone_rotation(arm_obj, "LowerArm_L", -10, 0, 0, 5)
    set_bone_rotation(arm_obj, "UpperArm_R", 0, 0, 0, 5)
    set_bone_rotation(arm_obj, "LowerArm_R", -10, 0, 0, 5)
    set_bone_rotation(arm_obj, "Spine", 5, 0, 0, 5)
    set_bone_rotation(arm_obj, "Chest", -3, 0, 0, 5)
    set_bone_location(arm_obj, "Hips", 0, 0, 0.02, 5)

    # --- Frame 10: Right contact (mirror) ---
    set_bone_rotation(arm_obj, "UpperLeg_L", 30, 0, 0, 10)
    set_bone_rotation(arm_obj, "LowerLeg_L", 50, 0, 0, 10)
    set_bone_rotation(arm_obj, "Foot_L", -10, 0, 0, 10)
    set_bone_rotation(arm_obj, "UpperLeg_R", -40, 0, 0, 10)
    set_bone_rotation(arm_obj, "LowerLeg_R", 10, 0, 0, 10)
    set_bone_rotation(arm_obj, "Foot_R", 15, 0, 0, 10)
    set_bone_rotation(arm_obj, "UpperArm_L", -30, 0, 0, 10)
    set_bone_rotation(arm_obj, "LowerArm_L", -25, 0, 0, 10)
    set_bone_rotation(arm_obj, "UpperArm_R", 25, 0, 0, 10)
    set_bone_rotation(arm_obj, "LowerArm_R", -20, 0, 0, 10)
    set_bone_rotation(arm_obj, "Spine", 5, -5, 0, 10)
    set_bone_rotation(arm_obj, "Chest", -3, 5, 0, 10)
    set_bone_location(arm_obj, "Hips", 0, 0, -0.03, 10)

    # --- Frame 15: Right passing (mirror) ---
    set_bone_rotation(arm_obj, "UpperLeg_L", -5, 0, 0, 15)
    set_bone_rotation(arm_obj, "LowerLeg_L", 10, 0, 0, 15)
    set_bone_rotation(arm_obj, "Foot_L", 0, 0, 0, 15)
    set_bone_rotation(arm_obj, "UpperLeg_R", 5, 0, 0, 15)
    set_bone_rotation(arm_obj, "LowerLeg_R", 45, 0, 0, 15)
    set_bone_rotation(arm_obj, "Foot_R", -20, 0, 0, 15)
    set_bone_rotation(arm_obj, "UpperArm_L", 0, 0, 0, 15)
    set_bone_rotation(arm_obj, "LowerArm_L", -10, 0, 0, 15)
    set_bone_rotation(arm_obj, "UpperArm_R", 0, 0, 0, 15)
    set_bone_rotation(arm_obj, "LowerArm_R", -10, 0, 0, 15)
    set_bone_rotation(arm_obj, "Spine", 5, 0, 0, 15)
    set_bone_rotation(arm_obj, "Chest", -3, 0, 0, 15)
    set_bone_location(arm_obj, "Hips", 0, 0, 0.02, 15)

    push_to_nla(arm_obj, action, "Run")
    print("  Created: Run (20 frames)")


# ---------------------------------------------------------------------------
# Animation: Jump (30 frames = 1.0s, non-looping)
# ---------------------------------------------------------------------------
def create_jump(arm_obj):
    """Jump: crouch → launch → airborne → land."""
    action = create_action(arm_obj, "Jump")
    reset_pose(arm_obj)

    # Frame 1: Standing (neutral)
    set_bone_rotation(arm_obj, "Spine", 0, 0, 0, 1)
    set_bone_rotation(arm_obj, "UpperLeg_L", 0, 0, 0, 1)
    set_bone_rotation(arm_obj, "UpperLeg_R", 0, 0, 0, 1)
    set_bone_rotation(arm_obj, "LowerLeg_L", 0, 0, 0, 1)
    set_bone_rotation(arm_obj, "LowerLeg_R", 0, 0, 0, 1)
    set_bone_rotation(arm_obj, "UpperArm_L", 0, 0, 0, 1)
    set_bone_rotation(arm_obj, "UpperArm_R", 0, 0, 0, 1)
    set_bone_location(arm_obj, "Hips", 0, 0, 0, 1)

    # Frame 6: Crouch (preparation)
    set_bone_rotation(arm_obj, "Spine", 15, 0, 0, 6)
    set_bone_rotation(arm_obj, "UpperLeg_L", -30, 0, 0, 6)
    set_bone_rotation(arm_obj, "UpperLeg_R", -30, 0, 0, 6)
    set_bone_rotation(arm_obj, "LowerLeg_L", 50, 0, 0, 6)
    set_bone_rotation(arm_obj, "LowerLeg_R", 50, 0, 0, 6)
    set_bone_rotation(arm_obj, "Foot_L", -15, 0, 0, 6)
    set_bone_rotation(arm_obj, "Foot_R", -15, 0, 0, 6)
    set_bone_rotation(arm_obj, "UpperArm_L", 20, 0, 0, 6)
    set_bone_rotation(arm_obj, "UpperArm_R", 20, 0, 0, 6)
    set_bone_rotation(arm_obj, "LowerArm_L", -20, 0, 0, 6)
    set_bone_rotation(arm_obj, "LowerArm_R", -20, 0, 0, 6)
    set_bone_location(arm_obj, "Hips", 0, 0, -0.1, 6)

    # Frame 10: Launch (explosive extension)
    set_bone_rotation(arm_obj, "Spine", -10, 0, 0, 10)
    set_bone_rotation(arm_obj, "UpperLeg_L", 10, 0, 0, 10)
    set_bone_rotation(arm_obj, "UpperLeg_R", 10, 0, 0, 10)
    set_bone_rotation(arm_obj, "LowerLeg_L", 5, 0, 0, 10)
    set_bone_rotation(arm_obj, "LowerLeg_R", 5, 0, 0, 10)
    set_bone_rotation(arm_obj, "Foot_L", 15, 0, 0, 10)
    set_bone_rotation(arm_obj, "Foot_R", 15, 0, 0, 10)
    set_bone_rotation(arm_obj, "UpperArm_L", -40, 0, 0, 10)
    set_bone_rotation(arm_obj, "UpperArm_R", -40, 0, 0, 10)
    set_bone_rotation(arm_obj, "LowerArm_L", -5, 0, 0, 10)
    set_bone_rotation(arm_obj, "LowerArm_R", -5, 0, 0, 10)
    set_bone_location(arm_obj, "Hips", 0, 0, 0.05, 10)

    # Frame 16: Airborne peak (tucked)
    set_bone_rotation(arm_obj, "Spine", -5, 0, 0, 16)
    set_bone_rotation(arm_obj, "UpperLeg_L", -15, 0, 0, 16)
    set_bone_rotation(arm_obj, "UpperLeg_R", -15, 0, 0, 16)
    set_bone_rotation(arm_obj, "LowerLeg_L", 25, 0, 0, 16)
    set_bone_rotation(arm_obj, "LowerLeg_R", 25, 0, 0, 16)
    set_bone_rotation(arm_obj, "Foot_L", 0, 0, 0, 16)
    set_bone_rotation(arm_obj, "Foot_R", 0, 0, 0, 16)
    set_bone_rotation(arm_obj, "UpperArm_L", -20, 0, -15, 16)
    set_bone_rotation(arm_obj, "UpperArm_R", -20, 0, 15, 16)
    set_bone_rotation(arm_obj, "LowerArm_L", -10, 0, 0, 16)
    set_bone_rotation(arm_obj, "LowerArm_R", -10, 0, 0, 16)
    set_bone_location(arm_obj, "Hips", 0, 0, 0.03, 16)

    # Frame 22: Landing impact (slight crouch)
    set_bone_rotation(arm_obj, "Spine", 10, 0, 0, 22)
    set_bone_rotation(arm_obj, "UpperLeg_L", -20, 0, 0, 22)
    set_bone_rotation(arm_obj, "UpperLeg_R", -20, 0, 0, 22)
    set_bone_rotation(arm_obj, "LowerLeg_L", 35, 0, 0, 22)
    set_bone_rotation(arm_obj, "LowerLeg_R", 35, 0, 0, 22)
    set_bone_rotation(arm_obj, "Foot_L", -10, 0, 0, 22)
    set_bone_rotation(arm_obj, "Foot_R", -10, 0, 0, 22)
    set_bone_rotation(arm_obj, "UpperArm_L", 10, 0, 0, 22)
    set_bone_rotation(arm_obj, "UpperArm_R", 10, 0, 0, 22)
    set_bone_rotation(arm_obj, "LowerArm_L", -15, 0, 0, 22)
    set_bone_rotation(arm_obj, "LowerArm_R", -15, 0, 0, 22)
    set_bone_location(arm_obj, "Hips", 0, 0, -0.06, 22)

    # Frame 30: Recovery (back to neutral)
    set_bone_rotation(arm_obj, "Spine", 0, 0, 0, 30)
    set_bone_rotation(arm_obj, "UpperLeg_L", 0, 0, 0, 30)
    set_bone_rotation(arm_obj, "UpperLeg_R", 0, 0, 0, 30)
    set_bone_rotation(arm_obj, "LowerLeg_L", 0, 0, 0, 30)
    set_bone_rotation(arm_obj, "LowerLeg_R", 0, 0, 0, 30)
    set_bone_rotation(arm_obj, "Foot_L", 0, 0, 0, 30)
    set_bone_rotation(arm_obj, "Foot_R", 0, 0, 0, 30)
    set_bone_rotation(arm_obj, "UpperArm_L", 0, 0, 0, 30)
    set_bone_rotation(arm_obj, "UpperArm_R", 0, 0, 0, 30)
    set_bone_rotation(arm_obj, "LowerArm_L", 0, 0, 0, 30)
    set_bone_rotation(arm_obj, "LowerArm_R", 0, 0, 0, 30)
    set_bone_location(arm_obj, "Hips", 0, 0, 0, 30)

    push_to_nla(arm_obj, action, "Jump")
    print("  Created: Jump (30 frames)")


# ---------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------
def main():
    print("=" * 60)
    print("  Generating animations")
    print("=" * 60)

    clear_scene()
    bpy.context.scene.render.fps = FPS

    # Import the rigged model
    arm_obj, mesh_obj = import_rigged_model()

    # Ensure armature is active
    bpy.context.view_layer.objects.active = arm_obj
    arm_obj.select_set(True)

    # Create all four animations
    create_idle(arm_obj)
    create_walk(arm_obj)
    create_run(arm_obj)
    create_jump(arm_obj)

    # Report NLA tracks
    print(f"  NLA tracks: {[t.name for t in arm_obj.animation_data.nla_tracks]}")

    # Export with animations
    bpy.ops.object.select_all(action='DESELECT')
    arm_obj.select_set(True)
    if mesh_obj:
        mesh_obj.select_set(True)
    bpy.context.view_layer.objects.active = arm_obj

    bpy.ops.export_scene.gltf(
        filepath=OUTPUT_PATH,
        export_format='GLB',
        use_selection=True,
        export_apply=False,
        export_yup=True,
        export_materials='EXPORT',
        export_skins=True,
        export_all_influences=True,
        export_animations=True,
        export_nla_strips=True,
    )

    file_size = os.path.getsize(OUTPUT_PATH)
    print(f"  Exported: {OUTPUT_PATH}")
    print(f"  File size: {file_size / 1024:.1f} KB")
    print("=" * 60)
    print("  Done! Animations baked into humanoid_rigged.glb")
    print("=" * 60)


if __name__ == "__main__":
    main()
