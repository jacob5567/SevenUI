using System.Collections.Generic;
using RoR2;
using RoR2.UI;
using UnityEngine;
using UnityEngine.UI;

namespace SevenUI
{
    // One scoreboard row's items as the canvas draws them: sorted into tier runs (TierGroups), in square slots of one
    // size wrapping across the items cell. The tiers' counts are under the name (ScoreboardRow), not in the grid. The game keeps
    // making, filling and placing the icons (ItemInventoryDisplay); each frame this moves any icon that isn't in its
    // slot back into it, and sorts them again only when the items change. It also lights the item the pointer is on and
    // can dim the rest.
    internal sealed class ScoreboardItemGrid
    {
        public const string HighlightName = "SevenUIItemHighlight";
        // The canvas's sizes at its 40px slot, which scale with the slot.
        public const float FullSlot = 40f;
        private const float FullGap = 4f;
        private const float DimmedAlpha = 0.35f;
        // The highlight: a 2px accent outline, 1px clear of the slot.
        private const float HighlightEdge = 2f;
        private const float HighlightOutset = 3f;
        // The outline sprite's edge, in its pixels (FlatSprite.Outline).
        private const float OutlineEdgePixels = 2f;
        // Where an icon's place is, on the icon: its top-left corner. The game's scoreboard icon has its pivot outside
        // its picture, a tenth of its size left of and above it.
        private static readonly Vector2 IconTopLeft = new Vector2(0f, 1f);
        // Against rounding in how many slots fit a line.
        private const float FitTolerance = 0.001f;

        public readonly ScoreboardStrip Strip;
        public readonly ItemInventoryDisplay Display;
        public readonly RectTransform Cell;
        public readonly Image Background;

        private readonly StyleRecorder recorder;
        // The items the icons showed when last sorted, in the game's order, and the slot each icon goes to.
        private readonly List<ItemIndex> sorted = new List<ItemIndex>();
        private readonly List<int> slotOf = new List<int>();
        private readonly List<int> groupOf = new List<int>();
        private readonly Dictionary<ItemIcon, CanvasGroup> fades = new Dictionary<ItemIcon, CanvasGroup>();
        private readonly List<ItemIcon> gone = new List<ItemIcon>();
        private RectTransform highlight;
        private int slots;
        private float slot = FullSlot;
        private float across = FullSlot;
        private ItemIndex highlightedItem = ItemIndex.None;
        private bool highlightDims;
        private bool highlightStale = true;

        public ScoreboardItemGrid(ScoreboardStrip strip, StyleRecorder recorder)
        {
            Strip = strip;
            Display = strip.itemInventoryDisplay;
            Cell = (RectTransform)Display.transform;
            Transform background = strip.transform.Find(ScoreboardStyler.RowBackgroundName);
            Background = background ? background.GetComponent<Image>() : null;
            this.recorder = recorder;
        }

        public bool Alive => Strip && Display;

        // Slots; 0 without items.
        public int Slots => slots;

        public float SlotSize => slot;

        // Sorts the icons again if the game changed what they show; true if it did.
        public bool Refresh()
        {
            List<ItemIcon> icons = Display.itemIcons;
            bool same = icons.Count == sorted.Count;
            for (int i = 0; same && i < icons.Count; i++)
            {
                same = icons[i] && icons[i].itemIndex == sorted[i];
            }
            if (same) return false;
            sorted.Clear();
            groupOf.Clear();
            foreach (ItemIcon icon in icons)
            {
                ItemIndex item = icon ? icon.itemIndex : ItemIndex.None;
                sorted.Add(item);
                groupOf.Add(TierGroups.Of(item));
            }
            Sort();
            ForgetGoneIcons();
            highlightStale = true;
            return true;
        }

        // Each tier's run in order, its items in the game's order, which is the order they were picked up.
        private void Sort()
        {
            slotOf.Clear();
            for (int i = 0; i < sorted.Count; i++)
            {
                slotOf.Add(0);
            }
            slots = 0;
            for (int group = 0; group < TierGroups.Count; group++)
            {
                for (int i = 0; i < groupOf.Count; i++)
                {
                    if (groupOf[i] == group)
                    {
                        slotOf[i] = slots++;
                    }
                }
            }
        }

        private void ForgetGoneIcons()
        {
            foreach (ItemIcon icon in fades.Keys)
            {
                if (!icon) gone.Add(icon);
            }
            foreach (ItemIcon icon in gone)
            {
                fades.Remove(icon);
            }
            gone.Clear();
        }

        // How tall the items are with slots this size in a cell this wide.
        public float HeightAt(float size, float width)
        {
            if (slots == 0) return 0f;
            int perLine = PerLine(size, width);
            int lines = (slots + perLine - 1) / perLine;
            return lines * Step(size) - Gap(size);
        }

