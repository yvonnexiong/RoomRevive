"""Parametric finish builder: FBX -> obsidian/white/graphite/red GLB + QC renders. Blender 4.5.
Args: in_fbx out_glb preview_base  r g b roughness"""
import bpy, sys, os, math
from mathutils import Vector

a = sys.argv[sys.argv.index("--") + 1:]
in_fbx, out_glb, preview = a[0], a[1], a[2]
r, g, b, rough = float(a[3]), float(a[4]), float(a[5]), float(a[6])

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=in_fbx)
meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH']

m = bpy.data.materials.new("Finish"); m.use_nodes = True
bsdf = m.node_tree.nodes.get("Principled BSDF")
def setin(n, v):
    if bsdf and n in bsdf.inputs:
        bsdf.inputs[n].default_value = v
setin("Base Color", (r, g, b, 1.0))
setin("Metallic", 0.0)                 # gate D: dielectric finishes
setin("Roughness", rough)
for cn in ("Coat Weight", "Clearcoat", "Clearcoat Weight"):
    setin(cn, 0.4)
for o in meshes:
    o.data.materials.clear(); o.data.materials.append(m)

mn = Vector((1e9, 1e9, 1e9)); mx = Vector((-1e9, -1e9, -1e9))
for o in meshes:
    for c in o.bound_box:
        w = o.matrix_world @ Vector(c)
        for i in range(3):
            mn[i] = min(mn[i], w[i]); mx[i] = max(mx[i], w[i])
dim = mx - mn; ctr = (mn + mx) / 2; rad = max(dim)
bpy.ops.export_scene.gltf(filepath=out_glb, export_format='GLB', use_selection=False)
print("GLB", os.path.basename(out_glb), os.path.isfile(out_glb), os.path.getsize(out_glb),
      "BBOX", [round(d, 3) for d in dim])

scn = bpy.context.scene
cam_d = bpy.data.cameras.new("c"); cam = bpy.data.objects.new("c", cam_d)
scn.collection.objects.link(cam); scn.camera = cam; cam_d.lens = 60
def rf(vec, path):
    cam.location = ctr + Vector(vec)
    d = ctr - cam.location
    cam.rotation_euler = d.to_track_quat('-Z', 'Y').to_euler()
    scn.render.filepath = path
    bpy.ops.render.render(write_still=True)
for ang, en in [((55, 0, 35), 2.6), ((62, 0, -120), 1.1), ((-50, 0, 160), 2.2)]:
    ld = bpy.data.lights.new("L", type='SUN'); ld.energy = en
    lo = bpy.data.objects.new("L", ld); scn.collection.objects.link(lo)
    lo.rotation_euler = tuple(math.radians(x) for x in ang)
world = bpy.data.worlds.new("W"); scn.world = world; world.use_nodes = True
world.node_tree.nodes["Background"].inputs[0].default_value = (0.5, 0.5, 0.53, 1)
try:
    scn.render.engine = 'BLENDER_EEVEE_NEXT'
except Exception:
    scn.render.engine = 'BLENDER_WORKBENCH'
scn.render.resolution_x = 640; scn.render.resolution_y = 900
scn.render.image_settings.file_format = 'PNG'
base = os.path.splitext(preview)[0]
rf((0, -2.0 * rad, 0.25 * rad), base + "_front.png")
rf((rad * 1.25, -rad * 1.7, rad * 0.95), base + "_3q.png")
print("DONE", os.path.basename(out_glb))
