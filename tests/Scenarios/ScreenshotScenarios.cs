using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ModTestRunner;
using RoR2;
using RoR2.ExpansionManagement;
using RoR2.UI;
using UnityEngine;

namespace SevenUI.Tests
{
    // Not checks: sets up each part of the HUD SevenUI restyles and saves a screenshot of it twice, in SevenUI's look and
    // with the mod's settings off, for comparing. Run with mod_test --mod SevenUI --suite screenshots; the runner logs
    // where the files went. Tooltips need the pointer, so they aren't shot. The
    // survivor shots turn the player into each survivor that adds HUD elements of its own, and skip the ones whose
    // expansion the player doesn't have.
    internal static class ScreenshotScenarios
    {
        private const string Suite = "screenshots";
        // Long enough for new HUD objects, restyling and the switch between looks to settle, and short enough that a
        // pickup notification or damage numbers are still showing.
        private const float SettleSeconds = 0.5f;
        private const float AllyCardSeconds = 1.5f;
        // The game ignores a new pickup for its first half second (GenericPickupController.waitDuration).
        private const float PickupWaitSeconds = 0.6f;
        private const float PickupTimeout = 5f;
        // The teleporter's boss takes a while to arrive once the event starts.
        private const float BossTimeout = 45f;
        private const int ShownKarma = 4;
        // Shares of the boss's health, leaving it at the canvas's 62% with 8% just lost.
        private const float FirstBossHit = 0.3f;
        private const float SecondBossHit = 0.08f;
        // Enough for the drones and the chest, at any difficulty or stage count the tests reach.
        private const int Money = 2000;
        private const float BarrierFraction = 0.4f;
        private const float LowHealthFraction = 0.25f;
        private static readonly float[] DamageHits = { 24f, 180f, 1350f, 26000f };
        // Each stack of BoostHp adds 10% to max health (what the game gives bosses for theirs), so the biggest hit
        // shows as thousands without killing the monster.
        private const int BoostHpStacks = 500;
        // Far enough back that the Beetle Queen doesn't fill the view, and the numbers are spread across it.
        private const float DamageNumberStandDistance = 14f;
        private const float DamageNumberSpread = 2.5f;
        private const float TeleportSettleSeconds = 0.2f;
        // A monster's own state machine, and long enough for the Beetle Queen's 4-second spawn.
        private const string BodyStateMachine = "Body";
        private const float SpawnTimeout = 10f;
        // For finding open ground for the Beetle Queen: a few directions with a clear view from about her stand
        // distance, measured from her middle (about 4 m up, and as wide).
        private const int ViewDirections = 16;
        private const int MinClearViews = 4;
        private const float MonsterMiddleHeight = 4f;
        private const float MonsterRadius = 4f;
        private const float EyeHeight = 1.8f;
        private const float GroundSearchHeight = 15f;
        // Long enough for the new body's HUD overlays to be made and restyled.
        private const float NewBodySeconds = 2f;
        private const float NewBodyTimeout = 10f;
        // Half of Void Fiend's corruption, so the meter shows a level and not empty.
        private const float VoidFiendCorruption = 50f;

        public static IEnumerable<Scenario> All()
        {
            yield return Shot("hud", Hud);
            yield return Shot("scoreboard", Scoreboard);
            yield return Shot("barrier_and_low_health", BarrierAndLowHealth);
            yield return Shot("cooldowns", Cooldowns);
            yield return Shot("ntormat_pages", NtormatPages);
            yield return Shot("eclipse_and_karma", EclipseAndKarma);
            yield return Shot("interaction_prompt", InteractionPrompt);
            yield return Shot("pickup_notification", PickupNotification);
            yield return Shot("ally_cards", AllyCards);
            yield return Shot("damage_numbers", DamageNumbers);
            yield return Survivor("railgunner", GameNames.Railgunner);
            yield return Survivor("void_fiend", GameNames.VoidFiend, AddCorruption);
            yield return Survivor("seeker", GameNames.Seeker);
            yield return Survivor("drone_tech", GameNames.DroneTech);
            yield return Survivor("drifter", GameNames.Drifter);
            yield return Survivor("acolyte", GameNames.Acolyte);
            // Starts the teleporter, which changes the stage, so it goes last.
            yield return new Scenario(2, Suite, "boss_bar", Stages.TitanicPlains, BossBar, onlyWhenNamed: true);
        }

        private static Scenario Shot(string name, System.Func<Harness, IEnumerator> run) =>
            new Scenario(1, Suite, name, Stages.TitanicPlains, run, onlyWhenNamed: true);

        private static Scenario Survivor(string name, string body, System.Action<CharacterBody> setUp = null) =>
            Shot($"survivor_{name}", h => AsSurvivor(h, body, setUp));

