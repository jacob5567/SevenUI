using System.Text;
using BepInEx.Bootstrap;
using RoR2;
using RoR2.UI;
using TMPro;
using UnityEngine;

namespace SevenUI
{
    // Takes the MusicDisplay mod's now-playing text, when it is installed, off the screen, to be drawn in the right info
    // bar's now-playing panel (NowPlayingPanel): the text's rendering is turned off, nothing else of it touched, and
    // what it says is published as NowPlaying. MusicDisplay makes one text object on the local user's notification canvas, outside
    // the HUD, and fades its alpha. Nothing of MusicDisplay is referenced, only the object's name; the text is only
    // read, and turned back on in Restore.
    internal sealed class MusicDisplayStyler : IStyler
    {
        public const string PluginGuid = "com.DestroyedClone.MusicDisplay";
        public const string TextName = "MusicNameDisplay";

        private readonly NowPlaying nowPlaying;
        private readonly StyleRecorder recorder = new StyleRecorder();
        private TMP_Text text;

        public MusicDisplayStyler(NowPlaying nowPlaying)
        {
            this.nowPlaying = nowPlaying;
        }

        public bool Enabled => Chainloader.PluginInfos.ContainsKey(PluginGuid) && SevenUIPlugin.Settings.FlatPanels.Value;

        // The text is found in Update: it is made by the other mod, in its own time.
        public void Apply()
        {
        }

        // The text drawn again at once, and no track.
        public void Restore()
        {
            recorder.Restore();
            text = null;
            nowPlaying.Clear();
        }

        // Every frame: a lookup until the text is found, then an assignment and a string read. The text is turned off
        // again each frame, as the mod may turn it on when it shows a track. A disabled text keeps its string.
        public void Update()
        {
            if (!text)
            {
                text = Find();
                if (!text)
                {
                    nowPlaying.Clear();
                    return;
                }
            }
            recorder.Disable(text);
            nowPlaying.Read(text.text);
        }

        private static TMP_Text Find()
        {
            LocalUser user = LocalUserManager.GetFirstLocalUser();
            Canvas canvas = user != null ? AchievementNotificationPanel.GetUserCanvas(user) : null;
            Transform found = canvas ? canvas.transform.Find(TextName) : null;
            return found ? found.GetComponent<TMP_Text>() : null;
        }

        public void Describe(StringBuilder output)
        {
            output.AppendLine($"  music display: text {(text ? text.name : "not found")}, track '{nowPlaying.Name}' by '{nowPlaying.Author}'");
        }
    }
}
