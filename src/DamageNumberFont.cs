using System.Collections.Generic;
using RoR2;
using UnityEngine;

namespace SevenUI
{
    // Draws the floating damage numbers in JetBrains Mono instead of the game's pixel digits. They are particles
    // whose shader picks each character from a sheet of digits (texDamageNumbers), so swapping the sheet for one in
    // the same layout (tools/make_damage_numbers.py) changes the font. The sheet is set on the numbers' shared
    // material, which the damage number manager draws with, and also Lost Seer's Lenses' execute effect, for its
    // burst of fake crit numbers.
    //
    // The shader lays a number's characters (up to the material's _CharacterLimit) side by side in square cells as
    // tall as the particle, which is that many cells wide. The game's pixel digits are square; these are narrower,
    // so the sheet stretches them to fill their squares and every particle drawn with the material is narrowed to
    // match. The renderers hold the particles at a fixed share of the screen (their min and max particle size are
    // equal), so that share is narrowed by as much.
    //
    // The manager is part of the stage's scene and made anew each stage, and the effect comes from a pool of copies
    // of its prefab. So on each new manager, every particle system drawn with the material, the prefabs and pooled
    // copies included, is narrowed if it isn't yet; copies made later from a narrowed prefab are narrowed already.
    internal static class DamageNumberFont
    {
        private const string SheetResource = "SevenUI.textures.DamageNumbers.png";
        private const string SheetProperty = "_MainTex";
        private const string CharacterLimitProperty = "_CharacterLimit";
        // A character's width over its height, CELL_WIDTH / CELL in tools/make_damage_numbers.py.
        public const float CellAspect = 91f / 128f;
        // For comparing a particle's width over its height with the game's and the mod's.
        private const float AspectTolerance = 0.01f;

        private static Texture2D sheet;
        private static bool sheetLoaded;
        // The manager whose stage was last narrowed.
        private static DamageNumberManager manager;
        private static Material material;
        private static Texture original;

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

        // Every particle system drawn with the damage number material: live, pooled, or a loaded prefab.
        public static IEnumerable<(ParticleSystem particles, ParticleSystemRenderer renderer)> Drawing(Material numbers)
        {
            foreach (ParticleSystemRenderer renderer in Resources.FindObjectsOfTypeAll<ParticleSystemRenderer>())
            {
                if (renderer.sharedMaterial != numbers) continue;
                ParticleSystem particles = renderer.GetComponent<ParticleSystem>();
                if (particles)
                {
                    yield return (particles, renderer);
                }
            }
        }

        // A particle's width over its height, in its material's cells: the character limit for the game's, that
        // times CellAspect for the mod's. Zero if it has no separate width.
        public static float CellsWide(ParticleSystem particles)
        {
            ParticleSystem.MainModule main = particles.main;
            float height = main.startSizeYMultiplier;
            return main.startSize3D && height > 0f ? main.startSizeXMultiplier / height : 0f;
        }

        // A full scan of the loaded particle renderers, once a stage.
        private static void Apply()
        {
            DamageNumberManager current = DamageNumberManager.instance;
            ParticleSystemRenderer renderer = current ? current.GetComponent<ParticleSystemRenderer>() : null;
            if (!renderer || !renderer.sharedMaterial) return;
            Texture2D mono = Sheet();
            if (!mono) return;

            Material shared = renderer.sharedMaterial;
            if (shared.GetTexture(SheetProperty) != mono)
            {
                original = shared.GetTexture(SheetProperty);
                shared.SetTexture(SheetProperty, mono);
            }
            material = shared;
            Fit(shared, CellAspect);
            manager = current;
        }

        // Numbers already showing keep the width they started with; they fade within a second or two.
        private static void Restore()
        {
            Fit(material, 1f);
            if (original && material.GetTexture(SheetProperty) == sheet)
            {
                material.SetTexture(SheetProperty, original);
            }
            manager = null;
            material = null;
            original = null;
        }

        // Gives each particle system drawn with the material cells this share of their height wide, from the game's
        // square cells or the mod's narrowed ones. A system made to any other width is left as it is.
        private static void Fit(Material numbers, float cellAspect)
        {
            float limit = numbers.GetFloat(CharacterLimitProperty);
            float target = limit * cellAspect;
            foreach ((ParticleSystem particles, ParticleSystemRenderer renderer) in Drawing(numbers))
            {
                float wide = CellsWide(particles);
                bool known = Mathf.Abs(wide - limit) < AspectTolerance ||
                             Mathf.Abs(wide - limit * CellAspect) < AspectTolerance;
                if (!known || Mathf.Abs(wide - target) < AspectTolerance) continue;
                float scale = target / wide;
                ParticleSystem.MainModule main = particles.main;
                main.startSizeXMultiplier *= scale;
                renderer.minParticleSize *= scale;
                renderer.maxParticleSize *= scale;
            }
        }

        // Loaded like the game's sheet: no mipmaps, bilinear, clamped.
        private static Texture2D Sheet()
        {
            if (sheetLoaded) return sheet;
            sheetLoaded = true;
            byte[] bytes = UiFactory.ReadResource(SheetResource);
            if (bytes == null)
            {
                SevenUIPlugin.Log.LogError($"The damage number sheet {SheetResource} is missing from the DLL");
                return null;
            }
            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!texture.LoadImage(bytes, true))
            {
                SevenUIPlugin.Log.LogError("The damage number sheet couldn't be loaded");
                return null;
            }
            texture.name = "SevenUIDamageNumbers";
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            sheet = texture;
            return sheet;
        }
    }
}
