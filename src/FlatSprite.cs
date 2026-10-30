using UnityEngine;

namespace SevenUI
{
    // Plain sprites drawn in code: a white one for flat bars, a sliced square outline, a sliced strip along the top, and
    // diagonal stripes to tile.
    internal static class FlatSprite
    {
        // The outline texture's size and border, in pixels. At 100 pixels per unit the border is this many HUD canvas
        // units wide.
        private const int OutlineTextureSize = 8;
        private const int OutlineBorder = 2;
        private const float PixelsPerUnit = 100f;
        // The top strip texture's height, and how much of it, from the top, is the strip: canvas units, as above.
        private const int TopStripTextureHeight = 8;
        private const int TopStripHeight = 6;

        // The stripes' tile, in pixels, half of it each color: at 100 pixels per unit, stripes about 3.5 canvas units
        // wide across.
        private const int HatchTextureSize = 10;

        private static Sprite white;
        private static Sprite hatch;
        private static Sprite outline;
        private static Sprite topStrip;

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
        public static Sprite Hatch
        {
            get
            {
                if (!hatch)
                {
                    Texture2D texture = new Texture2D(HatchTextureSize, HatchTextureSize, TextureFormat.RGBA32, false);
                    texture.name = "SevenUIHatch";
                    texture.filterMode = FilterMode.Bilinear;
                    texture.wrapMode = TextureWrapMode.Repeat;
                    for (int y = 0; y < HatchTextureSize; y++)
                    {
                        for (int x = 0; x < HatchTextureSize; x++)
                        {
                            bool stripe = (x - y + HatchTextureSize) % HatchTextureSize < HatchTextureSize / 2;
                            texture.SetPixel(x, y, stripe ? Palette.CurseStripe : Palette.CurseGap);
                        }
                    }
                    texture.Apply(false, true);
                    hatch = Sprite.Create(texture, new Rect(0f, 0f, HatchTextureSize, HatchTextureSize),
                        new Vector2(0.5f, 0.5f), PixelsPerUnit, 0, SpriteMeshType.FullRect);
                    hatch.name = "SevenUIHatch";
                }
                return hatch;
            }
        }
    }
}
