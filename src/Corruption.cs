using System.Collections.Generic;
using RoR2;
using RoR2.Items;
using UnityEngine;

namespace SevenUI
{
    // How an item's description says it is tied to held items by void corruption (I2, Jacob's wording, 2026-11-22). An
    // item a held void item would corrupt gets a line, "Corrupted by Polylute", in the void color. A void item that would
    // corrupt held items says so in its own sentence: the game's "Corrupts all Ukuleles" becomes "Corrupts your Ukulele
    // x2", in the same void style, so it isn't said twice (Jacob's pick after the I2 audit). Counted from permanent
    // stacks, as the pickers' counts are (H4). Reads only: ContagiousItemManager's table and the synced inventory.
    internal static class Corruption
    {
        private const string CorruptedByText = "Corrupted by {0}";
        private const string CorruptsText = "Corrupts your {0}";
        private const string HeldText = "{0} x{1}";
        // The game's sentence in a void item's description, in the void style and the last such span: "<style=cIsVoid>
        // Corrupts all Ukuleles</style>", or for Newly Hatched Zoea "<style=cIsVoid>Corrupts all </style><style=
        // cIsTierBoss>yellow items</style><style=cIsVoid></style>". The same in every language; Pluripotent Larva's
        // "corrupted" comes before it.
        private static readonly System.Text.RegularExpressions.Regex CorruptsSentence = new System.Text.RegularExpressions.Regex(
            @"<style=cIsVoid>(?!</style>)[^<]*</style>(?:<style=[^>]*>[^<]*</style>)*(?:<style=cIsVoid></style>)?",
            System.Text.RegularExpressions.RegexOptions.RightToLeft);
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

        // The line for an item a held void item would corrupt, as rich text, or "".
        public static string Line(ItemIndex item, Inventory inventory)
        {
            Counts(item, inventory, out int into, out _);
            if (into <= 0) return "";
            return Colored(Palette.CountVoid, string.Format(CorruptedByText, Name(ContagiousItemManager.GetTransformedItemIndex(item))));
        }

        // For a void item, what it would corrupt of the held items, as plain text ("Corrupts your Ukulele x2"), or "".
        public static string Corrupts(ItemIndex item, Inventory inventory)
        {
            if (!inventory || item == ItemIndex.None) return "";
            List<string> held = new List<string>();
            foreach (ContagiousItemManager.TransformationInfo info in ContagiousItemManager.transformationInfos)
            {
                if (info.transformedItem != item) continue;
                int count = inventory.GetItemCountPermanent(info.originalItem);
                if (count > 0)
                {
                    held.Add(string.Format(HeldText, Name(info.originalItem), count));
                }
            }
            return held.Count > 0 ? string.Format(CorruptsText, string.Join(", ", held)) : "";
        }

        // 'description' with its void sentence saying 'corrupts' (from Corrupts) instead, in the void style; with it
        // added on a line of its own when the description has no such sentence. 'description' as it is for no corrupts.
        public static string WithCorrupts(string description, string corrupts)
        {
            if (string.IsNullOrEmpty(corrupts) || description == null) return description;
            string sentence = "<style=cIsVoid>" + corrupts + "</style>";
            System.Text.RegularExpressions.Match match = CorruptsSentence.Match(description);
            return match.Success
                ? description.Substring(0, match.Index) + sentence + description.Substring(match.Index + match.Length)
                : description + "\n" + sentence;
        }

        private static string Name(ItemIndex item)
        {
            ItemDef def = ItemCatalog.GetItemDef(item);
            return def ? Language.GetString(def.nameToken) : "";
        }

        private static string Colored(Color color, string text) => "<color=#" + ColorUtility.ToHtmlStringRGB(color) + ">" + text + "</color>";
    }
}