        // How many lines the items take with slots this size in a cell this wide.
        public int LinesAt(float size, float width)
        {
            int perLine = PerLine(size, width);
            return (slots + perLine - 1) / perLine;
        }

        public void SetLayout(float size, float width)
        {
            if (Mathf.Approximately(size, slot) && Mathf.Approximately(width, across)) return;
            slot = size;
            across = width;
            highlightStale = true;
        }

        private static float Gap(float size) => size * FullGap / FullSlot;

        private static float Step(float size) => size + Gap(size);

        private static int PerLine(float size, float width) =>
            Mathf.Max(1, Mathf.FloorToInt((width + Gap(size)) / Step(size) + FitTolerance));

        // A slot's top-left corner, across and down from the cell's.
        private Vector2 SlotCorner(int place)
        {
            int perLine = PerLine(slot, across);
            float step = Step(slot);
            return new Vector2(place % perLine * step, place / perLine * step);
        }

        // Every frame while the board shows: a few reads per icon, and a move for those the game has just placed.
        public void Apply()
        {
            Rect area = Cell.rect;
            List<ItemIcon> icons = Display.itemIcons;
            for (int i = 0; i < icons.Count && i < slotOf.Count; i++)
            {
                ItemIcon icon = icons[i];
                RectTransform rect = icon ? icon.rectTransform : null;
                if (!rect) continue;
                Pin(rect);
                float width = rect.rect.width;
                if (width <= 0f) continue;
                Vector2 corner = SlotCorner(slotOf[i]);
                Vector3 place = new Vector3(area.xMin + corner.x, area.yMax - corner.y, 0f);
                if (rect.localPosition != place)
                {
                    rect.localPosition = place;
                }
                float scale = slot / width;
                if (!Mathf.Approximately(rect.localScale.x, scale))
                {
                    rect.localScale = new Vector3(scale, scale, 1f);
                }
            }
        }

        // With the pivot on the icon's corner, its place is its top-left corner. Restoring the game's look puts the
        // pivots back (ScoreboardStyler.FitItems).
        private static void Pin(RectTransform rect)
        {
            if (rect.pivot != IconTopLeft)
            {
                rect.pivot = IconTopLeft;
            }
        }

        // Outlines the slot of the given item, if this row holds it, and with dimOthers fades every other item; None
        // lights nothing. Changes nothing when the item, the icons and the layout are as they were.
        public void Highlight(ItemIndex item, bool dimOthers)
        {
            if (!highlightStale && item == highlightedItem && dimOthers == highlightDims) return;
            highlightStale = false;
            highlightedItem = item;
            highlightDims = dimOthers;
            List<ItemIcon> icons = Display.itemIcons;
            int match = -1;
            for (int i = 0; i < icons.Count; i++)
            {
                ItemIcon icon = icons[i];
                if (!icon) continue;
                bool same = item != ItemIndex.None && icon.itemIndex == item;
                if (same)
                {
                    match = i;
                }
                Fade(icon, dimOthers && item != ItemIndex.None && !same ? DimmedAlpha : 1f);
            }
            if (match < 0 || match >= slotOf.Count)
            {
                if (highlight) highlight.gameObject.SetActive(false);
                return;
            }
            if (!highlight)
            {
                highlight = NewHighlight();
            }
            highlight.gameObject.SetActive(true);
            highlight.SetAsLastSibling();
            Vector2 corner = SlotCorner(slotOf[match]);
            StatsSection.TopLeft(highlight, corner.x - HighlightOutset, corner.y - HighlightOutset, slot + 2f * HighlightOutset,
                slot + 2f * HighlightOutset);
        }

        private RectTransform NewHighlight()
        {
            RectTransform rect = NewObject(HighlightName, Cell);
            recorder.Add(rect.gameObject);
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = FlatSprite.Outline;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = OutlineEdgePixels / HighlightEdge;
            image.color = Palette.Accent;
            image.raycastTarget = false;
            return rect;
        }

        // Through a group of the mod's on the icon, made the first time the icon is dimmed.
        private void Fade(ItemIcon icon, float alpha)
        {
            if (!fades.TryGetValue(icon, out CanvasGroup group))
            {
                if (Mathf.Approximately(alpha, 1f)) return;
                group = icon.gameObject.AddComponent<CanvasGroup>();
                recorder.Add(group);
                fades[icon] = group;
            }
            if (group && !Mathf.Approximately(group.alpha, alpha))
            {
                group.alpha = alpha;
            }
        }

        // The icon showing this item, or null.
        public ItemIcon Find(ItemIndex item)
        {
            foreach (ItemIcon icon in Display.itemIcons)
            {
                if (icon && icon.itemIndex == item) return icon;
            }
            return null;
        }

        private static RectTransform NewObject(string name, Transform parent)
        {
            GameObject made = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
            made.layer = parent.gameObject.layer;
            made.transform.SetParent(parent, false);
            return (RectTransform)made.transform;
        }
    }
}
