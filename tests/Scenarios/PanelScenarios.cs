using System.Collections;
using System.Collections.Generic;
using ModTestRunner;
using UnityEngine;

namespace SevenUI.Tests
{
    // The FlatPanels setting: the top-right panels are flat, the objective panel has a header strip, and turning it off
    // brings back the game's panels. LookingGlass's copy of the objective panel gets a plain panel.
    internal static class PanelScenarios
    {
        private const string Suite = "panels";

        public static IEnumerable<Scenario> All()
        {
            yield return new Scenario(1, Suite, "objective_panel_is_flat", Stages.TitanicPlains, ObjectivePanelIsFlat);
            yield return new Scenario(1, Suite, "turning_off_restores_panels", Stages.TitanicPlains, TurningOffRestoresPanels);
            yield return new Scenario(1, Suite, "lookingglass_stats_panel_is_plain", Stages.TitanicPlains, LookingGlassStatsPanelIsPlain);
        }

        private static IEnumerator ObjectivePanelIsFlat(Harness h)
        {
            yield return h.ExpectObjectivePanelFlat();
        }

        private static IEnumerator TurningOffRestoresPanels(Harness h)
        {
            yield return h.ExpectObjectivePanelFlat();
            h.SetConfig(SevenUIPlugin.Settings.FlatPanels, false);
            yield return h.ExpectObjectivePanelVanilla();
            h.SetConfig(SevenUIPlugin.Settings.FlatPanels, true);
            yield return h.ExpectObjectivePanelFlat();
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
