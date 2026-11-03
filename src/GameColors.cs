using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using RoR2;
using UnityEngine;

namespace SevenUI
{
    // The game's colors for costs and item tiers (ColorCatalog), each with the canvas's color for the same thing: money,
    // health, coins and the tiers as the HUD shows them elsewhere. Built on first use, after the catalog is.
    internal static class GameColors
    {
        private static readonly (ColorCatalog.ColorIndex game, Color canvas)[] Pairs =
        {
            (ColorCatalog.ColorIndex.Money, Palette.Money),
            (ColorCatalog.ColorIndex.Blood, Palette.Debuff),
            (ColorCatalog.ColorIndex.LunarCoin, Palette.LunarCoin),
            (ColorCatalog.ColorIndex.VoidCoin, Palette.CountVoid),
            (ColorCatalog.ColorIndex.Tier1Item, Palette.Text),
            (ColorCatalog.ColorIndex.Tier2Item, Palette.CountUncommon),
            (ColorCatalog.ColorIndex.Tier3Item, Palette.CountLegendary),
            (ColorCatalog.ColorIndex.BossItem, Palette.CountBoss),
            (ColorCatalog.ColorIndex.LunarItem, Palette.CountLunar),
            (ColorCatalog.ColorIndex.VoidItem, Palette.CountVoid),
            (ColorCatalog.ColorIndex.Equipment, Palette.Equipment),
        };
        private static readonly Regex ColorTag = new Regex("<color=#(?<hex>[0-9A-Fa-f]{6})>");
        private static Dictionary<Color32, Color> canvasColors;

        // The canvas's color for a game color, or the game color itself when the canvas has none for it.
        public static Color ToCanvas(Color32 game)
        {
            game.a = byte.MaxValue;
            return Lookup().TryGetValue(game, out Color canvas) ? canvas : (Color)game;
        }

        // The first color tag's color in rich text, in the canvas's color; false without one.
        public static bool FirstTag(string text, out Color color)
        {
            Match match = ColorTag.Match(text ?? "");
            color = match.Success ? ToCanvas(Parse(match.Groups["hex"].Value)) : default;
            return match.Success;
        }

        // Rich text with each game color tag the canvas has a color for changed to that color.
        public static string Retag(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            return ColorTag.Replace(text, match =>
            {
                Color32 game = Parse(match.Groups["hex"].Value);
                return Lookup().TryGetValue(game, out Color canvas)
                    ? "<color=#" + ColorUtility.ToHtmlStringRGB(canvas) + ">"
                    : match.Value;
            });
        }

        private static Color32 Parse(string hex)
        {
            int value = int.Parse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            return new Color32((byte)(value >> 16), (byte)(value >> 8), (byte)value, byte.MaxValue);
        }

        private static Dictionary<Color32, Color> Lookup()
        {
            if (canvasColors != null) return canvasColors;
            canvasColors = new Dictionary<Color32, Color>(new Color32Comparer());
            foreach ((ColorCatalog.ColorIndex game, Color canvas) in Pairs)
            {
                Color32 color = ColorCatalog.GetColor(game);
                color.a = byte.MaxValue;
                canvasColors[color] = canvas;
            }
            return canvasColors;
        }
    }
}
