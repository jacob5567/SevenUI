using System.Collections.Generic;
using RoR2;
using RoR2.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SevenUI
{
    // The player's buffs on dark squares like the item bar's slots, with a strip along the bottom in green for a buff,
    // red for a debuff and gold for a cooldown, as on the design canvas; they wrap onto another row above when the
    // next would run past the bars' width. The game's BuffDisplay keeps choosing and ordering the icons, and puts them
    // in one row whenever they change; each frame they are moved to their place in the rows, in the game's order. Each
    // icon's own image is the buff's picture, so the squares are kept behind the icons as the display's first
    // children rather than as the icons' own.
    //
    // The stack count goes in the square's top-right corner, as a bare number, and LookingGlass's timer under the
    // square. LookingGlass writes both into the icon's one stack text ("<size=80%>3.2</size>\nx3"), so that text is
    // hidden; the count is read from the icon, and the timer from that text, into labels drawn over the icons.
    internal sealed class BuffRows
    {
        public const string SlotName = "SevenUIBuffSlot";
        public const string LabelsName = "SevenUIBuffLabels";
        // LookingGlass's timer, at the start of the stack text.
        private const string TimerStart = "<size=";
        private const string TimerEnd = "</size>";
        private const string PercentSuffix = "%";

        // The design canvas's sizes, in its pixels, scaled to the buff display's units: 34px squares 4px apart, with a
        // 2px strip; the count 13px, its baseline about 12.7px under the square's top and 2px from its right; the
        // timer 12px, its baseline about 14.6px under the square, which leaves 17px under each row for it. The
        // picture sits inside the square with a margin, a little larger than the canvas's placeholder.
        private const float Canvas = HudUnits.PerCanvasPixel;
        private const float SlotSize = 34f * Canvas;
        private const float SlotGap = 4f * Canvas;
        private const float IconSize = 25f * Canvas;
        private const float StripHeight = 2f * Canvas;
        private const float CountFontSize = 13f * Canvas;
        private const float CountInset = 2f * Canvas;
        private const float CountBaseline = 12.7f * Canvas;
        private const float TimerFontSize = 12f * Canvas;
        private const float TimerBaseline = 14.6f * Canvas;
        private const float TimerRoom = 17f * Canvas;
        private const float LineBox = 40f;

        private readonly BuffDisplay display;
        private readonly RectTransform rect;
        private readonly RectTransform bounds;
        private readonly StyleRecorder recorder;
        private readonly List<Image> slots = new List<Image>();
        private readonly List<Image> strips = new List<Image>();
        private readonly List<TMP_Text> counts = new List<TMP_Text>();
        private readonly List<TMP_Text> timers = new List<TMP_Text>();
        private readonly List<string> timerSources = new List<string>();
        // The buff and count each slot's count label shows.
        private readonly List<(BuffDef buff, int count)> countSources = new List<(BuffDef, int)>();
        private readonly HashSet<BuffIcon> styled = new HashSet<BuffIcon>();
        private readonly Vector3[] corners = new Vector3[4];
        private RectTransform labels;

        // The rows are no wider than the bounds, the bars under them.
        public BuffRows(BuffDisplay display, RectTransform bounds, StyleRecorder recorder)
        {
            this.display = display;
            rect = (RectTransform)display.transform;
            this.bounds = bounds;
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

        // A position compare and a few reads per icon when nothing changed.
        public void Update()
        {
            List<BuffDisplay.BuffIconDisplayData> buffs = display.buffIconDisplayData;
            if (buffs == null) return;
            float pitch = SlotSize + SlotGap;
            int perRow = Mathf.Max(1, Mathf.FloorToInt((RowWidth() + SlotGap) / pitch));
            float margin = (SlotSize - IconSize) / 2f;

            bool anyTimer = false;
            foreach (BuffDisplay.BuffIconDisplayData buff in buffs)
            {
                BuffIcon icon = buff.buffIconComponent;
                if (Shows(icon) && Timer(icon) != null)
                {
                    anyTimer = true;
                    break;
                }
            }
            float rowPitch = pitch + (anyTimer ? TimerRoom : 0f);

            int shown = 0;
            foreach (BuffDisplay.BuffIconDisplayData buff in buffs)
            {
                BuffIcon icon = buff.buffIconComponent;
                if (!Shows(icon)) continue;
                if (styled.Add(icon))
                {
                    StyleIcon(icon);
                }
                Vector2 origin = new Vector2(shown % perRow * pitch, shown / perRow * rowPitch);
                RectTransform iconRect = icon.rectTransform;
                Vector2 target = origin + new Vector2(margin, margin) + Vector2.Scale(iconRect.pivot, iconRect.rect.size);
                if (iconRect.anchoredPosition != target)
                {
                    iconRect.anchoredPosition = target;
                }
                PlaceSlot(shown, origin, StripColor(buff.buffDef));
                if (countSources[shown] != (icon.buffDef, icon.buffCount))
                {
                    countSources[shown] = (icon.buffDef, icon.buffCount);
                    SetText(counts[shown], Count(icon));
                }
                string timer = Timer(icon);
                if (!ReferenceEquals(timer, timerSources[shown]))
                {
                    timerSources[shown] = timer;
                    SetText(timers[shown], timer != null ? TimerText(timer) : "");
                }
                shown++;
            }
            for (int i = shown; i < slots.Count; i++)
            {
                if (slots[i] && slots[i].gameObject.activeSelf)
                {
                    slots[i].gameObject.SetActive(false);
                    counts[i].gameObject.SetActive(false);
                    timers[i].gameObject.SetActive(false);
                }
            }
            // The game adds new icons as the display's last children, over the labels; the labels go back on top.
            if (labels && labels.GetSiblingIndex() != rect.childCount - 1)
            {
                labels.SetAsLastSibling();
            }
        }

        private bool Shows(BuffIcon icon) => icon && icon.gameObject.activeSelf && icon.transform.parent == rect;

        // The bounds' width in the buff display's units.
        private float RowWidth()
        {
            if (!bounds) return rect.rect.width;
            bounds.GetWorldCorners(corners);
            return rect.InverseTransformPoint(corners[2]).x - rect.InverseTransformPoint(corners[0]).x;
        }

        private static Color StripColor(BuffDef buff)
        {
            if (!buff) return Palette.Buff;
            if (buff.isDebuff) return Palette.Debuff;
            return buff.isCooldown ? Palette.BuffCooldown : Palette.Buff;
        }

        // The count the game shows for a stacking buff, without its "x", and only once there is more than one, as on
        // an item; a percentage always shows.
        private static string Count(BuffIcon icon)
        {
            BuffDef buff = icon.buffDef;
            if (!buff || !buff.canStack) return "";
            int value = icon.buffCount + buff.displayOffset;
            if (buff.stackingDisplayMethod == BuffDef.StackingDisplayMethod.Percentage) return value + PercentSuffix;
            return value > 1 ? value.ToString() : "";
        }

        // LookingGlass's text in the icon's stack text, or null. The game writes that text from a string builder,
        // which leaves TMP's string stale (and makes reading it build a new one), so it is read only when TMP's string
        // is the last text written, as it is after LookingGlass sets it.
        private static string Timer(BuffIcon icon)
        {
            TMP_Text text = icon.stackCount;
            if (!text || !text.enabled || text.m_IsTextBackingStringDirty) return null;
            string written = text.m_text;
            return written != null && written.StartsWith(TimerStart, System.StringComparison.Ordinal) ? written : null;
        }

        private static string TimerText(string written)
        {
            int start = written.IndexOf('>') + 1;
            int end = written.IndexOf(TimerEnd, System.StringComparison.Ordinal);
            return start > 0 && end > start ? written.Substring(start, end - start) : "";
        }

        private static void SetText(TMP_Text label, string text)
        {
            if (!string.Equals(label.text, text, System.StringComparison.Ordinal))
            {
                label.text = text;
            }
        }

        // The picture smaller inside its square, and the game's stack text hidden for the mod's labels.
        private void StyleIcon(BuffIcon icon)
        {
            RectTransform iconRect = icon.rectTransform;
            recorder.RecordRect(iconRect);
            iconRect.anchorMin = iconRect.anchorMax = Vector2.zero;
            // The icon's aspect fitter sets its width from its height.
            iconRect.sizeDelta = new Vector2(IconSize, IconSize);
            if (!icon.stackCount) return;
            recorder.Record(icon.stackCount);
            icon.stackCount.alpha = 0f;
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
                counts[index].gameObject.SetActive(true);
                timers[index].gameObject.SetActive(true);
            }
            RectTransform slotRect = slot.rectTransform;
            RectTransform countRect = counts[index].rectTransform;
            Vector2 countAt = origin + new Vector2(SlotSize - CountInset, SlotSize - CountBaseline);
            if (slotRect.anchoredPosition != origin || countRect.anchoredPosition != countAt)
            {
                slotRect.anchoredPosition = origin;
                countRect.anchoredPosition = countAt;
                timers[index].rectTransform.anchoredPosition = origin + new Vector2(SlotSize / 2f, -TimerBaseline);
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

            if (!labels)
            {
                GameObject holder = NewUIObject(LabelsName, rect);
                labels = (RectTransform)holder.transform;
                labels.anchorMin = labels.anchorMax = labels.pivot = Vector2.zero;
                labels.sizeDelta = Vector2.zero;
                recorder.Add(holder);
            }
            TMP_Text count = AddLabel("Count", new Vector2(1f, 0.5f), TextAlignmentOptions.BaselineRight, CountFontSize);
            count.gameObject.AddComponent<CondensedText>();
            count.fontStyle = FontStyles.Bold;
            count.color = Palette.Text;
            counts.Add(count);
            TMP_Text timer = AddLabel("Timer", new Vector2(0.5f, 0.5f), TextAlignmentOptions.Baseline, TimerFontSize);
            timer.fontStyle = FontStyles.Bold;
            timer.color = Palette.BuffTimer;
            timers.Add(timer);
            timerSources.Add(null);
            countSources.Add((null, -1));
        }

        // A line over the icons, placed by its baseline at its box's middle. In the buff icon prefab's font, the
        // game's, so the font swap gives it Barlow like the rest of the HUD.
        private TMP_Text AddLabel(string name, Vector2 pivot, TextAlignmentOptions alignment, float size)
        {
            GameObject line = NewUIObject(name, labels);
            RectTransform lineRect = (RectTransform)line.transform;
            lineRect.anchorMin = lineRect.anchorMax = Vector2.zero;
            lineRect.pivot = pivot;
            lineRect.sizeDelta = new Vector2(SlotSize * 2f, LineBox);
            TextMeshProUGUI label = line.AddComponent<TextMeshProUGUI>();
            BuffIcon prefab = display.buffIconPrefab ? display.buffIconPrefab.GetComponent<BuffIcon>() : null;
            if (prefab && prefab.stackCount)
            {
                label.font = prefab.stackCount.font;
                label.fontSharedMaterial = prefab.stackCount.fontSharedMaterial;
            }
            label.fontSize = size;
            label.alignment = alignment;
            label.enableWordWrapping = false;
            label.overflowMode = TextOverflowModes.Overflow;
            label.raycastTarget = false;
            return label;
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
