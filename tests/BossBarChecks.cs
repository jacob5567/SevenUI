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
    // The boss bar's shield, barrier, curse and execute segments (BossBarEffects), read as shares of the bar's width and
    // compared with what the group's bodies hold, summed in the game's scale: over the group's largest observed maximum.
    internal static class BossBarChecks
    {
        // A share of the bar's width.
        private const float SpanTolerance = 0.02f;
        private const float MinimumSpan = 0.005f;
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
                held.CurrentMax += health.fullCombinedHealth;
                if (health.isInFrozenState && (body.bodyFlags & CharacterBody.BodyFlags.ImmuneToExecutes) == 0)
                {
                    held.Execute += ExecuteShare * health.fullCombinedHealth;
                }
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
            $"max {held.CurrentMax / held.Max:0.###} execute {held.Execute / held.Max:0.###}; " +
            string.Join(", ", new[]
            {
                BossBarEffects.ShieldName, BossBarEffects.VoidShieldName, BossBarEffects.BarrierName,
                BossBarEffects.CurseName, BossBarEffects.ExecuteName,
            }.Select(name => Describe(boss, name)));

        // The segments as the bodies call for, each drawn in its own place and color or not drawn at all: the shield after
        // the health, the void shield after that, the barrier after that so the three end where the game's fill does, the
        // curse from where the bodies' maximum ends to the end of the bar, and the execute mark where the frozen ones
        // would be executed.
        public static IEnumerator ExpectEffects(this Harness h, BossGroup group, List<CharacterBody> bodies, bool shield,
            bool voidShield, bool barrier, bool curse, bool execute)
        {
            HUDBossHealthBarController boss = BossBarSetUp.Bar(h);
            string failure = "no boss bar";
            yield return h.Until(
                () =>
                {
                    failure = Mismatch(boss, group, bodies, shield, voidShield, barrier, curse, execute);
                    return failure == null;
                },
                () => $"the boss bar's segments ({failure}; {Look(boss, Read(group, bodies), group)}) {h.Diagnosis()}");
        }

        // Null when the bar is as expected, or what isn't.
        private static string Mismatch(HUDBossHealthBarController boss, BossGroup group, List<CharacterBody> bodies,
            bool shield, bool voidShield, bool barrier, bool curse, bool execute)
        {
            if (!boss || !boss.fillRectImage) return "no boss bar";
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
            Image curseImage = Segment(boss, BossBarEffects.CurseName);
            Image executeImage = Segment(boss, BossBarEffects.ExecuteName);
            if (!shieldImage || !voidImage || !barrierImage || !curseImage || !executeImage) return "a segment is missing";

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
                if (!SpanIs(boss, barrierImage, voidEnd, barrierEnd)) return "barrier misplaced";
                if (!StackedWith(barrierImage, Palette.Barrier, FlatSprite.TopStrip)) return "barrier's color or sprite";
            }
            else if (Drawn(barrierImage))
            {
                return "barrier drawn";
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

        private static bool StackedWith(Image image, Color color, Sprite sprite) =>
            SevenUIChecks.Near(image.color, color) && image.sprite == sprite;

        // Every segment the mod draws is gone or not drawn.
        public static IEnumerator ExpectNoEffects(this Harness h)
        {
            HUDBossHealthBarController boss = BossBarSetUp.Bar(h);
            string[] names =
            {
                BossBarEffects.ShieldName, BossBarEffects.VoidShieldName, BossBarEffects.BarrierName,
                BossBarEffects.CurseName, BossBarEffects.ExecuteName,
            };
            yield return h.Until(
                () => boss && names.All(name => !Drawn(Segment(boss, name))),
                () => $"no mod segments on the boss bar ({string.Join(", ", names.Select(name => Describe(boss, name)))}) " +
                      h.Diagnosis());
        }

        // The game's trail of lost health and its fill are under every segment, and the segments sit on the fill's panel.
        public static IEnumerator ExpectSegmentsOverTrail(this Harness h)
        {
            HUDBossHealthBarController boss = BossBarSetUp.Bar(h);
            yield return h.Until(
                () =>
                {
                    int trail = boss.delayRectImage.transform.GetSiblingIndex();
                    int fill = boss.fillRectImage.transform.GetSiblingIndex();
                    return new[]
                    {
                        BossBarEffects.ShieldName, BossBarEffects.VoidShieldName, BossBarEffects.BarrierName,
                        BossBarEffects.CurseName, BossBarEffects.ExecuteName,
                    }.All(name =>
                    {
                        Image segment = Segment(boss, name);
                        return segment && segment.transform.parent == boss.fillRectImage.transform.parent &&
                               segment.transform.GetSiblingIndex() > Mathf.Max(trail, fill);
                    });
                },
                () => $"the boss's trail and fill drawn under every segment {h.Diagnosis()}");
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
