using System.Collections.Generic;
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
    // by as much as the row grows. The slot some survivors' meters hang from, above the row, keeps its place over it.
    // Other parts take their text sizes in the canvas's pixels from PixelsPer.
    internal sealed class SkillRow
    {
        public const string DividerName = "SevenUISkillDivider";
        // An empty slot above the skills that the game parents Operator's (DroneTech in code) drones and Seeker's lotus
        // to, placed for the game's row (HUD child locator entry RightUtilityArea).
        private const string UtilityAreaName = "UtilityArea";
        // Operator's display, hung in the utility area, and the slots in its queues ("DroneSlot1" to "DroneSlot3").
        public const string DroneOverlayName = "DroneTechDronesOverlay";
        public const string DroneSlotName = "DroneSlot";
        private const float FitTolerance = 0.01f;
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
        // The row as the game draws it and as SevenUI does, once applied, for the elements that appear later.
        private Rect rowBefore;
        private Rect rowAfter;
        private bool applied;
        // The survivors' own displays already fitted, which are put back with the row.
        private readonly HashSet<RectTransform> followed = new HashSet<RectTransform>();

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
            Rect screen = UiFactory.ScreenRect(camera);
            if (screen.height <= 0f) return null;
            // Up the scaler, which the cluster's slight turn about the vertical axis doesn't shorten.
            Vector2 bottom = RectTransformUtility.WorldToScreenPoint(camera, scaler.TransformPoint(Vector3.zero));
            Vector2 top = RectTransformUtility.WorldToScreenPoint(camera, scaler.TransformPoint(Vector3.up));
            float unit = (top - bottom).magnitude / (screen.height / HudUnits.CanvasHeight);
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
            Rect before = Rect.MinMaxRect(rowLeft, lastBox.yMin, lastBox.xMax, lastBox.yMax);
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
            Rect after = Rect.MinMaxRect(InScaler((RectTransform)row[0].transform).xMin, lastBox.yMin, lastBox.xMax,
                InScaler(last).yMax);
            FollowRow(scaler.Find(UtilityAreaName) as RectTransform, before, after, recorder);
            rowBefore = before;
            rowAfter = after;
            applied = true;
            FollowDroneDisplay(hud, recorder);
        }

        // Operator's (DroneTech in code) display of the drones queued to be commanded: an overlay the game creates with
        // the body, after the row is applied, under the utility area, so it has grown with the slots already. Its
        // big selected slot is then wider than a skill slot, so the overlay is fitted to a slot's width about its
        // bottom middle, which keeps it over the row; its queues ("Secondary Queue" and "Util Queue") are in it, so they
        // follow. Run on each apply; each instance is fitted once, once its slots have a size.
        public void FollowDroneDisplay(HUD hud, StyleRecorder recorder)
        {
            if (!applied || !hud.targetBodyObject || !hud.targetBodyObject.GetComponent<DroneTechController>()) return;
            RectTransform area = scaler.Find(UtilityAreaName) as RectTransform;
            if (!area) return;
            foreach (RectTransform display in area.GetComponentsInChildren<RectTransform>(true))
            {
                if (!display.name.StartsWith(DroneOverlayName) || followed.Contains(display)) continue;
                float width = 0f;
                foreach (RectTransform slot in display.GetComponentsInChildren<RectTransform>(true))
                {
                    if (slot.name.StartsWith(DroneSlotName))
                    {
                        width = Mathf.Max(width, slot.rect.width * PixelsPer(slot));
                    }
                }
                if (width <= 0f) continue;
                followed.Add(display);
                FitAboutBottom(display, SlotSize / width, recorder);
            }
        }

        // Scales a display by a factor about the middle of its bottom edge.
        private static void FitAboutBottom(RectTransform display, float factor, StyleRecorder recorder)
        {
            if (Mathf.Abs(factor - 1f) < FitTolerance) return;
            Vector3 scale = display.localScale;
            Vector2 position = display.anchoredPosition;
            recorder.OnRestore(() =>
            {
                if (!display) return;
                display.localScale = scale;
                display.anchoredPosition = position;
            });
            Vector3 bottom = new Vector3(display.rect.center.x, display.rect.yMin, 0f);
            Vector3 before = display.TransformPoint(bottom);
            display.localScale = new Vector3(scale.x * factor, scale.y * factor, scale.z);
            display.position += before - display.TransformPoint(bottom);
        }

        // Scaled with the slots about the row's top middle, so whatever hangs from it sits over the row as the game
        // draws it over its own.
        private static void FollowRow(RectTransform area, Rect before, Rect after, StyleRecorder recorder)
        {
            if (!area || before.height <= 0f) return;
            float growth = after.height / before.height;
            Vector3 scale = area.localScale;
            Vector2 position = area.anchoredPosition;
            recorder.OnRestore(() =>
            {
                if (!area) return;
                area.localScale = scale;
                area.anchoredPosition = position;
            });
            area.localScale = new Vector3(scale.x * growth, scale.y * growth, scale.z);
            Vector2 from = new Vector2(before.center.x, before.yMax);
            Vector2 to = new Vector2(after.center.x, after.yMax);
            Vector2 at = area.localPosition;
            area.anchoredPosition += to + (at - from) * growth - at;
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
            RectTransform rect = UiFactory.NewObject(DividerName, slot);
            GameObject divider = rect.gameObject;
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
