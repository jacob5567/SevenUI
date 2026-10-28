using UnityEngine;

namespace SevenUI
{
    // A plain white sprite, for flat bars the game draws from its own sprites. The game copies a bar's sprite to its
    // image only when the sprite changes, and a new bar image starts with its prefab's sprite, so "no sprite" would
    // leave new bars textured; a real sprite always gets copied.
    internal static class FlatSprite
    {
        private static Sprite white;

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
    }
}
