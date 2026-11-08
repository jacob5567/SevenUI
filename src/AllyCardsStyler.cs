using System.Collections.Generic;
using System.Text;
using RoR2.UI;
using UnityEngine;
using UnityEngine.UI;

namespace SevenUI
{
    // Restyles the ally cards on the left: each on a flat panel, with a flat health bar on the dark trough like the
    // player's. While the cards at the game's size would reach down over the chat, every card becomes a compact row
    // (AllyCompactCard). If even the rows would reach it, the rows that don't fit are hidden and one more row stands
    // in for them, with a portrait for each kind of ally hidden and "+N more" (AllyMoreRow). The game keeps adding,
    // filling, sorting and removing the cards; this only resizes and hides them and draws over them.
    internal sealed class AllyCardsStyler
    {
        private const string LeftClusterPath = "MainContainer/MainUIArea/SpringCanvas/LeftCluster";
        // Under each card's health bar.
        private const string TroughName = "BackgroundPanel";
        // Kept clear between the cards and the chat's messages, in the design canvas's pixels.
        private const float ChatGap = 8f * HudUnits.PerCanvasPixel;
        private const float SizeTolerance = 0.01f;

        private readonly Transform root;
        private readonly StyleRecorder recorder = new StyleRecorder();
        // In the game's order, top to bottom.
        private readonly List<AllyCardController> cards = new List<AllyCardController>();
        private readonly Dictionary<AllyCardController, AllyCompactCard> looks = new Dictionary<AllyCardController, AllyCompactCard>();
        private readonly List<AllyCardController> gone = new List<AllyCardController>();
        private readonly Vector3[] corners = new Vector3[4];
        // The cards' container (the game's AllyCardManager, which stacks them) and the chat's message area.
        private RectTransform container;
        private RectTransform chat;
        private AllyMoreRow more;
        // How far the container's bottom is pulled down past its own (see Stretch).
        private float stretch;

        public AllyCardsStyler(Transform root)
        {
            this.root = root;
        }

        public bool Compact { get; private set; }
        public int HiddenCount { get; private set; }

        public void Apply()
        {
            Transform cluster = root.Find(LeftClusterPath);
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
            foreach (AllyCompactCard look in looks.Values)
            {
                look.Revert();
            }
            looks.Clear();
            more?.Destroy();
            more = null;
            Stretch(0f);
            cards.Clear();
            Compact = false;
            HiddenCount = 0;
            recorder.Restore();
        }

        // Every frame while the panels are on: the game adds and removes cards as allies come and go. A few reads per
        // card; a card is only changed when its look or place in the stack changes.
        public void Update()
        {
            cards.RemoveAll(card => !card);
            ForgetGoneCards();
            if (!container || cards.Count == 0)
            {
                more?.Hide();
                return;
            }
            float room = Room();
            float fullHeight = 0f;
            foreach (AllyCardController card in cards)
            {
                fullHeight += LookOf(card).FullHeight;
            }
            bool compact = fullHeight > room + SizeTolerance;
            int shown = cards.Count;
            float row = AllyCompactCard.RowHeight;
            if (compact && cards.Count * row > room + SizeTolerance)
            {
                // The more row takes the last row's place.
                shown = Mathf.Max(0, Mathf.FloorToInt((room - row) / row + SizeTolerance));
            }
            for (int i = 0; i < cards.Count; i++)
            {
                LookOf(cards[i]).Set(compact, i >= shown);
            }
            Compact = compact;
            HiddenCount = cards.Count - shown;
            Stretch(compact ? cards.Count * row : 0f);
            if (HiddenCount > 0)
            {
                if (more == null || !more.Alive)
                {
                    more = new AllyMoreRow(container, cards[0].nameLabel);
                }
                more.Show(shown * row, cards, shown);
            }
            else
            {
                more?.Hide();
            }
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
        // hidden ones included, so a long list of compact rows would be squeezed together. The container's bottom is
        // pulled down until each row gets its height; the rows shown still end above the chat.
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
                    gone.Add(card);
                }
            }
            foreach (AllyCardController card in gone)
            {
                looks.Remove(card);
            }
            gone.Clear();
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
            output.AppendLine($"  ally cards: {cards.Count}, {(Compact ? "compact" : "full size")}, hidden under the more row: " +
                              $"{HiddenCount}, room above the chat: {(container ? Room().ToString("0.#") : "none")}, " +
                              $"container stretched {stretch:0.#}");
        }
    }
}
