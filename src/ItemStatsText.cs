using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace SevenUI
{
    // Splits a description LookingGlass wrote with its item stats into the description and the stats, so they can be
    // shown apart as the canvas's tooltip does. LookingGlass writes the whole in a size tag, the description, and then,
    // after a line like "With another stack, you will have:" in a picker, one line per stat:
    // <color="white">Label: </color><style="cIsDamage">45%</style> (equipment's without the color tag). Text it did
    // not write has no such lines and is left whole as the description.
    //
    // Ten of its labels carry numbers of their own in the game's styles (unquoted, <style=cIsHealing>), and some of
    // those have no value after them (LookingGlass's ItemDefinitions):
    //   Medkit:          Healing: <style=cIsHealing>20</style> + </color><style="cIsHealing">5% HP</style>
    //   Stun Grenade:    Stun Chance: <style=cIsDamage>0%</style> to <style=cIsDamage>25%</style>.</color>
    //   Molotov and the like: Proc Coefficients <style=cIsUtility>1.0</style> + ... <style=cSub>Rapid Contact</style>
    // The label is the words before its first number; the rest goes in front of the value, so Medkit's reads "Healing"
    // and "20 + 5% HP", and a line with no value of its own shows the rest as its value ("0% to 25%"). A line with no
    // value counts only in the white color LookingGlass gives item stats, or as a "Proc Coefficients" line, which its
    // equipment writes without the color; other text could be the game's description.
    internal static class ItemStatsText
    {
        // A label: plain text and the game's styled spans in it.
        private const string Label = "(?<label>(?:[^\\n<]|<style=\\w+>[^<\\n]*</style>)*?)";
        private static readonly Regex StatLine = new Regex(
            "\\n(?:(?:<color=\"white\">)?" + Label + "(?:</color>)?<style=\"\\w+\">(?<value>[^<\\n]*)</style>" +
            "|<color=\"white\">" + Label + "</color>(?=\\n|$)" +
            "|" + Label.Replace("(?<label>", "(?<label>" + ProcCoefficients) + "(?=\\n|$))",
            RegexOptions.Compiled);
        private const string ProcCoefficients = "Proc Coefficients ";
        private const string StyleStart = "<style=";
        private static readonly Regex Tag = new Regex("<[^>]*>", RegexOptions.Compiled);
        private const char SentenceEnd = '.';
        private static readonly Regex SizeWrapper = new Regex("^<size=[\\d.]+%>(?<inner>[\\s\\S]*)</size>$", RegexOptions.Compiled);
        private const string ParagraphBreak = "\n\n";
        private const char CaptionEnd = ':';
        private static readonly char[] LabelEnd = { ':', ' ' };

        internal sealed class Parts
        {
            public string Description = "";
            public string Caption = "";
            public readonly List<string> Labels = new List<string>();
            public readonly List<string> Values = new List<string>();

            public bool HasStats => Labels.Count > 0;
        }

        public static Parts Split(string text)
        {
            Parts parts = new Parts();
            if (string.IsNullOrEmpty(text)) return parts;
            Match wrapper = SizeWrapper.Match(text.Trim());
            string inner = wrapper.Success ? wrapper.Groups["inner"].Value : text;
            MatchCollection lines = StatLine.Matches(inner);
            if (lines.Count == 0)
            {
                parts.Description = wrapper.Success ? WithoutEmptyCaption(text, inner) : text;
                return parts;
            }
            foreach (Match line in lines)
            {
                (string label, string value) = LabelAndValue(line);
                parts.Labels.Add(label);
                parts.Values.Add(value);
            }
            string before = inner.Substring(0, lines[0].Index).TrimEnd();
            int lastBreak = before.LastIndexOf(ParagraphBreak, System.StringComparison.Ordinal);
            string tail = lastBreak >= 0 ? before.Substring(lastBreak + ParagraphBreak.Length) : "";
            if (tail.Length > 0 && tail.IndexOf('\n') < 0 && tail[tail.Length - 1] == CaptionEnd)
            {
                parts.Caption = tail;
                before = before.Substring(0, lastBreak).TrimEnd();
            }
            // Anything after the last stat (none from LookingGlass) stays with the description rather than being lost.
            Match last = lines[lines.Count - 1];
            string after = inner.Substring(last.Index + last.Length).Trim();
            parts.Description = after.Length > 0 ? before + ParagraphBreak + after : before;
            return parts;
        }

        // A stat line's label, without the numbers in it, and its value, with them in front.
        private static (string Label, string Value) LabelAndValue(Match line)
        {
            string label = line.Groups["label"].Value;
            string value = line.Groups["value"].Value;
            int styled = label.IndexOf(StyleStart, System.StringComparison.Ordinal);
            if (styled < 0) return (label.TrimEnd(LabelEnd), value);
            string rest = Tag.Replace(label.Substring(styled), "");
            return (label.Substring(0, styled).TrimEnd(LabelEnd), (rest + value).Trim().TrimEnd(SentenceEnd));
        }

        // The text with a caption put above its first stat as LookingGlass puts its own (a blank line after the
        // description, the caption, then the stats); the text as it was when it has no stats.
        public static string WithCaption(string text, string caption)
        {
            if (string.IsNullOrEmpty(text)) return text;
            Match first = StatLine.Match(text);
            return first.Success ? text.Insert(first.Index, "\n" + caption) : text;
        }

        // LookingGlass's Command descriptions are written without the player's master, so the stats it works out from
        // the master (Cautious Slug's regen, Tougher Times' block chance and a few more) fail, and it leaves the caption
        // with no stats under it. The caption is dropped then, as it heads nothing.
        private static string WithoutEmptyCaption(string text, string inner)
        {
            string trimmed = inner.TrimEnd();
            int lastBreak = trimmed.LastIndexOf(ParagraphBreak, System.StringComparison.Ordinal);
            if (lastBreak < 0) return text;
            string tail = trimmed.Substring(lastBreak + ParagraphBreak.Length);
            if (tail.Length == 0 || tail.IndexOf('\n') >= 0 || tail[tail.Length - 1] != CaptionEnd) return text;
            return trimmed.Substring(0, lastBreak).TrimEnd();
        }
    }
}
