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
            root.GetComponentsInChildren(true, scratch);
            foreach (TMP_Text text in scratch)
            {
                TMP_FontAsset font = text.font;
                // A text already swapped can be back in the game's font: an HGTextMeshProUGUI sets the language's
                // font when it first wakes, which for one swapped while inactive comes after the swap.
                if (!font || !font.name.StartsWith(GameFontPrefix)) continue;

                Material material = text.fontSharedMaterial;
                Material template = material ? material : font.material;
                TMP_FontAsset barlow = IsNumber(text) ? FontLibrary.Condensed(template) : FontLibrary.Body(template);
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
                FontLibrary.AddFallback(barlow, font);
                text.font = barlow;
                // Setting the font resets the material to the font's own, so put the retargeted preset back after.
                text.fontSharedMaterial = FontLibrary.Retarget(template, barlow);
            }
            scratch.Clear();
        }

        // The run timer, the player's level, and the cooldowns and stocks on skills and equipment are numbers only,
        // which read best in Barlow Condensed, as in the design canvas.
        private static bool IsNumber(TMP_Text text)
        {
            if (text.GetComponent<TimerText>()) return true;
            LevelText level = text.GetComponentInParent<LevelText>();
            if (level && level.targetText == text) return true;
            SkillIcon skill = text.GetComponentInParent<SkillIcon>();
            if (skill && (skill.cooldownText == text || skill.stockText == text)) return true;
            EquipmentIcon equipment = text.GetComponentInParent<EquipmentIcon>();
            return equipment && (equipment.cooldownText == text || equipment.stockText == text);
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
