using System.Text;
using RoR2;
using RoR2.UI;
using UnityEngine;

namespace SevenUI
{
    // With TightMargins on, draws the HUD's corners as near the screen's edges as on the design canvas: 28 canvas
    // pixels in at the sides and bottom, 24 at the top. The game keeps the corner clusters inside its main area, 5% of
    // the screen in from each edge, and each cluster's panels sit differently in its own box (the run info pokes above
    // it, the skills' key labels hang below it). So the spring canvas that holds the clusters is stretched over the whole screen, and each
    // corner cluster is moved until its outermost panel sits at the canvas's margin. The main area itself is left
    // alone, since the enemies' health bars are placed on it.
    //
    // The HUD is drawn by a perspective camera, and the main area sits behind the canvas, so it is drawn smaller than
    // the screen; everything here is measured on screen through the HUD's camera, not in the canvas's units.
    internal sealed class HudMargins
    {
        private const string MainAreaPath = "MainContainer/MainUIArea";
        private const string SpringCanvasPath = MainAreaPath + "/SpringCanvas";
        private const string UpperLeftName = "UpperLeftCluster";
        private const string UpperRightName = "UpperRightCluster";
        private const string BottomLeftName = "BottomLeftCluster";
        private const string BottomRightName = "BottomRightCluster";
        private const string HealthBarPath = "BarRoots/HealthbarRoot";

        // The design canvas's margins, in its pixels on a 1080-pixel-high screen.
        private const float CanvasHeight = 1080f;
        private const float SideMargin = 28f;
        private const float TopMargin = 24f;
        // Below this, in screen pixels, a cluster isn't moved, so it isn't dirtied every frame.
        private const float MovePixels = 0.05f;

        private readonly HUD hud;
        private readonly Transform root;
        private readonly StyleRecorder recorder = new StyleRecorder();
        private readonly Vector3[] corners = new Vector3[4];
        private Camera camera;
        private RectTransform area;
        private RectTransform spring;
        private RectTransform upperLeft;
        private RectTransform upperRight;
        private RectTransform bottomLeft;
        private RectTransform bottomRight;
        // The outermost panel of each cluster that isn't the cluster's own box.
        private RectTransform runInfo;
        private RectTransform healthBar;
        private RectTransform skillKey;
        private Rect filledScreen;

        public HudMargins(HUD hud, Transform root)
        {
            this.hud = hud;
            this.root = root;
        }

        public void Apply()
        {
            spring = root.Find(SpringCanvasPath) as RectTransform;
            area = root.Find(MainAreaPath) as RectTransform;
            if (!spring || !area) return;
            Canvas canvas = root.GetComponent<Canvas>();
            camera = canvas && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            recorder.RecordRect(spring);
            filledScreen = Rect.zero;

            upperLeft = spring.Find(UpperLeftName) as RectTransform;
            upperRight = spring.Find(UpperRightName) as RectTransform;
            bottomLeft = spring.Find(BottomLeftName) as RectTransform;
            bottomRight = spring.Find(BottomRightName) as RectTransform;
            Transform background = upperRight ? FindDeep(upperRight, RunInfoStyler.BackgroundName) : null;
            runInfo = background as RectTransform;
            healthBar = bottomLeft ? bottomLeft.Find(HealthBarPath) as RectTransform : null;
            InputBindingDisplayController key = hud.skillIcons != null && hud.skillIcons.Length > 0 && hud.skillIcons[0]
                ? hud.skillIcons[0].GetComponentInChildren<InputBindingDisplayController>(true)
                : null;
            // The key's plate, which fits the label.
            skillKey = key ? key.transform.parent as RectTransform : null;
        }

        public void Restore()
        {
            recorder.Restore();
        }

        // The HUD camera's part of the screen; a split-screen player's HUD has its own.
        private Rect Screen => camera ? camera.pixelRect : new Rect(0f, 0f, UnityEngine.Screen.width, UnityEngine.Screen.height);

