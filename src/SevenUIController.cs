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
        private TopStyler top;
        private ItemIconsStyler itemIcons;
        private AllyCardsStyler allyCards;
        private PromptsStyler prompts;
        private ScoreboardStyler scoreboard;
        private ChatStyler chat;
        private TiltFlattener tilt;
        private TooltipStyler tooltips;
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
            skillSlots = new SkillSlotsStyler(hud, root);
            top = new TopStyler(root);
            itemIcons = new ItemIconsStyler(root);
            allyCards = new AllyCardsStyler(root);
            prompts = new PromptsStyler(root);
            scoreboard = new ScoreboardStyler(root);
            chat = new ChatStyler(root);
            tilt = new TiltFlattener(root);
            tooltips = new TooltipStyler(hud, root);
            SevenUIPlugin.Settings.BarlowFont.SettingChanged += OnSettingChanged;
            SevenUIPlugin.Settings.FlatPanels.SettingChanged += OnSettingChanged;
        }

        // LateUpdate, after the game's Updates have added this frame's objects (objective rows are added in
        // ObjectivePanelController.Update), so they are restyled before they are first drawn.
        private void LateUpdate()
        {
            // Tooltips live outside the HUD, so they don't change its count; checking for a new one is one read.
            tooltips.Update(SevenUIPlugin.Settings.BarlowFont.Value, SevenUIPlugin.Settings.FlatPanels.Value);
            // Shared by every HUD, and safe to repeat from each.
            DamageNumberFont.Update(SevenUIPlugin.Settings.MonospaceDamageNumbers.Value);
            if (panelsApplied)
            {
                prompts.Update();
                skillSlots.Update();
            }

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
            top.Apply();
            itemIcons.Apply();
            allyCards.Apply();
            prompts.Apply();
            scoreboard.Apply();
            chat.Apply();
            tilt.Apply();
        }

        private void RestorePanels()
        {
            runInfo.Restore();
            rightInfoBar.Restore();
            playerBars.Restore();
            skillSlots.Restore();
            top.Restore();
            itemIcons.Restore();
            allyCards.Restore();
            prompts.Restore();
            scoreboard.Restore();
            chat.Restore();
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
            top.Describe(output);
            itemIcons.Describe(output);
            allyCards.Describe(output);
            prompts.Describe(output);
            scoreboard.Describe(output);
            chat.Describe(output);
            output.AppendLine($"  leveled tilts: {tilt.LeveledCount}");
            output.AppendLine($"  tooltips restyled: {tooltips.StyledCount}");
            output.AppendLine($"  monospace damage numbers: {DamageNumberFont.Applied}");
        }

        private void OnDestroy()
        {
            SevenUIPlugin.Settings.BarlowFont.SettingChanged -= OnSettingChanged;
            SevenUIPlugin.Settings.FlatPanels.SettingChanged -= OnSettingChanged;
        }
    }
}
