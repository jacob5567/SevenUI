using System.Collections.Generic;
using System.Text;
using RoR2;
using RoR2.UI;
using UnityEngine;
using UnityEngine.UI;

namespace SevenUI
{
    // Restyles the ally cards on the left: each on a flat panel, with a flat health bar on the dark trough like the
    // player's. Allies of one kind (the same name, following the same leader) always share one card, the kind's first,
    // with "×8" and a bar segment per ally (AllyKindParts); the kind's other cards are hidden and take no room. Players
    // and allies between bodies keep cards of their own. The cards stay in the game's order, in sections: one per
    // player card, the player and the kinds that follow that player, then last the viewer's own allies that follow no
    // player card. A player's card is full size and never folded (see IsPlayer). While the cards would reach down over
    // the chat, the cards of a single ally become compact rows (AllyCompactCard), while shared cards keep their size
    // and segments. If that is still too tall, each section folds the allies' cards that don't fit into a "+N more"
    // row of its own under the section's last card shown, with a portrait for each kind and N the allies they hold
    // (AllyMoreRow). Room is given out in priority order, not display order: the player cards, a more row for every
    // section with allies, the viewer's own allies, then the other players' allies top down (FitSections). The
    // game keeps adding, filling, sorting and removing the cards; this only resizes and hides them,
    // sets them in the order above and draws over them. While the scoreboard is open, the cards are hidden altogether,
    // as they would otherwise draw over the board's left edge. With CompactAllyCards off, none of the fitting happens:
    // the cards keep their size and none is folded. With GroupAllyCards off, every card stands alone, as the game has
    // it, and the sections still apply.
    internal sealed class AllyCardsStyler : IStyler
    {
        // How far up a chain of minion owners a kind's player is looked for.
        private const int MaxLeaderDepth = 8;
        // Under each card's health bar.
        private const string TroughName = "BackgroundPanel";
        // Kept clear between the cards and the chat's messages, in the design canvas's pixels.
        private const float ChatGap = 8f * HudUnits.PerCanvasPixel;
        private const float SizeTolerance = 0.01f;

        private readonly HUD hud;
        private readonly Transform root;
        private readonly StyleRecorder recorder = new StyleRecorder();
        // In the game's order, top to bottom.
        private readonly List<AllyCardController> cards = new List<AllyCardController>();
        // Each card shown, in that order: a kind's shared card, or a card of its own.
        private readonly List<Kind> kinds = new List<Kind>();
        private readonly Stack<Kind> spareKinds = new Stack<Kind>();
        // Kinds as sorted from the cards, before they are set in sections.
        private readonly List<Kind> unsorted = new List<Kind>();
        // Every section made, kept with its more row; the first has no player. Active holds those the cards fill now.
        private readonly List<Section> pool = new List<Section>();
        private readonly List<Section> active = new List<Section>();
        // Where each card shown belongs in the stack, from the top, set again after the game's layout runs.
        private readonly List<(RectTransform Rect, float Y)> places = new List<(RectTransform, float)>();
        private readonly Dictionary<AllyCardController, AllyCompactCard> looks = new Dictionary<AllyCardController, AllyCompactCard>();
        private readonly Dictionary<AllyCardController, AllyKindParts> shared = new Dictionary<AllyCardController, AllyKindParts>();
        // Each ally's name as the game shows it, found again when its body or drone tier changes: a hidden card's own
        // label stops updating.
        private readonly AllyNames names = new AllyNames();
        private readonly List<AllyCardController> goneCards = new List<AllyCardController>();
        private readonly Vector3[] corners = new Vector3[4];
        // The cards' container (the game's AllyCardManager, which stacks them) and the chat's message area.
        private RectTransform container;
        // The game's manager of the cards in container. Its layout indents a card by its indentWidth when the card's
        // shouldIndent is set, and lays the card's contents out to the narrower width. The game sets shouldIndent when
        // a minion's leader has a card, a player's or another ally's; the mod keeps to that rule (see SetIndent).
        private AllyCardManager cardManager;
        private RectTransform chat;
        // Fades the whole stack out while the scoreboard is open.
        private CanvasGroup hider;
        // How far the container's bottom is pulled down past its own (see Stretch).
        private float stretch;

