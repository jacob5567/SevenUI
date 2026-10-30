using TMPro;
using UnityEngine;

namespace SevenUI
{
    // Lines the scoreboard's column headers up with the first shown row's columns, as on the design canvas, where each
    // header starts at its column and the money's ends at its column's right. The game places its headers at fixed
    // spots for its own columns, which the restyle resizes and reorders. Moved again whenever the row's columns move.
    internal sealed class ScoreboardHeaders : MonoBehaviour
    {
        public RectTransform Rows;
        // Each header with the column it heads, and whether it is right-aligned to it.
        public TMP_Text[] Headers;
        public string[] Columns;
        public bool[] RightAligned;

        private readonly Vector3[] corners = new Vector3[4];
        private float[] shownEdges;

        // A few transformed points when nothing moved.
        private void LateUpdate()
        {
            RectTransform row = FirstRow();
            if (!row) return;
            if (shownEdges == null)
            {
                shownEdges = new float[Headers.Length];
            }
            RectTransform holder = (RectTransform)transform;
            for (int i = 0; i < Headers.Length; i++)
            {
                if (!Headers[i] || !(row.Find(Columns[i]) is RectTransform column) || !column.gameObject.activeSelf) continue;
                column.GetWorldCorners(corners);
                float edge = holder.InverseTransformPoint(corners[RightAligned[i] ? 2 : 0]).x;
                if (Mathf.Approximately(edge, shownEdges[i])) continue;
                shownEdges[i] = edge;
                RectTransform header = Headers[i].rectTransform;
                header.anchorMin = header.anchorMax = new Vector2(0f, 0.5f);
                header.pivot = new Vector2(RightAligned[i] ? 1f : 0f, 0.5f);
                header.anchoredPosition = new Vector2(edge - holder.rect.xMin, 0f);
            }
        }

        // The rows' backgrounds, which hold the columns.
        private RectTransform FirstRow()
        {
            if (!Rows) return null;
            foreach (Transform strip in Rows)
            {
                if (!strip.gameObject.activeSelf) continue;
                if (strip.Find(ScoreboardStyler.RowBackgroundName) is RectTransform background) return background;
            }
            return null;
        }
    }
}
