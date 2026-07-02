import bpy, os, glob

# GLB -> FBX for the appliance categories, mirroring convert_fridges.py.
# Each category's .glb files become .fbx under Assets/3DModels/<Category>/FromCatalog/.
cats = ["Hoods", "Cooktops", "CoffeeMachines", "Microwaves", "Dishwashers"]
base_src = r'C:\Unity-Git\RoomRevive\HTML_Editor\3D-models'
base_dst = r'C:\Unity-Git\RoomRevive\RoomRevive_unity\Assets\3DModels'

total = 0
for cat in cats:
    src = os.path.join(base_src, cat)
    dst = os.path.join(base_dst, cat, 'FromCatalog')
    os.makedirs(dst, exist_ok=True)
    files = sorted(glob.glob(os.path.join(src, '*.glb')))
    print('CATEGORY %s: %d glb' % (cat, len(files)))
    for f in files:
        name = os.path.splitext(os.path.basename(f))[0]
        bpy.ops.wm.read_factory_settings(use_empty=True)
        bpy.ops.import_scene.gltf(filepath=f)
        out = os.path.join(dst, name + '.fbx')
        bpy.ops.export_scene.fbx(filepath=out, path_mode='STRIP', embed_textures=False)
        total += 1
        print('CONVERTED %d [%s]: %s' % (total, cat, name))

print('DONE total=%d' % total)
