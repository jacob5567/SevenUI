using System.Collections.Generic;
using RoR2.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SevenUI
{
    // Lays the currencies out in a row from the money cluster's top-left corner, each on a panel fitted to its sign and
    // amount, as on the design canvas, in place of the game's column of strips as wide as the cluster. The game's
    // layout group is turned off and each currency placed by hand, again whenever an amount changes or a currency
    // appears.
    internal sealed class CurrencyRow
    {
        // In the money cluster's units, which match the design canvas's pixels.
        private const float PanelHeight = 40f;
        private const float PaddingX = 14f;
        private const float SignGap = 8f;
        private const float PanelGap = 8f;
        private const float AmountFontSize = 24f;
        // The dollar sign is a Barlow Condensed glyph; the coins are sprites, kept at the game's size for the amount.
        private const float DollarFontSize = 20f;
        private const string SpriteTag = "<sprite";

        private sealed class Currency
        {
            public RectTransform Rect;
            public TMP_Text Sign;
            public TMP_Text Amount;
            public string ShownAmount;
            public bool ShownActive;
        }

        private readonly RectTransform cluster;
        private readonly List<Currency> currencies = new List<Currency>();

        public CurrencyRow(HUD hud, RectTransform cluster, StyleRecorder recorder)
        {
            this.cluster = cluster;
            recorder.Disable(cluster.GetComponent<LayoutGroup>());
            foreach (MoneyText money in cluster.GetComponentsInChildren<MoneyText>(true))
            {
                if (money.transform.parent != cluster || !money.targetText) continue;
                Currency currency = new Currency { Rect = (RectTransform)money.transform, Amount = money.targetText };
                foreach (TMP_Text text in money.GetComponentsInChildren<TMP_Text>(true))
                {
                    if (text != money.targetText && text.transform.parent == money.transform)
                    {
                        currency.Sign = text;
                        break;
                    }
                }
                recorder.RecordRect(currency.Rect);
                Color? color = money == hud.moneyText ? Palette.Money : money == hud.lunarCoinText ? Palette.LunarCoin : (Color?)null;
                float gameAmountSize = currency.Amount.fontSize;
                StyleAmount(currency.Amount, color, recorder);
                StyleSign(currency.Sign, gameAmountSize, color, recorder);
                currencies.Add(currency);
            }
        }

        public bool Alive => cluster;

        // Cheap when nothing changed: an active flag and a short string per currency. The game writes the amount
        // every quarter second, so it is compared by value.
        public void Update()
        {
            bool changed = false;
            foreach (Currency currency in currencies)
            {
                if (!currency.Rect || !currency.Amount) continue;
                bool active = currency.Rect.gameObject.activeSelf;
                string amount = currency.Amount.text;
                if (active != currency.ShownActive || !string.Equals(amount, currency.ShownAmount, System.StringComparison.Ordinal))
                {
                    currency.ShownActive = active;
                    currency.ShownAmount = amount;
                    changed = true;
                }
            }
            if (changed)
            {
                Layout();
            }
        }

        private void Layout()
        {
            float x = 0f;
            foreach (Currency currency in currencies)
            {
                if (!currency.Rect || !currency.Amount || !currency.ShownActive) continue;
                float sign = currency.Sign ? currency.Sign.preferredWidth : 0f;
                float amount = currency.Amount.preferredWidth;
                if (currency.Sign)
                {
                    Place((RectTransform)currency.Sign.transform, 0f, PaddingX, sign);
                }
                Place((RectTransform)currency.Amount.transform, 1f, -PaddingX, amount);
                float width = PaddingX + sign + SignGap + amount + PaddingX;
                currency.Rect.anchorMin = currency.Rect.anchorMax = currency.Rect.pivot = new Vector2(0f, 1f);
                currency.Rect.anchoredPosition = new Vector2(x, 0f);
                currency.Rect.sizeDelta = new Vector2(width, PanelHeight);
                x += width + PanelGap;
            }
        }

        // A text as tall as its panel and as wide as its text, against the panel's left (0) or right (1) side.
        private static void Place(RectTransform text, float side, float offset, float width)
        {
            text.anchorMin = new Vector2(side, 0f);
            text.anchorMax = new Vector2(side, 1f);
            text.pivot = new Vector2(side, 0.5f);
            text.anchoredPosition = new Vector2(offset, 0f);
            text.sizeDelta = new Vector2(width, 0f);
        }

        // A text exactly as wide as its preferred width could otherwise wrap.
        private static void KeepOnOneLine(TMP_Text text, StyleRecorder recorder)
        {
            bool wrapping = text.enableWordWrapping;
            recorder.OnRestore(() =>
            {
                if (!text) return;
                text.enableWordWrapping = wrapping;
            });
            text.enableWordWrapping = false;
        }

        private static void StyleAmount(TMP_Text amount, Color? color, StyleRecorder recorder)
        {
            recorder.Record(amount);
            recorder.RecordRect((RectTransform)amount.transform);
            amount.fontSize = AmountFontSize;
            amount.fontStyle |= FontStyles.Bold;
            amount.alignment = TextAlignmentOptions.Right;
            amount.margin = Vector4.zero;
            KeepOnOneLine(amount, recorder);
            if (color.HasValue)
            {
                amount.color = color.Value;
            }
        }

        // A coin keeps its size against the amount, which the game draws at gameAmountSize.
        private static void StyleSign(TMP_Text sign, float gameAmountSize, Color? color, StyleRecorder recorder)
        {
            if (!sign) return;
            recorder.Record(sign);
            recorder.RecordRect((RectTransform)sign.transform);
            sign.fontSize = sign.text.Contains(SpriteTag) && gameAmountSize > 0f
                ? sign.fontSize * AmountFontSize / gameAmountSize
                : DollarFontSize;
            sign.alignment = TextAlignmentOptions.Left;
            sign.margin = Vector4.zero;
            KeepOnOneLine(sign, recorder);
            if (color.HasValue)
            {
                sign.color = color.Value;
            }
        }
    }
}
