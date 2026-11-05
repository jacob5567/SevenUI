using UnityEngine;
using UnityEngine.UI;

namespace SevenUI
{
    // Shades a graphic from one color at its left edge to another at its right, through its vertices' colors, which the
    // GPU blends across the quad: the transformation notification's strip and arrow, from the old item's tier color to
    // the new one's. The graphic's own color multiplies in, so it is left white.
    internal sealed class TierBlend : BaseMeshEffect
    {
        private Color from = Color.white;
        private Color to = Color.white;

        public Color From => from;
        public Color To => to;

        public void SetColors(Color left, Color right)
        {
            if (left == from && right == to) return;
            from = left;
            to = right;
            if (graphic)
            {
                graphic.SetVerticesDirty();
            }
        }

        public override void ModifyMesh(VertexHelper vertices)
        {
            if (!IsActive() || vertices.currentVertCount == 0) return;
            Rect rect = graphic.rectTransform.rect;
            UIVertex vertex = default;
            for (int i = 0; i < vertices.currentVertCount; i++)
            {
                vertices.PopulateUIVertex(ref vertex, i);
                float across = Mathf.InverseLerp(rect.xMin, rect.xMax, vertex.position.x);
                vertex.color = (Color)vertex.color * Color.Lerp(from, to, across);
                vertices.SetUIVertex(vertex, i);
            }
        }
    }
}
