using System.Collections.Generic;
using System.Text.RegularExpressions;
using RoR2;
using UnityEngine;

namespace SevenUI
{
    // Copies of the run timer's format as on the design canvas, "24:51": without the hundredths, and with only the
    // digits set evenly apart. The game's format sets every character, the colons too, half an em apart, which spaces
    // the colons widely in Barlow Condensed. Its digits differ in width, so they stay evenly set, at the widest one's
    // width, and the timer doesn't shift as it counts. Made once per format and shared by every HUD.
    internal static class TightTimerFormat
    {
        // Barlow Condensed's widest digit, 4, is 0.459 em.
        private const string DigitSpacing = "<mspace=0.46em>";
        private const string DigitSpacingEnd = "</mspace>";
        private const string NameSuffix = " (SevenUI)";
        private static readonly Regex SpacingTags = new Regex("</?mspace[^>]*>");

        private static readonly Dictionary<TimerStringFormatter, TimerStringFormatter> Copies =
            new Dictionary<TimerStringFormatter, TimerStringFormatter>();
        private static readonly HashSet<TimerStringFormatter> Made = new HashSet<TimerStringFormatter>();

        public static bool IsTight(TimerStringFormatter format) => format && Made.Contains(format);

        public static TimerStringFormatter Of(TimerStringFormatter original)
        {
            if (!original || Made.Contains(original)) return original;
            if (Copies.TryGetValue(original, out TimerStringFormatter copy) && copy) return copy;
            copy = Object.Instantiate(original);
            copy.name = original.name + NameSuffix;
            TimerStringFormatter.Format format = TimerStringFormatter.Format.Clone(original.format);
            format.prefix = Strip(format.prefix);
            format.suffix = Strip(format.suffix);
            if (format.units != null)
            {
                for (int i = 0; i < format.units.Length; i++)
                {
                    TimerStringFormatter.Format.Unit unit = format.units[i];
                    if (unit.conversionRate < 1.0)
                    {
                        // Parts of a second, which the formatter skips with no digits allowed.
                        unit.minDigits = 0;
                        unit.maxDigits = 0;
                    }
                    else
                    {
                        unit.prefix = Strip(unit.prefix) + DigitSpacing;
                        unit.suffix = DigitSpacingEnd + Strip(unit.suffix);
                    }
                    format.units[i] = unit;
                }
            }
            copy.format = format;
            Copies[original] = copy;
            Made.Add(copy);
            return copy;
        }

        private static string Strip(string text) => string.IsNullOrEmpty(text) ? text : SpacingTags.Replace(text, "");
    }
}
