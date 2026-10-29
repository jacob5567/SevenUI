using UnityEngine;

namespace SevenUI
{
    // Draws the equipment or key item slot at a skill slot's size, a skill's spacing from the end of the skill row.
    // The game draws both larger than the skills, and places the slots themselves (the key item's through a layout
    // group), so the slot's display root, which holds everything drawn in it, is moved instead. Refitted whenever the
    // skills move. A follower placed against the slot (the N'tormat's page selector, beside the key item) is moved and
    // scaled with it, so it keeps its place and size against the slot as drawn.
    internal sealed class SlotFitter
    {
        private readonly RectTransform display;
        private readonly RectTransform parent;
        // The skill at the row's end the slot sits beside, and the skill next to that one, which gives the spacing.
        private readonly RectTransform end;
        private readonly RectTransform inner;
        private readonly bool leftOfRow;
        private readonly RectTransform follower;
        private readonly Vector2 followerPosition;
        private readonly Vector3 followerScale;
        private Rect lastBox;

        public SlotFitter(RectTransform display, RectTransform end, RectTransform inner, bool leftOfRow, StyleRecorder recorder,
            RectTransform follower = null)
        {
            this.display = display;
            parent = (RectTransform)display.parent;
            this.end = end;
            this.inner = inner;
            this.leftOfRow = leftOfRow;
            Vector2 anchorMin = display.anchorMin;
            Vector2 anchorMax = display.anchorMax;
            Vector2 pivot = display.pivot;
            Vector2 position = display.anchoredPosition;
            Vector2 size = display.sizeDelta;
            recorder.OnRestore(() =>
            {
                if (!display) return;
                display.anchorMin = anchorMin;
                display.anchorMax = anchorMax;
                display.pivot = pivot;
                display.anchoredPosition = position;
                display.sizeDelta = size;
            });
            if (!follower || follower.parent != parent) return;
            this.follower = follower;
            followerPosition = follower.anchoredPosition;
            followerScale = follower.localScale;
            Vector2 position0 = followerPosition;
            Vector3 scale0 = followerScale;
            recorder.OnRestore(() =>
            {
                if (!follower) return;
                follower.anchoredPosition = position0;
                follower.localScale = scale0;
            });
        }

        public bool Alive => display && parent && end && inner;

        public RectTransform Display => display;

        // A few transforms of points; writes only when the skills have moved.
        public void Update()
        {
            if (!Alive) return;
            Rect endBox = InParent(end);
            Rect innerBox = InParent(inner);
            float gap = leftOfRow ? innerBox.xMin - endBox.xMax : endBox.xMin - innerBox.xMax;
            float x = leftOfRow ? endBox.xMin - gap - endBox.width : endBox.xMax + gap;
            Rect box = new Rect(x, endBox.yMin, endBox.width, endBox.height);
            if (box == lastBox) return;
            lastBox = box;
            // Anchored at the parent's bottom-left corner, so the box is set in the parent's own units.
            display.anchorMin = display.anchorMax = display.pivot = Vector2.zero;
            display.anchoredPosition = box.min - parent.rect.min;
            display.sizeDelta = box.size;
            if (follower)
            {
                // The follower's anchor point on the slot, moved to the same point on the box, and its offset from
                // it scaled with the box. It is anchored at a point, so its anchors are equal.
                Rect slot = parent.rect;
                float ratio = box.width / slot.width;
                Vector2 anchor = follower.anchorMin;
                Vector2 onSlot = slot.min + Vector2.Scale(anchor, slot.size);
                Vector2 onBox = box.min + Vector2.Scale(anchor, box.size);
                follower.anchoredPosition = onBox - onSlot + followerPosition * ratio;
                follower.localScale = followerScale * ratio;
            }
        }

        private Rect InParent(RectTransform rect)
        {
            Vector2 min = parent.InverseTransformPoint(rect.TransformPoint(rect.rect.min));
            Vector2 max = parent.InverseTransformPoint(rect.TransformPoint(rect.rect.max));
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }
    }
}
