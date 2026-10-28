using System.Collections.Generic;
using System.Text;
using RoR2.UI;
using UnityEngine;
using UnityEngine.UI;

namespace SevenUI
{
    // Restyles the ally cards on the left: each on a flat panel, with a flat health bar on the dark trough like the
    // player's. The game keeps adding, filling and removing the cards.
    internal sealed class AllyCardsStyler
    {
        private const string LeftClusterPath = "MainContainer/MainUIArea/SpringCanvas/LeftCluster";
        // Under each card's health bar.
        private const string TroughName = "BackgroundPanel";

        private readonly Transform root;
        private readonly StyleRecorder recorder = new StyleRecorder();
        private readonly List<AllyCardController> cards = new List<AllyCardController>();

        public AllyCardsStyler(Transform root)
        {
            this.root = root;
        }

        public void Apply()
        {
            Transform cluster = root.Find(LeftClusterPath);
            if (!cluster) return;
            cluster.GetComponentsInChildren(true, cards);
            foreach (AllyCardController card in cards)
            {
                StyleCard(card);
            }
            cards.Clear();
        }

        public void Restore()
        {
            recorder.Restore();
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
        }
    }
}
