using System.Collections.Generic;
using RoR2;
using RoR2.Items;
using UnityEngine;

namespace SevenUI
{
    // The line that says an item is tied to held items by void corruption (I2, Jacob's wording, 2026-11-22): "Corrupted
    // by Polylute" in the void color when a held void item would corrupt it, or for a void item "Corrupts your 2 Ukulele"
    // in the corrupted items' tier color. Counted from permanent stacks, as the pickers' counts are (H4). Reads only:
    // ContagiousItemManager's table and the synced inventory.
    internal static class Corruption
    {
        private const string CorruptedByText = "Corrupted by {0}";
        private const string CorruptsText = "Corrupts your {0}";
        // LookingGlass's own note in a picker option's full description, followed by the void item's description:
        // "\n\nHas been corrupted by: <style=cIsVoid>Polylute</style>\n\n" (CommandItemCountClass.CreateToolTip).
        private static readonly System.Text.RegularExpressions.Regex LookingGlassNote =
            new System.Text.RegularExpressions.Regex(@"\s*(Has been corrupted by:) <style=cIsVoid>(.*?)</style>\s*");
        // Space around the note, so the item's own description and the void item's read apart.
        private const string NoteGap = "\n<line-height=60%>\n</line-height>";

        // Whether the text has LookingGlass's note, which then says what the line would.
        public static bool HasLookingGlassNote(string text) => !string.IsNullOrEmpty(text) && LookingGlassNote.IsMatch(text);

        // LookingGlass's note in the void color, whole, set apart from the descriptions around it (I2, Jacob's call,
        // 2026-11-22).
        public static string StyleLookingGlassNote(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            return LookingGlassNote.Replace(text, match => NoteGap + "\n" + Colored(Palette.CountVoid, match.Groups[1].Value + " " +
                match.Groups[2].Value) + NoteGap + "\n");
        }

        // The held stacks behind the line: of the void item that would corrupt the item, and of the items a void item
        // would corrupt.
        public static void Counts(ItemIndex item, Inventory inventory, out int into, out int originals)
        {
            into = 0;
            originals = 0;
            if (!inventory || item == ItemIndex.None) return;
            ItemIndex transformed = ContagiousItemManager.GetTransformedItemIndex(item);
            if (transformed != ItemIndex.None)
            {
                into = inventory.GetItemCountPermanent(transformed);
            }
            foreach (ContagiousItemManager.TransformationInfo info in ContagiousItemManager.transformationInfos)
            {
                if (info.transformedItem == item)
                {
                    originals += inventory.GetItemCountPermanent(info.originalItem);
                }
            }
        }

        // The line as rich text, or "" when no held item ties to the item.
        public static string Line(ItemIndex item, Inventory inventory)
        {
            Counts(item, inventory, out int into, out int originals);
            if (into > 0)
            {
                return Colored(Palette.CountVoid, string.Format(CorruptedByText, Name(ContagiousItemManager.GetTransformedItemIndex(item))));
            }
            if (originals == 0) return "";
            List<string> held = new List<string>();
            Color color = Palette.Text;
            foreach (ContagiousItemManager.TransformationInfo info in ContagiousItemManager.transformationInfos)
            {
                if (info.transformedItem != item) continue;
                int count = inventory.GetItemCountPermanent(info.originalItem);
                if (count <= 0) continue;
                if (held.Count == 0)
                {
                    ItemDef first = ItemCatalog.GetItemDef(info.originalItem);
                    color = first ? GameColors.OfTier(first.tier, Palette.Text) : Palette.Text;
                }
                held.Add(count + " " + Name(info.originalItem));
            }
            return Colored(color, string.Format(CorruptsText, string.Join(", ", held)));
        }

        private static string Name(ItemIndex item)
        {
            ItemDef def = ItemCatalog.GetItemDef(item);
            return def ? Language.GetString(def.nameToken) : "";
        }

        private static string Colored(Color color, string text) => "<color=#" + ColorUtility.ToHtmlStringRGB(color) + ">" + text + "</color>";
    }
}
