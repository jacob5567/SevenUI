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
        // Every setting a styler's Enabled reads, which the controller restyles on.
        private ConfigEntry<bool>[] watched;
        // In apply order. The tilt is first, so the parts measured on screen (the skill row) are measured facing it, as
        // they are drawn. The margins are after the panels, as they find panels they add and measure the clusters once
        // they face the screen, and after the skill slots, since the skill row's end is the equipment slot as fitted.
        private IStyler[] stylers;
        // The same stylers in the order they are restored, which differs from the apply order: the tilt and the top
        // bars width go after the panels they were applied around, and the pause menu is restored last.
        private IStyler[] restoreOrder;
        // Parallel to stylers: whether each has been applied and not since restored.
        private bool[] applied;
        private PickerStyler pickers;
        // Not in the list: it runs whether or not FlatPanels is on (it restyles only the font then), and has nothing to
        // restore, as the game makes each tooltip new.
        private TooltipStyler tooltips;
        private int lastHierarchyCount = -1;

        public PickerStyler Pickers => pickers;

        private void Awake()
        {
            hud = GetComponent<HUD>();
            // The HUD is a root object, but go through .root in case another mod parents it.
            root = hud.transform.root;
            HudFontSwapper fontSwapper = new HudFontSwapper(hud, root);
            RunInfoStyler runInfo = new RunInfoStyler(root);
            RightInfoBarStyler rightInfoBar = new RightInfoBarStyler(root);
            PlayerBarsStyler playerBars = new PlayerBarsStyler(root);
            SkillSlotsStyler skillSlots = new SkillSlotsStyler(hud, root);
            TopStyler top = new TopStyler(hud, root);
            ItemIconsStyler itemIcons = new ItemIconsStyler(root);
            AllyCardsStyler allyCards = new AllyCardsStyler(hud, root);
            PromptsStyler prompts = new PromptsStyler(hud, root);
            pickers = new PickerStyler(hud, root);
            ScoreboardStyler scoreboard = new ScoreboardStyler(hud, root);
            ChatStyler chat = new ChatStyler(root);
            TiltFlattener tilt = new TiltFlattener(root);
            tooltips = new TooltipStyler(hud, root);
            // The pause menu is its own canvas, outside the HUD, made each time it opens.
            PauseScreenStyler pause = new PauseScreenStyler(hud, root);
            TopBarsWidth topBars = new TopBarsWidth(root);
            HudMargins margins = new HudMargins(hud, root, skillSlots);
            stylers = new IStyler[]
            {
                tilt, runInfo, rightInfoBar, playerBars, skillSlots, top, itemIcons, allyCards, prompts, pickers,
                scoreboard, chat, topBars, margins, fontSwapper, pause,
            };
            restoreOrder = new IStyler[]
            {
                runInfo, rightInfoBar, playerBars, skillSlots, top, itemIcons, allyCards, prompts, pickers, scoreboard,
                chat, tilt, topBars, margins, fontSwapper, pause,
            };
            applied = new bool[stylers.Length];
            watched = new[]
            {
                SevenUIPlugin.Settings.BarlowFont,
                SevenUIPlugin.Settings.FlatPanels,
                SevenUIPlugin.Settings.TightMargins,
            };
            foreach (ConfigEntry<bool> setting in watched)
            {
                setting.SettingChanged += OnSettingChanged;
            }
        }

        // LateUpdate, after the game's Updates have added this frame's objects (objective rows are added in
        // ObjectivePanelController.Update), so they are restyled before they are first drawn.
        private void LateUpdate()
        {
            // Tooltips live outside the HUD, so they don't change its count; checking for a new one is one read.
            tooltips.Update();
            // Static and shared by every HUD, and safe to repeat from each, so not one of the HUD's stylers.
            DamageNumberFont.Update(SevenUIPlugin.Settings.MonospaceDamageNumbers.Value);
            for (int i = 0; i < stylers.Length; i++)
            {
                if (applied[i])
                {
                    stylers[i].Update();
                }
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
            for (int i = 0; i < stylers.Length; i++)
            {
                if (stylers[i].Enabled)
                {
                    stylers[i].Apply();
                    applied[i] = true;
                }
            }
            foreach (IStyler styler in restoreOrder)
            {
                int i = Array.IndexOf(stylers, styler);
                if (applied[i] && !styler.Enabled)
                {
                    styler.Restore();
                    applied[i] = false;
                }
            }
        }

        private void OnSettingChanged(object sender, EventArgs e)
        {
            Restyle();
        }

        public void Describe(StringBuilder output)
        {
            output.AppendLine($"HUD {hud.name} (target {(hud.targetBodyObject ? hud.targetBodyObject.name : "none")})");
            output.AppendLine($"  hierarchy objects: {root.hierarchyCount}");
            foreach (IStyler styler in stylers)
            {
                styler.Describe(output);
            }
            output.AppendLine($"  tooltips restyled: {tooltips.StyledCount}");
            output.AppendLine($"  monospace damage numbers: {DamageNumberFont.Applied}");
        }

        private void OnDestroy()
        {
            foreach (ConfigEntry<bool> setting in watched)
            {
                setting.SettingChanged -= OnSettingChanged;
            }
        }
    }
}
