using UnityEngine;
using UIImage = UnityEngine.UI.Image;

public static class RoundedUISpriteUtility
{
    public static Sprite CreateRoundedSprite(int size = 128, int radius = 24)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = $"Generated_RoundedSprite_{size}_{radius}";
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        Color clear = new Color(1f, 1f, 1f, 0f);
        Color white = Color.white;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float px = x + 0.5f;
                float py = y + 0.5f;

                float cx = Mathf.Clamp(px, radius, size - radius);
                float cy = Mathf.Clamp(py, radius, size - radius);

                float distance = Vector2.Distance(
                    new Vector2(px, py),
                    new Vector2(cx, cy)
                );

                float alpha = Mathf.Clamp01(radius + 0.5f - distance);

                Color pixel = Color.Lerp(clear, white, alpha);
                texture.SetPixel(x, y, pixel);
            }
        }

        texture.Apply();

        Vector4 border = new Vector4(radius, radius, radius, radius);

        return Sprite.Create(
            texture,
            new Rect(0, 0, size, size),
            new Vector2(0.5f, 0.5f),
            100f,
            0,
            SpriteMeshType.FullRect,
            border
        );
    }

    public static void ApplyRoundedCorners(UIImage image, Color color, int radius = 24)
    {
        if (image == null) return;

        image.sprite = CreateRoundedSprite(128, radius);
        image.type = UIImage.Type.Sliced;
        image.color = color;
    }
}