using System.Collections.Generic;
using System.Diagnostics;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore;
using UnityEngine.TextCore.LowLevel;

namespace SevenUI
{
    // Turns a TrueType file into a static TextMeshPro font asset at runtime. The game's TMP can only build font assets
    // from a Unity Font object, which can't be loaded from a file, so this renders the SDF atlas with FontEngine's
    // internal (publicized) glyph methods and fills in the asset the way TMP_FontAsset.CreateFontAsset does.
    internal static class FontBaker
    {
        private const int SamplingPointSize = 64;
        private const int AtlasPadding = 6;
        private const int SmallAtlasSize = 1024;
        private const int LargeAtlasSize = 2048;

        // Basic Latin, Latin-1 Supplement, Latin Extended-A, and the punctuation and symbols the HUD uses.
        private static readonly (uint first, uint last)[] CharacterRanges =
        {
            (0x0020, 0x007E),
            (0x00A0, 0x017F),
            (0x2010, 0x2027),
            (0x2030, 0x2030),
            (0x2032, 0x2033),
            (0x20AC, 0x20AC),
            (0x2122, 0x2122),
            (0x2190, 0x2193),
            (0x2212, 0x2212),
        };

        // Returns null if the font can't be loaded or its glyphs don't fit the largest atlas.
        public static TMP_FontAsset Bake(byte[] fontFile, string name, Material template)
        {
            Stopwatch timer = Stopwatch.StartNew();
            FontEngine.InitializeFontEngine();
            FontEngineError error = FontEngine.LoadFontFace(fontFile, SamplingPointSize);
            if (error != FontEngineError.Success)
            {
                SevenUIPlugin.Log.LogError($"Couldn't load the font {name}: {error}");
                return null;
            }
            FaceInfo faceInfo = FontEngine.GetFaceInfo();

            List<uint> glyphIndexes = new List<uint>();
            List<(uint unicode, uint glyphIndex)> characters = new List<(uint, uint)>();
            HashSet<uint> seenGlyphs = new HashSet<uint>();
            foreach ((uint first, uint last) in CharacterRanges)
            {
                for (uint unicode = first; unicode <= last; unicode++)
                {
                    if (!FontEngine.TryGetGlyphIndex(unicode, out uint glyphIndex) || glyphIndex == 0) continue;
                    characters.Add((unicode, glyphIndex));
                    if (seenGlyphs.Add(glyphIndex))
                    {
                        glyphIndexes.Add(glyphIndex);
                    }
                }
            }

            foreach (int size in new[] { SmallAtlasSize, LargeAtlasSize })
            {
                TMP_FontAsset asset = TryBake(name, faceInfo, glyphIndexes, characters, size, template);
                if (asset)
                {
                    if (SevenUIPlugin.Settings.DebugLogging.Value)
                    {
                        SevenUIPlugin.Log.LogInfo(
                            $"Baked {name}: {characters.Count} characters, {glyphIndexes.Count} glyphs, {size}x{size} atlas, {timer.ElapsedMilliseconds} ms");
                    }
                    return asset;
                }
            }
            SevenUIPlugin.Log.LogError($"The glyphs of {name} don't fit a {LargeAtlasSize}x{LargeAtlasSize} atlas");
            return null;
        }

        private static TMP_FontAsset TryBake(string name, FaceInfo faceInfo, List<uint> glyphIndexes,
            List<(uint unicode, uint glyphIndex)> characters, int atlasSize, Material template)
        {
            Texture2D atlas = new Texture2D(atlasSize, atlasSize, TextureFormat.Alpha8, false)
            {
                name = name + " Atlas",
            };
            FontEngine.ResetAtlasTexture(atlas);

            // TMP keeps one pixel free on the right and top edges of an SDF atlas.
            List<GlyphRect> freeRects = new List<GlyphRect> { new GlyphRect(0, 0, atlasSize - 1, atlasSize - 1) };
            List<GlyphRect> usedRects = new List<GlyphRect>();
            bool allFit = FontEngine.TryAddGlyphsToTexture(glyphIndexes, AtlasPadding, GlyphPackingMode.BestShortSideFit,
                freeRects, usedRects, GlyphRenderMode.SDFAA, atlas, out Glyph[] glyphs);
            if (!allFit)
            {
                Object.Destroy(atlas);
                return null;
            }
            atlas.Apply(false, true);

            TMP_FontAsset asset = ScriptableObject.CreateInstance<TMP_FontAsset>();
            asset.name = name;
            asset.m_Version = "1.1.0";
            asset.faceInfo = faceInfo;
            asset.atlasPopulationMode = AtlasPopulationMode.Static;
            asset.atlasWidth = atlasSize;
            asset.atlasHeight = atlasSize;
            asset.atlasPadding = AtlasPadding;
            asset.atlasRenderMode = GlyphRenderMode.SDFAA;
            asset.atlasTextures = new[] { atlas };
            asset.atlas = atlas;
            asset.freeGlyphRects = freeRects;
            asset.usedGlyphRects = usedRects;

            Dictionary<uint, Glyph> glyphsByIndex = new Dictionary<uint, Glyph>();
            foreach (Glyph glyph in glyphs)
            {
                if (glyph == null) break;
                glyph.atlasIndex = 0;
                asset.glyphTable.Add(glyph);
                glyphsByIndex[glyph.index] = glyph;
            }
            foreach ((uint unicode, uint glyphIndex) in characters)
            {
                if (glyphsByIndex.TryGetValue(glyphIndex, out Glyph glyph))
                {
                    asset.characterTable.Add(new TMP_Character(unicode, asset, glyph));
                }
            }

            asset.material = MakeMaterial(template, atlas, name);
            asset.ReadFontAssetDefinition();
            return asset;
        }

        // A copy of the game font's material (so its shader, underlay and outline carry over) pointed at our atlas.
        public static Material RetargetMaterial(Material source, TMP_FontAsset font)
        {
            return MakeMaterial(source, font.atlasTexture, source.name + " (" + font.name + ")");
        }

        private static Material MakeMaterial(Material template, Texture2D atlas, string name)
        {
            ShaderUtilities.GetShaderPropertyIDs();
            Material material = new Material(template) { name = name };
            material.SetTexture(ShaderUtilities.ID_MainTex, atlas);
            material.SetFloat(ShaderUtilities.ID_TextureWidth, atlas.width);
            material.SetFloat(ShaderUtilities.ID_TextureHeight, atlas.height);
            material.SetFloat(ShaderUtilities.ID_GradientScale, AtlasPadding + 1);
            return material;
        }
    }
}
