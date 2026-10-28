using System;
using System.Text;
using BepInEx.Configuration;
using RoR2.UI;
using UnityEngine;

namespace SevenUI
{
    // One per HUD. Restyles the HUD's vanilla objects in place, and redoes it when the HUD gains objects (item
    // icons, notifications, ally cards, objective rows, other mods' panels), so the game's own components keep doing
    // the work.
    internal sealed class SevenUIController : MonoBehaviour
    {
        private HUD hud;
        private Transform root;
        private HudFontSwapper fontSwapper;
        private RunInfoStyler runInfo;
        private RightInfoBarStyler rightInfoBar;
        private PlayerBarsStyler playerBars;
        private SkillSlotsStyler skillSlots;
        private TiltFlattener tilt;
        private int lastHierarchyCount = -1;
        private bool fontApplied;
        private bool panelsApplied;

        private void Awake()
        {
            hud = GetComponent<HUD>();
            // The HUD is a root object, but go through .root in case another mod parents it.
            root = hud.transform.root;
            fontSwapper = new HudFontSwapper(root);
            runInfo = new RunInfoStyler(root);
            rightInfoBar = new RightInfoBarStyler(root);
            playerBars = new PlayerBarsStyler(root);
            skillSlots = new SkillSlotsStyler(root);
            tilt = new TiltFlattener(root);
            SevenUIPlugin.Settings.BarlowFont.SettingChanged += OnSettingChanged;
            SevenUIPlugin.Settings.FlatPanels.SettingChanged += OnSettingChanged;
        }

        // LateUpdate, after the game's Updates have added this frame's objects (objective rows are added in
        // ObjectivePanelController.Update), so they are restyled before they are first drawn.
        private void LateUpdate()
        {
            // Any object added under the HUD changes this count, and reading it is cheap, so it is the signal to
            // restyle new objects without walking the hierarchy every frame.
            int hierarchyCount = root.hierarchyCount;
            if (hierarchyCount == lastHierarchyCount) return;
            lastHierarchyCount = hierarchyCount;
            Restyle();
        }

        private void Restyle()
        {
            panelsApplied = Toggle(SevenUIPlugin.Settings.FlatPanels, panelsApplied, ApplyPanels, RestorePanels);
            fontApplied = Toggle(SevenUIPlugin.Settings.BarlowFont, fontApplied, fontSwapper.Apply, fontSwapper.Restore);
        }

        private void ApplyPanels()
        {
            runInfo.Apply();
            rightInfoBar.Apply();
            playerBars.Apply();
            skillSlots.Apply();
            tilt.Apply();
        }

        private void RestorePanels()
        {
            runInfo.Restore();
            rightInfoBar.Restore();
            playerBars.Restore();
            skillSlots.Restore();
            tilt.Restore();
        }

        private static bool Toggle(ConfigEntry<bool> setting, bool applied, Action apply, Action restore)
        {
            if (setting.Value)
            {
                apply();
                return true;
            }
            if (applied)
            {
                restore();
            }
            return false;
        }

        private void OnSettingChanged(object sender, EventArgs e)
        {
            Restyle();
        }

        public void Describe(StringBuilder output)
        {
            output.AppendLine($"HUD {hud.name} (target {(hud.targetBodyObject ? hud.targetBodyObject.name : "none")})");
            output.AppendLine($"  hierarchy objects: {root.hierarchyCount}");
            fontSwapper.Describe(output);
            runInfo.Describe(output);
            rightInfoBar.Describe(output);
            playerBars.Describe(output);
            skillSlots.Describe(output);
            output.AppendLine($"  leveled tilts: {tilt.LeveledCount}");
        }

        private void OnDestroy()
        {
            SevenUIPlugin.Settings.BarlowFont.SettingChanged -= OnSettingChanged;
            SevenUIPlugin.Settings.FlatPanels.SettingChanged -= OnSettingChanged;
            playerBars.Dispose();
        }
    }
}