        private sealed class Kind
        {
            // The card the kind shares: the first of a living ally, so the game's dead mark never shows over allies still
            // alive, or the first card if none lives.
            public AllyCardController Lead;
            public CharacterMaster Leader;
            // Drones of different upgrade tiers are different kinds, though the name's tier suffix is the only difference.
            public int Tier;
            // None for a card that stands alone.
            public string Name;
            public readonly List<CharacterMaster> Members = new List<CharacterMaster>();
            public readonly List<AllyCardController> Cards = new List<AllyCardController>();
            // The game's rule: indented when the kind's leader has a card, as a player other than the viewer or an ally
            // with minions of its own does.
            public bool Indent;
            public bool Shared => Members.Count > 1;
        }

        // A player's card and the allies that follow that player, which fold into a more row of their own, or the
        // viewer's allies with no player card.
        private sealed class Section
        {
            public int Index;
            public Kind Player;
            public readonly List<Kind> Allies = new List<Kind>();
            // How many of Allies show; the rest are under the more row, which has their cards in Overflow.
            public int Shown;
            public int HiddenAllies;
            public readonly List<AllyCardController> Overflow = new List<AllyCardController>();
            public AllyMoreRow Row;
        }

        public AllyCardsStyler(HUD hud, Transform root)
        {
            this.hud = hud;
            this.root = root;
        }

        public bool Compact { get; private set; }
        public int HiddenCount { get; private set; }

        public bool Enabled => SevenUIPlugin.Settings.FlatPanels.Value;

        public void Apply()
        {
            Transform cluster = root.Find(HudPaths.LeftCluster);
            if (!cluster) return;
            cluster.GetComponentsInChildren(true, cards);
            // Drifter's card for what it carries is an ally card too, but a part of its own display, at the game's size.
            cards.RemoveAll(card => card is BaggedCardController);
            foreach (AllyCardController card in cards)
            {
                StyleCard(card);
            }
            // After the canvas lays the cards out in the game's order, so a frame never draws them in it.
            Canvas.willRenderCanvases -= Place;
            Canvas.willRenderCanvases += Place;
            if (!container)
            {
                cardManager = cluster.GetComponentInChildren<AllyCardManager>(true);
                container = cardManager ? (RectTransform)cardManager.transform : null;
                stretch = 0f;
            }
            // Kept when the look is turned off, only shown again, as a new group couldn't be added while an old one
            // waited to be destroyed at the frame's end.
            if (container && !hider)
            {
                hider = container.GetComponent<CanvasGroup>();
                if (!hider)
                {
                    hider = container.gameObject.AddComponent<CanvasGroup>();
                }
            }
            if (!chat)
            {
                // The messages sit in the standard area unless the player is typing, when the game moves them to a
                // taller one; the cards keep to the standard one, so typing doesn't reshape them.
                ChatBox box = root.GetComponentInChildren<ChatBox>(true);
                chat = !box ? null : box.standardChatboxRect ? box.standardChatboxRect : (RectTransform)box.transform;
            }
        }

        public void Restore()
        {
            Canvas.willRenderCanvases -= Place;
            places.Clear();
            foreach (KeyValuePair<AllyCardController, AllyKindParts> entry in shared)
            {
                entry.Value.Remove(entry.Key);
            }
            shared.Clear();
            foreach (AllyCompactCard look in looks.Values)
            {
                look.Revert();
            }
            looks.Clear();
            foreach (Section section in pool)
            {
                section.Row?.Destroy();
            }
            pool.Clear();
            active.Clear();
            kinds.Clear();
            Stretch(0f);
            if (hider)
            {
                hider.alpha = 1f;
                hider.blocksRaycasts = true;
            }
            cards.Clear();
            names.Clear();
            Compact = false;
            HiddenCount = 0;
            recorder.Restore();
        }

