using System.Collections;
using System.Collections.Generic;
using ModTestRunner;
using RoR2;
using UnityEngine;

namespace SevenUI.Tests
{
    // The FlatPanels setting: the top-right panels are flat, the run info shows the mod's difficulty bar, the objective
    // panel has a header strip, the top bars take the design canvas's width, and turning it off brings back the
    // game's panels and widths. LookingGlass's copy of the objective panel gets a plain panel.
    internal static class PanelScenarios
    {
        private const string Suite = "panels";
        // Plenty for a small chest at any difficulty or stage count the tests reach.
        private const int ChestMoney = 1000;
        // The game counts the shown amount up over about this long.
        private const float CountUpSeconds = 3f;

        public static IEnumerable<Scenario> All()
        {
            yield return new Scenario(1, Suite, "hud_faces_screen", Stages.TitanicPlains, HudFacesScreen);
            yield return new Scenario(1, Suite, "run_info_is_flat", Stages.TitanicPlains, RunInfoIsFlat);
            yield return new Scenario(1, Suite, "objective_panel_is_flat", Stages.TitanicPlains, ObjectivePanelIsFlat);
            yield return new Scenario(1, Suite, "player_bars_are_flat", Stages.TitanicPlains, PlayerBarsAreFlat);
            yield return new Scenario(1, Suite, "skill_slots_are_flat", Stages.TitanicPlains, SkillSlotsAreFlat);
            yield return new Scenario(1, Suite, "ntormat_pages_follow_key_item", Stages.TitanicPlains, NtormatPagesFollowKeyItem);
            yield return new Scenario(1, Suite, "top_is_flat", Stages.TitanicPlains, TopIsFlat);
            yield return new Scenario(1, Suite, "item_icons_are_flat", Stages.TitanicPlains, ItemIconsAreFlat);
            yield return new Scenario(1, Suite, "prompt_is_flat", Stages.TitanicPlains, PromptIsFlat);
            yield return new Scenario(1, Suite, "prompt_panel_fits_text", Stages.TitanicPlains, PromptPanelFitsText);
            yield return new Scenario(1, Suite, "scoreboard_and_chat_are_flat", Stages.TitanicPlains, ScoreboardAndChatAreFlat);
            yield return new Scenario(1, Suite, "turning_off_restores_panels", Stages.TitanicPlains, TurningOffRestoresPanels);
            yield return new Scenario(1, Suite, "lookingglass_stats_panel_is_plain", Stages.TitanicPlains, LookingGlassStatsPanelIsPlain);
        }

        private static IEnumerator HudFacesScreen(Harness h)
        {
            yield return h.ExpectHudLevel();
        }

        private static IEnumerator RunInfoIsFlat(Harness h)
        {
            yield return h.ExpectRunInfoFlat();
            yield return h.ExpectRunInfoAsCanvas();
        }

        // More max health changes both numbers, so the text has to keep up after the switch from sprite digits.
        private static IEnumerator PlayerBarsAreFlat(Harness h)
        {
            yield return h.ExpectPlayerBarsFlat();
            h.Cmd($"{Commands.GiveItem} {GameNames.Knurl} 1");
            yield return h.Wait(0.5f);
            yield return h.ExpectPlayerBarsFlat();
        }

        private static IEnumerator SkillSlotsAreFlat(Harness h)
        {
            yield return h.ExpectSkillSlotsFlat();
            yield return h.ExpectExtraKeysPlain();
            yield return h.ExpectSlotsSkillSized();
        }

        private static IEnumerator NtormatPagesFollowKeyItem(Harness h)
        {
            yield return h.GiveNtormat();
            yield return h.ExpectNtormatPagesFollowSlot();
        }

        // Money changes the amount's width, so its panel has to be refitted.
        private static IEnumerator TopIsFlat(Harness h)
        {
            yield return h.ExpectTopFlat();
            yield return h.ExpectTopBarsAtCanvasWidth();
            yield return h.ExpectCurrenciesInRow();
            h.Cmd($"{Commands.GiveMoney} {ChestMoney}");
            yield return h.Wait(CountUpSeconds);
            yield return h.ExpectCurrenciesInRow();
        }

