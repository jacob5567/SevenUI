using RoR2;
using RoR2.UI;
using UnityEngine;
using UnityEngine.UI;

namespace SevenUI
{
    // The health a boss just lost, drawn as on the design canvas: a dark red stretch after the boss bar's fill that
    // stays for a moment after each hit, then drains down to the fill. The game's own trailing bar catches up within
    // a fraction of a second, too fast to read, so it is hidden and this one drawn in its place, under the fill. It
    // follows the fill the game draws, so it agrees with it on host and client.
    internal sealed class BossDamageTrail
    {
        public const string TrailName = "SevenUIBossLost";
        // How long the lost health stays after the last hit, then how long it takes to drain.
        private const float HoldSeconds = 0.6f;
        private const float DrainSeconds = 0.4f;

        private readonly HUDBossHealthBarController boss;
        private readonly Image trail;
        private BossGroup shownGroup;
        private float shown = 1f;
        private float lastHealth = 1f;
        private float holdUntil;
        // A share of the bar per second, set when a drain starts so every drain takes as long.
        private float drainRate;

        public BossDamageTrail(HUDBossHealthBarController boss, StyleRecorder recorder)
        {
            this.boss = boss;
            Image fill = boss.fillRectImage;
            recorder.Disable(boss.delayRectImage);

            GameObject created = new GameObject(TrailName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            created.layer = fill.gameObject.layer;
            RectTransform rect = (RectTransform)created.transform;
            rect.SetParent(fill.transform.parent, false);
            // Just before the fill, so the fill draws over it.
            rect.SetSiblingIndex(fill.transform.GetSiblingIndex());
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            trail = created.GetComponent<Image>();
            trail.sprite = FlatSprite.White;
            trail.type = Image.Type.Filled;
            trail.fillMethod = Image.FillMethod.Horizontal;
            trail.fillOrigin = (int)Image.OriginHorizontal.Left;
            trail.color = Palette.BossHealthLost;
            trail.raycastTarget = false;
            recorder.Add(created);
        }

        public bool Alive => boss && boss.fillRectImage && trail;

        public float Shown => shown;

        // A few reads while a boss bar shows.
        public void Update()
        {
            if (!boss.container || !boss.container.activeInHierarchy) return;
            float health = boss.fillRectImage.fillAmount;
            // A new boss, or health regained, starts with nothing lost.
            if (boss.currentBossGroup != shownGroup || health >= shown)
            {
                shownGroup = boss.currentBossGroup;
                shown = health;
                drainRate = 0f;
            }
            else if (health < lastHealth)
            {
                holdUntil = Time.time + HoldSeconds;
                drainRate = 0f;
            }
            else if (Time.time >= holdUntil)
            {
                if (drainRate <= 0f)
                {
                    drainRate = (shown - health) / DrainSeconds;
                }
                shown = Mathf.MoveTowards(shown, health, drainRate * Time.deltaTime);
            }
            lastHealth = health;
            if (trail.fillAmount != shown)
            {
                trail.fillAmount = shown;
            }
        }
    }
}
