using RoR2;
using RoR2.UI;
using UnityEngine;
using UnityEngine.UI;

namespace SevenUI
{
    // The boss bar drawn as the player's health bar is, over the game's single fill: the shield as a blue segment after
    // the health (pink for void shields), the barrier as a gold strip along the top from the bar's left end, the maximum
    // health a curse took as stripes at the end, Lunar Ruin over the health in the player bar's own style, and a line
    // where the boss is executed (frozen, or an elite under the shown player's Old Guillotine threshold). The game's
    // fill is the group's health, shield and barrier summed over its largest observed maximum; the parts are drawn from
    // the members' bodies in the same scale, and the fill's barrier share at the end (and any of it a frame behind) is
    // covered in the trough's color, since the player's barrier adds no length. The game's own trail follows that whole sum, so lost shield and barrier trailed too; it is hidden,
    // and SevenUI draws the player bar's instead: lost health only, under the shield, catching up as slowly, and on a
    // heal the new health in a lighter red until it catches up. Read from the group's members' bodies and the shown
    // player's, which the host and clients both have; nothing is sent or changed.
    internal sealed class BossBarEffects
    {
        public const string ShieldName = "SevenUIBossShield";
        public const string VoidShieldName = "SevenUIBossVoidShield";
        public const string BarrierName = "SevenUIBossBarrier";
        public const string BarrierCoverName = "SevenUIBossBarrierCover";
        public const string TrailName = "SevenUIBossTrail";
        public const string HealName = "SevenUIBossHeal";
        public const string LunarRuinName = "SevenUIBossLunarRuin";
        public const string CurseName = "SevenUIBossCurse";
        public const string ExecuteName = "SevenUIBossExecute";

        // The player bar's: its trail catches up with a smooth time of 0.2 s (the boss bar's own takes 0.1 s), and the
        // healed part shows only once it is more than 1% of the bar ahead of the trail.
        public const float TrailSmoothTime = 0.2f;
        public const float HealLead = 0.01f;
        // The execute line's width, in the top cluster's units.
        private const float ExecuteMarkWidth = 2f;
        private const float ChangeTolerance = 0.0001f;
        private static readonly Vector2 Unset = new Vector2(-1f, -1f);

        private readonly HUDBossHealthBarController boss;
        private readonly HUD hud;
        private readonly Image shield;
        private readonly Image voidShield;
        private readonly Image barrier;
        private readonly Image barrierCover;
        private readonly Image trail;
        private readonly Image heal;
        private readonly Image lunarRuin;
        private readonly float lunarRuinSize;
        private readonly Image curse;
        private readonly Image execute;
        // Where each part was last drawn, from and to; a part is drawn again only when its place moved by more than the
        // tolerance from there, so a part easing in small steps still lands where it should.
        private Vector2 shownShield = Unset;
        private Vector2 shownVoidShield = Unset;
        private Vector2 shownCover = Unset;
        private Vector2 shownBarrier = Unset;
        private Vector2 shownTrail = Unset;
        private Vector2 shownHeal = Unset;
        private Vector2 shownLunarRuin = Unset;
        private Vector2 shownCurse = Unset;
        private float shownExecute = -1f;
        private BossGroup trailGroup;
        private float trailShare = -1f;
        private float trailVelocity;
        private GameObject viewerObject;
        private CharacterBody viewer;

        // Where each part is drawn, as shares of the bar's width.
        private struct Shares
        {
            public float HealthEnd;
            public float ShieldEnd;
            public float VoidEnd;
            public float FillEnd;
            public float Barrier;
            public float TrailEnd;
            public float HealStart;
            public float LunarRuin;
            public float CurseStart;
            public float Execute;

            public static readonly Shares None = new Shares { CurseStart = 1f };
        }

        private BossBarEffects(HUDBossHealthBarController boss, HUD hud, Image shield, Image voidShield, Image barrier,
            Image barrierCover, Image trail, Image heal, Image lunarRuin, float lunarRuinSize, Image curse, Image execute)
        {
            this.boss = boss;
            this.hud = hud;
            this.shield = shield;
            this.voidShield = voidShield;
            this.barrier = barrier;
            this.barrierCover = barrierCover;
            this.trail = trail;
            this.heal = heal;
            this.lunarRuin = lunarRuin;
            this.lunarRuinSize = lunarRuinSize;
            this.curse = curse;
            this.execute = execute;
        }

