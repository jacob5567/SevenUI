using System.Text.RegularExpressions;
using RoR2;
using RoR2.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SevenUI
{
    // The interaction prompt as on the design canvas: on one flat panel at the HUD's right edge, the key in a thin
    // outlined box, then the action ("Open Chest") and its cost in bold, "$25" in the canvas's gold, without the game's
    // brackets. The Inspect prompt, when the game shows it, is a second row in the same style. The game's prompt rows
    // are strips as wide as the screen's right column, which only look right as gradients fading out, with the key in
    // yellow; their texts stay in place, hidden, and are copied into the panel's rows whenever they change, since the
    // prompt's words depend on what the player looks at. The panel's right edge follows the run info's, wherever the
    // game or LookingGlass (its "Move Purchase Text") puts the prompt.
    internal sealed class PromptPanel
    {
        public const string PanelName = "SevenUIPromptPanel";
        // The design canvas's sizes, in its pixels, scaled to the cluster's units: 8px by 14px of padding, 26px boxes
        // with a 1px edge around 14px keys, 18px text 12px apart, and rows 6px apart.
        private const float Canvas = HudUnits.PerCanvasPixel;
        private const float PaddingX = 14f * Canvas;
        private const float PaddingY = 8f * Canvas;
        private const float BoxSize = 26f * Canvas;
        private const float BoxEdge = 1f * Canvas;
        private const float KeyInset = 6f * Canvas;
        private const float KeyFontSize = 14f * Canvas;
        private const float TextFontSize = 18f * Canvas;
        private const float Gap = 12f * Canvas;
        private const float RowGap = 6f * Canvas;
        // The outline sprite's edge, in its pixels (FlatSprite.Outline).
        private const float OutlineEdgePixels = 2f;
        // The game's purchase prompt puts its cost in brackets after the action (PurchaseInteraction.GetContextString).
        private static readonly Regex Cost = new Regex(@"^(?<action>.*?)\s*(<nobr>)?\((?<cost>.*)\)(</nobr>)?\s*$",
            RegexOptions.Singleline);
        private static readonly Regex StyleTags = new Regex("</?style[^>]*>");

        private sealed class Row
        {
            public TMP_Text GameKey;
            public TMP_Text GameText;
            public GameObject Display;
            public RectTransform Box;
            public TMP_Text Key;
            public TMP_Text Action;
            public TMP_Text Price;
            public string ShownKey;
            public string ShownText;
            public bool ShownActive;
        }

        private readonly ContextManager prompt;
        private readonly RectTransform parent;
        private readonly RectTransform panel;
        private readonly RectTransform rightEdge;
        private readonly Row[] rows;
        private readonly Vector3[] corners = new Vector3[4];
        private readonly string gameMoneyColor;
        private readonly string moneyColor;
        private float shownRight = float.NaN;
        private TMP_FontAsset shownFont;

        public PromptPanel(ContextManager prompt, RectTransform rightEdge, StyleRecorder recorder)
        {
            this.prompt = prompt;
            this.rightEdge = rightEdge;
            parent = (RectTransform)prompt.transform;
            gameMoneyColor = "<color=#" + ColorCatalog.GetColorHexString(ColorCatalog.ColorIndex.Money) + ">";
            moneyColor = "<color=#" + ColorUtility.ToHtmlStringRGB(Palette.Money) + ">";

            GameObject created = NewUIObject(PanelName, parent);
            created.AddComponent<LayoutIgnorer>();
            panel = (RectTransform)created.transform;
            // First, so the game's rows, hidden, stay over it.
            panel.SetAsFirstSibling();
            panel.anchorMin = panel.anchorMax = new Vector2(0f, 0.5f);
            panel.pivot = Vector2.one;
            Image image = created.AddComponent<Image>();
            image.color = Palette.Panel;
            image.raycastTarget = false;
            created.SetActive(false);
            recorder.Add(created);

            rows = new[]
            {
                AddRow(prompt.glyphTMP, prompt.descriptionTMP, prompt.contextDisplay, recorder),
                AddRow(prompt.inspectGlyphTMP, prompt.inspectDescriptionTMP, prompt.inspectDisplay, recorder),
            };
        }

        public bool Alive => prompt && panel;

        public ContextManager Prompt => prompt;

        public RectTransform Panel => panel;

        public string ShownAction => rows[0].Action.text;

        public string ShownPrice => rows[0].Price.text;

        private Row AddRow(TMP_Text gameKey, TMP_Text gameText, GameObject display, StyleRecorder recorder)
        {
            Row row = new Row { GameKey = gameKey, GameText = gameText, Display = display };
            foreach (TMP_Text text in new[] { gameKey, gameText })
            {
                if (!text) continue;
                recorder.Record(text);
                text.alpha = 0f;
            }
            GameObject box = NewUIObject("Key", panel);
            row.Box = (RectTransform)box.transform;
            row.Box.anchorMin = row.Box.anchorMax = row.Box.pivot = new Vector2(0f, 1f);
            Image outline = box.AddComponent<Image>();
            outline.sprite = FlatSprite.Outline;
            outline.type = Image.Type.Sliced;
            outline.pixelsPerUnitMultiplier = OutlineEdgePixels / BoxEdge;
            outline.color = Palette.HeaderText;
            outline.raycastTarget = false;
            row.Key = AddText("Glyph", box.transform, gameKey, KeyFontSize, TextAlignmentOptions.Center);
            row.Key.fontStyle = FontStyles.Bold;
            RectTransform keyRect = row.Key.rectTransform;
            keyRect.anchorMin = Vector2.zero;
            keyRect.anchorMax = Vector2.one;
            keyRect.offsetMin = keyRect.offsetMax = Vector2.zero;
            row.Action = AddText("Action", panel, gameText, TextFontSize, TextAlignmentOptions.MidlineLeft);
            row.Price = AddText("Cost", panel, gameText, TextFontSize, TextAlignmentOptions.MidlineLeft);
            row.Price.fontStyle = FontStyles.Bold;
            row.Price.color = Palette.Money;
            return row;
        }

        // In the game text's font, so the font swap gives it Barlow like the rest of the HUD, and with its sprites for
        // a gamepad's buttons.
        private static TMP_Text AddText(string name, Transform holder, TMP_Text like, float size, TextAlignmentOptions alignment)
        {
            GameObject line = NewUIObject(name, holder);
            TextMeshProUGUI text = line.AddComponent<TextMeshProUGUI>();
            if (like)
            {
                text.font = like.font;
                text.fontSharedMaterial = like.fontSharedMaterial;
                text.spriteAsset = like.spriteAsset;
            }
            text.fontSize = size;
            text.color = Palette.Text;
            text.alignment = alignment;
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false;
            RectTransform rect = (RectTransform)line.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            return text;
        }

        // Cheap when nothing changed: two active flags, four short strings, which the game builds anew every frame
        // while the player looks at something and so are compared by value, a font and one point of the run info.
        // The rows are laid out again only after something changed.
        public void Update()
        {
            if (!Alive) return;
            bool changed = false;
            foreach (Row row in rows)
            {
                bool active = row.Display && row.Display.activeInHierarchy;
                string key = row.GameKey ? row.GameKey.text : null;
                string text = row.GameText ? row.GameText.text : null;
                if (active == row.ShownActive && string.Equals(key, row.ShownKey, System.StringComparison.Ordinal) &&
                    string.Equals(text, row.ShownText, System.StringComparison.Ordinal)) continue;
                row.ShownActive = active;
                row.ShownKey = key;
                row.ShownText = text;
                Copy(row);
                changed = true;
            }
            float right = RightEdge();
            if (rows[0].Action.font != shownFont || !Mathf.Approximately(right, shownRight))
            {
                shownFont = rows[0].Action.font;
                shownRight = right;
                changed = true;
            }
            if (changed)
            {
                Fit();
            }
        }

        private void Copy(Row row)
        {
            row.Key.text = row.ShownKey != null ? StyleTags.Replace(row.ShownKey, "") : "";
            string text = row.ShownText ?? "";
            Match match = Cost.Match(text);
            if (match.Success)
            {
                row.Action.text = match.Groups["action"].Value;
                row.Price.text = match.Groups["cost"].Value.Replace(gameMoneyColor, moneyColor);
            }
            else
            {
                row.Action.text = text;
                row.Price.text = "";
            }
        }

        // The run info's right edge in the prompt's units, or the prompt's own right side without one.
        private float RightEdge()
        {
            if (!rightEdge || !rightEdge.gameObject.activeInHierarchy) return parent.rect.xMax - PaddingX;
            rightEdge.GetWorldCorners(corners);
            return parent.InverseTransformPoint(corners[2]).x;
        }

        // Each shown row from the top, its parts left to right; the panel around them, its right edge at the run
        // info's and its top over the prompt's first row.
        private void Fit()
        {
            bool any = false;
            foreach (Row row in rows)
            {
                bool shown = Shown(row);
                row.Box.gameObject.SetActive(shown);
                row.Action.gameObject.SetActive(shown);
                row.Price.gameObject.SetActive(shown && row.Price.text.Length > 0);
                any |= shown;
            }
            // Before measuring: a text is set up for the canvas when it first becomes active, and measures far too
            // narrow before that.
            panel.gameObject.SetActive(any);
            if (!any) return;
            float y = -PaddingY;
            float width = 0f;
            bool first = true;
            foreach (Row row in rows)
            {
                if (!Shown(row)) continue;
                if (!first)
                {
                    y -= RowGap;
                }
                first = false;
                float keyWidth = Mathf.Max(BoxSize, row.Key.GetPreferredValues(row.Key.text).x + 2f * KeyInset);
                float x = PaddingX;
                row.Box.anchoredPosition = new Vector2(x, y);
                row.Box.sizeDelta = new Vector2(keyWidth, BoxSize);
                x += keyWidth + Gap;
                x = Place(row.Action, x, y);
                if (row.Price.gameObject.activeSelf)
                {
                    x = Place(row.Price, x + Gap, y);
                }
                width = Mathf.Max(width, x);
                y -= BoxSize;
            }
            Vector2 size = new Vector2(width + PaddingX, -y + PaddingY);
            panel.sizeDelta = size;
            // The panel is anchored at the prompt's left middle; its first row centered on the prompt's.
            float top = BoxSize / 2f + PaddingY;
            panel.anchoredPosition = new Vector2(shownRight - parent.rect.xMin, top);
        }

        private static bool Shown(Row row) => row.ShownActive && row.Action.text.Length > 0;

        private static float Place(TMP_Text text, float x, float y)
        {
            float width = text.GetPreferredValues(text.text).x;
            text.rectTransform.anchoredPosition = new Vector2(x, y);
            text.rectTransform.sizeDelta = new Vector2(width, BoxSize);
            return x + width;
        }

        private static GameObject NewUIObject(string name, Transform holder)
        {
            GameObject created = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
            created.layer = holder.gameObject.layer;
            created.transform.SetParent(holder, false);
            return created;
        }
    }
}
