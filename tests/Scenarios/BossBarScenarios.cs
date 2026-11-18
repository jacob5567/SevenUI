using System.Collections;
using System.Collections.Generic;
using ModTestRunner;
using RoR2;
using RoR2.UI;

namespace SevenUI.Tests
{
    // The restyled boss bar's shield, barrier, curse and execute segments (BossBarEffects), on a boss group the test
    // makes of Lemurians: none for a boss with only health, each in its own place and color, summed over two bosses,
    // under the game's trail, and gone with FlatPanels off. The settings they need are set for the whole run.
    internal static class BossBarScenarios
    {
        private const float HurtHealth = 0.6f;
        private const float BarrierShare = 0.3f;
        private const float HitShare = 0.15f;
        private const float SecondHealth = 0.8f;
        private const int SecondShieldStacks = 3;

        public static IEnumerable<Scenario> All()
        {
            yield return Boss("boss_bar_health_only", HealthOnly);
            yield return Boss("boss_bar_shield", Shield);
            yield return Boss("boss_bar_void_shield", VoidShield);
            yield return Boss("boss_bar_barrier", Barrier);
            yield return Boss("boss_bar_curse", Curse);
            yield return Boss("boss_bar_execute_mark", ExecuteMark);
            yield return Boss("boss_bar_trail_behind_segments", TrailBehindSegments);
            yield return Boss("boss_bar_two_bosses_sum", TwoBossesSum);
            yield return Boss("boss_bar_flat_panels_switch", FlatPanelsSwitch);
        }

        private static Scenario Boss(string name, System.Func<Harness, IEnumerator> run) =>
            new Scenario(1, name, Stages.TitanicPlains, run);

        // A group of one hurt Lemurian: only the game's fill, and no segment of the mod's.
        private static IEnumerator HealthOnly(Harness h)
        {
            List<CharacterBody> bodies = new List<CharacterBody>();
            BossGroup group = null;
            yield return BossBarSetUp.Group(h, 1, bodies, found => group = found);
            h.SetHealthFraction(bodies[0], HurtHealth);
            yield return h.ExpectEffects(group, bodies, false, false, false, false, false);
            yield return h.ExpectSegmentsOverTrail();
        }

        // The shield starts where the health ends, runs for the shield's share of the group's maximum, and the segments
        // end where the game's fill does.
        private static IEnumerator Shield(Harness h)
        {
            List<CharacterBody> bodies = new List<CharacterBody>();
            BossGroup group = null;
            yield return BossBarSetUp.Group(h, 1, bodies, found => group = found);
            yield return BossBarSetUp.Shielded(h, bodies[0]);
            h.SetHealthFraction(bodies[0], HurtHealth);
            yield return h.ExpectEffects(group, bodies, true, false, false, false, false);
            yield return h.AimAway();
            yield return h.CaptureLooks("shield");
        }

        // Plasma Shrimp's shield draws in the void shield's color, as the game's own bars do, and the plain shield stays
        // off.
        private static IEnumerator VoidShield(Harness h)
        {
            List<CharacterBody> bodies = new List<CharacterBody>();
            BossGroup group = null;
            yield return BossBarSetUp.Group(h, 1, bodies, found => group = found);
            yield return BossBarSetUp.Shielded(h, bodies[0], BossBarSetUp.ShieldStacks, true);
            h.SetHealthFraction(bodies[0], HurtHealth);
            yield return h.ExpectEffects(group, bodies, false, true, false, false, false);
        }

        // Barrier given on the server shows as the gold strip after the health, as long as the barrier.
        private static IEnumerator Barrier(Harness h)
        {
            List<CharacterBody> bodies = new List<CharacterBody>();
            BossGroup group = null;
            yield return BossBarSetUp.Group(h, 1, bodies, found => group = found);
            h.SetHealthFraction(bodies[0], HurtHealth);
            HealthComponent health = bodies[0].healthComponent;
            // Server method; the runner is the host.
            health.AddBarrier(health.fullCombinedHealth * BarrierShare);
            yield return h.ExpectEffects(group, bodies, false, false, true, false, false);
            yield return h.AimAway();
            yield return h.CaptureLooks("barrier");
        }

        // A curse a boss gets after the bar first saw its health takes the end of the bar, which its health can no
        // longer reach.
        private static IEnumerator Curse(Harness h)
        {
            List<CharacterBody> bodies = new List<CharacterBody>();
            BossGroup group = null;
            yield return BossBarSetUp.Group(h, 1, bodies, found => group = found);
            yield return BossBarChecks.Cursed(h, bodies[0]);
            yield return h.ExpectEffects(group, bodies, false, false, false, true, false);
            yield return h.AimAway();
            yield return h.CaptureLooks("curse");
        }

