using UnityEngine;
using UnityEngine.UI;

namespace SevenUI
{
    // A Winnowing trial's progress drawn as the teleporter's charge is (ChargeRow): a thin bar under the objective,
    // filled to the share the game fills its own bar to, which stays hidden. The trial's tracker sets that share from
    // the trial's synced progress as the panel updates.
    internal sealed class ProgressRow : MonoBehaviour
    {
        public Image GameBar;
        public RectTransform Fill;

        private float shownShare = -1f;

        // LateUpdate, after the panel has updated the game's bar this frame. A float compared when nothing changed.
        private void LateUpdate()
        {
            if (!GameBar || !Fill) return;
            float share = Mathf.Clamp01(GameBar.fillAmount);
            if (Mathf.Approximately(share, shownShare)) return;
            shownShare = share;
            Fill.anchorMax = new Vector2(share, 1f);
        }
    }
}
