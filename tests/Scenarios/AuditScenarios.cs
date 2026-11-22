using System.Collections;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using ModTestRunner;
using RoR2;
using UnityEngine;

namespace SevenUI.Tests
{
    // H11: screenshots of Jacob's other mods (KarmaWarnings, ReincarnationDisplay, SaleStarReminder, RecipeHelper) with
    // SevenUI, in its look and the game's, for the audit of where they clash. Each mod's DebugAlwaysShow forces its
    // display on, so nothing has to be staged. They check nothing; run them with --suite audit. A mod not installed
    // skips its scenario.
    internal static class AuditScenarios
    {
        private const float SettleSeconds = 1f;
        // Enough different items for two rows in the item bar.
        private const int ItemBarRows = 30;
        private const string MealPrep = "RoR2/DLC3/MealPrep/MealPrep.prefab";

        public static IEnumerable<Scenario> All()
        {
            yield return new Scenario(1, "audit_karma_warnings", Stages.TitanicPlains, KarmaWarnings);
            yield return new Scenario(1, "audit_reincarnation_display", Stages.TitanicPlains, ReincarnationDisplay);
            yield return new Scenario(1, "audit_sale_star_reminder", Stages.TitanicPlains, SaleStarReminder);
            yield return new Scenario(1, "audit_recipe_helper", Stages.TitanicPlains, RecipeHelper);
        }

        // The banner above the crosshair, then with the Mithrix preview's banner lines and its panel under the
        // objectives.
        private static IEnumerator KarmaWarnings(Harness h)
        {
            BaseUnityPlugin mod = Mod(h, "KarmaWarnings");
            Set(h, mod, "Debug", "DebugAlwaysShow", true);
            yield return h.AimAway();
            yield return h.Wait(SettleSeconds);
            yield return h.CaptureLooks("banner");
            foreach (ConfigDefinition key in mod.Config.Keys)
            {
                if (key.Section != "General" || key.Key != "MithrixPreview") continue;
                ConfigEntryBase preview = mod.Config[key];
                object before = preview.BoxedValue;
                h.OnCleanup(() => preview.BoxedValue = before);
                foreach (string mode in new[] { "Banner", "Sidebar" })
                {
                    preview.BoxedValue = System.Enum.Parse(preview.SettingType, mode);
                    yield return h.Wait(SettleSeconds);
                    yield return h.CaptureLooks("mithrix_" + mode.ToLowerInvariant());
                }
            }
        }

        // The panel and the hint above the N'tormat's icon, so the N'tormat is held.
        private static IEnumerator ReincarnationDisplay(Harness h)
        {
            BaseUnityPlugin mod = Mod(h, "ReincarnationDisplay");
            yield return h.GiveNtormat();
            Set(h, mod, "Debug", "DebugAlwaysShow", true);
            yield return h.AimAway();
            yield return h.Wait(SettleSeconds);
            yield return h.CaptureLooks();
        }

        // The reminder under the item bar, with its most-valuable-chest line.
        private static IEnumerator SaleStarReminder(Harness h)
        {
            BaseUnityPlugin mod = Mod(h, "SaleStarReminder");
            Set(h, mod, "Debug", "DebugAlwaysShow", true);
            foreach (ConfigDefinition key in mod.Config.Keys)
            {
                if (key.Key.IndexOf("Valuable", System.StringComparison.OrdinalIgnoreCase) >= 0 && mod.Config[key] is ConfigEntry<bool> line)
                {
                    bool before = line.Value;
                    h.OnCleanup(() => line.Value = before);
                    line.Value = true;
                }
            }
            yield return h.AimAway();
            yield return h.Wait(SettleSeconds);
            yield return h.CaptureLooks();
            // With two rows in the item bar, which the reminder should stay under.
            ScoreboardSetUp.GiveDifferentItems(h, ItemBarRows);
            yield return h.Wait(SettleSeconds);
            yield return h.CaptureLooks("full_bar");
        }

        // The sidebar beside the CHEF's dialog, listing every recipe (DebugAlwaysShow), with the dialog opened from a
        // Wandering CHEF spawned before the player.
        private static IEnumerator RecipeHelper(Harness h)
        {
            BaseUnityPlugin mod = Mod(h, "RecipeHelper");
            Set(h, mod, "Debug", "DebugAlwaysShow", true);
            NetworkUIPromptController prompt = h.SpawnPrefab<NetworkUIPromptController>(MealPrep);
            if (!prompt) throw h.Skip("no Wandering CHEF to spawn");
            Interactor interactor = Harness.LocalBody.GetComponent<Interactor>();
            h.OnCleanup(() =>
            {
                if (prompt) prompt.ClearParticipant();
            });
            prompt.SetParticipantMasterFromInteractor(interactor);
            yield return h.Until(() => Object.FindObjectOfType<RoR2.UI.CraftingPanel>(), "the CHEF's dialog open");
            yield return h.Wait(SettleSeconds * 2f);
            yield return h.CaptureLooks();
        }

        private static BaseUnityPlugin Mod(Harness h, string name)
        {
            if (!Chainloader.PluginInfos.TryGetValue("sevenstack." + name, out PluginInfo info) || !info.Instance)
            {
                throw h.Skip(name + " isn't installed");
            }
            return info.Instance;
        }

        private static void Set(Harness h, BaseUnityPlugin mod, string section, string key, bool value)
        {
            if (!mod.Config.TryGetEntry(section, key, out ConfigEntry<bool> entry)) throw h.Fail($"{mod.Info.Metadata.Name} has no {section}.{key}");
            bool before = entry.Value;
            h.OnCleanup(() => entry.Value = before);
            entry.Value = value;
        }
    }
}
