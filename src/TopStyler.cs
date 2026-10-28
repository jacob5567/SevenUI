using System.Collections.Generic;
using System.Text;
using RoR2.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SevenUI
{
    // Restyles the top of the HUD: each currency on its own flat panel without the cluster's frame, the item bar
    // without its frame, and the boss bar flat with the boss's name in Barlow Condensed capitals.
    internal sealed class TopStyler
    {
        private const string UpperLeftPath = "MainContainer/MainUIArea/SpringCanvas/UpperLeftCluster";
        private const string TopCenterPath = "MainContainer/MainUIArea/SpringCanvas/TopCenterCluster";
        // Under each currency's MoneyText object.
        private const string CurrencyBackgroundName = "BackgroundPanel";
        // Under the boss bar's container.
        private const string BossTroughName = "BossHealthBarContainer";
        private const string BossShieldPath = "BossHealthBarContainer/BackgroundPanel/ShieldPanel";

        // In HUD canvas units, where the game draws the boss's name at 32.
        private const float BossNameFontSize = 28f;
        // 1px at 28px, in hundredths of an em.
        private const float BossNameTracking = 4f;

        private readonly Transform root;
        private readonly StyleRecorder recorder = new StyleRecorder();
        private readonly List<MoneyText> currencies = new List<MoneyText>();

        public TopStyler(Transform root)
        {
            this.root = root;
        }

        public void Apply()
        {
            Transform upperLeft = root.Find(UpperLeftPath);
            if (upperLeft)
            {
                StyleCurrencies(upperLeft);
            }
            Transform topCenter = root.Find(TopCenterPath);
            if (topCenter)
            {
                foreach (ItemInventoryDisplay items in topCenter.GetComponentsInChildren<ItemInventoryDisplay>(true))
                {
                    recorder.Disable(items.GetComponent<Image>());
                }
                foreach (HUDBossHealthBarController boss in topCenter.GetComponentsInChildren<HUDBossHealthBarController>(true))
                {
                    StyleBossBar(boss);
                }
            }
        }

        public void Restore()
        {
            recorder.Restore();
        }

        // The cluster's frame goes, and the soft strip behind each currency becomes a flat panel.
        private void StyleCurrencies(Transform cluster)
        {
            recorder.Disable(cluster.GetComponent<Image>());
            cluster.GetComponentsInChildren(true, currencies);
            foreach (MoneyText currency in currencies)
            {
                Transform background = currency.transform.Find(CurrencyBackgroundName);
                RawImage strip = background ? background.GetComponent<RawImage>() : null;
                if (strip)
                {
                    recorder.Record(strip);
                    strip.texture = null;
                    strip.color = Palette.Panel;
                }
                if (currency.targetText)
                {
                    recorder.Record(currency.targetText);
                    currency.targetText.fontStyle |= FontStyles.Bold;
                }
            }
            currencies.Clear();
        }

        // The boss bar's fills are filled images whose sprite the game sets once, so a flat sprite stays.
        private void StyleBossBar(HUDBossHealthBarController boss)
        {
            if (boss.container)
            {
                Transform trough = boss.container.transform.Find(BossTroughName);
                Image troughImage = trough ? trough.GetComponent<Image>() : null;
                if (troughImage)
                {
                    recorder.Record(troughImage);
                    troughImage.sprite = null;
                    troughImage.type = Image.Type.Simple;
                    troughImage.color = Palette.Trough;
                }
                Transform shield = boss.container.transform.Find(BossShieldPath);
                FlattenFill(shield ? shield.GetComponent<Image>() : null, null);
            }
            FlattenFill(boss.fillRectImage, Palette.BossHealth);
            FlattenFill(boss.delayRectImage, Palette.BossHealthLost);

            if (boss.bossNameLabel)
            {
                recorder.Record(boss.bossNameLabel);
                boss.bossNameLabel.fontSize = BossNameFontSize;
                boss.bossNameLabel.fontStyle |= FontStyles.UpperCase;
                boss.bossNameLabel.characterSpacing = BossNameTracking;
                boss.bossNameLabel.color = Palette.Text;
            }
            if (boss.bossSubtitleLabel)
            {
                recorder.Record(boss.bossSubtitleLabel);
                boss.bossSubtitleLabel.color = Palette.HeaderText;
            }
            if (boss.healthLabel)
            {
                recorder.Record(boss.healthLabel);
                boss.healthLabel.fontStyle |= FontStyles.Bold;
                boss.healthLabel.color = Palette.Text;
            }
        }

        // Keeps the image's fill type, which is how the game shows the amount; null keeps the game's color.
        private void FlattenFill(Image image, Color? color)
        {
            if (!image) return;
            recorder.Record(image);
            image.sprite = FlatSprite.White;
            if (color.HasValue)
            {
                image.color = color.Value;
            }
        }

        public void Describe(StringBuilder output)
        {
            output.AppendLine($"  restyled top values: {recorder.Count}");
        }
    }
}