        // Turns the player into the survivor, shoots the HUD with its own elements, and turns the player back at
        // cleanup.
        private static IEnumerator AsSurvivor(Harness h, string bodyName, System.Action<CharacterBody> setUp)
        {
            GameObject prefab = BodyCatalog.FindBodyPrefab(bodyName);
            if (!prefab) throw h.Skip($"no body called {bodyName}");
            CharacterMaster master = Harness.LocalBody.master;
            ExpansionRequirementComponent expansion = prefab.GetComponent<ExpansionRequirementComponent>();
            if (expansion && !expansion.PlayerCanUseBody(master.playerCharacterMasterController))
            {
                throw h.Skip($"{bodyName} needs an expansion the player doesn't have");
            }

            string original = master.bodyPrefab.name;
            h.OnCleanup(() => h.Cmd($"{GameNames.SpawnAs} {original}"));
            h.Cmd($"{GameNames.SpawnAs} {bodyName}");
            BodyIndex index = BodyCatalog.FindBodyIndex(bodyName);
            yield return h.Until(() => Harness.LocalBody && Harness.LocalBody.bodyIndex == index, $"the player as {bodyName}",
                NewBodyTimeout);
            setUp?.Invoke(Harness.LocalBody);
            yield return h.Wait(NewBodySeconds);
            yield return h.AimAway();
            yield return h.CaptureLooks();
        }

        // A server method; the runner is the host.
        private static void AddCorruption(CharacterBody body)
        {
            VoidSurvivorController controller = body.GetComponent<VoidSurvivorController>();
            if (controller)
            {
                controller.AddCorruption(VoidFiendCorruption);
            }
        }

        // A spread of item tiers with stacks, a shield, an equipment and money: most of the HUD at once.
        private static IEnumerator Hud(Harness h)
        {
            GiveItemSpread(h);
            h.Cmd($"{Commands.GiveItem} {GameNames.PersonalShield} 2");
            h.Cmd($"{Commands.GiveEquipment} {GameNames.Fruit}");
            h.Cmd($"{Commands.GiveMoney} {Money}");
            yield return h.Wait(SettleSeconds);
            yield return h.CaptureLooks();
        }

        // The tab screen, with items in the player's row. The HUD closes the scoreboard every frame unless the info key
        // is held or it was opened as a toggle, as the pause menu's button opens it; closing it ends the toggle.
        private static IEnumerator Scoreboard(Harness h)
        {
            GiveItemSpread(h);
            h.Cmd($"{Commands.GiveEquipment} {GameNames.Fruit}");
            LocalUser user = LocalUserManager.GetFirstLocalUser();
            h.OnCleanup(() => HUD.SetScoreboardActive(user, false, EScoreboardDeactivateReturnContext.Game));
            HUD.SetScoreboardActive(user, true, EScoreboardDeactivateReturnContext.Game, shouldForceScoreboardToggle: true);
            yield return h.Until(() => HUD.IsScoreboardActive(user), "the scoreboard open");
            yield return h.Wait(SettleSeconds);
            yield return h.CaptureLooks();
        }

        private static IEnumerator NtormatPages(Harness h)
        {
            yield return h.GiveNtormat();
            yield return h.Wait(SettleSeconds);
            yield return h.CaptureLooks();
        }

        // One item of each main tier, and a stack of a common one.
        private static void GiveItemSpread(Harness h)
        {
            foreach (ItemTier tier in new[] { ItemTier.Tier1, ItemTier.Tier2, ItemTier.Tier3, ItemTier.Lunar, ItemTier.Boss,
                         ItemTier.VoidTier1 })
            {
                string item = Harness.ItemOfTier(tier);
                if (item != null)
                {
                    h.Cmd($"{Commands.GiveItem} {item} {(tier == ItemTier.Tier1 ? 4 : 1)}");
                }
            }
        }

        private static IEnumerator BarrierAndLowHealth(Harness h)
        {
            h.Cmd($"{Commands.GiveItem} {GameNames.PersonalShield} 1");
            yield return h.Wait(SettleSeconds);
            HealthComponent health = Harness.LocalBody.healthComponent;
            h.SetHealthFraction(Harness.LocalBody, LowHealthFraction);
            // A server method; the runner is the host. Barrier decays, so the screenshots follow at once.
            health.AddBarrier(health.fullCombinedHealth * BarrierFraction);
            yield return h.Wait(SettleSeconds);
            yield return h.CaptureLooks();
        }

