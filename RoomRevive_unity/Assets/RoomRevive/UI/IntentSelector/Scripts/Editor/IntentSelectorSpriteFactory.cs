using System.IO;
using UnityEditor;
using UnityEngine;

namespace RoomRevive.IntentSelector.EditorTools
{
    /// <summary>
    /// Bakes the rounded-corner mask + 3 gradient card backgrounds that the legacy
    /// IntentCardSelectorUI generated at runtime. We persist them as PNGs so the
    /// new prefab-based system can reference them without runtime texture generation.
    /// Idempotent: re-running reuses existing assets.
    /// </summary>
    public static class IntentSelectorSpriteFactory
    {
        public const string GeneratedFolder = "Assets/RoomRevive/UI/IntentSelector/GeneratedSprites";

        public const int CardWidthPx = 260;
        public const int CardImageHpx = 236; // 260 / 1.1
        public const int CornerRadiusPx = 8;
        public const int RoundedMaskSize = 64;

        public static Sprite GetOrCreateRoundedMask()
        {
            EnsureFolder();
            string path = GeneratedFolder + "/RoundedMask_" + CornerRadiusPx + ".png";
            Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (existing != null) return existing;

            Texture2D tex = BuildRoundedMaskTexture(RoundedMaskSize, CornerRadiusPx);
            WritePng(tex, path);
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            ConfigureAsSlicedSprite(path, CornerRadiusPx);
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        public static Sprite GetOrCreateCalmGradient() => GetOrCreateGradient(
            "Card_Calm",
            new[] { Hex(0xE8DFCE), Hex(0xD4C8AC), Hex(0xB8A582) });

        public static Sprite GetOrCreateHostGradient() => GetOrCreateGradient(
            "Card_Host",
            new[] { Hex(0x5A4030), Hex(0x3D2A1C), Hex(0x2A1C12) });

        public static Sprite GetOrCreateFastGradient() => GetOrCreateGradient(
            "Card_Fast",
            new[] { Hex(0xC8CDD5), Hex(0xA8AEB8), Hex(0x88909C) });

        static Sprite GetOrCreateGradient(string name, Color[] stops)
        {
            EnsureFolder();
            string path = GeneratedFolder + "/" + name + ".png";
            Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (existing != null) return existing;

            Texture2D tex = BuildLinearGradientTexture(CardWidthPx, CardImageHpx, 160f, stops, new[] { 0f, 0.6f, 1f });
            WritePng(tex, path);
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            ConfigureAsSimpleSprite(path);
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        // ── Texture builders (ported from legacy IntentCardSelectorUI runtime gen) ──

        static Texture2D BuildRoundedMaskTexture(int size, float radius)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color32[] pixels = new Color32[size * size];
            float r = Mathf.Clamp(radius, 0f, size * 0.5f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float cx = x + 0.5f;
                    float cy = y + 0.5f;
                    float ex = Mathf.Max(r - cx, cx - (size - r), 0f);
                    float ey = Mathf.Max(r - cy, cy - (size - r), 0f);
                    float a = Mathf.Clamp01(r - Mathf.Sqrt(ex * ex + ey * ey) + 0.5f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }

        static Texture2D BuildLinearGradientTexture(int w, int h, float angleDeg, Color[] colors, float[] stops)
        {
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            Color32[] pixels = new Color32[w * h];

            float rad = angleDeg * Mathf.Deg2Rad;
            float dx = Mathf.Sin(rad);
            float dy = -Mathf.Cos(rad);

            float[] c = { 0, dx, dy, dx + dy };
            float minT = Mathf.Min(c[0], c[1], c[2], c[3]);
            float maxT = Mathf.Max(c[0], c[1], c[2], c[3]);
            float range = maxT - minT;

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float u = (x + 0.5f) / w;
                    float v = (y + 0.5f) / h;
                    float t = ((u * dx + v * dy) - minT) / range;

                    Color col = colors[0];
                    for (int i = 0; i < stops.Length - 1; i++)
                    {
                        if (t >= stops[i] && t <= stops[i + 1])
                        {
                            col = Color.Lerp(colors[i], colors[i + 1], (t - stops[i]) / (stops[i + 1] - stops[i]));
                            break;
                        }
                    }
                    if (t > stops[stops.Length - 1]) col = colors[colors.Length - 1];

                    pixels[y * w + x] = new Color32(
                        (byte)(col.r * 255 + 0.5f),
                        (byte)(col.g * 255 + 0.5f),
                        (byte)(col.b * 255 + 0.5f),
                        255);
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }

        // ── Asset importer config ──────────────────────────────────────────────

        static void ConfigureAsSlicedSprite(string path, int radius)
        {
            TextureImporter ti = (TextureImporter)AssetImporter.GetAtPath(path);
            if (ti == null) return;
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.alphaIsTransparency = true;
            ti.mipmapEnabled = false;
            ti.filterMode = FilterMode.Bilinear;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.spriteBorder = new Vector4(radius, radius, radius, radius);
            ti.SaveAndReimport();
        }

        static void ConfigureAsSimpleSprite(string path)
        {
            TextureImporter ti = (TextureImporter)AssetImporter.GetAtPath(path);
            if (ti == null) return;
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.alphaIsTransparency = false;
            ti.mipmapEnabled = false;
            ti.filterMode = FilterMode.Bilinear;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.SaveAndReimport();
        }

        static void WritePng(Texture2D tex, string assetPath)
        {
            byte[] data = tex.EncodeToPNG();
            File.WriteAllBytes(assetPath, data);
        }

        static void EnsureFolder()
        {
            if (AssetDatabase.IsValidFolder(GeneratedFolder)) return;
            string parent = Path.GetDirectoryName(GeneratedFolder).Replace('\\', '/');
            string leaf = Path.GetFileName(GeneratedFolder);
            if (!AssetDatabase.IsValidFolder(parent))
                throw new System.InvalidOperationException("Parent folder missing: " + parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        static Color Hex(uint rgb) => new Color(
            ((rgb >> 16) & 0xFF) / 255f,
            ((rgb >> 8) & 0xFF) / 255f,
            (rgb & 0xFF) / 255f);
    }
}
