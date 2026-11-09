using System.Collections.Generic;
using System.Text;
using RoR2.UI;
using UnityEngine;
using UnityEngine.UI;

namespace SevenUI
{
    // Restyles the chat's input field, shown while typing, as an outlined field like the pickers' cancel button: a
    // faint face the stage shows through, a 1px outline in the divider's grey, and a 2px accent strip down its left
    // edge, which marks it as the field in use. The messages are plain text and already in Barlow.
    internal sealed class ChatStyler
    {
        // Under the chat box.
        private const string InputFieldPath = "PermanentBG/Input Field";
        public const string OutlineName = "SevenUIChatOutline";
        public const string EdgeName = "SevenUIChatEdge";

        // The design canvas's sizes, in its pixels, scaled to the HUD's units.
        private const float Canvas = HudUnits.PerCanvasPixel;
        private const float OutlineEdge = 1f * Canvas;
        private const float EdgeWidth = 2f * Canvas;

        private readonly Transform root;
        private readonly StyleRecorder recorder = new StyleRecorder();
        private readonly List<ChatBox> chats = new List<ChatBox>();
        // Each field's image and the mod's parts over it, which follow the image on and off.
        private readonly List<(Image Field, Graphic[] Parts)> fields = new List<(Image, Graphic[])>();

        public ChatStyler(Transform root)
        {
            this.root = root;
        }

        public void Apply()
        {
            root.GetComponentsInChildren(true, chats);
            foreach (ChatBox chat in chats)
            {
                Transform field = chat.transform.Find(InputFieldPath);
                Image image = field ? field.GetComponent<Image>() : null;
                if (!image) continue;
                recorder.Record(image);
                image.sprite = null;
                image.type = Image.Type.Simple;
                image.color = Palette.GhostButton;
                // Once per field: each part added changes the HUD's object count, which restyles the HUD again on
                // the next frame, so adding them on every pass would restyle it every frame as it grows.
                if (field.Find(OutlineName)) continue;

                PixelOutline outline = PixelOutline.Add(field, OutlineName, Palette.Divider, OutlineEdge);
                recorder.Add(outline.gameObject);
                RectTransform edge = (RectTransform)NewImage(EdgeName, field, Palette.Accent).transform;
                edge.anchorMax = new Vector2(0f, 1f);
                edge.pivot = new Vector2(0f, 0.5f);
                edge.sizeDelta = new Vector2(EdgeWidth, 0f);
                // Under the typed text.
                edge.SetAsFirstSibling();
                outline.transform.SetAsFirstSibling();
                fields.Add((image, new Graphic[] { outline, edge.GetComponent<Image>() }));
            }
            chats.Clear();
            Update();
        }

        // Every frame: the game shows and hides the field by turning its image on and off (ChatBox.showInput), not
        // always its object, so the outline and the strip follow the image. A read and a compare per field.
        public void Update()
        {
            foreach ((Image field, Graphic[] parts) in fields)
            {
                bool shown = field && field.enabled;
                foreach (Graphic part in parts)
                {
                    if (part && part.enabled != shown)
                    {
                        part.enabled = shown;
                    }
                }
            }
        }

        public void Restore()
        {
            recorder.Restore();
            fields.Clear();
        }

        public void Describe(StringBuilder output)
        {
            output.AppendLine($"  restyled chat values: {recorder.Count}, outlined input fields: {fields.Count}");
        }

        // Stretched over the field, outside its layout, and passing clicks through to it.
        private Image NewImage(string name, Transform field, Color color)
        {
            GameObject created = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
            created.layer = field.gameObject.layer;
            created.transform.SetParent(field, false);
            recorder.Add(created);
            RectTransform rect = (RectTransform)created.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            created.AddComponent<LayoutElement>().ignoreLayout = true;
            Image image = created.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }
    }
}
