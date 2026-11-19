using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ModTestRunner;
using RoR2;
using RoR2.UI;
using UnityEngine;
using UnityEngine.UI;

namespace SevenUI.Tests
{
    // The boss bar's parts (BossBarEffects), read as shares of the bar's width and compared with what the group's bodies
    // hold, summed in the game's scale: over the group's largest observed maximum, and with the player's health bar.
    internal static class BossBarChecks
    {
        // A share of the bar's width.
        private const float SpanTolerance = 0.02f;
        private const float MinimumSpan = 0.005f;
        // How long the trail must be right after a hit to count as one.
        private const float TrailShown = 0.05f;
        // How long a part that mustn't show is watched after a hit, longer than the player bar's trail takes.
        private const float NoTrailSeconds = 1f;
        private const float ExecuteShare = 0.3f;
        private const float CurseStacks = 50;

        // What the group's bodies hold, in the game's scale.
        private struct Held
        {
            public float Max;
            public float Health;
            public float Shield;
            public float VoidShield;
            public float Barrier;
            public float Ruined;
            public float CurrentMax;
            public float Execute;
        }

        private static Held Read(BossGroup group, List<CharacterBody> bodies)
        {
            Held held = new Held { Max = group.totalMaxObservedMaxHealth };
            foreach (CharacterBody body in bodies.Where(body => body && body.healthComponent))
            {
                HealthComponent health = body.healthComponent;
                HealthComponent.HealthBarValues values = health.GetHealthBarValues();
                held.Health += health.health;
                if (values.hasVoidShields)
                {
                    held.VoidShield += health.shield;
                }
                else
                {
                    held.Shield += health.shield;
                }
                held.Barrier += health.barrier;
                if (body.HasBuff(DLC2Content.Buffs.lunarruin))
                {
                    held.Ruined += health.health;
                }
                held.CurrentMax += health.fullCombinedHealth;
                // As the player's bar: frozen, or an elite under the shown player's Old Guillotine threshold.
                float threshold = health.isInFrozenState && (body.bodyFlags & CharacterBody.BodyFlags.ImmuneToExecutes) == 0
                    ? ExecuteShare
                    : 0f;
                if (body.isElite && Harness.LocalBody)
                {
                    threshold = Mathf.Max(threshold, Harness.LocalBody.executeEliteHealthFraction);
                }
                held.Execute += threshold * health.fullCombinedHealth;
            }
            return held;
        }

        private static Image Segment(HUDBossHealthBarController boss, string name)
        {
            Transform bar = boss.fillRectImage ? boss.fillRectImage.transform.parent : null;
            Transform found = bar ? bar.Find(name) : null;
            return found ? found.GetComponent<Image>() : null;
        }

        private static bool Drawn(Image segment) => segment && segment.enabled && segment.gameObject.activeInHierarchy;

        // The segment's left and right edges as shares of the bar's width, from where they are drawn.
        private static Vector2 Span(HUDBossHealthBarController boss, Image segment)
        {
            Vector3[] bar = new Vector3[4];
            Vector3[] drawn = new Vector3[4];
            ((RectTransform)boss.fillRectImage.transform.parent).GetWorldCorners(bar);
            segment.rectTransform.GetWorldCorners(drawn);
            float width = bar[3].x - bar[0].x;
            return new Vector2((drawn[0].x - bar[0].x) / width, (drawn[3].x - bar[0].x) / width);
        }

        private static bool SpanIs(HUDBossHealthBarController boss, Image segment, float from, float to)
        {
            if (!Drawn(segment)) return false;
            Vector2 span = Span(boss, segment);
            return Mathf.Abs(span.x - from) < SpanTolerance && Mathf.Abs(span.y - to) < SpanTolerance;
        }

