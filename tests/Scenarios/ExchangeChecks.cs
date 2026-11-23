using System.Linq;
using RoR2;
using TMPro;
using UnityEngine;

namespace SevenUI.Tests
{
    // The scrappers' exchange strip (J2 design E), shared by the item scrapper's and the Drone Scrapper's checks.
    internal static class ExchangeChecks
    {
        private const float Tolerance = 1f;

        // Null when the strip spans the panel's bottom edge to edge and trades the option the details show for the
        // scrap the game would give, the game's inspect panel hidden; otherwise what differs.
        internal static string Problem(PickerPanel panel)
        {
            if (!panel.Alive) return "the panel closed";
            PickerExchange exchange = panel.Exchange;
            RectTransform strip = exchange.Strip;
            if (!strip || !strip.gameObject.activeInHierarchy) return "the exchange strip is not showing";
            Transform inspect = panel.Component.GetComponentsInChildren<Transform>(true).FirstOrDefault(part => part.name == "InspectPanel");
            if (inspect && inspect.gameObject.activeInHierarchy) return "the game's inspect panel is still showing beside the strip";
            if (panel.DetailsNameText && panel.DetailsNameText.isActiveAndEnabled) return "the details still show the option's name, which the strip shows";
            RectTransform main = panel.MainPanel;
            Rect stripRect = RectIn(main, strip);
            Rect mainRect = main.rect;
            if (Mathf.Abs(stripRect.xMin - mainRect.xMin) > Tolerance || Mathf.Abs(stripRect.xMax - mainRect.xMax) > Tolerance)
                return $"the strip runs {stripRect.xMin:0.#} to {stripRect.xMax:0.#}, not the panel's {mainRect.xMin:0.#} to {mainRect.xMax:0.#}";
            if (Mathf.Abs(stripRect.yMin - mainRect.yMin) > Tolerance) return $"the strip ends at {stripRect.yMin:0.#}, not the panel's bottom {mainRect.yMin:0.#}";
            if (Mathf.Abs(stripRect.height - PickerExchange.Height) > Tolerance) return $"the strip is {stripRect.height:0.#} tall, not {PickerExchange.Height:0.#}";
            int index = panel.DetailsIndex;
            if (index < 0 || index >= panel.Buttons.Count) return "the details show no option";
            PickerEntry entry = panel.Buttons[index].Entry;
            Exchange expected = panel.Source.ExchangeOf(index);
            if (entry == null || expected == null || expected.Scrap == null) return $"option {index} has no scrap to give";
            string name = exchange.NameText.text;
            if (!name.Contains(entry.Name)) return $"the strip names \"{name}\", not option {index}'s {entry.Name}";
            if (!SevenUIChecks.Near(exchange.NameText.color, entry.Color)) return $"the strip's name is {exchange.NameText.color}, not the tier's {entry.Color}";
            Sprite scrapIcon = PickupIcons.Get(expected.Scrap);
            if (!scrapIcon) return $"the {expected.Scrap.internalName} icon hasn't loaded";
            if (!exchange.ScrapIconImage.enabled || exchange.ScrapIconImage.sprite != scrapIcon) return $"the strip shows {(exchange.ScrapIconImage.sprite ? exchange.ScrapIconImage.sprite.name : "no scrap")}, not {expected.Scrap.internalName}";
            string scrapName = Language.GetString(expected.Scrap.nameToken);
            if (exchange.ScrapNameText.text != scrapName) return $"the strip's scrap is \"{exchange.ScrapNameText.text}\", not \"{scrapName}\"";
            string count = "×" + expected.ScrapCount;
            if (exchange.ScrapCountText.text != count) return $"the strip gives \"{exchange.ScrapCountText.text}\", not \"{count}\"";
            if (!exchange.OptionIcon.enabled || !exchange.OptionIcon.sprite) return "the strip's slot shows no icon";
            // Every part inside the strip, the option's name clear of the scrap's side.
            foreach (TMP_Text text in new[] { exchange.NameText, exchange.SubText, exchange.ScrapNameText, exchange.ScrapCountText })
            {
                if (string.IsNullOrEmpty(text.text)) continue;
                Rect rect = RectIn(strip, text.rectTransform);
                if (rect.xMin < strip.rect.xMin - Tolerance || rect.xMax > strip.rect.xMax + Tolerance ||
                    rect.yMin < strip.rect.yMin - Tolerance || rect.yMax > strip.rect.yMax + Tolerance)
                    return $"the strip's {text.name} {rect} runs outside it {strip.rect}";
            }
            Rect nameRect = RectIn(strip, exchange.NameText.rectTransform);
            Rect iconRect = RectIn(strip, (RectTransform)exchange.ScrapIconImage.transform);
            if (nameRect.xMax > iconRect.xMin) return "the option's name runs into the scrap's side";
            return null;
        }

        private static Rect RectIn(RectTransform root, RectTransform rect)
        {
            Vector3[] corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Vector3 min = root.InverseTransformPoint(corners[0]);
            Vector3 max = root.InverseTransformPoint(corners[2]);
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }
    }
}
