using UnityEngine;
using UnityEngine.UI;

namespace SevenUI
{
    // The design canvas's cooldown: a dark cover over the top of a skill icon that shrinks as the skill recharges, in
    // place of the game's radial overlay. It follows the overlay, which the game turns on while a skill recharges and
    // whose alpha it sets to the recharge progress (SkillIcon.LateUpdate), so it needs nothing from the skill itself.
    internal sealed class CooldownCover : MonoBehaviour
    {
        // Below this change the cover isn't moved, so the layout isn't dirtied every frame.
        private const float Epsilon = 0.001f;

        public RawImage Overlay;
        public Image Cover;

        private float shownRemaining = -1f;

        public float ShownRemaining => shownRemaining;

        private void LateUpdate()
        {
            float remaining = Overlay && Overlay.enabled ? Mathf.Clamp01(1f - Overlay.color.a) : 0f;
            if (Mathf.Abs(remaining - shownRemaining) <= Epsilon) return;
            shownRemaining = remaining;
            Cover.enabled = remaining > 0f;
            Cover.rectTransform.anchorMin = new Vector2(0f, 1f - remaining);
        }
    }
}
