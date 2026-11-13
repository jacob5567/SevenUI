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
    // appears. The lunar coins' sign is the mod's flat coin in place of the game's shaded one.
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
        // The flat lunar coin, 16px across, as tall as the dollar sign's figures.
        public const string LunarCoinName = "SevenUILunarCoin";
        private const float CoinSize = 16f;

        private sealed class Currency
        {
            public RectTransform Rect;
            public TMP_Text Sign;
            public RectTransform Coin;
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
                if (money == hud.lunarCoinText)
                {
                    currency.Coin = AddCoin(currency, recorder);
                }
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
                float sign = currency.Coin ? CoinSize : currency.Sign ? currency.Sign.preferredWidth : 0f;
                float amount = currency.Amount.preferredWidth;
                if (currency.Sign && !currency.Coin)
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

        // The flat coin at the panel's left, centered up and down, with the game's coin sprite hidden.
        private static RectTransform AddCoin(Currency currency, StyleRecorder recorder)
        {
            if (currency.Sign)
            {
                currency.Sign.alpha = 0f;
            }
            RectTransform coin = UiFactory.NewObject(LunarCoinName, currency.Rect);
            GameObject created = coin.gameObject;
            coin.anchorMin = coin.anchorMax = coin.pivot = new Vector2(0f, 0.5f);
            coin.anchoredPosition = new Vector2(PaddingX, 0f);
            coin.sizeDelta = new Vector2(CoinSize, CoinSize);
            Image image = created.AddComponent<Image>();
            image.sprite = FlatSprite.LunarCoin;
            image.color = Palette.LunarCoin;
            image.raycastTarget = false;
            recorder.Add(created);
            return coin;
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

        private static void StyleAmount(TMP_Text amount, Color? color, StyleRecorder recorder)
        {
            recorder.Record(amount);
            recorder.RecordRect((RectTransform)amount.transform);
            amount.fontSize = AmountFontSize;
            amount.fontStyle |= FontStyles.Bold;
            // Midline centers the drawn figures on the panel; centering the line would leave room under them for
            // descenders figures don't have, and draw them low.
            amount.alignment = TextAlignmentOptions.MidlineRight;
            amount.margin = Vector4.zero;
            UiFactory.KeepOnOneLine(amount, recorder);
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
            sign.alignment = TextAlignmentOptions.MidlineLeft;
            sign.margin = Vector4.zero;
            UiFactory.KeepOnOneLine(sign, recorder);
            if (color.HasValue)
            {
                sign.color = color.Value;
            }
        }
    }
}
