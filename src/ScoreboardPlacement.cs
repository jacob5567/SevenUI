using UnityEngine;
using UnityEngine.UI;

namespace SevenUI
{
    // The scoreboard as wide as on the design canvas, 1220 of its pixels, and, with ScoreboardBesideRunInfo, where the
    // canvas puts it: 110 pixels down from the top of the screen, centered between the screen's left edge and the run
    // info's column. The game's panel spans the screen less 616 units and lays the board out centered in it, between
    // 140 units of padding on each side. Without the setting the panel stays the game's and the padding is what leaves
    // the canvas's width in the middle. Narrower screens get what room there is.
    //
    // The HUD is drawn by a perspective camera, a little smaller than its units (HudUnits), so the board is measured and
    // placed on screen through the camera, as HudMargins places the corners.
    internal sealed class ScoreboardPlacement
    {
        // In the design canvas's pixels, on a 1080-pixel-high screen.
        private const float CanvasHeight = 1080f;
        private const float BoardWidth = 1220f;
        private const float BoardTop = 110f;
        // Kept between the board and the screen's edge or the column when there isn't room for the canvas's width.
        private const float MinMargin = 16f;
        // In the scoreboard's units.
        private const float Tolerance = 0.5f;

        private readonly RectTransform panel;
        private readonly VerticalLayoutGroup container;
        private readonly Transform hud;
        private readonly Camera camera;
        private readonly Vector2 gameAnchorMin;
        private readonly Vector2 gameAnchorMax;
        private readonly Vector2 gameOffsetMin;
        private readonly Vector2 gameOffsetMax;
        private readonly Vector3[] corners = new Vector3[4];
        private RectTransform column;

        public ScoreboardPlacement(RectTransform panel, VerticalLayoutGroup container, Transform hud, StyleRecorder recorder)
        {
            this.panel = panel;
            this.container = container;
            this.hud = hud;
            Canvas canvas = hud.GetComponent<Canvas>();
            camera = canvas && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            recorder.RecordRect(panel);
            recorder.Record(container);
            gameAnchorMin = panel.anchorMin;
            gameAnchorMax = panel.anchorMax;
            gameOffsetMin = panel.offsetMin;
            gameOffsetMax = panel.offsetMax;
        }

        public bool Alive => panel && container;

        // The HUD camera's part of the screen; a split-screen player's HUD has its own.
        private Rect Screen => camera ? camera.pixelRect : new Rect(0f, 0f, UnityEngine.Screen.width, UnityEngine.Screen.height);

        // While the scoreboard is open: a few projected points, and a change when the screen, the column or the setting
        // has.
        public void Update(bool besideRunInfo)
        {
            if (!panel.gameObject.activeInHierarchy) return;
            RectTransform space = panel.parent as RectTransform;
            if (!space) return;
            Rect screen = Screen;
            float pixel = screen.height / CanvasHeight;
            float columnLeft = besideRunInfo ? ColumnLeft() : float.NaN;
            if (float.IsNaN(columnLeft))
            {
                Place(gameAnchorMin, gameAnchorMax, gameOffsetMin, gameOffsetMax);
                // The canvas's width in the panel's units, measured across the screen's middle; the rest split either
                // side, in whole units, rounded so the board is at most a unit off.
                if (!ToLocal(space, new Vector2(screen.center.x - BoardWidth * pixel / 2f, screen.center.y), out Vector2 from) ||
                    !ToLocal(space, new Vector2(screen.center.x + BoardWidth * pixel / 2f, screen.center.y), out Vector2 to)) return;
                float width = space.rect.width * (gameAnchorMax.x - gameAnchorMin.x) + gameOffsetMax.x - gameOffsetMin.x;
                Pad(Mathf.Max(0, Mathf.RoundToInt((width - (to.x - from.x)) / 2f)));
                return;
            }
            float room = columnLeft - screen.xMin;
            float boardWidth = Mathf.Min(BoardWidth * pixel, room - 2f * MinMargin * pixel);
            float left = screen.xMin + (room - boardWidth) / 2f;
            float top = screen.yMax - BoardTop * pixel;
            if (!ToLocal(space, new Vector2(left, top), out Vector2 topLeft) ||
                !ToLocal(space, new Vector2(left + boardWidth, top), out Vector2 topRight)) return;
            // Anchored to the left edge and top of the panel's space, keeping the game's bottom.
            Rect area = space.rect;
            Place(new Vector2(0f, gameAnchorMin.y), new Vector2(0f, 1f),
                new Vector2(topLeft.x - area.xMin, gameOffsetMin.y), new Vector2(topRight.x - area.xMin, topLeft.y - area.yMax));
            Pad(0);
        }

        // A screen point in the space's units, found on the board's own plane, which sits behind the space's: a point
        // on the space's plane would show further in. Moving between parallel planes keeps the units.
        private bool ToLocal(RectTransform space, Vector2 screenPoint, out Vector2 local)
        {
            RectTransform board = (RectTransform)container.transform;
            local = Vector2.zero;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(board, screenPoint, camera, out Vector2 onBoard)) return false;
            local = space.InverseTransformPoint(board.TransformPoint(onBoard));
            return true;
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

        // The left edge of the run info's panel on screen; NaN without one.
        private float ColumnLeft()
        {
            if (!column)
            {
                column = FindDeep(hud, RunInfoStyler.BackgroundName) as RectTransform;
            }
            if (!column || !column.gameObject.activeInHierarchy) return float.NaN;
            column.GetWorldCorners(corners);
            float left = float.MaxValue;
            foreach (Vector3 corner in corners)
            {
                left = Mathf.Min(left, RectTransformUtility.WorldToScreenPoint(camera, corner).x);
            }
            return left;
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
