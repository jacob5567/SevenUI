using System.Collections.Generic;
using System.Text;
using RoR2.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SevenUI
{
    // Restyles the interaction prompt ("Open Chest") and the pickup notifications: flat translucent panels in place of
    // the soft gradients, with the notification's description in grey. Found by their components, wherever they sit
    // in the HUD: LookingGlass moves the prompt.
    internal sealed class PromptsStyler
    {
        // Under a notification's canvas group.
        private const string NotificationBackdropName = "Backdrop";
        private const string NotificationTextAreaName = "TextArea";

        private readonly Transform root;
        private readonly StyleRecorder recorder = new StyleRecorder();
        private readonly List<ContextManager> prompts = new List<ContextManager>();
        private readonly List<GenericNotification> notifications = new List<GenericNotification>();

        public PromptsStyler(Transform root)
        {
            this.root = root;
        }

        public void Apply()
        {
            root.GetComponentsInChildren(true, prompts);
            foreach (ContextManager prompt in prompts)
            {
                FlattenStrip(prompt.contextDisplay);
                FlattenStrip(prompt.inspectDisplay);
            }
            prompts.Clear();
            root.GetComponentsInChildren(true, notifications);
            foreach (GenericNotification notification in notifications)
            {
                StyleNotification(notification);
            }
            notifications.Clear();
        }

        public void Restore()
        {
            recorder.Restore();
        }

        // The prompt's soft strip, which the game shows and hides by its object, becomes a flat panel.
        private void FlattenStrip(GameObject display)
        {
            RawImage strip = display ? display.GetComponent<RawImage>() : null;
            if (!strip) return;
            recorder.Record(strip);
            strip.texture = null;
            strip.color = Palette.Panel;
        }

        private void StyleNotification(GenericNotification notification)
        {
            Transform content = notification.canvasGroup ? notification.canvasGroup.transform : notification.transform;
            Transform backdrop = content.Find(NotificationBackdropName);
            Image backdropImage = backdrop ? backdrop.GetComponent<Image>() : null;
            if (backdropImage)
            {
                recorder.Record(backdropImage);
                backdropImage.sprite = null;
                backdropImage.type = Image.Type.Simple;
                backdropImage.color = Palette.NotificationPanel;
            }
            Transform textArea = content.Find(NotificationTextAreaName);
            recorder.Disable(textArea ? textArea.GetComponent<RawImage>() : null);

            // The title keeps the color the game gives it, the item's tier color.
            if (notification.titleTMP)
            {
                recorder.Record(notification.titleTMP);
                notification.titleTMP.fontStyle |= FontStyles.Bold;
            }
            TMP_Text description = notification.descriptionText ? notification.descriptionText.GetComponent<TMP_Text>() : null;
            if (description)
            {
                recorder.Record(description);
                description.color = Palette.HeaderText;
            }
        }

        public void Describe(StringBuilder output)
        {
            output.AppendLine($"  restyled prompt and notification values: {recorder.Count}");
        }
    }
}
