using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ModTestRunner;
using RoR2;
using RoR2.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SevenUI.Tests
{
    // Checks that parts of the HUD sit and size as on the design canvas: the currencies in a row, each on a panel
    // fitted to its amount. Measured in the HUD's own units, so they hold at any resolution.
    internal static class CanvasLayoutChecks
    {
        // In the money cluster's units.
        private const float RowTolerance = 0.5f;

        private const string SpringCanvasPath = "MainContainer/MainUIArea/SpringCanvas";
        private const string UpperLeftPath = SpringCanvasPath + "/UpperLeftCluster";

        private static Bounds In(RectTransform space, RectTransform rect)
        {
            Vector3[] corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Bounds bounds = new Bounds(space.InverseTransformPoint(corners[0]), Vector3.zero);
            foreach (Vector3 corner in corners)
            {
                bounds.Encapsulate(space.InverseTransformPoint(corner));
            }
            return bounds;
        }

        private static HUD Hud(Harness h) => SevenUIChecks.HudRoot(h).GetComponent<HUD>();

        // The shown currencies left to right, in the money cluster's units.
        private static List<(MoneyText money, Bounds panel, Bounds amount)> Currencies(Harness h)
        {
            HUD hud = Hud(h);
            RectTransform cluster = SevenUIChecks.HudRoot(h).Find(UpperLeftPath) as RectTransform;
            if (!cluster) return new List<(MoneyText, Bounds, Bounds)>();
            return new[] { hud.moneyText, hud.voidCoinText, hud.lunarCoinText }
                .Where(money => money && money.gameObject.activeInHierarchy && money.targetText)
                .Select(money => (money, In(cluster, (RectTransform)money.transform), AmountBounds(cluster, money.targetText)))
                .OrderBy(c => c.Item2.min.x).ToList();
        }

        // The amount's drawn characters, not its box.
        private static Bounds AmountBounds(RectTransform cluster, TMP_Text amount)
        {
            Bounds local = amount.textBounds;
            Bounds bounds = new Bounds(cluster.InverseTransformPoint(amount.transform.TransformPoint(local.min)), Vector3.zero);
            bounds.Encapsulate(cluster.InverseTransformPoint(amount.transform.TransformPoint(local.max)));
            return bounds;
        }

        private static string CurrencyLook(List<(MoneyText money, Bounds panel, Bounds amount)> currencies) =>
            string.Join(", ", currencies.Select(c =>
                $"{c.money.name} panel x {c.panel.min.x:0.#}-{c.panel.max.x:0.#} top {c.panel.max.y:0.#}, " +
                $"amount x {c.amount.min.x:0.#}-{c.amount.max.x:0.#}, color {c.money.targetText.color}"));

        // Side by side with tops level, each panel holding its amount, and the money and lunar coins in the canvas's
        // colors.
        public static IEnumerator ExpectCurrenciesInRow(this Harness h)
        {
            HUD hud = Hud(h);
            List<(MoneyText money, Bounds panel, Bounds amount)> currencies = null;
            yield return h.Until(
                () =>
                {
                    currencies = Currencies(h);
                    if (currencies.Count == 0) return false;
                    for (int i = 0; i < currencies.Count; i++)
                    {
                        Bounds panel = currencies[i].panel;
                        Bounds amount = currencies[i].amount;
                        if (Mathf.Abs(panel.max.y - currencies[0].panel.max.y) > RowTolerance) return false;
                        if (i > 0 && panel.min.x < currencies[i - 1].panel.max.x) return false;
                        if (amount.min.x < panel.min.x || amount.max.x > panel.max.x + RowTolerance) return false;
                    }
                    return SevenUIChecks.Near(hud.moneyText.targetText.color, Palette.Money) &&
                           (!hud.lunarCoinText || SevenUIChecks.Near(hud.lunarCoinText.targetText.color, Palette.LunarCoin));
                },
                () => $"the currencies in a row on fitted panels ({CurrencyLook(currencies)}) {h.Diagnosis()}");
        }

    }
}
