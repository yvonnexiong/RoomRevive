"""Import a GLB, report its materials/textures, render front + 3q. Blender 4.5."""
import bpy, sys, os, math
from mathutils import Vector

a = sys.argv[sys.argv.index("--") + 1:]
glb, preview = a[0], a[1]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=glb)
meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH']
mats = set(); tex = 0
for o in meshes:
    for s in o.material_slots:
        if s.material:
            mats.add(s.material.name)
            if s.material.use_nodes:
                for n in s.material.node_tree.nodes:
                    if n.type == 'TEX_IMAGE' and n.image:
                        tex += 1
print("MESHES", len(meshes), "MATERIALS", len(mats), "TEX", tex)
print("MATNAMES", list(mats)[:15])

mn = Vector((1e9, 1e9, 1e9)); mx = Vector((-1e9, -1e9, -1e9))
for o in meshes:
    for c in o.bound_box:
        w = o.matrix_world @ Vector(c)
        for i in range(3):
            mn[i] = min(mn[i], w[i]); mx[i] = max(mx[i], w[i])
dim = mx - mn; ctr = (mn + mx) / 2; rad = max(dim)
print("BBOX", [round(d, 3) for d in dim])

scn = bpy.context.scene
cam_d = bpy.data.cameras.new("c"); cam = bpy.data.objects.new("c", cam_d)
scn.collection.objects.link(cam); scn.camera = cam; cam_d.lens = 55
def rf(vec, path):
    cam.location = ctr + Vector(vec)
    d = ctr - cam.location
    cam.rotation_euler = d.to_track_quat('-Z', 'Y').to_euler()
    scn.render.filepath = path; bpy.ops.render.render(write_still=True)
for ang, en in [((55, 0, 35), 3.0), ((60, 0, -120), 1.5), ((-50, 0, 150), 2.5)]:
    ld = bpy.data.lights.new("L", type='SUN'); ld.energy = en
    lo = bpy.data.objects.new("L", ld); scn.collection.objects.link(lo)
    lo.rotation_euler = tuple(math.radians(x) for x in ang)
world = bpy.data.worlds.new("W"); scn.world = world; world.use_nodes = True
world.node_tree.nodes["Background"].inputs[0].default_value = (0.75, 0.75, 0.78, 1)
try:
    scn.render.engine = 'BLENDER_EEVEE_NEXT'
except Exception:
    scn.render.engine = 'BLENDER_WORKBENCH'
scn.render.resolution_x = 640; scn.render.resolution_y = 900
scn.render.image_settings.file_format = 'PNG'
base = os.path.splitext(preview)[0]
rf((0, -2.0 * rad, 0.2 * rad), base + "_front.png")
rf((0, 2.0 * rad, 0.2 * rad), base + "_back.png")
print("DONE")
