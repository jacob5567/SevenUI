using System.Text;
using UnityEngine;

namespace SevenUI
{
    // Holds the top cluster, which holds the item bar and the boss bar, to the design canvas's width: 900 of its 1920
    // pixels across. The game makes it nearly as wide as its main area, and the run info's column, widened past the
    // game's width, would otherwise meet a full item bar.
    //
    // The HUD is drawn by a perspective camera, and the main area sits behind the canvas, so it is drawn smaller than
    // the screen; the width is measured on screen through the HUD's camera, not in the canvas's units.
    internal sealed class TopBarsWidth
    {
        private const string TopCenterPath = "MainContainer/MainUIArea/SpringCanvas/TopCenterCluster";
        private const float ScreenShare = 900f / 1920f;
        // Below this, in screen pixels, the cluster isn't resized, so it isn't dirtied every frame.
        private const float FitPixels = 0.5f;

        private readonly Transform root;
        private readonly StyleRecorder recorder = new StyleRecorder();
        private readonly Vector3[] corners = new Vector3[4];
        private Camera camera;
        private RectTransform topCenter;

        public TopBarsWidth(Transform root)
        {
            this.root = root;
        }

        public void Apply()
        {
            topCenter = root.Find(TopCenterPath) as RectTransform;
            if (!topCenter) return;
            Canvas canvas = root.GetComponent<Canvas>();
            camera = canvas && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            recorder.RecordRect(topCenter);
        }

        public void Restore()
        {
            recorder.Restore();
        }

        // The HUD camera's part of the screen; a split-screen player's HUD has its own.
        private Rect Screen => camera ? camera.pixelRect : new Rect(0f, 0f, UnityEngine.Screen.width, UnityEngine.Screen.height);

        // Two projected points; refitted when the shown width drifts, as it does when the screen changes.
        public void Update()
        {
            if (!topCenter) return;
            Rect screen = Screen;
            topCenter.GetWorldCorners(corners);
            float shown = (Project(corners[2]) - Project(corners[0])).x;
            float width = topCenter.rect.width;
            if (shown <= 0f || width <= 0f) return;
            float target = screen.width * ScreenShare;
            if (Mathf.Abs(shown - target) < FitPixels) return;
            // The cluster faces the camera, so its width shows on screen scaled evenly.
            topCenter.sizeDelta += new Vector2(target * width / shown - width, 0f);
        }

        private Vector2 Project(Vector3 world) => RectTransformUtility.WorldToScreenPoint(camera, world);

        public void Describe(StringBuilder output)
        {
            output.AppendLine($"  top bars: {(topCenter ? $"{topCenter.rect.width:0} wide" : "none")}, camera={(camera ? camera.name : "none")}");
        }
    }
}
