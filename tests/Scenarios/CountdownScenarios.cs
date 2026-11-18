using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using ModTestRunner;
using RoR2;
using RoR2.UI;
using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace SevenUI.Tests
{
    // The countdown panels (the moon's escape, a Winnowing trial, Champ Rush's doom timer), each spawned from the game's
    // own prefab under the top cluster with the timer set: flat, in Barlow, the time as mm:ss, a bar that drains and
    // turns red, out of the other panels' way, back to the game's look when FlatPanels is off, and gone when the
    // game removes the panel. The countdowns suite (SevenUITests.Suites).
    internal static class CountdownScenarios
    {
        private const string MoonPrefab = "Prefabs/UI/HudModules/HudCountdownPanel";
        private const string TrialAddress = "9f990f5551f502e4f99fcc4d0690331e";
        private const string DoomKeyPart = "DoomTimer";
        private const string DoomPanelField = "trialTimerAddress";
        private const float StyleTimeout = 3f;
        private const float TypingTimeout = 10f;
        private const double FullSeconds = 125.0;
        private const double HalfSeconds = 60.0;
        private const double LowSeconds = 5.0;
        private const string FullText = "02:05";
        private const string LowText = "00:05";
        private const float Tolerance = 0.01f;
        private const float OverlapTolerance = 0.5f;
        // The least a warning color's brightest channel may be, and its least opacity, to read on the dark panel.
        private const float ReadableChannel = 0.5f;
        private const float ReadableAlpha = 0.9f;

        public static IEnumerable<Scenario> All()
        {
            yield return new Scenario(1, "moon_countdown_panel", Stages.TitanicPlains,
                h => Run(h, "moon", () => LoadLegacy(h), false));
            yield return new Scenario(1, "trial_countdown_panel", Stages.TitanicPlains,
                h => Run(h, "trial", () => LoadAddress(h, TrialAddress), true));
            yield return new Scenario(1, "doom_timer_countdown_panel", Stages.TitanicPlains,
                h => Run(h, "doom", () => LoadAddress(h, DoomPanelKey(h)), false));
        }

        private static GameObject LoadLegacy(Harness h)
        {
            GameObject prefab = Resources.Load<GameObject>(MoonPrefab);
            if (!prefab) throw h.Skip($"{MoonPrefab} loaded as nothing");
            return prefab;
        }

        private static string FindDoomKey(Harness h)
        {
            foreach (var locator in Addressables.ResourceLocators)
            {
                foreach (object key in locator.Keys)
                {
                    if (key is string text && text.Contains(DoomKeyPart) && text.EndsWith(".prefab")) return text;
                }
            }
            throw h.Skip("no addressable for the doom timer's panel");
        }

        // The doom timer's prefab is Champ Rush's controller; the panel it shows is the asset its trialTimerAddress names.
        private static string DoomPanelKey(Harness h)
        {
            string controllerKey = FindDoomKey(h);
            GameObject controller;
            try
            {
                controller = Addressables.LoadAssetAsync<GameObject>(controllerKey).WaitForCompletion();
            }
            catch (Exception exception)
            {
                throw h.Skip($"could not load {controllerKey}: {exception.Message}");
            }
            DoomTimer doom = controller ? controller.GetComponentInChildren<DoomTimer>(true) : null;
            if (!doom) throw h.Skip($"{controllerKey} has no DoomTimer");
            FieldInfo field = typeof(DoomTimer).GetField(DoomPanelField, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            AssetReference reference = field != null ? field.GetValue(doom) as AssetReference : null;
            if (reference == null || !reference.RuntimeKeyIsValid()) throw h.Skip($"the DoomTimer in {controllerKey} names no panel");
            return reference.RuntimeKey.ToString();
        }

        private static GameObject LoadAddress(Harness h, string address)
        {
            GameObject prefab;
            try
            {
                prefab = Addressables.LoadAssetAsync<GameObject>(address).WaitForCompletion();
            }
            catch (Exception exception)
            {
                throw h.Skip($"could not load {address}: {exception.Message}");
            }
            if (!prefab) throw h.Skip($"{address} loaded as nothing");
            if (!prefab.GetComponentInChildren<TimerText>(true)) throw h.Skip($"{address} has no TimerText");
            return prefab;
        }

        private static CountdownStyler Styler(Harness h)
        {
            SevenUIController controller = SevenUIChecks.Controller;
            if (!controller) throw h.Fail("no SevenUI controller on the HUD");
            return controller.Countdowns;
        }

        private static CountdownPanel Find(Harness h, RectTransform root) =>
            Styler(h).Panels.FirstOrDefault(panel => panel.Alive && panel.Root == root);

        private static IEnumerator Run(Harness h, string name, Func<GameObject> load, bool trial)
        {
            GameObject prefab = load();
            Transform hud = SevenUIChecks.HudRoot(h);
            Transform cluster = hud.Find(HudPaths.TopCenterCluster);
            if (!cluster) throw h.Skip("the HUD has no top cluster");
            GameObject instance = Object.Instantiate(prefab, cluster);
            try
            {
                TimerText timer = instance.GetComponentInChildren<TimerText>(true);
                // Champ Rush's panel carries a trial's warning colors too, which the restyle keeps.
                trial = trial || timer.GetComponent<TrialTimerController>();
                RectTransform root = (RectTransform)instance.transform;
                timer.seconds = FullSeconds;
                CountdownPanel panel = null;
                yield return h.Until(() => (panel = Find(h, root)) != null, $"the {name} countdown panel restyled", StyleTimeout);
                yield return h.Wait(0.3f);

                // Flat, in Barlow, spaced capitals.
                Image background = panel.Root.GetComponentsInChildren<Image>().FirstOrDefault(image => image.name == "Background");
                if (!background || !background.enabled || background.sprite || !SevenUIChecks.Near(background.color, Palette.Panel))
                {
                    throw h.Fail($"{name}: the panel is not flat");
                }
                foreach (TMP_Text text in new[] { panel.Label, panel.Time })
                {
                    if (!text.font.name.StartsWith(Texts.BarlowFontPrefix)) throw h.Fail($"{name}: '{text.name}' is in {text.font.name}");
                }
                if ((panel.Label.fontStyle & FontStyles.UpperCase) == 0 || panel.Label.characterSpacing <= 0f)
                {
                    throw h.Fail($"{name}: the label is not in spaced capitals ({panel.Label.fontStyle}, spacing {panel.Label.characterSpacing})");
                }

                // The time as mm:ss, and following the timer.
                yield return h.Until(() => Shown(panel) == FullText, () => $"{name}: '{FullText}' (shows '{Shown(panel)}')", StyleTimeout);
                if (panel.Time.preferredWidth > panel.Root.rect.width) throw h.Fail($"{name}: the time is wider than the panel");
                if (panel.Time.font.name.IndexOf("Condensed", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    throw h.Fail($"{name}: the time is not in Barlow Condensed ({panel.Time.font.name})");
                }
                h.Log($"{name}: label '{panel.Label.text}', time '{panel.Time.text}', total {panel.Total:0.#}");
                CheckPlace(h, name, hud, cluster, panel);
                yield return h.Screenshot($"{name}_full");

                // The bar drains with the time left.
                double total = panel.Total;
                timer.seconds = HalfSeconds;
                yield return h.Until(() => Shown(panel) == "01:00", () => $"{name}: '01:00' (shows '{Shown(panel)}')", StyleTimeout);
                float share = (float)(HalfSeconds / total);
                if (Mathf.Abs(panel.Fill.anchorMax.x - Mathf.Clamp01(share)) > Tolerance || panel.FillImage.color == Palette.LowHealth)
                {
                    throw h.Fail($"{name}: the bar is at {panel.Fill.anchorMax.x:0.00}, not {share:0.00} of {total:0.#} s");
                }

                // Red under ten seconds, and still readable on a trial's warning.
                timer.seconds = LowSeconds;
                yield return h.Until(() => Shown(panel) == LowText, () => $"{name}: '{LowText}' (shows '{Shown(panel)}')", StyleTimeout);
                if (!SevenUIChecks.Near(panel.FillImage.color, Palette.LowHealth)) throw h.Fail($"{name}: the bar is not red under 10 s");
                Color shown = panel.Time.color;
                if (!trial && !SevenUIChecks.Near(shown, Palette.LowHealth)) throw h.Fail($"{name}: the time is not red under 10 s ({shown})");
                if (shown.a < ReadableAlpha || Mathf.Max(shown.r, shown.g, shown.b) < ReadableChannel)
                {
                    throw h.Fail($"{name}: the time's color is hard to read at 5 s ({shown})");
                }
                yield return h.Screenshot($"{name}_low");

                // FlatPanels off gives the game's panel back; on restyles it.
                TimerStringFormatter styledFormat = timer.format;
                h.SetConfig(SevenUIPlugin.Settings.FlatPanels, false);
                yield return h.Until(() => !root.Find(CountdownPanel.HolderName), $"{name}: the mod's panel removed with FlatPanels off", StyleTimeout);
                if (timer.format == styledFormat || TightTimerFormat.IsTight(timer.format)) throw h.Fail($"{name}: the timer's format was not restored");
                // The recorder destroys added components at the frame's end.
                yield return h.Until(() => !root.GetComponent<LayoutIgnorer>(), $"{name}: the panel taken back into its layout", StyleTimeout);
                // The game's typewriter, resumed, turns the label on once it has typed it.
                yield return h.Until(() => timer.targetLabel.enabled, $"{name}: the game's time text shown again", TypingTimeout);
                yield return h.Screenshot($"{name}_off");
                h.SetConfig(SevenUIPlugin.Settings.FlatPanels, true);
                yield return h.Until(() => (panel = Find(h, root)) != null && Shown(panel) == LowText, $"{name}: restyled with FlatPanels on", StyleTimeout);

                // Destroyed with the game's panel, it leaves nothing.
                Object.Destroy(instance);
                yield return h.Until(() => !cluster.GetComponentsInChildren<Transform>(true).Any(t => t.name == CountdownPanel.HolderName)
                    && Styler(h).Panels.All(p => p.Root != root), $"{name}: nothing left of the destroyed panel", StyleTimeout);
            }
            finally
            {
                if (instance)
                {
                    Object.Destroy(instance);
                }
            }
        }

        private static Bounds In(RectTransform space, RectTransform rect)
        {
            Vector3[] corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Bounds bounds = new Bounds(space.InverseTransformPoint(corners[0]), Vector3.zero);
            for (int i = 1; i < corners.Length; i++)
            {
                bounds.Encapsulate(space.InverseTransformPoint(corners[i]));
            }
            return bounds;
        }

        private static bool Overlap(Bounds a, Bounds b) =>
            a.min.x < b.max.x - OverlapTolerance && b.min.x < a.max.x - OverlapTolerance &&
            a.min.y < b.max.y - OverlapTolerance && b.min.y < a.max.y - OverlapTolerance;

        // On the HUD's canvas, and apart from the item bar, the boss bar and the run info on the right.
        private static void CheckPlace(Harness h, string name, Transform hud, Transform cluster, CountdownPanel panel)
        {
            RectTransform canvas = hud.GetComponentInParent<Canvas>().rootCanvas.transform as RectTransform;
            Bounds box = In(canvas, panel.Root);
            if (box.min.x < canvas.rect.xMin || box.max.x > canvas.rect.xMax || box.min.y < canvas.rect.yMin || box.max.y > canvas.rect.yMax)
            {
                throw h.Fail($"{name}: the panel is off the screen ({box})");
            }
            List<(string, RectTransform)> others = new List<(string, RectTransform)>();
            foreach (ItemInventoryDisplay items in cluster.GetComponentsInChildren<ItemInventoryDisplay>(false))
            {
                others.Add(("item bar", (RectTransform)items.transform));
            }
            foreach (HUDBossHealthBarController boss in cluster.GetComponentsInChildren<HUDBossHealthBarController>(false))
            {
                if (boss.container && boss.container.activeInHierarchy)
                {
                    others.Add(("boss bar", (RectTransform)boss.container.transform));
                }
            }
            Transform right = hud.Find(HudPaths.UpperRightCluster);
            if (right)
            {
                foreach (TMP_Text text in right.GetComponentsInChildren<TMP_Text>(false))
                {
                    if (text.gameObject.activeInHierarchy && !string.IsNullOrWhiteSpace(text.text))
                    {
                        others.Add(("right panel text '" + text.name + "'", text.rectTransform));
                    }
                }
            }
            foreach ((string what, RectTransform rect) in others)
            {
                if (Overlap(box, In(canvas, rect))) throw h.Fail($"{name}: the panel overlaps the {what}");
            }
        }
    

        // The time as read, without the tags that give each digit the same width.
        private static readonly Regex Tags = new Regex("<[^>]*>");

        private static string Shown(CountdownPanel panel) => Tags.Replace(panel.Time.text, "");
    }
}
