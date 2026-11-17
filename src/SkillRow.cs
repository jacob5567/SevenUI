using System.Collections.Generic;
using System.Linq;
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
        private readonly List<BaggedCardController> bagCards = new List<BaggedCardController>();

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
            FollowDisplays(hud, recorder);
        }

        // The displays the game hangs over the row, which grew with it, shrunk back to the size the game draws them at.
        // Operator's (DroneTech in code) display of the drones queued to be commanded: an overlay the game creates with
        // the body, after the row is applied, under the utility area, so FollowRow has grown it with the slots
        // already, in the game's proportion to a skill slot, so a fit to the slots would change nothing. But the slots
        // are larger than the game's, which makes the display larger on screen than the game's own. It is therefore
        // shrunk back by the row's growth, to the size the game draws it at, and keeps its bottom right corner where
        // FollowRow put it over the row. Its queues ("Secondary Queue" and "Util Queue") are in
        // it, so they follow. Run on each apply; each instance is fitted once, once its slots have a size.
        public void FollowDisplays(HUD hud, StyleRecorder recorder)
        {
            if (!applied || rowAfter.height <= 0f || rowBefore.height <= 0f) return;
            RectTransform area = scaler.Find(UtilityAreaName) as RectTransform;
            if (hud.targetBodyObject && hud.targetBodyObject.GetComponent<DroneTechController>() && area)
            {
                FollowDroneDisplay(area, recorder);
            }
            FollowBagDisplay(area, recorder);
        }

        private void FollowDroneDisplay(RectTransform area, StyleRecorder recorder)
        {
            foreach (RectTransform display in area.GetComponentsInChildren<RectTransform>(true))
            {
                if (!display.name.StartsWith(DroneOverlayName) || followed.Contains(display)) continue;
                List<RectTransform> slots = display.GetComponentsInChildren<RectTransform>(true)
                    .Where(slot => slot.name.StartsWith(DroneSlotName) && slot.rect.width > 0f && slot.rect.height > 0f)
                    .ToList();
                if (slots.Count == 0) continue;
                followed.Add(display);
                FitAboutCorner(display, slots, rowBefore.height / rowAfter.height, recorder);
            }
        }

        // Drifter's display of what it carries (an arrow, the portrait, the name and a health bar), an overlay the game
        // creates when something is bagged. It is found by its BaggedCardController, whichever body is played, and
        // scaled like Operator's about the bottom right corner of the card.
        // Only an overlay under the grown scaler is fitted: elsewhere the row's growth doesn't apply to it.
        private void FollowBagDisplay(RectTransform area, StyleRecorder recorder)
        {
            scaler.GetComponentsInChildren(true, bagCards);
            foreach (BaggedCardController card in bagCards)
            {
                RectTransform cardRect = (RectTransform)card.transform;
                RectTransform root = OverlayRoot(cardRect, area);
                if (!root || followed.Contains(root) || cardRect.rect.width <= 0f || cardRect.rect.height <= 0f) continue;
                followed.Add(root);
                FitAboutCorner(root, new List<RectTransform> { cardRect }, rowBefore.height / rowAfter.height, recorder);
            }
            bagCards.Clear();
        }

        // The top of the overlay a part is in: the child of the utility area, or of the scaler without one.
        private RectTransform OverlayRoot(RectTransform part, RectTransform area)
        {
            for (Transform at = part; at && at != scaler; at = at.parent)
            {
                if (at.parent == scaler || (area && at.parent == area)) return at as RectTransform;
            }
            return null;
        }

        // Scales a display by a factor about the bottom right corner of the box its slots fill. The display's own rect
        // is no guide: it reaches well below the slots, so scaling about it slides them down past the row.
        private void FitAboutCorner(RectTransform display, List<RectTransform> slots, float factor, StyleRecorder recorder)
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
            Rect before = InScaler(slots);
            display.localScale = new Vector3(scale.x * factor, scale.y * factor, scale.z);
            Rect after = InScaler(slots);
            Vector2 shift = new Vector2(before.xMax - after.xMax, before.yMin - after.yMin);
            display.position += scaler.TransformVector(shift);
        }

        private Rect InScaler(List<RectTransform> rects)
        {
            Rect box = InScaler(rects[0]);
            foreach (RectTransform rect in rects)
            {
                Rect next = InScaler(rect);
                box = Rect.MinMaxRect(Mathf.Min(box.xMin, next.xMin), Mathf.Min(box.yMin, next.yMin),
                    Mathf.Max(box.xMax, next.xMax), Mathf.Max(box.yMax, next.yMax));
            }
            return box;
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
