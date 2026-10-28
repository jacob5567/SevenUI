using RoR2.UI;
using UnityEngine;
using UnityEngine.UI;

namespace SevenUI
{
    // Restyles the tooltips of things in the HUD (items, skills, equipment): Barlow text, a bold title, and no blue
    // outline. The game makes each tooltip as its own canvas, outside the HUD, when the pointer moves onto something,
    // and destroys it after, so this checks once a frame for a new one and leaves the rest to the game. Tooltips of
    // menus, which share the player's event system, are left alone.
    internal sealed class TooltipStyler
    {
        private const string OutlinePath = "TooltipOffset/Panel/Outline";

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

            if (barlowFont)
            {
                new HudFontSwapper(tooltip.transform).Apply();
            }
            if (flatPanels)
            {
                Transform outline = tooltip.transform.Find(OutlinePath);
                Image outlineImage = outline ? outline.GetComponent<Image>() : null;
                if (outlineImage)
                {
                    outlineImage.enabled = false;
                }
                if (tooltip.titleLabel)
                {
                    tooltip.titleLabel.fontStyle |= TMPro.FontStyles.Bold;
                }
            }
            StyledCount++;
        }
    }
}
