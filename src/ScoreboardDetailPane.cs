using System.Collections.Generic;
using RoR2;
using RoR2.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SevenUI
{
    // The scoreboard's docked description of the item, equipment or key item the pointer is on, or was last on, in
    // place of the tooltip that would cover the items: a dark panel with its color (the item's tier, the equipment's
    // orange, or the game's for lunar and boss equipment and key items) along its top, the icon on a slot beside its
    // name in that color, its description, LookingGlass's stats apart under a line when it adds them, and under another
    // line each player holding it, by portrait and name, with their stack of an item. The text is what the game gave
    // the tooltip when something (LookingGlass) replaced it, and otherwise the full description, which says more than
    // the tooltip's pickup line. Laid out by hand from its top left.
    internal sealed class ScoreboardDetailPane
    {
        public const string PaneName = "SevenUIItemPane";
        public const string HolderName = "SevenUIItemHolder";

        // The canvas's sizes, in its pixels, which the scoreboard's units match.
        private const float StripHeight = 3f;
        private const float PaddingX = 16f;
        private const float PaddingY = 14f;
        private const float IconSlot = 48f;
        private const float IconInset = 4f;
        private const float IconStripHeight = 3f;
        private const float TitleGap = 12f;
        private const float TitleSize = 20f;
        private const float DescriptionGap = 10f;
        private const float DescriptionSize = 15f;
        private const float StatsSize = 14f;
        private const float HoldersGap = 12f;
        private const float DividerHeight = 1f;
        private const float HoldersBelowDivider = 10f;
        private const float PortraitSize = 22f;
        private const float PortraitGap = 6f;
        private const float HolderSize = 14f;
        private const float HolderGap = 14f;
        private const float HolderLineGap = 6f;
        private const float StatsUnit = 1f;
        private const string Times = "×";

        private sealed class Holder
        {
            public RectTransform Rect;
            public RectTransform Slot;
            public RawImage Portrait;
            public TMP_Text Label;
        }

        public readonly RectTransform Root;

        private readonly Image strip;
        private readonly Image iconStrip;
        private readonly RectTransform iconSlot;
        private readonly RawImage icon;
        private readonly TMP_Text title;
        private readonly TMP_Text description;
        private readonly StatsSection stats;
        private readonly RectTransform divider;
        private readonly List<Holder> holders = new List<Holder>();
        private readonly TMP_Text style;
        private int holderCount;
        private float laidOutWidth = -1f;
        private TMP_FontAsset laidOutFont;
        private bool stale = true;

        public ScoreboardDetailPane(Transform parent, TMP_Text style, StyleRecorder recorder)
        {
            this.style = style;
            Root = NewObject(PaneName, parent);
            recorder.Add(Root.gameObject);
            Image panel = Root.gameObject.AddComponent<Image>();
            panel.color = Palette.TooltipPanel;
            panel.raycastTarget = false;
            strip = NewImage("TierStrip", Root, Palette.Text);
            RectTransform stripRect = strip.rectTransform;
            stripRect.anchorMin = new Vector2(0f, 1f);
            stripRect.anchorMax = Vector2.one;
            stripRect.pivot = new Vector2(0.5f, 1f);
            stripRect.anchoredPosition = Vector2.zero;
            stripRect.sizeDelta = new Vector2(0f, StripHeight);

            iconSlot = NewImage("Slot", Root, Palette.ItemSlot).rectTransform;
            icon = NewObject("Icon", iconSlot).gameObject.AddComponent<RawImage>();
            icon.raycastTarget = false;
            RectTransform iconRect = icon.rectTransform;
            iconRect.anchorMin = Vector2.zero;
            iconRect.anchorMax = Vector2.one;
            iconRect.offsetMin = new Vector2(IconInset, IconInset);
            iconRect.offsetMax = new Vector2(-IconInset, -IconInset);
            iconStrip = NewImage("TierStrip", iconSlot, Palette.Text);
            RectTransform iconStripRect = iconStrip.rectTransform;
            iconStripRect.anchorMin = Vector2.zero;
            iconStripRect.anchorMax = new Vector2(1f, 0f);
            iconStripRect.pivot = new Vector2(0.5f, 0f);
            iconStripRect.anchoredPosition = Vector2.zero;
            iconStripRect.sizeDelta = new Vector2(0f, IconStripHeight);

            title = NewText("Title", Root, TitleSize, Palette.Text);
            title.fontStyle = FontStyles.Bold;
            description = NewText("Description", Root, DescriptionSize, Palette.TooltipBody);
            stats = new StatsSection(Root, description, StatsUnit, StatsSize, recorder.Add);
            divider = NewImage("Divider", Root, Palette.Divider).rectTransform;
            Root.gameObject.SetActive(false);
        }

        // What it describes: one of these, the others None, or nothing while hidden.
        public ItemIndex Item { get; private set; } = ItemIndex.None;
        public EquipmentIndex Equipment { get; private set; } = EquipmentIndex.None;
        public KeyItemIndex KeyItem { get; private set; } = KeyItemIndex.None;

        public bool Shown { get; private set; }

        // Its height at the width it was last placed at.
        public float Height { get; private set; }

        // Read by the SevenUI.Tests plugin.
        public string Title => title.text;
        public int HolderCount => holderCount;
        public string HolderText(int index) => holders[index].Label.text;

        // Describes the item the icon shows, with every row holding it.
        public void Show(ItemIcon shown, IReadOnlyList<ScoreboardItemGrid> grids)
        {
            ItemDef item = ItemCatalog.GetItemDef(shown.itemIndex);
            if (!item) return;
            Describe(TierGroups.ColorOf(TierGroups.Of(shown.itemIndex)), shown.image ? shown.image.texture : null,
                shown.tooltipProvider, item.nameToken, item.descriptionToken);
            Item = item.itemIndex;
            holderCount = 0;
            foreach (ScoreboardItemGrid grid in grids)
            {
                ItemIcon held = grid.Alive ? grid.Find(Item) : null;
                if (held)
                {
                    AddHolder(grid.Strip, " <b>" + Times + held.itemCount + "</b>");
                }
            }
            Finish();
        }

        // Describes a row's equipment, with every row holding the same. Reads only.
        public void Show(EquipmentDef equipment, EquipmentIcon shown, IReadOnlyList<ScoreboardItemGrid> grids)
        {
            Color color = equipment.isLunar ? Palette.CountLunar : equipment.isBoss ? Palette.CountBoss : Palette.Equipment;
            Texture texture = shown.iconImage && shown.iconImage.texture ? shown.iconImage.texture : AsyncTexture(shown.asyncIconImage);
            Describe(color, texture, shown.tooltipProvider, equipment.nameToken, equipment.descriptionToken);
            Equipment = equipment.equipmentIndex;
            holderCount = 0;
            foreach (ScoreboardItemGrid grid in grids)
            {
                Inventory inventory = grid.Alive && grid.Strip.master ? grid.Strip.master.inventory : null;
                if (inventory && inventory.currentEquipmentIndex == Equipment)
                {
                    AddHolder(grid.Strip, "");
                }
            }
            Finish();
        }

        // Describes a row's key item, with every row holding the same. Reads only.
        public void Show(KeyItemDef keyItem, KeyItemIcon shown, IReadOnlyList<ScoreboardItemGrid> grids)
        {
            Describe(ColorCatalog.GetColor(keyItem.colorIndex), AsyncTexture(shown.asyncIconImage), shown.tooltipProvider,
                keyItem.nameToken, keyItem.descriptionToken);
            KeyItem = keyItem.keyItemIndex;
            holderCount = 0;
            foreach (ScoreboardItemGrid grid in grids)
            {
                Inventory inventory = grid.Alive && grid.Strip.master ? grid.Strip.master.inventory : null;
                if (inventory && inventory.keyItemIndex == KeyItem)
                {
                    AddHolder(grid.Strip, "");
                }
            }
            Finish();
        }

        private static Texture AsyncTexture(AsyncRawImage image) => image && image.renderer ? image.renderer.texture : null;

        private void Describe(Color color, Texture texture, TooltipProvider provider, string nameToken, string descriptionToken)
        {
            Item = ItemIndex.None;
            Equipment = EquipmentIndex.None;
            KeyItem = KeyItemIndex.None;
            strip.color = color;
            iconStrip.color = color;
            icon.texture = texture;
            title.text = provider ? provider.titleText : Language.GetString(nameToken);
            title.color = color;
            string body = provider && !string.IsNullOrEmpty(provider.overrideBodyText)
                ? provider.overrideBodyText
                : Language.GetString(descriptionToken);
            ItemStatsText.Parts parts = ItemStatsText.Split(body);
            description.text = parts.Description;
            stats.Set(parts);
        }

        private void AddHolder(ScoreboardStrip row, string suffix)
        {
            if (holderCount == holders.Count)
            {
                holders.Add(NewHolder());
            }
            Holder holder = holders[holderCount++];
            holder.Portrait.texture = row.classIcon ? row.classIcon.texture : null;
            holder.Label.text = (row.nameLabel ? row.nameLabel.text : "") + suffix;
        }

        private void Finish()
        {
            for (int i = 0; i < holders.Count; i++)
            {
                holders[i].Rect.gameObject.SetActive(i < holderCount);
            }
            divider.gameObject.SetActive(holderCount > 0);
            Root.gameObject.SetActive(true);
            Shown = true;
            stale = true;
        }

        public void Hide()
        {
            Item = ItemIndex.None;
            Equipment = EquipmentIndex.None;
            KeyItem = KeyItemIndex.None;
            Shown = false;
            Root.gameObject.SetActive(false);
        }

        // At x, y from its parent's top left, this wide; laid out again when its content, its width or its font (once
        // the font swap reaches its texts) changed.
        public void Place(float x, float y, float width)
        {
            if (stale || !Mathf.Approximately(width, laidOutWidth) || description.font != laidOutFont)
            {
                stale = false;
                laidOutWidth = width;
                laidOutFont = description.font;
                Height = Layout(width);
            }
            StatsSection.TopLeft(Root, x, y, width, Height);
        }

        private float Layout(float width)
        {
            float inner = Mathf.Max(0f, width - 2f * PaddingX);
            float y = StripHeight + PaddingY;
            StatsSection.TopLeft(iconSlot, PaddingX, y, IconSlot, IconSlot);
            float titleX = PaddingX + IconSlot + TitleGap;
            float titleWidth = Mathf.Max(0f, inner - IconSlot - TitleGap);
            float titleHeight = title.GetPreferredValues(title.text, titleWidth, 0f).y;
            StatsSection.TopLeft(title.rectTransform, titleX, y + Mathf.Max(0f, (IconSlot - titleHeight) / 2f), titleWidth,
                titleHeight);
            y += Mathf.Max(IconSlot, titleHeight) + DescriptionGap;
            float descriptionHeight = description.GetPreferredValues(description.text, inner, 0f).y;
            StatsSection.TopLeft(description.rectTransform, PaddingX, y, inner, descriptionHeight);
            y += descriptionHeight;
            y += stats.Layout(PaddingX, y, inner);
            if (holderCount > 0)
            {
                y += HoldersGap;
                StatsSection.TopLeft(divider, PaddingX, y, inner, DividerHeight);
                y += DividerHeight + HoldersBelowDivider;
                y = LayOutHolders(y, inner);
            }
            return y + PaddingY;
        }

        // Side by side, wrapping onto another line when the next doesn't fit.
        private float LayOutHolders(float top, float width)
        {
            float x = 0f;
            float y = top;
            for (int i = 0; i < holderCount; i++)
            {
                Holder holder = holders[i];
                Vector2 text = holder.Label.GetPreferredValues(holder.Label.text, float.PositiveInfinity, float.PositiveInfinity);
                float entryWidth = PortraitSize + PortraitGap + text.x;
                if (x > 0f && x + entryWidth > width)
                {
                    x = 0f;
                    y += PortraitSize + HolderLineGap;
                }
                StatsSection.TopLeft(holder.Rect, PaddingX + x, y, entryWidth, PortraitSize);
                StatsSection.TopLeft(holder.Slot, 0f, 0f, PortraitSize, PortraitSize);
                StatsSection.TopLeft(holder.Label.rectTransform, PortraitSize + PortraitGap, (PortraitSize - text.y) / 2f,
                    text.x, text.y);
                x += entryWidth + HolderGap;
            }
            return y + PortraitSize;
        }

        private Holder NewHolder()
        {
            Holder holder = new Holder { Rect = NewObject(HolderName, Root) };
            holder.Slot = NewImage("PortraitSlot", holder.Rect, Palette.ItemSlot).rectTransform;
            holder.Portrait = NewObject("Portrait", holder.Slot).gameObject.AddComponent<RawImage>();
            holder.Portrait.raycastTarget = false;
            RectTransform portrait = holder.Portrait.rectTransform;
            portrait.anchorMin = Vector2.zero;
            portrait.anchorMax = Vector2.one;
            portrait.offsetMin = portrait.offsetMax = Vector2.zero;
            holder.Label = NewText("Label", holder.Rect, HolderSize, Palette.Text);
            holder.Label.enableWordWrapping = false;
            return holder;
        }

        private TMP_Text NewText(string name, Transform parent, float size, Color color)
        {
            TextMeshProUGUI text = NewObject(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
            if (style)
            {
                text.font = style.font;
                text.fontSharedMaterial = style.fontSharedMaterial;
                text.spriteAsset = style.spriteAsset;
            }
            text.fontSize = size;
            text.color = color;
            text.alignment = TextAlignmentOptions.TopLeft;
            text.enableWordWrapping = true;
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false;
            return text;
        }

        private static Image NewImage(string name, Transform parent, Color color)
        {
            Image image = NewObject(name, parent).gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
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
