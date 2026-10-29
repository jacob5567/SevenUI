using RoR2;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SevenUI
{
    // Lays out one objective's row in the objective panel as on the design canvas: its text left-aligned at the
    // canvas's size after a small checkbox, in place of the game's centered text beside a large one. Each strip lays
    // out its text with its own layout group, padded on the left for the checkbox; the group is set to give the text
    // the row's whole width, so a long objective wraps and the row grows to fit it.
    internal static class ObjectiveRow
    {
        // Names in the objective strip's ChildLocator (ObjectivePanelController.ObjectiveTracker).
        private const string LabelName = "Label";
        private const string CheckboxName = "Checkbox";

        // The design canvas's sizes, in its pixels, scaled to the run info's units: 16px text after a 14px box, and
        // rows 8px apart.
        private const float Canvas = RunInfoUnits.PerCanvasPixel;
        public const float FontSize = 16f * Canvas;
        private const float RowInset = 14f * Canvas;
        private const float BoxSize = 14f * Canvas;
        private const float BoxGap = 8f * Canvas;
        private const float RowGap = 8f * Canvas;
        private const float LineHeight = 1.2f;

        // False for a strip without the game's label, layout group and layout element.
        public static bool Style(Transform strip, StyleRecorder recorder)
        {
            ChildLocator locator = strip.GetComponent<ChildLocator>();
            TMP_Text label = locator ? locator.FindChildComponent<TMP_Text>(LabelName) : null;
            VerticalLayoutGroup layout = strip.GetComponent<VerticalLayoutGroup>();
            LayoutElement element = strip.GetComponent<LayoutElement>();
            if (!label || !layout || !element || label.transform.parent != strip) return false;

            recorder.Record(label);
            recorder.Record(layout);
            recorder.Record(element);
            label.fontSize = FontSize;
            label.alignment = TextAlignmentOptions.Left;

            bool controlWidth = layout.childControlWidth;
            bool expandWidth = layout.childForceExpandWidth;
            recorder.OnRestore(() =>
            {
                if (!layout) return;
                layout.childControlWidth = controlWidth;
                layout.childForceExpandWidth = expandWidth;
            });
            layout.childControlWidth = true;
            layout.childForceExpandWidth = true;
            int gap = Mathf.RoundToInt(RowGap / 2f);
            layout.padding = new RectOffset(Mathf.RoundToInt(RowInset + BoxSize + BoxGap), Mathf.RoundToInt(RowInset), gap, gap);
            element.minHeight = FontSize * LineHeight + RowGap;

            // The checkbox is the label's child, placed against its left side.
            RectTransform box = locator.FindChild(CheckboxName) as RectTransform;
            if (box)
            {
                recorder.RecordRect(box);
                box.anchorMin = box.anchorMax = new Vector2(0f, 0.5f);
                box.pivot = new Vector2(0.5f, 0.5f);
                box.sizeDelta = new Vector2(BoxSize, BoxSize);
                box.anchoredPosition = new Vector2(-(BoxGap + BoxSize / 2f), 0f);
            }
            return true;
        }
    }
}