        // A stack and a single item, then more of the single item, so a count appears on an icon already restyled.
        private static IEnumerator ItemIconsAreFlat(Harness h)
        {
            h.Cmd($"{Commands.GiveItem} {GameNames.Syringe} 3");
            h.Cmd($"{Commands.GiveItem} {GameNames.Hoof} 1");
            yield return h.Wait(0.5f);
            yield return h.ExpectItemIconsFlat();
            h.Cmd($"{Commands.GiveItem} {GameNames.Hoof} 1");
            yield return h.Wait(0.5f);
            yield return h.ExpectItemIconsFlat();
        }

        private static IEnumerator PromptIsFlat(Harness h)
        {
            yield return h.ExpectPromptFlat();
        }

        // The game shows no action for a chest the player can't afford, only Inspect.
        private static IEnumerator PromptPanelFitsText(Harness h)
        {
            h.Cmd($"{Commands.GiveMoney} {ChestMoney}");
            PurchaseInteraction chest = null;
            yield return h.Spawned<PurchaseInteraction>($"{Commands.SpawnInteractable} {GameNames.SmallChest}", found => chest = found);
            yield return h.StandBefore(chest.gameObject);
            yield return h.ExpectPromptPanelFitsText();
        }

        private static IEnumerator ScoreboardAndChatAreFlat(Harness h)
        {
            yield return h.ExpectOverlaysFlat();
        }

        private static IEnumerator ObjectivePanelIsFlat(Harness h)
        {
            yield return h.ExpectObjectivePanelFlat();
            yield return h.ExpectObjectiveRowsAsCanvas();
        }

        private static IEnumerator TurningOffRestoresPanels(Harness h)
        {
            h.Cmd($"{Commands.GiveItem} {GameNames.Syringe} 2");
            yield return h.Wait(0.5f);
            yield return h.ExpectRunInfoFlat();
            yield return h.ExpectObjectivePanelFlat();
            yield return h.ExpectPlayerBarsFlat();
            yield return h.ExpectSkillSlotsFlat();
            yield return h.ExpectHudLevel();
            yield return h.ExpectTopFlat();
            yield return h.ExpectItemIconsFlat();
            yield return h.ExpectPromptFlat();
            yield return h.ExpectOverlaysFlat();
            yield return h.ExpectTopBarsAtCanvasWidth();
            h.SetConfig(SevenUIPlugin.Settings.FlatPanels, false);
            yield return h.ExpectRunInfoVanilla();
            yield return h.ExpectObjectivePanelVanilla();
            yield return h.ExpectPlayerBarsVanilla();
            yield return h.ExpectSkillSlotsVanilla();
            yield return h.ExpectHudTilted();
            yield return h.ExpectTopVanilla();
            yield return h.ExpectItemIconsVanilla();
            yield return h.ExpectPromptVanilla();
            yield return h.ExpectOverlaysVanilla();
            yield return h.ExpectTopBarsAtGameWidth();
            h.SetConfig(SevenUIPlugin.Settings.FlatPanels, true);
            yield return h.ExpectRunInfoFlat();
            yield return h.ExpectObjectivePanelFlat();
            yield return h.ExpectPlayerBarsFlat();
            yield return h.ExpectSkillSlotsFlat();
            yield return h.ExpectHudLevel();
            yield return h.ExpectTopFlat();
            yield return h.ExpectItemIconsFlat();
            yield return h.ExpectPromptFlat();
            yield return h.ExpectOverlaysFlat();
            yield return h.ExpectTopBarsAtCanvasWidth();
        }

        // LookingGlass copies the objective panel for its stats, header strip and all if the copy is made after the
        // restyle, so the copy must lose the strip and the header's spaced capitals.
        private static IEnumerator LookingGlassStatsPanelIsPlain(Harness h)
        {
            Transform panel = null;
            yield return h.WaitForLookingGlassPanel(found => panel = found);
            yield return h.ExpectPlainPanel(panel, "LookingGlass's stats panel");
        }
    }
}
