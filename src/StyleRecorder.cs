using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SevenUI
{
    // Remembers the game's values for each HUD object the mod restyles, the first time it touches it, and the objects
    // and components it adds, so turning a setting off puts the HUD back as the game drew it.
    internal sealed class StyleRecorder
    {
        public struct ImageLook
        {
            public Sprite Sprite;
            public Image.Type Type;
            public Color Color;
        }

        public struct LabelLook
        {
            public float FontSize;
            public FontStyles FontStyle;
            public float CharacterSpacing;
            public Color Color;
            public TextAlignmentOptions Alignment;
            public Vector4 Margin;
        }

        public struct RectLook
        {
            public Vector2 AnchorMin;
            public Vector2 AnchorMax;
            public Vector2 Pivot;
            public Vector2 Position;
            public Vector2 Size;
        }

        public struct LayoutLook
        {
            public int Left;
            public int Right;
            public int Top;
            public int Bottom;
            public float Spacing;
        }

        private readonly Dictionary<Image, ImageLook> images = new Dictionary<Image, ImageLook>();
        private readonly Dictionary<RawImage, (Texture texture, Color color)> rawImages =
            new Dictionary<RawImage, (Texture, Color)>();
        private readonly Dictionary<TMP_Text, LabelLook> labels = new Dictionary<TMP_Text, LabelLook>();
        private readonly Dictionary<TMP_Text, string> texts = new Dictionary<TMP_Text, string>();
        private readonly Dictionary<Behaviour, bool> enabledStates = new Dictionary<Behaviour, bool>();
        private readonly Dictionary<GameObject, bool> activeStates = new Dictionary<GameObject, bool>();
        private readonly Dictionary<Transform, Quaternion> rotations = new Dictionary<Transform, Quaternion>();
        private readonly Dictionary<Transform, Vector3> scales = new Dictionary<Transform, Vector3>();
        private readonly Dictionary<RectTransform, Vector2> positions = new Dictionary<RectTransform, Vector2>();
        private readonly Dictionary<RectTransform, RectLook> rects = new Dictionary<RectTransform, RectLook>();
        private readonly List<System.Action> undos = new List<System.Action>();
        private readonly Dictionary<HorizontalOrVerticalLayoutGroup, LayoutLook> layouts =
            new Dictionary<HorizontalOrVerticalLayoutGroup, LayoutLook>();
        private readonly Dictionary<LayoutElement, Vector2> minAndPreferredHeights = new Dictionary<LayoutElement, Vector2>();
        private readonly List<Object> added = new List<Object>();

        public int Count => images.Count + rawImages.Count + labels.Count + texts.Count + enabledStates.Count + activeStates.Count +
                            rotations.Count + scales.Count + positions.Count + rects.Count + layouts.Count + minAndPreferredHeights.Count + added.Count + undos.Count;

        public static ImageLook Capture(Image image) => new ImageLook
        {
            Sprite = image.sprite,
            Type = image.type,
            Color = image.color,
        };

        public static LabelLook Capture(TMP_Text label) => new LabelLook
        {
            FontSize = label.fontSize,
            FontStyle = label.fontStyle,
            CharacterSpacing = label.characterSpacing,
            Color = label.color,
            Alignment = label.alignment,
            Margin = label.margin,
        };

        public static LayoutLook Capture(HorizontalOrVerticalLayoutGroup group) => new LayoutLook
        {
            Left = group.padding.left,
            Right = group.padding.right,
            Top = group.padding.top,
            Bottom = group.padding.bottom,
            Spacing = group.spacing,
        };

        public void Record(Image image)
        {
            if (image && !images.ContainsKey(image))
            {
                images[image] = Capture(image);
            }
        }

        // For an object copied from one the mod had already restyled: its own values are the mod's, so it gets the
        // game's values recorded for the original.
        public void RecordAs(Image image, Image original)
        {
            if (image && !images.ContainsKey(image) && images.TryGetValue(original, out ImageLook look))
            {
                images[image] = look;
            }
        }

        public void Record(RawImage image)
        {
            if (image && !rawImages.ContainsKey(image))
            {
                rawImages[image] = (image.texture, image.color);
            }
        }

        public void Record(TMP_Text label)
        {
            if (label && !labels.ContainsKey(label))
            {
                labels[label] = Capture(label);
            }
        }

        public bool TryGet(TMP_Text label, out LabelLook look) => labels.TryGetValue(label, out look);

        // Only for text the game sets once, such as a title. Text the game updates as it changes (the stage count,
        // the timer) would be put back stale.
        public void RecordText(TMP_Text label)
        {
            if (label && !texts.ContainsKey(label))
            {
                texts[label] = label.text;
            }
        }

        // For a graphic or other component the mod turns on or off. Only for ones the game leaves on or off for good,
        // or turning the setting off would undo the game's own toggling.
        public void SetEnabled(Behaviour behaviour, bool enabled)
        {
            if (!behaviour) return;
            if (!enabledStates.ContainsKey(behaviour))
            {
                enabledStates[behaviour] = behaviour.enabled;
            }
            behaviour.enabled = enabled;
        }

        public void Disable(Behaviour behaviour)
        {
            SetEnabled(behaviour, false);
        }

        // Likewise for objects the game leaves active or inactive for good.
        public void SetActive(GameObject gameObject, bool active)
        {
            if (!gameObject) return;
            if (!activeStates.ContainsKey(gameObject))
            {
                activeStates[gameObject] = gameObject.activeSelf;
            }
            gameObject.SetActive(active);
        }

        public void SetLocalRotation(Transform transform, Quaternion rotation)
        {
            if (!transform) return;
            if (!rotations.ContainsKey(transform))
            {
                rotations[transform] = transform.localRotation;
            }
            transform.localRotation = rotation;
        }

        public void SetLocalScale(Transform transform, Vector3 scale)
        {
            if (!transform) return;
            if (!scales.ContainsKey(transform))
            {
                scales[transform] = transform.localScale;
            }
            transform.localScale = scale;
        }

        public void SetAnchoredPosition(RectTransform rect, Vector2 position)
        {
            if (!rect) return;
            if (!positions.ContainsKey(rect))
            {
                positions[rect] = rect.anchoredPosition;
            }
            rect.anchoredPosition = position;
        }

        public bool HasPosition(RectTransform rect) => positions.ContainsKey(rect);

        // For a rect the mod places itself: its anchors, pivot, position and size.
        public void RecordRect(RectTransform rect)
        {
            if (rect && !rects.ContainsKey(rect))
            {
                rects[rect] = new RectLook
                {
                    AnchorMin = rect.anchorMin,
                    AnchorMax = rect.anchorMax,
                    Pivot = rect.pivot,
                    Position = rect.anchoredPosition,
                    Size = rect.sizeDelta,
                };
            }
        }

        public bool HasRect(RectTransform rect) => rects.ContainsKey(rect);

        // For a change that isn't a plain value, put back by the given action, which must check its objects still
        // exist. Run in the order recorded, after the values above are put back.
        public void OnRestore(System.Action undo)
        {
            undos.Add(undo);
        }

        public void Record(HorizontalOrVerticalLayoutGroup group)
        {
            if (group && !layouts.ContainsKey(group))
            {
                layouts[group] = Capture(group);
            }
        }

        public bool TryGet(HorizontalOrVerticalLayoutGroup group, out LayoutLook look) => layouts.TryGetValue(group, out look);

        public void Record(LayoutElement element)
        {
            if (element && !minAndPreferredHeights.ContainsKey(element))
            {
                minAndPreferredHeights[element] = new Vector2(element.minHeight, element.preferredHeight);
            }
        }

        public bool TryGet(LayoutElement element, out Vector2 minAndPreferredHeight) =>
            minAndPreferredHeights.TryGetValue(element, out minAndPreferredHeight);

        // An object or component the mod created, destroyed when the setting is turned off.
        public void Add(Object created)
        {
            added.Add(created);
        }

        public static void Apply(Image image, ImageLook look)
        {
            image.sprite = look.Sprite;
            image.type = look.Type;
            image.color = look.Color;
        }

        public static void ApplyStyle(TMP_Text label, LabelLook look)
        {
            label.fontSize = look.FontSize;
            label.fontStyle = look.FontStyle;
            label.characterSpacing = look.CharacterSpacing;
            label.color = look.Color;
            label.alignment = look.Alignment;
            label.margin = look.Margin;
        }

        public static void Apply(HorizontalOrVerticalLayoutGroup group, LayoutLook look)
        {
            group.padding = new RectOffset(look.Left, look.Right, look.Top, look.Bottom);
            group.spacing = look.Spacing;
        }

        public void Restore()
        {
            foreach (KeyValuePair<Image, ImageLook> entry in images)
            {
                if (entry.Key)
                {
                    Apply(entry.Key, entry.Value);
                }
            }
            foreach (KeyValuePair<RawImage, (Texture texture, Color color)> entry in rawImages)
            {
                if (!entry.Key) continue;
                entry.Key.texture = entry.Value.texture;
                entry.Key.color = entry.Value.color;
            }
            foreach (KeyValuePair<TMP_Text, LabelLook> entry in labels)
            {
                if (entry.Key)
                {
                    ApplyStyle(entry.Key, entry.Value);
                }
            }
            foreach (KeyValuePair<TMP_Text, string> entry in texts)
            {
                if (entry.Key)
                {
                    entry.Key.text = entry.Value;
                }
            }
            foreach (KeyValuePair<Behaviour, bool> entry in enabledStates)
            {
                if (entry.Key)
                {
                    entry.Key.enabled = entry.Value;
                }
            }
            foreach (KeyValuePair<HorizontalOrVerticalLayoutGroup, LayoutLook> entry in layouts)
            {
                if (entry.Key)
                {
                    Apply(entry.Key, entry.Value);
                }
            }
            foreach (KeyValuePair<LayoutElement, Vector2> entry in minAndPreferredHeights)
            {
                if (!entry.Key) continue;
                entry.Key.minHeight = entry.Value.x;
                entry.Key.preferredHeight = entry.Value.y;
            }
            foreach (KeyValuePair<GameObject, bool> entry in activeStates)
            {
                if (entry.Key)
                {
                    entry.Key.SetActive(entry.Value);
                }
            }
            foreach (KeyValuePair<RectTransform, Vector2> entry in positions)
            {
                if (entry.Key)
                {
                    entry.Key.anchoredPosition = entry.Value;
                }
            }
            foreach (KeyValuePair<RectTransform, RectLook> entry in rects)
            {
                if (!entry.Key) continue;
                entry.Key.anchorMin = entry.Value.AnchorMin;
                entry.Key.anchorMax = entry.Value.AnchorMax;
                entry.Key.pivot = entry.Value.Pivot;
                entry.Key.anchoredPosition = entry.Value.Position;
                entry.Key.sizeDelta = entry.Value.Size;
            }
            foreach (KeyValuePair<Transform, Quaternion> entry in rotations)
            {
                if (entry.Key)
                {
                    entry.Key.localRotation = entry.Value;
                }
            }
            foreach (KeyValuePair<Transform, Vector3> entry in scales)
            {
                if (entry.Key)
                {
                    entry.Key.localScale = entry.Value;
                }
            }
            foreach (System.Action undo in undos)
            {
                undo();
            }
            foreach (Object created in added)
            {
                if (created)
                {
                    Object.Destroy(created);
                }
            }
            images.Clear();
            rawImages.Clear();
            labels.Clear();
            texts.Clear();
            enabledStates.Clear();
            activeStates.Clear();
            rotations.Clear();
            scales.Clear();
            positions.Clear();
            rects.Clear();
            undos.Clear();
            layouts.Clear();
            minAndPreferredHeights.Clear();
            added.Clear();
        }
    }
}
