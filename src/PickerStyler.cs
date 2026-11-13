using System.Collections.Generic;
using System.Text;
using RoR2.UI;
using UnityEngine;

namespace SevenUI
{
    // Restyles the HUD's pick-an-item panels (PickerPanel), which the game opens under the HUD for Command, Void
    // Potentials and the like, and destroys when the choice is made or cancelled.
    internal sealed class PickerStyler : IStyler
    {
        private readonly HUD hud;
        private readonly Transform root;
        private readonly List<PickupPickerPanel> found = new List<PickupPickerPanel>();
        private readonly List<PickerPanel> panels = new List<PickerPanel>();

        public PickerStyler(HUD hud, Transform root)
        {
            this.hud = hud;
            this.root = root;
        }

        public IReadOnlyList<PickerPanel> Panels => panels;

        public bool Enabled => SevenUIPlugin.Settings.FlatPanels.Value;

        public void Apply()
        {
            panels.RemoveAll(panel => !panel.Alive);
            root.GetComponentsInChildren(true, found);
            foreach (PickupPickerPanel panel in found)
            {
                if (panels.Exists(styled => styled.Panel == panel)) continue;
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

        // Every frame while the panels are on; nothing to do while no picker is open.
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
                output.AppendLine($"  picker {panel.Panel.name}: options={panel.Buttons.Count} cards={panel.Cards} " +
                                  $"size={panel.MainPanel.sizeDelta}");
            }
        }
    }
}
