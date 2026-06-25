"""
Render a 360 equirectangular panorama from a fixed point in the CURRENT Blender scene.
Run it from inside Blender: Scripting workspace -> New -> paste -> Run (Alt+P),
or Text Editor -> Open -> this file -> Run.

It renders whatever is in your scene right now (uses the open session, not a saved file).
"""
import bpy, math

# ===================== EDIT THESE =====================
LOC       = (-0.678, -1.6929, 0.29034)   # camera position, Blender world coords (the cube's location)
YAW_DEG   = 0.0                           # spin around vertical (Z) to choose what faces the image centre
RES_X, RES_Y = 2048, 1024                 # equirectangular MUST be 2:1 (bump to 4096x2048 for final quality)
SAMPLES   = 64                            # Cycles samples (lower = faster, noisier)
OUTPUT    = r"C:\Unity-Git\SplatSelector\panorama_from_cube.png"
# =====================================================

scene = bpy.context.scene

# Panoramic cameras only render in Cycles (Eevee cannot do equirectangular).
scene.render.engine = 'CYCLES'
try:
    scene.cycles.samples = SAMPLES
except Exception:
    pass

# Try to render on the GPU (much faster). Falls back to CPU if no GPU backend.
try:
    prefs = bpy.context.preferences.addons['cycles'].preferences
    for backend in ('OPTIX', 'CUDA', 'HIP', 'ONEAPI', 'METAL'):
        try:
            prefs.compute_device_type = backend
            prefs.get_devices()
            if any(d.type != 'CPU' for d in prefs.devices):
                for d in prefs.devices:
                    d.use = (d.type != 'CPU')
                scene.cycles.device = 'GPU'
                print("Cycles GPU backend:", backend)
                break
        except Exception:
            continue
except Exception as e:
    print("GPU setup skipped:", e)

# Reuse the camera across runs so we don't pile up PanoCam.001, .002, ...
cam = bpy.data.objects.get("PanoCam")
if cam is None:
    cam = bpy.data.objects.new("PanoCam", bpy.data.cameras.new("PanoCam"))
    scene.collection.objects.link(cam)

cd = cam.data
cd.type = 'PANO'
# The equirectangular flag moved between Blender versions:
try:
    cd.panorama_type = 'EQUIRECTANGULAR'          # Blender 4.x
except Exception:
    cd.cycles.panorama_type = 'EQUIRECTANGULAR'   # Blender 3.x

cam.location = LOC
# (90 deg on X) stands the camera up so the horizon is centred and world +Z is up.
# At YAW 0 the image centre faces world +Y; change YAW_DEG to rotate the view.
cam.rotation_euler = (math.radians(90.0), 0.0, math.radians(YAW_DEG))
scene.camera = cam

# Hide any SMALL mesh that encloses the camera (the position-marker cube) so it
# doesn't box the camera in and render pure black.
import mathutils
p = mathutils.Vector(LOC)
for o in scene.objects:
    if o.type != 'MESH' or o is cam:
        continue
    bb = [o.matrix_world @ mathutils.Vector(c) for c in o.bound_box]
    xs = [v.x for v in bb]; ys = [v.y for v in bb]; zs = [v.z for v in bb]
    size = max(max(xs)-min(xs), max(ys)-min(ys), max(zs)-min(zs))
    encloses = (min(xs) <= p.x <= max(xs)) and (min(ys) <= p.y <= max(ys)) and (min(zs) <= p.z <= max(zs))
    if encloses and (size < 1.5 or o.name.lower().startswith('cube')):
        o.hide_render = True
        print("Hidden marker object from render:", o.name)

# The room textures already have lighting baked in, so show them as unlit emission
# (reproduces the captured photo look instead of a dark, re-lit render).
EMISSIVE = True
if EMISSIVE:
    for mat in bpy.data.materials:
        if not mat.use_nodes:
            continue
        nt = mat.node_tree
        out = next((n for n in nt.nodes if n.type == 'OUTPUT_MATERIAL'), None)
        bsdf = next((n for n in nt.nodes if n.type == 'BSDF_PRINCIPLED'), None)
        if not out:
            continue
        emis = nt.nodes.new('ShaderNodeEmission')
        if bsdf and bsdf.inputs['Base Color'].is_linked:
            nt.links.new(bsdf.inputs['Base Color'].links[0].from_socket, emis.inputs['Color'])
        elif bsdf:
            emis.inputs['Color'].default_value = bsdf.inputs['Base Color'].default_value
        nt.links.new(emis.outputs['Emission'], out.inputs['Surface'])
    # neutral view transform so colours match the source photos
    try:
        scene.view_settings.view_transform = 'Standard'
    except Exception:
        pass

scene.render.resolution_x = RES_X
scene.render.resolution_y = RES_Y
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = 'PNG'
scene.render.filepath = OUTPUT

bpy.ops.render.render(write_still=True)
print("Saved panorama ->", OUTPUT)
