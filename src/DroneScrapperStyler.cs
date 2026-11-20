using System.Collections.Generic;
using System.Text;
using RoR2;
using RoR2.UI;
using UnityEngine;

namespace SevenUI
{
    // Restyles the HUD's Drone Scrapper panel (DLC3) as the other pick-an-item panels are (PickerPanel). It is its own
    // class in the game, not a PickupPickerPanel, so PickerStyler never sees it.
    internal sealed class DroneScrapperStyler : IStyler
    {
        private readonly HUD hud;
        private readonly Transform root;
        private readonly List<DroneScrapperPickerPanel> found = new List<DroneScrapperPickerPanel>();
        private readonly List<PickerPanel> panels = new List<PickerPanel>();

        public DroneScrapperStyler(HUD hud, Transform root)
        {
            this.hud = hud;
            this.root = root;
        }

        public IReadOnlyList<PickerPanel> Panels => panels;

        public bool Enabled => SevenUIPlugin.Settings.FlatPanels.Value && SevenUIPlugin.Settings.RestyleItemPickers.Value;

        public void Apply()
        {
            panels.RemoveAll(panel => !panel.Alive);
            root.GetComponentsInChildren(true, found);
            foreach (DroneScrapperPickerPanel panel in found)
            {
                if (panels.Exists(styled => styled.Component == panel)) continue;
                PickerPanel styled = PickerPanel.For(panel, hud);
                if (styled == null) continue;
                panels.Add(styled);
                // Now, in the frame the game opened it, so it is never drawn in the game's look.
                styled.Update();
            }
            found.Clear();
        }

        public void Restore()
        {
            foreach (PickerPanel panel in panels)
            {
                panel.Restore();
            }
            panels.Clear();
        }

        // Every frame while the panel is on; nothing to do while no scrapper is open.
        public void Update()
        {
            for (int i = panels.Count - 1; i >= 0; i--)
            {
                if (!panels[i].Alive)
                {
                    panels.RemoveAt(i);
                    continue;
                }
                panels[i].Update();
            }
        }

        public void Describe(StringBuilder output)
        {
            foreach (PickerPanel panel in panels)
            {
                if (!panel.Alive) continue;
                output.AppendLine($"  drone scrapper {panel.Component.name}: options={panel.Buttons.Count} cards={panel.Cards} " +
                                  $"empty={panel.Empty} size={panel.MainPanel.sizeDelta}");
            }
        }
    }
}
