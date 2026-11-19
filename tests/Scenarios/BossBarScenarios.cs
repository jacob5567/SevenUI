using System.Collections;
using System.Collections.Generic;
using ModTestRunner;
using RoR2;
using RoR2.UI;

namespace SevenUI.Tests
{
    // The restyled boss bar's parts (BossBarEffects), on a boss group the test makes of Lemurians, checked against the
    // player's health bar: none for a boss with only health, each in its own place and color, summed over two bosses,
    // a trail for lost health only, the healed part, Lunar Ruin, Old Guillotine's line on an elite, and gone with
    // FlatPanels off. The settings they need are set for the whole run.
    internal static class BossBarScenarios
    {
        private const float HurtHealth = 0.6f;
        private const float BarrierShare = 0.3f;
        private const float HitShare = 0.15f;
        // Hits that leave some of the shield or barrier they land on.
        private const float SoakedShare = 0.5f;
        private const float HealShare = 0.2f;
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
            yield return Boss("boss_bar_shield_loss_not_trailed", ShieldLossNotTrailed);
            yield return Boss("boss_bar_barrier_loss_not_trailed", BarrierLossNotTrailed);
            yield return Boss("boss_bar_heal_shown", HealShown);
            yield return Boss("boss_bar_lunar_ruin", LunarRuin);
            yield return Boss("boss_bar_guillotine_on_elite", GuillotineOnElite);
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

        // Barrier given on the server shows as the gold strip from the bar's left end, as long as the barrier, as on the player's bar.
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

        // A hit to the health leaves the trail from the health's end, which catches up and goes, with the parts in the
        // player bar's order.
        private static IEnumerator TrailBehindSegments(Harness h)
        {
            List<CharacterBody> bodies = new List<CharacterBody>();
            BossGroup group = null;
            yield return BossBarSetUp.Group(h, 1, bodies, found => group = found);
            h.SetHealthFraction(bodies[0], HurtHealth);
            yield return h.ExpectEffects(group, bodies, false, false, false, false, false);
            yield return h.ExpectSegmentsOverTrail();
            HUDBossHealthBarController boss = BossBarSetUp.Bar(h);
            BossBarChecks.Hit(bodies[0], group.totalMaxObservedMaxHealth * HitShare);
            yield return h.ExpectHealthTrail(boss, true);
            yield return h.AimAway();
            yield return h.CaptureLooks("trail");
            yield return h.ExpectHealthTrail(boss, false);
            yield return h.ExpectEffects(group, bodies, false, false, false, false, false);
            yield return h.ExpectSegmentsOverTrail();
        }

        // A hit the shield soaks shortens the shield and leaves no trail, as on the player's bar.
        private static IEnumerator ShieldLossNotTrailed(Harness h)
        {
            List<CharacterBody> bodies = new List<CharacterBody>();
            BossGroup group = null;
            yield return BossBarSetUp.Group(h, 1, bodies, found => group = found);
            yield return BossBarSetUp.Shielded(h, bodies[0]);
            h.SetHealthFraction(bodies[0], HurtHealth);
            yield return h.ExpectEffects(group, bodies, true, false, false, false, false);
            HUDBossHealthBarController boss = BossBarSetUp.Bar(h);
            BossBarChecks.Hit(bodies[0], bodies[0].healthComponent.shield * SoakedShare);
            yield return h.ExpectNoTrail(boss);
            yield return h.ExpectEffects(group, bodies, true, false, false, false, false);
        }

        // A hit the barrier soaks shortens the gold strip and leaves no trail, as on the player's bar.
        private static IEnumerator BarrierLossNotTrailed(Harness h)
        {
            List<CharacterBody> bodies = new List<CharacterBody>();
            BossGroup group = null;
            yield return BossBarSetUp.Group(h, 1, bodies, found => group = found);
            h.SetHealthFraction(bodies[0], HurtHealth);
            HealthComponent health = bodies[0].healthComponent;
            // Server method; the runner is the host.
            health.AddBarrier(health.fullCombinedHealth * BarrierShare);
            yield return h.ExpectEffects(group, bodies, false, false, true, false, false);
            HUDBossHealthBarController boss = BossBarSetUp.Bar(h);
            BossBarChecks.Hit(bodies[0], health.barrier * SoakedShare);
            yield return h.ExpectNoTrail(boss);
            yield return h.ExpectEffects(group, bodies, false, false, true, false, false);
            yield return h.AimAway();
            yield return h.CaptureLooks("barrier_hit");
        }

