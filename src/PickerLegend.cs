using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static SevenUI.PickerPanel;

namespace SevenUI
{
    // A picker panel's header and key legend: the game's title as the header, and its "Cancel" legend under the panel.
    internal sealed class PickerLegend
    {
        private readonly PickerUi ui;

        public PickerLegend(PickerUi ui)
        {
            this.ui = ui;
        }

        // The game's title, "What is Your Command?", as the header: in the strip over the grid, or over the cards.
        public void StyleTitle()
        {
            TMP_Text title = ui.Title;
            if (!title) return;
            ui.Recorder.Record(title);
            ui.Recorder.RecordRect(title.rectTransform);
            HeaderStyle.Apply(title);
            // Top-aligned, since the void potential's title ends in a line break.
            title.alignment = TextAlignmentOptions.TopLeft;
            title.enableWordWrapping = false;
            title.overflowMode = TextOverflowModes.Overflow;
            // Art the game hangs on the title, such as the delusion panel's outline frames around it.
            foreach (Image decoration in title.GetComponentsInChildren<Image>(true))
            {
                ui.Recorder.Disable(decoration);
            }
        }

        // The game's key legend ("Cancel"), without its backdrop, right-aligned under the panel.
        public void StyleLegend()
        {
            Transform legend = ui.Juice.Find(LegendName);
            if (!legend) return;
            ui.Recorder.Disable(legend.GetComponent<Image>());
            RectTransform rect = (RectTransform)legend;
            ui.Recorder.RecordRect(rect);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -LegendGap);
            HorizontalOrVerticalLayoutGroup group = legend.GetComponent<HorizontalOrVerticalLayoutGroup>();
            if (!group) return;
            ui.Recorder.Record(group);
            TextAnchor alignment = group.childAlignment;
            ui.Recorder.OnRestore(() =>
            {
                if (group)
                {
                    group.childAlignment = alignment;
                }
            });
            group.childAlignment = TextAnchor.UpperRight;
            group.padding = new RectOffset(0, 0, 0, 0);
        }
    }
}
