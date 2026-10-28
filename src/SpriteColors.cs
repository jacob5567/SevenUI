using System.Collections.Generic;
using UnityEngine;

namespace SevenUI
{
    // The average color and opacity of a game sprite, for drawing it flat in the color it had. Game textures can't be
    // read from the CPU, so the sprite is drawn into a small render texture once and read back from that.
    internal static class SpriteColors
    {
        // The sprite is shrunk to this many pixels a side; enough for an average.
        private const int SampleSize = 16;

        private static readonly Dictionary<Sprite, Color> Averages = new Dictionary<Sprite, Color>();

        // RGB weighted by each pixel's opacity, and the mean opacity as alpha. White if the sprite can't be read.
        public static Color Average(Sprite sprite)
        {
            if (!sprite) return Color.white;
            if (Averages.TryGetValue(sprite, out Color average)) return average;
            average = Measure(sprite);
            Averages[sprite] = average;
            return average;
        }

        private static Color Measure(Sprite sprite)
        {
            Texture texture = sprite.texture;
            Rect rect;
            try
            {
                rect = sprite.textureRect;
            }
            catch (System.Exception)
            {
                // A tightly packed sprite has no rectangle of its own; its whole texture is close enough.
                rect = new Rect(0f, 0f, texture.width, texture.height);
            }

            RenderTexture target = RenderTexture.GetTemporary(SampleSize, SampleSize, 0, RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.sRGB);
            RenderTexture previous = RenderTexture.active;
            Texture2D readback = new Texture2D(SampleSize, SampleSize, TextureFormat.RGBA32, false);
            try
            {
                Graphics.Blit(texture, target, new Vector2(rect.width / texture.width, rect.height / texture.height),
                    new Vector2(rect.x / texture.width, rect.y / texture.height));
                RenderTexture.active = target;
                readback.ReadPixels(new Rect(0f, 0f, SampleSize, SampleSize), 0, 0);
                readback.Apply(false);
                Color32[] pixels = readback.GetPixels32();
                float r = 0f, g = 0f, b = 0f, alpha = 0f;
                foreach (Color32 pixel in pixels)
                {
                    float a = pixel.a / 255f;
                    r += pixel.r / 255f * a;
                    g += pixel.g / 255f * a;
                    b += pixel.b / 255f * a;
                    alpha += a;
                }
                if (alpha <= 0f) return Color.white;
                return new Color(r / alpha, g / alpha, b / alpha, alpha / pixels.Length);
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
                Object.Destroy(readback);
            }
        }
    }
}
