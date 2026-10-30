using System.Collections.Generic;
using System.Text;
using RoR2.UI;
using TMPro;
using UnityEngine;

namespace SevenUI
{
    // Switches the game's Bombardier font to Barlow on every text under one HUD, and can put the originals back.
    internal sealed class HudFontSwapper
    {
        // The Bombardier font assets (tmpBombDropShadow and its variants). Languages the game draws in Noto Sans
        // (Russian, Chinese, Japanese, Korean, ...) don't use them, so their text stays as it is.
        private const string GameFontPrefix = "tmpBomb";

        private readonly Transform root;
        private readonly Dictionary<TMP_Text, (TMP_FontAsset font, Material material)> originals =
            new Dictionary<TMP_Text, (TMP_FontAsset, Material)>();
        // Texts whose wake-up font setting the mod turned off (see Apply), to turn back on when it restores them.
        private readonly HashSet<HGTextMeshProUGUI> languageFontTexts = new HashSet<HGTextMeshProUGUI>();
        private readonly List<TMP_Text> scratch = new List<TMP_Text>();
        private bool warnedBakeFailed;

        public HudFontSwapper(Transform root)
        {
            this.root = root;
        }

        public int SwappedCount => originals.Count;

        // Swaps every text still in the game's font. Cheap to repeat, but walks the whole hierarchy, so call it only
        // when the hierarchy changed.
        public void Apply()
        {
            TMP_FontAsset languageFont = HGTextMeshProUGUI.defaultLanguageFont;
            bool languageUsesGameFont = !languageFont || languageFont.name.StartsWith(GameFontPrefix);
            root.GetComponentsInChildren(true, scratch);
            foreach (TMP_Text text in scratch)
            {
                TMP_FontAsset font = text.font;
                if (!font || !font.name.StartsWith(GameFontPrefix)) continue;

                // An HGTextMeshProUGUI sets the language's font when it first wakes, which for one still inactive
                // (a boss bar before any boss) comes after this swap. In a Noto Sans language that font is the right
                // one, so the text is left for it; otherwise the wake-up setting is turned off below so Barlow stays.
                HGTextMeshProUGUI languageText = text as HGTextMeshProUGUI;
                bool setsLanguageFont = languageText && languageText.useLanguageDefaultFont;
                if (setsLanguageFont && !languageUsesGameFont) continue;

                Material material = text.fontSharedMaterial;
                Material template = material ? material : font.material;
                TMP_FontAsset barlow = UsesCondensed(text) ? FontLibrary.Condensed(template) : FontLibrary.Body(template);
                if (!barlow)
                {
                    if (!warnedBakeFailed)
                    {
                        warnedBakeFailed = true;
                        SevenUIPlugin.Log.LogWarning("Barlow couldn't be baked, so the HUD keeps the game's font");
                    }
                    return;
                }

                if (!originals.ContainsKey(text))
                {
                    originals[text] = (font, material);
                }
                if (setsLanguageFont)
                {
                    languageText.useLanguageDefaultFont = false;
                    languageFontTexts.Add(languageText);
                }
                FontLibrary.AddFallback(barlow, font);
                text.font = barlow;
                // Setting the font resets the material to the font's own, so put the retargeted preset back after.
                text.fontSharedMaterial = FontLibrary.Retarget(template, barlow);
            }
            scratch.Clear();
        }

        // Numbers (the run timer, the player's level, item and buff stacks, and the cooldowns and stocks on skills and
        // equipment) read best in Barlow Condensed, as in the design canvas, as do the boss's name in capitals and the
        // dollar sign (a currency's other text; the coins' signs are sprites).
        private static bool UsesCondensed(TMP_Text text)
        {
            if (text.GetComponent<TimerText>() || text.GetComponent<CondensedText>()) return true;
            LevelText level = text.GetComponentInParent<LevelText>();
            if (level && level.targetText == text) return true;
            SkillIcon skill = text.GetComponentInParent<SkillIcon>();
            if (skill && (skill.cooldownText == text || skill.stockText == text)) return true;
            EquipmentIcon equipment = text.GetComponentInParent<EquipmentIcon>();
            if (equipment && (equipment.cooldownText == text || equipment.stockText == text)) return true;
            ItemIcon item = text.GetComponentInParent<ItemIcon>();
            if (item && item.stackText == text) return true;
            BuffIcon buff = text.GetComponentInParent<BuffIcon>();
            if (buff && buff.stackCount == text) return true;
            MoneyText currency = text.GetComponentInParent<MoneyText>();
            if (currency && currency.targetText != text) return true;
            HUDBossHealthBarController boss = text.GetComponentInParent<HUDBossHealthBarController>();
            return boss && boss.bossNameLabel == text;
        }

        public void Restore()
        {
            foreach (KeyValuePair<TMP_Text, (TMP_FontAsset font, Material material)> entry in originals)
            {
                if (!entry.Key) continue;
                entry.Key.font = entry.Value.font;
                if (entry.Value.material)
                {
                    entry.Key.fontSharedMaterial = entry.Value.material;
                }
            }
            originals.Clear();
            foreach (HGTextMeshProUGUI text in languageFontTexts)
            {
                if (text)
                {
                    text.useLanguageDefaultFont = true;
                }
            }
            languageFontTexts.Clear();
        }

        // For the dump key: how many texts use each font, and what was swapped.
        public void Describe(StringBuilder output)
        {
            Dictionary<string, int> fonts = new Dictionary<string, int>();
            root.GetComponentsInChildren(true, scratch);
            foreach (TMP_Text text in scratch)
            {
                string key = (text.font ? text.font.name : "(none)") + " / " +
                             (text.fontSharedMaterial ? text.fontSharedMaterial.name : "(none)");
                fonts.TryGetValue(key, out int count);
                fonts[key] = count + 1;
            }
            scratch.Clear();
            output.AppendLine($"  swapped texts: {originals.Count}");
            foreach (KeyValuePair<string, int> entry in fonts)
            {
                output.AppendLine($"  {entry.Value,4} x {entry.Key}");
            }
        }
    }
}
