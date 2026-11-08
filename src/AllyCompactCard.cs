using RoR2.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SevenUI
{
    // One ally card's compact look and its place in the stack: as a compact row it is the design canvas's 28px row, a
    // small portrait and then the name and the health bar side by side on one line; hidden, it is turned off and takes
    // no height. Keeps what it changes, so the card goes back to the game's look when it leaves the compact look.
    internal sealed class AllyCompactCard
    {
        private const float Canvas = HudUnits.PerCanvasPixel;
        public const float RowHeight = 28f * Canvas;
        private const float NameSize = 15f * Canvas;
        private const float NameLeft = 6f * Canvas;
        private const float BarGap = 8f * Canvas;
        private const float BarRight = 8f * Canvas;
        private const float BarHeight = 5f * Canvas;
        // The name's share of the space beside the portrait, as on the canvas (118 of its 202px); the bar takes the rest.
        private const float NameShare = 0.58f;

        public readonly AllyCardController Card;
        // The card's height in the game's look.
        public readonly float FullHeight;
        private readonly LayoutElement portraitLayout;
        private readonly float portraitWidth;
        // The portrait's badges (critically hurt, dead, the drone's item), drawn at a fixed size around its middle.
        private readonly RectTransform[] badges;
        private readonly Vector2[] badgePositions;
        private readonly Vector3[] badgeScales;
        // The name and the bar's column, which the game stacks with a vertical layout group.
        private readonly RectTransform bars;
        private readonly VerticalLayoutGroup barsLayout;
        private readonly float barsHeight;
        // The card's own row padding, which the portrait and the column sit inside.
        private readonly float paddingY;
        private readonly float nameMaxSize;
        private readonly TextAlignmentOptions nameAlignment;
        private readonly TextOverflowModes nameOverflow;

        public bool Compact { get; private set; }
        public bool Hidden { get; private set; }

        public AllyCompactCard(AllyCardController card)
        {
            Card = card;
            FullHeight = card.layoutElement ? card.layoutElement.preferredHeight : 0f;
            HorizontalLayoutGroup row = card.GetComponent<HorizontalLayoutGroup>();
            paddingY = row ? row.padding.vertical : 0f;
            if (card.portraitIconImage)
            {
                portraitLayout = card.portraitIconImage.GetComponent<LayoutElement>();
                portraitWidth = portraitLayout ? portraitLayout.preferredWidth : 0f;
                Transform portrait = card.portraitIconImage.transform;
                badges = new RectTransform[portrait.childCount];
                badgePositions = new Vector2[badges.Length];
                badgeScales = new Vector3[badges.Length];
                for (int i = 0; i < badges.Length; i++)
                {
                    badges[i] = portrait.GetChild(i) as RectTransform;
                    if (!badges[i]) continue;
                    badgePositions[i] = badges[i].anchoredPosition;
                    badgeScales[i] = badges[i].localScale;
                }
            }
            bars = card.nameLabel ? card.nameLabel.transform.parent as RectTransform : null;
            barsLayout = bars ? bars.GetComponent<VerticalLayoutGroup>() : null;
            barsHeight = bars ? bars.sizeDelta.y : 0f;
            if (card.nameLabel)
            {
                nameMaxSize = card.nameLabel.fontSizeMax;
                nameAlignment = card.nameLabel.alignment;
                nameOverflow = card.nameLabel.overflowMode;
            }
        }

        // Only what changed, so it is cheap to call every frame.
        public void Set(bool compact, bool hidden)
        {
            if (!Card) return;
            if (compact != Compact && CanCompact)
            {
                Compact = compact;
                if (compact)
                {
                    ApplyCompact();
                }
                else
                {
                    RevertCompact();
                }
            }
            Hidden = hidden;
            float height = hidden ? 0f : Compact ? RowHeight : FullHeight;
            if (Card.layoutElement && Card.layoutElement.preferredHeight != height)
            {
                Card.layoutElement.preferredHeight = height;
            }
            if (Card.gameObject.activeSelf == hidden)
            {
                Card.gameObject.SetActive(!hidden);
            }
        }

        public void Revert() => Set(false, false);

        private bool CanCompact => portraitLayout && bars && barsLayout && Card.nameLabel && Card.healthBar;

        // The portrait's width sets its height (the game's aspect fitter), so it shrinks to the row; its badges shrink
        // with it. The column's layout group is turned off and its two children placed side by side.
        private void ApplyCompact()
        {
            float inner = RowHeight - paddingY;
            portraitLayout.preferredWidth = inner;
            float scale = portraitWidth > 0f ? inner / portraitWidth : 1f;
            for (int i = 0; i < badges.Length; i++)
            {
                if (!badges[i]) continue;
                badges[i].anchoredPosition = badgePositions[i] * scale;
                badges[i].localScale = badgeScales[i] * scale;
            }

            barsLayout.enabled = false;
            // The card's row lays the column out across but leaves its height alone.
            bars.sizeDelta = new Vector2(bars.sizeDelta.x, inner);
            TMP_Text name = Card.nameLabel;
            name.fontSizeMax = NameSize;
            name.alignment = TextAlignmentOptions.MidlineLeft;
            name.overflowMode = TextOverflowModes.Ellipsis;
            RectTransform nameRect = name.rectTransform;
            nameRect.anchorMin = Vector2.zero;
            nameRect.anchorMax = new Vector2(NameShare, 1f);
            nameRect.offsetMin = new Vector2(NameLeft, 0f);
            nameRect.offsetMax = Vector2.zero;
            RectTransform bar = (RectTransform)Card.healthBar.transform;
            bar.anchorMin = new Vector2(NameShare, 0.5f);
            bar.anchorMax = new Vector2(1f, 0.5f);
            bar.offsetMin = new Vector2(BarGap, -BarHeight / 2f);
            bar.offsetMax = new Vector2(-BarRight, BarHeight / 2f);
            LayoutRebuilder.MarkLayoutForRebuild(Card.rectTransform);
        }

        // Turned back on, the column's layout group places and sizes the name and the bar again.
        private void RevertCompact()
        {
            portraitLayout.preferredWidth = portraitWidth;
            for (int i = 0; i < badges.Length; i++)
            {
                if (!badges[i]) continue;
                badges[i].anchoredPosition = badgePositions[i];
                badges[i].localScale = badgeScales[i];
            }
            bars.sizeDelta = new Vector2(bars.sizeDelta.x, barsHeight);
            TMP_Text name = Card.nameLabel;
            name.fontSizeMax = nameMaxSize;
            name.alignment = nameAlignment;
            name.overflowMode = nameOverflow;
            barsLayout.enabled = true;
            LayoutRebuilder.MarkLayoutForRebuild(Card.rectTransform);
        }
    }
}
