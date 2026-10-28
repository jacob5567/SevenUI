using System.Collections.Generic;
using System.Text;
using RoR2.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SevenUI
{
    // Restyles the item icons in the item bar at the top: each on a dark slot with its tier's color along the bottom,
    // and the stack count as Barlow Condensed text instead of sprite digits. The game keeps laying out the icons and
    // choosing what they show.
    internal sealed class ItemIconsStyler
    {
        private const string TopCenterPath = "MainContainer/MainUIArea/SpringCanvas/TopCenterCluster";
        // Under each item icon.
        private const string SpriteCountName = "Managed_Sprite_ItemCounter";
        private const string SlotName = "SevenUIItemSlot";

        // In the icon's own units (the icon is 58 across before the bar scales it).
        private const float SlotInset = 1f;
        private const float TierStripHeight = 4f;
        private const float CountFontSize = 20f;

        private readonly Transform root;
        private readonly StyleRecorder recorder = new StyleRecorder();
        private readonly List<ItemInventoryDisplay> displays = new List<ItemInventoryDisplay>();
        private readonly List<ItemIcon> icons = new List<ItemIcon>();

        public ItemIconsStyler(Transform root)
        {
            this.root = root;
        }

        public void Apply()
        {
            Transform topCenter = root.Find(TopCenterPath);
            if (!topCenter) return;
            topCenter.GetComponentsInChildren(true, displays);
            foreach (ItemInventoryDisplay display in displays)
            {
                display.GetComponentsInChildren(true, icons);
                foreach (ItemIcon icon in icons)
                {
                    StyleIcon(icon);
                }
                icons.Clear();
            }
            displays.Clear();
        }

        public void Restore()
        {
            recorder.Restore();
        }

        private void StyleIcon(ItemIcon icon)
        {
            if (icon.transform.Find(SlotName)) return;

            // The game shows and hides the sprite digits as the count changes, so they are hidden as a group.
            Transform spriteCount = icon.transform.Find(SpriteCountName);
            if (spriteCount && !spriteCount.GetComponent<CanvasGroup>())
            {
                CanvasGroup group = spriteCount.gameObject.AddComponent<CanvasGroup>();
                group.alpha = 0f;
                group.interactable = false;
                group.blocksRaycasts = false;
                recorder.Add(group);
            }
            // The icon still has the text label it used before sprite digits; the game no longer writes to it.
            TMP_Text count = icon.stackText;
            if (count)
            {
                recorder.Record(count);
                recorder.SetActive(count.gameObject, true);
                count.fontSize = CountFontSize;
                count.fontStyle |= FontStyles.Bold;
                count.color = Color.white;
                count.text = "";
            }

            GameObject slot = NewUIObject(SlotName, icon.transform);
            // First child, so it draws behind the item's picture.
            slot.transform.SetAsFirstSibling();
            RectTransform slotRect = (RectTransform)slot.transform;
            slotRect.anchorMin = Vector2.zero;
            slotRect.anchorMax = Vector2.one;
            slotRect.offsetMin = new Vector2(SlotInset, SlotInset);
            slotRect.offsetMax = new Vector2(-SlotInset, -SlotInset);
            Image slotImage = slot.AddComponent<Image>();
            slotImage.color = Palette.ItemSlot;
            slotImage.raycastTarget = false;

            GameObject strip = NewUIObject("TierStrip", slot.transform);
            RectTransform stripRect = (RectTransform)strip.transform;
            stripRect.anchorMin = Vector2.zero;
            stripRect.anchorMax = new Vector2(1f, 0f);
            stripRect.offsetMin = Vector2.zero;
            stripRect.offsetMax = new Vector2(0f, TierStripHeight);
            Image stripImage = strip.AddComponent<Image>();
            stripImage.raycastTarget = false;
            stripImage.enabled = false;

            ItemSlot follower = slot.AddComponent<ItemSlot>();
            follower.Icon = icon;
            follower.TierStrip = stripImage;
            follower.Count = count;
            recorder.Add(slot);
        }

        private static GameObject NewUIObject(string name, Transform parent)
        {
            GameObject created = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
            created.layer = parent.gameObject.layer;
            created.transform.SetParent(parent, false);
            return created;
        }

        public void Describe(StringBuilder output)
        {
            output.AppendLine($"  restyled item icon values: {recorder.Count}");
        }
    }
}
