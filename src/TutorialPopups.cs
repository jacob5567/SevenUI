using RoR2.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SevenUI
{
    // The HUD's tutorial popups (equipment, sprinting, remote operation), drawn as the tooltip's dark panel with the
    // accent strip along the top, the text in the HUD's white and the arrow in the panel's color, in place of the
    // game's solid yellow with black text. The game still decides when they show (HUDTutorialController), sizes them
    // to their text, places them and animates them in.
    internal static class TutorialPopups
    {
        public const string StripName = "SevenUITutorialStrip";
        private const string ArrowName = "TutorialArrow";
        private const string TextName = "TutorialText";

        // In the design canvas's pixels.
        private const float FontSize = 14f;
        private const float StripHeight = 2f;
        private const float PaddingX = 12f;
        private const float PaddingTop = 8f;
        private const float PaddingBottom = 6f;

        // The popups sit in the skill cluster's scaler, so their canvas sizes come from the skill row's measure.
        public static void Style(HUD hud, SkillRow row, StyleRecorder recorder)
        {
            HUDTutorialController tutorials = hud.GetComponent<HUDTutorialController>();
            if (!tutorials) return;
            Style(tutorials.equipmentTutorialObject, row, recorder);
            Style(tutorials.sprintTutorialObject, row, recorder);
            Style(tutorials.remoteOpTutorialObject, row, recorder);
        }

        private static void Style(GameObject popup, SkillRow row, StyleRecorder recorder)
        {
            if (!popup || !popup.transform.parent || popup.transform.Find(StripName)) return;
            // Measured by the parent, as the popup's own scale is animated from nothing as it pops in; it rests at 1.
            float perUnit = row.PixelsPer(popup.transform.parent);
            if (perUnit <= 0f) return;

            Image panel = popup.GetComponent<Image>();
            if (panel)
            {
                recorder.Record(panel);
                panel.sprite = null;
                panel.type = Image.Type.Simple;
                panel.color = Palette.TooltipPanel;
            }
            HorizontalOrVerticalLayoutGroup layout = popup.GetComponent<HorizontalOrVerticalLayoutGroup>();
            if (layout)
            {
                recorder.Record(layout);
                layout.padding.left = Mathf.RoundToInt(PaddingX / perUnit);
                layout.padding.right = layout.padding.left;
                layout.padding.top = Mathf.RoundToInt(PaddingTop / perUnit);
                layout.padding.bottom = Mathf.RoundToInt(PaddingBottom / perUnit);
                // Padding is read at the next layout pass, which a changed value doesn't ask for by itself.
                LayoutRebuilder.MarkLayoutForRebuild((RectTransform)popup.transform);
            }
            Image arrow = popup.transform.Find(ArrowName)?.GetComponent<Image>();
            if (arrow)
            {
                recorder.Record(arrow);
                arrow.color = Palette.TooltipPanel;
            }
            TMP_Text text = popup.transform.Find(TextName)?.GetComponent<TMP_Text>();
            if (text)
            {
                recorder.Record(text);
                text.fontSize = FontSize / perUnit;
                text.color = Palette.Text;
            }
            AddStrip(popup, StripHeight / perUnit, recorder);
        }

        // Out of the popup's layout group, which would otherwise line it up beside the text.
        private static void AddStrip(GameObject popup, float height, StyleRecorder recorder)
        {
            GameObject strip = new GameObject(StripName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image),
                typeof(LayoutIgnorer));
            strip.layer = popup.layer;
            RectTransform rect = (RectTransform)strip.transform;
            rect.SetParent(popup.transform, false);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(0f, height);
            Image image = strip.GetComponent<Image>();
            image.color = Palette.Accent;
            image.raycastTarget = false;
            recorder.Add(strip);
        }
    }
}
