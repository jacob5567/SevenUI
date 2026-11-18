using RoR2;
using RoR2.UI;
using UnityEngine;
using UnityEngine.UI;

namespace SevenUI
{
    // What the game's single boss fill leaves out, drawn over it in the player bar's colors and patterns: the shield as
    // a blue segment after the health (pink for void shields), the barrier as a gold strip along the top after the
    // shield, the maximum health a curse took as stripes at the end of the bar, and a line where a frozen boss is
    // executed. The game's fill is the group's health, shield and barrier summed over its largest observed maximum, so
    // the segments are cut from the same sums, and end where the fill ends. Read from the group's members' bodies, which
    // the host and clients both have; nothing is sent or changed.
    internal sealed class BossBarEffects
    {
        public const string ShieldName = "SevenUIBossShield";
        public const string VoidShieldName = "SevenUIBossVoidShield";
        public const string BarrierName = "SevenUIBossBarrier";
        public const string CurseName = "SevenUIBossCurse";
        public const string ExecuteName = "SevenUIBossExecute";

        // The execute line's width, in the top cluster's units.
        private const float ExecuteMarkWidth = 2f;
        private const float ChangeTolerance = 0.0001f;

        private readonly HUDBossHealthBarController boss;
        private readonly Image shield;
        private readonly Image voidShield;
        private readonly Image barrier;
        private readonly Image curse;
        private readonly Image execute;
        private float shownShieldStart = -1f;
        private float shownShieldEnd = -1f;
        private float shownVoidEnd = -1f;
        private float shownBarrierEnd = -1f;
        private float shownCurseStart = -1f;
        private float shownExecute = -1f;

        private BossBarEffects(HUDBossHealthBarController boss, Image shield, Image voidShield, Image barrier,
            Image curse, Image execute)
        {
            this.boss = boss;
            this.shield = shield;
            this.voidShield = voidShield;
            this.barrier = barrier;
            this.curse = curse;
            this.execute = execute;
        }

        // Null when the boss bar has no fill panel to draw on.
        public static BossBarEffects Style(HUDBossHealthBarController boss, StyleRecorder recorder)
        {
            RectTransform bar = boss.fillRectImage ? boss.fillRectImage.transform.parent as RectTransform : null;
            if (!bar) return null;

            Image shield = Segment(ShieldName, bar, Palette.Shield, FlatSprite.White, Image.Type.Simple, recorder);
            Image voidShield = Segment(VoidShieldName, bar, Palette.VoidShield, FlatSprite.White, Image.Type.Simple,
                recorder);
            // The strip stays within the bar, as the player's barrier does.
            Image barrier = Segment(BarrierName, bar, Palette.Barrier, FlatSprite.TopStrip, Image.Type.Sliced, recorder);
            Image curse = Segment(CurseName, bar, Color.white, FlatSprite.Hatch, Image.Type.Tiled, recorder);
            Image execute = Segment(ExecuteName, bar, Palette.ExecuteMark, FlatSprite.White, Image.Type.Simple, recorder);

            // Over both of the game's fills, so the trail of lost health stays behind every segment.
            int above = Mathf.Max(boss.fillRectImage.transform.GetSiblingIndex(),
                boss.delayRectImage ? boss.delayRectImage.transform.GetSiblingIndex() : 0);
            foreach (Image image in new[] { shield, voidShield, barrier, curse, execute })
            {
                image.transform.SetSiblingIndex(above + 1);
                above = image.transform.GetSiblingIndex();
            }
            return new BossBarEffects(boss, shield, voidShield, barrier, curse, execute);
        }

        public bool Alive => boss && shield && voidShield && barrier && curse && execute;

