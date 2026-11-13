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
    // and allies between bodies keep cards of their own. While the cards would reach down over the chat, the cards of
    // a single ally become compact rows (AllyCompactCard), while shared cards keep their size and segments. If that is
    // still too tall, the cards that don't fit are hidden and one more row stands in for them, with a portrait for each
    // kind and "+N more", N the allies they hold (AllyMoreRow). The game keeps adding, filling, sorting and removing
    // the cards; this only resizes and hides them and draws over them. While the scoreboard is open, the cards are
    // hidden altogether, as they would otherwise draw over the board's left edge.
    internal sealed class AllyCardsStyler : IStyler
    {
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
        private readonly List<AllyCardController> overflow = new List<AllyCardController>();
        private readonly Dictionary<AllyCardController, AllyCompactCard> looks = new Dictionary<AllyCardController, AllyCompactCard>();
        private readonly Dictionary<AllyCardController, AllyKindParts> shared = new Dictionary<AllyCardController, AllyKindParts>();
        // Each ally's name as the game shows it, found once per body: a hidden card's own label stops updating.
        private readonly Dictionary<CharacterMaster, (CharacterBody Body, string Name)> names =
            new Dictionary<CharacterMaster, (CharacterBody, string)>();
        private readonly List<AllyCardController> goneCards = new List<AllyCardController>();
        private readonly List<CharacterMaster> goneMasters = new List<CharacterMaster>();
        private readonly Vector3[] corners = new Vector3[4];
        // The cards' container (the game's AllyCardManager, which stacks them) and the chat's message area.
        private RectTransform container;
        private RectTransform chat;
        private AllyMoreRow more;
        // Fades the whole stack out while the scoreboard is open.
        private CanvasGroup hider;
        // How far the container's bottom is pulled down past its own (see Stretch).
        private float stretch;
        // The game indents a minion's card under its leader by its manager's indentWidth; zero while the cards are
        // restyled, so every card starts at the same edge. The values are kept for Restore, per manager touched.
        private readonly Dictionary<AllyCardManager, float> savedIndents = new Dictionary<AllyCardManager, float>();

        private sealed class Kind
        {
            // The kind's first card, which the kind shares.
            public AllyCardController Lead;
            public CharacterMaster Leader;
            // None for a card that stands alone.
            public string Name;
            public readonly List<CharacterMaster> Members = new List<CharacterMaster>();
            public readonly List<AllyCardController> Cards = new List<AllyCardController>();
            public bool Shared => Members.Count > 1;
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
            foreach (AllyCardController card in cards)
            {
                StyleCard(card);
            }
            if (!container)
            {
                AllyCardManager manager = cluster.GetComponentInChildren<AllyCardManager>(true);
                container = manager ? (RectTransform)manager.transform : null;
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
            more?.Destroy();
            more = null;
            foreach (KeyValuePair<AllyCardManager, float> entry in savedIndents)
            {
                if (entry.Key)
                {
                    entry.Key.indentWidth = entry.Value;
                    LayoutRebuilder.MarkLayoutForRebuild((RectTransform)entry.Key.transform);
                }
            }
            savedIndents.Clear();
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
                more?.Hide();
                return;
            }
            SortIntoKinds();
            float room = Room();
            float fullHeight = 0f;
            foreach (Kind kind in kinds)
            {
                fullHeight += LookOf(kind.Lead).FullHeight;
            }
            bool compact = fullHeight > room + SizeTolerance;
            float used = 0f;
            int shown = 0;
            float total = 0f;
            foreach (Kind kind in kinds)
            {
                total += Height(kind, compact);
            }
            if (total > room + SizeTolerance)
            {
                // The more row takes a compact row's room under the cards that fit.
                while (shown < kinds.Count && used + Height(kinds[shown], compact) + AllyCompactCard.RowHeight <= room + SizeTolerance)
                {
                    used += Height(kinds[shown], compact);
                    shown++;
                }
            }
            else
            {
                shown = kinds.Count;
                used = total;
            }

            float tallest = 0f;
            int hiddenAllies = 0;
            overflow.Clear();
            for (int i = 0; i < kinds.Count; i++)
            {
                Kind kind = kinds[i];
                bool fits = i < shown;
                ShowKind(kind, compact, fits);
                if (fits)
                {
                    tallest = Mathf.Max(tallest, Height(kind, compact));
                }
                else
                {
                    hiddenAllies += Mathf.Max(1, kind.Members.Count);
                    overflow.Add(kind.Lead);
                }
            }
            Compact = compact;
            HiddenCount = hiddenAllies;
            Stretch(cards.Count * tallest);
            if (overflow.Count > 0)
            {
                if (more == null || !more.Alive)
                {
                    more = new AllyMoreRow(container, cards[0].nameLabel, PaddingLeft(kinds[0].Lead));
                }
                more.Show(used, overflow, hiddenAllies, PaddingLeft(kinds[0].Lead));
            }
            else
            {
                more?.Hide();
            }
        }

        // A shared card keeps the game's height, with its segments; a card of one ally becomes a compact row when the
        // cards must be compact.
        private float Height(Kind kind, bool compact) =>
            compact && !kind.Shared ? AllyCompactCard.RowHeight : LookOf(kind.Lead).FullHeight;

        // The left padding of a card's row, where its portrait starts inside it.
        private static int PaddingLeft(AllyCardController card)
        {
            HorizontalLayoutGroup row = card ? card.GetComponent<HorizontalLayoutGroup>() : null;
            return row ? row.padding.left : 0;
        }

        // The game indents a minion's card under its leader by its manager's indentWidth, writing the card's offsets
        // only when it lays the cards out. The manager that holds the card may not be the one found first, so each
        // card's own parent is zeroed, and the card's offsets are put right whenever they differ.
        private void Unindent(AllyCardController card)
        {
            AllyCardManager manager = card.transform.parent ? card.transform.parent.GetComponent<AllyCardManager>() : null;
            if (manager && manager.indentWidth != 0f)
            {
                if (!savedIndents.ContainsKey(manager))
                {
                    savedIndents.Add(manager, manager.indentWidth);
                }
                manager.indentWidth = 0f;
            }
            RectTransform rect = card.rectTransform;
            if (rect.offsetMin.x != 0f || rect.offsetMax.x != 0f)
            {
                rect.offsetMin = new Vector2(0f, rect.offsetMin.y);
                rect.offsetMax = new Vector2(0f, rect.offsetMax.y);
            }
        }

        private void ShowKind(Kind kind, bool compact, bool fits)
        {
            foreach (AllyCardController card in kind.Cards)
            {
                Unindent(card);
                AllyCompactCard look = LookOf(card);
                if (card != kind.Lead)
                {
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
                look.Set(compact && !kind.Shared, !fits);
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
                kind.Name = null;
                kind.Members.Clear();
                kind.Cards.Clear();
                spareKinds.Push(kind);
            }
            kinds.Clear();
            foreach (AllyCardController card in cards)
            {
                CharacterMaster master = card.sourceMaster;
                // Players stand alone, as does an ally between bodies, which has no name to group by.
                string name = master && !master.playerCharacterMasterController ? NameOf(master) : null;
                CharacterMaster leader = master && master.minionOwnership ? master.minionOwnership.ownerMaster : null;
                Kind found = null;
                if (name != null)
                {
                    foreach (Kind kind in kinds)
                    {
                        if (kind.Name == name && kind.Leader == leader)
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
                    found.Name = name;
                    kinds.Add(found);
                }
                found.Cards.Add(card);
                if (master)
                {
                    found.Members.Add(master);
                }
            }
        }

        // The name the game's card shows (Util.GetBestBodyName, as AllyCardController reads it), found again only when
        // the ally's body changes.
        private string NameOf(CharacterMaster master)
        {
            CharacterBody body = master.GetBody();
            if (!body) return null;
            if (names.TryGetValue(master, out (CharacterBody Body, string Name) known) && known.Body == body) return known.Name;
            string name = Util.GetBestBodyName(body.gameObject);
            names[master] = (body, name);
            if (names.Count > cards.Count * 2)
            {
                ForgetGoneMasters();
            }
            return name;
        }

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

        private void ForgetGoneMasters()
        {
            foreach (CharacterMaster master in names.Keys)
            {
                if (!master)
                {
                    goneMasters.Add(master);
                }
            }
            foreach (CharacterMaster master in goneMasters)
            {
                names.Remove(master);
            }
            goneMasters.Clear();
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
            output.AppendLine($"  ally cards: {cards.Count} in {kinds.Count} kinds, {shared.Count} shared, " +
                              $"{(Compact ? "single allies compact" : "full size")}, allies under the more row: {HiddenCount}, " +
                              $"room above the chat: {(container ? Room().ToString("0.#") : "none")}, container stretched {stretch:0.#}");
        }
    }
}
