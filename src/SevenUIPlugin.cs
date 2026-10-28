using System.Text;
using BepInEx;
using BepInEx.Logging;
using RoR2.UI;

namespace SevenUI
{
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    [BepInDependency(RiskOfOptionsCompat.GUID, BepInDependency.DependencyFlags.SoftDependency)]
    public class SevenUIPlugin : BaseUnityPlugin
    {
        public const string PluginGUID = "sevenstack.SevenUI";
        public const string PluginName = "SevenUI";
        public const string PluginVersion = PluginVersionInfo.Version;

        internal static ManualLogSource Log;
        internal static PluginConfig Settings;

        private void Awake()
        {
            Log = Logger;
            Settings = new PluginConfig(Config);

            if (RiskOfOptionsCompat.Enabled)
            {
                RiskOfOptionsCompat.Register(Settings);
            }

            HUD.onHudTargetChangedGlobal += OnHudTargetChanged;
        }

        private void OnDestroy()
        {
            HUD.onHudTargetChangedGlobal -= OnHudTargetChanged;
        }

        // Fires whenever any HUD changes the body it is showing, so each HUD gets exactly one controller.
        private static void OnHudTargetChanged(HUD hud)
        {
            if (hud && !hud.GetComponent<SevenUIController>())
            {
                hud.gameObject.AddComponent<SevenUIController>();
            }
        }

        private void Update()
        {
            if (Settings.DebugLogging.Value && Settings.DumpKey.Value.IsDown())
            {
                Dump();
            }
        }

        // Logs each HUD's fonts and what the mod restyled.
        private static void Dump()
        {
            StringBuilder output = new StringBuilder("SevenUI dump:\n");
            foreach (HUD hud in HUD.readOnlyInstanceList)
            {
                SevenUIController controller = hud ? hud.GetComponent<SevenUIController>() : null;
                if (controller)
                {
                    controller.Describe(output);
                }
                else if (hud)
                {
                    output.AppendLine($"HUD {hud.name}: no controller");
                }
            }
            Log.LogInfo(output.ToString());
        }
    }
}
