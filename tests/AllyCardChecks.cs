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
    // that still doesn't fit, each section's cards that don't go under a "+N more" row of its own. The cards are in
    // sections: each player's card in the game's order with the kinds following that player, then last the viewer's
    // allies that follow no player card. Players are exempt: full size and shown. Room goes out in priority order:
    // the players' height, a more row for each section with allies, the viewer's own allies, then the players' allies
    // top down, each leaving room for the rows of the player sections below it. If the players and a row for every
    // section with allies don't fit, every ally folds and the cards run on over the chat. (The game's ally cards skip a player
    // without a network user, so the stand-in players of the scoreboard tests get no card unless they are linked to
    // the viewer's, as ScoreboardSetUp.AddPlayers can.)
    internal static class AllyCardChecks
    {
        // AllyCardManager.indentWidth in the game: how far a minion's card sits in under its leader's.
        private const float GameIndent = 16f;
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
            // Every ally folded because the players and a row per section alone don't fit; the cards run past the room.
            public bool Fallback;
            public int HiddenAllies;
            public List<Section> Sections;
        }

        // A player's card (none for the viewer's own allies) and the kinds that follow that player, the first Shown of
        // which stand; the rest are under the section's more row, if any is expected.
        internal sealed class Section
        {
            public Kind Player;
            public readonly List<Kind> Allies = new List<Kind>();
            public int Shown;
            public RectTransform Row;
            public int Hidden => Allies.Skip(Shown).Sum(kind => Mathf.Max(1, kind.Allies));
            public IEnumerable<Kind> ShownKinds => Allies.Take(Shown);
        }

        internal sealed class Kind
        {
            public AllyCardController Lead;
            public string Name;
            public CharacterMaster Leader;
            public int Allies;
            public bool Player;
            public CharacterMaster Master => Lead ? Lead.sourceMaster : null;
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

        // With grouped false, as with GroupAllyCards off: every ally is a kind of its own.
        public static Layout Measure(Harness h, bool grouped = true)
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
                string name = grouped && body && !master.playerCharacterMasterController ? Util.GetBestBodyName(body.gameObject) : null;
                CharacterMaster leader = master && master.minionOwnership ? master.minionOwnership.ownerMaster : null;
                Kind kind = name == null ? null : kinds.FirstOrDefault(found => found.Name == name && found.Leader == leader);
                if (kind == null)
                {
                    kinds.Add(new Kind
                    {
                        Lead = card, Name = name, Leader = leader, Allies = master ? 1 : 0,
                        Player = master && master.playerCharacterMasterController,
                    });
                }
                else
                {
                    kind.Allies++;
                    sharing.Add(card);
                }
            }
            List<Section> sections = Sections(kinds);
            kinds = sections.SelectMany(section => (section.Player != null ? new[] { section.Player } : new Kind[0])
                .Concat(section.Allies)).ToList();
            layout.Kinds = kinds.Count;
            bool compact = kinds.Count * GameHeight > room + Tolerance;
            float Height(Kind kind) => compact && kind.Allies <= 1 && !kind.Player ? row : GameHeight;
            bool fold = kinds.Sum(Height) > room + Tolerance;
            float playerHeights = sections.Sum(section => section.Player != null ? Height(section.Player) : 0f);
            int withAllies = sections.Count(section => section.Allies.Count > 0);
            int playerRows = sections.Count(section => section.Player != null && section.Allies.Count > 0);
            // Cards that fit with the rows alone: otherwise every ally folds and the players run over the chat.
            bool fallback = fold && playerHeights + row * withAllies > room + Tolerance;
            foreach (Section section in sections)
            {
                section.Shown = fold ? 0 : section.Allies.Count;
            }
            if (fold && !fallback)
            {
                // The viewer's own allies take the room left after the players and their rows, then the players' allies
                // follow top down, each leaving room for the rows of the player sections below.
                Section own = sections.FirstOrDefault(section => section.Player == null);
                float ownUsed = own != null ? Fill(own, 0f, room - playerHeights - row * playerRows, 0, Height, row) : 0f;
                float used = playerHeights + ownUsed;
                int laterRows = playerRows;
                foreach (Section section in sections.Where(section => section.Player != null))
                {
                    laterRows -= section.Allies.Count > 0 ? 1 : 0;
                    used += Fill(section, used, room, laterRows, Height, row);
                }
            }
            layout.Compact = compact;
            layout.Fallback = fallback;
            layout.Sections = sections;
            layout.HiddenAllies = sections.Sum(section => section.Hidden);
            layout.Problem = Compare(cluster, room, kinds, sections, sharing, compact, fallback, cards, h);
            return layout;
        }

        // Shows the section's allies top down while they fit under limit, with the section's own more row and laterRows
        // more rows after them; returns the height used, the section's row included.
        private static float Fill(Section section, float used, float limit, int laterRows, System.Func<Kind, float> height, float row)
        {
            float start = used;
            while (section.Shown < section.Allies.Count)
            {
                float rowAfter = section.Shown < section.Allies.Count - 1 ? row : 0f;
                if (used + height(section.Allies[section.Shown]) + rowAfter + row * laterRows > limit + Tolerance) break;
                used += height(section.Allies[section.Shown]);
                section.Shown++;
            }
            if (section.Shown < section.Allies.Count)
            {
                used += row;
            }
            return used - start;
        }

        // The sections as the mod sets them: each player card in the game's order with the kinds that follow that
        // player's master, then last the viewer's allies that follow no player card.
        private static List<Section> Sections(List<Kind> kinds)
        {
            List<Section> players = kinds.Where(kind => kind.Player).Select(kind => new Section { Player = kind }).ToList();
            Section own = new Section();
            foreach (Kind kind in kinds.Where(kind => !kind.Player))
            {
                Section owner = kind.Leader ? players.FirstOrDefault(section => section.Player.Master == kind.Leader) : null;
                (owner ?? own).Allies.Add(kind);
            }
            List<Section> sections = new List<Section>(players);
            if (own.Allies.Count > 0)
            {
                sections.Add(own);
            }
            return sections;
        }

        private static string Compare(RectTransform cluster, float room, List<Kind> kinds, List<Section> sections,
            HashSet<AllyCardController> sharing, bool compact, bool fallback, List<AllyCardController> cards, Harness h)
        {
            foreach (AllyCardController card in sharing)
            {
                if (card.gameObject.activeSelf) return $"a second {Name(card)} has a card of its own";
            }
            List<RectTransform> rows = More(h);
            // The rows come in the sections' order, top to bottom, and a row under a player is indented.
            List<bool> underPlayer = sections.Where(section => section.Hidden > 0).Select(section => section.Player != null).ToList();
            // So is each card of an ally another player owns.
            HashSet<AllyCardController> playersAllies = new HashSet<AllyCardController>(sections
                .Where(section => section.Player != null).SelectMany(section => section.Allies).Select(kind => kind.Lead));
            string edges = CompareEdges(cluster, kinds, compact, cards, rows, underPlayer, playersAllies);
            if (edges != null) return edges;
            float above = 0f;
            int rowsSeen = 0;
            foreach (Section section in sections)
            {
                List<(Kind Kind, bool Fits)> order = new List<(Kind, bool)>();
                if (section.Player != null)
                {
                    order.Add((section.Player, true));
                }
                for (int i = 0; i < section.Allies.Count; i++)
                {
                    order.Add((section.Allies[i], i < section.Shown));
                }
                foreach ((Kind kind, bool fits) in order)
                {
                    string problem = CompareCard(cluster, room, kind, fits, compact, ref above, kinds.IndexOf(kind), kinds.Count);
                    if (problem != null) return problem;
                }
                if (section.Hidden == 0) continue;
                // The rows are in the sections' order, top to bottom.
                RectTransform more = rowsSeen < rows.Count ? rows[rowsSeen++] : null;
                string owner = section.Player != null ? $"{Name(section.Player.Lead)}'s" : "the viewer's";
                if (more == null) return $"no more row for {owner} {section.Hidden} allies hidden";
                section.Row = more;
                float moreTop = Depth(cluster, more) - more.rect.height;
                if (Mathf.Abs(moreTop - above) > Tolerance) return $"{owner} more row starts {moreTop:0.#} units down, not {above:0.#}";
                above += AllyCompactCard.RowHeight;
                float moreDepth = Depth(cluster, more);
                if (moreDepth > room + Tolerance && !fallback) return $"{owner} more row reaches {moreDepth:0.#} units down, past the {room:0.#} above the chat";
                TMP_Text label = more.GetComponentInChildren<TMP_Text>(true);
                string text = $"+{section.Hidden} more";
                if (!label || label.text != text) return $"{owner} more row reads '{(label ? label.text : "")}', not '{text}'";
                List<Texture> portraits = new List<Texture>();
                foreach (Kind kind in section.Allies.Skip(section.Shown))
                {
                    CharacterBody body = kind.Master ? kind.Master.GetBody() : null;
                    if (body && body.portraitIcon && !portraits.Contains(body.portraitIcon) && portraits.Count < AllyMoreRow.MaxIcons)
                    {
                        portraits.Add(body.portraitIcon);
                    }
                }
                List<Texture> icons = more.GetComponentsInChildren<RawImage>()
                    .Where(icon => icon.name == AllyMoreRow.IconName).Select(icon => icon.texture).ToList();
                if (!icons.SequenceEqual(portraits)) return $"{owner} more row shows {icons.Count} portraits, not the {portraits.Count} kinds hidden";
            }
            return rowsSeen < rows.Count ? $"{rows.Count} more rows show, {rowsSeen} sections fold" : null;
        }

        private static string CompareCard(RectTransform cluster, float room, Kind kind, bool fits, bool compact, ref float above,
            int index, int count)
        {
            AllyCardController card = kind.Lead;
            if (kind.Player && !fits) return $"player {Name(card)}'s card is folded away";
            if (card.gameObject.activeSelf != fits) return $"{Name(card)}'s card is {(fits ? "hidden" : "shown")} (kind {index + 1} of {count})";
            if (!fits) return null;
            float height = compact && kind.Allies <= 1 && !kind.Player ? AllyCompactCard.RowHeight : GameHeight;
            float tall = card.rectTransform.rect.height;
            if (Mathf.Abs(tall - height) > Tolerance) return $"{Name(card)}'s card is {tall:0.#} units tall, not {height:0.#}";
            // The card's row gives the portrait any spare width, so a compact row's portrait can grow past the
            // row (the game's own portrait is a little taller than its card, so only its width is compared).
            LayoutElement portrait = card.portraitIconImage ? card.portraitIconImage.GetComponent<LayoutElement>() : null;
            float portraitWidth = portrait ? card.portraitIconImage.rectTransform.rect.width : 0f;
            if (compact && kind.Allies <= 1 && portrait && Mathf.Abs(portraitWidth - portrait.preferredWidth) > Tolerance)
            {
                return $"{Name(card)}'s compact portrait is {portraitWidth:0.#} units wide, not {portrait.preferredWidth:0.#}. {RowLayout(card)}";
            }
            float depth = Depth(cluster, card.rectTransform);
            // Each card's top is the height of the cards and rows above it, in the order above.
            float top = depth - card.rectTransform.rect.height;
            if (Mathf.Abs(top - above) > Tolerance) return $"{Name(card)}'s card starts {top:0.#} units down, not {above:0.#} (kind {index + 1})";
            above += height;
            if (!kind.Player && depth > room + Tolerance) return $"{Name(card)}'s card reaches {depth:0.#} units down, past the {room:0.#} above the chat";
            TMP_Text countLabel = card.nameLabel ? card.nameLabel.transform.Find(AllyKindParts.CountName)?.GetComponent<TMP_Text>() : null;
            if (kind.Allies > 1)
            {
                if (!countLabel || countLabel.text != "×" + kind.Allies) return $"{Name(card)}'s card reads '{(countLabel ? countLabel.text : "")}', not ×{kind.Allies}";
                Transform segments = card.healthBar ? card.healthBar.transform.parent.Find(AllyKindParts.SegmentsName) : null;
                int bars = segments ? segments.Cast<Transform>().Count(segment => segment.gameObject.activeSelf) : 0;
                if (bars != kind.Allies) return $"{Name(card)}'s card has {bars} bar segments, not {kind.Allies}";
                if (card.healthBar.gameObject.activeSelf) return $"{Name(card)}'s card still shows the game's bar";
            }
            else if (countLabel && countLabel.gameObject.activeInHierarchy)
            {
                return $"{Name(card)}'s card of one ally shows a count";
            }
            return null;
        }

        // Every row drawn (the first card shown, the other cards, the more rows) must start at the first's backdrop
        // edge, with its icon the same distance in, except that a player's allies' cards and more row start the game's
        // minion indent further in. Measured from the world corners, as drawn: the game indents a minion's card under its
        // leader by moving the card's rect, which a portrait's edge alone would hide.
        private static string CompareEdges(RectTransform cluster, List<Kind> kinds, bool compact, List<AllyCardController> cards,
            List<RectTransform> rows, List<bool> underPlayer, HashSet<AllyCardController> playersAllies)
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
                // The game's indent is in the cards' container's units; measured here in the cluster's.
                float indent = playersAllies.Contains(card) ? Indent(cluster, card.rectTransform) : 0f;
                string problem = CompareRow($"{Name(card)}'s {look} card", back, icon, indent, ref backdrop, ref inset);
                if (problem != null) return problem;
            }
            for (int i = 0; i < rows.Count; i++)
            {
                RectTransform more = rows[i];
                float indent = i < underPlayer.Count && underPlayer[i] ? Indent(cluster, more) : 0f;
                RectTransform slot = more.Cast<Transform>()
                    .FirstOrDefault(child => child.name == AllyMoreRow.IconName && child.gameObject.activeSelf) as RectTransform;
                float back = LeftEdge(cluster, more);
                float icon = LeftEdge(cluster, slot ? slot : more);
                string problem = CompareRow($"the more row {more.name}", back, icon, indent, ref backdrop, ref inset);
                if (problem != null) return problem;
            }
            return null;
        }

        // The game's minion indent, in the cluster's units, for a rect in the cards' container.
        private static float Indent(RectTransform cluster, RectTransform rect) =>
            cluster.InverseTransformVector(rect.parent.TransformVector(new Vector3(GameIndent, 0f, 0f))).x;

        private static string CompareRow(string row, float back, float icon, float indent, ref float? backdrop, ref float? inset)
        {
            backdrop = backdrop ?? back - indent;
            inset = inset ?? icon - back;
            if (Mathf.Abs(back - backdrop.Value - indent) > Tolerance)
            {
                return indent == 0f
                    ? $"{row} has its backdrop {back:0.#} units across, not {backdrop.Value:0.#} like the first"
                    : $"{row} has its backdrop {back:0.#} units across, not {backdrop.Value + indent:0.#}, the first's {backdrop.Value:0.#} indented {indent:0.#} like another player's ally";
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
            return More(h).Count > 0 ? "the more row is still shown" : null;
        }

        // True when any card carries the mod's count or segments.
        public static bool Grouped(Harness h)
        {
            foreach (AllyCardController card in Cards(h))
            {
                if (card.nameLabel && card.nameLabel.transform.Find(AllyKindParts.CountName)) return true;
                if (card.healthBar && card.healthBar.transform.parent.Find(AllyKindParts.SegmentsName)) return true;
            }
            return false;
        }

        // Null when every kind's first card is shown at the game's height and there is no more row. Shared cards keep
        // their count and segments, and a kind's other cards stay hidden, so unlike GameLook this leaves them be.
        public static string FullSize(Harness h)
        {
            List<AllyCardController> cards = Cards(h);
            List<string> seen = new List<string>();
            foreach (AllyCardController card in cards)
            {
                CharacterMaster master = card.sourceMaster;
                CharacterBody body = master ? master.GetBody() : null;
                string kind = body && !master.playerCharacterMasterController ? Util.GetBestBodyName(body.gameObject) : null;
                CharacterMaster leader = master && master.minionOwnership ? master.minionOwnership.ownerMaster : null;
                if (kind != null)
                {
                    string key = kind + "/" + (leader ? leader.GetInstanceID().ToString() : "");
                    if (seen.Contains(key)) continue;
                    seen.Add(key);
                }
                if (!card.gameObject.activeSelf) return $"{Name(card)}'s card is hidden";
                float tall = card.rectTransform.rect.height;
                if (Mathf.Abs(tall - GameHeight) > Tolerance) return $"{Name(card)}'s card is {tall:0.#} units tall, not {GameHeight:0.#}";
            }
            return More(h).Count > 0 ? "a more row is still shown" : null;
        }

        // How the card's row lays out its children, for a failure message: the row group's settings, then each child's
        // width, the sizes the layout reads from it, and its layout components.
        internal static string RowLayout(AllyCardController card)
        {
            HorizontalLayoutGroup row = card.GetComponent<HorizontalLayoutGroup>();
            string text = $"Card {card.rectTransform.rect.width:0.#}x{card.rectTransform.rect.height:0.#}; row " +
                          (row ? $"enabled={row.enabled} controlW={row.childControlWidth} controlH={row.childControlHeight} " +
                                 $"expandW={row.childForceExpandWidth} expandH={row.childForceExpandHeight} " +
                                 $"padding={row.padding} spacing={row.spacing} align={row.childAlignment}" : "none") + ";";
            foreach (RectTransform child in card.transform)
            {
                string parts = string.Join(",", child.GetComponents<Component>()
                    .Where(part => part is ILayoutElement || part is ILayoutController)
                    .Select(part => part.GetType().Name + (part is Behaviour behaviour && !behaviour.enabled ? "(off)" : "") +
                                    (part is AspectRatioFitter fitter ? $"({fitter.aspectMode} {fitter.aspectRatio:0.##})" : "") +
                                    (part is LayoutElement element ? $"(pref {element.preferredWidth:0.#} flex {element.flexibleWidth:0.#} prio {element.layoutPriority})" : "")));
                text += $" {child.name} active={child.gameObject.activeSelf} {child.rect.width:0.#}x{child.rect.height:0.#} " +
                        $"min={LayoutUtility.GetMinWidth(child):0.#} pref={LayoutUtility.GetPreferredWidth(child):0.#} " +
                        $"flex={LayoutUtility.GetFlexibleWidth(child):0.#} [{parts}];";
            }
            return text;
        }

        // The more rows shown, top to bottom: one per section that folds, each named AllyMoreRow.Name and its index.
        internal static List<RectTransform> More(Harness h)
        {
            AllyCardManager manager = SevenUIChecks.HudRoot(h).GetComponentInChildren<AllyCardManager>(true);
            List<RectTransform> rows = new List<RectTransform>();
            if (!manager) return rows;
            foreach (Transform child in manager.transform)
            {
                if (child.name.StartsWith(AllyMoreRow.Name) && child.gameObject.activeSelf)
                {
                    rows.Add((RectTransform)child);
                }
            }
            return rows.OrderBy(row => Depth(manager.transform as RectTransform, row)).ToList();
        }

        // How far below the cluster's top the rect ends, in the cluster's units.
        internal static float Depth(RectTransform cluster, RectTransform rect)
        {
            rect.GetWorldCorners(Corners);
            return cluster.rect.yMax - cluster.InverseTransformPoint(Corners[0]).y;
        }

        private static string Name(AllyCardController card) => card.nameLabel ? card.nameLabel.text : card.name;
    }
}
