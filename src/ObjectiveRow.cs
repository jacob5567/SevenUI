using RoR2;
using RoR2.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SevenUI
{
    // Lays out one objective's row in the objective panel as on the design canvas: its text left-aligned at the
    // canvas's size after a small flat checkbox (ObjectiveCheckbox), in place of the game's centered text beside a
    // large one. Each strip lays out its text with its own layout group, padded on the left for the checkbox; the group
    // is set to give the text the row's whole width, so a long objective wraps and the row grows to fit it. The
    // teleporter's charge has no checkbox: its percentage sits at the right and a thin bar under the text (ChargeRow).
    // The texts are drawn without the game's inline icons (ObjectiveIcons).
    internal static class ObjectiveRow
    {
        // Names in the objective strip's ChildLocator (ObjectivePanelController.ObjectiveTracker).
        private const string LabelName = "Label";
        private const string CheckboxName = "Checkbox";
        public const string BoxName = "SevenUICheckbox";
        public const string ChargeName = "SevenUICharge";

        // The design canvas's sizes, in its pixels, scaled to the run info's units: 16px text after a 14px box with a
        // 2px edge, and rows 8px apart. The charge's bar is 4px, 6px under the text, and its percentage takes up to
        // 44px at the right.
        private const float Canvas = RunInfoUnits.PerCanvasPixel;
        public const float FontSize = 16f * Canvas;
        private const float RowInset = 14f * Canvas;
        private const float BoxSize = 14f * Canvas;
        private const float BoxEdge = 2f * Canvas;
        private const float BoxGap = 8f * Canvas;
        private const float RowGap = 8f * Canvas;
        private const float LineHeight = 1.2f;
        private const float ChargeBarHeight = 4f * Canvas;
        private const float ChargeBarGap = 6f * Canvas;
        private const float PercentWidth = 44f * Canvas;
        // The outline sprite's edge, in its pixels (FlatSprite.Outline).
        private const float OutlineEdgePixels = 2f;

        // False for a strip without the game's label, layout group and layout element. The panel, when given, names
        // the strip's objective.
        public static bool Style(Transform strip, ObjectivePanelController panel, StyleRecorder recorder)
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
            // Also on the charge's hidden label, whose lines set the row's height.
            ObjectiveIcons.Apply(label, recorder);

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
            element.minHeight = FontSize * LineHeight + RowGap;

            RectTransform box = locator.FindChild(CheckboxName) as RectTransform;
            HoldoutZoneController zone = ChargeZone(strip, panel);
            if (zone)
            {
                // Room at the right for the percentage, and under the text for the bar.
                int under = Mathf.RoundToInt(RowGap / 2f + ChargeBarGap + ChargeBarHeight);
                layout.padding = new RectOffset(Mathf.RoundToInt(RowInset), Mathf.RoundToInt(RowInset + PercentWidth), gap, under);
                element.minHeight += ChargeBarGap + ChargeBarHeight;
                if (box)
                {
                    recorder.SetActive(box.gameObject, false);
                }
                if (!label.transform.Find(ChargeName))
                {
                    AddCharge(strip, label, zone, recorder);
                }
                return true;
            }

            layout.padding = new RectOffset(Mathf.RoundToInt(RowInset + BoxSize + BoxGap), Mathf.RoundToInt(RowInset), gap, gap);
            // The checkbox is the label's child, placed against its left side.
            if (box)
            {
                recorder.RecordRect(box);
                box.anchorMin = box.anchorMax = new Vector2(0f, 0.5f);
                box.pivot = new Vector2(0.5f, 0.5f);
                box.sizeDelta = new Vector2(BoxSize, BoxSize);
                box.anchoredPosition = new Vector2(-(BoxGap + BoxSize / 2f), 0f);
                Image image = box.GetComponent<Image>();
                if (image && panel && !box.Find(BoxName))
                {
                    AddBox(box, image, panel.checkboxActiveSprite, recorder);
                }
            }
            return true;
        }

        // The holdout zone (the teleporter's, or another charging zone's) whose charge the strip shows, if it does.
        private static HoldoutZoneController ChargeZone(Transform strip, ObjectivePanelController panel)
        {
            if (!panel) return null;
            foreach (ObjectivePanelController.ObjectiveTracker tracker in panel.objectiveTrackers)
            {
                if (tracker.stripObject == strip.gameObject && tracker is HoldoutZoneController.ChargeHoldoutZoneObjectiveTracker)
                {
                    return tracker.sourceDescriptor.source as HoldoutZoneController;
                }
            }
            return null;
        }

        private static void AddBox(RectTransform checkbox, Image image, Sprite openSprite, StyleRecorder recorder)
        {
            GameObject created = NewUIObject(BoxName, checkbox);
            Stretch((RectTransform)created.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Image outline = created.AddComponent<Image>();
            outline.sprite = FlatSprite.Outline;
            outline.type = Image.Type.Sliced;
            outline.pixelsPerUnitMultiplier = OutlineEdgePixels / BoxEdge;
            outline.color = Palette.HeaderText;
            outline.raycastTarget = false;
            outline.enabled = false;
            ObjectiveCheckbox follower = created.AddComponent<ObjectiveCheckbox>();
            follower.Checkbox = image;
            follower.OpenSprite = openSprite;
            follower.Box = outline;
            // The follower turns the game's picture off while the box is open; restoring turns it back on.
            recorder.SetEnabled(image, image.enabled);
            recorder.Add(created);
        }

        // The copy of the text over the game's, the percentage beside its first line, and the bar under it, all in the
        // game label's font so the font swap gives them Barlow like the rest of the HUD.
        private static void AddCharge(Transform strip, TMP_Text label, HoldoutZoneController zone, StyleRecorder recorder)
        {
            GameObject holder = NewUIObject(ChargeName, label.transform);
            Stretch((RectTransform)holder.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            TMP_Text copy = AddText(holder.transform, label);
            Stretch(copy.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            copy.alignment = TextAlignmentOptions.Left;
            copy.enableWordWrapping = label.enableWordWrapping;
            ObjectiveIcons.ApplyToCopy(copy);

            TMP_Text percent = AddText(holder.transform, label);
            RectTransform percentRect = percent.rectTransform;
            percentRect.anchorMin = percentRect.anchorMax = Vector2.one;
            percentRect.pivot = Vector2.one;
            percentRect.sizeDelta = new Vector2(PercentWidth, FontSize * LineHeight);
            percentRect.anchoredPosition = new Vector2(PercentWidth, 0f);
            percent.alignment = TextAlignmentOptions.TopRight;
            percent.fontStyle = FontStyles.Bold;
            percent.color = Palette.Accent;
            percent.enableWordWrapping = false;

            // Under the text, across the row inside its insets: the strip's own child, outside its layout.
            GameObject bar = NewUIObject("SevenUIChargeBar", strip);
            bar.AddComponent<LayoutIgnorer>();
            Stretch((RectTransform)bar.transform, Vector2.zero, new Vector2(1f, 0f), new Vector2(RowInset, RowGap / 2f),
                new Vector2(-RowInset, RowGap / 2f + ChargeBarHeight));
            Image trough = bar.AddComponent<Image>();
            trough.color = Palette.Trough;
            trough.raycastTarget = false;
            GameObject fill = NewUIObject("Fill", bar.transform);
            RectTransform fillRect = (RectTransform)fill.transform;
            Stretch(fillRect, Vector2.zero, new Vector2(0f, 1f), Vector2.zero, Vector2.zero);
            Image fillImage = fill.AddComponent<Image>();
            fillImage.color = Palette.Accent;
            fillImage.raycastTarget = false;

            ChargeRow row = holder.AddComponent<ChargeRow>();
            row.Zone = zone;
            row.GameLabel = label;
            row.Label = copy;
            row.Percent = percent;
            row.Fill = fillRect;
            recorder.Add(holder);
            recorder.Add(bar);
        }

        private static TMP_Text AddText(Transform parent, TMP_Text like)
        {
            GameObject line = NewUIObject("Text", parent);
            TextMeshProUGUI text = line.AddComponent<TextMeshProUGUI>();
            text.font = like.font;
            text.fontSharedMaterial = like.fontSharedMaterial;
            text.spriteAsset = like.spriteAsset;
            text.fontSize = like.fontSize;
            text.richText = true;
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false;
            return text;
        }

        private static GameObject NewUIObject(string name, Transform parent)
        {
            GameObject created = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
            created.layer = parent.gameObject.layer;
            created.transform.SetParent(parent, false);
            return created;
        }

        private static void Stretch(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin,
            Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }
    }
}