        // Every frame while the panels are on: the game reassigns its cards as allies come and go, and each ally's
        // health changes. A few reads per card; a card is only changed when its look or place changes, and a segment
        // when its ally's health changed.
        public void Update()
        {
            // The game turns the scoreboard's panel on and off as it opens and closes.
            bool board = hud && hud.scoreboardPanel && hud.scoreboardPanel.activeSelf;
            if (hider && hider.alpha != (board ? 0f : 1f))
            {
                hider.alpha = board ? 0f : 1f;
                hider.blocksRaycasts = !board;
            }
            cards.RemoveAll(card => !card);
            ForgetGoneCards();
            if (!container || cards.Count == 0)
            {
                foreach (Section section in pool)
                {
                    section.Row?.Hide();
                }
                return;
            }
            SortIntoKinds();
            float room = Room();
            float fullHeight = 0f;
            foreach (Kind kind in kinds)
            {
                fullHeight += LookOf(kind.Lead).FullHeight;
            }
            // With the setting off the cards keep their size and none is folded away, whatever the room.
            bool fold = SevenUIPlugin.Settings.CompactAllyCards.Value;
            bool compact = fold && fullHeight > room + SizeTolerance;
            float total = 0f;
            foreach (Kind kind in kinds)
            {
                total += Height(kind, compact);
            }
            FitSections(compact, room, fold && total > room + SizeTolerance);

            float tallest = 0f;
            int hiddenAllies = 0;
            float top = 0f;
            places.Clear();
            foreach (Section section in active)
            {
                if (section.Player != null)
                {
                    ShowKind(section.Player, compact, true);
                    tallest = Mathf.Max(tallest, Height(section.Player, compact));
                    places.Add((section.Player.Lead.rectTransform, -top));
                    top += Height(section.Player, compact);
                }
                section.Overflow.Clear();
                int hiddenHere = 0;
                bool hiddenIndent = true;
                for (int i = 0; i < section.Allies.Count; i++)
                {
                    Kind kind = section.Allies[i];
                    bool fits = i < section.Shown;
                    ShowKind(kind, compact, fits);
                    if (fits)
                    {
                        tallest = Mathf.Max(tallest, Height(kind, compact));
                        places.Add((kind.Lead.rectTransform, -top));
                        top += Height(kind, compact);
                    }
                    else
                    {
                        hiddenHere += Mathf.Max(1, kind.Members.Count);
                        hiddenIndent &= kind.Indent;
                        section.Overflow.Add(kind.Lead);
                    }
                }
                section.HiddenAllies = hiddenHere;
                hiddenAllies += hiddenHere;
                if (hiddenHere > 0)
                {
                    int padding = PaddingLeft(kinds[0].Lead);
                    if (section.Row == null || !section.Row.Alive)
                    {
                        section.Row = new AllyMoreRow(container, cards[0].nameLabel, padding, section.Index);
                    }
                    // Indented as the cards it stands for would be: under another player, as all of that player's
                    // allies are, but not for the viewer's own allies, which have no card above them to hang under.
                    float indent = hiddenIndent ? GameIndent() : 0f;
                    section.Row.Show(top, section.Overflow, hiddenHere, padding, indent);
                    top += AllyCompactCard.RowHeight;
                }
                else
                {
                    section.Row?.Hide();
                }
            }
            // Sections the cards no longer fill have no row to show.
            foreach (Section section in pool)
            {
                if (!active.Contains(section))
                {
                    section.Row?.Hide();
                }
            }
            Compact = compact;
            HiddenCount = hiddenAllies;
            Stretch(cards.Count * tallest);
            Place();
        }

        // Decides how many of each section's allies show. Players always show at full size. If the cards must fold,
        // room is given in priority order while the sections stay in display order (players first, the viewer's own
        // allies last): the player cards, then a more row for each section with allies, then the viewer's own allies,
        // then the other players' allies top down. An ally shows only if, with it, there is still room for its
        // section's more row (unless it is the section's last ally) and a row for each later player section that has
        // allies; the first ally that doesn't fit ends its section's shown allies. If the player cards and a row for
        // each section with allies (the viewer's own included) don't fit together, every ally folds, and the player
        // cards run on over the chat, the viewer's own row after them.
        private void FitSections(bool compact, float room, bool fold)
        {
            float playerHeights = 0f;
            int withAllies = 0;
            int playerRows = 0;
            Section own = null;
            foreach (Section section in active)
            {
                if (section.Player != null)
                {
                    playerHeights += Height(section.Player, compact);
                    playerRows += section.Allies.Count > 0 ? 1 : 0;
                }
                else
                {
                    own = section;
                }
                withAllies += section.Allies.Count > 0 ? 1 : 0;
                section.Shown = fold ? 0 : section.Allies.Count;
            }
            if (!fold) return;
            if (playerHeights + AllyCompactCard.RowHeight * withAllies > room + SizeTolerance) return;
            // The viewer's own allies get the room left once the players and every player section's row are kept.
            float ownUsed = 0f;
            if (own != null)
            {
                float budget = room - playerHeights - AllyCompactCard.RowHeight * playerRows;
                ownUsed = Fill(own, compact, 0f, budget, 0);
            }
            float used = playerHeights + ownUsed;
            int laterRows = playerRows;
            foreach (Section section in active)
            {
                if (section.Player == null) continue;
                laterRows -= section.Allies.Count > 0 ? 1 : 0;
                used += Fill(section, compact, used, room, laterRows);
            }
        }

