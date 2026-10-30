using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SevenUI
{
    // LookingGlass's stats as on the design canvas: under a header strip with the panel's title ("STATS") and
    // "LOOKINGGLASS", its entries in two columns, each a grey name with its value in bold white at the right. LookingGlass
    // writes all of them as one text, a title line and then a "Name: value" line each, several times a second; that text
    // stays in place, hidden, and is read again into the columns whenever it changes. A line without a colon is shown
    // as a name alone. The panel's height follows the columns, through a layout element that outranks LookingGlass's.
    internal sealed class StatsGrid : MonoBehaviour
    {
        private const char LineBreak = '\n';
        private const char NameEnd = ':';
        private static readonly Regex Tags = new Regex("<[^>]*>");

        public TMP_Text Source;
        public TMP_Text Title;
        public TMP_Text[] Names;
        public TMP_Text[] Values;
        public LayoutElement Height;
        // In the text's units: the rows' pitch, and the room above and below the rows.
        public float RowPitch;
        public float Around;

        private readonly List<(string name, string value)> entries = new List<(string, string)>();
        private readonly StringBuilder[] columnNames = { new StringBuilder(), new StringBuilder() };
        private readonly StringBuilder[] columnValues = { new StringBuilder(), new StringBuilder() };
        private string shownSource;

        public int ShownEntries => entries.Count;

        // A string compare when nothing changed; LookingGlass writes a new text a few times a second.
        private void LateUpdate()
        {
            if (!Source) return;
            string text = Source.text;
            if (ReferenceEquals(text, shownSource)) return;
            shownSource = text;
            Read(text);
        }

        private void Read(string text)
        {
            entries.Clear();
            string title = null;
            foreach (string line in (text ?? "").Split(LineBreak))
            {
                string plain = Tags.Replace(line, "").Trim();
                if (plain.Length == 0) continue;
                int end = plain.IndexOf(NameEnd);
                if (title == null && entries.Count == 0 && end < 0)
                {
                    title = plain;
                    continue;
                }
                entries.Add(end > 0 ? (plain.Substring(0, end).Trim(), plain.Substring(end + 1).Trim()) : (plain, ""));
            }
            if (title != null && Title.text != title)
            {
                Title.text = title;
            }

            string lineHeight = $"<line-height={RowPitch:0.###}>";
            for (int column = 0; column < 2; column++)
            {
                columnNames[column].Clear().Append(lineHeight);
                columnValues[column].Clear().Append(lineHeight);
            }
            for (int i = 0; i < entries.Count; i++)
            {
                int column = i % 2;
                if (i >= 2)
                {
                    columnNames[column].Append(LineBreak);
                    columnValues[column].Append(LineBreak);
                }
                columnNames[column].Append(entries[i].name);
                columnValues[column].Append(entries[i].value);
            }
            for (int column = 0; column < 2; column++)
            {
                Names[column].SetText(columnNames[column]);
                Values[column].SetText(columnValues[column]);
            }
            int rows = (entries.Count + 1) / 2;
            float height = Around + rows * RowPitch;
            Height.minHeight = height;
            Height.preferredHeight = height;
        }
    }
}
