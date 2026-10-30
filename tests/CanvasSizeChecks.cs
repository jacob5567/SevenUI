using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ModTestRunner;
using RoR2;
using RoR2.UI;
using TMPro;
using UnityEngine;

namespace SevenUI.Tests
{
    // Checks that the bottom left draws at the design canvas's sizes on screen, in its pixels on a 1080-pixel-high
    // screen: the level's block beside the bars, the health and experience bars' heights, and the buffs' counts in
    // their squares' top-right corners.
    internal static class CanvasSizeChecks
    {
        private const float CanvasHeight = 1080f;
        // Within a pixel or so: the clusters are drawn in perspective, and turned slightly before the mod levels them.
        private const float SizeTolerance = 1.5f;
        private const float HealthHeight = 22f;
        private const float ExpHeight = 4f;
        // The level's 52px block and the 12px gap after it.
        private const float BarsInset = 64f;

        // Canvas pixels per screen pixel, and a rect's box on screen in canvas pixels.
        private static (Camera camera, float unit) Screen(Harness h)
        {
            Camera camera = CanvasLayoutChecks.HudCamera(SevenUIChecks.HudRoot(h));
            return (camera, CanvasLayoutChecks.ScreenOf(camera).height / CanvasHeight);
        }

        private static Rect Box(Harness h, RectTransform rect)
        {
            (Camera camera, float unit) = Screen(h);
            Rect box = CanvasLayoutChecks.OnScreen(camera, rect);
            return new Rect(box.x / unit, box.y / unit, box.width / unit, box.height / unit);
        }

        private static bool Near(float value, float expected) => Mathf.Abs(value - expected) < SizeTolerance;

        private static string BoxText(Rect box) => $"x {box.xMin:0.#}-{box.xMax:0.#} y {box.yMin:0.#}-{box.yMax:0.#}";

        public static IEnumerator ExpectPlayerBarsAsCanvas(this Harness h)
        {
            string look = null;
            yield return h.Until(
                () =>
                {
                    RectTransform bars = SevenUIChecks.HudRoot(h).Find(GameNames.PlayerBars) as RectTransform;
                    RectTransform health = bars ? bars.Find("HealthbarRoot") as RectTransform : null;
                    RectTransform exp = bars ? bars.Find(GameNames.ExpBar) as RectTransform : null;
                    RectTransform level = bars ? bars.Find(GameNames.LevelRoot) as RectTransform : null;
                    if (!health || !exp || !level) return false;
                    Rect all = Box(h, bars);
                    Rect healthBox = Box(h, health);
                    Rect expBox = Box(h, exp);
                    Rect levelBox = Box(h, level);
                    look = $"bars {BoxText(all)}, health {BoxText(healthBox)}, experience {BoxText(expBox)}, level {BoxText(levelBox)}";
                    return Near(healthBox.height, HealthHeight) && Near(expBox.height, ExpHeight) &&
                           Near(healthBox.xMin - all.xMin, BarsInset) && Near(expBox.xMin, healthBox.xMin) &&
                           Near(expBox.yMin, all.yMin) && healthBox.yMin > expBox.yMax && levelBox.xMax <= healthBox.xMin;
                },
                () => $"the level beside a {HealthHeight}px health bar over a {ExpHeight}px experience bar, " +
                      $"{BarsInset}px in ({look}) {h.Diagnosis()}");
        }

        // Each stacking buff with more than one stack shows its count in its square's top-right quarter, and the game's
        // stack text is hidden.
        public static IEnumerator ExpectBuffCountsInCorner(this Harness h)
        {
            string look = null;
            yield return h.Until(
                () =>
                {
                    BuffDisplay display = CanvasLayoutChecks.PlayerBuffs(h);
                    Transform labels = display ? display.transform.Find(BuffRows.LabelsName) : null;
                    if (!labels) return false;
                    List<BuffIcon> stacked = display.buffIconDisplayData
                        .Select(data => data.buffIconComponent)
                        .Where(icon => icon && icon.gameObject.activeSelf && icon.buffDef && icon.buffDef.canStack &&
                                       icon.buffCount + icon.buffDef.displayOffset > 1)
                        .ToList();
                    List<TMP_Text> counts = labels.Cast<Transform>().Where(child => child.name == "Count" && child.gameObject.activeSelf)
                        .Select(child => child.GetComponent<TMP_Text>()).ToList();
                    look = $"stacked {string.Join(" ", stacked.Select(icon => icon.buffCount))}, counts " +
                           string.Join(" ", counts.Select(count => $"'{count.text}'"));
                    if (stacked.Count == 0) return false;
                    return stacked.All(icon =>
                    {
                        string expected = (icon.buffCount + icon.buffDef.displayOffset).ToString();
                        Rect square = Box(h, icon.rectTransform);
                        return icon.stackCount.alpha == 0f && counts.Any(count =>
                        {
                            if (count.text != expected) return false;
                            Bounds text = count.textBounds;
                            Vector3 center = count.transform.TransformPoint(text.center);
                            (Camera camera, float unit) = Screen(h);
                            Vector2 point = RectTransformUtility.WorldToScreenPoint(camera, center) / unit;
                            return point.x > square.center.x && point.y > square.center.y && point.x < square.xMax + SizeTolerance * 4f;
                        });
                    });
                },
                () => $"the stack counts in the buffs' top-right corners ({look}) {h.Diagnosis()}");
        }
    }
}
