using System.Collections.Generic;
using RoR2.UI;
using UnityEngine;
using UnityEngine.UI;

namespace SevenUI
{
    // One-shot protection's green stripes on a health bar, under the barrier and the effects drawn over the health. The
    // game draws its bars in the order of its list, protection after the barrier, the flash and the execute threshold,
    // which its translucent pattern could be; the flat style's opaque stripes would hide them. So the game's protection
    // bar is kept but drawn clear (FlatHealthStyles), as the marker of where the protection is, and this draws the
    // stripes over the same span just above the health, shield and curse. Only its own image is ever moved. They are
    // the health's green, or, when the game draws the health in another color (infusion or a boss's red, a void body's
    // purple, low health's red), stripes of that color.
    internal sealed class ProtectionStripes : MonoBehaviour
    {
        public const string StripesName = "SevenUIProtectionStripes";

        private const float ColorTolerance = 0.01f;

        public HealthBar Bar;

        private Image stripes;
        private readonly List<Image> bars = new List<Image>();

        // LateUpdate, after the bar has placed this frame's images. A few reads of the bar's children.
        private void LateUpdate()
        {
            RectTransform container = Bar ? Bar.barContainer : null;
            if (!container) return;
            if (!stripes)
            {
                stripes = NewStripes(container);
            }
            container.GetComponentsInChildren(false, bars);
            Image marker = null;
            int over = -1;
            foreach (Image image in bars)
            {
                if (image == stripes || image.transform.parent != container || !image.enabled) continue;
                if (image.sprite == FlatSprite.ProtectionHatch)
                {
                    marker = image;
                }
                else if (image.sprite == FlatSprite.White || image.sprite == FlatSprite.Hatch)
                {
                    // The health, its trailing parts, the shield and the curse: the layers the stripes go over.
                    over = Mathf.Max(over, image.transform.GetSiblingIndex());
                }
            }
            bars.Clear();
            stripes.enabled = marker;
            if (!marker) return;
            Tint(Bar.barInfoCollection.trailingOverHealthbarInfo.color);

            RectTransform from = marker.rectTransform;
            RectTransform to = stripes.rectTransform;
            if (to.anchorMin != from.anchorMin || to.anchorMax != from.anchorMax || to.sizeDelta != from.sizeDelta ||
                to.anchoredPosition != from.anchoredPosition)
            {
                to.anchorMin = from.anchorMin;
                to.anchorMax = from.anchorMax;
                to.pivot = from.pivot;
                to.sizeDelta = from.sizeDelta;
                to.anchoredPosition = from.anchoredPosition;
            }
            // Just above the last layer it goes over; moving it from below that layer shifts the layer down one.
            int at = stripes.transform.GetSiblingIndex();
            int wanted = at < over ? over : over + 1;
            if (at != wanted)
            {
                stripes.transform.SetSiblingIndex(wanted);
            }
        }

        // The green pair as chosen for the canvas's health; any other health color tints the neutral pair.
        private void Tint(Color health)
        {
            bool green = SameColor(health, Palette.Health);
            Sprite sprite = green ? FlatSprite.ProtectionHatch : FlatSprite.TintedProtectionHatch;
            Color color = green ? Color.white : new Color(health.r, health.g, health.b, 1f);
            if (stripes.sprite != sprite)
            {
                stripes.sprite = sprite;
            }
            if (stripes.color != color)
            {
                stripes.color = color;
            }
        }

        private static bool SameColor(Color a, Color b) =>
            Mathf.Abs(a.r - b.r) < ColorTolerance && Mathf.Abs(a.g - b.g) < ColorTolerance && Mathf.Abs(a.b - b.b) < ColorTolerance;

        private static Image NewStripes(RectTransform container)
        {
            GameObject created = new GameObject(StripesName, typeof(RectTransform), typeof(CanvasRenderer));
            created.layer = container.gameObject.layer;
            created.transform.SetParent(container, false);
            Image image = created.AddComponent<Image>();
            image.sprite = FlatSprite.ProtectionHatch;
            image.type = Image.Type.Tiled;
            image.raycastTarget = false;
            image.enabled = false;
            return image;
        }

        private void OnDestroy()
        {
            if (stripes)
            {
                Destroy(stripes.gameObject);
            }
        }
    }
}
