using System;
using RoR2.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SevenUI
{
    // A scoreboard row's line under its name block, as on the design canvas's option F: the portraits of up to four of
    // the player's ally kinds, stacked and overlapping, then the number of allies ("7 allies"), as a small outlined
    // button that opens the player's allies under the board (ScoreboardAllies). Its outline is the accent's while that
    // panel shows this player. A player without allies has "No allies" there instead, so the rows stay alike; the line
    // is hidden while no player has any.
    //
    // It is the game's kind of button (MPButton), as the row's own are, with the row's UIInputPassthrough. While any
    // UI object is selected the game takes the player's input away unless that object lets it through
    // (PlayerCharacterMasterController via LocalUser.isUIFocused), and only an MPButton's selection is dropped once it is
    // hidden (MPEventSystem.ValidateCurrentSelectedGameobject). A plain Unity button stayed selected after a click,
    // even with the scoreboard closed, and took every key away until the player clicked elsewhere.
    internal sealed class ScoreboardAlliesSummary : MonoBehaviour
    {
        public const string SummaryName = "SevenUIAlliesSummary";

        private const float Canvas = HudUnits.PerCanvasPixel;

        // The canvas's sizes, in its pixels, scaled to the HUD's units (HudUnits).
        private const float PaddingLeft = 3f * Canvas;
        private const float PaddingRight = 8f * Canvas;
        private const float Cell = 20f * Canvas;
        private const float CellBorder = 2f * Canvas;
        // How much of each portrait the next covers.
        private const float Overlap = 7f * Canvas;
        private const float TextGap = 6f * Canvas;
        private const float TextSize = 13f * Canvas;
        private const float OutlineEdge = 1f * Canvas;
        private const int MaxStacked = 4;
        private const string AlliesWord = " allies";
        private const string AllyWord = " ally";
        private const string NoAllies = "No allies";
        private static readonly Color Rest = new Color(1f, 1f, 1f, 0.04f);

        private readonly RawImage[] portraits = new RawImage[MaxStacked];
        private readonly RectTransform[] cells = new RectTransform[MaxStacked];
        private Image face;
        private MPButton button;
        private TMP_Text label;
        private PixelOutline outline;
        private int shownShape = -1;
        private int shownTotal = -1;
        private TMP_FontAsset shownFont;
        private bool shownOpen;

        // Read by the SevenUI.Tests plugin.
        public bool Clickable => button && button.interactable && gameObject.activeSelf;
        public string Text => label ? label.text : "";
        public int Stacked { get; private set; }
        public bool Lit => outline && outline.color == Palette.Accent;
        public Button Button => button;

        // Under the row's name block ('texts', the mod's holder of its lines), in the font of 'like'.
        public static ScoreboardAlliesSummary Add(Transform texts, TMP_Text like, Action onClick)
        {
            RectTransform rect = UiFactory.NewObject(SummaryName, texts);
            rect.gameObject.AddComponent<LayoutIgnorer>();
            ScoreboardAlliesSummary summary = rect.gameObject.AddComponent<ScoreboardAlliesSummary>();
            summary.Build(rect, like, onClick);
            rect.gameObject.SetActive(false);
            return summary;
        }

        private void Build(RectTransform rect, TMP_Text like, Action onClick)
        {
            face = rect.gameObject.AddComponent<Image>();
            face.color = Color.white;
            // MPButton finds its event system through this in its Awake, so it goes first.
            rect.gameObject.AddComponent<MPEventSystemLocator>();
            button = rect.gameObject.AddComponent<MPButton>();
            // As on the game's scoreboard row: while selected, the player can still move.
            rect.gameObject.AddComponent<UIInputPassthrough>().OnlyAllowMovement = true;
            button.targetGraphic = face;
            ColorBlock colors = button.colors;
            colors.normalColor = Rest;
            colors.highlightedColor = Palette.HoverFace;
            colors.selectedColor = Palette.HoverFace;
            colors.pressedColor = Palette.HoverFace;
            colors.disabledColor = Color.clear;
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0f;
            button.colors = colors;
            button.onClick.AddListener(() => onClick());
            for (int i = 0; i < MaxStacked; i++)
            {
                // Each portrait on a dark square a little larger, which reads as a border where the next overlaps it.
                Image border = UiFactory.NewImage("Portrait" + i, rect, Palette.KeyLip);
                cells[i] = border.rectTransform;
                RectTransform slot = UiFactory.NewImage("Slot", cells[i], Palette.ItemSlot).rectTransform;
                UiFactory.Stretch(slot, Vector2.zero, Vector2.one, new Vector2(CellBorder, CellBorder),
                    new Vector2(-CellBorder, -CellBorder));
                portraits[i] = UiFactory.NewObject("Picture", slot).gameObject.AddComponent<RawImage>();
                portraits[i].raycastTarget = false;
                UiFactory.Stretch(portraits[i].rectTransform);
            }
            // Earlier portraits over later ones, as the canvas stacks them.
            for (int i = MaxStacked - 1; i >= 0; i--)
            {
                cells[i].SetAsLastSibling();
            }
            label = UiFactory.NewText("Count", rect, like, TextSize, Palette.HeaderText);
            label.alignment = TextAlignmentOptions.MidlineLeft;
            UiFactory.KeepOnOneLine(label);
            outline = PixelOutline.Add(rect, "Outline", Palette.Divider, OutlineEdge);
        }

        // The player's allies, or "No allies" without any; hidden while the rows have no summary line. Changes only
        // what differs from the last call.
        public void Show(ScoreboardAllies.PlayerAllies allies, bool line, bool open)
        {
            if (gameObject.activeSelf != line)
            {
                gameObject.SetActive(line);
            }
            if (!line) return;
            int total = allies != null ? allies.Total : 0;
            int shape = allies != null ? allies.Shape : 0;
            if (total != shownTotal || shape != shownShape || label.font != shownFont)
            {
                shownTotal = total;
                shownShape = shape;
                shownFont = label.font;
                Draw(allies, total);
            }
            if (open != shownOpen || outline.enabled != total > 0)
            {
                shownOpen = open;
                outline.enabled = total > 0;
                outline.color = open ? Palette.Accent : Palette.Divider;
            }
        }

        private void Draw(ScoreboardAllies.PlayerAllies allies, int total)
        {
            bool any = total > 0;
            button.interactable = any;
            face.enabled = any;
            face.raycastTarget = any;
            Stacked = any ? Mathf.Min(MaxStacked, allies.Kinds.Count) : 0;
            float x = PaddingLeft;
            float height = ScoreboardStyler.AlliesLineHeight;
            for (int i = 0; i < MaxStacked; i++)
            {
                bool shown = i < Stacked;
                if (cells[i].gameObject.activeSelf != shown)
                {
                    cells[i].gameObject.SetActive(shown);
                }
                if (!shown) continue;
                portraits[i].texture = allies.Kinds[i].Portrait;
                StatsSection.TopLeft(cells[i], x, (height - Cell) / 2f, Cell, Cell);
                x += Cell - Overlap;
            }
            if (Stacked > 0)
            {
                x += Overlap + TextGap;
            }
            else
            {
                x = 0f;
            }
            label.text = any
                ? "<color=#" + ColorUtility.ToHtmlStringRGB(Palette.Text) + "><b>" + total + "</b></color>" +
                  (total == 1 ? AllyWord : AlliesWord)
                : NoAllies;
            label.color = any ? Palette.HeaderText : Palette.HeaderSubtle;
            float textWidth = label.GetPreferredValues(label.text, float.PositiveInfinity, height).x;
            StatsSection.TopLeft(label.rectTransform, x, 0f, textWidth, height);
            float width = x + textWidth + (any ? PaddingRight : 0f);
            StatsSection.TopLeft((RectTransform)transform, 0f, ScoreboardStyler.NameBlockHeight + ScoreboardStyler.AlliesLineGap,
                width, height);
        }
    }
}