        // A heal shows the new health in the lighter red until the trail catches up, as on the player's bar.
        private static IEnumerator HealShown(Harness h)
        {
            List<CharacterBody> bodies = new List<CharacterBody>();
            BossGroup group = null;
            yield return BossBarSetUp.Group(h, 1, bodies, found => group = found);
            h.SetHealthFraction(bodies[0], HurtHealth);
            yield return h.ExpectEffects(group, bodies, false, false, false, false, false);
            HUDBossHealthBarController boss = BossBarSetUp.Bar(h);
            HealthComponent health = bodies[0].healthComponent;
            // Server method; the runner is the host.
            health.Heal(health.fullHealth * HealShare, default);
            yield return h.ExpectHealedPart(boss, true);
            yield return h.AimAway();
            yield return h.CaptureLooks("heal");
            yield return h.ExpectHealedPart(boss, false);
            yield return h.ExpectEffects(group, bodies, false, false, false, false, false);
        }

        // Lunar Ruin covers the health in the player bar's own style, and goes with the debuff.
        private static IEnumerator LunarRuin(Harness h)
        {
            HealthBarStyle style = BossBarChecks.PlayerStyle();
            if (!style || !style.lunarRuinStyle.enabled || !style.lunarRuinStyle.sprite)
            {
                throw h.Skip("the player's health bar style doesn't draw Lunar Ruin");
            }
            List<CharacterBody> bodies = new List<CharacterBody>();
            BossGroup group = null;
            yield return BossBarSetUp.Group(h, 1, bodies, found => group = found);
            CharacterBody body = bodies[0];
            h.SetHealthFraction(body, HurtHealth);
            yield return h.ExpectEffects(group, bodies, false, false, false, false, false);
            // Server method; the runner is the host.
            body.AddBuff(DLC2Content.Buffs.lunarruin);
            yield return h.ExpectEffects(group, bodies, false, false, false, false, false, true);
            yield return h.ExpectSegmentsOverTrail();
            yield return h.AimAway();
            yield return h.CaptureLooks("lunar_ruin");
            body.RemoveBuff(DLC2Content.Buffs.lunarruin);
            yield return h.ExpectEffects(group, bodies, false, false, false, false, false);
        }

        // With Old Guillotine on the player, an elite boss gets the execute line at the item's threshold, as an elite's
        // overhead bar does; a boss that isn't elite gets none.
        private static IEnumerator GuillotineOnElite(Harness h)
        {
            CharacterBody player = Harness.LocalBody;
            Inventory playerItems = player ? player.inventory : null;
            if (!playerItems) throw h.Skip("no player inventory");
            List<CharacterBody> bodies = new List<CharacterBody>();
            BossGroup group = null;
            yield return BossBarSetUp.Group(h, 1, bodies, found => group = found);
            CharacterBody body = bodies[0];
            h.SetHealthFraction(body, HurtHealth);

            // Server methods; the runner is the host.
            playerItems.GiveItemPermanent(RoR2Content.Items.ExecuteLowHealthElite);
            h.OnCleanup(() =>
            {
                if (playerItems)
                {
                    playerItems.RemoveItemPermanent(RoR2Content.Items.ExecuteLowHealthElite);
                }
            });
            yield return h.Until(() => player.executeEliteHealthFraction > 0f, "Old Guillotine's threshold on the player");
            yield return h.ExpectEffects(group, bodies, false, false, false, false, false);

            yield return h.Until(() => body.inventory, "the boss's inventory");
            body.inventory.SetEquipmentIndex(RoR2Content.Equipment.AffixRed.equipmentIndex, EquipmentLocation.zero);
            yield return h.Until(() => body.isElite, "the boss turned elite");
            yield return h.ExpectEffects(group, bodies, false, false, false, false, true);
            yield return h.AimAway();
            yield return h.CaptureLooks("guillotine");
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
