using System.Collections;
using System.Collections.Generic;
using ModTestRunner;
using RoR2;
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
            yield return new Scenario(1, Suite, "level_uses_barlow_condensed", Stages.TitanicPlains, LevelUsesBarlowCondensed);
            yield return new Scenario(1, Suite, "sleeping_text_keeps_barlow", Stages.TitanicPlains, SleepingTextKeepsBarlow);
            yield return new Scenario(1, Suite, "damage_numbers_use_monospace", Stages.TitanicPlains, DamageNumbersUseMonospace);
            yield return new Scenario(1, Suite, "damage_numbers_narrowed_on_next_stage", Stages.TitanicPlains, DamageNumbersNarrowedOnNextStage);
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

        private static IEnumerator LevelUsesBarlowCondensed(Harness h)
        {
            yield return h.ExpectLevelCondensed();
        }

        private static IEnumerator SleepingTextKeepsBarlow(Harness h)
        {
            yield return h.ExpectSleepingTextBarlow();
        }

        private static IEnumerator DamageNumbersUseMonospace(Harness h)
        {
            yield return h.ExpectDamageNumbersMonospace(true);
            h.SetConfig(SevenUIPlugin.Settings.MonospaceDamageNumbers, false);
            yield return h.ExpectDamageNumbersMonospace(false);
            h.SetConfig(SevenUIPlugin.Settings.MonospaceDamageNumbers, true);
            yield return h.ExpectDamageNumbersMonospace(true);
        }

        // Each stage makes a new damage number manager, whose particle is the game's width again, while the shared
        // material keeps the mod's sheet: the stretched digits would draw wide in square cells.
        private static IEnumerator DamageNumbersNarrowedOnNextStage(Harness h)
        {
            yield return h.ExpectDamageNumbersMonospace(true);
            DamageNumberManager before = DamageNumberManager.instance;
            yield return h.GoTo(Stages.DistantRoost);
            yield return h.Until(() => DamageNumberManager.instance && DamageNumberManager.instance != before,
                "a new damage number manager on the next stage");
            yield return h.ExpectDamageNumbersMonospace(true);
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
