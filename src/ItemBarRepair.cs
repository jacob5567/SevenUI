using System.Collections.Generic;
using RoR2.UI;
using UnityEngine;

namespace SevenUI
{
    // Puts the item bar's icons back on one grid when the game leaves one at another size. The game re-lays out every
    // icon only when a new icon count gives a new size (ItemInventoryDisplay.OnIconCountChanged, against its remembered
    // currentIconScale), but a change of the bar's width re-lays them out without updating that size
    // (OnRectTransformDimensionsChange). TopBarsWidth changes the width whenever the cluster drifts on screen, so after
    // one, an item taken and another given (a Lunar Cauldron's trade) can place the new icon at the size and place of
    // the old count's grid, bigger than the rest and off the rows (AllocateIcons lays new icons out for the count before
    // them). When the icon count or the width changes, the icons' scales are compared; if they differ, the bar is turned
    // off and on, which runs the game's own full layout (OnEnable calls LayoutAllIcons). That touches only the bar.
    internal sealed class ItemBarRepair
    {
        // Frames to check after a change, as the game adds and removes icons on the next update.
        private const int CheckFrames = 2;
        private const float ScaleTolerance = 0.001f;

        private readonly List<ItemIcon> icons = new List<ItemIcon>();
        private ItemInventoryDisplay bar;
        private RectTransform rect;
        private int shownChildren = -1;
        private float shownWidth = -1f;
        private int checksLeft;

        public int Repairs { get; private set; }

        public void Attach(ItemInventoryDisplay display)
        {
            bar = display;
            rect = display ? (RectTransform)display.transform : null;
            shownChildren = -1;
            shownWidth = -1f;
        }

        public void Update()
        {
            if (!bar || !bar.isActiveAndEnabled) return;
            int children = rect.childCount;
            float width = rect.rect.width;
            if (children != shownChildren || width != shownWidth)
            {
                shownChildren = children;
                shownWidth = width;
                checksLeft = CheckFrames;
            }
            if (checksLeft <= 0) return;
            checksLeft--;
            if (!Mismatched(out int count)) return;
            Repairs++;
            if (SevenUIPlugin.Settings.DebugLogging.Value)
            {
                SevenUIPlugin.Log.LogInfo($"Item bar: an icon was off the grid ({count} icons, {width:0.#} wide); laid out again");
            }
            bar.enabled = false;
            bar.enabled = true;
        }

        // Whether any of the bar's icons is at another scale than the last one laid out.
        private bool Mismatched(out int count)
        {
            rect.GetComponentsInChildren(false, icons);
            count = icons.Count;
            bool mismatched = false;
            for (int i = 0; i < icons.Count - 1 && !mismatched; i++)
            {
                mismatched = Mathf.Abs(icons[i].rectTransform.localScale.x - icons[icons.Count - 1].rectTransform.localScale.x) > ScaleTolerance;
            }
            icons.Clear();
            return mismatched;
        }
    }
}
