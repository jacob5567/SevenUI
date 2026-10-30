using System.Text.RegularExpressions;
using TMPro;

namespace SevenUI
{
    // Draws an objective's text without the game's inline icons, as the design canvas does: the teleporter's icon
    // after "Teleporter" pushes the most common objective onto a second line, and the word is already in red. A text
    // preprocessor, so the label's text stays the game's; the game writes the text again whenever it differs from the
    // objective's.
    internal sealed class ObjectiveIcons : ITextPreprocessor
    {
        private const string SpriteTag = "<sprite";
        private static readonly Regex Sprites = new Regex(@"\s*<sprite[^>]*>", RegexOptions.IgnoreCase);

        private readonly ITextPreprocessor previous;

        private ObjectiveIcons(ITextPreprocessor previous)
        {
            this.previous = previous;
        }

        public string PreprocessText(string text)
        {
            if (previous != null)
            {
                text = previous.PreprocessText(text);
            }
            if (string.IsNullOrEmpty(text) || text.IndexOf(SpriteTag, System.StringComparison.OrdinalIgnoreCase) < 0) return text;
            return Sprites.Replace(text, "");
        }

        // Keeps any preprocessor the label already has, and puts it back on restore.
        public static void Apply(TMP_Text label, StyleRecorder recorder)
        {
            if (label.textPreprocessor is ObjectiveIcons) return;
            ITextPreprocessor kept = label.textPreprocessor;
            recorder.OnRestore(() =>
            {
                if (!label) return;
                label.textPreprocessor = kept;
                label.SetAllDirty();
            });
            label.textPreprocessor = new ObjectiveIcons(kept);
            // The preprocessor runs when the text is parsed again, which a change of its own doesn't ask for.
            label.SetAllDirty();
        }

        // On a label the mod made, which goes with the restyle.
        public static void ApplyToCopy(TMP_Text label)
        {
            label.textPreprocessor = new ObjectiveIcons(null);
        }
    }
}