        // Null when the boss bar has no fill panel to draw on.
        public static BossBarEffects Style(HUDBossHealthBarController boss, StyleRecorder recorder)
        {
            RectTransform bar = boss.fillRectImage ? boss.fillRectImage.transform.parent as RectTransform : null;
            if (!bar) return null;
            HUD hud = boss.GetComponentInParent<HUD>();

            // The game's trail of the whole sum; SevenUI draws the health's own. The game only turns its object on and
            // off, so the image stays off.
            recorder.Disable(boss.delayRectImage);
            Image barrierCover = Segment(BarrierCoverName, bar, Palette.Trough, FlatSprite.White, Image.Type.Simple,
                recorder);
            Image trail = Segment(TrailName, bar, Palette.BossHealthLost, FlatSprite.White, Image.Type.Simple, recorder);
            Image heal = Segment(HealName, bar, Palette.BossHealthHealing, FlatSprite.White, Image.Type.Simple, recorder);
            Image shield = Segment(ShieldName, bar, Palette.Shield, FlatSprite.White, Image.Type.Simple, recorder);
            Image voidShield = Segment(VoidShieldName, bar, Palette.VoidShield, FlatSprite.White, Image.Type.Simple,
                recorder);
            // The strip stays within the bar, as the player's barrier does.
            Image barrier = Segment(BarrierName, bar, Palette.Barrier, FlatSprite.TopStrip, Image.Type.Sliced, recorder);
            // Lunar Ruin in the player bar's own style, which SevenUI leaves as the game's.
            HealthBarStyle.BarStyle ruinStyle = hud && hud.healthBar && hud.healthBar._style
                ? hud.healthBar._style.lunarRuinStyle
                : default;
            Image lunarRuin = ruinStyle.enabled && ruinStyle.sprite
                ? Segment(LunarRuinName, bar, ruinStyle.baseColor, ruinStyle.sprite, ruinStyle.imageType, recorder)
                : null;
            Image curse = Segment(CurseName, bar, Color.white, FlatSprite.Hatch, Image.Type.Tiled, recorder);
            Image execute = Segment(ExecuteName, bar, Palette.ExecuteMark, FlatSprite.White, Image.Type.Simple, recorder);

            // Over the game's fill, in the player bar's order: the trail under the shield, the barrier and effects over
            // the health.
            int above = boss.fillRectImage.transform.GetSiblingIndex();
            foreach (Image image in new[] { barrierCover, trail, heal, shield, voidShield, barrier, lunarRuin, curse, execute })
            {
                if (!image) continue;
                image.transform.SetSiblingIndex(above + 1);
                above = image.transform.GetSiblingIndex();
            }
            return new BossBarEffects(boss, hud, shield, voidShield, barrier, barrierCover, trail, heal, lunarRuin,
                ruinStyle.sizeDelta, curse, execute);
        }

        public bool Alive => boss && shield && voidShield && barrier && barrierCover && trail && heal && curse && execute;

        // Per frame while a boss bar shows: a loop over the group's members reading their bodies' health values, and a
        // few floats compared with what is drawn.
        public void Update()
        {
            if (!boss.container.activeInHierarchy) return;
            BossGroup group = boss.currentBossGroup;
            float max = group ? group.totalMaxObservedMaxHealth : 0f;
            if (!group || !group.combatSquad || max <= 0f)
            {
                trailGroup = null;
                Show(Shares.None);
                return;
            }
            CharacterBody shownPlayer = Viewer();

            // Not the members' own health fractions: those are shares of each one's maximum, the bar's are of the group's.
            float healths = 0f;
            float shields = 0f;
            float voidShields = 0f;
            float barriers = 0f;
            float ruined = 0f;
            float lostMax = 0f;
            float currentMax = 0f;
            float executed = 0f;
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
                healths += health.health;
                if (values.hasVoidShields)
                {
                    voidShields += health.shield;
                }
                else
                {
                    shields += health.shield;
                }
                barriers += health.barrier;
                if (values.hasLunarRuin)
                {
                    ruined += health.health;
                }
                currentMax += health.fullCombinedHealth;
                lostMax += health.fullHealth * (body.cursePenalty - 1f);
                // The game's frozen fraction is of the member's maximum before the curse; back to its share of the
                // combined health, as Old Guillotine's is. The player bar takes the larger of the two for an elite.
                float threshold = values.cullFraction * body.cursePenalty;
                if (values.isElite && shownPlayer)
                {
                    threshold = Mathf.Max(threshold, shownPlayer.executeEliteHealthFraction);
                }
                executed += threshold * health.fullCombinedHealth;
            }

