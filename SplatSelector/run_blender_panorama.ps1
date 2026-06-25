# Runs Blender headless to render the 360 panorama from render_panorama.py.
# Renders the SAVED scene file (close/save Blender first if you changed it).

$blender = "C:\Program Files\Blender Foundation\Blender 4.5\blender.exe"
$blend   = "C:\Users\chril\Downloads\YinanKitchen.blend"
$script  = "C:\Unity-Git\SplatSelector\render_panorama.py"
$output  = "C:\Unity-Git\SplatSelector\panorama_from_cube.png"

if (Test-Path $output) { Remove-Item $output -Force }

Write-Host "Rendering panorama with Blender (headless)..."
& $blender --background $blend --python $script --log-level 0

if (Test-Path $output) {
    Write-Host "DONE -> $output"
} else {
    Write-Host "No image produced - check the Blender output above."
}
