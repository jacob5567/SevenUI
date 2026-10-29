using System.Collections.Generic;
using RoR2;
using RoR2.UI;
using UnityEngine;
using UnityEngine.UI;

namespace SevenUI
{
    // The player's buffs on dark squares like the item bar's slots, with a strip along the bottom in green for a buff,
    // red for a debuff and gold for a cooldown, as on the design canvas; they wrap onto another row above when the
    // next would run past the health bar's width. The game's BuffDisplay keeps choosing and ordering the icons, and
    // puts them in one row whenever they change; each frame they are moved to their place in the rows, in the game's
    // order. Each icon's own image is the buff's picture, so the squares are kept behind the icons as the display's
    // first children rather than as the icons' own.
    internal sealed class BuffRows
    {
        public const string SlotName = "SevenUIBuffSlot";
        // In the buff display's units, about 0.875 of the design canvas's pixels (the game's 48-unit icons show at
        // 42): the canvas's 34px squares 4px apart, with a 2px strip, and its 13px count. The picture sits inside
        // the square with a margin, as the canvas's does.
        private const float SlotSize = 39f;
        private const float SlotGap = 4.5f;
        private const float IconSize = 29f;
        private const float StripHeight = 2.5f;
        private const float CountFontSize = 15f;
        private const float CountInset = 2.5f;

        private readonly BuffDisplay display;
        private readonly RectTransform rect;
        private readonly RectTransform healthBar;
        private readonly StyleRecorder recorder;
        private readonly List<Image> slots = new List<Image>();
        private readonly List<Image> strips = new List<Image>();
        private readonly HashSet<BuffIcon> styled = new HashSet<BuffIcon>();
        private readonly Vector3[] corners = new Vector3[4];

        public BuffRows(BuffDisplay display, RectTransform healthBar, StyleRecorder recorder)
        {
            this.display = display;
            rect = (RectTransform)display.transform;
            this.healthBar = healthBar;
            this.recorder = recorder;
            // Once the look is restored, the icons go back to the game's single row, in its order, until it next lays
            // them out itself.
            recorder.OnRestore(() =>
            {
                if (!display || display.buffIconDisplayData == null) return;
                float x = 0f;
                foreach (BuffDisplay.BuffIconDisplayData data in display.buffIconDisplayData)
                {
                    if (!data.buffIconComponent) continue;
                    data.buffIconComponent.rectTransform.anchoredPosition = new Vector2(x, 0f);
                    x += display.iconWidth;
                }
            });
        }

        public bool Alive => display && rect;

        // A position compare per icon when nothing moved.
        public void Update()
        {
            List<BuffDisplay.BuffIconDisplayData> buffs = display.buffIconDisplayData;
            if (buffs == null) return;
            float pitch = SlotSize + SlotGap;
            int perRow = Mathf.Max(1, Mathf.FloorToInt((RowWidth() + SlotGap) / pitch));
            float margin = (SlotSize - IconSize) / 2f;
            int shown = 0;
            foreach (BuffDisplay.BuffIconDisplayData buff in buffs)
            {
                BuffIcon icon = buff.buffIconComponent;
                if (!icon || !icon.gameObject.activeSelf || icon.transform.parent != rect) continue;
                if (styled.Add(icon))
                {
                    StyleIcon(icon, margin);
                }
                Vector2 origin = new Vector2(shown % perRow * pitch, shown / perRow * pitch);
                RectTransform iconRect = icon.rectTransform;
                Vector2 target = origin + new Vector2(margin, margin) + Vector2.Scale(iconRect.pivot, iconRect.rect.size);
                if (iconRect.anchoredPosition != target)
                {
                    iconRect.anchoredPosition = target;
                }
                PlaceSlot(shown, origin, StripColor(buff.buffDef));
                shown++;
            }
            for (int i = shown; i < slots.Count; i++)
            {
                if (slots[i] && slots[i].gameObject.activeSelf)
                {
                    slots[i].gameObject.SetActive(false);
                }
            }
        }

        // The health bar's width in the buff display's units.
        private float RowWidth()
        {
            if (!healthBar) return rect.rect.width;
            healthBar.GetWorldCorners(corners);
            return rect.InverseTransformPoint(corners[2]).x - rect.InverseTransformPoint(corners[0]).x;
        }

        private static Color StripColor(BuffDef buff)
        {
            if (!buff) return Palette.Buff;
            if (buff.isDebuff) return Palette.Debuff;
            return buff.isCooldown ? Palette.BuffCooldown : Palette.Buff;
        }

        // The picture smaller inside its square, and the count in the square's bottom-right corner, as on an item.
        private void StyleIcon(BuffIcon icon, float margin)
        {
            RectTransform iconRect = icon.rectTransform;
            recorder.RecordRect(iconRect);
            iconRect.anchorMin = iconRect.anchorMax = Vector2.zero;
            // The icon's aspect fitter sets its width from its height.
            iconRect.sizeDelta = new Vector2(IconSize, IconSize);
            if (!icon.stackCount) return;
            RectTransform count = icon.stackCount.rectTransform;
            recorder.Record(icon.stackCount);
            recorder.RecordRect(count);
            count.anchorMin = count.anchorMax = new Vector2(1f, 0f);
            count.pivot = new Vector2(1f, 0f);
            count.anchoredPosition = new Vector2(margin - CountInset, CountInset - margin);
            count.sizeDelta = new Vector2(SlotSize, SlotSize);
            icon.stackCount.fontSize = CountFontSize;
            icon.stackCount.alignment = TMPro.TextAlignmentOptions.BottomRight;
            icon.stackCount.margin = Vector4.zero;
        }

        private void PlaceSlot(int index, Vector2 origin, Color stripColor)
        {
            while (slots.Count <= index)
            {
                AddSlot();
            }
            Image slot = slots[index];
            if (!slot) return;
            if (!slot.gameObject.activeSelf)
            {
                slot.gameObject.SetActive(true);
            }
            RectTransform slotRect = slot.rectTransform;
            if (slotRect.anchoredPosition != origin)
            {
                slotRect.anchoredPosition = origin;
            }
            if (strips[index].color != stripColor)
            {
                strips[index].color = stripColor;
            }
        }

        private void AddSlot()
        {
            GameObject created = NewUIObject(SlotName, rect);
            // First, so the icons, which the game adds after, draw over it.
            created.transform.SetAsFirstSibling();
            RectTransform slotRect = (RectTransform)created.transform;
            slotRect.anchorMin = slotRect.anchorMax = slotRect.pivot = Vector2.zero;
            slotRect.sizeDelta = new Vector2(SlotSize, SlotSize);
            Image slot = created.AddComponent<Image>();
            slot.color = Palette.ItemSlot;
            slot.raycastTarget = false;

            GameObject strip = NewUIObject("Strip", created.transform);
            RectTransform stripRect = (RectTransform)strip.transform;
            stripRect.anchorMin = Vector2.zero;
            stripRect.anchorMax = new Vector2(1f, 0f);
            stripRect.offsetMin = Vector2.zero;
            stripRect.offsetMax = new Vector2(0f, StripHeight);
            Image stripImage = strip.AddComponent<Image>();
            stripImage.raycastTarget = false;

            slots.Add(slot);
            strips.Add(stripImage);
            recorder.Add(created);
        }

        private static GameObject NewUIObject(string name, Transform parent)
        {
            GameObject created = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
            created.layer = parent.gameObject.layer;
            created.transform.SetParent(parent, false);
            return created;
        }
    }
}
