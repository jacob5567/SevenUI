using System.Collections.Generic;
using RoR2;
using RoR2.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SevenUI
{
    // The row under the ally cards that stands in for the cards hidden because they would reach the chat: a small
    // portrait for each kind of ally hidden, up to a few, and "+N more" at its right, N the allies those cards hold.
    // As tall as a compact row, on the cards' flat panel; placed by AllyCardsStyler under the last card shown.
    internal sealed class AllyMoreRow
    {
        public const string Name = "SevenUIAllyMore";
        public const string IconName = "Kind";
        // The design canvas's sizes, in its pixels, scaled to the HUD's units.
        private const float Canvas = HudUnits.PerCanvasPixel;
        private const float IconSize = 22f * Canvas;
        private const float IconGap = 4f * Canvas;
        private const int PaddingRight = 8;
        private const float FontSize = 15f * Canvas;
        // Enough to fill the row's left beside the longest count.
        public const int MaxIcons = 5;

        private readonly RectTransform rect;
        private readonly HorizontalLayoutGroup layout;
        private readonly TextMeshProUGUI label;
        private readonly List<RawImage> icons = new List<RawImage>();
        private readonly List<Texture> kinds = new List<Texture>();
        private int shownCount = -1;

        // In the font and material of a card's name, so the font swap gives it Barlow if the name is still in the
        // game's font.
        public AllyMoreRow(RectTransform container, TMP_Text style, int paddingLeft)
        {
            rect = NewObject(Name, container);
            // Pinned to the container's top across its width, like the cards the game places.
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(0f, AllyCompactCard.RowHeight);
            Image panel = rect.gameObject.AddComponent<Image>();
            panel.color = Palette.Panel;
            panel.raycastTarget = false;
            layout = rect.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(paddingLeft, Mathf.RoundToInt(PaddingRight * Canvas), 0, 0);
            layout.spacing = IconGap;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;

            for (int i = 0; i < MaxIcons; i++)
            {
                RectTransform slot = NewObject(IconName, rect);
                RawImage icon = slot.gameObject.AddComponent<RawImage>();
                icon.raycastTarget = false;
                LayoutElement size = slot.gameObject.AddComponent<LayoutElement>();
                size.preferredWidth = size.preferredHeight = IconSize;
                slot.gameObject.SetActive(false);
                icons.Add(icon);
            }
            NewObject("Space", rect).gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

            label = NewObject("Count", rect).gameObject.AddComponent<TextMeshProUGUI>();
            if (style)
            {
                label.font = style.font;
                label.fontSharedMaterial = style.fontSharedMaterial;
            }
            label.fontSize = FontSize;
            label.fontStyle = FontStyles.Bold;
            label.color = Palette.HeaderText;
            label.alignment = TextAlignmentOptions.MidlineRight;
            label.enableWordWrapping = false;
            label.overflowMode = TextOverflowModes.Overflow;
            label.raycastTarget = false;
        }

        public bool Alive => rect;

        // Under the cards shown, standing in for the cards given, which hold that many allies. Only what changed is set.
        // The row's icons start where the cards' content does, so the row takes the cards' left padding.
        public void Show(float top, List<AllyCardController> hidden, int allies, int paddingLeft)
        {
            if (!rect.gameObject.activeSelf)
            {
                rect.gameObject.SetActive(true);
            }
            if (layout.padding.left != paddingLeft)
            {
                // Assigned whole, as that is what marks the layout for a rebuild.
                layout.padding = new RectOffset(paddingLeft, layout.padding.right, 0, 0);
            }
            // No card indent: the row stands at the container's left edge, like the cards.
            if (rect.offsetMin.x != 0f || rect.offsetMax.x != 0f)
            {
                rect.offsetMin = new Vector2(0f, rect.offsetMin.y);
                rect.offsetMax = new Vector2(0f, rect.offsetMax.y);
            }
            if (rect.anchoredPosition.y != -top)
            {
                rect.anchoredPosition = new Vector2(0f, -top);
            }
            if (allies != shownCount)
            {
                shownCount = allies;
                label.text = "+" + allies + " more";
            }
            // A hidden card stops updating its own portrait, so each kind is read from the ally's body.
            kinds.Clear();
            for (int i = 0; i < hidden.Count && kinds.Count < MaxIcons; i++)
            {
                CharacterMaster master = hidden[i] ? hidden[i].sourceMaster : null;
                CharacterBody body = master ? master.GetBody() : null;
                Texture portrait = body ? body.portraitIcon : null;
                if (portrait && !kinds.Contains(portrait))
                {
                    kinds.Add(portrait);
                }
            }
            for (int i = 0; i < icons.Count; i++)
            {
                Texture kind = i < kinds.Count ? kinds[i] : null;
                if (icons[i].texture != kind)
                {
                    icons[i].texture = kind;
                }
                bool shown = kind;
                if (icons[i].gameObject.activeSelf != shown)
                {
                    icons[i].gameObject.SetActive(shown);
                }
            }
        }

        public void Hide()
        {
            if (rect && rect.gameObject.activeSelf)
            {
                rect.gameObject.SetActive(false);
            }
        }

        public void Destroy()
        {
            if (rect)
            {
                Object.Destroy(rect.gameObject);
            }
        }

        private static RectTransform NewObject(string name, Transform parent)
        {
            GameObject created = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
            created.layer = parent.gameObject.layer;
            created.transform.SetParent(parent, false);
            return (RectTransform)created.transform;
        }
    }
}
