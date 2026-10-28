using System.Collections.Generic;
using System.Text;
using RoR2.UI;
using UnityEngine;
using UnityEngine.UI;

namespace SevenUI
{
    // Restyles the chat's input field, shown while typing, as a flat dark panel. The messages are plain text and
    // already in Barlow.
    internal sealed class ChatStyler
    {
        // Under the chat box.
        private const string InputFieldPath = "PermanentBG/Input Field";

        private readonly Transform root;
        private readonly StyleRecorder recorder = new StyleRecorder();
        private readonly List<ChatBox> chats = new List<ChatBox>();

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
                // The game turns the field's image on and off with the input, so only its look changes.
                recorder.Record(image);
                image.sprite = null;
                image.type = Image.Type.Simple;
                image.color = Palette.ScoreboardPanel;
            }
            chats.Clear();
        }

        public void Restore()
        {
            recorder.Restore();
        }

        public void Describe(StringBuilder output)
        {
            output.AppendLine($"  restyled chat values: {recorder.Count}");
        }
    }
}
