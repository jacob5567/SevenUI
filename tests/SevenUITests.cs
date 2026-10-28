using System.Collections.Generic;
using System.Linq;
using System.Text;
using ModTestRunner;

namespace SevenUI.Tests
{
    // The SevenUI scenarios, and what the runner needs to know about the mod: when they can run, when a stage is ready,
    // and what the mod saw at a failure.
    internal sealed class SevenUITests : ModTests
    {
        public override string Name => SevenUIPlugin.PluginName;

        public override IEnumerable<Scenario> Scenarios()
        {
            return FontScenarios.All().Concat(PanelScenarios.All());
        }

        public override string Precondition(Scenario scenario)
        {
            PluginConfig settings = SevenUIPlugin.Settings;
            if (!settings.BarlowFont.Value) return "BarlowFont is off in the SevenUI config";
            if (!settings.FlatPanels.Value) return "FlatPanels is off in the SevenUI config";
            if (settings.DebugAlwaysShow.Value) return "DebugAlwaysShow is on in the SevenUI config";
            return null;
        }

        // The controller attaches when the HUD first gets a body, a little after the stage loads.
        public override bool Ready()
        {
            return SevenUIChecks.Controller;
        }

        // The mod's dump-key lines for the HUD.
        public override string Diagnose()
        {
            SevenUIController controller = SevenUIChecks.Controller;
            if (!controller) return null;
            StringBuilder output = new StringBuilder();
            controller.Describe(output);
            return output.ToString();
        }
    }
}
