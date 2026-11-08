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
    // The ally cards' stack against the chat, measured from the game's own objects in the cards' cluster's units: the
    // room runs from the cluster's top down to the chat's message area, less the mod's gap, and never past the
    // cluster's bottom, where the game would start squeezing the cards together.
    internal static class AllyCardChecks
    {
        private const string ClusterPath = "MainContainer/MainUIArea/SpringCanvas/LeftCluster";
        // As the mod keeps it, in the HUD's units.
        private const float ChatGap = 8f * HudUnits.PerCanvasPixel;
        private const float Tolerance = 0.5f;
        private static readonly Vector3[] Corners = new Vector3[4];

        public static RectTransform Cluster(Harness h) => SevenUIChecks.HudRoot(h).Find(ClusterPath) as RectTransform;

        // In the game's order, top to bottom.
        public static List<AllyCardController> Cards(Harness h)
        {
            RectTransform cluster = Cluster(h);
            return cluster ? cluster.GetComponentsInChildren<AllyCardController>(true).ToList() : new List<AllyCardController>();
        }

        // A card's height at the game's size, from the first card, while there are few enough to show at it.
        public static float FullHeight(Harness h)
        {
            AllyCardController card = Cards(h).FirstOrDefault();
            return card && card.layoutElement ? card.layoutElement.preferredHeight : 0f;
        }

        public static float Room(Harness h)
        {
            RectTransform cluster = Cluster(h);
            ChatBox box = SevenUIChecks.HudRoot(h).GetComponentInChildren<ChatBox>(true);
            RectTransform chat = box ? box.standardChatboxRect : null;
            if (!chat) return cluster.rect.height;
            chat.GetWorldCorners(Corners);
            float chatTop = cluster.InverseTransformPoint(Corners[1]).y;
            return Mathf.Min(cluster.rect.height, cluster.rect.yMax - chatTop - ChatGap);
        }

        // How many cards of that height fit in the room.
        public static int Fitting(Harness h, float height) => Mathf.FloorToInt(Room(h) / height + 0.01f);

        // Null when every card shows at that height, none reaching past the room, and the more row doesn't show.
        public static string AllShown(Harness h, float height, int count)
        {
            List<AllyCardController> cards = Cards(h);
            if (cards.Count != count) return $"{cards.Count} ally cards, not {count}";
            RectTransform cluster = Cluster(h);
            float room = Room(h);
            foreach (AllyCardController card in cards)
            {
                if (!card.gameObject.activeSelf) return $"{Name(card)}'s card is hidden";
                float tall = card.rectTransform.rect.height;
                if (Mathf.Abs(tall - height) > Tolerance) return $"{Name(card)}'s card is {tall:0.#} units tall, not {height:0.#}";
                float depth = Depth(cluster, card.rectTransform);
                if (depth > room + Tolerance)
                {
                    return $"{Name(card)}'s card reaches {depth:0.#} units down, past the {room:0.#} above the chat";
                }
            }
            RectTransform more = More(h);
            if (more && more.gameObject.activeSelf) return "the more row shows";
            return null;
        }

        // Null when the compact rows that fit above the chat show, with the more row under them standing in for the
        // rest: "+N more" and a portrait for each kind hidden, in the order they come.
        public static string Overflowing(Harness h, int count)
        {
            List<AllyCardController> cards = Cards(h);
            if (cards.Count != count) return $"{cards.Count} ally cards, not {count}";
            RectTransform cluster = Cluster(h);
            float room = Room(h);
            float row = AllyCompactCard.RowHeight;
            int shown = Mathf.Max(0, Mathf.FloorToInt((room - row) / row + 0.01f));
            int hidden = cards.Count - shown;
            if (hidden <= 0) return $"all {cards.Count} cards fit as rows in {room:0.#} units";
            for (int i = 0; i < cards.Count; i++)
            {
                AllyCardController card = cards[i];
                bool expected = i < shown;
                if (card.gameObject.activeSelf != expected) return $"card {i + 1} of {cards.Count} is {(expected ? "hidden" : "shown")}";
                if (!expected) continue;
                float tall = card.rectTransform.rect.height;
                if (Mathf.Abs(tall - row) > Tolerance) return $"{Name(card)}'s row is {tall:0.#} units tall, not {row:0.#}";
            }
            RectTransform more = More(h);
            if (!more || !more.gameObject.activeSelf) return $"no more row for the {hidden} cards hidden";
            float moreTop = Depth(cluster, more) - more.rect.height;
            if (Mathf.Abs(moreTop - shown * row) > Tolerance) return $"the more row starts {moreTop:0.#} units down, not {shown * row:0.#}";
            float moreDepth = Depth(cluster, more);
            if (moreDepth > room + Tolerance) return $"the more row reaches {moreDepth:0.#} units down, past the {room:0.#} above the chat";
            TMP_Text label = more.GetComponentInChildren<TMP_Text>(true);
            string text = $"+{hidden} more";
            if (!label || label.text != text) return $"the more row reads '{(label ? label.text : "")}', not '{text}'";
            List<Texture> kinds = new List<Texture>();
            foreach (AllyCardController card in cards.Skip(shown))
            {
                CharacterBody body = card.sourceMaster ? card.sourceMaster.GetBody() : null;
                if (body && body.portraitIcon && !kinds.Contains(body.portraitIcon) && kinds.Count < AllyMoreRow.MaxIcons)
                {
                    kinds.Add(body.portraitIcon);
                }
            }
            List<Texture> icons = more.GetComponentsInChildren<RawImage>()
                .Where(icon => icon.name == AllyMoreRow.IconName).Select(icon => icon.texture).ToList();
            if (!icons.SequenceEqual(kinds)) return $"the more row shows {icons.Count} portraits, not the {kinds.Count} kinds hidden";
            return null;
        }

        // Null when every card shows at the game's size again and the mod's more row is gone.
        public static string GameLook(Harness h, int count, float full)
        {
            List<AllyCardController> cards = Cards(h);
            if (cards.Count != count) return $"{cards.Count} ally cards, not {count}";
            foreach (AllyCardController card in cards)
            {
                if (!card.gameObject.activeSelf) return $"{Name(card)}'s card is hidden";
                float tall = card.layoutElement ? card.layoutElement.preferredHeight : 0f;
                if (Mathf.Abs(tall - full) > Tolerance) return $"{Name(card)}'s card asks for {tall:0.#} units, not {full:0.#}";
            }
            return More(h) ? "the more row is still there" : null;
        }

        private static RectTransform More(Harness h)
        {
            AllyCardManager manager = SevenUIChecks.HudRoot(h).GetComponentInChildren<AllyCardManager>(true);
            return manager ? manager.transform.Find(AllyMoreRow.Name) as RectTransform : null;
        }

        // How far below the cluster's top the rect ends, in the cluster's units.
        private static float Depth(RectTransform cluster, RectTransform rect)
        {
            rect.GetWorldCorners(Corners);
            return cluster.rect.yMax - cluster.InverseTransformPoint(Corners[0]).y;
        }

        private static string Name(AllyCardController card) => card.nameLabel ? card.nameLabel.text : card.name;
    }
}
