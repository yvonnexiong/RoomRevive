"""Redo CM5310: multi-material (glossy black body + chrome central spout) per gate D. Blender 4.5."""
import bpy, sys, os, math
from mathutils import Vector

a = sys.argv[sys.argv.index("--") + 1:]
in_fbx, out_glb, preview = a[0], a[1], a[2]

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=in_fbx)
ms = [o for o in bpy.context.scene.objects if o.type == 'MESH']
for o in bpy.context.scene.objects:
    o.select_set(False)
for o in ms:
    o.select_set(True)
bpy.context.view_layer.objects.active = ms[0]
if len(ms) > 1:
    bpy.ops.object.join()
obj = bpy.context.view_layer.objects.active
bpy.ops.object.select_all(action='DESELECT'); obj.select_set(True)
bpy.context.view_layer.objects.active = obj
bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
bpy.ops.mesh.separate(type='LOOSE'); bpy.ops.object.mode_set(mode='OBJECT')
parts = [o for o in bpy.context.scene.objects if o.type == 'MESH']

def bb(o):
    cs = [o.matrix_world @ Vector(c) for c in o.bound_box]
    mn = Vector((min(c[i] for c in cs) for i in range(3)))
    mx = Vector((max(c[i] for c in cs) for i in range(3)))
    return mn, mx

gmn = Vector((1e9, 1e9, 1e9)); gmx = Vector((-1e9, -1e9, -1e9))
for o in parts:
    mn, mx = bb(o)
    for i in range(3):
        gmn[i] = min(gmn[i], mn[i]); gmx[i] = max(gmx[i], mx[i])
cen = (gmn + gmx) / 2; zext = gmx.z - gmn.z

def mat_black():
    m = bpy.data.materials.new("Body"); m.use_nodes = True
    b = m.node_tree.nodes.get("Principled BSDF")
    b.inputs["Base Color"].default_value = (0.008, 0.008, 0.011, 1)
    b.inputs["Metallic"].default_value = 0.0
    b.inputs["Roughness"].default_value = 0.28
    for cn in ("Coat Weight", "Clearcoat", "Clearcoat Weight"):
        if cn in b.inputs:
            b.inputs[cn].default_value = 0.5
    return m

def mat_chrome():
    m = bpy.data.materials.new("Chrome"); m.use_nodes = True
    b = m.node_tree.nodes.get("Principled BSDF")
    b.inputs["Base Color"].default_value = (0.82, 0.82, 0.85, 1)
    b.inputs["Metallic"].default_value = 1.0
    b.inputs["Roughness"].default_value = 0.12
    return m

black, chrome = mat_black(), mat_chrome()
nchrome = 0
for o in parts:
    mn, mx = bb(o); c = (mn + mx) / 2; xw = mx.x - mn.x
    is_spout = (xw < 0.13 and abs(c.x - cen.x) < 0.07 and c.y < gmn.y + 0.17
                and (gmn.z + 0.06) < c.z < (gmn.z + 0.78 * zext))
    o.data.materials.clear()
    o.data.materials.append(chrome if is_spout else black)
    if is_spout:
        nchrome += 1
        print("CHROME part c=", [round(v, 3) for v in c], "xw=", round(xw, 3))
print("PARTS", len(parts), "CHROME", nchrome)

bpy.ops.export_scene.gltf(filepath=out_glb, export_format='GLB', use_selection=False)
print("GLB", os.path.isfile(out_glb), os.path.getsize(out_glb))

scn = bpy.context.scene
cam_d = bpy.data.cameras.new("c"); cam = bpy.data.objects.new("c", cam_d)
scn.collection.objects.link(cam); scn.camera = cam; cam_d.lens = 60
ctr = cen; rad = max(gmx - gmn)
def rf(vec, path):
    cam.location = ctr + Vector(vec)
    d = ctr - cam.location
    cam.rotation_euler = d.to_track_quat('-Z', 'Y').to_euler()
    scn.render.filepath = path; bpy.ops.render.render(write_still=True)
for ang, en in [((55, 0, 35), 3.5), ((60, 0, -120), 1.4), ((-50, 0, 150), 3.0)]:
    ld = bpy.data.lights.new("L", type='SUN'); ld.energy = en
    lo = bpy.data.objects.new("L", ld); scn.collection.objects.link(lo)
    lo.rotation_euler = tuple(math.radians(x) for x in ang)
world = bpy.data.worlds.new("W"); scn.world = world; world.use_nodes = True
world.node_tree.nodes["Background"].inputs[0].default_value = (0.4, 0.4, 0.43, 1)
try:
    scn.render.engine = 'BLENDER_EEVEE_NEXT'
except Exception:
    scn.render.engine = 'BLENDER_WORKBENCH'
scn.render.resolution_x = 640; scn.render.resolution_y = 900
scn.render.image_settings.file_format = 'PNG'
base = os.path.splitext(preview)[0]
rf((0, -2.0 * rad, 0.22 * rad), base + "_front.png")
rf((rad * 1.2, -rad * 1.7, rad * 0.9), base + "_3q.png")
print("DONE")
