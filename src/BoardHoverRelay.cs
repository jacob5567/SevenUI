using RoR2.UI;
using UnityEngine;
using UnityEngine.EventSystems;

namespace SevenUI
{
    // On an item, equipment or key item in the scoreboard's rows while the docked panel describes them: tells the board
    // what the pointer (or a gamepad's selection) is on, in place of the game's tooltip provider beside it, which the
    // board switches off. The game makes a new tooltip, a canvas of its own, each time the pointer reaches an item,
    // which the board then had to hide; moving across a crowded board that halved the frame rate (H10).
    internal sealed class BoardHoverRelay : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
    {
        public ScoreboardItemsBoard Board;
        public TooltipProvider Provider;

        public void OnPointerEnter(PointerEventData eventData) => Enter();

        public void OnPointerExit(PointerEventData eventData) => Leave();

        public void OnSelect(BaseEventData eventData) => Enter();

        public void OnDeselect(BaseEventData eventData) => Leave();

        private void Enter()
        {
            if (Board) Board.Hover(Provider);
        }

        private void Leave()
        {
            if (Board) Board.Unhover(Provider);
        }
    }
}
