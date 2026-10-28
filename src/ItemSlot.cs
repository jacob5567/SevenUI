using RoR2;
using RoR2.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SevenUI
{
    // The mod's look for one item icon: a dark slot with a strip in the item's tier color along the bottom, and the
    // stack count as text. The game reuses icons for other items as the inventory changes, so it follows the icon's
    // item and count rather than setting them once.
    internal sealed class ItemSlot : MonoBehaviour
    {
        public ItemIcon Icon;
        public Image TierStrip;
        public TMP_Text Count;

        private ItemIndex shownItem = ItemIndex.None;
        private int shownCount = -1;

        public int ShownCount => shownCount;

        private void LateUpdate()
        {
            if (!Icon) return;
            if (Icon.itemIndex != shownItem)
            {
                shownItem = Icon.itemIndex;
                ItemDef item = ItemCatalog.GetItemDef(shownItem);
                ItemTierDef tier = item ? ItemTierCatalog.GetItemTierDef(item.tier) : null;
                TierStrip.enabled = tier;
                if (tier)
                {
                    TierStrip.color = ColorCatalog.GetColor(tier.colorIndex);
                }
            }
            // The game shows a count only for stacks of two or more.
            if (Count && Icon.itemCount != shownCount)
            {
                shownCount = Icon.itemCount;
                Count.text = shownCount > 1 ? shownCount.ToString() : "";
            }
        }
    }
}
