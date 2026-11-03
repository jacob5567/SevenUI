using UnityEngine;

namespace SevenUI
{
    // Plain sprites drawn in code: a white one for flat bars, a sliced square outline, a sliced square with rounded
    // corners, a sliced strip along the top, and diagonal stripes to tile, dark for a curse and green for one-shot
    // protection.
    internal static class FlatSprite
    {
        // The outline texture's size and border, in pixels. At 100 pixels per unit the border is this many HUD canvas
        // units wide.
        private const int OutlineTextureSize = 8;
        private const int OutlineBorder = 2;
        private const float PixelsPerUnit = 100f;
        // The rounded square's texture size and corner radius, in pixels; the corners are its slice borders.
        public const int RoundedRadius = 6;
        private const int RoundedTextureSize = 2 * RoundedRadius + 2;
        // The top strip texture's height, and how much of it, from the top, is the strip: canvas units, as above.
        private const int TopStripTextureHeight = 8;
        private const int TopStripHeight = 6;

        // The stripes' tile, in pixels, half of it each color: at 100 pixels per unit, stripes about 3.5 canvas units
        // wide across.
        private const int HatchTextureSize = 10;

        private static Sprite white;
        private static Sprite hatch;
        private static Sprite protectionHatch;
        private static Sprite tintedProtectionHatch;
        public const string ProtectionHatchName = "SevenUIProtectionHatch";
        public const string TintedProtectionHatchName = "SevenUIProtectionHatchTinted";
        private static Sprite outline;
        private static Sprite topStrip;
        private static Sprite rounded;

