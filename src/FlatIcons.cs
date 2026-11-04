using UnityEngine;

namespace SevenUI
{
    // The scoreboard and sprint buttons' icons, drawn in code as flat white strokes to tint: a ranked list (three rows,
    // each with a square marker), a double chevron for sprint, and the same chevrons pointing down for descending,
    // which the sprint button shows while the player flies a remote-operated drone. Each is a union of square-capped
    // strokes, so the chevrons' corners come out mitred.
    internal static class FlatIcons
    {
        // Each icon is designed on a 30-unit grid, as on the design canvas, and drawn in the middle of a 36-unit
        // square: the game's button is 36 units, and the canvas leaves the icon a little room inside it. Drawn at 128
        // pixels with mipmaps, so it stays smooth at the button's 30 to 70 screen pixels.
        private const int TextureSize = 128;
        private const float Grid = 36f;
        private const float DesignOffset = 3f;
        private const float PixelsPerUnit = 100f;

        // The list's rows and markers, in the design's units, from the top left: (x, y, width, height).
        private static readonly Rect[] ListParts =
        {
            new Rect(10f, 7f, 16f, 2f),
            new Rect(10f, 14f, 16f, 2f),
            new Rect(10f, 21f, 12f, 2f),
            new Rect(4f, 6f, 4f, 4f),
            new Rect(4f, 13f, 4f, 4f),
            new Rect(4f, 20f, 4f, 4f),
        };

        // The chevrons' strokes, in the design's units, from the top left: from, to.
        private static readonly Vector2[] ChevronStrokes =
        {
            new Vector2(7f, 8f), new Vector2(14f, 15f),
            new Vector2(14f, 15f), new Vector2(7f, 22f),
            new Vector2(16f, 8f), new Vector2(23f, 15f),
            new Vector2(23f, 15f), new Vector2(16f, 22f),
        };
        // The sprint chevrons turned to point down.
        private static readonly Vector2[] DescendStrokes =
        {
            new Vector2(8f, 7f), new Vector2(15f, 14f),
            new Vector2(15f, 14f), new Vector2(22f, 7f),
            new Vector2(8f, 16f), new Vector2(15f, 23f),
            new Vector2(15f, 23f), new Vector2(22f, 16f),
        };
        private const float ChevronHalfWidth = 1.25f;

        private static Sprite scoreboard;
        private static Sprite sprint;
        private static Sprite descend;
        public const string ScoreboardName = "SevenUIScoreboardIcon";
        public const string SprintName = "SevenUISprintIcon";
        public const string DescendName = "SevenUIDescendIcon";

        public static Sprite Scoreboard
        {
            get
            {
                if (!scoreboard)
                {
                    scoreboard = Draw(ScoreboardName, ListDistance);
                }
                return scoreboard;
            }
        }

        public static Sprite Sprint
        {
            get
            {
                if (!sprint)
                {
                    sprint = Draw(SprintName, point => StrokesDistance(point, ChevronStrokes));
                }
                return sprint;
            }
        }

        public static Sprite Descend
        {
            get
            {
                if (!descend)
                {
                    descend = Draw(DescendName, point => StrokesDistance(point, DescendStrokes));
                }
                return descend;
            }
        }

        private delegate float Shape(Vector2 point);

        // Each pixel's coverage from the shape's signed distance (negative inside), in design units from the top left.
        private static Sprite Draw(string name, Shape distance)
        {
            Texture2D texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, true);
            texture.name = name;
            texture.filterMode = FilterMode.Trilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            float pixelsPerGrid = TextureSize / Grid;
            for (int y = 0; y < TextureSize; y++)
            {
                for (int x = 0; x < TextureSize; x++)
                {
                    // Texture rows run bottom to top; the design's run top to bottom.
                    Vector2 point = new Vector2((x + 0.5f) / pixelsPerGrid - DesignOffset,
                        Grid - (y + 0.5f) / pixelsPerGrid - DesignOffset);
                    float alpha = Mathf.Clamp01(0.5f - distance(point) * pixelsPerGrid);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
            texture.Apply(true, true);
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, TextureSize, TextureSize), new Vector2(0.5f, 0.5f),
                PixelsPerUnit);
            sprite.name = name;
            return sprite;
        }

        private static float ListDistance(Vector2 point)
        {
            float nearest = float.MaxValue;
            foreach (Rect part in ListParts)
            {
                nearest = Mathf.Min(nearest, BoxDistance(point - part.center, part.size / 2f));
            }
            return nearest;
        }

        // Strokes given as pairs of points: from, to.
        private static float StrokesDistance(Vector2 point, Vector2[] strokes)
        {
            float nearest = float.MaxValue;
            for (int i = 0; i < strokes.Length; i += 2)
            {
                nearest = Mathf.Min(nearest, StrokeDistance(point, strokes[i], strokes[i + 1]));
            }
            return nearest;
        }

        // A straight stroke with square caps: a box along the segment, reaching half its width past each end.
        private static float StrokeDistance(Vector2 point, Vector2 from, Vector2 to)
        {
            Vector2 along = to - from;
            float length = along.magnitude;
            Vector2 direction = along / length;
            Vector2 local = point - (from + to) / 2f;
            Vector2 rotated = new Vector2(Vector2.Dot(local, direction),
                Vector2.Dot(local, new Vector2(-direction.y, direction.x)));
            return BoxDistance(rotated, new Vector2(length / 2f + ChevronHalfWidth, ChevronHalfWidth));
        }

        // The signed distance from a point to an axis-aligned box around the origin.
        private static float BoxDistance(Vector2 point, Vector2 halfSize)
        {
            Vector2 outside = new Vector2(Mathf.Abs(point.x) - halfSize.x, Mathf.Abs(point.y) - halfSize.y);
            Vector2 positive = Vector2.Max(outside, Vector2.zero);
            return positive.magnitude + Mathf.Min(Mathf.Max(outside.x, outside.y), 0f);
        }
    }
}
