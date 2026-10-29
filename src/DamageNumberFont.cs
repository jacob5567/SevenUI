using System.IO;
using RoR2;
using UnityEngine;

namespace SevenUI
{
    // Draws the floating damage numbers in JetBrains Mono instead of the game's pixel digits. They are particles
    // whose shader picks each character from a sheet of digits (texDamageNumbers), so swapping the sheet for one in
    // the same layout (tools/make_damage_numbers.py) changes the font. The damage number manager is not part of a HUD;
    // the sheet is set on its shared material.
    //
    // Each particle is four characters wide (start size 4 by 1) and the shader gives each character a square. The
    // game's pixel digits are square; these are narrower, so the sheet stretches them to fill their squares and the
    // particle is narrowed to match. The renderer holds the particle at a fixed share of the screen (its min and max
    // particle size are equal), so that share is narrowed by as much.
    //
    // The manager is part of the stage's scene and is made anew each stage, but its material is an asset every
    // manager shares, so the material keeps the sheet while each new manager's particle has to be narrowed again.
    internal static class DamageNumberFont
    {
        private const string SheetResource = "SevenUI.textures.DamageNumbers.png";
        private const string SheetProperty = "_MainTex";
        // A character's width over its height, CELL_WIDTH / CELL in tools/make_damage_numbers.py.
        private const float CellAspect = 94f / 128f;

        private static Texture2D sheet;
        private static bool sheetLoaded;
        // The manager whose particle is narrowed.
        private static DamageNumberManager manager;
        private static Material material;
        private static Texture original;
        private static float originalSizeX;
        private static float originalMinSize;
        private static float originalMaxSize;

        public static bool Applied => manager && manager == DamageNumberManager.instance && material && sheet &&
                                      material.GetTexture(SheetProperty) == sheet;

        // Called every frame, since a stage's new manager changes nothing in the HUD. One read when nothing changed.
        public static void Update(bool monospace)
        {
            if (monospace)
            {
                if (DamageNumberManager.instance != manager)
                {
                    Apply();
                }
            }
            else if (material)
            {
                Restore();
            }
        }

        private static void Apply()
        {
            DamageNumberManager current = DamageNumberManager.instance;
            ParticleSystemRenderer renderer = current ? current.GetComponent<ParticleSystemRenderer>() : null;
            ParticleSystem particles = current ? current.GetComponent<ParticleSystem>() : null;
            if (!renderer || !renderer.sharedMaterial || !particles) return;
            Texture2D mono = Sheet();
            if (!mono) return;

            Material shared = renderer.sharedMaterial;
            if (shared.GetTexture(SheetProperty) != mono)
            {
                original = shared.GetTexture(SheetProperty);
                shared.SetTexture(SheetProperty, mono);
            }
            material = shared;

            // A new manager is a fresh copy of the game's prefab, so these are the game's sizes.
            ParticleSystem.MainModule main = particles.main;
            originalSizeX = main.startSizeXMultiplier;
            originalMinSize = renderer.minParticleSize;
            originalMaxSize = renderer.maxParticleSize;
            main.startSizeXMultiplier = originalSizeX * CellAspect;
            renderer.minParticleSize = originalMinSize * CellAspect;
            renderer.maxParticleSize = originalMaxSize * CellAspect;
            manager = current;
        }

        // Numbers already showing keep the width they started with; they fade within a second or two.
        private static void Restore()
        {
            if (material && original && material.GetTexture(SheetProperty) == sheet)
            {
                material.SetTexture(SheetProperty, original);
            }
            if (manager)
            {
                ParticleSystemRenderer renderer = manager.GetComponent<ParticleSystemRenderer>();
                ParticleSystem particles = manager.GetComponent<ParticleSystem>();
                if (renderer && particles)
                {
                    ParticleSystem.MainModule main = particles.main;
                    main.startSizeXMultiplier = originalSizeX;
                    renderer.minParticleSize = originalMinSize;
                    renderer.maxParticleSize = originalMaxSize;
                }
            }
            manager = null;
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
