using RoR2;
using RoR2.UI;
using UnityEngine;
using UnityEngine.UI;

namespace SevenUI
{
    // Sizes the skill row as on the design canvas: 68px slots 8px apart, ending where the game's row ends, with the
    // key labels 5px under them and a 2px divider between the skills and the equipment (the key item's, on the other
    // side, is added with it). The game draws the skills a little smaller, further apart, inside a cluster drawn
    // behind the canvas's plane, so the canvas's pixels are measured on screen once, through the HUD's camera; the
    // skills are then scaled and moved in the cluster's scaler, and the inventory and sprint buttons left of them move
    // by as much as the row grows. Other parts take their text sizes in the canvas's pixels from PixelsPer.
    internal sealed class SkillRow
    {
        public const string DividerName = "SevenUISkillDivider";
        private const float CanvasHeight = 1080f;
        public const float SlotSize = 68f;
        public const float Gap = 8f;
        private const float KeyGap = 5f;
        private const float KeyLineHeight = 1.2f;
        private const float DividerWidth = 2f;
        private const float DividerMargin = 4f;
        // The equipment and key item sit a gap past their dividers, so this much further from the row's end than the
        // skills are from each other, and as a share of a slot.
        public const float DividedExtraGap = DividerMargin * 2f + DividerWidth + Gap;
        public const float DividedExtraGapShare = DividedExtraGap / SlotSize;

        private readonly RectTransform scaler;
        // Canvas pixels per unit of the scaler.
        private readonly float unit;

        private SkillRow(RectTransform scaler, float unit)
        {
            this.scaler = scaler;
            this.unit = unit;
        }

        // Null until the row can be measured: four skills in one scaler, shown on screen.
        public static SkillRow Measure(HUD hud, Transform root)
        {
            SkillIcon[] row = hud.skillIcons;
            if (row == null || row.Length == 0) return null;
            RectTransform scaler = row[0] ? row[0].transform.parent as RectTransform : null;
            if (!scaler) return null;
            foreach (SkillIcon skill in row)
            {
                if (!skill || skill.transform.parent != scaler) return null;
            }
            Canvas canvas = root.GetComponent<Canvas>();
            Camera camera = canvas && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            Rect screen = camera ? camera.pixelRect : new Rect(0f, 0f, Screen.width, Screen.height);
            if (screen.height <= 0f) return null;
            // Up the scaler, which the cluster's slight turn about the vertical axis doesn't shorten.
            Vector2 bottom = RectTransformUtility.WorldToScreenPoint(camera, scaler.TransformPoint(Vector3.zero));
            Vector2 top = RectTransformUtility.WorldToScreenPoint(camera, scaler.TransformPoint(Vector3.up));
            float unit = (top - bottom).magnitude / (screen.height / CanvasHeight);
            return unit > 0f ? new SkillRow(scaler, unit) : null;
        }

        // A length in canvas pixels in the scaler's units.
        public float ScalerLength(float canvasPixels) => canvasPixels / unit;

        // Canvas pixels per unit of a transform drawn in the scaler.
        public float PixelsPer(Transform transform)
        {
            float scale = scaler.lossyScale.y;
            return scale > 0f ? unit * transform.lossyScale.y / scale : 0f;
        }

        // The size in a text's own units that shows at the given canvas size.
        public float FontSize(Transform text, float canvasSize)
        {
            float perUnit = PixelsPer(text);
            return perUnit > 0f ? canvasSize / perUnit : canvasSize;
        }

        public void Apply(HUD hud, Transform extraButtons, StyleRecorder recorder)
        {
            SkillIcon[] row = hud.skillIcons;
            RectTransform last = (RectTransform)row[row.Length - 1].transform;
            Rect lastBox = InScaler(last);
            float rowLeft = InScaler((RectTransform)row[0].transform).xMin;
            float slot = SlotSize / unit;
            float pitch = (SlotSize + Gap) / unit;
            for (int i = 0; i < row.Length; i++)
            {
                RectTransform skill = (RectTransform)row[i].transform;
                if (skill.rect.height <= 0f) continue;
                Vector3 scale = skill.localScale;
                Vector2 position = skill.anchoredPosition;
                recorder.OnRestore(() =>
                {
                    if (!skill) return;
                    skill.localScale = scale;
                    skill.anchoredPosition = position;
                });
                float size = slot / skill.rect.height;
                skill.localScale = new Vector3(size, size, scale.z);
                // The row keeps its right end and its bottom.
                Rect box = InScaler(skill);
                Vector2 target = new Vector2(lastBox.xMax - (row.Length - 1 - i) * pitch - slot, lastBox.yMin);
                skill.anchoredPosition += target - box.min;
                PlaceKey(row[i], recorder);
            }
            float growth = rowLeft - InScaler((RectTransform)row[0].transform).xMin;
            if (extraButtons is RectTransform extras && extras.parent == scaler && growth != 0f)
            {
                recorder.SetAnchoredPosition(extras, extras.anchoredPosition - new Vector2(growth, 0f));
            }
            if (!last.Find(DividerName))
            {
                AddDivider(last, recorder);
            }
        }

        // The key's plate, which fits its label, centered a gap under the slot.
        private void PlaceKey(SkillIcon skill, StyleRecorder recorder)
        {
            InputBindingDisplayController key = skill.GetComponentInChildren<InputBindingDisplayController>(true);
            RectTransform plate = key ? key.transform.parent as RectTransform : null;
            float perUnit = PixelsPer(skill.transform);
            if (!plate || plate.parent != skill.transform || perUnit <= 0f) return;
            RectTransform slot = (RectTransform)skill.transform;
            float center = slot.rect.yMin - (KeyGap + KeyLineHeight * SkillSlotsStyler.KeyFontSize / 2f) / perUnit;
            float current = slot.InverseTransformPoint(plate.TransformPoint(plate.rect.center)).y;
            recorder.SetAnchoredPosition(plate, plate.anchoredPosition + new Vector2(0f, center - current));
        }

        // Between a slot and the slot a divided gap right of it, at the slot's height, shown and hidden with it.
        public void AddDivider(RectTransform slot, StyleRecorder recorder)
        {
            float perUnit = PixelsPer(slot);
            if (perUnit <= 0f) return;
            GameObject divider = new GameObject(DividerName, typeof(RectTransform), typeof(CanvasRenderer));
            divider.layer = slot.gameObject.layer;
            RectTransform rect = (RectTransform)divider.transform;
            rect.SetParent(slot, false);
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2((Gap + DividerMargin + DividerWidth / 2f) / perUnit, 0f);
            rect.sizeDelta = new Vector2(DividerWidth / perUnit, 0f);
            Image image = divider.AddComponent<Image>();
            image.color = Palette.Divider;
            image.raycastTarget = false;
            recorder.Add(divider);
        }

        private Rect InScaler(RectTransform rect)
        {
            Vector2 min = scaler.InverseTransformPoint(rect.TransformPoint(rect.rect.min));
            Vector2 max = scaler.InverseTransformPoint(rect.TransformPoint(rect.rect.max));
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }
    }
}
