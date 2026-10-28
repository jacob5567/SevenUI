using UnityEngine;

namespace SevenUI
{
    // Plain sprites drawn in code: a white one for flat bars, and a sliced square outline.
    internal static class FlatSprite
    {
        // The outline texture's size and border, in pixels. At 100 pixels per unit the border is this many HUD canvas
        // units wide.
        private const int OutlineTextureSize = 8;
        private const int OutlineBorder = 2;
        private const float PixelsPerUnit = 100f;

        private static Sprite white;
        private static Sprite outline;

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
    }
}
