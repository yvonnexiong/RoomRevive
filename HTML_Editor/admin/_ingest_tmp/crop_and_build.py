"""Like build_finish, but drop disconnected parts offset outside the main body's X-range
(e.g. a detachable milk flask bundled beside the machine in the CAD). Blender 4.5."""
import bpy, sys, os, math
from mathutils import Vector

a = sys.argv[sys.argv.index("--") + 1:]
in_fbx, out_glb, preview = a[0], a[1], a[2]
r, g, b, rough = float(a[3]), float(a[4]), float(a[5]), float(a[6])

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=in_fbx)
meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH']
for o in bpy.context.scene.objects:
    o.select_set(False)
for o in meshes:
    o.select_set(True)
bpy.context.view_layer.objects.active = meshes[0]
if len(meshes) > 1:
    bpy.ops.object.join()
obj = bpy.context.view_layer.objects.active
bpy.ops.object.select_all(action='DESELECT'); obj.select_set(True)
bpy.context.view_layer.objects.active = obj
bpy.ops.object.mode_set(mode='EDIT')
bpy.ops.mesh.select_all(action='SELECT')
bpy.ops.mesh.separate(type='LOOSE')
bpy.ops.object.mode_set(mode='OBJECT')
parts = [o for o in bpy.context.scene.objects if o.type == 'MESH']

def bb(o):
    cs = [o.matrix_world @ Vector(c) for c in o.bound_box]
    mn = Vector((min(c[i] for c in cs) for i in range(3)))
    mx = Vector((max(c[i] for c in cs) for i in range(3)))
    return mn, mx

main = max(parts, key=lambda o: len(o.data.vertices))
mmn, mmx = bb(main)
keep, drop = [], []
for o in parts:
    mn, mx = bb(o); cx = (mn.x + mx.x) / 2
    if mmn.x - 0.03 <= cx <= mmx.x + 0.03:
        keep.append(o)
    else:
        drop.append(o)
print("PARTS", len(parts), "KEEP", len(keep), "DROP", len(drop),
      "MAIN_X", round(mmn.x, 3), round(mmx.x, 3),
      "DROP_CX", [round((bb(o)[0].x + bb(o)[1].x) / 2, 3) for o in drop])
for o in drop:
    bpy.data.objects.remove(o, do_unlink=True)

mat = bpy.data.materials.new("Finish"); mat.use_nodes = True
bsdf = mat.node_tree.nodes.get("Principled BSDF")
def setin(n, v):
    if bsdf and n in bsdf.inputs:
        bsdf.inputs[n].default_value = v
setin("Base Color", (r, g, b, 1.0)); setin("Metallic", 0.0); setin("Roughness", rough)
for cn in ("Coat Weight", "Clearcoat", "Clearcoat Weight"):
    setin(cn, 0.4)
for o in keep:
    o.data.materials.clear(); o.data.materials.append(mat)

mn = Vector((1e9, 1e9, 1e9)); mx = Vector((-1e9, -1e9, -1e9))
for o in keep:
    a2, b2 = bb(o)
    for i in range(3):
        mn[i] = min(mn[i], a2[i]); mx[i] = max(mx[i], b2[i])
dim = mx - mn; ctr = (mn + mx) / 2; rad = max(dim)
bpy.ops.export_scene.gltf(filepath=out_glb, export_format='GLB', use_selection=False)
print("GLB", os.path.basename(out_glb), os.path.isfile(out_glb), os.path.getsize(out_glb), "BBOX", [round(d, 3) for d in dim])

scn = bpy.context.scene
cam_d = bpy.data.cameras.new("c"); cam = bpy.data.objects.new("c", cam_d)
scn.collection.objects.link(cam); scn.camera = cam; cam_d.lens = 60
def rf(vec, path):
    cam.location = ctr + Vector(vec)
    d = ctr - cam.location
    cam.rotation_euler = d.to_track_quat('-Z', 'Y').to_euler()
    scn.render.filepath = path; bpy.ops.render.render(write_still=True)
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
print("DONE")
