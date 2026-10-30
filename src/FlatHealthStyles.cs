using System.Collections.Generic;
using RoR2.UI;
using UnityEngine;
using UnityEngine.UI;

namespace SevenUI
{
    // Flat copies of the game's health bar styles, made once per style and shared by every health bar the mod
    // restyles (the player's, ally cards'). Like the baked fonts they last for the game, since several HUDs use them.
    internal static class FlatHealthStyles
    {
        private const string NameSuffix = " (SevenUI)";

        private const string LowNameSuffix = " (SevenUI, low health)";

        private static readonly Dictionary<HealthBarStyle, HealthBarStyle> Copies =
            new Dictionary<HealthBarStyle, HealthBarStyle>();
        private static readonly Dictionary<HealthBarStyle, HealthBarStyle> LowCopies =
            new Dictionary<HealthBarStyle, HealthBarStyle>();
        private static readonly HashSet<HealthBarStyle> Flat = new HashSet<HealthBarStyle>();

        // Gives the bar flat copies of its styles, and records how to put the originals back.
        public static void Apply(HealthBar bar, StyleRecorder recorder)
        {
            if (!bar || !bar._style || Flat.Contains(bar._style)) return;
            HealthBarStyle style = bar._style;
            HealthBarStyle allyStyle = bar._allyStyle;
            bar._style = Of(style);
            bar._allyStyle = allyStyle ? Of(allyStyle) : null;
            recorder.OnRestore(() =>
            {
                if (!bar) return;
                bar._style = style;
                bar._allyStyle = allyStyle;
            });
        }

        // The flat style's copy for health that is low (under 25%), with the health in the canvas's steady red where the
        // game flashes it; the player's bar switches to it while its health is low (PlayerHealthReadout). The style
        // itself if it isn't one of the flat copies.
        public static HealthBarStyle LowHealth(HealthBarStyle flat)
        {
            if (!flat || !Flat.Contains(flat) || LowCopies.ContainsValue(flat)) return flat;
            if (LowCopies.TryGetValue(flat, out HealthBarStyle copy) && copy) return copy;
            copy = Object.Instantiate(flat);
            copy.name = flat.name.Replace(NameSuffix, LowNameSuffix);
            copy.flashOnHealthCritical = false;
            copy.trailingOverHealthBarStyle.baseColor = Palette.LowHealth;
            LowCopies[flat] = copy;
            Flat.Add(copy);
            return copy;
        }

        // The plain bars (health, its trailing parts, shield) are drawn flat in the design canvas's colors, the barrier
        // as a strip along the top, so the health and shield under it still show, and the maximum health a curse has
        // taken away as dark stripes. Bars whose pattern means something else (the execute threshold, one-shot
        // protection, low health) keep their sprites. Health keeps the game's color for a void or infused body, which
        // the game sets over the style's.
        private static HealthBarStyle Of(HealthBarStyle original)
        {
            if (Copies.TryGetValue(original, out HealthBarStyle copy) && copy) return copy;
            copy = Object.Instantiate(original);
            copy.name = original.name + NameSuffix;
            FlattenBar(ref copy.trailingUnderHealthBarStyle);
            FlattenBar(ref copy.instantHealthBarStyle);
            FlattenBar(ref copy.trailingOverHealthBarStyle);
            FlattenBar(ref copy.regainableHealthStyle);
            FlattenBar(ref copy.shieldBarStyle);
            StripBar(ref copy.barrierBarStyle);
            copy.trailingOverHealthBarStyle.baseColor = Palette.Health;
            copy.shieldBarStyle.baseColor = Palette.Shield;
            HatchBar(ref copy.curseBarStyle);
            Copies[original] = copy;
            Flat.Add(copy);
            return copy;
        }

        // Several bars get their color from their sprite, not their base color: the trailing damage is a grey base on
        // an orange-red sprite, the regainable health a white base on a translucent olive one. So the sprite's own
        // average color and opacity are folded into the base color, and the flat bar keeps the game's color.
        private static void FlattenBar(ref HealthBarStyle.BarStyle bar)
        {
            if (!bar.sprite) return;
            bar.baseColor *= SpriteColors.Average(bar.sprite);
            bar.sprite = FlatSprite.White;
        }

        private static void HatchBar(ref HealthBarStyle.BarStyle bar)
        {
            if (!bar.sprite) return;
            bar.baseColor = Color.white;
            bar.sprite = FlatSprite.Hatch;
            bar.imageType = Image.Type.Tiled;
            bar.sizeDelta = 0f;
        }

        // The game's barrier is a gold frame drawn larger than the bar (a positive size delta), over the health. The
        // strip stays within the bar.
        private static void StripBar(ref HealthBarStyle.BarStyle bar)
        {
            if (!bar.sprite) return;
            bar.baseColor = Palette.Barrier;
            bar.sprite = FlatSprite.TopStrip;
            bar.imageType = Image.Type.Sliced;
            bar.sizeDelta = 0f;
        }
    }
}
