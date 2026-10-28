using System.IO;
using RoR2;
using UnityEngine;

namespace SevenUI
{
    // Draws the floating damage numbers in JetBrains Mono instead of the game's pixel digits. They are particles
    // whose shader picks each character from a sheet of digits (texDamageNumbers), so swapping the sheet for one in
    // the same layout (tools/make_damage_numbers.py) changes the font. The damage number manager is one for the whole
    // game, not part of a HUD, and the sheet is set on its shared material.
    //
    // Each particle is four characters wide (start size 4 by 1) and the shader gives each character a square. The
    // game's pixel digits are square; these are narrower, so the sheet stretches them to fill their squares and the
    // particle is narrowed to match. The renderer holds the particle's width at a fixed share of the screen (its
    // min and max particle size are equal), so that share is narrowed too, or the digits would grow taller instead.
    internal static class DamageNumberFont
    {
        private const string SheetResource = "SevenUI.textures.DamageNumbers.png";
        private const string SheetProperty = "_MainTex";
        // A character's width over its height, CELL_WIDTH / CELL in tools/make_damage_numbers.py.
        private const float CellAspect = 100f / 128f;

        private static Texture2D sheet;
        private static bool sheetLoaded;
        private static ParticleSystemRenderer renderer;
        private static Material material;
        private static Texture original;
        private static float originalSizeX;
        private static float originalMinSize;
        private static float originalMaxSize;

        public static bool Applied => material && sheet && material.GetTexture(SheetProperty) == sheet;

        public static void Apply()
        {
            DamageNumberManager manager = DamageNumberManager.instance;
            ParticleSystemRenderer current = manager ? manager.GetComponent<ParticleSystemRenderer>() : null;
            ParticleSystem particles = manager ? manager.GetComponent<ParticleSystem>() : null;
            if (!current || !current.sharedMaterial || !particles) return;
            Texture2D mono = Sheet();
            if (!mono) return;
            if (current.sharedMaterial.GetTexture(SheetProperty) == mono) return;

            renderer = current;
            material = current.sharedMaterial;
            original = material.GetTexture(SheetProperty);
            ParticleSystem.MainModule main = particles.main;
            originalSizeX = main.startSizeXMultiplier;
            originalMinSize = current.minParticleSize;
            originalMaxSize = current.maxParticleSize;

            material.SetTexture(SheetProperty, mono);
            main.startSizeXMultiplier = originalSizeX * CellAspect;
            current.minParticleSize = originalMinSize * CellAspect;
            current.maxParticleSize = originalMaxSize * CellAspect;
        }

        // Numbers already showing keep the width they started with; they fade within a second or two.
        public static void Restore()
        {
            if (material && original && material.GetTexture(SheetProperty) == sheet)
            {
                material.SetTexture(SheetProperty, original);
                if (renderer)
                {
                    renderer.minParticleSize = originalMinSize;
                    renderer.maxParticleSize = originalMaxSize;
                    ParticleSystem particles = renderer.GetComponent<ParticleSystem>();
                    if (particles)
                    {
                        ParticleSystem.MainModule main = particles.main;
                        main.startSizeXMultiplier = originalSizeX;
                    }
                }
            }
            renderer = null;
            material = null;
            original = null;
        }

        // Loaded like the game's sheet: no mipmaps, bilinear, clamped.
        private static Texture2D Sheet()
        {
            if (sheetLoaded) return sheet;
            sheetLoaded = true;
            using (Stream stream = typeof(DamageNumberFont).Assembly.GetManifestResourceStream(SheetResource))
            {
                if (stream == null)
                {
                    SevenUIPlugin.Log.LogError($"The damage number sheet {SheetResource} is missing from the DLL");
                    return null;
                }
                using (MemoryStream bytes = new MemoryStream())
                {
                    stream.CopyTo(bytes);
                    Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    if (!texture.LoadImage(bytes.ToArray(), true))
                    {
                        SevenUIPlugin.Log.LogError("The damage number sheet couldn't be loaded");
                        return null;
                    }
                    texture.name = "SevenUIDamageNumbers";
                    texture.filterMode = FilterMode.Bilinear;
                    texture.wrapMode = TextureWrapMode.Clamp;
                    sheet = texture;
                }
            }
            return sheet;
        }
    }
}
