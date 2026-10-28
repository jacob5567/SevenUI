using System.Collections;
using System.Collections.Generic;
using ModTestRunner;
using UnityEngine;

namespace SevenUI.Tests
{
    // The BarlowFont setting: HUD text is drawn in Barlow (the run timer in Barlow Condensed), including text the HUD
    // gains later, and goes back to the game's font when the setting is turned off.
    internal static class FontScenarios
    {
        private const string Suite = "font";

        public static IEnumerable<Scenario> All()
        {
            yield return new Scenario(1, Suite, "hud_text_uses_barlow", Stages.TitanicPlains, HudTextUsesBarlow);
            yield return new Scenario(1, Suite, "new_item_icons_use_barlow", Stages.TitanicPlains, NewItemIconsUseBarlow);
            yield return new Scenario(1, Suite, "timer_uses_barlow_condensed", Stages.TitanicPlains, TimerUsesBarlowCondensed);
            yield return new Scenario(1, Suite, "turning_off_restores_game_font", Stages.TitanicPlains, TurningOffRestoresGameFont);
            yield return new Scenario(1, Suite, "lookingglass_stats_use_barlow", Stages.TitanicPlains, LookingGlassStatsUseBarlow);
        }

        private static IEnumerator HudTextUsesBarlow(Harness h)
        {
            yield return h.ExpectHudBarlow();
        }

        // Stacked items add item icons with stack counts, which are created after the HUD was first restyled.
        private static IEnumerator NewItemIconsUseBarlow(Harness h)
        {
            yield return h.ExpectHudBarlow();
            h.Cmd($"{Commands.GiveItem} {GameNames.Syringe} 3");
            h.Cmd($"{Commands.GiveItem} {GameNames.Hoof} 2");
            yield return h.Wait(0.5f);
            yield return h.ExpectHudBarlow();
        }

        private static IEnumerator TimerUsesBarlowCondensed(Harness h)
        {
            yield return h.ExpectTimerCondensed();
        }

        private static IEnumerator TurningOffRestoresGameFont(Harness h)
        {
            yield return h.ExpectHudBarlow();
            h.SetConfig(SevenUIPlugin.Settings.BarlowFont, false);
            yield return h.ExpectHudGameFont();
            h.SetConfig(SevenUIPlugin.Settings.BarlowFont, true);
            yield return h.ExpectHudBarlow();
        }

        // LookingGlass copies the objective panel for its stats, so the copy must end up in Barlow too.
        private static IEnumerator LookingGlassStatsUseBarlow(Harness h)
        {
            Transform panel = null;
            yield return h.WaitForLookingGlassPanel(found => panel = found);
            yield return h.ExpectAllBarlow(panel, "LookingGlass's stats panel");
        }
    }
}
