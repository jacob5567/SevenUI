using System.Text;
using RoR2.UI;
using UnityEngine;

namespace SevenUI
{
    // One per HUD. Restyles the HUD's vanilla objects in place, and redoes it when the HUD gains objects (item
    // icons, notifications, ally cards, other mods' panels), so the game's own components keep doing the work.
    internal sealed class SevenUIController : MonoBehaviour
    {
        private HUD hud;
        private Transform root;
        private HudFontSwapper fontSwapper;
        private int lastHierarchyCount = -1;
        private bool fontApplied;

        private void Awake()
        {
            hud = GetComponent<HUD>();
            // The HUD is a root object, but go through .root in case another mod parents it.
            root = hud.transform.root;
            fontSwapper = new HudFontSwapper(root);
            SevenUIPlugin.Settings.BarlowFont.SettingChanged += OnFontSettingChanged;
        }

        private void Update()
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
            if (SevenUIPlugin.Settings.BarlowFont.Value)
            {
                fontSwapper.Apply();
                fontApplied = true;
            }
            else if (fontApplied)
            {
                fontSwapper.Restore();
                fontApplied = false;
            }
        }

        private void OnFontSettingChanged(object sender, System.EventArgs e)
        {
            Restyle();
        }

        public void Describe(StringBuilder output)
        {
            output.AppendLine($"HUD {hud.name} (target {(hud.targetBodyObject ? hud.targetBodyObject.name : "none")})");
            output.AppendLine($"  hierarchy objects: {root.hierarchyCount}");
            fontSwapper.Describe(output);
        }

        private void OnDestroy()
        {
            SevenUIPlugin.Settings.BarlowFont.SettingChanged -= OnFontSettingChanged;
        }
    }
}