        // Shows the section's allies top down while they fit under limit, with the section's own row and laterRows more
        // rows after them. Returns the height used, the section's row included.
        private float Fill(Section section, bool compact, float used, float limit, int laterRows)
        {
            float start = used;
            float reserved = AllyCompactCard.RowHeight * laterRows;
            while (section.Shown < section.Allies.Count)
            {
                float height = Height(section.Allies[section.Shown], compact);
                float rowAfter = section.Shown < section.Allies.Count - 1 ? AllyCompactCard.RowHeight : 0f;
                if (used + height + rowAfter + reserved > limit + SizeTolerance) break;
                used += height;
                section.Shown++;
            }
            if (section.Shown < section.Allies.Count)
            {
                used += AllyCompactCard.RowHeight;
            }
            return used - start;
        }

        // The one rule for cards that stay: a player's card keeps its full size and is never folded under a more row.
        // A later pin of a kind goes here.
        private static bool IsPlayer(Kind kind)
        {
            CharacterMaster master = kind.Lead.sourceMaster;
            return master && master.playerCharacterMasterController;
        }

        // A shared card or a player's keeps the game's height, with its segments; a card of one ally becomes a compact row
        // when the cards must be compact.
        private float Height(Kind kind, bool compact) =>
            Compacts(kind, compact) ? AllyCompactCard.RowHeight : LookOf(kind.Lead).FullHeight;

        private static bool Compacts(Kind kind, bool compact) => compact && !kind.Shared && !IsPlayer(kind);

        // The game steps each card down in its own order, a player's minions right under the player, so a card is
        // moved to its place in ours, only when it isn't there. Each card's top is its height above, as the game's
        // layout sums them while the container is stretched to give every card its height.
        private void Place()
        {
            // The HUD can be destroyed without a Restore (the run ends), and the event is static.
            if (!root)
            {
                Canvas.willRenderCanvases -= Place;
                return;
            }
            foreach ((RectTransform rect, float y) in places)
            {
                if (!rect) continue;
                Vector2 position = rect.anchoredPosition;
                if (Mathf.Abs(position.y - y) > SizeTolerance)
                {
                    rect.anchoredPosition = new Vector2(position.x, y);
                }
            }
        }

        // The left padding of a card's row, where its portrait starts inside it.
        private static int PaddingLeft(AllyCardController card)
        {
            HorizontalLayoutGroup row = card ? card.GetComponent<HorizontalLayoutGroup>() : null;
            return row ? row.padding.left : 0;
        }

        // The indent the game gives a minion's card under its leader's.
        private float GameIndent() => cardManager ? cardManager.indentWidth : 0f;

        // The kind's indent on each of its cards, so a card handed to a kind with another leader takes the kind's. The
        // game re-sets the flag when its allies change, so it is set again each frame, and the cards laid out again
        // when it changed.
        private void SetIndent(AllyCardController card, bool indent)
        {
            if (card.shouldIndent == indent) return;
            card.shouldIndent = indent;
            if (container)
            {
                LayoutRebuilder.MarkLayoutForRebuild(container);
            }
        }

        private void ShowKind(Kind kind, bool compact, bool fits)
        {
            foreach (AllyCardController card in kind.Cards)
            {
                SetIndent(card, kind.Indent);
                AllyCompactCard look = LookOf(card);
                if (card != kind.Lead)
                {
                    // A card that was the lead drops its parts, as the kind's lead moved to a living ally.
                    if (shared.TryGetValue(card, out AllyKindParts old))
                    {
                        old.Remove(card);
                        shared.Remove(card);
                    }
                    look.Set(false, true);
                    continue;
                }
                bool hasParts = shared.TryGetValue(card, out AllyKindParts parts);
                if (!kind.Shared && hasParts)
                {
                    parts.Remove(card);
                    shared.Remove(card);
                }
                // The compact look comes off before a kind's parts go on: they share the bars' column.
                look.Set(Compacts(kind, compact), !fits);
                if (!kind.Shared || !fits) continue;
                if (!hasParts)
                {
                    parts = AllyKindParts.Add(card);
                    if (parts == null) continue;
                    shared.Add(card, parts);
                }
                parts.Update(card, kind.Members);
            }
        }

