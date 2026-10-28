using System.Collections.Generic;
using System.Text;
using RoR2.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SevenUI
{
    // Restyles the player's level, experience, health and buffs in the bottom left: flat bars on a dark trough, the
    // health numbers as text instead of sprite digits, the level under a small "LV" header, and bold buff counts. The game keeps filling the
    // bars and writing the numbers.
    internal sealed class PlayerBarsStyler
    {
        private const string BarRootsPath = "MainContainer/MainUIArea/SpringCanvas/BottomLeftCluster/BarRoots";
        // Vanilla object names under BarRoots.
        private const string HealthBarPath = "HealthbarRoot";
        private const string SpriteNumbersPath = "HealthbarRoot/Managed_Sprite_HP";
        private const string SlashPath = "HealthbarRoot/Slash";
        private const string LevelRootPath = "LevelDisplayCluster/LevelDisplayRoot";
        private const string LevelPrefixPath = "LevelDisplayCluster/LevelDisplayRoot/PrefixText";
        private const string ExpBarPath = "LevelDisplayCluster/ExpBarRoot";
        private const string ExpTroughPath = "LevelDisplayCluster/ExpBarRoot/ShrunkenRoot";
        private const string ExpFillPath = "LevelDisplayCluster/ExpBarRoot/ShrunkenRoot/FillPanel";
        private const string BuffsPath = "LevelDisplayCluster/BuffDisplayRoot";

        // In HUD canvas units, where the game draws the health numbers' text at 24 and its sprite digits about 16 high.
        private const float HealthFontSize = 18f;

        private readonly Transform root;
        private readonly StyleRecorder recorder = new StyleRecorder();
        private readonly List<BuffIcon> buffIcons = new List<BuffIcon>();

        public PlayerBarsStyler(Transform root)
        {
            this.root = root;
        }

        public void Apply()
        {
            Transform bars = root.Find(BarRootsPath);
            if (!bars) return;
            StyleHealthBar(bars);
            StyleLevel(bars);
            StyleExpBar(bars);
            StyleBuffs(bars);
        }

        public void Restore()
        {
            recorder.Restore();
        }

        private void StyleHealthBar(Transform bars)
        {
            HealthBar healthBar = Find<HealthBar>(bars, HealthBarPath);
            if (!healthBar) return;

            Image trough = healthBar.GetComponent<Image>();
            if (trough)
            {
                recorder.Record(trough);
                FlattenTrough(trough);
            }

            FlatHealthStyles.Apply(healthBar, recorder);

            // The health bar writes its numbers as text when it has no sprite-digit manager, into the text labels
            // its prefab still has under "Slash".
            Transform slash = bars.Find(SlashPath);
            if (healthBar.spriteAsNumberManager && slash)
            {
                SpriteAsNumberManager spriteNumbers = healthBar.spriteAsNumberManager;
                healthBar.spriteAsNumberManager = null;
                ForceNumberRefresh(healthBar);
                recorder.OnRestore(() =>
                {
                    if (!healthBar) return;
                    healthBar.spriteAsNumberManager = spriteNumbers;
                    ForceNumberRefresh(healthBar);
                });
                recorder.SetActive(bars.Find(SpriteNumbersPath)?.gameObject, false);
                recorder.SetActive(slash.gameObject, true);
            }
            if (slash)
            {
                StyleHealthNumbers(healthBar, slash.GetComponent<TMP_Text>());
            }
        }

        // The current health reads first, in the bold face; the slash and full health are secondary.
        private void StyleHealthNumbers(HealthBar healthBar, TMP_Text slash)
        {
            foreach (TMP_Text label in new[] { slash, healthBar.currentHealthText, healthBar.fullHealthText })
            {
                if (!label) continue;
                recorder.Record(label);
                label.fontSize = HealthFontSize;
                bool current = label == healthBar.currentHealthText;
                label.fontStyle = current ? label.fontStyle | FontStyles.Bold : label.fontStyle & ~FontStyles.Bold;
                label.color = current ? Palette.Text : Palette.HeaderText;
            }
        }

        // The game writes a number only when it differs from the one it last wrote, so after switching between text
        // and sprite digits the new one would show nothing until health changed.
        private static void ForceNumberRefresh(HealthBar healthBar)
        {
            healthBar.displayStringCurrentHealth = -1f;
            healthBar.displayStringFullHealth = -1f;
            healthBar.oldHealth = -1f;
            healthBar.oldFullHealth = -1f;
        }

        private void StyleLevel(Transform bars)
        {
            Transform levelRoot = bars.Find(LevelRootPath);
            LevelText level = levelRoot ? levelRoot.GetComponent<LevelText>() : null;
            if (level && level.targetText)
            {
                recorder.Record(level.targetText);
                level.targetText.color = Palette.Accent;
            }

            TMP_Text prefix = Find<TMP_Text>(bars, LevelPrefixPath);
            if (prefix)
            {
                recorder.Record(prefix);
                recorder.RecordText(prefix);
                HeaderStyle.Apply(prefix);
                // "Lv:" becomes a header, which doesn't need the colon.
                string text = prefix.text;
                if (text.EndsWith(":") || text.EndsWith("："))
                {
                    prefix.text = text.Substring(0, text.Length - 1);
                }
            }
        }

        // A thin accent fill on the trough color. The game's trough is the faded-ends backdrop around a narrower
        // inner bar; the inner bar becomes the trough so the bar is thin.
        private void StyleExpBar(Transform bars)
        {
            recorder.Disable(Find<Image>(bars, ExpBarPath));
            Image trough = Find<Image>(bars, ExpTroughPath);
            if (trough)
            {
                recorder.Record(trough);
                recorder.SetEnabled(trough, true);
                FlattenTrough(trough);
            }
            Image fill = Find<Image>(bars, ExpFillPath);
            if (fill)
            {
                recorder.Record(fill);
                fill.sprite = FlatSprite.White;
                fill.type = Image.Type.Simple;
                fill.color = Palette.Accent;
            }
        }

        // The buffs' stack counts, and the timers LookingGlass writes into the same text, in bold Barlow Condensed.
        private void StyleBuffs(Transform bars)
        {
            Transform buffs = bars.Find(BuffsPath);
            if (!buffs) return;
            buffs.GetComponentsInChildren(true, buffIcons);
            foreach (BuffIcon buff in buffIcons)
            {
                if (!buff.stackCount) continue;
                recorder.Record(buff.stackCount);
                buff.stackCount.fontStyle |= FontStyles.Bold;
                buff.stackCount.color = Palette.Text;
            }
            buffIcons.Clear();
        }

        private static void FlattenTrough(Image image)
        {
            image.sprite = null;
            image.type = Image.Type.Simple;
            image.color = Palette.Trough;
        }

        private static T Find<T>(Transform parent, string path) where T : Component
        {
            Transform found = parent.Find(path);
            return found ? found.GetComponent<T>() : null;
        }

        public void Describe(StringBuilder output)
        {
            output.AppendLine($"  restyled player bar values: {recorder.Count}");
            Transform bars = root.Find(BarRootsPath);
            HealthBar healthBar = bars ? Find<HealthBar>(bars, HealthBarPath) : null;
            if (!healthBar) return;
            output.AppendLine($"  health bar style={(healthBar._style ? healthBar._style.name : "none")} " +
                              $"ally style={(healthBar._allyStyle ? healthBar._allyStyle.name : "none")} " +
                              $"sprite digits={(bool)healthBar.spriteAsNumberManager} " +
                              $"text={(healthBar.currentHealthText ? healthBar.currentHealthText.text : "-")}/" +
                              $"{(healthBar.fullHealthText ? healthBar.fullHealthText.text : "-")}");
        }
    }
}
