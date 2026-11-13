using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SevenUI
{
    // The small builders the stylers share: UI objects, stretched rects, flat images, texts, embedded resources and
    // the HUD camera's screen rectangle.
    internal static class UiFactory
    {
        // On the parent's layer, which the HUD camera needs to draw it.
        public static RectTransform NewObject(string name, Transform parent)
        {
            GameObject created = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
            created.layer = parent.gameObject.layer;
            created.transform.SetParent(parent, false);
            return (RectTransform)created.transform;
        }

        public static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        public static void Stretch(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin,
            Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        public static Image NewImage(string name, Transform parent, Color color)
        {
            Image image = NewObject(name, parent).gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        public static TMP_Text NewText(string name, Transform parent, TMP_Text like, float size, Color color)
        {
            TextMeshProUGUI text = NewObject(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
            text.font = like.font;
            text.fontSharedMaterial = like.fontSharedMaterial;
            text.fontSize = size;
            text.color = color;
            text.raycastTarget = false;
            return text;
        }

        // A text exactly as wide as its preferred width could otherwise wrap.
        public static void KeepOnOneLine(TMP_Text text, StyleRecorder recorder)
        {
            bool wrapping = text.enableWordWrapping;
            recorder.OnRestore(() =>
            {
                if (!text) return;
                text.enableWordWrapping = wrapping;
            });
            text.enableWordWrapping = false;
        }

        public static void Flatten(Image image, Color color)
        {
            image.sprite = null;
            image.type = Image.Type.Simple;
            image.color = color;
        }

        // The bytes of a resource embedded in the DLL, or null when it isn't there.
        public static byte[] ReadResource(string name)
        {
            using (Stream stream = typeof(UiFactory).Assembly.GetManifestResourceStream(name))
            {
                if (stream == null) return null;
                using (MemoryStream bytes = new MemoryStream())
                {
                    stream.CopyTo(bytes);
                    return bytes.ToArray();
                }
            }
        }

        // The camera's pixels, or the screen's when the canvas has no camera (overlay).
        public static Rect ScreenRect(Camera camera) =>
            camera ? camera.pixelRect : new Rect(0f, 0f, UnityEngine.Screen.width, UnityEngine.Screen.height);
    }
}
