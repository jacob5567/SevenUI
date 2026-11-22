using UnityEngine;

namespace SevenUI
{
    // Splits a row into equal spans with gaps between, every edge on a whole screen pixel. A 2px gap of the design
    // canvas is 2.67 screen pixels at 1440p, so spans laid out in the canvas's units draw gaps 2 or 3 pixels wide
    // depending on where they land. Here each gap is a whole number of pixels (at least one), all the same, and the
    // pixels left over go to the spans, one each from the left.
    internal static class PixelSpans
    {
        private static readonly Vector3[] Corners = new Vector3[4];

        // The row's left and right edges on the screen, in pixels; false when it has no width there.
        public static bool ScreenEdges(RectTransform row, Camera eye, out float left, out float right)
        {
            row.GetWorldCorners(Corners);
            left = RectTransformUtility.WorldToScreenPoint(eye, Corners[0]).x;
            right = RectTransformUtility.WorldToScreenPoint(eye, Corners[2]).x;
            return right > left;
        }

        // Each span's left edge and width in the row's own units, measured from the row's left edge. False when the row
        // is too narrow for a pixel per span.
        public static bool Split(RectTransform row, Camera eye, float gap, float[] lefts, float[] widths, int count)
        {
            if (count <= 0 || !ScreenEdges(row, eye, out float screenLeft, out float screenRight)) return false;
            Rect rect = row.rect;
            float pixelsPerUnit = (screenRight - screenLeft) / rect.width;
            float left = Mathf.Round(screenLeft);
            int room = Mathf.RoundToInt(screenRight) - (int)left;
            int gapPixels = count > 1 ? Mathf.Max(1, Mathf.RoundToInt(gap * pixelsPerUnit)) : 0;
            int spanRoom = room - gapPixels * (count - 1);
            if (spanRoom < count) return false;
            int each = spanRoom / count;
            int extra = spanRoom - each * count;
            float at = left;
            for (int i = 0; i < count; i++)
            {
                int width = each + (i < extra ? 1 : 0);
                lefts[i] = (at - screenLeft) / pixelsPerUnit;
                widths[i] = width / pixelsPerUnit;
                at += width + gapPixels;
            }
            return true;
        }

        // The camera the row's canvas is drawn by; none for an overlay canvas.
        public static Camera Eye(Component at)
        {
            Canvas canvas = at.GetComponentInParent<Canvas>();
            Canvas root = canvas ? canvas.rootCanvas : null;
            return root && root.renderMode != RenderMode.ScreenSpaceOverlay ? root.worldCamera : null;
        }
    }
}
