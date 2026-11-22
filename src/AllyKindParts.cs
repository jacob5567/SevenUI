using System.Collections.Generic;
using RoR2;
using RoR2.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SevenUI
{
    // What the mod adds to the card a kind of ally shares when there is more than one of them: "×8" after the name, and
    // the health bar split into a segment per ally, each showing that ally's health, green or red under a quarter, in
    // place of the game's bar. The segments and their gaps land on whole screen pixels (PixelSpans), so every gap draws
    // as wide. Only what changed is set each frame.
    internal sealed class AllyKindParts
    {
        public const string SegmentsName = "SevenUIAllySegments";
        public const string CountName = "SevenUIAllyCount";

        // The design canvas's sizes, in its pixels, scaled to the HUD's units: 2px between segments, and the count 6px
        // after the name.
        private const float Canvas = HudUnits.PerCanvasPixel;
        private const float SegmentGap = 2f * Canvas;
        private const float CountGap = 6f * Canvas;
        // A segment turns red under this share of its ally's health, as the player's bar does.
        private const float LowHealthShare = 0.25f;

        private readonly TMP_Text count;
        private readonly RectTransform segments;
        private readonly List<Image> fills = new List<Image>();
        private readonly List<GameObject> slots = new List<GameObject>();
        private int shownCount = -1;
        private string shownName;
        private float[] shown = new float[0];
        private float[] spanLefts = new float[0];
        private float[] spanWidths = new float[0];
        private float placedLeft = float.NaN;
        private float placedRight = float.NaN;
        private int placedCount = -1;

        private AllyKindParts(TMP_Text count, RectTransform segments)
        {
            this.count = count;
            this.segments = segments;
        }

        // The count as a child of the name, so it follows it; the segments in the bars' column where the health bar
        // was, as tall as it. None for a card without a name or a bar.
        public static AllyKindParts Add(AllyCardController card)
        {
            if (!card.nameLabel || !card.healthBar) return null;
            RectTransform countRect = UiFactory.NewObject(CountName, card.nameLabel.transform);
            countRect.anchorMin = countRect.anchorMax = new Vector2(0f, 0.5f);
            countRect.pivot = new Vector2(0f, 0.5f);
            countRect.sizeDelta = new Vector2(0f, card.nameLabel.rectTransform.rect.height);
            TextMeshProUGUI text = countRect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = card.nameLabel.font;
            text.fontSharedMaterial = card.nameLabel.fontSharedMaterial;
            text.fontSize = card.nameLabel.fontSize;
            text.fontStyle = FontStyles.Bold;
            text.color = Palette.Accent;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false;
            countRect.gameObject.AddComponent<CondensedText>();

            Transform bar = card.healthBar.transform;
            RectTransform segments = UiFactory.NewObject(SegmentsName, bar.parent);
            segments.SetSiblingIndex(bar.GetSiblingIndex() + 1);
            LayoutElement barLayout = bar.GetComponent<LayoutElement>();
            LayoutElement layout = segments.gameObject.AddComponent<LayoutElement>();
            layout.preferredHeight = barLayout ? barLayout.preferredHeight : ((RectTransform)bar).rect.height;
            bar.gameObject.SetActive(false);
            return new AllyKindParts(text, segments);
        }

        public void Update(AllyCardController card, List<CharacterMaster> members)
        {
            // The game's dead mark sits outside the hidden bar, which alone turns it off, and only while active. The
            // game hands its cards to other allies as allies die, so a dead drone's card, mark shown, can become this
            // shared card of living allies; the mark is kept off here instead.
            Image dead = card.healthBar ? card.healthBar.deadImage : null;
            if (dead && dead.enabled)
            {
                dead.enabled = false;
            }
            int allies = members.Count;
            string name = card.nameLabel ? card.nameLabel.text : "";
            if (allies != shownCount || name != shownName)
            {
                shownCount = allies;
                shownName = name;
                count.text = "×" + allies;
                float nameWidth = card.nameLabel ? card.nameLabel.GetPreferredValues(name).x : 0f;
                count.rectTransform.anchoredPosition = new Vector2(nameWidth + CountGap, 0f);
                SetSegmentCount(allies);
            }
            PlaceSegments(allies);
            for (int i = 0; i < allies; i++)
            {
                CharacterBody body = members[i] ? members[i].GetBody() : null;
                float share = body && body.healthComponent ? Mathf.Clamp01(body.healthComponent.combinedHealthFraction) : 0f;
                if (Mathf.Abs(share - shown[i]) < 0.001f) continue;
                shown[i] = share;
                RectTransform fill = fills[i].rectTransform;
                fill.anchorMax = new Vector2(share, 1f);
                fills[i].color = share < LowHealthShare ? Palette.LowHealth : Palette.Health;
            }
        }

        // Back to the game's bar.
        public void Remove(AllyCardController card)
        {
            if (count)
            {
                Object.Destroy(count.gameObject);
            }
            if (segments)
            {
                Object.Destroy(segments.gameObject);
            }
            if (card && card.healthBar)
            {
                card.healthBar.gameObject.SetActive(true);
                // The bar turns the mark on or off only when its ally's state changes from what it last saw, which
                // may be from before it was hidden, so the mark is set to the ally's state now. That is the state of the
                // card's ally, not of the bar's source: when the game hands a destroyed ally's card on (a scrap, or a
                // Drone Combiner taking one of a pair), the bar may still follow the destroyed body this frame, and a
                // mark set from it would stay, as the bar then sees a living ally again, unchanged from what it last saw.
                HealthBar bar = card.healthBar;
                if (bar.deadImage && !bar.altSource)
                {
                    CharacterBody body = card.sourceMaster ? card.sourceMaster.GetBody() : null;
                    bar.deadImage.enabled = !(body && body.healthComponent && body.healthComponent.alive);
                }
            }
        }

        // Two projected corners a frame; the segments move only when the row's edges move by a pixel or the count
        // changes.
        private void PlaceSegments(int allies)
        {
            if (!segments) return;
            Camera eye = PixelSpans.Eye(segments);
            if (!PixelSpans.ScreenEdges(segments, eye, out float left, out float right)) return;
            left = Mathf.Round(left);
            right = Mathf.Round(right);
            if (allies == placedCount && left == placedLeft && right == placedRight) return;
            if (spanLefts.Length < allies)
            {
                spanLefts = new float[allies];
                spanWidths = new float[allies];
            }
            if (!PixelSpans.Split(segments, eye, SegmentGap, spanLefts, spanWidths, allies)) return;
            placedCount = allies;
            placedLeft = left;
            placedRight = right;
            for (int i = 0; i < allies; i++)
            {
                RectTransform slot = (RectTransform)slots[i].transform;
                slot.anchoredPosition = new Vector2(spanLefts[i], 0f);
                slot.sizeDelta = new Vector2(spanWidths[i], 0f);
            }
        }

        private void SetSegmentCount(int allies)
        {
            while (slots.Count < allies)
            {
                RectTransform slot = UiFactory.NewObject("Segment", segments);
                Image trough = slot.gameObject.AddComponent<Image>();
                trough.color = Palette.Trough;
                trough.raycastTarget = false;
                slot.anchorMin = Vector2.zero;
                slot.anchorMax = Vector2.up;
                slot.pivot = new Vector2(0f, 0.5f);
                RectTransform fill = UiFactory.NewObject("Fill", slot);
                fill.anchorMin = Vector2.zero;
                fill.anchorMax = Vector2.one;
                fill.offsetMin = fill.offsetMax = Vector2.zero;
                Image fillImage = fill.gameObject.AddComponent<Image>();
                fillImage.color = Palette.Health;
                fillImage.raycastTarget = false;
                slots.Add(slot.gameObject);
                fills.Add(fillImage);
            }
            for (int i = 0; i < slots.Count; i++)
            {
                bool used = i < allies;
                if (slots[i].activeSelf != used)
                {
                    slots[i].SetActive(used);
                }
            }
            // Unknown, so each fill is set on the next read.
            shown = new float[allies];
            for (int i = 0; i < allies; i++)
            {
                shown[i] = -1f;
            }
        }
    }
}
