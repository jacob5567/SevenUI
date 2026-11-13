using RoR2;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SevenUI
{
    // Karma in the run info, in place of the game's icon: a row under the difficulty bar of one segment per karma level,
    // filled up to the run's karma, with "KARMA 4" at its right where the bar has the difficulty's name. Laid out in the
    // difficulty bar's units, a row's height and gap below the bar's middle; RunInfoStyler shows it and makes room for
    // it while karma is on.
    internal sealed class KarmaRow
    {
        public const string RowName = "SevenUIKarma";
        public const string LabelName = "Label";
        public const string SegmentName = "Segment";
        // The game's word for Karma, from its rules.
        private const string KarmaToken = "RULE_HEADER_KARMA";
        // The design's sizes, in canvas pixels: 4px segments 2px apart, the label 10px past them.
        private const float Canvas = RunInfoUnits.PerCanvasPixel;
        private const float SegmentHeight = 4f * Canvas;
        private const float SegmentGap = 2f * Canvas;
        private const float LabelGap = 10f * Canvas;

        public readonly RectTransform Root;
        private readonly Image[] segments;
        private readonly TMP_Text label;
        private int shownKarma = -1;
        private TMP_FontAsset shownFont;
        private float shownWidth = -1f;

        // 'rowOffset' is how far the row's middle sits below the bar's; 'inset' how far its ends stay inside the bar's.
        public KarmaRow(RectTransform bar, TMP_Text fontFrom, float rowHeight, float rowOffset, float inset, float labelSize)
        {
            Root = UiFactory.NewObject(RowName, bar);
            Root.anchorMin = new Vector2(0f, 0.5f);
            Root.anchorMax = new Vector2(1f, 0.5f);
            Root.pivot = new Vector2(0.5f, 0.5f);
            Root.offsetMin = new Vector2(inset, -rowOffset - rowHeight / 2f);
            Root.offsetMax = new Vector2(-inset, -rowOffset + rowHeight / 2f);

            segments = new Image[KarmaManager.maxKarma];
            for (int i = 0; i < segments.Length; i++)
            {
                segments[i] = UiFactory.NewObject(SegmentName, Root).gameObject.AddComponent<Image>();
                segments[i].color = Palette.Trough;
                segments[i].raycastTarget = false;
            }

            RectTransform labelRect = UiFactory.NewObject(LabelName, Root);
            TextMeshProUGUI text = labelRect.gameObject.AddComponent<TextMeshProUGUI>();
            // Starts in the game's font, so the font swap gives it Barlow like the rest of the HUD.
            if (fontFrom)
            {
                text.font = fontFrom.font;
                text.fontSharedMaterial = fontFrom.fontSharedMaterial;
            }
            HeaderStyle.Apply(text);
            text.fontSize = labelSize;
            text.color = Palette.Karma;
            text.alignment = TextAlignmentOptions.MidlineRight;
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false;
            label = text;
            Root.gameObject.SetActive(false);
        }

        public bool Alive => Root;

        public int ShownKarma => shownKarma;

        public string ShownLabel => label.text;

        // Fills the segments up to the karma and writes its number; laid out again only when the number, the font or the
        // row's width changes. Does nothing while the row is inactive (its panel hidden), where the label measures far
        // too narrow and the width reads zero; the next Set once it is active lays it out.
        public void Set(int karma)
        {
            if (!Root || !Root.gameObject.activeInHierarchy) return;
            float width = Root.rect.width;
            if (karma == shownKarma && label.font == shownFont && Mathf.Approximately(width, shownWidth)) return;
            shownKarma = karma;
            shownFont = label.font;
            shownWidth = width;
            label.text = Language.GetString(KarmaToken) + " " + karma;
            for (int i = 0; i < segments.Length; i++)
            {
                segments[i].color = i < karma ? Palette.Karma : Palette.Trough;
            }
            Layout();
        }

        private void Layout()
        {
            float width = label.GetPreferredValues(label.text, float.PositiveInfinity, float.PositiveInfinity).x;
            RectTransform labelRect = label.rectTransform;
            labelRect.anchorMin = new Vector2(1f, 0f);
            labelRect.anchorMax = Vector2.one;
            labelRect.pivot = new Vector2(1f, 0.5f);
            labelRect.anchoredPosition = Vector2.zero;
            labelRect.sizeDelta = new Vector2(width, 0f);

            float room = Root.rect.width - width - LabelGap;
            float segment = (room - SegmentGap * (segments.Length - 1)) / segments.Length;
            for (int i = 0; i < segments.Length; i++)
            {
                RectTransform rect = segments[i].rectTransform;
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 0.5f);
                rect.anchoredPosition = new Vector2(i * (segment + SegmentGap), 0f);
                rect.sizeDelta = new Vector2(segment, SegmentHeight);
            }
        }
    }
}
