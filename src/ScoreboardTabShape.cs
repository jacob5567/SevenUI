using UnityEngine;
using UnityEngine.UI;

namespace SevenUI
{
    // The collapsed scoreboard's tab: the shown player's row and their items as one shape, filled in the tab's tint
    // and outlined, its right corners rounded, and a thin line down the other rows' right edge where they sit beside
    // the board's empty space. Drawn behind the rows, on the rows' container (ScoreboardItemsBoard places it).
    internal sealed class ScoreboardTabShape : MaskableGraphic
    {
        private const float Canvas = HudUnits.PerCanvasPixel;
        // The canvas's 2px outline, inside the shape's edges, its 8px corners, and the 1px line beside the rows.
        private const float LineWidth = 2f * Canvas;
        private const float CornerRadius = 8f * Canvas;
        private const float EdgeWidth = 1f * Canvas;
        private const int CornerSegments = 8;

        // The canvas's tint over its board, as drawn in the game's light (CanvasBlend).
        public static readonly Color FillColor = CanvasBlend.Over(Palette.ScoreboardTab, ScoreboardStyler.CanvasBoard);

        // Across from the container's left and down from its top, in the HUD's units: the shown row, its items' area
        // (from the row's right edge to the board's, at least as tall as the row) and the rows' top and bottom.
        private Rect row;
        private Rect items;
        private float rowsTop;
        private float rowsBottom;
        private bool placed;

        // Read by the SevenUI.Tests plugin.
        public Rect RowArea => row;
        public Rect ItemsArea => items;

        // Draws again only when the shape moved.
        public void Place(Rect shownRow, Rect itemsArea, float top, float bottom)
        {
            if (placed && shownRow == row && itemsArea == items && top == rowsTop && bottom == rowsBottom) return;
            placed = true;
            row = shownRow;
            items = itemsArea;
            rowsTop = top;
            rowsBottom = bottom;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (!placed) return;
            Rect area = rectTransform.rect;
            Color32 fill = FillColor;
            Color32 line = Palette.ScoreboardTabLine;
            Color32 edge = Palette.RowDivider;
            float radius = Mathf.Min(CornerRadius, items.width / 2f, items.height / 2f);
            Vector2 topCorner = new Vector2(items.xMax - radius, items.yMin + radius);
            Vector2 bottomCorner = new Vector2(items.xMax - radius, items.yMax - radius);

            // The fill, in parts that don't overlap, since it is translucent.
            Quad(vh, area, row.xMin, row.yMin, row.xMax, row.yMax, fill);
            Quad(vh, area, items.xMin, items.yMin, items.xMax - radius, items.yMax, fill);
            Quad(vh, area, items.xMax - radius, items.yMin + radius, items.xMax, items.yMax - radius, fill);
            Arc(vh, area, topCorner, 0f, radius, -90f, fill);
            Arc(vh, area, bottomCorner, 0f, radius, 0f, fill);

            // The outline. Where the items' area reaches above or below the row, it steps out at the row's right.
            Quad(vh, area, row.xMin, row.yMin, row.xMin + LineWidth, row.yMax, line);
            Quad(vh, area, row.xMin, row.yMin, row.xMax + LineWidth, row.yMin + LineWidth, line);
            Quad(vh, area, row.xMin, row.yMax - LineWidth, row.xMax + LineWidth, row.yMax, line);
            if (items.yMin < row.yMin)
            {
                Quad(vh, area, items.xMin, items.yMin, items.xMin + LineWidth, row.yMin + LineWidth, line);
            }
            if (items.yMax > row.yMax)
            {
                Quad(vh, area, items.xMin, row.yMax - LineWidth, items.xMin + LineWidth, items.yMax, line);
            }
            Quad(vh, area, items.xMin, items.yMin, items.xMax - radius, items.yMin + LineWidth, line);
            Quad(vh, area, items.xMin, items.yMax - LineWidth, items.xMax - radius, items.yMax, line);
            Quad(vh, area, items.xMax - LineWidth, items.yMin + radius, items.xMax, items.yMax - radius, line);
            Arc(vh, area, topCorner, radius - LineWidth, radius, -90f, line);
            Arc(vh, area, bottomCorner, radius - LineWidth, radius, 0f, line);

            if (items.yMin > rowsTop)
            {
                Quad(vh, area, items.xMin, rowsTop, items.xMin + EdgeWidth, items.yMin, edge);
            }
            if (items.yMax < rowsBottom)
            {
                Quad(vh, area, items.xMin, items.yMax, items.xMin + EdgeWidth, rowsBottom, edge);
            }
        }

        // From across and down to the rect's own space, which runs up.
        private static Vector2 Local(Rect area, float x, float y) => new Vector2(area.xMin + x, area.yMax - y);

        private static void Quad(VertexHelper vh, Rect area, float left, float top, float right, float bottom, Color32 color)
        {
            if (right <= left || bottom <= top) return;
            int start = vh.currentVertCount;
            vh.AddVert(Local(area, left, top), color, Vector2.zero);
            vh.AddVert(Local(area, right, top), color, Vector2.zero);
            vh.AddVert(Local(area, right, bottom), color, Vector2.zero);
            vh.AddVert(Local(area, left, bottom), color, Vector2.zero);
            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start, start + 2, start + 3);
        }

        // A quarter ring from inner to outer around the center, turning a quarter from the angle (degrees, clockwise
        // on screen from the right); an inner of 0 fills the quarter.
        private static void Arc(VertexHelper vh, Rect area, Vector2 center, float inner, float outer, float from, Color32 color)
        {
            if (outer <= 0f) return;
            int start = vh.currentVertCount;
            for (int i = 0; i <= CornerSegments; i++)
            {
                float angle = (from + 90f * i / CornerSegments) * Mathf.Deg2Rad;
                Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                vh.AddVert(Local(area, center.x + direction.x * inner, center.y + direction.y * inner), color, Vector2.zero);
                vh.AddVert(Local(area, center.x + direction.x * outer, center.y + direction.y * outer), color, Vector2.zero);
            }
            for (int i = 0; i < CornerSegments; i++)
            {
                int a = start + 2 * i;
                vh.AddTriangle(a, a + 1, a + 3);
                vh.AddTriangle(a, a + 3, a + 2);
            }
        }
    }
}