        private static string Describe(HUDBossHealthBarController boss, string name)
        {
            Image segment = Segment(boss, name);
            if (!segment) return $"{name}: none";
            Vector2 span = Drawn(segment) ? Span(boss, segment) : Vector2.zero;
            return $"{name}: {(Drawn(segment) ? $"{span.x:0.###}-{span.y:0.###}" : "not drawn")}";
        }

        private static string Look(HUDBossHealthBarController boss, Held held, BossGroup group) =>
            $"fill {boss.fillRectImage.fillAmount:0.###}, held health {held.Health / held.Max:0.###} shield " +
            $"{held.Shield / held.Max:0.###} void {held.VoidShield / held.Max:0.###} barrier {held.Barrier / held.Max:0.###} " +
            $"ruin {held.Ruined / held.Max:0.###} max {held.CurrentMax / held.Max:0.###} execute {held.Execute / held.Max:0.###}; " +
            string.Join(", ", new[]
            {
                BossBarEffects.ShieldName, BossBarEffects.VoidShieldName, BossBarEffects.BarrierCoverName,
                BossBarEffects.BarrierName, BossBarEffects.TrailName, BossBarEffects.HealName,
                BossBarEffects.LunarRuinName, BossBarEffects.CurseName, BossBarEffects.ExecuteName,
            }.Select(name => Describe(boss, name)));

        // The settled bar as the bodies call for, each part drawn in its own place and color or not drawn at all: the
        // shield after the health, the void shield after that, the trough-colored cover over the barrier's share at the
        // end of the game's fill, the barrier strip from the bar's left end for that share, as on the player's bar,
        // Lunar Ruin over the ruined members' health in the player bar's style, the curse from where the bodies' maximum
        // ends to the end of the bar, and the execute mark where they would be executed; no trail or healed part, and
        // the game's own trail never drawn.
        public static IEnumerator ExpectEffects(this Harness h, BossGroup group, List<CharacterBody> bodies, bool shield,
            bool voidShield, bool barrier, bool curse, bool execute, bool lunarRuin = false)
        {
            HUDBossHealthBarController boss = BossBarSetUp.Bar(h);
            string failure = "no boss bar";
            yield return h.Until(
                () =>
                {
                    failure = Mismatch(boss, group, bodies, shield, voidShield, barrier, curse, execute, lunarRuin);
                    return failure == null;
                },
                () => $"the boss bar's segments ({failure}; {Look(boss, Read(group, bodies), group)}) {h.Diagnosis()}");
        }

        // Null when the bar is as expected, or what isn't.
        private static string Mismatch(HUDBossHealthBarController boss, BossGroup group, List<CharacterBody> bodies,
            bool shield, bool voidShield, bool barrier, bool curse, bool execute, bool lunarRuin)
        {
            if (!boss || !boss.fillRectImage) return "no boss bar";
            if (boss.delayRectImage && boss.delayRectImage.enabled) return "the game's own trail is drawn";
            Held held = Read(group, bodies);
            if (held.Max <= 0f) return "no observed maximum";
            float shieldStart = held.Health / held.Max;
            float shieldEnd = shieldStart + held.Shield / held.Max;
            float voidEnd = shieldEnd + held.VoidShield / held.Max;
            float barrierEnd = voidEnd + held.Barrier / held.Max;

            // The bodies' sums reach the game's fill, so the segments, which are cut from them, end there too.
            if (Mathf.Abs(barrierEnd - boss.fillRectImage.fillAmount) > SpanTolerance)
            {
                return $"the bodies' health, shield and barrier end at {barrierEnd:0.###}, not at the fill";
            }
            Image shieldImage = Segment(boss, BossBarEffects.ShieldName);
            Image voidImage = Segment(boss, BossBarEffects.VoidShieldName);
            Image barrierImage = Segment(boss, BossBarEffects.BarrierName);
            Image coverImage = Segment(boss, BossBarEffects.BarrierCoverName);
            Image curseImage = Segment(boss, BossBarEffects.CurseName);
            Image executeImage = Segment(boss, BossBarEffects.ExecuteName);
            Image trailImage = Segment(boss, BossBarEffects.TrailName);
            Image healImage = Segment(boss, BossBarEffects.HealName);
            Image ruinImage = Segment(boss, BossBarEffects.LunarRuinName);
            if (!shieldImage || !voidImage || !barrierImage || !coverImage || !curseImage || !executeImage || !trailImage ||
                !healImage) return "a segment is missing";
            if (Drawn(trailImage)) return "the trail is still drawn";
            if (Drawn(healImage)) return "the healed part is still drawn";

            if (shield)
            {
                if (!SpanIs(boss, shieldImage, shieldStart, shieldEnd)) return "shield misplaced";
                if (!StackedWith(shieldImage, Palette.Shield, FlatSprite.White)) return "shield's color or sprite";
            }
            else if (Drawn(shieldImage))
            {
                return "shield drawn";
            }
            if (voidShield)
            {
                if (!SpanIs(boss, voidImage, shieldEnd, voidEnd)) return "void shield misplaced";
                if (!StackedWith(voidImage, Palette.VoidShield, FlatSprite.White)) return "void shield's color or sprite";
            }
            else if (Drawn(voidImage))
            {
                return "void shield drawn";
            }
            if (barrier)
            {
                if (!SpanIs(boss, barrierImage, 0f, held.Barrier / held.Max)) return "barrier doesn't start at the bar's left end";
                if (!StackedWith(barrierImage, Palette.Barrier, FlatSprite.TopStrip)) return "barrier's color or sprite";
                if (!SpanIs(boss, coverImage, voidEnd, barrierEnd)) return "the barrier's share of the fill isn't covered";
                if (!StackedWith(coverImage, Palette.Trough, FlatSprite.White)) return "barrier cover's color or sprite";
                if (barrierImage.transform.GetSiblingIndex() < coverImage.transform.GetSiblingIndex()) return "barrier under its cover";
            }
            else if (Drawn(barrierImage) || Drawn(coverImage))
            {
                return "barrier drawn";
            }
            if (lunarRuin)
            {
                HealthBarStyle.BarStyle style = PlayerStyle().lunarRuinStyle;
                if (!ruinImage) return "no Lunar Ruin part";
                if (!SpanIs(boss, ruinImage, 0f, held.Ruined / held.Max)) return "Lunar Ruin misplaced";
                if (ruinImage.sprite != style.sprite || !SevenUIChecks.Near(ruinImage.color, style.baseColor))
                {
                    return "Lunar Ruin not in the player bar's style";
                }
            }
            else if (ruinImage && Drawn(ruinImage))
            {
                return "Lunar Ruin drawn";
            }
            if (curse)
            {
                if (!SpanIs(boss, curseImage, held.CurrentMax / held.Max, 1f)) return "curse misplaced";
                if (curseImage.sprite != FlatSprite.Hatch || curseImage.type != Image.Type.Tiled) return "curse not striped";
            }
            else if (Drawn(curseImage))
            {
                return "curse drawn";
            }
            if (execute)
            {
                float at = held.Execute / held.Max;
                if (!Drawn(executeImage)) return "execute mark not drawn";
                Vector2 span = Span(boss, executeImage);
                if (Mathf.Abs((span.x + span.y) / 2f - at) > SpanTolerance || span.y - span.x <= 0f)
                {
                    return $"execute mark misplaced (should be at {at:0.###})";
                }
                if (!StackedWith(executeImage, Palette.ExecuteMark, FlatSprite.White)) return "execute mark's color";
            }
            else if (Drawn(executeImage))
            {
                return "execute mark drawn";
            }
            return null;
        }

        // The local player's health bar style, whose Lunar Ruin the boss bar copies.
        internal static HealthBarStyle PlayerStyle()
        {
            HUD hud = HUD.readOnlyInstanceList.FirstOrDefault(found => found && found.healthBar);
            return hud ? hud.healthBar._style : null;
        }

        private static bool StackedWith(Image image, Color color, Sprite sprite) =>
            SevenUIChecks.Near(image.color, color) && image.sprite == sprite;

        // Every segment the mod draws is gone or not drawn.
        public static IEnumerator ExpectNoEffects(this Harness h)
        {
            HUDBossHealthBarController boss = BossBarSetUp.Bar(h);
            string[] names =
            {
                BossBarEffects.ShieldName, BossBarEffects.VoidShieldName, BossBarEffects.BarrierCoverName,
                BossBarEffects.BarrierName, BossBarEffects.TrailName, BossBarEffects.HealName,
                BossBarEffects.LunarRuinName, BossBarEffects.CurseName, BossBarEffects.ExecuteName,
            };
            yield return h.Until(
                () => boss && names.All(name => !Drawn(Segment(boss, name))),
                () => $"no mod segments on the boss bar ({string.Join(", ", names.Select(name => Describe(boss, name)))}) " +
                      h.Diagnosis());
        }

        // The parts sit on the fill's panel over the game's fill, in the player bar's order: the cover, then the trail
        // under the shield, the healed part, the shields, the barrier and Lunar Ruin over the health, then the curse and
        // the execute mark.
        public static IEnumerator ExpectSegmentsOverTrail(this Harness h)
        {
            HUDBossHealthBarController boss = BossBarSetUp.Bar(h);
            string[] order =
            {
                BossBarEffects.BarrierCoverName, BossBarEffects.TrailName, BossBarEffects.HealName, BossBarEffects.ShieldName,
                BossBarEffects.VoidShieldName, BossBarEffects.BarrierName, BossBarEffects.LunarRuinName,
                BossBarEffects.CurseName, BossBarEffects.ExecuteName,
            };
            yield return h.Until(
                () =>
                {
                    int below = boss.fillRectImage.transform.GetSiblingIndex();
                    foreach (string name in order)
                    {
                        Image segment = Segment(boss, name);
                        // Lunar Ruin is only made when the player's style draws it.
                        if (!segment && name == BossBarEffects.LunarRuinName) continue;
                        if (!segment || segment.transform.parent != boss.fillRectImage.transform.parent) return false;
                        if (segment.transform.GetSiblingIndex() <= below) return false;
                        below = segment.transform.GetSiblingIndex();
                    }
                    return true;
                },
                () => $"the parts over the fill in the player bar's order {h.Diagnosis()}");
        }

        // Right after a hit to its health the trail runs from the health's end, as long as the hit; a moment later it
        // has caught up and is gone. The game's own trail stays hidden.
        public static IEnumerator ExpectHealthTrail(this Harness h, HUDBossHealthBarController boss, bool trailing)
        {
            yield return h.Until(
                () =>
                {
                    Image trail = Segment(boss, BossBarEffects.TrailName);
                    if (!trail || (boss.delayRectImage && boss.delayRectImage.enabled)) return false;
                    if (!trailing) return !Drawn(trail);
                    if (!Drawn(trail)) return false;
                    Vector2 span = Span(boss, trail);
                    return span.y - span.x > TrailShown;
                },
                () => $"the boss's lost health {(trailing ? "trailing after the health" : "caught up")} " +
                      $"({Describe(boss, BossBarEffects.TrailName)}) {h.Diagnosis()}");
        }

        // A hit that only takes shield or barrier leaves no trail, as on the player's bar, for longer than a trail takes.
        public static IEnumerator ExpectNoTrail(this Harness h, HUDBossHealthBarController boss)
        {
            yield return h.Holds(
                () => !Drawn(Segment(boss, BossBarEffects.TrailName)) && !(boss.delayRectImage && boss.delayRectImage.enabled),
                () => $"no trail after a hit that took no health ({Describe(boss, BossBarEffects.TrailName)})",
                NoTrailSeconds);
        }

        // Right after a heal the new health shows in the lighter red, from the trail's end to the health's; a moment
        // later the trail has caught up and it is gone.
        public static IEnumerator ExpectHealedPart(this Harness h, HUDBossHealthBarController boss, bool healing)
        {
            yield return h.Until(
                () =>
                {
                    Image heal = Segment(boss, BossBarEffects.HealName);
                    if (!heal) return false;
                    if (!healing) return !Drawn(heal);
                    if (!Drawn(heal) || !StackedWith(heal, Palette.BossHealthHealing, FlatSprite.White)) return false;
                    Vector2 span = Span(boss, heal);
                    return span.y - span.x > TrailShown;
                },
                () => $"the healed health {(healing ? "drawn in the lighter red" : "caught up")} " +
                      $"({Describe(boss, BossBarEffects.HealName)}) {h.Diagnosis()}");
        }

        public static void Hit(CharacterBody body, float damage)
        {
            body.healthComponent.TakeDamage(new DamageInfo
            {
                attacker = Harness.LocalBody.gameObject,
                damage = damage,
                damageType = DamageType.BypassArmor,
                position = body.corePosition,
                procCoefficient = 0f,
                damageColorIndex = DamageColorIndex.Default,
            });
        }

        // Curses a body for a third of its maximum health, as the game counts a curse: a stack adds 0.01 to the penalty.
        public static IEnumerator Cursed(Harness h, CharacterBody body)
        {
            for (int i = 0; i < CurseStacks; i++)
            {
                body.AddBuff(RoR2Content.Buffs.PermanentCurse);
            }
            yield return h.Until(() => body.cursePenalty > 1f + CurseStacks * 0.01f * 0.9f,
                () => $"the body's curse penalty ({body.cursePenalty}) {h.Diagnosis()}");
        }
    }
}
