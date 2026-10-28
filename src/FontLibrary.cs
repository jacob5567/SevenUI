using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;

namespace SevenUI
{
    // The Barlow font assets, baked once each on first use from the TTFs embedded in the DLL, and the retargeted
    // copies of the game's text materials.
    internal static class FontLibrary
    {
        private const string MediumResource = "SevenUI.fonts.Barlow-Medium.ttf";
        private const string SemiBoldResource = "SevenUI.fonts.Barlow-SemiBold.ttf";
        private const string CondensedResource = "SevenUI.fonts.BarlowCondensed-SemiBold.ttf";

        // TMP's font weight table is indexed by weight / 100, so 7 is bold (700).
        private const int BoldWeightIndex = 7;

        private static bool baked;
        private static TMP_FontAsset body;
        private static bool condensedBaked;
        private static TMP_FontAsset condensed;
        private static readonly Dictionary<(Material, TMP_FontAsset), Material> RetargetedMaterials =
            new Dictionary<(Material, TMP_FontAsset), Material>();

        // Barlow Medium with Barlow SemiBold as its bold, or null if baking failed. The template material is a game
        // font's, so the baked asset's own material uses the game's text shader.
        public static TMP_FontAsset Body(Material template)
        {
            if (!baked)
            {
                baked = true;
                body = BakeEmbedded(MediumResource, "Barlow Medium", template);
                TMP_FontAsset semiBold = BakeEmbedded(SemiBoldResource, "Barlow SemiBold", template);
                if (body && semiBold)
                {
                    body.fontWeightTable[BoldWeightIndex].regularTypeface = semiBold;
                }
            }
            return body;
        }

        // Barlow Condensed SemiBold, for numbers such as the run timer, or null if baking failed.
        public static TMP_FontAsset Condensed(Material template)
        {
            if (!condensedBaked)
            {
                condensedBaked = true;
                condensed = BakeEmbedded(CondensedResource, "Barlow Condensed SemiBold", template);
            }
            return condensed;
        }

        // A copy of a game text material (keeping its underlay or outline) that samples the given font's atlas.
        public static Material Retarget(Material source, TMP_FontAsset font)
        {
            if (!RetargetedMaterials.TryGetValue((source, font), out Material material) || !material)
            {
                material = FontBaker.RetargetMaterial(source, font);
                RetargetedMaterials[(source, font)] = material;
            }
            return material;
        }

        // Makes sure glyphs Barlow lacks are drawn from the game font the text used before.
        public static void AddFallback(TMP_FontAsset font, TMP_FontAsset fallback)
        {
            if (font.fallbackFontAssetTable == null)
            {
                font.fallbackFontAssetTable = new List<TMP_FontAsset>();
            }
            if (!font.fallbackFontAssetTable.Contains(fallback))
            {
                font.fallbackFontAssetTable.Add(fallback);
            }
            TMP_FontAsset bold = font.fontWeightTable[BoldWeightIndex].regularTypeface;
            if (bold && bold != font)
            {
                AddFallback(bold, fallback);
            }
        }

        private static TMP_FontAsset BakeEmbedded(string resource, string name, Material template)
        {
            using (Stream stream = typeof(FontLibrary).Assembly.GetManifestResourceStream(resource))
            {
                if (stream == null)
                {
                    SevenUIPlugin.Log.LogError($"The font resource {resource} is missing from the DLL");
                    return null;
                }
                using (MemoryStream bytes = new MemoryStream())
                {
                    stream.CopyTo(bytes);
                    return FontBaker.Bake(bytes.ToArray(), name, template);
                }
            }
        }
    }
}
