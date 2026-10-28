using TMPro;

namespace SevenUI
{
    // The design canvas's header text: small grey spaced capitals in the bold face, used for panel headers and the
    // difficulty name.
    internal static class HeaderStyle
    {
        // In HUD canvas units, where the game draws body text at 12.
        public const float FontSize = 11f;
        // TMP character spacing is in hundredths of an em: 1.5px at 12px.
        public const float Tracking = 12.5f;

        public static void Apply(TMP_Text label)
        {
            label.fontSize = FontSize;
            label.fontStyle = (label.fontStyle | FontStyles.UpperCase | FontStyles.Bold) & ~FontStyles.Italic;
            label.characterSpacing = Tracking;
            label.color = Palette.HeaderText;
        }
    }
}
