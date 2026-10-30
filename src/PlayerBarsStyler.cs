using System.Text;
using RoR2.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SevenUI
{
    // Restyles the player's level, experience, health and buffs in the bottom left, laid out as on the design canvas:
    // the level in a block at the left, a small "LV" header over the number in the accent; beside it the health
    // numbers and regeneration (PlayerHealthReadout), the health bar under them, and a thin experience bar under that.
    // The bars are flat on a dark trough, and the buffs sit on squares in rows above it all, no wider than it
    // (BuffRows). The game lays the level and experience out in a row above the health bar; its layout group is turned
    // off, and the parts are anchored in place. The game keeps filling the bars and writing the level.
    internal sealed class PlayerBarsStyler
    {
        public const string BarRootsPath = "MainContainer/MainUIArea/SpringCanvas/BottomLeftCluster/BarRoots";
        // Vanilla object names under BarRoots.
        private const string HealthBarPath = "HealthbarRoot";
        private const string SpriteNumbersPath = "HealthbarRoot/Managed_Sprite_HP";
        private const string SlashPath = "HealthbarRoot/Slash";
        private const string LevelClusterPath = "LevelDisplayCluster";
        private const string LevelRootPath = "LevelDisplayCluster/LevelDisplayRoot";
        private const string LevelPrefixPath = "LevelDisplayCluster/LevelDisplayRoot/PrefixText";
        private const string ExpBarPath = "LevelDisplayCluster/ExpBarRoot";
        private const string ExpTroughPath = "LevelDisplayCluster/ExpBarRoot/ShrunkenRoot";
        private const string ExpFillPath = "LevelDisplayCluster/ExpBarRoot/ShrunkenRoot/FillPanel";
        private const string BuffsPath = "LevelDisplayCluster/BuffDisplayRoot";
        public const string ReadoutName = "SevenUIHealthReadout";

        // The design canvas's sizes, in its pixels, scaled to the cluster's units. From the bottom: the 4px experience
        // bar, 4px under the 22px health bar, and 4px above that the row of numbers, whose 18px text has its baseline
        // about 3.6px above the row's bottom. The level's block is 52px wide, 12px from the bars, with the number's
        // baseline near the bottom and the header's 37px up.
        private const float Canvas = HudUnits.PerCanvasPixel;
        private const float ExpHeight = 4f * Canvas;
        private const float BarGap = 4f * Canvas;
        private const float HealthHeight = 22f * Canvas;
        private const float TextBaseline = 7.6f * Canvas;
        private const float BarsHeight = 56f * Canvas;
        private const float LevelWidth = 52f * Canvas;
        private const float LevelGap = 12f * Canvas;
        private const float LevelBaseline = 1.7f * Canvas;
        private const float LevelHeaderBaseline = 36.8f * Canvas;
        private const float LevelFontSize = 34f * Canvas;
        private const float LevelHeaderFontSize = 11f * Canvas;
        private const float HealthFontSize = 18f * Canvas;
        private const float RegenFontSize = 14f * Canvas;
        // How far the buffs' bottom row sits above the numbers: the canvas's 10px gap and 14px margin, which leave room
        // for LookingGlass's timers under the squares.
        private const float BuffsGap = 24f * Canvas;
        // Tall enough for any of the texts, which are placed by their baselines at their boxes' middles.
        private const float LineBox = 60f;

        private readonly Transform root;
        private readonly StyleRecorder recorder = new StyleRecorder();
        private BuffRows buffRows;

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
            Place(bars);
        }

        public void Restore()
        {
            recorder.Restore();
            buffRows = null;
        }

        // Every frame while applied, so the buffs keep their rows as the game changes them.
        public void Update()
        {
            if (buffRows != null && buffRows.Alive)
            {
                buffRows.Update();
            }
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
            // its prefab still has under "Slash". Those stay hidden; the readout above the bar shows the same numbers.
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
            }
            if (slash)
            {
                recorder.SetActive(slash.gameObject, false);
                if (!healthBar.transform.Find(ReadoutName))
                {
                    AddReadout(healthBar, slash.GetComponent<TMP_Text>());
                }
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

        // The numbers at the left and the regeneration at the right, on one baseline above the bar, in the game label's
        // font so the font swap gives them Barlow like the rest of the HUD.
        private void AddReadout(HealthBar healthBar, TMP_Text fontFrom)
        {
            GameObject holder = NewUIObject(ReadoutName, healthBar.transform);
            RectTransform holderRect = (RectTransform)holder.transform;
            holderRect.anchorMin = new Vector2(0f, 1f);
            holderRect.anchorMax = Vector2.one;
            holderRect.pivot = new Vector2(0.5f, 0.5f);
            holderRect.sizeDelta = new Vector2(0f, LineBox);
            holderRect.anchoredPosition = new Vector2(0f, TextBaseline);
            PlayerHealthReadout readout = holder.AddComponent<PlayerHealthReadout>();
            readout.Bar = healthBar;
            readout.Health = AddLabel(holder.transform, "Health", TextAlignmentOptions.BaselineLeft, HealthFontSize, fontFrom);
            readout.Regen = AddLabel(holder.transform, "Regen", TextAlignmentOptions.BaselineRight, RegenFontSize, fontFrom);
            recorder.Add(holder);
        }

        private static TMP_Text AddLabel(Transform parent, string name, TextAlignmentOptions alignment, float size,
            TMP_Text fontFrom)
        {
            GameObject line = NewUIObject(name, parent);
            RectTransform rect = (RectTransform)line.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            TextMeshProUGUI label = line.AddComponent<TextMeshProUGUI>();
            if (fontFrom)
            {
                label.font = fontFrom.font;
                label.fontSharedMaterial = fontFrom.fontSharedMaterial;
            }
            label.fontSize = size;
            label.alignment = alignment;
            label.enableWordWrapping = false;
            label.overflowMode = TextOverflowModes.Overflow;
            label.raycastTarget = false;
            return label;
        }

        private void StyleLevel(Transform bars)
        {
            Transform levelRoot = bars.Find(LevelRootPath);
            LevelText level = levelRoot ? levelRoot.GetComponent<LevelText>() : null;
            if (level && level.targetText)
            {
                recorder.Record(level.targetText);
                level.targetText.color = Palette.Accent;
                level.targetText.fontSize = LevelFontSize;
                level.targetText.alignment = TextAlignmentOptions.Baseline;
            }

            TMP_Text prefix = Find<TMP_Text>(bars, LevelPrefixPath);
            if (prefix)
            {
                recorder.Record(prefix);
                recorder.RecordText(prefix);
                HeaderStyle.Apply(prefix);
                prefix.fontSize = LevelHeaderFontSize;
                prefix.alignment = TextAlignmentOptions.Baseline;
                // "Lv:" becomes a header, which doesn't need the colon.
                string text = prefix.text;
                if (text.EndsWith(":") || text.EndsWith("："))
                {
                    prefix.text = text.Substring(0, text.Length - 1);
                }
            }
        }

        // A thin accent fill on the trough color. The game's trough is the faded-ends backdrop around a narrower
        // inner bar; the inner bar becomes the trough, the bar's whole height.
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

        private void StyleBuffs(Transform bars)
        {
            Transform buffs = bars.Find(BuffsPath);
            BuffDisplay display = buffs ? buffs.GetComponent<BuffDisplay>() : null;
            if (display && (buffRows == null || !buffRows.Alive))
            {
                buffRows = new BuffRows(display, (RectTransform)bars, recorder);
            }
        }

        // The bars' root, its layout group off, is as tall as the canvas's row, and its parts are anchored in it: the
        // level's block at the left, the bars beside it from the bottom up, and the buffs above. The level cluster,
        // which holds the level, the experience bar and the buffs, covers the whole root.
        private void Place(Transform bars)
        {
            RectTransform barsRect = (RectTransform)bars;
            if (recorder.HasRect(barsRect)) return;
            recorder.Disable(bars.GetComponent<VerticalLayoutGroup>());
            recorder.RecordRect(barsRect);
            barsRect.offsetMax = new Vector2(barsRect.offsetMax.x, barsRect.offsetMin.y + BarsHeight);

            float barsLeft = LevelWidth + LevelGap;
            float healthBottom = ExpHeight + BarGap;
            Anchor(bars.Find(LevelClusterPath), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Anchor(bars.Find(LevelRootPath), Vector2.zero, new Vector2(0f, 1f), Vector2.zero, new Vector2(LevelWidth, 0f));
            Anchor(bars.Find(ExpBarPath), Vector2.zero, new Vector2(1f, 0f), new Vector2(barsLeft, 0f),
                new Vector2(0f, ExpHeight));
            Anchor(bars.Find(ExpTroughPath), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Anchor(bars.Find(HealthBarPath), Vector2.zero, new Vector2(1f, 0f), new Vector2(barsLeft, healthBottom),
                new Vector2(0f, healthBottom + HealthHeight));
            Anchor(bars.Find(BuffsPath), new Vector2(0f, 1f), Vector2.one, new Vector2(0f, BuffsGap),
                new Vector2(0f, BuffsGap));

            // TMP puts a baseline-aligned line's baseline at its box's middle.
            Transform levelRoot = bars.Find(LevelRootPath);
            LevelText level = levelRoot ? levelRoot.GetComponent<LevelText>() : null;
            if (level && level.targetText)
            {
                AnchorLine(level.targetText.transform, LevelBaseline);
            }
            AnchorLine(bars.Find(LevelPrefixPath), LevelHeaderBaseline);
        }

        private void Anchor(Transform transform, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            if (!(transform is RectTransform rect)) return;
            recorder.RecordRect(rect);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        // A line across its parent whose baseline is the given height above the parent's bottom.
        private void AnchorLine(Transform transform, float baseline)
        {
            if (!(transform is RectTransform rect)) return;
            recorder.RecordRect(rect);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(0f, LineBox);
            rect.anchoredPosition = new Vector2(0f, baseline);
        }

        private static void FlattenTrough(Image image)
        {
            image.sprite = null;
            image.type = Image.Type.Simple;
            image.color = Palette.Trough;
        }

        private static GameObject NewUIObject(string name, Transform parent)
        {
            GameObject created = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
            created.layer = parent.gameObject.layer;
            created.transform.SetParent(parent, false);
            return created;
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
            PlayerHealthReadout readout = healthBar.GetComponentInChildren<PlayerHealthReadout>(true);
            output.AppendLine($"  health bar style={(healthBar._style ? healthBar._style.name : "none")} " +
                              $"ally style={(healthBar._allyStyle ? healthBar._allyStyle.name : "none")} " +
                              $"sprite digits={(bool)healthBar.spriteAsNumberManager} " +
                              $"readout={(readout ? $"'{readout.Health.text}' '{readout.Regen.text}' low={readout.ShownLow}" : "none")}");
        }
    }
}
