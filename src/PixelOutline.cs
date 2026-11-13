using UnityEngine;
using UnityEngine.UI;

namespace SevenUI
{
    // A square outline whose sides land on whole screen pixels, all as wide. A sliced sprite's thin edge falls wherever
    // the layout puts it: at 1440p a 1px edge of the design canvas is 1.33 screen pixels, and draws 1 or 2 pixels wide
    // depending on where it lands, so a button's top and bottom edges differ. This rounds the outline's sides to the
    // nearest screen pixel and its width to a whole number of pixels (at least one), and draws again when it moves on
    // the screen. Tinted by its color, like an image.
    internal sealed class PixelOutline : MaskableGraphic
    {
        // The edge's width in the rect's own units, before rounding.
        public float Edge;

        private readonly Vector3[] corners = new Vector3[4];

        // Read by the SevenUI.Tests plugin: the outline's outer edges as last drawn, in screen pixels, and its sides'
        // width in pixels.
        public Rect DrawnPixels { get; private set; }
        public float DrawnWidth { get; private set; }
        private Vector2 drawnMin;
        private Vector2 drawnMax;

        // Stretched over the holder, outside its layout, passing clicks through, and hidden until shown.
        public static PixelOutline Add(Transform holder, string name, Color color, float edge)
        {
            RectTransform rect = UiFactory.NewObject(name, holder);
            GameObject created = rect.gameObject;
            UiFactory.Stretch(rect);
            created.AddComponent<LayoutElement>().ignoreLayout = true;
            PixelOutline outline = created.AddComponent<PixelOutline>();
            outline.Edge = edge;
            outline.color = color;
            outline.raycastTarget = false;
            outline.enabled = false;
            return outline;
        }

        // Two projected corners a frame while shown; draws again only when they moved.
        private void LateUpdate()
        {
            if (!ScreenBox(Eye, out Vector2 min, out Vector2 max)) return;
            if (min == drawnMin && max == drawnMax) return;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Camera eye = Eye;
            Rect rect = rectTransform.rect;
            if (!ScreenBox(eye, out Vector2 min, out Vector2 max) || rect.width <= 0f) return;
            drawnMin = min;
            drawnMax = max;
            float width = Mathf.Max(1f, Mathf.Round(Edge * (max.x - min.x) / rect.width));
            float left = Mathf.Round(min.x);
            float right = Mathf.Round(max.x);
            float bottom = Mathf.Round(min.y);
            float top = Mathf.Round(max.y);
            if (right - left <= 2f * width || top - bottom <= 2f * width) return;
            DrawnPixels = Rect.MinMaxRect(left, bottom, right, top);
            DrawnWidth = width;
            Quad(vh, eye, left, top - width, right, top);
            Quad(vh, eye, left, bottom, right, bottom + width);
            Quad(vh, eye, left, bottom + width, left + width, top - width);
            Quad(vh, eye, right - width, bottom + width, right, top - width);
        }

        // The camera the outline's canvas is drawn by; none for an overlay canvas.
        private Camera Eye
        {
            get
            {
                Canvas root = canvas ? canvas.rootCanvas : null;
                return root && root.renderMode != RenderMode.ScreenSpaceOverlay ? root.worldCamera : null;
            }
        }

        private bool ScreenBox(Camera eye, out Vector2 min, out Vector2 max)
        {
            rectTransform.GetWorldCorners(corners);
            Vector2 a = RectTransformUtility.WorldToScreenPoint(eye, corners[0]);
            Vector2 b = RectTransformUtility.WorldToScreenPoint(eye, corners[2]);
            min = Vector2.Min(a, b);
            max = Vector2.Max(a, b);
            return max.x > min.x && max.y > min.y;
        }

        // A quad between screen pixel edges, brought back into the rect's own space: the canvas faces the screen, so the
        // mapping is flat.
        private void Quad(VertexHelper vh, Camera eye, float left, float bottom, float right, float top)
        {
            int start = vh.currentVertCount;
            Color32 tint = color;
            vh.AddVert(Local(eye, left, bottom), tint, Vector2.zero);
            vh.AddVert(Local(eye, left, top), tint, Vector2.zero);
            vh.AddVert(Local(eye, right, top), tint, Vector2.zero);
            vh.AddVert(Local(eye, right, bottom), tint, Vector2.zero);
            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start, start + 2, start + 3);
        }

        private Vector3 Local(Camera eye, float x, float y)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, new Vector2(x, y), eye, out Vector2 local);
            return local;
        }
    }
}
