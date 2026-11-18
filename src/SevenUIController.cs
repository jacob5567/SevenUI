using System;
using System.Collections.Generic;
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
        // The stylers with per-frame work, in the order they update. It differs from the apply order: the margins and
        // the top bars width are last, so the top bars are fitted to the margins as they settle in the same frame.
        private IStyler[] updateOrder;
        // The stylers applied and not since restored.
        private readonly HashSet<IStyler> applied = new HashSet<IStyler>();
        // The stylers restored in the frame 'restoredFrame'. What a Restore destroys stays until that frame's end, where
        // the styler applied again would still find it, by name or by component, and skip adding its own; so a styler
        // restored this frame is applied again from the next.
        private readonly HashSet<IStyler> restoredNow = new HashSet<IStyler>();
        private int restoredFrame = -1;
        // Applied after every other styler's apply and restore, so turning the panels off puts them back first.
        private HudFontSwapper fontSwapper;
        private PauseScreenStyler pause;
        private TiltFlattener tilt;
        private SkillSlotsStyler skillSlots;
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
            fontSwapper = new HudFontSwapper(hud, root);
            RunInfoStyler runInfo = new RunInfoStyler(root);
            RightInfoBarStyler rightInfoBar = new RightInfoBarStyler(root);
            PlayerBarsStyler playerBars = new PlayerBarsStyler(root);
            skillSlots = new SkillSlotsStyler(hud, root);
            TopStyler top = new TopStyler(hud, root);
            ItemIconsStyler itemIcons = new ItemIconsStyler(root);
            AllyCardsStyler allyCards = new AllyCardsStyler(hud, root);
            PromptsStyler prompts = new PromptsStyler(hud, root);
            pickers = new PickerStyler(hud, root);
            ScoreboardStyler scoreboard = new ScoreboardStyler(hud, root);
            ChatStyler chat = new ChatStyler(root);
            tilt = new TiltFlattener(root);
            tooltips = new TooltipStyler(hud, root);
            // The pause menu is its own canvas, outside the HUD, made each time it opens.
            pause = new PauseScreenStyler(hud, root);
            MusicDisplayStyler music = new MusicDisplayStyler();
            TopBarsWidth topBars = new TopBarsWidth(root);
            HudMargins margins = new HudMargins(hud, root, skillSlots);
            stylers = new IStyler[]
            {
                tilt, runInfo, rightInfoBar, playerBars, skillSlots, top, itemIcons, allyCards, prompts, pickers,
                scoreboard, chat, topBars, margins, fontSwapper, pause, music,
            };
            restoreOrder = new IStyler[]
            {
                runInfo, rightInfoBar, playerBars, skillSlots, top, itemIcons, allyCards, prompts, pickers, scoreboard,
                chat, tilt, topBars, margins, fontSwapper, pause, music,
            };
            updateOrder = new IStyler[]
            {
                runInfo, prompts, pickers, playerBars, top, skillSlots, scoreboard, chat, allyCards, pause, music, margins, topBars,
            };
            watched = new[]
            {
                SevenUIPlugin.Settings.BarlowFont,
                SevenUIPlugin.Settings.FlatPanels,
                SevenUIPlugin.Settings.TightMargins,
                SevenUIPlugin.Settings.EquipmentDeck,
                SevenUIPlugin.Settings.RestyleItemPickers,
                SevenUIPlugin.Settings.CompactAllyCards,
                SevenUIPlugin.Settings.GroupAllyCards,
                SevenUIPlugin.Settings.LevelHud,
                SevenUIPlugin.Settings.ScoreboardDetailPanel,
                SevenUIPlugin.Settings.ScoreboardTabs,
                SevenUIPlugin.Settings.RestylePauseMenu,
                SevenUIPlugin.Settings.RestyleTooltips,
                SevenUIPlugin.Settings.TimerHundredths,
                SevenUIPlugin.Settings.ShowHealthRegen,
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
            foreach (IStyler styler in updateOrder)
            {
                if (applied.Contains(styler))
                {
                    styler.Update();
                }
            }

            // Any object added under the HUD changes this count, and reading it is cheap, so it is the signal to
            // restyle new objects without walking the hierarchy every frame.
            int hierarchyCount = root.hierarchyCount;
            if (hierarchyCount == lastHierarchyCount && restoredNow.Count == 0) return;
            lastHierarchyCount = hierarchyCount;
            Restyle();
        }

        private void Restore(IStyler styler)
        {
            styler.Restore();
            restoredNow.Add(styler);
            restoredFrame = Time.frameCount;
        }

        private void Restyle()
        {
            if (restoredFrame != Time.frameCount)
            {
                restoredNow.Clear();
            }
            // The skill row is measured on screen once, so a tilt that comes or goes after that leaves it measured for
            // the wrong view. Put the tilt back (or have the row measured again) before anything is applied.
            if (tilt.Enabled != applied.Contains(tilt))
            {
                if (applied.Remove(skillSlots))
                {
                    Restore(skillSlots);
                }
                if (!tilt.Enabled)
                {
                    applied.Remove(tilt);
                    Restore(tilt);
                }
            }
            foreach (IStyler styler in stylers)
            {
                if (styler != fontSwapper && styler.Enabled && !restoredNow.Contains(styler))
                {
                    styler.Apply();
                    applied.Add(styler);
                }
            }
            foreach (IStyler styler in restoreOrder)
            {
                if (!styler.Enabled && applied.Remove(styler))
                {
                    Restore(styler);
                }
            }
            // After the panels are restored, so the font is applied to the panels as they are left.
            if (fontSwapper.Enabled && !restoredNow.Contains(fontSwapper))
            {
                fontSwapper.Apply();
                applied.Add(fontSwapper);
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
            fontSwapper.Describe(output);
            foreach (IStyler styler in restoreOrder)
            {
                if (styler != fontSwapper && styler != pause)
                {
                    styler.Describe(output);
                }
            }
            output.AppendLine($"  tooltips restyled: {tooltips.StyledCount}");
            pause.Describe(output);
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
