using System.Collections;
using System.Collections.Generic;
using ModTestRunner;
using UnityEngine;

namespace SevenUI.Tests
{
    // The FlatPanels setting: the top-right panels are flat, the run info shows the mod's difficulty bar, the objective
    // panel has a header strip, and turning it off brings back the game's panels. LookingGlass's copy of the objective
    // panel gets a plain panel.
    internal static class PanelScenarios
    {
        private const string Suite = "panels";

        public static IEnumerable<Scenario> All()
        {
            yield return new Scenario(1, Suite, "hud_faces_screen", Stages.TitanicPlains, HudFacesScreen);
            yield return new Scenario(1, Suite, "run_info_is_flat", Stages.TitanicPlains, RunInfoIsFlat);
            yield return new Scenario(1, Suite, "objective_panel_is_flat", Stages.TitanicPlains, ObjectivePanelIsFlat);
            yield return new Scenario(1, Suite, "player_bars_are_flat", Stages.TitanicPlains, PlayerBarsAreFlat);
            yield return new Scenario(1, Suite, "skill_slots_are_flat", Stages.TitanicPlains, SkillSlotsAreFlat);
            yield return new Scenario(1, Suite, "top_is_flat", Stages.TitanicPlains, TopIsFlat);
            yield return new Scenario(1, Suite, "item_icons_are_flat", Stages.TitanicPlains, ItemIconsAreFlat);
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
        }

        private static IEnumerator TopIsFlat(Harness h)
        {
            yield return h.ExpectTopFlat();
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

        private static IEnumerator ObjectivePanelIsFlat(Harness h)
        {
            yield return h.ExpectObjectivePanelFlat();
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
            h.SetConfig(SevenUIPlugin.Settings.FlatPanels, false);
            yield return h.ExpectRunInfoVanilla();
            yield return h.ExpectObjectivePanelVanilla();
            yield return h.ExpectPlayerBarsVanilla();
            yield return h.ExpectSkillSlotsVanilla();
            yield return h.ExpectHudTilted();
            yield return h.ExpectTopVanilla();
            yield return h.ExpectItemIconsVanilla();
            h.SetConfig(SevenUIPlugin.Settings.FlatPanels, true);
            yield return h.ExpectRunInfoFlat();
            yield return h.ExpectObjectivePanelFlat();
            yield return h.ExpectPlayerBarsFlat();
            yield return h.ExpectSkillSlotsFlat();
            yield return h.ExpectHudLevel();
            yield return h.ExpectTopFlat();
            yield return h.ExpectItemIconsFlat();
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
