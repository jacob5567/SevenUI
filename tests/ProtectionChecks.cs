using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ModTestRunner;
using RoR2;
using RoR2.UI;
using UnityEngine;
using UnityEngine.UI;

namespace SevenUI.Tests
{
    // One-shot protection on health bars: the health it covers drawn wherever the bar's style draws it, over the share
    // of the bar the game gives it, in the mod's green stripes and never the game's own, or with the mod's look off, in
    // the game's own and never the mod's. The combat bars' styles leave it out, so a bar with one of those draws none.
    internal static class ProtectionChecks
    {
        // The game's sprite for it (HUDHealthBar's ospStyle).
        private const string GameSpriteName = "texUIOSPBar";
        // A share of the bar.
        private const float ShareTolerance = 0.02f;

        public static IEnumerator ExpectProtectionStriped(this Harness h, Func<IEnumerable<HealthBar>> bars, string whose,
            bool modded)
        {
            string wanted = modded ? FlatSprite.ProtectionHatchName : GameSpriteName;
            string unwanted = modded ? GameSpriteName : FlatSprite.ProtectionHatchName;
            string look = "no health bars showing";
            yield return h.Until(
                () =>
                {
                    List<HealthBar> shown = bars().Where(bar => bar && bar.isActiveAndEnabled && bar.source).ToList();
                    if (shown.Count == 0) return false;
                    look = string.Join("; ", shown.Select(bar => Look(bar, wanted, unwanted)));
                    return shown.All(bar => Problem(bar, wanted, unwanted) == null);
                },
                () => $"one-shot protection on {whose} health bars in {wanted} ({look}) {h.Diagnosis()}");
            h.Log($"One-shot protection on {whose} health bars, {(modded ? "SevenUI's" : "the game's")} look: {look}");
        }

        private static string Look(HealthBar bar, string wanted, string unwanted)
        {
            string name = bar.source.body ? bar.source.body.name : bar.name;
            return $"{name}, style {(bar.style ? bar.style.name : "none")}: {Problem(bar, wanted, unwanted) ?? "fine"}";
        }

        private static string Problem(HealthBar bar, string wanted, string unwanted)
        {
            List<Image> images = bar.GetComponentsInChildren<Image>()
                .Where(image => image.enabled && image.sprite && image.gameObject.activeInHierarchy).ToList();
            if (images.Any(image => image.sprite.name == unwanted)) return $"drawn in {unwanted}";
            Image stripes = images.FirstOrDefault(image => image.sprite.name == wanted);
            bool drawn = bar.style && bar.style.ospStyle.enabled;
            if (!drawn) return stripes ? "stripes its style doesn't draw" : null;
            // Reads the bar's numbers only.
            float expected = bar.source.GetHealthBarValues().ospFraction;
            if (expected <= 0f) return null;
            if (!stripes) return $"no stripes over {expected:P0}";
            float share = stripes.rectTransform.anchorMax.x - stripes.rectTransform.anchorMin.x;
            return Mathf.Abs(share - expected) < ShareTolerance ? null : $"stripes over {share:P0}, not {expected:P0}";
        }
    }
}
