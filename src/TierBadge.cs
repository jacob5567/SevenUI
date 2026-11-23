using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SevenUI
{
    // Draws a drone's tier badge (DroneTier.Badge) behind its tag: a rounded square, filled with a dark shade of the
    // tier's color and outlined in a lighter one, centered on the tag's letters both ways (I1 badge design B, Jacob's
    // pick, 2026-11-22). The game's rich text can't round or outline a highlight, so the badge is two images, the
    // outline and the fill inside it, just before the label among its siblings so they draw under its text. They're
    // placed from the label's layout of the tag, after each change to its text, and follow the label as it moves.
    internal sealed class TierBadge : MonoBehaviour
    {
        // How far the badge reaches past the tag's letters, and its corners: in the tag's em. The height is the tag's
        // cap height plus twice the vertical reach, roomy above and below the number (Jacob, 2026-11-22).
        public const float PadXEm = 0.3f;
        private const float PadYEm = 0.34f;
        private const float RadiusEm = 0.24f;
        // The outline's width in the tag's em, never under one screen pixel.
        public const float BorderEm = 0.08f;
        // The fill and outline: the tier's color darkened to these shares of itself; the fill is slightly see-through.
        private const float FillShade = 0.32f;
        private const float FillAlpha = 0.9f;
        private const float BorderShade = 0.55f;

        public const string OutlineName = "SevenUITierBadgeOutline";
        public const string FillName = "SevenUITierBadgeFill";

        private TMP_Text label;
        private Image outline;
        private Image fill;
        private bool shown;
        // The tag's letters in the label's own space: their middle, their advance width, and the tag's size.
        private Vector2 middle;
        private float width;
        private float em;
        private Color color;

        // Draws the badge behind every tier tag in 'label'; does nothing more if it already does.
        public static void Attach(TMP_Text label)
        {
            if (label && !label.GetComponent<TierBadge>())
            {
                label.gameObject.AddComponent<TierBadge>();
            }
        }

        private void OnEnable()
        {
            label = GetComponent<TMP_Text>();
            if (!label) return;
            label.OnPreRenderText += Measure;
            if (label.textInfo != null)
            {
                Measure(label.textInfo);
            }
        }

        private void OnDisable()
        {
            if (label)
            {
                label.OnPreRenderText -= Measure;
            }
            Show(false);
        }

        private void OnDestroy()
        {
            if (outline)
            {
                Destroy(outline.gameObject);
            }
            if (fill)
            {
                Destroy(fill.gameObject);
            }
        }

        // Finds the tag (the link DroneTier.Badge wraps it in) in the label's new layout.
        private void Measure(TMP_TextInfo info)
        {
            shown = false;
            for (int i = 0; info != null && i < info.linkCount; i++)
            {
                TMP_LinkInfo link = info.linkInfo[i];
                if (!DroneTier.IsBadgeLink(link.GetLinkID(), out color)) continue;
                int first = link.linkTextfirstCharacterIndex;
                int last = first + link.linkTextLength - 1;
                if (first < 0 || last >= info.characterCount || !info.characterInfo[first].isVisible ||
                    !info.characterInfo[last].isVisible) return;
                TMP_CharacterInfo start = info.characterInfo[first];
                TMP_CharacterInfo end = info.characterInfo[last];
                // The glyph quads carry the font's padding evenly on every side, so their middle is the letters'.
                float left = Mathf.Min(start.bottomLeft.x, start.topLeft.x);
                float right = Mathf.Max(end.bottomRight.x, end.topRight.x);
                float bottom = Mathf.Min(start.bottomLeft.y, end.bottomRight.y);
                float top = Mathf.Max(start.topLeft.y, end.topRight.y);
                middle = new Vector2((left + right) / 2f, (bottom + top) / 2f);
                width = end.xAdvance - start.origin;
                em = start.pointSize;
                shown = em > 0f;
                return;
            }
        }

        private void LateUpdate()
        {
            Show(shown && label && label.isActiveAndEnabled);
            if (!shown || !outline) return;
            Transform parent = label.transform.parent;
            // Sizes in the label's space, rounded to whole screen pixels so the outline is even all round: a screen pixel
            // is a canvas unit over the canvas's scale, and the label may be scaled within the canvas.
            Canvas canvas = label.canvas ? label.canvas.rootCanvas : null;
            float inCanvas = canvas && canvas.transform.lossyScale.x > 0f ? label.transform.lossyScale.x / canvas.transform.lossyScale.x : 1f;
            float pixel = 1f / Mathf.Max((canvas ? canvas.scaleFactor : 1f) * inCanvas, 1e-6f);
            float border = Mathf.Max(1f, Mathf.Round(BorderEm * em / pixel)) * pixel;
            float capHeight = CapHeight();
            Vector2 size = new Vector2(Mathf.Round((width + 2f * PadXEm * em) / pixel) * pixel,
                Mathf.Round((capHeight + 2f * PadYEm * em) / pixel) * pixel);
            Vector3 position = parent.InverseTransformPoint(label.transform.TransformPoint(middle));
            float ratio = parent.lossyScale.x > 0f ? label.transform.lossyScale.x / parent.lossyScale.x : 1f;
            Place(outline, position, size * ratio, RadiusEm * em * ratio);
            Place(fill, position, (size - 2f * border * Vector2.one) * ratio, Mathf.Max(RadiusEm * em - border, 0f) * ratio);
            float alpha = label.color.a;
            outline.color = Shade(color, BorderShade, alpha);
            fill.color = Shade(color, FillShade, FillAlpha * alpha);
            int index = label.transform.GetSiblingIndex();
            if (fill.transform.GetSiblingIndex() != index - 1 || outline.transform.GetSiblingIndex() != index - 2)
            {
                outline.transform.SetSiblingIndex(index);
                fill.transform.SetSiblingIndex(label.transform.GetSiblingIndex());
            }
        }

        // The tag's cap height: the font's, at the tag's size.
        private float CapHeight()
        {
            TMP_FontAsset font = label.font;
            if (font && font.faceInfo.pointSize > 0f && font.faceInfo.capLine > 0f)
            {
                return font.faceInfo.capLine / font.faceInfo.pointSize * em;
            }
            return 0.7f * em;
        }

        private static void Place(Image image, Vector3 position, Vector2 size, float radius)
        {
            RectTransform rect = image.rectTransform;
            rect.localPosition = position;
            rect.sizeDelta = size;
            // FlatSprite.Rounded's corners are RoundedRadius canvas units across at a multiplier of 1.
            image.pixelsPerUnitMultiplier = radius > 0f ? FlatSprite.RoundedRadius / radius : 100f;
        }

        private static Color Shade(Color color, float shade, float alpha) =>
            new Color(color.r * shade, color.g * shade, color.b * shade, alpha);

        private void Show(bool show)
        {
            if (show && !outline)
            {
                outline = NewImage(OutlineName);
                fill = NewImage(FillName);
            }
            if (outline && outline.enabled != show)
            {
                outline.enabled = show;
                fill.enabled = show;
            }
        }

        private Image NewImage(string name)
        {
            RectTransform rect = UiFactory.NewObject(name, label.transform.parent);
            rect.gameObject.AddComponent<LayoutIgnorer>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = FlatSprite.Rounded;
            image.type = Image.Type.Sliced;
            image.raycastTarget = false;
            image.enabled = false;
            return image;
        }
    }
}