        // Sorts the cards into kinds, in the cards' order: the first card of a name and leader starts a kind, which the
        // rest of its allies join.
        private void SortIntoKinds()
        {
            foreach (Kind kind in kinds)
            {
                kind.Lead = null;
                kind.Leader = null;
                kind.Indent = false;
                kind.Tier = 0;
                kind.Name = null;
                kind.Members.Clear();
                kind.Cards.Clear();
                spareKinds.Push(kind);
            }
            kinds.Clear();
            bool group = SevenUIPlugin.Settings.GroupAllyCards.Value;
            foreach (AllyCardController card in cards)
            {
                CharacterMaster master = card.sourceMaster;
                // Players stand alone, as does an ally between bodies, which has no name to group by. With grouping off
                // no card has a name, so each is a kind of its own and ShowKind takes off any parts it had.
                int tier = 0;
                string name = group && master && !master.playerCharacterMasterController ? NameOf(master, out tier) : null;
                CharacterMaster leader = master && master.minionOwnership ? master.minionOwnership.ownerMaster : null;
                Kind found = null;
                if (name != null)
                {
                    foreach (Kind kind in kinds)
                    {
                        if (kind.Name == name && kind.Tier == tier && kind.Leader == leader)
                        {
                            found = kind;
                            break;
                        }
                    }
                }
                if (found == null)
                {
                    found = spareKinds.Count > 0 ? spareKinds.Pop() : new Kind();
                    found.Lead = card;
                    found.Leader = leader;
                    found.Tier = tier;
                    found.Name = name;
                    kinds.Add(found);
                }
                found.Cards.Add(card);
                // A dead ally's body lingers for a while, and its card would still be counted and drawn as a segment.
                if (IsAlive(master))
                {
                    found.Members.Add(master);
                    if (!IsAlive(found.Lead.sourceMaster))
                    {
                        found.Lead = card;
                    }
                }
            }
            // As the game's AllyCardManager.BuildFromCharacterData does: a minion is indented when its leader has a card.
            foreach (Kind kind in kinds)
            {
                kind.Indent = HasCard(kind.Leader);
            }
            BuildSections();
        }

        private bool HasCard(CharacterMaster master)
        {
            if (!master) return false;
            foreach (AllyCardController card in cards)
            {
                if (card.sourceMaster == master) return true;
            }
            return false;
        }

        // Sets the kinds in sections, each in the game's order: one section per player card, the player and then the
        // kinds that follow that player's master, then last the viewer's own allies that follow no player card (the
        // minions of the local player, who may have no card, and of allies between bodies). Kinds is rebuilt in that
        // order; a section's Index stays its place in the pool, own first, for its more row's name.
        private void BuildSections()
        {
            unsorted.Clear();
            unsorted.AddRange(kinds);
            kinds.Clear();
            active.Clear();
            foreach (Section section in pool)
            {
                section.Player = null;
                section.Allies.Clear();
            }
            int next = 1;
            foreach (Kind kind in unsorted)
            {
                if (!IsPlayer(kind)) continue;
                Section section = SectionAt(next++);
                section.Player = kind;
            }
            SectionAt(0);
            foreach (Kind kind in unsorted)
            {
                if (IsPlayer(kind)) continue;
                OwnerOf(kind, next).Allies.Add(kind);
            }
            for (int n = 1; n <= next; n++)
            {
                Section section = pool[n % next];
                if (section.Player == null && section.Allies.Count == 0) continue;
                active.Add(section);
                if (section.Player != null)
                {
                    kinds.Add(section.Player);
                }
                kinds.AddRange(section.Allies);
            }
        }

        // The section of the player the kind follows, up its chain of leaders, so a minion of another player's drone
        // stays with that player; the viewer's own section if no player card leads it. The chain is cut short in case
        // an owner ever leads back round to itself.
        private Section OwnerOf(Kind kind, int playerSections)
        {
            CharacterMaster leader = kind.Leader;
            for (int depth = 0; leader && depth < MaxLeaderDepth; depth++)
            {
                for (int i = 1; i < playerSections; i++)
                {
                    if (pool[i].Player.Lead.sourceMaster == leader) return pool[i];
                }
                leader = leader.minionOwnership ? leader.minionOwnership.ownerMaster : null;
            }
            return pool[0];
        }

