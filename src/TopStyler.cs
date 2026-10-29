using System.Collections.Generic;
using System.Text;
using RoR2.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SevenUI
{
    // Restyles the top of the HUD: the currencies in a row, each on its own flat panel fitted to it, without the
    // cluster's frame; the item bar without its frame; and the boss bar flat, with the boss's name in Barlow Condensed
    // capitals in a title row above it (BossTitleRow) and the health just lost trailing its fill (BossDamageTrail).
    internal sealed class TopStyler
    {
        private const string UpperLeftPath = "MainContainer/MainUIArea/SpringCanvas/UpperLeftCluster";
        private const string TopCenterPath = "MainContainer/MainUIArea/SpringCanvas/TopCenterCluster";
        // Under each currency's MoneyText object.
        private const string CurrencyBackgroundName = "BackgroundPanel";
        // Under the boss bar's container.
        // The container's backdrop is wider than the fills, its faded ends hiding the difference, so the trough goes on
        // the panel that holds the fills instead, whose own image the game leaves off.
        private const string BossBackdropName = "BossHealthBarContainer";
        private const string BossTroughPath = "BossHealthBarContainer/BackgroundPanel";
        private const string BossShieldPath = "BossHealthBarContainer/BackgroundPanel/ShieldPanel";

        // In HUD canvas units, where the game draws the boss's name at 32.
        private const float BossNameFontSize = 28f;
        // 1px at 28px, in hundredths of an em.
        private const float BossNameTracking = 4f;

        private readonly HUD hud;
        private readonly Transform root;
        private readonly StyleRecorder recorder = new StyleRecorder();
        private readonly List<MoneyText> currencies = new List<MoneyText>();
        private CurrencyRow currencyRow;
        private BossTitleRow bossRow;
        private BossDamageTrail bossTrail;

        public TopStyler(HUD hud, Transform root)
        {
            this.hud = hud;
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
            currencyRow = null;
            bossRow = null;
            bossTrail = null;
        }

        public void Update()
        {
            if (currencyRow != null && currencyRow.Alive)
            {
                currencyRow.Update();
            }
            if (bossRow != null && bossRow.Alive)
            {
                bossRow.Update();
            }
            if (bossTrail != null && bossTrail.Alive)
            {
                bossTrail.Update();
            }
        }

        // The cluster's frame goes, and the soft strip behind each currency becomes a flat panel.
        private void StyleCurrencies(Transform cluster)
        {
            if (currencyRow == null || !currencyRow.Alive)
            {
                currencyRow = new CurrencyRow(hud, (RectTransform)cluster, recorder);
            }
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
            }
            currencies.Clear();
        }

        // The boss bar's fills are filled images whose sprite the game sets once, so a flat sprite stays.
        private void StyleBossBar(HUDBossHealthBarController boss)
        {
            if (boss.container)
            {
                Transform backdrop = boss.container.transform.Find(BossBackdropName);
                recorder.Disable(backdrop ? backdrop.GetComponent<Image>() : null);
                Transform trough = boss.container.transform.Find(BossTroughPath);
                Image troughImage = trough ? trough.GetComponent<Image>() : null;
                if (troughImage)
                {
                    recorder.Record(troughImage);
                    troughImage.sprite = null;
                    troughImage.type = Image.Type.Simple;
                    troughImage.color = Palette.Trough;
                    recorder.SetEnabled(troughImage, true);
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
            if ((bossRow == null || !bossRow.Alive) && boss.container)
            {
                bossRow = BossTitleRow.Style(boss, recorder);
            }
            if ((bossTrail == null || !bossTrail.Alive) && boss.fillRectImage && boss.delayRectImage)
            {
                bossTrail = new BossDamageTrail(boss, recorder);
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