        // Spending a stock starts the skill recharging, which shows the cooldown cover and number.
        private static IEnumerator Cooldowns(Harness h)
        {
            h.Cmd($"{Commands.GiveEquipment} {GameNames.Fruit}");
            SkillLocator skills = Harness.LocalBody.skillLocator;
            foreach (GenericSkill skill in new[] { skills.secondary, skills.utility, skills.special })
            {
                if (skill && skill.stock > 0)
                {
                    skill.DeductStock(skill.stock);
                }
            }
            yield return h.Wait(SettleSeconds);
            yield return h.CaptureLooks();
        }

        private static IEnumerator InteractionPrompt(Harness h)
        {
            // The game shows no action for a chest the player can't afford.
            h.Cmd($"{Commands.GiveMoney} {Money}");
            PurchaseInteraction chest = null;
            yield return h.Spawned<PurchaseInteraction>($"{Commands.SpawnInteractable} {GameNames.SmallChest}", found => chest = found);
            yield return h.StandBefore(chest.gameObject);
            yield return h.Wait(SettleSeconds);
            yield return h.CaptureLooks();
        }

        // Walking onto a pickup picks it up, which shows its notification.
        private static IEnumerator PickupNotification(Harness h)
        {
            string item = Harness.ItemOfTier(ItemTier.Tier3) ?? Harness.ItemOfTier(ItemTier.Tier1);
            ItemIndex index = ItemCatalog.FindItemIndex(item);
            Inventory inventory = Harness.LocalBody.inventory;
            int before = inventory.GetItemCountPermanent(index);
            GenericPickupController pickup = h.SpawnPickup(item);
            yield return h.Wait(PickupWaitSeconds);
            h.Teleport(pickup.transform.position);
            yield return h.Until(() => inventory.GetItemCountPermanent(index) > before, $"the player picking up {item}", PickupTimeout);
            yield return h.Wait(SettleSeconds);
            yield return h.CaptureLooks();
        }

        private static IEnumerator AllyCards(Harness h)
        {
            h.Cmd($"{Commands.GiveMoney} {Money}");
            for (int i = 0; i < 2; i++)
            {
                PurchaseInteraction drone = null;
                yield return h.Spawned<PurchaseInteraction>($"{Commands.SpawnInteractable} {GameNames.BrokenDrone}",
                    found => drone = found);
                yield return h.Interact(drone.gameObject);
            }
            yield return h.Wait(AllyCardSeconds);
            yield return h.AimAway();
            yield return h.CaptureLooks();
        }

        // Hits from small to tens of thousands, some critical, so the numbers show digits, K and the crit mark.
        private static IEnumerator DamageNumbers(Harness h)
        {
            CharacterBody monster = null;
            yield return h.Spawned<CharacterBody>($"{Commands.SpawnAi} {GameNames.ToughMonster} 1", found => monster = found);
            // A new body gets its inventory from its master a moment after it appears.
            yield return h.Until(() => monster && monster.inventory, "the Beetle Queen's inventory");
            // She digs up out of the ground for her first seconds, with her middle below it, so no spot can see her.
            EntityStateMachine body = EntityStateMachine.FindByCustomName(monster.gameObject, BodyStateMachine);
            yield return h.Until(() => !monster || !body || body.IsInMainState(), "the Beetle Queen out of the ground",
                SpawnTimeout);
            // Server methods; the runner is the host.
            monster.inventory.GiveItemPermanent(RoR2Content.Items.BoostHp, BoostHpStacks);
            Vector3 spot = OpenSpot(Harness.LocalBody.footPosition);
            yield return HitAndShoot(h, monster, spot, "sevenui");
            h.SetConfig(SevenUIPlugin.Settings.MonospaceDamageNumbers, false);
            yield return HitAndShoot(h, monster, spot, "vanilla");
        }

        // The monster appears wherever the game finds room, which can leave no clear view of it, and wanders between
        // the shots. So it is put on open ground before each shot (OpenSpot), and the player stands before it from
        // there.
        private static IEnumerator HitAndShoot(Harness h, CharacterBody monster, Vector3 spot, string look)
        {
            TeleportHelper.TeleportBody(monster, spot, forceOutOfVehicle: false);
            yield return h.Wait(TeleportSettleSeconds);
            yield return h.StandBefore(monster, DamageNumberStandDistance);
            Hit(monster);
            yield return h.Wait(SettleSeconds);
            yield return h.Screenshot(look);
        }

        // Somewhere the player can see the monster from its stand distance: the first of the stage's player spawn
        // points, then where the player stands, with ground and a clear line to the monster from enough of the spots
        // around it. Earlier scenarios can leave the player in a dip, where every spot around is behind terrain.
        private static Vector3 OpenSpot(Vector3 fallback)
        {
            List<Vector3> candidates = SpawnPoint.readOnlyInstancesList.Where(point => point)
                .Select(point => point.transform.position).ToList();
            candidates.Add(fallback);
            foreach (Vector3 candidate in candidates)
            {
                if (ClearViews(candidate) >= MinClearViews) return candidate;
            }
            return fallback;
        }