        // A frozen boss that can be executed gets the line at 30% of its health; thawed, or immune to executes, it gets
        // none.
        private static IEnumerator ExecuteMark(Harness h)
        {
            List<CharacterBody> bodies = new List<CharacterBody>();
            BossGroup group = null;
            yield return BossBarSetUp.Group(h, 1, bodies, found => group = found);
            CharacterBody body = bodies[0];
            HealthComponent health = body.healthComponent;
            h.OnCleanup(() =>
            {
                if (health)
                {
                    health.isInFrozenState = false;
                }
            });
            h.SetHealthFraction(body, HurtHealth);
            yield return h.ExpectEffects(group, bodies, false, false, false, false, false);
            health.isInFrozenState = true;
            yield return h.ExpectEffects(group, bodies, false, false, false, false, true);
            yield return h.AimAway();
            yield return h.CaptureLooks("execute");

            CharacterBody.BodyFlags flags = body.bodyFlags;
            body.bodyFlags |= CharacterBody.BodyFlags.ImmuneToExecutes;
            yield return h.ExpectEffects(group, bodies, false, false, false, false, false);
            body.bodyFlags = flags;
            health.isInFrozenState = false;
            yield return h.ExpectEffects(group, bodies, false, false, false, false, false);
        }

        // Damage leaves the game's trail beyond the fill, then it drains to it, all under the shield and the barrier.
        private static IEnumerator TrailBehindSegments(Harness h)
        {
            List<CharacterBody> bodies = new List<CharacterBody>();
            BossGroup group = null;
            yield return BossBarSetUp.Group(h, 1, bodies, found => group = found);
            yield return BossBarSetUp.Shielded(h, bodies[0]);
            h.SetHealthFraction(bodies[0], HurtHealth);
            HealthComponent health = bodies[0].healthComponent;
            health.AddBarrier(health.fullCombinedHealth * BarrierShare);
            yield return h.ExpectEffects(group, bodies, true, false, true, false, false);
            yield return h.ExpectSegmentsOverTrail();
            HUDBossHealthBarController boss = BossBarSetUp.Bar(h);
            BossBarChecks.Hit(bodies[0], group.totalMaxObservedMaxHealth * HitShare);
            yield return h.ExpectBossTrail(boss, true);
            yield return h.ExpectBossTrail(boss, false);
            yield return h.ExpectEffects(group, bodies, true, false, true, false, false);
            yield return h.ExpectSegmentsOverTrail();
        }

        // Two bosses: the first's void shield and the second's plain shield and barrier, each summed over the group's
        // maximum and drawn in the shield, void shield and barrier order, ending at the fill.
        private static IEnumerator TwoBossesSum(Harness h)
        {
            List<CharacterBody> bodies = new List<CharacterBody>();
            BossGroup group = null;
            yield return BossBarSetUp.Group(h, 2, bodies, found => group = found);
            yield return BossBarSetUp.Shielded(h, bodies[0], BossBarSetUp.ShieldStacks, true);
            yield return BossBarSetUp.Shielded(h, bodies[1], SecondShieldStacks);
            h.SetHealthFraction(bodies[0], HurtHealth);
            h.SetHealthFraction(bodies[1], SecondHealth);
            HealthComponent second = bodies[1].healthComponent;
            second.AddBarrier(second.fullCombinedHealth * BarrierShare);
            yield return h.ExpectEffects(group, bodies, true, true, true, false, false);
            yield return h.AimAway();
            yield return h.CaptureLooks("two_bosses");
        }

        // With FlatPanels off the game's bar is back and the segments are gone; on again, they return in place.
        private static IEnumerator FlatPanelsSwitch(Harness h)
        {
            PluginConfig settings = SevenUIPlugin.Settings;
            h.OnCleanup(() => h.SetConfig(settings.FlatPanels, true));
            List<CharacterBody> bodies = new List<CharacterBody>();
            BossGroup group = null;
            yield return BossBarSetUp.Group(h, 1, bodies, found => group = found);
            yield return BossBarSetUp.Shielded(h, bodies[0]);
            h.SetHealthFraction(bodies[0], HurtHealth);
            HealthComponent health = bodies[0].healthComponent;
            health.AddBarrier(health.fullCombinedHealth * BarrierShare);
            yield return h.ExpectEffects(group, bodies, true, false, true, false, false);

            h.SetConfig(settings.FlatPanels, false);
            yield return h.ExpectNoEffects();
            yield return h.ExpectTopVanilla();
            h.SetConfig(settings.FlatPanels, true);
            yield return h.ExpectTopFlat();
            yield return h.ExpectEffects(group, bodies, true, false, true, false, false);
        }
    }
}
