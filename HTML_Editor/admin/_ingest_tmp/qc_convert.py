"""Faithful FBX -> GLB (keep bundled texture) + a QC preview render. Blender 4.5 headless."""
import bpy, sys, os, math
from mathutils import Vector

argv = sys.argv[sys.argv.index("--") + 1:]
in_fbx, out_glb, preview = argv[0], argv[1], argv[2]

bpy.ops.wm.read_factory_settings(use_empty=True)

try:
    bpy.ops.import_scene.fbx(filepath=in_fbx)
except Exception as e:
    print("IMPORT_FAIL", e); raise

meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH']
mats = set()
img_tex = 0
for o in meshes:
    for s in o.material_slots:
        if s.material:
            mats.add(s.material.name)
            if s.material.use_nodes:
                for n in s.material.node_tree.nodes:
                    if n.type == 'TEX_IMAGE' and n.image:
                        img_tex += 1
print("MESHES", len(meshes), "MATERIALS", len(mats), "IMAGE_TEX_NODES", img_tex)
print("MAT_NAMES", list(mats)[:12])

# combined world-space bbox
mn = Vector((1e9, 1e9, 1e9)); mx = Vector((-1e9, -1e9, -1e9))
for o in meshes:
    for c in o.bound_box:
        w = o.matrix_world @ Vector(c)
        for i in range(3):
            mn[i] = min(mn[i], w[i]); mx[i] = max(mx[i], w[i])
dim = mx - mn; ctr = (mn + mx) / 2
print("BBOX_DIM", [round(d, 3) for d in dim], "CENTER", [round(c, 3) for c in ctr])

# export GLB first (so a render bug can't lose it)
bpy.ops.export_scene.gltf(filepath=out_glb, export_format='GLB', use_selection=False)
print("GLB_WRITTEN", os.path.isfile(out_glb), os.path.getsize(out_glb) if os.path.isfile(out_glb) else 0)

# --- QC preview render ---
scn = bpy.context.scene
r = max(dim) if max(dim) > 0 else 1.0
cam_data = bpy.data.cameras.new("QCCam"); cam = bpy.data.objects.new("QCCam", cam_data)
scn.collection.objects.link(cam); scn.camera = cam
cam_data.lens = 55

def render_from(vec, path):
    cam.location = ctr + Vector(vec)
    d = ctr - cam.location
    cam.rotation_euler = d.to_track_quat('-Z', 'Y').to_euler()
    scn.render.filepath = path
    bpy.ops.render.render(write_still=True)
    print("RENDER", os.path.basename(path), os.path.isfile(path))

light_data = bpy.data.lights.new("Sun", type='SUN'); light_data.energy = 3.5
light = bpy.data.objects.new("Sun", light_data); scn.collection.objects.link(light)
light.rotation_euler = (math.radians(55), 0, math.radians(35))
world = bpy.data.worlds.new("W"); scn.world = world; world.use_nodes = True
bg = world.node_tree.nodes["Background"]; bg.inputs[0].default_value = (0.9, 0.9, 0.92, 1)
try:
    scn.render.engine = 'BLENDER_EEVEE_NEXT'
except Exception:
    scn.render.engine = 'BLENDER_WORKBENCH'
scn.render.resolution_x = 640; scn.render.resolution_y = 900
scn.render.image_settings.file_format = 'PNG'

base = os.path.splitext(preview)[0]
# two opposite 3/4 views so we see the real front regardless of CAD orientation
render_from((r * 1.2, -r * 1.9, r * 1.1), base + "_A.png")
render_from((-r * 1.2, r * 1.9, r * 1.1), base + "_B.png")
print("DONE")
