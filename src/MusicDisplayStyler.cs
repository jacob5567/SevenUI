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

        // MusicDisplay puts its text on the user's canvas when it first shows a track and never moves it: the HUD's
        // canvas if that was in a run, but the application's main canvas, which outlives every scene, if it was in the
        // menus, as it is when the title music plays. So both are looked in. Read by the SevenUI.Tests plugin.
        internal static TMP_Text Find()
        {
            LocalUser user = LocalUserManager.GetFirstLocalUser();
            Canvas canvas = user != null ? AchievementNotificationPanel.GetUserCanvas(user) : null;
            Transform found = canvas ? canvas.transform.Find(TextName) : null;
            if (!found)
            {
                Canvas main = RoR2Application.instance ? RoR2Application.instance.mainCanvas : null;
                found = main ? main.transform.Find(TextName) : null;
            }
            return found ? found.GetComponent<TMP_Text>() : null;
        }

        public void Describe(StringBuilder output)
        {
            output.AppendLine($"  music display: text {(text ? text.name : "not found")}, track '{nowPlaying.Name}' by '{nowPlaying.Author}'");
        }
    }
}
