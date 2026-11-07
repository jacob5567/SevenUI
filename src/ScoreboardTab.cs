using RoR2.UI;
using UnityEngine;
using UnityEngine.EventSystems;

namespace SevenUI
{
    // On each scoreboard row. While the rows are collapsed, the pointer entering anything in the row, its line or the
    // items shown for it, reaches the row too, and shows that player.
    internal sealed class ScoreboardTab : MonoBehaviour, IPointerEnterHandler
    {
        public ScoreboardItemsBoard Board;
        public ScoreboardStrip Strip;

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (Board)
            {
                Board.Show(Strip);
            }
        }
    }
}
