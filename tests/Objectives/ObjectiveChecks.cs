using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ModTestRunner;
using RoR2.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SevenUI.Tests
{
    // Checks the test objectives' strips once they show, as the design canvas's rows: the text left-aligned at the
    // canvas's size without the game's inline icons, everything the strip draws inside the panel and inside its own
    // row, so no part sticks out of the panel or over the next row; and on a trial's strip, the mod's thin bar under
    // the text filled to the trial's progress, in place of the game's bar and the badge left of the text.
    internal static class ObjectiveChecks
    {
        private const string LabelName = "Label";
        private const string GameProgressBarName = "ProgressBar";
        // In the panel's units: a pixel and a half of the canvas.
        private const float Tolerance = 1.5f * RunInfoUnits.PerCanvasPixel;
        private const float ProgressTolerance = 0.02f;

        public static IEnumerator ExpectObjectivesAsCanvas(this Harness h, List<TestObjectiveSource> sources)
        {
            yield return TestObjectives.SkipStripAnimations(h, sources);
            List<string> problems = new List<string>();
            yield return h.Until(
                () =>
                {
                    problems = Problems(h, sources);
                    return problems.Count == 0;
                },
                () => $"{sources.Count} objectives styled as the canvas's rows, inside the panel and their own rows " +
                      $"({string.Join("; ", problems)}) {h.Diagnosis()}");
        }

        private static List<string> Problems(Harness h, List<TestObjectiveSource> sources)
        {
            List<string> problems = new List<string>();
            RectTransform panel = h.FindUnderHud(GameNames.ObjectivePanel) as RectTransform;
            ObjectivePanelController controller = panel ? panel.GetComponent<ObjectivePanelController>() : null;
            if (!controller)
            {
                problems.Add("no objective panel");
                return problems;
            }
            foreach (TestObjectiveSource source in sources)
            {
                ObjectivePanelController.ObjectiveTracker tracker =
                    controller.objectiveTrackers.FirstOrDefault(shown => shown.sourceDescriptor.source == source);
                string problem = tracker != null ? Problem(panel, tracker.stripObject, source) : "not shown";
                if (problem != null)
                {
                    problems.Add($"'{source.Text}': {problem}");
                }
            }
            return problems;
        }

        private static string Problem(RectTransform panel, GameObject strip, TestObjectiveSource source)
        {
            ChildLocator locator = strip ? strip.GetComponent<ChildLocator>() : null;
            TMP_Text label = locator ? locator.FindChildComponent<TMP_Text>(LabelName) : null;
            if (!label) return "no label";
            CanvasGroup group = strip.GetComponent<CanvasGroup>();
            if (group && group.alpha < 1f) return $"faded to {group.alpha:0.##}";
            // A strip's text is measured once it's drawn; until then its bounds are empty and nothing is checked.
            if (label.text != source.Text || label.textInfo == null || label.textInfo.characterCount == 0)
            {
                return $"text not drawn ('{label.text}')";
            }
            if (label.alignment != TextAlignmentOptions.Left || !Mathf.Approximately(label.fontSize, ObjectiveRow.FontSize))
            {
                return $"label not styled ({label.alignment}, {label.fontSize:0.##})";
            }
            if (CanvasLayoutChecks.DrawsSprite(label)) return "draws an icon";

            Rect panelBox = panel.rect;
            Rect row = InPanel(panel, (RectTransform)strip.transform);
            Rect text = InPanel(panel, label.transform, label.textBounds);
            foreach ((string name, Rect box) in Drawn(panel, strip))
            {
                if (box.xMin < panelBox.xMin - Tolerance || box.xMax > panelBox.xMax + Tolerance)
                {
                    return $"{name} {BoxText(box)} out of the panel {BoxText(panelBox)}";
                }
                if (box.yMin < row.yMin - Tolerance || box.yMax > row.yMax + Tolerance)
                {
                    return $"{name} {BoxText(box)} out of its row {BoxText(row)}";
                }
            }

            Image gameBar = locator.FindChildComponent<Image>(GameProgressBarName);
            if (!gameBar) return null;
            if (gameBar.enabled && gameBar.gameObject.activeInHierarchy) return "the game's progress bar still drawn";
            RectTransform bar = strip.transform.Find(ObjectiveRow.ProgressName) as RectTransform;
            RectTransform fill = bar && bar.childCount > 0 ? bar.GetChild(0) as RectTransform : null;
            if (!fill || !bar.gameObject.activeInHierarchy) return "no progress bar of the mod's";
            Rect barBox = InPanel(panel, bar);
            float share = barBox.width > 0f ? InPanel(panel, fill).width / barBox.width : 0f;
            if (Mathf.Abs(share - source.Progress) > ProgressTolerance) return $"bar filled to {share:P0}, not {source.Progress:P0}";
            if (barBox.yMax > text.yMin + Tolerance) return $"bar {BoxText(barBox)} not under the text {BoxText(text)}";
            return null;
        }

        // Each visible image and text in the strip, and any sprite drawn in it, in the panel's units.
        private static IEnumerable<(string name, Rect box)> Drawn(RectTransform panel, GameObject strip)
        {
            foreach (Graphic graphic in strip.GetComponentsInChildren<Graphic>())
            {
                if (!graphic.enabled || graphic.color.a <= 0f) continue;
                if (graphic is TMP_Text text)
                {
                    if (text.textInfo == null || text.textInfo.characterCount == 0 || text.alpha <= 0f) continue;
                    yield return (graphic.name, InPanel(panel, text.transform, text.textBounds));
                }
                else if (graphic.rectTransform.rect.width > 0f && graphic.rectTransform.rect.height > 0f)
                {
                    yield return (graphic.name, InPanel(panel, graphic.rectTransform));
                }
            }
            foreach (SpriteRenderer sprite in strip.GetComponentsInChildren<SpriteRenderer>())
            {
                if (!sprite.enabled || !sprite.sprite) continue;
                Bounds bounds = sprite.bounds;
                Vector2 min = panel.InverseTransformPoint(bounds.min);
                Vector2 max = panel.InverseTransformPoint(bounds.max);
                yield return (sprite.name, Rect.MinMaxRect(Mathf.Min(min.x, max.x), Mathf.Min(min.y, max.y),
                    Mathf.Max(min.x, max.x), Mathf.Max(min.y, max.y)));
            }
        }

        private static Rect InPanel(RectTransform panel, RectTransform rect)
        {
            Rect local = rect.rect;
            return InPanel(panel, rect, new Bounds(local.center, local.size));
        }

        private static Rect InPanel(RectTransform panel, Transform transform, Bounds local)
        {
            Vector2 min = panel.InverseTransformPoint(transform.TransformPoint(local.min));
            Vector2 max = panel.InverseTransformPoint(transform.TransformPoint(local.max));
            return Rect.MinMaxRect(Mathf.Min(min.x, max.x), Mathf.Min(min.y, max.y), Mathf.Max(min.x, max.x), Mathf.Max(min.y, max.y));
        }

        private static string BoxText(Rect box) => $"x {box.xMin:0.#}-{box.xMax:0.#} y {box.yMin:0.#}-{box.yMax:0.#}";
    }
}
