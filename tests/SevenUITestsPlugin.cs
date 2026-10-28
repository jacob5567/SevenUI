using BepInEx;
using ModTestRunner;

namespace SevenUI.Tests
{
    // Registers the SevenUI in-game tests with ModTestRunner, which runs them (mod_test --mod SevenUI).
    // Dev-only: it is never packaged or published.
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    [BepInDependency(SevenUIPlugin.PluginGUID)]
    [BepInDependency(ModTestRunnerPlugin.PluginGUID)]
    public class SevenUITestsPlugin : BaseUnityPlugin
    {
        public const string PluginGUID = "sevenstack.SevenUI.Tests";
        public const string PluginName = "SevenUI.Tests";
        public const string PluginVersion = "0.0.1";

        private void Awake()
        {
            TestRegistry.Register(new SevenUITests());
        }
    }
}