        // Of the directions around the spot, how many have ground at the stand distance and a clear line from eye
        // height there to the monster's middle.
        private static int ClearViews(Vector3 spot)
        {
            Vector3 middle = spot + Vector3.up * MonsterMiddleHeight;
            float distance = DamageNumberStandDistance + MonsterRadius;
            int clear = 0;
            for (int i = 0; i < ViewDirections; i++)
            {
                Vector3 around = spot + Quaternion.Euler(0f, i * 360f / ViewDirections, 0f) * Vector3.forward * distance;
                if (!Physics.Raycast(around + Vector3.up * GroundSearchHeight, Vector3.down, out RaycastHit ground,
                        GroundSearchHeight * 2f, LayerIndex.world.mask)) continue;
                Vector3 eye = ground.point + Vector3.up * EyeHeight;
                if (!Physics.Linecast(eye, middle, LayerIndex.world.mask))
                {
                    clear++;
                }
            }
            return clear;
        }

        // Server methods, as the runner is the host, which sends each hit's number to every player. A number appears
        // where the hit landed, so the hits land in a row on the monster's near side: one at its middle would be
        // hidden inside it.
        private static void Hit(CharacterBody monster)
        {
            if (!monster || !monster.healthComponent) return;
            Vector3 eye = Harness.LocalBody.inputBank.aimOrigin;
            Vector3 toward = Vector3.ProjectOnPlane(eye - monster.corePosition, Vector3.up).normalized;
            Vector3 across = Vector3.Cross(Vector3.up, toward);
            Vector3 front = monster.corePosition + toward * (monster.radius + 1f);
            for (int i = 0; i < DamageHits.Length; i++)
            {
                monster.healthComponent.TakeDamage(new DamageInfo
                {
                    attacker = Harness.LocalBody.gameObject,
                    damage = Mathf.Min(DamageHits[i], monster.healthComponent.combinedHealth * 0.9f),
                    crit = i % 2 == 1,
                    position = front + across * ((i - (DamageHits.Length - 1) / 2f) * DamageNumberSpread),
                    procCoefficient = 0f,
                    damageColorIndex = DamageColorIndex.Default,
                });
            }
        }

        // The longest Eclipse name with Karma on, in the run info.
        private static IEnumerator EclipseAndKarma(Harness h)
        {
            RunSetUp.SetDifficulty(h, DifficultyIndex.Eclipse8);
            RunSetUp.SetKarma(h, ShownKarma);
            yield return h.ExpectDifficultyAndKarmaShown(ShownKarma);
            yield return h.CaptureLooks();
        }

        private static IEnumerator BossBar(Harness h)
        {
            TeleporterInteraction teleporter = TeleporterInteraction.instance;
            if (!teleporter) throw h.Skip("this stage has no teleporter");
            yield return h.Interact(teleporter.gameObject);
            HUDBossHealthBarController boss = null;
            yield return h.Until(
                () =>
                {
                    boss = Object.FindObjectOfType<HUDBossHealthBarController>();
                    return boss && boss.container && boss.container.activeInHierarchy;
                },
                "the teleporter's boss and its health bar", BossTimeout);
            yield return h.ExpectBossRowFilled(boss);
            yield return h.ExpectChargeRow();
            yield return h.AimAway();
            // As on the canvas: most of the health left, and a hit just taken drawn after it.
            HitBoss(boss, FirstBossHit);
            yield return h.ExpectBossTrail(boss, true);
            yield return h.ExpectBossTrail(boss, false);
            HitBoss(boss, SecondBossHit);
            yield return h.ExpectBossTrail(boss, true);
            yield return h.CaptureLooks();
        }

        // Takes a share of the boss group's health off its first living member, past its armor.
        private static void HitBoss(HUDBossHealthBarController boss, float share)
        {
            BossGroup group = boss.currentBossGroup;
            CharacterMaster master = group && group.combatSquad
                ? group.combatSquad.readOnlyMembersList.FirstOrDefault(member => member && member.GetBody())
                : null;
            CharacterBody body = master ? master.GetBody() : null;
            if (!body) return;
            body.healthComponent.TakeDamage(new DamageInfo
            {
                attacker = Harness.LocalBody.gameObject,
                damage = group.totalMaxObservedMaxHealth * share,
                damageType = DamageType.BypassArmor,
                position = body.corePosition,
                procCoefficient = 0f,
                damageColorIndex = DamageColorIndex.Default,
            });
        }
    }
}
