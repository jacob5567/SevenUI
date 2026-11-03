using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace SevenUI
{
    // Splits a description LookingGlass wrote with its item stats into the description and the stats, so they can be
    // shown apart as the canvas's tooltip does. LookingGlass writes the whole in a size tag, the description, and then,
    // after a line like "With another stack, you will have:" in a picker, one line per stat:
    // <color="white">Label: </color><style="cIsDamage">45%</style> (equipment's without the color tag). Text it did
    // not write has no such lines and is left whole as the description.
    internal static class ItemStatsText
    {
        private static readonly Regex StatLine = new Regex(
            "\\n(?:<color=\"white\">)?(?<label>[^\\n<]*?)(?:</color>)?<style=\"\\w+\">(?<value>[^<\\n]*)</style>",
            RegexOptions.Compiled);
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
                parts.Description = text;
                return parts;
            }
            foreach (Match line in lines)
            {
                parts.Labels.Add(line.Groups["label"].Value.TrimEnd(LabelEnd));
                parts.Values.Add(line.Groups["value"].Value);
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
    }
}
