using System.Text.RegularExpressions;
using RoR2;
using RoR2.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SevenUI
{
    // The interaction prompt on one flat panel at the HUD's right edge, with a 2px strip along its top in the color of
    // what the action costs: gold for money, red for health, the tier's color for items, and the accent when it is free.
    // Each row is a key on a small keycap, then the action ("Open chest") and its cost in bold without the game's
    // brackets; the keys share a column, so "Open chest" and "Inspect" start at the same x whether the key is E or Tab.
    // A cost in lunar coins shows the mod's flat coin before its number, as the top-left currencies do.
    // The game's prompt rows are strips as wide as the screen's right column, which only look right as gradients fading
    // out, with the key in yellow; their texts stay in place, hidden, and are copied into the panel's rows whenever they
    // change, since the prompt's words depend on what the player looks at. The panel's right edge follows the run
    // info's, wherever the game or LookingGlass (its "Move Purchase Text") puts the prompt.
    internal sealed class PromptPanel
    {
        public const string PanelName = "SevenUIPromptPanel";
        public const string StripName = "Strip";
        public const string KeyFaceName = "Face";
        // The design's sizes, in canvas pixels, scaled to the cluster's units: 14px of padding at the sides, 8px below
        // and 10px above, which holds the 2px strip; 18px text 12px past the key column, and rows 6px apart.
        private const float Canvas = HudUnits.PerCanvasPixel;
        private const float PaddingX = 14f * Canvas;
        private const float PaddingTop = 10f * Canvas;
        private const float PaddingBottom = 8f * Canvas;
        private const float StripHeight = 2f * Canvas;
        private const float TextFontSize = 18f * Canvas;
        private const float Gap = 12f * Canvas;
        private const float RowGap = 6f * Canvas;
        // A key 26px tall and at least as wide, its label 7px from its sides: an ItemSlot face with corners 3px round
        // over a darker lip showing 2px below it, and a 1px light along its top. The 14px label is centered on the face.
        private const float KeySize = 26f * Canvas;
        private const float KeyPaddingX = 7f * Canvas;
        private const float KeyLipHeight = 2f * Canvas;
        private const float KeyHighlightHeight = 1f * Canvas;
        private const float KeyRadius = 3f * Canvas;
        private const float KeyFontSize = 14f * Canvas;
        // The flat lunar coin, 14px across and 3px before the number.
        public const string CoinName = "Coin";
        private const float CoinSize = 14f * Canvas;
        private const float CoinGap = 3f * Canvas;
        // The game's lunar coin in a cost (COST_LUNARCOIN_FORMAT: " <sprite name="LunarCoin" tint=1>{0}"), with the
        // space before it.
        private static readonly Regex LunarCoinSprite = new Regex(@"\s*<sprite name=""LunarCoin""[^>]*>");
        // The game's purchase prompt puts its cost in brackets after the action (PurchaseInteraction.GetContextString).
        private static readonly Regex Cost = new Regex(@"^(?<action>.*?)\s*(<nobr>)?\((?<cost>.*)\)(</nobr>)?\s*$",
            RegexOptions.Singleline);
        private static readonly Regex StyleTags = new Regex("</?style[^>]*>");
        // A gamepad's buttons are sprites, drawn as they are with no key behind them.
        private const string SpriteTag = "<sprite";

        private sealed class Row
        {
            public TMP_Text GameKey;
            public TMP_Text GameText;
            public GameObject Display;
            public RectTransform Box;
            public GameObject Cap;
            public TMP_Text Key;
            public TMP_Text Action;
            public TMP_Text Price;
            public Image Coin;
            public bool PaysInCoins;
            public string ShownKey;
            public string ShownText;
            public bool ShownActive;
            public Color StripColor;
        }

        private readonly ContextManager prompt;
        private readonly RectTransform parent;
        private readonly RectTransform panel;
        private readonly Image strip;
        private readonly RectTransform rightEdge;
        private readonly Row[] rows;
        private readonly Vector3[] corners = new Vector3[4];
        private float shownRight = float.NaN;
        private TMP_FontAsset shownFont;

        public PromptPanel(ContextManager prompt, RectTransform rightEdge, StyleRecorder recorder)
        {
            this.prompt = prompt;
            this.rightEdge = rightEdge;
            parent = (RectTransform)prompt.transform;

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
            strip = NewImage(StripName, panel, Palette.Accent);
            RectTransform stripRect = strip.rectTransform;
            stripRect.anchorMin = new Vector2(0f, 1f);
            stripRect.anchorMax = Vector2.one;
            stripRect.pivot = new Vector2(0.5f, 1f);
            stripRect.anchoredPosition = Vector2.zero;
            stripRect.sizeDelta = new Vector2(0f, StripHeight);
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

        public Color StripColor => strip.color;

        private Row AddRow(TMP_Text gameKey, TMP_Text gameText, GameObject display, StyleRecorder recorder)
        {
            Row row = new Row { GameKey = gameKey, GameText = gameText, Display = display, StripColor = Palette.Accent };
            foreach (TMP_Text text in new[] { gameKey, gameText })
            {
                if (!text) continue;
                recorder.Record(text);
                text.alpha = 0f;
            }
            row.Box = (RectTransform)NewUIObject("Key", panel).transform;
            row.Box.anchorMin = row.Box.anchorMax = row.Box.pivot = new Vector2(0f, 1f);

            // The lip fills the key; the face covers all but its bottom, and the light runs along the face's top
            // between its round corners.
            row.Cap = NewUIObject("Cap", row.Box);
            Stretch((RectTransform)row.Cap.transform, 0f);
            Stretch(Rounded(NewImage("Lip", row.Cap.transform, Palette.KeyLip)).rectTransform, 0f);
            Image face = Rounded(NewImage(KeyFaceName, row.Cap.transform, Palette.ItemSlot));
            Stretch(face.rectTransform, KeyLipHeight);
            RectTransform light = NewImage("Light", face.transform, Palette.KeyHighlight).rectTransform;
            light.anchorMin = new Vector2(0f, 1f);
            light.anchorMax = Vector2.one;
            light.pivot = new Vector2(0.5f, 1f);
            light.anchoredPosition = Vector2.zero;
            light.sizeDelta = new Vector2(-2f * KeyRadius, KeyHighlightHeight);

            // Midline centers the label's drawn letters rather than its line, whose room for descenders would leave a
            // capital low.
            row.Key = AddText("Glyph", row.Box, gameKey, KeyFontSize, TextAlignmentOptions.Midline);
            row.Key.fontStyle = FontStyles.Bold;
            row.Action = AddText("Action", panel, gameText, TextFontSize, TextAlignmentOptions.MidlineLeft);
            row.Price = AddText("Cost", panel, gameText, TextFontSize, TextAlignmentOptions.MidlineLeft);
            row.Price.fontStyle = FontStyles.Bold;
            row.Price.color = Palette.Money;
            row.Coin = NewImage(CoinName, panel, Palette.LunarCoin);
            row.Coin.sprite = FlatSprite.LunarCoin;
            RectTransform coin = row.Coin.rectTransform;
            coin.anchorMin = coin.anchorMax = coin.pivot = new Vector2(0f, 1f);
            coin.sizeDelta = new Vector2(CoinSize, CoinSize);
            row.Coin.gameObject.SetActive(false);
            return row;
        }

        private static Image Rounded(Image image)
        {
            image.sprite = FlatSprite.Rounded;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = FlatSprite.RoundedRadius / KeyRadius;
            return image;
        }

        // Fills its parent but for 'bottom' units at the bottom.
        private static void Stretch(RectTransform rect, float bottom)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(0f, bottom);
            rect.offsetMax = Vector2.zero;
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
                MatchKeyFonts();
                changed = true;
            }
            if (changed)
            {
                Fit();
            }
        }

        // The game draws its key glyphs in a font the font swap leaves alone, so the keys take the actions' font, Barlow
        // once it is swapped, keeping their own sprites for a gamepad's buttons. Without the actions' drop shadow: the
        // label sits on the keycap's face, not over the stage.
        private void MatchKeyFonts()
        {
            foreach (Row row in rows)
            {
                if (!row.Action.font || !row.Action.fontSharedMaterial) continue;
                row.Key.font = row.Action.font;
                row.Key.fontSharedMaterial = FontLibrary.WithoutShadow(row.Action.fontSharedMaterial);
            }
        }

        private void Copy(Row row)
        {
            row.Key.text = row.ShownKey != null ? StyleTags.Replace(row.ShownKey, "") : "";
            string text = row.ShownText ?? "";
            Match match = Cost.Match(text);
            string cost = match.Success ? match.Groups["cost"].Value : "";
            row.Action.text = GameColors.Retag(match.Success ? match.Groups["action"].Value : text);
            string price = LunarCoinSprite.Replace(cost, "");
            row.Price.text = GameColors.Retag(price);
            row.StripColor = GameColors.FirstTag(cost, out Color costColor) ? costColor : PickupColor();
            row.Coin.color = row.StripColor;
            row.PaysInCoins = price.Length != cost.Length;
        }

        // The tier's color of the item or equipment the player is looking at, whose prompt ("Get Hopoo Feather") names
        // it in plain text; the accent for anything else, which costs nothing. The pickup's tier shows in the world.
        private Color PickupColor()
        {
            HUD hud = prompt.hud;
            InteractionDriver driver = hud && hud.targetBodyObject ? hud.targetBodyObject.GetComponent<InteractionDriver>() : null;
            GameObject target = driver ? driver.currentInteractable : null;
            GenericPickupController pickup = target ? target.GetComponent<GenericPickupController>() : null;
            PickupDef def = pickup ? PickupCatalog.GetPickupDef(pickup.pickup.pickupIndex) : null;
            return def != null ? GameColors.ToCanvas(def.baseColor) : Palette.Accent;
        }

        // The run info's right edge in the prompt's units, or the prompt's own right side without one.
        private float RightEdge()
        {
            if (!rightEdge || !rightEdge.gameObject.activeInHierarchy) return parent.rect.xMax - PaddingX;
            rightEdge.GetWorldCorners(corners);
            return parent.InverseTransformPoint(corners[2]).x;
        }

        // Each shown row from the top, the keys in one column as wide as the widest and the texts past it; the panel
        // around them, its right edge at the run info's and its top over the prompt's first row, and its strip in the
        // first shown row's color.
        private void Fit()
        {
            bool any = false;
            foreach (Row row in rows)
            {
                bool shown = Shown(row);
                row.Box.gameObject.SetActive(shown);
                row.Action.gameObject.SetActive(shown);
                row.Price.gameObject.SetActive(shown && row.Price.text.Length > 0);
                row.Coin.gameObject.SetActive(shown && row.PaysInCoins && row.Price.text.Length > 0);
                any |= shown;
            }
            // Before measuring: a text is set up for the canvas when it first becomes active, and measures far too
            // narrow before that.
            panel.gameObject.SetActive(any);
            if (!any) return;

            float column = 0f;
            bool stripSet = false;
            foreach (Row row in rows)
            {
                if (!Shown(row)) continue;
                bool sprite = row.Key.text.Contains(SpriteTag);
                row.Cap.SetActive(!sprite);
                // A sprite fills the key's whole height; a label is centered on the face above the lip.
                Stretch(row.Key.rectTransform, sprite ? 0f : KeyLipHeight);
                row.Box.sizeDelta = new Vector2(Mathf.Max(KeySize, row.Key.GetPreferredValues(row.Key.text).x + 2f * KeyPaddingX), KeySize);
                column = Mathf.Max(column, row.Box.sizeDelta.x);
                if (!stripSet)
                {
                    strip.color = row.StripColor;
                    stripSet = true;
                }
            }

            float y = -PaddingTop;
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
                row.Box.anchoredPosition = new Vector2(PaddingX, y);
                float x = Place(row.Action, PaddingX + column + Gap, y);
                if (row.Price.gameObject.activeSelf)
                {
                    x += Gap;
                    if (row.Coin.gameObject.activeSelf)
                    {
                        row.Coin.rectTransform.anchoredPosition = new Vector2(x, y - (KeySize - CoinSize) / 2f);
                        x += CoinSize + CoinGap;
                    }
                    x = Place(row.Price, x, y);
                }
                width = Mathf.Max(width, x);
                y -= KeySize;
            }
            panel.sizeDelta = new Vector2(width + PaddingX, -y + PaddingBottom);
            // The panel is anchored at the prompt's left middle; its first row centered on the prompt's.
            float top = KeySize / 2f + PaddingTop;
            panel.anchoredPosition = new Vector2(shownRight - parent.rect.xMin, top);
        }

        private static bool Shown(Row row) => row.ShownActive && row.Action.text.Length > 0;

        private static float Place(TMP_Text text, float x, float y)
        {
            float width = text.GetPreferredValues(text.text).x;
            text.rectTransform.anchoredPosition = new Vector2(x, y);
            text.rectTransform.sizeDelta = new Vector2(width, KeySize);
            return x + width;
        }

        private static Image NewImage(string name, Transform holder, Color color)
        {
            Image image = NewUIObject(name, holder).AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
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
