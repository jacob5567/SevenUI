namespace SevenUI
{
    // The track the MusicDisplay mod's text shows, read by MusicDisplayStyler and drawn by the scoreboard's footer.
    // Empty when MusicDisplay is absent, its text is blank or the panels are off.
    internal sealed class NowPlaying
    {
        public string Name { get; private set; } = string.Empty;
        public string Author { get; private set; } = string.Empty;

        public bool Present => Name.Length > 0;

        // The text's first line is the track's name and the rest its author; blank text is no track.
        public void Read(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                Clear();
                return;
            }
            string trimmed = text.Trim();
            int split = trimmed.IndexOf('\n');
            Name = (split < 0 ? trimmed : trimmed.Substring(0, split)).Trim();
            Author = split < 0 ? string.Empty : trimmed.Substring(split + 1).Trim();
        }

        public void Clear()
        {
            Name = string.Empty;
            Author = string.Empty;
        }
    }
}
