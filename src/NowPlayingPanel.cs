using System.Collections.Generic;
using System.Text;
using BepInEx.Bootstrap;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SevenUI
{
    // The now-playing panel at the bottom of the right info bar, under the run info's other panels: a flat panel with
    // a header strip like the objective panel's, the track's name and under it its author, each on one line. It is
    // made here, in the bar's own layout, so it sits below whatever is there; the MusicDisplay mod's text is hidden by
    // MusicDisplayStyler, which reads the track into NowPlaying. The panel is hidden when there is no track.
    internal sealed class NowPlayingPanel : IStyler
    {
        public const string PanelName = "SevenUINowPlayingPanel";
        private const string RightInfoBarName = "RightInfoBar";
        private const string HeaderStripName = "SevenUIHeaderStrip";
        private const string HeaderName = "SevenUIHeader";
        private const string NameName = "SevenUITrackName";
        private const string AuthorName = "SevenUITrackAuthor";
        private const string HeaderText = "NOW PLAYING";

        // The objective rows' size and padding: 16px text, rows 8px apart, ends 14px inside the panel.
        private const float Canvas = RunInfoUnits.PerCanvasPixel;
        private const float BodyFontSize = ObjectiveRow.FontSize;
        private const float BodyInset = 14f * Canvas;
        private const float LineGap = 8f * Canvas / 2f;
        private const float BottomPadding = 4f;

        private readonly Transform root;
        private readonly NowPlaying nowPlaying;
        private readonly StyleRecorder recorder = new StyleRecorder();
        private readonly List<Transform> scratch = new List<Transform>();
        private Transform bar;
        private Transform panel;
        private TMP_Text nameText;
        private TMP_Text authorText;
        private string shownName;
        private string shownAuthor;
        private bool shownActive;

        public NowPlayingPanel(Transform root, NowPlaying nowPlaying)
        {
            this.root = root;
            this.nowPlaying = nowPlaying;
        }

        public bool Enabled => Chainloader.PluginInfos.ContainsKey(MusicDisplayStyler.PluginGuid) &&
                               SevenUIPlugin.Settings.FlatPanels.Value;

        // The panel is made in Update, once the bar and a game font to start the texts in are there.
        public void Apply()
        {
        }

        public void Restore()
        {
            recorder.Restore();
            bar = null;
            panel = null;
            nameText = null;
            authorText = null;
            shownName = null;
            shownAuthor = null;
            shownActive = false;
        }

        // Every frame: cheap reads. The texts are rewritten only when the track changes; the panel is kept last, as
        // other mods (LookingGlass) add their panels to the bar later.
        public void Update()
        {
            if (!panel)
            {
                panel = null;
                if (!bar)
                {
                    bar = FindBar();
                }
                if (!bar || !Build())
                {
                    return;
                }
            }
            if (panel.GetSiblingIndex() != panel.parent.childCount - 1)
            {
                panel.SetAsLastSibling();
            }

            bool present = nowPlaying.Present;
            if (present && (nowPlaying.Name != shownName || nowPlaying.Author != shownAuthor))
            {
                shownName = nowPlaying.Name;
                shownAuthor = nowPlaying.Author;
                nameText.text = shownName;
                authorText.text = shownAuthor;
                authorText.gameObject.SetActive(shownAuthor.Length > 0);
            }
            if (present != shownActive)
            {
                shownActive = present;
                panel.gameObject.SetActive(present);
            }
        }

        private Transform FindBar()
        {
            Transform cluster = root.Find(HudPaths.UpperRightCluster);
            if (!cluster) return null;
            cluster.GetComponentsInChildren(true, scratch);
            Transform found = null;
            foreach (Transform transform in scratch)
            {
                if (transform.name == RightInfoBarName)
                {
                    found = transform;
                    break;
                }
            }
            scratch.Clear();
            return found;
        }

        // False until the bar has a text in the game's font to copy.
        private bool Build()
        {
            TMP_Text like = bar.GetComponentInChildren<TMP_Text>(true);
            if (!like) return false;

            RectTransform rect = UiFactory.NewObject(PanelName, bar);
            panel = rect;
            recorder.Add(rect.gameObject);
            UiFactory.Flatten(rect.gameObject.AddComponent<Image>(), Palette.Panel);
            rect.GetComponent<Image>().raycastTarget = false;
            VerticalLayoutGroup layout = rect.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(0, 0, 0, Mathf.RoundToInt(BottomPadding));
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            RectTransform strip = UiFactory.NewObject(HeaderStripName, rect);
            Image stripImage = strip.gameObject.AddComponent<Image>();
            stripImage.color = Palette.HeaderStrip;
            stripImage.raycastTarget = false;
            strip.gameObject.AddComponent<LayoutIgnorer>();
            strip.anchorMin = new Vector2(0f, 1f);
            strip.anchorMax = new Vector2(1f, 1f);
            strip.pivot = new Vector2(0.5f, 1f);
            strip.anchoredPosition = Vector2.zero;
            strip.sizeDelta = new Vector2(0f, RightInfoBarStyler.HeaderHeight);

            // Started in the game's font, so the font swap gives each Barlow like the rest of the HUD.
            TMP_Text header = NewLine(HeaderName, rect, like, RightInfoBarStyler.HeaderFontSize, Palette.HeaderText);
            HeaderStyle.Apply(header);
            header.fontSize = RightInfoBarStyler.HeaderFontSize;
            header.alignment = TextAlignmentOptions.MidlineLeft;
            header.margin = new Vector4(RightInfoBarStyler.HeaderInset, 0f, RightInfoBarStyler.HeaderInset, 0f);
            header.text = HeaderText;
            LayoutElement headerElement = header.GetComponent<LayoutElement>();
            headerElement.minHeight = RightInfoBarStyler.HeaderHeight;
            headerElement.preferredHeight = RightInfoBarStyler.HeaderHeight;

            nameText = NewLine(NameName, rect, like, BodyFontSize, Palette.Text);
            nameText.margin = new Vector4(BodyInset, LineGap, BodyInset, LineGap);
            authorText = NewLine(AuthorName, rect, like, BodyFontSize, Palette.HeaderText);
            authorText.margin = new Vector4(BodyInset, 0f, BodyInset, LineGap);

            shownName = null;
            shownAuthor = null;
            shownActive = true;
            rect.gameObject.SetActive(false);
            shownActive = false;
            return true;
        }

        // One line that ends in an ellipsis rather than wrapping or widening the panel: it takes the panel's width
        // whatever its own.
        private static TMP_Text NewLine(string name, Transform parent, TMP_Text like, float size, Color color)
        {
            TMP_Text text = UiFactory.NewText(name, parent, like, size, color);
            // In the game's font even when the text it is made like is already in Barlow, so turning Barlow off puts
            // this one back in the game's font too.
            if (HudFontSwapper.GameFont && text.font != HudFontSwapper.GameFont)
            {
                text.font = HudFontSwapper.GameFont;
                text.fontSharedMaterial = HudFontSwapper.GameMaterial ? HudFontSwapper.GameMaterial : HudFontSwapper.GameFont.material;
            }
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            LayoutElement element = text.gameObject.AddComponent<LayoutElement>();
            element.preferredWidth = 0f;
            element.flexibleWidth = 1f;
            return text;
        }

        public void Describe(StringBuilder output)
        {
            output.AppendLine($"  now playing panel: {(panel ? (panel.gameObject.activeSelf ? "shown" : "hidden") : "not built")}, track '{nowPlaying.Name}' by '{nowPlaying.Author}'");
        }
    }
}
