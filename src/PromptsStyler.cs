using System.Collections.Generic;
using System.Text;
using RoR2.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SevenUI
{
    // Restyles the interaction prompt ("Open Chest") and the pickup notifications: flat translucent panels in place of
    // the soft gradients, the prompt's fitted to its text (PromptPanel), with the notification's description in grey.
    // Found by their components, wherever they sit in the HUD: LookingGlass moves the prompt.
    internal sealed class PromptsStyler
    {
        // Under a notification's canvas group.
        private const string NotificationBackdropName = "Backdrop";
        private const string NotificationTextAreaName = "TextArea";

        private readonly Transform root;
        private readonly StyleRecorder recorder = new StyleRecorder();
        private readonly List<ContextManager> prompts = new List<ContextManager>();
        private readonly List<PromptPanel> panels = new List<PromptPanel>();
        private readonly List<GenericNotification> notifications = new List<GenericNotification>();

        public PromptsStyler(Transform root)
        {
            this.root = root;
        }

        public void Apply()
        {
            root.GetComponentsInChildren(true, prompts);
            panels.RemoveAll(panel => !panel.Alive);
            foreach (ContextManager prompt in prompts)
            {
                HideStrip(prompt.contextDisplay);
                HideStrip(prompt.inspectDisplay);
                if (!panels.Exists(panel => panel.Prompt == prompt))
                {
                    panels.Add(new PromptPanel(prompt, recorder));
                }
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
            panels.Clear();
        }

        // Every frame while the panels are on: the prompt's text changes without adding objects to the HUD.
        public void Update()
        {
            foreach (PromptPanel panel in panels)
            {
                panel.Update();
            }
        }

        // Each prompt row's soft strip gives way to the one fitted panel. The game shows and hides the rows by their
        // objects, never the strip's component.
        private void HideStrip(GameObject display)
        {
            recorder.Disable(display ? display.GetComponent<RawImage>() : null);
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
            foreach (PromptPanel panel in panels)
            {
                if (!panel.Alive) continue;
                output.AppendLine($"  prompt panel: active={panel.Panel.gameObject.activeSelf} position={panel.Panel.anchoredPosition} " +
                                  $"size={panel.Panel.sizeDelta}");
            }
        }
    }
}
