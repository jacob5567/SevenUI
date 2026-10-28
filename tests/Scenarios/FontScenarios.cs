using System.Collections;
using System.Collections.Generic;
using BepInEx.Bootstrap;
using ModTestRunner;
using UnityEngine;

namespace SevenUI.Tests
{
    // The BarlowFont setting: HUD text is drawn in Barlow, including text the HUD gains later, and goes back to the
    // game's font when the setting is turned off.
    internal static class FontScenarios
    {
        private const string Suite = "font";
        // How long LookingGlass may take to add its stats panel; it builds it once the body's stats are known.
        private const float LookingGlassPanelTimeout = 10f;

        public static IEnumerable<Scenario> All()
        {
            yield return new Scenario(1, Suite, "hud_text_uses_barlow", Stages.TitanicPlains, HudTextUsesBarlow);
            yield return new Scenario(1, Suite, "new_item_icons_use_barlow", Stages.TitanicPlains, NewItemIconsUseBarlow);
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
            if (!Chainloader.PluginInfos.ContainsKey(GameNames.LookingGlassGUID)) throw h.Skip("LookingGlass isn't installed");

            Transform panel = null;
            float deadline = Time.time + LookingGlassPanelTimeout;
            while (!panel && Time.time < deadline)
            {
                panel = h.FindUnderHud(GameNames.LookingGlassPanel);
                yield return null;
            }
            if (!panel) throw h.Skip("LookingGlass's stats panel didn't appear (its stats display may be off or detached)");

            yield return h.ExpectAllBarlow(panel, "LookingGlass's stats panel");
        }
    }
}
