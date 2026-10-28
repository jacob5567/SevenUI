using System.Collections.Generic;
using RoR2.UI;
using UnityEngine;

namespace SevenUI
{
    // Flat copies of the game's health bar styles, made once per style and shared by every health bar the mod
    // restyles (the player's, ally cards'). Like the baked fonts they last for the game, since several HUDs use them.
    internal static class FlatHealthStyles
    {
        private const string NameSuffix = " (SevenUI)";

        private static readonly Dictionary<HealthBarStyle, HealthBarStyle> Copies =
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

        // The plain bars (health, its trailing parts, shield, barrier) are drawn flat in their own colors. Bars whose
        // pattern means something (curse, the execute threshold, one-shot protection, low health) keep their sprites.
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
            FlattenBar(ref copy.barrierBarStyle);
            Copies[original] = copy;
            Flat.Add(copy);
            return copy;
        }

        // Several bars get their color from their sprite, not their base color: the barrier is a white base on a
        // translucent gold sprite, the trailing damage a grey base on an orange-red one. So the sprite's own average
        // color and opacity are folded into the base color, and the flat bar keeps the game's color.
        private static void FlattenBar(ref HealthBarStyle.BarStyle bar)
        {
            if (!bar.sprite) return;
            bar.baseColor *= SpriteColors.Average(bar.sprite);
            bar.sprite = FlatSprite.White;
        }
    }
}
