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
    // The ally cards' stack against the chat, worked out from the game's own objects and compared with what shows. The
    // room runs from the cards' cluster's top down to the chat's message area, less the mod's gap, and never past the
    // cluster's bottom. Allies of one name following one leader share a card (players and allies between bodies keep
    // their own); while every card at the game's height doesn't fit, the cards of a single ally are compact rows; if
    // that still doesn't fit, the cards that don't go under a "+N more" row.
    internal static class AllyCardChecks
    {
        private const string ClusterPath = "MainContainer/MainUIArea/SpringCanvas/LeftCluster";
        // The game's ally card (Prefabs/UI/AllyCard) asks for this height.
        public const float GameHeight = 48f;
        // As the mod keeps it, in the HUD's units.
        private const float ChatGap = 8f * HudUnits.PerCanvasPixel;
        private const float Tolerance = 0.5f;
        private static readonly Vector3[] Corners = new Vector3[4];

        // What the cards show, and the first difference from what they should, if any.
        internal sealed class Layout
        {
            public string Problem;
            public int Cards;
            public int Kinds;
            public bool Compact;
            public int HiddenAllies;
        }

        private sealed class Kind
        {
            public AllyCardController Lead;
            public string Name;
            public CharacterMaster Leader;
            public int Allies;
        }

        public static RectTransform Cluster(Harness h) => SevenUIChecks.HudRoot(h).Find(ClusterPath) as RectTransform;

        // In the game's order, top to bottom.
        public static List<AllyCardController> Cards(Harness h)
        {
            RectTransform cluster = Cluster(h);
            return cluster ? cluster.GetComponentsInChildren<AllyCardController>(true).ToList() : new List<AllyCardController>();
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

        public static Layout Measure(Harness h)
        {
            List<AllyCardController> cards = Cards(h);
            Layout layout = new Layout { Cards = cards.Count };
            RectTransform cluster = Cluster(h);
            float room = Room(h);
            float row = AllyCompactCard.RowHeight;
            List<Kind> kinds = new List<Kind>();
            HashSet<AllyCardController> sharing = new HashSet<AllyCardController>();
            foreach (AllyCardController card in cards)
            {
                CharacterMaster master = card.sourceMaster;
                CharacterBody body = master ? master.GetBody() : null;
                string name = body && !master.playerCharacterMasterController ? Util.GetBestBodyName(body.gameObject) : null;
                CharacterMaster leader = master && master.minionOwnership ? master.minionOwnership.ownerMaster : null;
                Kind kind = name == null ? null : kinds.FirstOrDefault(found => found.Name == name && found.Leader == leader);
                if (kind == null)
                {
                    kinds.Add(new Kind { Lead = card, Name = name, Leader = leader, Allies = master ? 1 : 0 });
                }
                else
                {
                    kind.Allies++;
                    sharing.Add(card);
                }
            }
            layout.Kinds = kinds.Count;
            bool compact = kinds.Count * GameHeight > room + Tolerance;
            float Height(Kind kind) => compact && kind.Allies <= 1 ? row : GameHeight;
            int shown = kinds.Count;
            float used = kinds.Sum(Height);
            if (used > room + Tolerance)
            {
                shown = 0;
                used = 0f;
                while (shown < kinds.Count && used + Height(kinds[shown]) + row <= room + Tolerance)
                {
                    used += Height(kinds[shown++]);
                }
            }
            layout.Compact = compact;
            layout.HiddenAllies = kinds.Skip(shown).Sum(kind => Mathf.Max(1, kind.Allies));
            layout.Problem = Compare(cluster, room, kinds, sharing, shown, used, compact, cards, h);
            return layout;
        }

        private static string Compare(RectTransform cluster, float room, List<Kind> kinds, HashSet<AllyCardController> sharing,
            int shown, float used, bool compact, List<AllyCardController> cards, Harness h)
        {
            foreach (AllyCardController card in sharing)
            {
                if (card.gameObject.activeSelf) return $"a second {Name(card)} has a card of its own";
            }
            string edges = CompareEdges(cluster, kinds, compact, cards, More(h));
            if (edges != null) return edges;
            for (int i = 0; i < kinds.Count; i++)
            {
                Kind kind = kinds[i];
                AllyCardController card = kind.Lead;
                bool fits = i < shown;
                if (card.gameObject.activeSelf != fits) return $"{Name(card)}'s card is {(fits ? "hidden" : "shown")} (kind {i + 1} of {kinds.Count}, {shown} fit)";
                if (!fits) continue;
                float height = compact && kind.Allies <= 1 ? AllyCompactCard.RowHeight : GameHeight;
                float tall = card.rectTransform.rect.height;
                if (Mathf.Abs(tall - height) > Tolerance) return $"{Name(card)}'s card is {tall:0.#} units tall, not {height:0.#}";
                float depth = Depth(cluster, card.rectTransform);
                if (depth > room + Tolerance) return $"{Name(card)}'s card reaches {depth:0.#} units down, past the {room:0.#} above the chat";
                TMP_Text count = card.nameLabel ? card.nameLabel.transform.Find(AllyKindParts.CountName)?.GetComponent<TMP_Text>() : null;
                if (kind.Allies > 1)
                {
                    if (!count || count.text != "×" + kind.Allies) return $"{Name(card)}'s card reads '{(count ? count.text : "")}', not ×{kind.Allies}";
                    Transform segments = card.healthBar ? card.healthBar.transform.parent.Find(AllyKindParts.SegmentsName) : null;
                    int bars = segments ? segments.Cast<Transform>().Count(segment => segment.gameObject.activeSelf) : 0;
                    if (bars != kind.Allies) return $"{Name(card)}'s card has {bars} bar segments, not {kind.Allies}";
                    if (card.healthBar.gameObject.activeSelf) return $"{Name(card)}'s card still shows the game's bar";
                }
                else if (count && count.gameObject.activeInHierarchy)
                {
                    return $"{Name(card)}'s card of one ally shows a count";
                }
            }

            RectTransform more = More(h);
            if (shown == kinds.Count) return more && more.gameObject.activeSelf ? "the more row shows with every card shown" : null;
            int hidden = kinds.Skip(shown).Sum(kind => Mathf.Max(1, kind.Allies));
            if (!more || !more.gameObject.activeSelf) return $"no more row for the {hidden} allies hidden";
            float moreTop = Depth(cluster, more) - more.rect.height;
            if (Mathf.Abs(moreTop - used) > Tolerance) return $"the more row starts {moreTop:0.#} units down, not {used:0.#}";
            float moreDepth = Depth(cluster, more);
            if (moreDepth > room + Tolerance) return $"the more row reaches {moreDepth:0.#} units down, past the {room:0.#} above the chat";
            TMP_Text label = more.GetComponentInChildren<TMP_Text>(true);
            string text = $"+{hidden} more";
            if (!label || label.text != text) return $"the more row reads '{(label ? label.text : "")}', not '{text}'";
            List<Texture> portraits = new List<Texture>();
            foreach (Kind kind in kinds.Skip(shown))
            {
                CharacterBody body = kind.Lead.sourceMaster ? kind.Lead.sourceMaster.GetBody() : null;
                if (body && body.portraitIcon && !portraits.Contains(body.portraitIcon) && portraits.Count < AllyMoreRow.MaxIcons)
                {
                    portraits.Add(body.portraitIcon);
                }
            }
            List<Texture> icons = more.GetComponentsInChildren<RawImage>()
                .Where(icon => icon.name == AllyMoreRow.IconName).Select(icon => icon.texture).ToList();
            if (!icons.SequenceEqual(portraits)) return $"the more row shows {icons.Count} portraits, not the {portraits.Count} kinds hidden";
            return null;
        }

        // Every row drawn (the first card shown, the other cards, the more row) must start at the first's backdrop
        // edge, with its icon the same distance in. Measured from the world corners, as drawn: the game indents a
        // minion's card under its leader by moving the card's rect, which a portrait's edge alone would hide.
        private static string CompareEdges(RectTransform cluster, List<Kind> kinds, bool compact, List<AllyCardController> cards,
            RectTransform more)
        {
            float? backdrop = null;
            float? inset = null;
            foreach (AllyCardController card in cards)
            {
                if (!card.gameObject.activeSelf) continue;
                float back = LeftEdge(cluster, card.rectTransform);
                // Without a portrait the card's own edge stands in for the icon.
                float icon = LeftEdge(cluster, card.portraitIconImage ? card.portraitIconImage.rectTransform : card.rectTransform);
                bool isShared = kinds.Any(kind => kind.Lead == card && kind.Allies > 1);
                string look = isShared ? "shared" : compact ? "compact" : "full";
                string problem = CompareRow($"{Name(card)}'s {look} card", back, icon, ref backdrop, ref inset);
                if (problem != null) return problem;
            }
            if (more && more.gameObject.activeSelf)
            {
                RectTransform slot = more.Cast<Transform>()
                    .FirstOrDefault(child => child.name == AllyMoreRow.IconName && child.gameObject.activeSelf) as RectTransform;
                float back = LeftEdge(cluster, more);
                float icon = LeftEdge(cluster, slot ? slot : more);
                return CompareRow("the more row", back, icon, ref backdrop, ref inset);
            }
            return null;
        }

        private static string CompareRow(string row, float back, float icon, ref float? backdrop, ref float? inset)
        {
            backdrop = backdrop ?? back;
            inset = inset ?? icon - back;
            if (Mathf.Abs(back - backdrop.Value) > Tolerance)
            {
                return $"{row} has its backdrop {back:0.#} units across, not {backdrop.Value:0.#} like the first";
            }
            if (Mathf.Abs(icon - back - inset.Value) > Tolerance)
            {
                return $"{row} has its icon {icon - back:0.#} units in from its backdrop (icon at {icon:0.#}, backdrop at {back:0.#}), not {inset.Value:0.#} like the first";
            }
            return null;
        }

        private static float LeftEdge(RectTransform cluster, RectTransform rect)
        {
            rect.GetWorldCorners(Corners);
            return cluster.InverseTransformPoint(Corners[0]).x;
        }

        // Null when every card shows at the game's height with the game's bar again, and the mod's rows and counts
        // are gone.
        public static string GameLook(Harness h, int count)
        {
            List<AllyCardController> cards = Cards(h);
            if (cards.Count != count) return $"{cards.Count} ally cards, not {count}";
            foreach (AllyCardController card in cards)
            {
                if (!card.gameObject.activeSelf) return $"{Name(card)}'s card is hidden";
                float tall = card.layoutElement ? card.layoutElement.preferredHeight : 0f;
                if (Mathf.Abs(tall - GameHeight) > Tolerance) return $"{Name(card)}'s card asks for {tall:0.#} units, not {GameHeight:0.#}";
                if (card.nameLabel && card.nameLabel.transform.Find(AllyKindParts.CountName)) return $"{Name(card)}'s card still has a count";
                if (card.healthBar && !card.healthBar.gameObject.activeSelf) return $"{Name(card)}'s card has no bar";
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
