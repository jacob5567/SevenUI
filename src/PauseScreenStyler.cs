using System.Collections.Generic;
using System.Text;
using RoR2;
using RoR2.UI;
using RoR2.UI.SkinControllers;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SevenUI
{
    // Restyles the pause menu as on SevenUI's design: "PAUSED" in large condensed capitals over a line of the run (the
    // time, stage and level, and difficulty, copied from the run info), and the game's buttons as outlined buttons in
    // one centered stack, with Quit To Menu and Quit To Desktop side by side under the rest. The game makes the menu as
    // its own canvas, outside the HUD, each time it opens and destroys it when it closes, so this styles each new one
    // once and leaves it; the game's blurred backdrop, its submenus (settings, logbook) and the lobby panel keep their look.
    internal sealed class PauseScreenStyler
    {
        public const string TitleName = "SevenUIPauseTitle";
        public const string RunLineName = "SevenUIPauseRun";
        public const string OptionsName = "OptionsPanel (JUICED)";
        private const string LabelName = "ButtonText";
        private const string BaseOutlineName = "BaseOutline";
        private const string HoverOutlineName = "HoverOutline";
        private const string TimerPath = "TimerPanel/TimerText";
        public static readonly string[] QuitTokens = { "PAUSE_QUIT_TO_MENU", "PAUSE_QUIT_TO_DESKTOP" };
        private const string Separator = "   ";

        // In the pause canvas's units, which are a 1080-pixel-high screen's pixels: an 84px title with 4% tracking, the
        // run line 10px under it at 20px, the buttons 44px under that, 360 by 50 and 10px apart with 21px labels, and the
        // quits 18px under them, 42px high with 17px labels.
        private const float TitleSize = 84f;
        private const float TitleTracking = 4f;
        private const float RunLineGap = 10f;
        private const float RunLineSize = 20f;
        private const float ButtonsGap = 44f;
        public const float ButtonWidth = 360f;
        private const float ButtonHeight = 50f;
        private const float ButtonGap = 10f;
        private const float ButtonTextSize = 21f;
        private const float QuitsGap = 18f;
        private const float QuitHeight = 42f;
        private const float QuitTextSize = 17f;
        private const float OutlineEdge = 1f;
        // The share of its color a label keeps while its button can't be pressed (Resume, for a second after a
        // multiplayer pause), as in the item pickers.
        private const float UnavailableAlpha = 0.4f;

        private readonly HUD hud;
        private readonly Transform hudRoot;
        private readonly List<Entry> buttons = new List<Entry>();
        private PauseScreenController styled;
        private RectTransform options;
        private TMP_Text title;
        private TMP_Text runLine;
        private RunInfoText run;
        private TMP_Text timer;
        private string shownRun;
        private int shownActive = -1;

        private sealed class Entry
        {
            public HGButton Button;
            public Image Face;
            public TMP_Text Label;
            public PixelOutline Rest;
            public PixelOutline Hover;
            public bool Quit;
            public bool ShownHover;
            public bool ShownAvailable = true;
        }

        public PauseScreenStyler(HUD hud, Transform hudRoot)
        {
            this.hud = hud;
            this.hudRoot = hudRoot;
        }

        public bool Styled => styled;

        // Every frame while the panels are on: a list read while no menu is open; with one, a hover and an
        // availability read per button, an active count, and building and comparing the run line.
        public void Update(bool barlowFont)
        {
            PauseScreenController menu = PauseScreenController.instancesList.Count > 0 ? PauseScreenController.instancesList[0] : null;
            if (menu != styled)
            {
                buttons.Clear();
                styled = null;
                options = null;
                run = null;
                timer = null;
                shownRun = null;
                shownActive = -1;
                // Each HUD runs this; with several (split screen) the first to see the menu styles it.
                if (menu && menu.mainPanel && !menu.mainPanel.Find(OptionsName + "/" + TitleName))
                {
                    Style(menu, barlowFont);
                }
            }
            if (!styled || !options) return;
            int active = CountActive();
            if (active != shownActive)
            {
                shownActive = active;
                Layout();
            }
            foreach (Entry entry in buttons)
            {
                UpdateButton(entry);
            }
            string line = RunText();
            if (!string.Equals(line, shownRun, System.StringComparison.Ordinal))
            {
                shownRun = line;
                runLine.text = line;
            }
        }

        private void Style(PauseScreenController menu, bool barlowFont)
        {
            options = menu.mainPanel.Find(OptionsName) as RectTransform;
            if (!options) return;
            styled = menu;
            // The panel behind the buttons, and the layout that stacks them; the stack is laid out here instead. The
            // menu's own juice pans and sizes the main panel and this one as it opens, so they keep their places and
            // sizes, and everything is placed from this panel's middle.
            DisableImage(menu.mainPanel);
            DisableImage(options);
            VerticalLayoutGroup stack = options.GetComponent<VerticalLayoutGroup>();
            if (stack)
            {
                stack.enabled = false;
            }
            TMP_Text font = null;
            foreach (Transform child in options)
            {
                HGButton button = child.GetComponent<HGButton>();
                // Only the menu's own row of buttons: the lobby panel's buttons sit deeper.
                if (!button) continue;
                Entry entry = StyleButton(button);
                buttons.Add(entry);
                font = font ? font : entry.Label;
            }
            title = AddText(TitleName, font, TitleSize, Palette.Text);
            title.gameObject.AddComponent<CondensedText>();
            title.fontStyle = FontStyles.UpperCase;
            title.characterSpacing = TitleTracking;
            title.text = Language.GetString("PAUSE_PAUSED");
            runLine = AddText(RunLineName, font, RunLineSize, Palette.HeaderText);
            runLine.richText = true;
            // Found once: the HUD's run info outlives the menu.
            run = hudRoot ? hudRoot.GetComponentInChildren<RunInfoText>() : null;
            Transform panel = run && run.transform.parent ? run.transform.parent.parent : null;
            Transform timerObject = panel ? panel.Find(TimerPath) : null;
            timer = timerObject ? timerObject.GetComponent<TMP_Text>() : null;
            if (barlowFont)
            {
                new HudFontSwapper(hud, menu.transform).Apply();
            }
        }

        private Entry StyleButton(HGButton button)
        {
            Entry entry = new Entry { Button = button };
            LanguageTextMeshController language = button.GetComponent<LanguageTextMeshController>();
            entry.Quit = language && System.Array.IndexOf(QuitTokens, language.token) >= 0;
            // The game's skin puts its own look and text style back on the button every frame.
            ButtonSkinController skin = button.GetComponent<ButtonSkinController>();
            if (skin)
            {
                skin.enabled = false;
            }
            ColorBlock colors = button.colors;
            colors.normalColor = colors.highlightedColor = colors.pressedColor = colors.selectedColor = Color.white;
            button.colors = colors;
            entry.Face = button.image ? button.image : button.GetComponent<Image>();
            if (entry.Face)
            {
                entry.Face.sprite = null;
                entry.Face.type = Image.Type.Simple;
                entry.Face.material = null;
                entry.Face.color = Palette.Panel;
            }
            DisableImage(button.transform.Find(BaseOutlineName));
            DisableImage(button.transform.Find(HoverOutlineName));
            // The button turns this image back on every frame while it can be pressed.
            button.imageOnInteractable = null;
            Transform label = button.transform.Find(LabelName);
            entry.Label = label ? label.GetComponent<TMP_Text>() : button.GetComponentInChildren<TMP_Text>(true);
            if (entry.Label)
            {
                entry.Label.enableAutoSizing = false;
                entry.Label.enableWordWrapping = false;
                entry.Label.fontSize = entry.Quit ? QuitTextSize : ButtonTextSize;
                entry.Label.fontStyle = FontStyles.Normal;
                entry.Label.characterSpacing = 0f;
                entry.Label.margin = Vector4.zero;
                entry.Label.alignment = TextAlignmentOptions.Center;
                entry.Label.color = entry.Quit ? Palette.HeaderText : Palette.Text;
                RectTransform rect = entry.Label.rectTransform;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = rect.offsetMax = Vector2.zero;
            }
            entry.Rest = AddOutline(button.transform, Palette.Divider);
            entry.Rest.enabled = true;
            entry.Hover = AddOutline(button.transform, Palette.Accent);
            return entry;
        }

        // From the top of the block, which is centered on the panel's middle: the title, the run line, the stack, and
        // the quits in one row under it.
        private void Layout()
        {
            int stacked = 0;
            int quits = 0;
            foreach (Entry entry in buttons)
            {
                if (!entry.Button || !entry.Button.gameObject.activeSelf) continue;
                if (entry.Quit)
                {
                    quits++;
                }
                else
                {
                    stacked++;
                }
            }
            float titleHeight = title.GetPreferredValues("A").y;
            float runHeight = runLine.GetPreferredValues("A").y;
            float stackHeight = stacked > 0 ? stacked * ButtonHeight + (stacked - 1) * ButtonGap : 0f;
            float height = titleHeight + RunLineGap + runHeight + ButtonsGap + stackHeight + (quits > 0 ? QuitsGap + QuitHeight : 0f);
            float y = height / 2f;
            Place(title.rectTransform, 0f, y, ButtonWidth * 2f, titleHeight);
            y -= titleHeight + RunLineGap;
            Place(runLine.rectTransform, 0f, y, ButtonWidth * 2f, runHeight);
            y -= runHeight + ButtonsGap;
            float quitWidth = quits > 0 ? (ButtonWidth - (quits - 1) * ButtonGap) / quits : 0f;
            float quitX = -ButtonWidth / 2f + quitWidth / 2f;
            float quitY = y - stackHeight - QuitsGap;
            foreach (Entry entry in buttons)
            {
                if (!entry.Button || !entry.Button.gameObject.activeSelf) continue;
                RectTransform rect = (RectTransform)entry.Button.transform;
                if (entry.Quit)
                {
                    Place(rect, quitX, quitY, quitWidth, QuitHeight);
                    quitX += quitWidth + ButtonGap;
                }
                else
                {
                    Place(rect, 0f, y, ButtonWidth, ButtonHeight);
                    y -= ButtonHeight + ButtonGap;
                }
            }
        }

        private int CountActive()
        {
            int active = 0;
            foreach (Entry entry in buttons)
            {
                if (entry.Button && entry.Button.gameObject.activeSelf)
                {
                    active++;
                }
            }
            return active;
        }

        // The hovered button (the pointer's, or a gamepad's selection) gets the accent outline and a faint accent face,
        // and a quit's grey label turns white.
        private static void UpdateButton(Entry entry)
        {
            if (!entry.Button) return;
            bool hovered = entry.Button.hovering;
            if (hovered != entry.ShownHover)
            {
                entry.ShownHover = hovered;
                entry.Hover.enabled = hovered;
                entry.Rest.enabled = !hovered;
                if (entry.Face)
                {
                    entry.Face.color = hovered ? Palette.HoverFace : Palette.Panel;
                }
                if (entry.Label && entry.Quit)
                {
                    entry.Label.color = hovered ? Palette.Text : Palette.HeaderText;
                }
            }
            bool available = entry.Button.interactable;
            if (available != entry.ShownAvailable && entry.Label)
            {
                entry.ShownAvailable = available;
                entry.Label.alpha = available ? 1f : UnavailableAlpha;
            }
        }

        // The run info's time, stage and level, and difficulty, as the HUD shows them, so they agree with it on host and
        // client: the time and difficulty in bold, the difficulty in the accent.
        private string RunText()
        {
            if (!run) return "";
            StringBuilder line = new StringBuilder();
            if (timer && !string.IsNullOrEmpty(timer.text))
            {
                line.Append("<color=#").Append(ColorUtility.ToHtmlStringRGB(Palette.Text)).Append("><b>").Append(timer.text).Append("</b></color>");
            }
            string stage = run.StageAndLevel ? run.StageAndLevel.text : "";
            if (!string.IsNullOrEmpty(stage))
            {
                line.Append(line.Length > 0 ? Separator : "").Append(stage);
            }
            string difficulty = run.ShownDifficulty;
            if (!string.IsNullOrEmpty(difficulty))
            {
                line.Append(line.Length > 0 ? Separator : "").Append("<color=#").Append(ColorUtility.ToHtmlStringRGB(Palette.Accent))
                    .Append("><b>").Append(difficulty).Append("</b></color>");
            }
            return line.ToString();
        }

        private TMP_Text AddText(string name, TMP_Text font, float size, Color color)
        {
            TextMeshProUGUI text = UiFactory.NewObject(name, options).gameObject.AddComponent<TextMeshProUGUI>();
            if (font)
            {
                text.font = font.font;
                text.fontSharedMaterial = font.fontSharedMaterial;
            }
            text.fontSize = size;
            text.color = color;
            text.alignment = TextAlignmentOptions.Top;
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false;
            return text;
        }

        private static PixelOutline AddOutline(Transform button, Color color) =>
            PixelOutline.Add(button, "SevenUIPauseOutline", color, OutlineEdge);

        private static void DisableImage(Transform holder)
        {
            Image image = holder ? holder.GetComponent<Image>() : null;
            if (image)
            {
                image.enabled = false;
            }
        }

        // Centered across at x from the panel's middle, its top y above the middle.
        private static void Place(RectTransform rect, float x, float top, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(x, top);
            rect.sizeDelta = new Vector2(width, height);
        }

        public void Describe(StringBuilder output)
        {
            output.AppendLine($"  pause menu styled: {(bool)styled} ({buttons.Count} buttons)");
        }
    }
}