        private Section SectionAt(int index)
        {
            while (pool.Count <= index)
            {
                pool.Add(new Section { Index = pool.Count });
            }
            return pool[index];
        }

        private static bool IsAlive(CharacterMaster master)
        {
            if (!master) return false;
            CharacterBody body = master.GetBody();
            return body && body.healthComponent && body.healthComponent.alive;
        }

        // The name and tier the ally's kind is grouped by (AllyNames).
        private string NameOf(CharacterMaster master, out int tier) => names.NameOf(master, cards.Count, out tier);

        // The height the cards may take down from the container's top: to the chat's top, less a gap, and never past
        // the container's own bottom, where the game would start squeezing them together.
        private float Room()
        {
            float height = container.rect.height - stretch;
            if (!chat) return height;
            chat.GetWorldCorners(corners);
            // The chat and the cards' cluster both sit on the spring canvas's plane, so the chat's top left corner
            // comes into the container's units directly.
            float chatTop = container.InverseTransformPoint(corners[1]).y;
            return Mathf.Min(height, container.rect.yMax - chatTop - ChatGap);
        }

        // The game steps each card down by its height but at most the container's height over the number of cards,
        // the hidden ones included, so with many allies sharing cards or folded into the more row, the cards shown
        // would be squeezed over each other. The container's bottom is pulled down until each card gets its height;
        // the cards shown still end above the chat.
        private void Stretch(float needed)
        {
            if (!container) return;
            float own = container.rect.height - stretch;
            float wanted = Mathf.Max(0f, needed - own);
            if (Mathf.Abs(wanted - stretch) < SizeTolerance) return;
            container.offsetMin -= new Vector2(0f, wanted - stretch);
            stretch = wanted;
        }

        private AllyCompactCard LookOf(AllyCardController card)
        {
            if (!looks.TryGetValue(card, out AllyCompactCard look))
            {
                look = new AllyCompactCard(card);
                looks.Add(card, look);
            }
            return look;
        }

        private void ForgetGoneCards()
        {
            foreach (AllyCardController card in looks.Keys)
            {
                if (!card)
                {
                    goneCards.Add(card);
                }
            }
            foreach (AllyCardController card in shared.Keys)
            {
                if (!card)
                {
                    goneCards.Add(card);
                }
            }
            foreach (AllyCardController card in goneCards)
            {
                looks.Remove(card);
                shared.Remove(card);
            }
            goneCards.Clear();
        }

        private void StyleCard(AllyCardController card)
        {
            Image panel = card.GetComponent<Image>();
            if (panel)
            {
                recorder.Record(panel);
                panel.sprite = null;
                panel.type = Image.Type.Simple;
                panel.color = Palette.Panel;
            }
            if (!card.healthBar) return;
            FlatHealthStyles.Apply(card.healthBar, recorder);
            Transform trough = card.healthBar.transform.Find(TroughName);
            Image troughImage = trough ? trough.GetComponent<Image>() : null;
            if (troughImage)
            {
                recorder.Record(troughImage);
                troughImage.sprite = null;
                troughImage.type = Image.Type.Simple;
                troughImage.color = Palette.Trough;
            }
        }

        public void Describe(StringBuilder output)
        {
            output.AppendLine($"  restyled ally card values: {recorder.Count}");
            StringBuilder folded = new StringBuilder();
            foreach (Section section in active)
            {
                if (section.HiddenAllies == 0) continue;
                CharacterBody owner = section.Player != null && section.Player.Lead.sourceMaster ?
                    section.Player.Lead.sourceMaster.GetBody() : null;
                folded.Append($" [{(owner ? owner.GetUserName() : "viewer's own")}: {section.HiddenAllies} allies in " +
                              $"{section.Allies.Count - section.Shown} of {section.Allies.Count} kinds folded]");
            }
            output.AppendLine($"  ally cards: {cards.Count} in {kinds.Count} kinds, {shared.Count} shared, " +
                              $"{(Compact ? "single allies compact" : "full size")}, allies under the more row: {HiddenCount}, " +
                              $"room above the chat: {(container ? Room().ToString("0.#") : "none")}, container stretched {stretch:0.#}");
            output.AppendLine($"  ally sections: {active.Count} (players, then the viewer's own allies), folded:{(folded.Length > 0 ? folded.ToString() : " none")}");
        }
    }
}