            // The health and the shields after it, from the bodies as they are now. The group's sum, which the game's fill
            // shows, is a frame behind them: worked back from it, a hit to the shield seemed to take health for a frame
            // and left a trail. The cover hides whatever of the fill runs past the shields.
            Shares shares = default;
            shares.FillEnd = Mathf.Clamp01(group.totalObservedHealth / max);
            shares.HealthEnd = Mathf.Clamp01(healths / max);
            shares.ShieldEnd = Mathf.Clamp01(shares.HealthEnd + shields / max);
            shares.VoidEnd = Mathf.Clamp01(shares.ShieldEnd + voidShields / max);
            shares.Barrier = Mathf.Clamp01(barriers / max);
            shares.LunarRuin = Mathf.Min(shares.HealthEnd, ruined / max);
            // A curse on a member that was cursed from its start took nothing from the observed maximum, so it shows
            // none; one that came later took as much as the maximum fell.
            shares.CurseStart = 1f - Mathf.Clamp01(Mathf.Min(lostMax / max, 1f - currentMax / max));
            shares.Execute = Mathf.Clamp01(executed / max);

            // The player bar's trail of the health alone, starting where the health is for a new group.
            if (group != trailGroup || trailShare < 0f)
            {
                trailGroup = group;
                trailShare = shares.HealthEnd;
                trailVelocity = 0f;
            }
            trailShare = Mathf.SmoothDamp(trailShare, shares.HealthEnd, ref trailVelocity, TrailSmoothTime,
                float.PositiveInfinity, Time.deltaTime);
            shares.TrailEnd = Mathf.Max(trailShare, shares.HealthEnd);
            shares.HealStart = Mathf.Min(trailShare + HealLead, shares.HealthEnd);
            Show(shares);
        }

        // The body the HUD shows, as the game's overhead bars use for Old Guillotine: a teammate's while spectating.
        private CharacterBody Viewer()
        {
            GameObject target = hud ? hud.targetBodyObject : null;
            if (target != viewerObject)
            {
                viewerObject = target;
                viewer = target ? target.GetComponent<CharacterBody>() : null;
            }
            return viewer;
        }

        // Draws the parts, changing only those whose place moved.
        private void Show(Shares wanted)
        {
            Place(shield, ref shownShield, wanted.HealthEnd, wanted.ShieldEnd);
            Place(voidShield, ref shownVoidShield, wanted.ShieldEnd, wanted.VoidEnd);
            Place(barrierCover, ref shownCover, wanted.VoidEnd, wanted.FillEnd);
            Place(barrier, ref shownBarrier, 0f, wanted.Barrier);
            Place(trail, ref shownTrail, wanted.HealthEnd, wanted.TrailEnd);
            Place(heal, ref shownHeal, wanted.HealStart, wanted.HealthEnd);
            if (lunarRuin)
            {
                Place(lunarRuin, ref shownLunarRuin, 0f, wanted.LunarRuin, lunarRuinSize);
            }
            Place(curse, ref shownCurse, wanted.CurseStart, 1f);
            if (Moved(shownExecute, wanted.Execute))
            {
                shownExecute = wanted.Execute;
                execute.enabled = wanted.Execute > 0f;
                RectTransform rect = execute.rectTransform;
                rect.anchorMin = new Vector2(wanted.Execute, 0f);
                rect.anchorMax = new Vector2(wanted.Execute, 1f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = Vector2.zero;
                rect.sizeDelta = new Vector2(ExecuteMarkWidth, 0f);
            }
        }

        private static bool Moved(float shown, float wanted) => Mathf.Abs(shown - wanted) > ChangeTolerance;

        // Fills the bar's height between two shares of its width, when that moved from where it was last drawn; not
        // drawn when it has no length. A size delta is the player bar's: some of its styles draw a part a little past
        // the bar's edges.
        private static void Place(Image segment, ref Vector2 shown, float from, float to, float sizeDelta = 0f)
        {
            // A part with no length is hidden at once, though the last step to it was too small to count as a move.
            if (to - from <= ChangeTolerance)
            {
                segment.enabled = false;
                shown = new Vector2(from, to);
                return;
            }
            if (!Moved(shown.x, from) && !Moved(shown.y, to) && segment.enabled) return;
            shown = new Vector2(from, to);
            segment.enabled = true;
            RectTransform rect = segment.rectTransform;
            rect.anchorMin = new Vector2(from, 0f);
            rect.anchorMax = new Vector2(to, 1f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = sizeDelta == 0f ? Vector2.zero : new Vector2(sizeDelta * 0.5f + 1f, sizeDelta + 1f);
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
