using System.Text;
using RoR2;
using RoR2.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SevenUI
{
    // Keeps one scoreboard row as on the design canvas while it shows: tinted in the accent if it is the viewer's own
    // (not while the collapsed board's tab, which has its own tint, takes it in),
    // the player's survivor and level ("Commando · Lv 17") under their name, the row's items under
    // that ("40 items · 22 9 4 2 1 2", the total in bold, then the common, uncommon, legendary, boss, lunar and void
    // counts in their tiers' colors), the name cut short before the money at the right of its line, and the
    // gamepad's focus outlines shown only while they are lit. The counts are read again when the player's inventory
    // changes, and the name's room when the money's text does.
    internal sealed class ScoreboardRow : MonoBehaviour
    {
        private const string Separator = " · ";
        private const string LevelToken = "HUD_LEVEL_PREFIX";
        // The canvas's word after the total, which the game has no translation of, so it shows in English only.
        private const string ItemsWord = " items";
        private const string English = "en";
        private const float Canvas = HudUnits.PerCanvasPixel;
        // Between the name and the money on its line.
        private const float MoneyGap = 8f * Canvas;
        private static readonly StringBuilder Text = new StringBuilder();

        // The canvas's tint for the viewer's row, over its board, as drawn in the game's light (CanvasBlend).
        public static readonly Color OwnRowColor = CanvasBlend.Over(Palette.ScoreboardOwnRow, ScoreboardStyler.CanvasBoard);

        public ScoreboardStrip Strip;
        public HUD Hud;
        public Image Background;
        public TMP_Text Survivor;
        public TMP_Text Counts;
        public TMP_Text Name;
        // The money at the right of the name's line.
        public TMP_Text Money;
        public Image[] FocusOutlines;
        // Set by the board while the row is in the collapsed board's tab (ScoreboardTabShape).
        public bool InTab;

        private CharacterMaster shownMaster;
        private CharacterMaster shownViewer;
        private bool tintedInTab;
        private CharacterBody shownBody;
        private int shownLevel = -1;
        private Inventory countedInventory;
        private bool countsStale = true;
        private string shownMoney;
        private TMP_FontAsset shownMoneyFont;

        // A few reads and compares when nothing changed.
        private void LateUpdate()
        {
            if (!Strip) return;
            CharacterMaster master = Strip.master;
            CharacterMaster viewer = Hud && Hud.localUserViewer != null ? Hud.localUserViewer.cachedMaster : null;
            if (master != shownMaster || viewer != shownViewer || InTab != tintedInTab)
            {
                shownMaster = master;
                shownViewer = viewer;
                tintedInTab = InTab;
                Background.color = master && master == viewer && !InTab ? OwnRowColor : Color.clear;
            }

            CharacterBody body = master ? master.GetBody() : null;
            int level = body ? Mathf.FloorToInt(body.level) : -1;
            if (body != shownBody || level != shownLevel)
            {
                shownBody = body;
                shownLevel = level;
                Survivor.text = body ? body.GetDisplayName() + Separator + LevelPrefix() + " " + level : "";
            }

            Inventory inventory = master ? master.inventory : null;
            if (inventory != countedInventory)
            {
                Watch(inventory);
            }
            if (countsStale)
            {
                countsStale = false;
                Counts.text = inventory ? CountsText(inventory) : "";
            }

            if (Money && Name && (!string.Equals(Money.text, shownMoney, System.StringComparison.Ordinal) || Money.font != shownMoneyFont))
            {
                shownMoney = Money.text;
                shownMoneyFont = Money.font;
                Vector4 margin = Name.margin;
                Name.margin = new Vector4(margin.x, margin.y, Money.preferredWidth + MoneyGap, margin.w);
            }

            // The game lights an outline yellow while the gamepad focuses its part, and darkens it after.
            foreach (Image outline in FocusOutlines)
            {
                if (!outline) continue;
                bool lit = outline.color.r > 0.5f;
                if (outline.enabled != lit)
                {
                    outline.enabled = lit;
                }
            }
        }

        private void Watch(Inventory inventory)
        {
            if (countedInventory)
            {
                countedInventory.onInventoryChanged -= MarkStale;
            }
            countedInventory = inventory;
            if (inventory)
            {
                inventory.onInventoryChanged += MarkStale;
            }
            countsStale = true;
        }

        private void MarkStale()
        {
            countsStale = true;
        }

        private void OnDestroy()
        {
            Watch(null);
        }

        // Reads only: the inventory's count of each tier's items.
        private static string CountsText(Inventory inventory)
        {
            int total = 0;
            Text.Clear().Append(Separator);
            foreach ((ItemTier[] tiers, Color color) in TierGroups.Main)
            {
                int count = 0;
                foreach (ItemTier tier in tiers)
                {
                    count += inventory.GetTotalItemCountOfTier(tier);
                }
                total += count;
                Text.Append("<color=#").Append(ColorUtility.ToHtmlStringRGB(color)).Append('>').Append(count)
                    .Append("</color> ");
            }
            string tierCounts = Text.ToString().TrimEnd();
            Text.Clear().Append("<b><color=#").Append(ColorUtility.ToHtmlStringRGB(Palette.Text)).Append('>')
                .Append(total).Append(Language.currentLanguageName == English ? ItemsWord : "").Append("</color></b>")
                .Append(tierCounts);
            return Text.ToString();
        }

        // "Lv", the game's level header without its colon.
        private static string LevelPrefix()
        {
            string prefix = Language.GetString(LevelToken);
            return prefix.TrimEnd(':', '：');
        }
    }
}
