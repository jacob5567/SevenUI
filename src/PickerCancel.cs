using RoR2.UI;
using TMPro;
using UnityEngine;
using static SevenUI.PickerPanel;

namespace SevenUI
{
    // A picker panel's cancel button, which the game hangs just under its panel: styled as a small outlined button, or
    // later if the game hasn't shown it yet.
    internal sealed class PickerCancel
    {
        private readonly PickerPanel owner;
        private readonly PickerUi ui;

        public PickerCancel(PickerPanel owner, PickerUi ui)
        {
            this.owner = owner;
            this.ui = ui;
        }

        public PickerButton Button { get; private set; }

        // A cancel button the game hadn't shown yet when the panel was styled, styled once it is.
        public bool Waiting { get; private set; }

        public void Clear()
        {
            Button = null;
            Waiting = false;
        }

        // The game's cancel button, which it hangs just under its panel, there for the pointer: a small outlined button
        // at the panel's right under it, with a faint face the stage shows through, a thin grey outline and grey text,
        // lit with the options' accent outline and white text while hovered.
        public void StyleCancel()
        {
            Transform button = ui.Juice.Find(CancelName);
            MPButton mpButton = button ? button.GetComponent<MPButton>() : null;
            if (!mpButton) return;
            // Shown for the first time, a button wakes its components, and the game's skin then puts its own look on it
            // over this one even while turned off; the cursed book shows its cancel button after the panel opens.
            Waiting = !button.gameObject.activeInHierarchy;
            if (Waiting) return;
            Button = ui.StyleButton(mpButton, null, Palette.GhostButton);
            RectTransform rect = (RectTransform)button;
            ui.Recorder.RecordRect(rect);
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = Vector2.one;
            rect.anchoredPosition = new Vector2(0f, -LegendGap);
            rect.sizeDelta = new Vector2(CancelWidth, CancelHeight);
            // The game sizes the label to fit its own larger button, which overrides the font size: its label is
            // 13px regular text, centered in the button.
            foreach (TMP_Text text in button.GetComponentsInChildren<TMP_Text>(true))
            {
                ui.Recorder.Record(text);
                text.enableAutoSizing = false;
                text.enableWordWrapping = false;
                text.fontSize = CancelTextSize;
                text.fontStyle = FontStyles.Normal;
                text.characterSpacing = 0f;
                text.margin = Vector4.zero;
                text.alignment = TextAlignmentOptions.Center;
                text.color = Palette.HeaderText;
                if (!Button.Label)
                {
                    Button.Label = text;
                }
            }
            RectTransform outlines = owner.NewObject("SevenUIPickerCancel", button);
            Stretch(outlines);
            Button.RestOutline = ui.AddOutline(outlines, Palette.Divider, RestOutlineEdge);
            Button.RestOutline.enabled = true;
            Button.Outline = ui.AddOutline(outlines, Palette.Accent, OutlineEdge);
        }
    }
}
