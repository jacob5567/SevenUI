using RoR2;
using RoR2.UI;
using UnityEngine;
using UnityEngine.UI;

namespace SevenUI
{
    // The thin bar along the bottom of a skill with charges, as on the design canvas: it fills in the accent as the
    // next charge recharges, and is full once every charge is back. Shown only where the game shows the skill's stock.
    internal sealed class RechargeBar : MonoBehaviour
    {
        // Steps of the fill, so its width changes only a few hundred times over a recharge.
        private const float Steps = 200f;

        public SkillIcon Skill;
        public Image Trough;
        public Image Fill;

        private int shownStep = -1;

        // A few reads of the skill when nothing changed.
        private void Update()
        {
            GenericSkill skill = Skill ? Skill.targetSkill : null;
            bool shown = skill && skill.skillDef && skill.maxStock > 1 && !skill.skillDef.hideStockCount;
            if (Trough.enabled != shown)
            {
                Trough.enabled = shown;
                Fill.enabled = shown;
            }
            if (!shown) return;
            // finalRechargeInterval is the field cooldownRemaining reads; CalculateFinalRechargeInterval works it out
            // again.
            float interval = skill.finalRechargeInterval;
            float filled = skill.stock >= skill.maxStock || interval <= 0f ? 1f : Mathf.Clamp01(skill.rechargeStopwatch / interval);
            int step = Mathf.RoundToInt(filled * Steps);
            if (step == shownStep) return;
            shownStep = step;
            Fill.rectTransform.anchorMax = new Vector2(step / Steps, 1f);
        }
    }
}
