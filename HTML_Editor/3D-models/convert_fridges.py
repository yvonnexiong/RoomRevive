import bpy, os, glob

src = r'C:\Unity-Git\RoomRevive\HTML_Editor\3D-models\Fridges'
dst = r'C:\Unity-Git\RoomRevive\RoomRevive_unity\Assets\3DModels\Fridges\FromCatalog'
os.makedirs(dst, exist_ok=True)

count = 0
for f in sorted(glob.glob(os.path.join(src, '*.glb'))):
    name = os.path.splitext(os.path.basename(f))[0]
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=f)
    out = os.path.join(dst, name + '.fbx')
    bpy.ops.export_scene.fbx(filepath=out, path_mode='STRIP', embed_textures=False)
    count += 1
    print('CONVERTED %d: %s' % (count, name))

print('DONE total=%d' % count)
