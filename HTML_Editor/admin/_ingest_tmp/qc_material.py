"""Apply an obsidian-black finish to the CAD geometry, export GLB, render QC. Blender 4.5."""
import bpy, sys, os, math
from mathutils import Vector

argv = sys.argv[sys.argv.index("--") + 1:]
in_fbx, out_glb, preview = argv[0], argv[1], argv[2]

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=in_fbx)
meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH']

m = bpy.data.materials.new("ObsidianBlack"); m.use_nodes = True
bsdf = m.node_tree.nodes.get("Principled BSDF")
def setin(name, val):
    if bsdf and name in bsdf.inputs:
        bsdf.inputs[name].default_value = val; return True
    return False
setin("Base Color", (0.013, 0.013, 0.016, 1.0))
setin("Metallic", 0.0)                                 # gate D: dielectric, not metallic
setin("Roughness", 0.18)                               # gate D: low roughness = obsidian, not grey
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
dim = mx - mn; ctr = (mn + mx) / 2; r = max(dim)

bpy.ops.export_scene.gltf(filepath=out_glb, export_format='GLB', use_selection=False)
print("GLB", os.path.isfile(out_glb), os.path.getsize(out_glb))

scn = bpy.context.scene
cam_d = bpy.data.cameras.new("c"); cam = bpy.data.objects.new("c", cam_d)
scn.collection.objects.link(cam); scn.camera = cam; cam_d.lens = 60
def render_from(vec, path):
    cam.location = ctr + Vector(vec)
    d = ctr - cam.location
    cam.rotation_euler = d.to_track_quat('-Z', 'Y').to_euler()
    scn.render.filepath = path
    bpy.ops.render.render(write_still=True)
    print("R", os.path.basename(path), os.path.isfile(path))

for ang, en in [((55, 0, 35), 3.0), ((62, 0, -120), 1.2), ((-50, 0, 160), 2.6)]:
    ld = bpy.data.lights.new("L", type='SUN'); ld.energy = en
    lo = bpy.data.objects.new("L", ld); scn.collection.objects.link(lo)
    lo.rotation_euler = tuple(math.radians(a) for a in ang)
world = bpy.data.worlds.new("W"); scn.world = world; world.use_nodes = True
world.node_tree.nodes["Background"].inputs[0].default_value = (0.62, 0.62, 0.66, 1)
try:
    scn.render.engine = 'BLENDER_EEVEE_NEXT'
except Exception:
    scn.render.engine = 'BLENDER_WORKBENCH'
scn.render.resolution_x = 640; scn.render.resolution_y = 900
scn.render.image_settings.file_format = 'PNG'

base = os.path.splitext(preview)[0]
render_from((0, -2.0 * r, 0.25 * r), base + "_front.png")
render_from((r * 1.25, -r * 1.7, r * 0.95), base + "_3q.png")
print("DONE")
