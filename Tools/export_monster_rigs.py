# Exports the rigged Skeleton, Skell Mage and Orc from Monster Pack's Blender source to FBX for Unity.
# Unzip Assets/Monster Pack/Blender Source/Blender_source.zip, then:
#   flatpak run org.blender.Blender -b "Monsters Pack Master.blend" --python Tools/export_monster_rigs.py
# In Unity the FBXs are Humanoid with the rigify bones mapped by hand (spine=Hips ... spine.005=Head), stored in their .meta.
import bpy, sys
import os
out = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "Assets", "Monster Pack", "Rigged")
rigs = {"Skell rig": "Skeleton_Rigged", "Skell Mage rig": "SkellMage_Rigged", "Orc rig": "Orc_Rigged"}
for rig_name, file in rigs.items():
    arm = bpy.data.objects[rig_name]
    bpy.ops.object.select_all(action='DESELECT')
    arm.select_set(True)
    for c in arm.children: c.select_set(True)
    bpy.context.view_layer.objects.active = arm
    # rest pose for the export
    arm.data.pose_position = "REST"
    arm.location = (0.0, 0.0, 0.0)   # each rig sits somewhere else in the master file: export it at the origin
    bpy.ops.export_scene.fbx(filepath=f"{out}/{file}.fbx", use_selection=True, object_types={'ARMATURE','MESH'},
        use_mesh_modifiers=True, add_leaf_bones=False, bake_anim=False, apply_scale_options='FBX_SCALE_ALL',
        axis_forward='-Z', axis_up='Y', primary_bone_axis='Y', secondary_bone_axis='X', mesh_smooth_type='FACE')
    print("EXPORTED", file, [b.name for b in arm.data.bones if b.parent is None], "parents:",
          {b.name: (b.parent.name if b.parent else None) for b in arm.data.bones if b.name in ("thigh.L","spine.001","spine.004","spine.005","shoulder.L","pelvis.L")})
