using System.Collections.Generic;
using System.Text;
using RoR2;
using RoR2.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SevenUI
{
    // Restyles the interaction prompt ("Open Chest") and the pickup notifications as on the design canvas: flat
    // translucent panels in place of the soft gradients, the prompt's laid out by PromptPanel, and the notification
    // smaller and higher, with a light strip along its top and its icon on a dark slot.
    // Found by their components, wherever they sit in the HUD: LookingGlass moves the prompt.
    internal sealed class PromptsStyler
    {
        private const string UpperRightClusterPath = "MainContainer/MainUIArea/SpringCanvas/UpperRightCluster";
        // Under a notification's canvas group.
        private const string NotificationBackdropName = "Backdrop";
        private const string NotificationTextAreaName = "TextArea";
        private const string NotificationIconAreaName = "IconArea";
        public const string NotificationStripName = "SevenUINotificationStrip";
        // The design canvas's sizes, in its pixels, scaled to the HUD's units, which the notifications' are once they
        // aren't scaled up: a 500px panel with 14px by 10px of padding and a 3px strip, a 56px slot with the
        // icon inside it, 14px from the 402px of text, a 22px title 2px over a 16px description. Its middle is 758px
        // down a 1080px screen.
        private const float Canvas = HudUnits.PerCanvasPixel;
        private const float NotificationPaddingX = 14f * Canvas;
        private const float NotificationPaddingY = 10f * Canvas;
        private const float NotificationGap = 14f * Canvas;
        private const float NotificationStripHeight = 3f * Canvas;
        private const float NotificationSlotSize = 56f * Canvas;
        private const float NotificationIconSize = 44f * Canvas;
        private const float NotificationTextWidth = 402f * Canvas;
        private const float NotificationTitleSize = 22f * Canvas;
        private const float NotificationDescriptionSize = 16f * Canvas;
        private const float NotificationTextGap = 2f * Canvas;
        private const float NotificationMiddleDown = 758f / 1080f;

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
                    panels.Add(new PromptPanel(prompt, RunInfoPanel(), recorder));
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

        // The run info's panel, whose right edge the prompt's panel lines up with.
        private RectTransform RunInfoPanel()
        {
            Transform cluster = root.Find(UpperRightClusterPath);
            if (!cluster) return null;
            foreach (RectTransform rect in cluster.GetComponentsInChildren<RectTransform>(true))
            {
                if (rect.name == RunInfoStyler.BackgroundName) return rect;
            }
            return null;
        }

        // Each prompt row's soft strip gives way to the one fitted panel. The game shows and hides the rows by their
        // objects, never the strip's component.
        private void HideStrip(GameObject display)
        {
            recorder.Disable(display ? display.GetComponent<RawImage>() : null);
        }

        // As on the design canvas: a 500px panel with a light strip along its top, the icon on a dark slot and the
        // title in bold white over the description in grey, centered with its middle 758px down a 1080px screen. The
        // game draws it 1.3 times larger, about 670px wide, low over the skills, with the title in the item's tier
        // color. Done on every pass over the HUD, so each step only changes what isn't changed yet.
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
                // The game's backdrop reaches past the content by the content's padding; flat, it is the panel.
                RectTransform backdropRect = (RectTransform)backdrop;
                recorder.RecordRect(backdropRect);
                backdropRect.anchoredPosition = Vector2.zero;
                backdropRect.sizeDelta = Vector2.zero;
                if (!backdrop.Find(NotificationStripName))
                {
                    AddStrip(backdrop);
                }
            }
            Transform textArea = content.Find(NotificationTextAreaName);
            recorder.Disable(textArea ? textArea.GetComponent<RawImage>() : null);

            if (notification.titleTMP)
            {
                recorder.Record(notification.titleTMP);
                notification.titleTMP.fontStyle |= FontStyles.Bold;
                notification.titleTMP.fontSize = NotificationTitleSize;
                notification.titleTMP.color = Palette.Text;
                SetPreferredWidth(notification.titleTMP.GetComponent<LayoutElement>());
            }
            TMP_Text description = notification.descriptionText ? notification.descriptionText.GetComponent<TMP_Text>() : null;
            if (description)
            {
                recorder.Record(description);
                description.fontSize = NotificationDescriptionSize;
                description.color = Palette.HeaderText;
                SetPreferredWidth(description.GetComponent<LayoutElement>());
            }

            HorizontalLayoutGroup row = content.GetComponent<HorizontalLayoutGroup>();
            Transform iconArea = content.Find(NotificationIconAreaName);
            VerticalLayoutGroup texts = textArea ? textArea.GetComponent<VerticalLayoutGroup>() : null;
            if (!row || !iconArea || !texts || content == notification.transform) return;
            if (!recorder.HasPosition((RectTransform)notification.transform))
            {
                Vector3 scale = content.localScale;
                bool expand = texts.childForceExpandHeight;
                TextAnchor alignment = texts.childAlignment;
                recorder.OnRestore(() =>
                {
                    if (content)
                    {
                        content.localScale = scale;
                    }
                    if (texts)
                    {
                        texts.childForceExpandHeight = expand;
                        texts.childAlignment = alignment;
                    }
                });
                content.localScale = Vector3.one;
                texts.childForceExpandHeight = false;
                texts.childAlignment = TextAnchor.MiddleLeft;
                Place((RectTransform)notification.transform);
            }
            recorder.Record(row);
            int paddingX = Mathf.RoundToInt(NotificationPaddingX);
            int paddingY = Mathf.RoundToInt(NotificationPaddingY);
            row.padding = new RectOffset(paddingX, paddingX, paddingY, paddingY);
            row.spacing = NotificationGap;
            recorder.Record(texts);
            texts.padding = new RectOffset(0, 0, 0, 0);
            texts.spacing = NotificationTextGap;
            StyleIconArea(iconArea, notification.iconImage);
        }

        private void SetPreferredWidth(LayoutElement element)
        {
            if (!element) return;
            float width = element.preferredWidth;
            if (Mathf.Approximately(width, NotificationTextWidth)) return;
            recorder.OnRestore(() =>
            {
                if (element)
                {
                    element.preferredWidth = width;
                }
            });
            element.preferredWidth = NotificationTextWidth;
        }

        // The slot behind the icon, which keeps a margin inside it; the game's temporary and rental marks cover the
        // slot as they covered the icon. The drone and trial notifications hold their icon in a frame of their own,
        // drawn larger than the slot (the trial's at 1.6 times, its icon at 0.45 inside it); the frame goes, as the
        // generic notification's slot background does, and the icon is centered at the generic one's size.
        private void StyleIconArea(Transform iconArea, RawImage icon)
        {
            LayoutElement element = iconArea.GetComponent<LayoutElement>();
            if (element)
            {
                recorder.Record(element);
                element.minHeight = NotificationSlotSize;
                element.preferredHeight = NotificationSlotSize;
                float width = element.preferredWidth;
                if (!Mathf.Approximately(width, NotificationSlotSize))
                {
                    recorder.OnRestore(() =>
                    {
                        if (element)
                        {
                            element.preferredWidth = width;
                        }
                    });
                    element.preferredWidth = NotificationSlotSize;
                }
            }
            Image slot = iconArea.GetComponent<Image>();
            if (slot)
            {
                recorder.Record(slot);
                recorder.SetEnabled(slot, true);
                slot.sprite = null;
                slot.type = Image.Type.Simple;
                slot.color = Palette.ItemSlot;
            }
            foreach (Transform child in iconArea)
            {
                RectTransform rect = (RectTransform)child;
                if (recorder.HasRect(rect)) continue;
                if (icon && child == icon.transform)
                {
                    CenterInSlot(rect, NotificationIconSize);
                }
                else if (icon && icon.transform.parent == child)
                {
                    CenterInSlot(rect, NotificationSlotSize);
                    recorder.Disable(child.GetComponent<Graphic>());
                    CenterInSlot(icon.rectTransform, NotificationIconSize);
                }
                else
                {
                    CenterInSlot(rect, NotificationSlotSize);
                }
            }
        }

        private void CenterInSlot(RectTransform rect, float size)
        {
            recorder.RecordRect(rect);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(size, size);
            recorder.SetLocalScale(rect, Vector3.one);
            // The trial's frame and icon pop in on a scale curve, which sets their scale from a base scale, theirs in
            // the prefab, every frame; the curve keeps playing, from the base of one.
            ObjectScaleCurve curve = rect.GetComponent<ObjectScaleCurve>();
            if (curve && curve.baseScale != Vector3.one)
            {
                Vector3 baseScale = curve.baseScale;
                recorder.OnRestore(() =>
                {
                    if (curve)
                    {
                        curve.baseScale = baseScale;
                    }
                });
                curve.baseScale = Vector3.one;
            }
        }

        // Centered across the screen, its middle the canvas's distance down. The notification is a child of the game's
        // notification area, whose content is centered on it. The HUD's camera draws in perspective, so the spot is
        // found on screen, through the camera, rather than in the HUD's units.
        private void Place(RectTransform panel)
        {
            RectTransform parent = panel.parent as RectTransform;
            Canvas canvas = root.GetComponent<Canvas>();
            if (!parent || !canvas) return;
            Camera camera = canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            Rect screen = camera ? camera.pixelRect : new Rect(0f, 0f, Screen.width, Screen.height);
            Vector2 spot = new Vector2(screen.center.x, screen.yMax - screen.height * NotificationMiddleDown);
            if (!RectTransformUtility.ScreenPointToWorldPointInRectangle(parent, spot, camera, out Vector3 target)) return;
            Vector3 middle = panel.TransformPoint(panel.rect.center);
            Vector2 shift = parent.InverseTransformPoint(target) - parent.InverseTransformPoint(middle);
            recorder.SetAnchoredPosition(panel, panel.anchoredPosition + shift);
        }

        private void AddStrip(Transform backdrop)
        {
            GameObject strip = new GameObject(NotificationStripName, typeof(RectTransform), typeof(CanvasRenderer));
            strip.layer = backdrop.gameObject.layer;
            RectTransform rect = (RectTransform)strip.transform;
            rect.SetParent(backdrop, false);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(0f, -NotificationStripHeight);
            rect.offsetMax = Vector2.zero;
            Image image = strip.AddComponent<Image>();
            image.color = Palette.Text;
            image.raycastTarget = false;
            recorder.Add(strip);
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