        // Per frame while a boss bar shows: a loop over the group's members reading their bodies' health values, and a
        // few floats compared with what is drawn.
        public void Update()
        {
            if (!boss.container.activeInHierarchy) return;
            BossGroup group = boss.currentBossGroup;
            float max = group ? group.totalMaxObservedMaxHealth : 0f;
            if (!group || !group.combatSquad || max <= 0f)
            {
                Show(0f, 0f, 0f, 0f, 1f, 0f);
                return;
            }

            // Not the members' own health fractions: those are shares of each one's maximum, the bar's are of the group's.
            float shields = 0f;
            float voidShields = 0f;
            float barriers = 0f;
            float lostMax = 0f;
            float currentMax = 0f;
            float cull = 0f;
            System.Collections.ObjectModel.ReadOnlyCollection<CharacterMaster> members = group.combatSquad.readOnlyMembersList;
            for (int i = 0; i < members.Count; i++)
            {
                CharacterMaster master = members[i];
                if (!master || !master.TryGetBody(out CharacterBody body)) continue;
                HealthComponent health = body.healthComponent;
                if (!health) continue;
                // The game's group sum counts only the members of its current name priority.
                float priority = master.TryGetComponent(out BossNamePriority named) ? named.priority : 0f;
                if (priority != group.currentPriority) continue;

                HealthComponent.HealthBarValues values = health.GetHealthBarValues();
                if (values.hasVoidShields)
                {
                    voidShields += health.shield;
                }
                else
                {
                    shields += health.shield;
                }
                barriers += health.barrier;
                currentMax += health.fullCombinedHealth;
                lostMax += health.fullHealth * (body.cursePenalty - 1f);
                // The game's fraction is of the member's maximum before the curse; back to health.
                cull += values.cullFraction * health.fullCombinedHealth * body.cursePenalty;
            }

            // The fill's end, and the barrier and the shield backwards from it, so the three end where the game's does
            // even when the members are read a frame apart from the group's sums.
            float total = Mathf.Clamp01(group.totalObservedHealth / max);
            float barrierEnd = total;
            float voidEnd = Mathf.Max(0f, barrierEnd - barriers / max);
            float shieldEnd = Mathf.Max(0f, voidEnd - voidShields / max);
            float shieldStart = Mathf.Max(0f, shieldEnd - shields / max);
            // A curse on a member that was cursed from its start took nothing from the observed maximum, so it shows
            // none; one that came later took as much as the maximum fell.
            float cursed = Mathf.Clamp01(Mathf.Min(lostMax / max, 1f - currentMax / max));
            Show(shieldStart, shieldEnd, voidEnd, barrierEnd, 1f - cursed, Mathf.Clamp01(cull / max));
        }

        // Draws the segments, changing only those whose place moved. The shield runs from its start to its end, the void
        // shield on from there, and the barrier on from that.
        private void Show(float shieldStart, float shieldEnd, float voidEnd, float barrierEnd, float curseStart,
            float executeAt)
        {
            bool shieldMoved = Moved(shownShieldStart, shieldStart) || Moved(shownShieldEnd, shieldEnd);
            bool voidMoved = shieldMoved || Moved(shownVoidEnd, voidEnd);
            bool barrierMoved = voidMoved || Moved(shownBarrierEnd, barrierEnd);
            if (shieldMoved)
            {
                Place(shield, shieldStart, shieldEnd);
            }
            if (voidMoved)
            {
                Place(voidShield, shieldEnd, voidEnd);
            }
            if (barrierMoved)
            {
                Place(barrier, voidEnd, barrierEnd);
            }
            shownShieldStart = shieldStart;
            shownShieldEnd = shieldEnd;
            shownVoidEnd = voidEnd;
            shownBarrierEnd = barrierEnd;
            if (Moved(shownCurseStart, curseStart))
            {
                shownCurseStart = curseStart;
                Place(curse, curseStart, 1f);
            }
            if (Moved(shownExecute, executeAt))
            {
                shownExecute = executeAt;
                execute.enabled = executeAt > 0f;
                RectTransform rect = execute.rectTransform;
                rect.anchorMin = new Vector2(executeAt, 0f);
                rect.anchorMax = new Vector2(executeAt, 1f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = Vector2.zero;
                rect.sizeDelta = new Vector2(ExecuteMarkWidth, 0f);
            }
        }

        private static bool Moved(float shown, float wanted) => Mathf.Abs(shown - wanted) > ChangeTolerance;

        // Filling the bar's height between two shares of its width; not drawn when it has no length.
        private static void Place(Image segment, float from, float to)
        {
            segment.enabled = to - from > ChangeTolerance;
            RectTransform rect = segment.rectTransform;
            rect.anchorMin = new Vector2(from, 0f);
            rect.anchorMax = new Vector2(to, 1f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static Image Segment(string name, RectTransform bar, Color color, Sprite sprite, Image.Type type,
            StyleRecorder recorder)
        {
            Image image = UiFactory.NewImage(name, bar, color);
            image.sprite = sprite;
            image.type = type;
            image.enabled = false;
            recorder.Add(image.gameObject);
            return image;
        }
    }
}
