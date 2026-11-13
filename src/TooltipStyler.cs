using RoR2;
using RoR2.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SevenUI
{
    // Restyles the tooltips of things in the HUD (items, skills, equipment) as the canvas's item tooltip: a dark
    // panel with a 2px strip of the item's tier color along the top, a header band with the bold name and an item's
    // stack count, the description, and LookingGlass's stats, when it adds them, apart under a line. The game makes
    // each tooltip as its own canvas, outside the HUD, when the pointer moves onto something, and destroys it after,
    // so this checks once a frame for a new one and leaves the rest to the game. Tooltips of menus, which share the
    // player's event system, are left alone.
    internal sealed class TooltipStyler
    {
        private const string PanelPath = "TooltipOffset/Panel";
        private const string OutlineName = "Outline";
        private const string TitleRectName = "TitleRect";
        private const string BodyRectName = "BodyRect";

        // The canvas's tooltip is 340px wide where the game's is 400 units: its sizes are scaled to keep the game's width.
        private const float GameWidth = 400f;
        private const float CanvasWidth = 340f;
        private const float Unit = GameWidth / CanvasWidth;
        private const float TitleSize = 16f * Unit;
        private const float CountSize = 16f * Unit;
        private const float BodySize = 12.5f * Unit;
        private const float StripHeight = 2f * Unit;
        private const int PaddingX = (int)(12f * Unit);
        private const int TitlePaddingY = (int)(8f * Unit);
        private const int BodyPaddingTop = (int)(8f * Unit);
        private const int BodyPaddingBottom = (int)(10f * Unit);

        private readonly HUD hud;
        private readonly Transform root;
        private TooltipController seen;

        public TooltipStyler(HUD hud, Transform root)
        {
            this.hud = hud;
            this.root = root;
        }

        public int StyledCount { get; private set; }

        public void Update(bool barlowFont, bool flatPanels)
        {
            MPEventSystem events = hud.eventSystemProvider ? hud.eventSystemProvider.resolvedEventSystem : null;
            TooltipController tooltip = events ? events.currentTooltip : null;
            if (tooltip == seen) return;
            seen = tooltip;
            TooltipProvider provider = events ? events.currentTooltipProvider : null;
            if (!tooltip || !provider || !provider.transform.IsChildOf(root)) return;

            StatsSection stats = flatPanels ? Restyle(tooltip, provider) : null;
            if (barlowFont)
            {
                new HudFontSwapper(hud, tooltip.transform).Apply();
            }
            // Measured once the texts have their font.
            if (stats != null)
            {
                LayoutStats(stats);
            }
            StyledCount++;
        }

        private static StatsSection Restyle(TooltipController tooltip, TooltipProvider provider)
        {
            Transform panel = tooltip.transform.Find(PanelPath);
            if (!panel) return null;
            Transform outline = panel.Find(OutlineName);
            Image outlineImage = outline ? outline.GetComponent<Image>() : null;
            if (outlineImage)
            {
                outlineImage.enabled = false;
            }
            RectTransform titleRect = panel.Find(TitleRectName) as RectTransform;
            RectTransform bodyRect = panel.Find(BodyRectName) as RectTransform;
            if (!titleRect || !bodyRect || !tooltip.titleLabel || !tooltip.bodyLabel) return null;

            // The panel: the canvas's near-black, with the header band a faint light tint over it. The body's own image
            // is the game's blur of what is behind, which a color only tints, so the near-black is an image over it.
            Image body = UiFactory.NewObject("SevenUIBodyPanel", bodyRect).gameObject.AddComponent<Image>();
            body.color = Palette.TooltipPanel;
            body.raycastTarget = false;
            Fill((RectTransform)body.transform);
            body.transform.SetAsFirstSibling();
            if (tooltip.colorHighlightImage)
            {
                tooltip.colorHighlightImage.color = Palette.TooltipPanel;
            }
            Image band = UiFactory.NewObject("SevenUIHeaderBand", titleRect).gameObject.AddComponent<Image>();
            band.color = CanvasBlend.Over(Palette.TooltipHeader, Palette.TooltipPanel);
            band.raycastTarget = false;
            Fill((RectTransform)band.transform);
            band.transform.SetAsFirstSibling();
            Image strip = UiFactory.NewObject("SevenUITierStrip", titleRect).gameObject.AddComponent<Image>();
            strip.color = StripColor(provider);
            strip.raycastTarget = false;
            RectTransform stripRect = (RectTransform)strip.transform;
            Fill(stripRect);
            stripRect.anchorMin = new Vector2(0f, 1f);
            stripRect.pivot = new Vector2(0.5f, 1f);
            stripRect.sizeDelta = new Vector2(0f, StripHeight);

            Pad(titleRect, TitlePaddingY, TitlePaddingY);
            Pad(bodyRect, BodyPaddingTop, BodyPaddingBottom);
            tooltip.titleLabel.fontSize = TitleSize;
            tooltip.titleLabel.fontStyle |= FontStyles.Bold;
            int count = StackCount(provider);
            if (count > 0)
            {
                TMP_Text label = UiFactory.NewText("SevenUIStackCount", titleRect, tooltip.titleLabel, CountSize, Palette.HeaderText);
                // Barlow Condensed SemiBold, the canvas's 600 weight, unbolded.
                label.gameObject.AddComponent<CondensedText>();
                label.alignment = TextAlignmentOptions.Right;
                label.text = "×" + count;
                RectTransform countRect = label.rectTransform;
                Fill(countRect);
                countRect.offsetMax = new Vector2(-PaddingX, 0f);
                tooltip.titleLabel.margin = new Vector4(0f, 0f, label.GetPreferredValues().x + PaddingX, 0f);
            }

            ItemStatsText.Parts parts = ItemStatsText.Split(tooltip.bodyLabel.text);
            tooltip.bodyLabel.text = parts.Description;
            tooltip.bodyLabel.fontSize = BodySize;
            tooltip.bodyLabel.color = Palette.TooltipBody;
            if (!parts.HasStats) return null;
            StatsSection stats = new StatsSection(bodyRect, tooltip.bodyLabel, Unit, BodySize);
            stats.Root.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            stats.Set(parts);
            return stats;
        }

        // Outside the body's layout, which places its children by its own settings: the body's bottom padding makes
        // room for the section, and the section sits in that room, from the body's bottom left corner.
        private static void LayoutStats(StatsSection stats)
        {
            RectTransform body = (RectTransform)stats.Root.parent;
            HorizontalOrVerticalLayoutGroup layout = body.GetComponent<HorizontalOrVerticalLayoutGroup>();
            if (!layout) return;
            float width = GameWidth - 2f * PaddingX;
            float height = stats.Layout(0f, 0f, width);
            layout.padding = new RectOffset(PaddingX, PaddingX, BodyPaddingTop, BodyPaddingBottom + Mathf.CeilToInt(height));
            RectTransform root = stats.Root;
            root.anchorMin = root.anchorMax = root.pivot = Vector2.zero;
            root.anchoredPosition = new Vector2(PaddingX, BodyPaddingBottom);
            root.sizeDelta = new Vector2(width, height);
        }

        // An item's tier color, equipment's orange, and otherwise the color the game gave the tooltip's title.
        private static Color StripColor(TooltipProvider provider)
        {
            ItemIcon item = provider.GetComponent<ItemIcon>();
            if (item && item.itemIndex != ItemIndex.None)
            {
                PickupDef pickup = PickupCatalog.GetPickupDef(PickupCatalog.FindPickupIndex(item.itemIndex));
                if (pickup != null) return pickup.baseColor;
            }
            if (provider.GetComponent<EquipmentIcon>()) return Palette.Equipment;
            return provider.titleColor;
        }

        private static int StackCount(TooltipProvider provider)
        {
            ItemIcon item = provider.GetComponent<ItemIcon>();
            return item ? item.itemCount : 0;
        }

        private static void Pad(RectTransform rect, int top, int bottom)
        {
            HorizontalOrVerticalLayoutGroup layout = rect.GetComponent<HorizontalOrVerticalLayoutGroup>();
            if (!layout) return;
            layout.padding = new RectOffset(PaddingX, PaddingX, top, bottom);
        }

        private static void Fill(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            LayoutElement element = rect.gameObject.AddComponent<LayoutElement>();
            element.ignoreLayout = true;
        }
    }
}