        // For flat bars the game draws from its own sprites. The game copies a bar's sprite to its image only when the
        // sprite changes, and a new bar image starts with its prefab's sprite, so "no sprite" would leave new bars
        // textured; a real sprite always gets copied.
        public static Sprite White
        {
            get
            {
                if (!white)
                {
                    Texture2D texture = Texture2D.whiteTexture;
                    white = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f));
                    white.name = "SevenUIFlat";
                }
                return white;
            }
        }

        // A white square outline for a sliced image, tinted by the image's color.
        public static Sprite Outline
        {
            get
            {
                if (!outline)
                {
                    Texture2D texture = new Texture2D(OutlineTextureSize, OutlineTextureSize, TextureFormat.RGBA32, false);
                    texture.name = "SevenUIOutline";
                    texture.filterMode = FilterMode.Point;
                    texture.wrapMode = TextureWrapMode.Clamp;
                    for (int y = 0; y < OutlineTextureSize; y++)
                    {
                        for (int x = 0; x < OutlineTextureSize; x++)
                        {
                            bool edge = x < OutlineBorder || y < OutlineBorder || x >= OutlineTextureSize - OutlineBorder ||
                                        y >= OutlineTextureSize - OutlineBorder;
                            texture.SetPixel(x, y, edge ? Color.white : Color.clear);
                        }
                    }
                    texture.Apply(false, true);
                    outline = Sprite.Create(texture, new Rect(0f, 0f, OutlineTextureSize, OutlineTextureSize),
                        new Vector2(0.5f, 0.5f), PixelsPerUnit, 0, SpriteMeshType.FullRect,
                        new Vector4(OutlineBorder, OutlineBorder, OutlineBorder, OutlineBorder));
                    outline.name = "SevenUIOutline";
                }
                return outline;
            }
        }

        // A white square with rounded, smoothed corners for a sliced image, tinted by the image's color. The corners are
        // RoundedRadius canvas units across at a pixels-per-unit multiplier of 1; a larger multiplier makes them smaller.
        public static Sprite Rounded
        {
            get
            {
                if (!rounded)
                {
                    Texture2D texture = new Texture2D(RoundedTextureSize, RoundedTextureSize, TextureFormat.RGBA32, false);
                    texture.name = "SevenUIRounded";
                    texture.filterMode = FilterMode.Bilinear;
                    texture.wrapMode = TextureWrapMode.Clamp;
                    float far = RoundedTextureSize - RoundedRadius;
                    for (int y = 0; y < RoundedTextureSize; y++)
                    {
                        for (int x = 0; x < RoundedTextureSize; x++)
                        {
                            // The pixel's middle, its distance from the nearest corner's circle center, and coverage
                            // from that across the circle's one-pixel edge.
                            float px = x + 0.5f;
                            float py = y + 0.5f;
                            float dx = Mathf.Max(RoundedRadius - px, px - far, 0f);
                            float dy = Mathf.Max(RoundedRadius - py, py - far, 0f);
                            float alpha = Mathf.Clamp01(RoundedRadius + 0.5f - Mathf.Sqrt(dx * dx + dy * dy));
                            texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                        }
                    }
                    texture.Apply(false, true);
                    rounded = Sprite.Create(texture, new Rect(0f, 0f, RoundedTextureSize, RoundedTextureSize),
                        new Vector2(0.5f, 0.5f), PixelsPerUnit, 0, SpriteMeshType.FullRect,
                        new Vector4(RoundedRadius, RoundedRadius, RoundedRadius, RoundedRadius));
                    rounded.name = "SevenUIRounded";
                }
                return rounded;
            }
        }

        // A white strip along the top of a sliced image, the same height however tall the image is, with nothing below
        // it. Tinted by the image's color.
        public static Sprite TopStrip
        {
            get
            {
                if (!topStrip)
                {
                    Texture2D texture = new Texture2D(1, TopStripTextureHeight, TextureFormat.RGBA32, false);
                    texture.name = "SevenUITopStrip";
                    texture.filterMode = FilterMode.Point;
                    texture.wrapMode = TextureWrapMode.Clamp;
                    for (int y = 0; y < TopStripTextureHeight; y++)
                    {
                        texture.SetPixel(0, y, y >= TopStripTextureHeight - TopStripHeight ? Color.white : Color.clear);
                    }
                    texture.Apply(false, true);
                    topStrip = Sprite.Create(texture, new Rect(0f, 0f, 1f, TopStripTextureHeight), new Vector2(0.5f, 0.5f),
                        PixelsPerUnit, 0, SpriteMeshType.FullRect, new Vector4(0f, 0f, 0f, TopStripHeight));
                    topStrip.name = "SevenUITopStrip";
                }
                return topStrip;
            }
        }
    
        // Dark stripes running up to the right, in the curse's colors, for a tiled image.
        public static Sprite Hatch => hatch ? hatch : hatch = MakeHatch("SevenUIHatch", Palette.CurseStripe, Palette.CurseGap);

        // The same stripes in greens, for the health one-shot protection covers.
        public static Sprite ProtectionHatch => protectionHatch
            ? protectionHatch
            : protectionHatch = MakeHatch(ProtectionHatchName, Palette.ProtectionStripe, Palette.ProtectionGap);

        // The same stripes in white and a grey as much darker as the green's gap, for an image tinted with the health's
        // color when the game draws the health in another (infusion's red, a void body's purple).
        public static Sprite TintedProtectionHatch => tintedProtectionHatch
            ? tintedProtectionHatch
            : tintedProtectionHatch = MakeHatch(TintedProtectionHatchName, Color.white, ProtectionGapShade);

        private static Color ProtectionGapShade
        {
            get
            {
                float shade = Palette.ProtectionGap.grayscale / Palette.ProtectionStripe.grayscale;
                return new Color(shade, shade, shade, 1f);
            }
        }

        private static Sprite MakeHatch(string name, Color stripeColor, Color gapColor)
        {
            Texture2D texture = new Texture2D(HatchTextureSize, HatchTextureSize, TextureFormat.RGBA32, false);
            texture.name = name;
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Repeat;
            for (int y = 0; y < HatchTextureSize; y++)
            {
                for (int x = 0; x < HatchTextureSize; x++)
                {
                    bool stripe = (x - y + HatchTextureSize) % HatchTextureSize < HatchTextureSize / 2;
                    texture.SetPixel(x, y, stripe ? stripeColor : gapColor);
                }
            }
            texture.Apply(false, true);
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, HatchTextureSize, HatchTextureSize),
                new Vector2(0.5f, 0.5f), PixelsPerUnit, 0, SpriteMeshType.FullRect);
            sprite.name = name;
            return sprite;
        }
    }
}
