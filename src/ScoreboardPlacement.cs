using UnityEngine;
using UnityEngine.UI;

namespace SevenUI
{
    // The scoreboard as wide as on the design canvas, 1220 of its pixels (the scoreboard's units), and, with
    // ScoreboardBesideRunInfo, where the canvas puts it: 110 down from the top of the screen, centered between the
    // screen's left edge and the run info's column. The game's panel spans the screen less 616 units and lays the board
    // out centered in it, between 140 units of padding on each side, 1024 wide. Without the setting the panel stays the
    // game's and the padding is what leaves the canvas's width in the middle. Narrower screens get what room there is.
    internal sealed class ScoreboardPlacement
    {
        private const float BoardWidth = 1220f;
        private const float BoardTop = 110f;
        // Kept between the board and the screen's edge or the column when there isn't room for the canvas's width.
        private const float MinMargin = 16f;
        // In the scoreboard's units.
        private const float Tolerance = 0.5f;

        private readonly RectTransform panel;
        private readonly VerticalLayoutGroup container;
        private readonly Transform hud;
        private readonly Vector2 gameAnchorMin;
        private readonly Vector2 gameAnchorMax;
        private readonly Vector2 gameOffsetMin;
        private readonly Vector2 gameOffsetMax;
        private RectTransform column;

        public ScoreboardPlacement(RectTransform panel, VerticalLayoutGroup container, Transform hud, StyleRecorder recorder)
        {
            this.panel = panel;
            this.container = container;
            this.hud = hud;
            recorder.RecordRect(panel);
            recorder.Record(container);
            gameAnchorMin = panel.anchorMin;
            gameAnchorMax = panel.anchorMax;
            gameOffsetMin = panel.offsetMin;
            gameOffsetMax = panel.offsetMax;
        }

        public bool Alive => panel && container;

        // While the scoreboard is open: a few reads, and a change when the screen, the column or the setting has.
        public void Update(bool besideRunInfo)
        {
            if (!panel.gameObject.activeInHierarchy) return;
            RectTransform space = panel.parent as RectTransform;
            float columnLeft = besideRunInfo && space ? ColumnLeft(space) : float.NaN;
            if (float.IsNaN(columnLeft))
            {
                Place(gameAnchorMin, gameAnchorMax, gameOffsetMin, gameOffsetMax);
                // The panel's width, less the canvas's board, split either side.
                float width = space ? space.rect.width * (gameAnchorMax.x - gameAnchorMin.x) + gameOffsetMax.x - gameOffsetMin.x : 0f;
                Pad(Mathf.Max(0, Mathf.FloorToInt((width - BoardWidth) / 2f)));
                return;
            }
            Rect area = space.rect;
            float room = columnLeft - area.xMin;
            float boardWidth = Mathf.Min(BoardWidth, room - 2f * MinMargin);
            float left = (room - boardWidth) / 2f;
            // Anchored to the screen's left edge and top, keeping the game's bottom.
            Place(new Vector2(0f, gameAnchorMin.y), new Vector2(0f, 1f), new Vector2(left, gameOffsetMin.y),
                new Vector2(left + boardWidth, -BoardTop));
            Pad(0);
        }

        private void Place(Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            if (panel.anchorMin == anchorMin && panel.anchorMax == anchorMax &&
                (panel.offsetMin - offsetMin).sqrMagnitude < Tolerance * Tolerance &&
                (panel.offsetMax - offsetMax).sqrMagnitude < Tolerance * Tolerance) return;
            panel.anchorMin = anchorMin;
            panel.anchorMax = anchorMax;
            panel.offsetMin = offsetMin;
            panel.offsetMax = offsetMax;
        }

        private void Pad(int side)
        {
            RectOffset padding = container.padding;
            if (padding.left == side && padding.right == side) return;
            container.padding = new RectOffset(side, side, padding.top, padding.bottom);
        }

        // The left edge of the run info's panel, in the space the scoreboard is placed in; NaN without one.
        private float ColumnLeft(RectTransform space)
        {
            if (!column)
            {
                Transform found = FindDeep(hud, RunInfoStyler.BackgroundName);
                column = found as RectTransform;
            }
            if (!column || !column.gameObject.activeInHierarchy) return float.NaN;
            Rect box = column.rect;
            Vector3 bottomLeft = space.InverseTransformPoint(column.TransformPoint(new Vector3(box.xMin, box.yMin, 0f)));
            Vector3 topLeft = space.InverseTransformPoint(column.TransformPoint(new Vector3(box.xMin, box.yMax, 0f)));
            return Mathf.Min(bottomLeft.x, topLeft.x);
        }

        private static Transform FindDeep(Transform parent, string name)
        {
            foreach (Transform child in parent)
            {
                if (child.name == name) return child;
                Transform found = FindDeep(child, name);
                if (found) return found;
            }
            return null;
        }
    }
}
