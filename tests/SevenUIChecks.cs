using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ModTestRunner;
using RoR2.UI;
using TMPro;
using UnityEngine;

namespace SevenUI.Tests
{
    // The SevenUI checks, as Harness extension methods so scenarios call them like the runner's own helpers. They read
    // the HUD's texts directly; the mod itself is read only through SevenUIController.Describe.
    internal static class SevenUIChecks
    {
        public static SevenUIController Controller =>
            HUD.readOnlyInstanceList.Where(hud => hud).Select(hud => hud.GetComponent<SevenUIController>())
                .FirstOrDefault(controller => controller);

        private static IEnumerable<TMP_Text> ActiveTexts(Transform root)
        {
            return root.GetComponentsInChildren<TMP_Text>(false).Where(text => text.font);
        }

        private static bool IsGameFont(TMP_Text text) => text.font.name.StartsWith(GameNames.GameFontPrefix);

        private static bool IsBarlow(TMP_Text text) => text.font.name.StartsWith(Texts.BarlowFontPrefix);

        // The texts still in the game's font, by path, for failure messages.
        private static string GameFontTexts(Transform root)
        {
            List<string> paths = ActiveTexts(root).Where(IsGameFont).Select(text => PathOf(text.transform, root)).ToList();
            return paths.Count == 0 ? "none" : $"{paths.Count}: {string.Join(", ", paths.Take(8))}";
        }

        private static string PathOf(Transform transform, Transform root)
        {
            List<string> names = new List<string>();
            for (Transform t = transform; t && t != root; t = t.parent)
            {
                names.Add(t.name);
            }
            names.Reverse();
            return string.Join("/", names);
        }

        private static Transform HudRoot(Harness h)
        {
            SevenUIController controller = Controller;
            if (!controller) throw h.Fail("no SevenUI controller on the HUD");
            return controller.transform.root;
        }

        // Waits until every active text under the root (the HUD, or a panel in it) is drawn in Barlow, and at least
        // one is.
        public static IEnumerator ExpectAllBarlow(this Harness h, Transform root, string what)
        {
            yield return h.Until(
                () => ActiveTexts(root).Any(IsBarlow) && !ActiveTexts(root).Any(IsGameFont),
                () => $"{what} in Barlow (still in the game's font: {GameFontTexts(root)}) {h.Diagnosis()}");
        }

        public static IEnumerator ExpectHudBarlow(this Harness h)
        {
            yield return h.ExpectAllBarlow(HudRoot(h), "all HUD text");
        }

        // Waits until no active HUD text is in Barlow and the game's font is back.
        public static IEnumerator ExpectHudGameFont(this Harness h)
        {
            Transform root = HudRoot(h);
            yield return h.Until(
                () => ActiveTexts(root).Any(IsGameFont) && !ActiveTexts(root).Any(IsBarlow),
                () => $"all HUD text back in the game's font ({ActiveTexts(root).Count(IsBarlow)} still in Barlow) {h.Diagnosis()}");
        }

        public static Transform FindUnderHud(this Harness h, string name)
        {
            return HudRoot(h).GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);
        }
    }
}
