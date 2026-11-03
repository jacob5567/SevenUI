using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SevenUI
{
    // LookingGlass's item stats under a description, as on the canvas's item tooltip: a 1px line 8px below the text,
    // then, 8px under it, a row per stat with its name on the left and its value on the right in the accent color.
    // A picker's "With another stack, you will have:" goes above the rows. Laid out by hand from its parent's top
    // left, so the tooltip and the pickers can both place it.
    internal sealed class StatsSection
    {
        public const string SectionName = "SevenUIStats";

        // The canvas's sizes, in its pixels.
        private const float GapAbove = 8f;
        private const float DividerHeight = 1f;
        private const float GapBelow = 8f;
        private const float RowGap = 2f;
        private const float LabelValueGap = 12f;

        public readonly RectTransform Root;

        private readonly TMP_Text style;
        private readonly float unit;
        private readonly float textSize;
        private readonly Action<GameObject> created;
        private readonly RectTransform divider;
        private readonly TMP_Text caption;
        private readonly List<TMP_Text> labels = new List<TMP_Text>();
        private readonly List<TMP_Text> values = new List<TMP_Text>();
        private int rows;

        // 'style' is a text whose font the section's texts take; 'unit' is the parent canvas's units per canvas pixel;
        // 'created' is told of each object made, for a caller that removes them on restoring the game's look.
        public StatsSection(Transform parent, TMP_Text style, float unit, float textSize, Action<GameObject> created = null)
        {
            this.style = style;
            this.unit = unit;
            this.textSize = textSize;
            this.created = created;
            Root = NewObject(SectionName, parent);
            divider = NewObject("Divider", Root);
            Image line = divider.gameObject.AddComponent<Image>();
            line.color = Palette.Divider;
            line.raycastTarget = false;
            caption = NewText("Caption", Palette.HeaderText, TextAlignmentOptions.TopLeft);
            Root.gameObject.SetActive(false);
        }

        public bool Shown => rows > 0;

        public void Set(ItemStatsText.Parts parts)
        {
            rows = parts.Labels.Count;
            while (labels.Count < rows)
            {
                labels.Add(NewText("Label", Palette.HeaderText, TextAlignmentOptions.TopLeft));
                TMP_Text value = NewText("Value", Palette.Accent, TextAlignmentOptions.TopRight);
                value.fontStyle = FontStyles.Bold;
                values.Add(value);
            }
            for (int i = 0; i < labels.Count; i++)
            {
                bool used = i < rows;
                labels[i].gameObject.SetActive(used);
                values[i].gameObject.SetActive(used);
                labels[i].text = used ? parts.Labels[i] : "";
                values[i].text = used ? parts.Values[i] : "";
            }
            caption.text = parts.Caption;
            caption.gameObject.SetActive(parts.Caption.Length > 0);
            Root.gameObject.SetActive(Shown);
        }

        // Places the section at x, y from its parent's top left, this wide, and returns its height, the gap above it
        // included; 0 without stats.
        public float Layout(float x, float top, float width)
        {
            if (!Shown) return 0f;
            float y = GapAbove * unit;
            TopLeft(divider, 0f, y, width, DividerHeight * unit);
            y += (DividerHeight + GapBelow) * unit;
            if (caption.gameObject.activeSelf)
            {
                float height = caption.GetPreferredValues(caption.text, width, 0f).y;
                TopLeft(caption.rectTransform, 0f, y, width, height);
                y += height + RowGap * unit;
            }
            for (int i = 0; i < rows; i++)
            {
                TMP_Text value = values[i];
                float valueWidth = Mathf.Min(value.GetPreferredValues(value.text).x, width);
                float labelWidth = Mathf.Max(0f, width - valueWidth - LabelValueGap * unit);
                float height = Mathf.Max(labels[i].GetPreferredValues(labels[i].text, labelWidth, 0f).y,
                    value.GetPreferredValues(value.text, valueWidth, 0f).y);
                TopLeft(labels[i].rectTransform, 0f, y, labelWidth, height);
                TopLeft(value.rectTransform, width - valueWidth, y, valueWidth, height);
                y += height + (i < rows - 1 ? RowGap * unit : 0f);
            }
            TopLeft(Root, x, top, width, y);
            return y;
        }

        // Placed from its parent's top left corner, x across and y down.
        public static void TopLeft(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
        }

        private TMP_Text NewText(string name, Color color, TextAlignmentOptions alignment)
        {
            RectTransform rect = NewObject(name, Root);
            TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            if (style)
            {
                text.font = style.font;
                text.fontSharedMaterial = style.fontSharedMaterial;
                text.spriteAsset = style.spriteAsset;
            }
            text.fontSize = textSize;
            text.color = color;
            text.alignment = alignment;
            text.enableWordWrapping = true;
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false;
            return text;
        }

        private RectTransform NewObject(string name, Transform holder)
        {
            GameObject made = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
            made.layer = holder.gameObject.layer;
            made.transform.SetParent(holder, false);
            created?.Invoke(made);
            return (RectTransform)made.transform;
        }
    }
}