        // A few projected points; moves a cluster only when its panels have moved from the margin. The equipment slot
        // is the skill row's right end once the mod has fitted it.
        public void Update(RectTransform equipmentSlot)
        {
            if (!spring || !area) return;
            Rect screen = Screen;
            if (screen != filledScreen)
            {
                Fill(screen);
            }
            Fit(screen, upperLeft, upperLeft, upperLeft, false, true);
            Fit(screen, upperRight, runInfo, runInfo, true, true);
            Fit(screen, bottomLeft, healthBar, healthBar, false, false);
            RectTransform rowEnd = equipmentSlot ? equipmentSlot : LastSkill();
            Fit(screen, bottomRight, rowEnd, skillKey, true, false);
        }

        // Stretches the spring canvas over what the screen shows of the main area's plane. The top cluster, which
        // spans it, is held to its width by TopBarsWidth.
        private void Fill(Rect screen)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(area, screen.min, camera, out Vector2 min) ||
                !RectTransformUtility.ScreenPointToLocalPointInRectangle(area, screen.max, camera, out Vector2 max)) return;
            Rect rect = area.rect;
            spring.anchorMin = new Vector2((min.x - rect.xMin) / rect.width, (min.y - rect.yMin) / rect.height);
            spring.anchorMax = new Vector2((max.x - rect.xMin) / rect.width, (max.y - rect.yMin) / rect.height);
            spring.anchoredPosition = Vector2.zero;
            spring.sizeDelta = Vector2.zero;
            filledScreen = screen;
        }

        private RectTransform LastSkill()
        {
            if (hud.skillIcons == null) return null;
            for (int i = hud.skillIcons.Length - 1; i >= 0; i--)
            {
                if (hud.skillIcons[i] && hud.skillIcons[i].gameObject.activeInHierarchy)
                {
                    return (RectTransform)hud.skillIcons[i].transform;
                }
            }
            return null;
        }

        // Moves the cluster so the horizontal panel's outer side and the vertical panel's outer edge sit at the margins.
        private void Fit(Rect screen, RectTransform cluster, RectTransform horizontal, RectTransform vertical, bool right,
            bool top)
        {
            if (!cluster || !horizontal || !vertical || cluster.parent != spring) return;
            float pixel = screen.height / CanvasHeight;
            Rect across = OnScreen(horizontal);
            Rect upDown = OnScreen(vertical);
            float gapX = right ? screen.xMax - across.xMax : across.xMin - screen.xMin;
            float gapY = top ? screen.yMax - upDown.yMax : upDown.yMin - screen.yMin;
            float marginY = (top ? TopMargin : SideMargin) * pixel;
            Vector2 shift = new Vector2(right ? gapX - SideMargin * pixel : SideMargin * pixel - gapX,
                top ? gapY - marginY : marginY - gapY);
            if (Mathf.Abs(shift.x) < MovePixels && Mathf.Abs(shift.y) < MovePixels) return;
            // The spring canvas faces the camera, so a move in its plane shows on screen scaled evenly.
            float perUnit = (Project(spring.TransformPoint(Vector3.right)) - Project(spring.TransformPoint(Vector3.zero))).magnitude;
            if (perUnit <= 0f) return;
            recorder.SetAnchoredPosition(cluster, cluster.anchoredPosition + shift / perUnit);
        }

        private Vector2 Project(Vector3 world) => RectTransformUtility.WorldToScreenPoint(camera, world);

        private Rect OnScreen(RectTransform rect)
        {
            rect.GetWorldCorners(corners);
            Vector2 min = Project(corners[0]);
            Vector2 max = min;
            for (int i = 1; i < corners.Length; i++)
            {
                Vector2 point = Project(corners[i]);
                min = Vector2.Min(min, point);
                max = Vector2.Max(max, point);
            }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private static Transform FindDeep(Transform parent, string name)
        {
            foreach (Transform child in parent.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == name) return child;
            }
            return null;
        }

        public void Describe(StringBuilder output)
        {
            output.AppendLine($"  margins: spring canvas {(spring ? $"{spring.anchorMin}-{spring.anchorMax}" : "none")}, " +
                              $"camera={(camera ? camera.name : "none")}, run info={(bool)runInfo}, health bar={(bool)healthBar}, " +
                              $"skill key={(bool)skillKey}");
        }
    }
}
