using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ModTestRunner;
using RoR2;
using RoR2.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SevenUI.Tests
{
    // The FlatPanels setting: the top-right panels are flat, the run info shows the mod's difficulty bar, the objective
    // panel has a header strip, the top bars take the design canvas's width, TightMargins moves the corners to the
    // canvas's margins, and turning it off brings back the game's panels, widths and margins. LookingGlass's copy of the
    // objective panel shows its stats in two columns.
    internal static class PanelScenarios
    {
        // Plenty for a small chest at any difficulty or stage count the tests reach.
        private const int ChestMoney = 1000;
        // The game counts the shown amount up over about this long.
        private const float CountUpSeconds = 3f;
        // Long enough for the pause menu to pan and fade in before its screenshot.
        private const float PauseSettleSeconds = 0.6f;
        private const float PauseTolerance = 1f;
        // Long enough for the chat's input to fade in before its screenshot.
        private const float ChatSettleSeconds = 0.3f;
        // How long a pushed notification can wait behind others in the queue, and how long it takes to fade in.
        private const float NotificationShownTimeout = 30f;
        private const float NotificationFadeInSeconds = 0.5f;
        // The Trial of Infernal Greed's texts.
        private const string TrialNameToken = "QUEST_TRIAL_SADISMMONEY_NAME";
        private const string TrialDescriptionToken = "QUEST_TRIAL_SADISMMONEY_DESC";
        private const float TrialNotificationSeconds = 2f;
        // The health bar fits about a dozen buff slots across.
        private const int ManyBuffs = 20;
        private const int StackedBuffCount = 3;
        private const int AllyCount = 2;
        // One, so its card keeps the game's health bar: allies of one kind share a card with a bar split per ally.
        private const int ProtectionAllyCount = 1;
        // Long enough for the Functional Coupler to add its equipment set.
        private const float BadgeSettleSeconds = 0.5f;
        // In the design canvas's pixels on its 1080-pixel-high screen.
        private const float BadgeTolerance = 2f;
        private const float BadgeCanvasHeight = 1080f;
        // Healing Drones among the last allies, so the more row has two kinds to show.
        private const int OverflowHealingDrones = 2;
        // Barrier decays, so plenty for both looks' checks.
        private const float ProtectionBarrierShare = 0.8f;

        public static IEnumerable<Scenario> All()
        {
            yield return new Scenario(1, "hud_faces_screen", Stages.TitanicPlains, HudFacesScreen);
            yield return new Scenario(1, "tight_margins_switch", Stages.TitanicPlains, TightMarginsSwitch);
            yield return new Scenario(1, "run_info_is_flat", Stages.TitanicPlains, RunInfoIsFlat);
            yield return new Scenario(1, "eclipse_and_karma_shown", Stages.TitanicPlains, EclipseAndKarmaShown);
            yield return new Scenario(1, "artifacts_in_run_info", Stages.TitanicPlains, ArtifactsInRunInfo);
            yield return new Scenario(1, "objective_panel_is_flat", Stages.TitanicPlains, ObjectivePanelIsFlat);
            yield return new Scenario(1, "player_bars_are_flat", Stages.TitanicPlains, PlayerBarsAreFlat);
            yield return new Scenario(1, "buffs_wrap_in_rows", Stages.TitanicPlains, BuffsWrapInRows);
            yield return new Scenario(1, "skill_slots_are_flat", Stages.TitanicPlains, SkillSlotsAreFlat);
            yield return new Scenario(1, "notification_icons_in_slot", Stages.TitanicPlains, NotificationIconsInSlot);
            yield return new Scenario(1, "transformation_notification_blend", Stages.TitanicPlains, TransformationNotificationBlend);
            yield return new Scenario(1, "void_transformation_notification_blend", Stages.TitanicPlains,
                VoidTransformationNotificationBlend);
            yield return new Scenario(1, "stocks_in_slot_corners", Stages.TitanicPlains, StocksInSlotCorners);
            yield return new Scenario(1, "next_equipment_badge_swaps", Stages.TitanicPlains, NextEquipmentBadgeSwaps);
            yield return new Scenario(1, "ntormat_pages_follow_key_item", Stages.TitanicPlains, NtormatPagesFollowKeyItem);
            yield return new Scenario(1, "top_is_flat", Stages.TitanicPlains, TopIsFlat);
            yield return new Scenario(1, "item_icons_are_flat", Stages.TitanicPlains, ItemIconsAreFlat);
            yield return new Scenario(1, "prompt_is_flat", Stages.TitanicPlains, PromptIsFlat);
            yield return new Scenario(1, "prompt_as_canvas", Stages.TitanicPlains, PromptAsCanvas);
            yield return new Scenario(1, "prompt_strip_health", Stages.TitanicPlains, PromptStripHealth);
            yield return new Scenario(1, "prompt_strip_equipment", Stages.TitanicPlains, PromptStripEquipment);
            yield return new Scenario(1, "prompt_strip_free", Stages.TitanicPlains, PromptStripFree);
            yield return new Scenario(1, "prompt_lunar_cost", Stages.TitanicPlains, PromptLunarCost);
            yield return new Scenario(1, "lunar_coin_flat", Stages.TitanicPlains, LunarCoinFlat);
            yield return new Scenario(1, "scoreboard_and_chat_are_flat", Stages.TitanicPlains, ScoreboardAndChatAreFlat);
            yield return new Scenario(1, "chat_input_outlined", Stages.TitanicPlains, ChatInputOutlined);
            yield return new Scenario(1, "pause_menu_as_canvas", Stages.TitanicPlains, PauseMenuAsCanvas);
            yield return new Scenario(1, "hud_settles", Stages.TitanicPlains, HudSettles);
            yield return new Scenario(1, "protection_striped_on_player", Stages.TitanicPlains, ProtectionStripedOnPlayer);
            yield return new Scenario(1, "protection_tinted_with_infusion", Stages.TitanicPlains, ProtectionTintedWithInfusion);
            yield return new Scenario(1, "protection_striped_on_allies", Stages.TitanicPlains, ProtectionStripedOnAllies);
            yield return new Scenario(1, "ally_cards_keep_above_chat", Stages.TitanicPlains, AllyCardsKeepAboveChat);
            yield return new Scenario(1, "turning_off_restores_panels", Stages.TitanicPlains, TurningOffRestoresPanels);
            yield return new Scenario(1, "lookingglass_stats_in_columns", Stages.TitanicPlains, LookingGlassStatsInColumns);
        }

        private static IEnumerator HudFacesScreen(Harness h)
        {
            yield return h.ExpectHudLevel();
        }

        // Both ways; the top bars keep the canvas's width either way.
        private static IEnumerator TightMarginsSwitch(Harness h)
        {
            h.SetConfig(SevenUIPlugin.Settings.TightMargins, true);
            yield return h.ExpectHudAtCanvasMargins();
            yield return h.ExpectTopBarsAtCanvasWidth();
            h.SetConfig(SevenUIPlugin.Settings.TightMargins, false);
            yield return h.ExpectHudAtGameMargins();
            yield return h.ExpectTopBarsAtCanvasWidth();
        }

        // Each Eclipse level by name, the longest with Karma at its highest, and Karma turned off again. A run starts
        // with Karma on when the lobby's starting Karma rule is set, so it is turned off first.
        private static IEnumerator EclipseAndKarmaShown(Harness h)
        {
            RunSetUp.SetKarma(h, RunSetUp.KarmaOff);
            for (DifficultyIndex eclipse = DifficultyIndex.Eclipse1; eclipse <= DifficultyIndex.Eclipse8; eclipse++)
            {
                RunSetUp.SetDifficulty(h, eclipse);
                yield return h.ExpectDifficultyAndKarmaShown(RunSetUp.KarmaOff);
            }
            RunSetUp.SetKarma(h, KarmaManager.maxKarma);
            yield return h.ExpectDifficultyAndKarmaShown(KarmaManager.maxKarma);
            RunSetUp.SetKarma(h, 1);
            yield return h.ExpectDifficultyAndKarmaShown(1);
            RunSetUp.SetKarma(h, RunSetUp.KarmaOff);
            yield return h.ExpectDifficultyAndKarmaShown(RunSetUp.KarmaOff);
        }

        // Artifacts whose effects wait for a stage, a spawn or a death, so turning them on mid-stage leaves the test's
        // player alone (unlike Glass's health or Death's shared fate). Read when a scenario runs, after the content
        // has loaded.
        private static ArtifactDef[] FewArtifacts() => new[]
        {
            RoR2Content.Artifacts.Command, RoR2Content.Artifacts.Sacrifice, RoR2Content.Artifacts.Swarms,
        };

        // More than the seven that fit at full size, so the icons shrink.
        private static ArtifactDef[] ManyArtifacts() => FewArtifacts().Concat(new[]
        {
            RoR2Content.Artifacts.EliteOnly, RoR2Content.Artifacts.Bomb, RoR2Content.Artifacts.WeakAssKnees,
            RoR2Content.Artifacts.WispOnDeath, RoR2Content.Artifacts.SingleMonsterType, RoR2Content.Artifacts.MixEnemy,
            RoR2Content.Artifacts.FriendlyFire, RoR2Content.Artifacts.RandomSurvivorOnRespawn,
        }).ToArray();

        // The artifacts in a row of the run info, under Karma's while it shows, shrinking to stay on one line, and
        // gone with the last artifact.
        private static IEnumerator ArtifactsInRunInfo(Harness h)
        {
            RunSetUp.SetKarma(h, RunSetUp.KarmaOff);
            RunSetUp.SetArtifacts(h);
            yield return h.ExpectArtifactsShown(0);
            RunSetUp.SetArtifacts(h, FewArtifacts());
            yield return h.ExpectArtifactsShown(FewArtifacts().Length);
            yield return h.Screenshot("few");
            RunSetUp.SetKarma(h, 2);
            yield return h.ExpectDifficultyAndKarmaShown(2);
            yield return h.ExpectArtifactsShown(FewArtifacts().Length);
            yield return h.Screenshot("with_karma");
            RunSetUp.SetArtifacts(h, ManyArtifacts());
            yield return h.ExpectArtifactsShown(ManyArtifacts().Length);
            yield return h.Screenshot("many");
            RunSetUp.SetArtifacts(h);
            yield return h.ExpectArtifactsShown(0);
        }

        // The canvas's run info has no Karma or artifacts row; a run started with either lobby rule set would show one.
        private static IEnumerator RunInfoIsFlat(Harness h)
        {
            RunSetUp.SetKarma(h, RunSetUp.KarmaOff);
            RunSetUp.SetArtifacts(h);
            yield return h.ExpectRunInfoFlat();
            yield return h.ExpectRunInfoAsCanvas();
            yield return h.ExpectRunInfoCanvasHeight();
        }

        // More max health changes both numbers, so the text has to keep up after the switch from sprite digits.
        private static IEnumerator PlayerBarsAreFlat(Harness h)
        {
            yield return h.ExpectPlayerBarsFlat();
            yield return h.ExpectPlayerBarsAsCanvas();
            h.Cmd($"{Commands.GiveItem} {GameNames.Knurl} 1");
            yield return h.Wait(0.5f);
            yield return h.ExpectPlayerBarsFlat();
        }

        // More buffs than fit on one row of the health bar's width, then the game's look and back.
        private static IEnumerator BuffsWrapInRows(Harness h)
        {
            GiveBuffs(h, ManyBuffs);
            GiveStacks(h, StackedBuffCount);
            yield return h.ExpectBuffsInRows(2);
            yield return h.ExpectBuffCountsInCorner();
            h.SetConfig(SevenUIPlugin.Settings.FlatPanels, false);
            yield return h.ExpectBuffsInGameRow();
            h.SetConfig(SevenUIPlugin.Settings.FlatPanels, true);
            yield return h.ExpectBuffsInRows(2);
        }

        // Distinct shown buffs that aren't elite auras, each taken off again at cleanup. The runner is the host.
        private static void GiveBuffs(Harness h, int count)
        {
            CharacterBody body = Harness.LocalBody;
            foreach (BuffIndex buff in BuffCatalog.nonHiddenBuffIndices
                         .Where(index => BuffCatalog.GetBuffDef(index) is BuffDef def && def.iconSprite && !def.isElite &&
                                         !body.HasBuff(index))
                         .Take(count))
            {
                body.AddBuff(buff);
                h.OnCleanup(() =>
                {
                    if (!body) return;
                    body.RemoveBuff(buff);
                });
            }
        }

        // Stacks of one stacking buff the player doesn't have, counted as stacks, taken off again at cleanup.
        private static void GiveStacks(Harness h, int stacks)
        {
            CharacterBody body = Harness.LocalBody;
            BuffIndex buff = BuffCatalog.nonHiddenBuffIndices.Where(index =>
                    BuffCatalog.GetBuffDef(index) is BuffDef def && def.iconSprite && !def.isElite && def.canStack &&
                    def.stackingDisplayMethod != BuffDef.StackingDisplayMethod.Percentage && def.displayOffset == 0 &&
                    !body.HasBuff(index))
                .DefaultIfEmpty(BuffIndex.None).First();
            if (buff == BuffIndex.None) throw h.Skip("no stacking buff to give");
            for (int i = 0; i < stacks; i++)
            {
                body.AddBuff(buff);
            }
            h.OnCleanup(() =>
            {
                if (!body) return;
                body.SetBuffCount(buff, 0);
            });
        }

        private static IEnumerator SkillSlotsAreFlat(Harness h)
        {
            yield return h.ExpectSkillSlotsFlat();
            yield return h.ExpectExtraKeysPlain();
            yield return h.ExpectExtraIconsFlat();
            yield return h.ExpectTutorialPopupsFlat();
            yield return h.ExpectSlotsSkillSized();
            yield return h.ExpectSkillRowAsCanvas();
            yield return h.ExpectUtilityAreaFollowsRow();
        }

        // A Winnowing trial's notification and a repaired drone's, which the game draws from prefabs of their own with
        // the icon in a frame; the trial's strip in the lime of the trial's icon, the drone's in its kind's color. Pushed
        // straight to the local player's queue, which shows them in turn; the trial's for a short while, so the drone's
        // follows soon.
        private static IEnumerator NotificationIconsInSlot(Harness h)
        {
            CharacterMaster master = Harness.LocalBody.master;
            CharacterMasterNotificationQueue.PushTrialNotification(master, new TrialVariantEventRelay.TrialVariantNotificationInfo
            {
                nameToken = TrialNameToken,
                descriptionToken = TrialDescriptionToken,
            }, TrialNotificationSeconds);
            yield return h.ExpectNotificationAsCanvas(Palette.Trial, TrialNameToken);
            DroneIndex drone = DroneCatalog.GetDroneIndexFromBodyIndex(BodyCatalog.FindBodyIndex(GameNames.GunnerDroneBody));
            if (drone == DroneIndex.None) throw h.Skip("no drone for the Gunner Drone's body");
            CharacterMasterNotificationQueue.PushDroneNotification(master, drone);
            DroneDef droneDef = DroneCatalog.GetDroneDef(drone);
            yield return h.ExpectNotificationAsCanvas(GameColors.ToCanvas(ColorCatalog.GetColor(droneDef.color)), droneDef.nameToken);
        }

        // AtG Missile Mk. 1 turning into Plasma Shrimp, pushed straight to the local player's queue: green to void. Once
        // as the generic notification, whose old icon sits in the icon area's row, and once as the corruption's, whose
        // old icon sits in a void frame of its own. The screenshot is taken before the check, so a failure has one.
        private static IEnumerator TransformationNotificationBlend(Harness h)
        {
            yield return TransformationBlend(h, CharacterMasterNotificationQueue.TransformationType.Default, "transformation");
        }

        private static IEnumerator VoidTransformationNotificationBlend(Harness h)
        {
            yield return TransformationBlend(h, CharacterMasterNotificationQueue.TransformationType.ContagiousVoid,
                "void_transformation");
        }

        private static IEnumerator TransformationBlend(Harness h, CharacterMasterNotificationQueue.TransformationType type,
            string screenshot)
        {
            ItemDef missile = ItemCatalog.GetItemDef(ItemCatalog.FindItemIndex(GameNames.AtgMissileItem));
            ItemDef shrimp = ItemCatalog.GetItemDef(ItemCatalog.FindItemIndex(GameNames.PlasmaShrimpItem));
            if (!missile || !shrimp) throw h.Skip("no AtG Missile Mk. 1 or Plasma Shrimp");
            CharacterMasterNotificationQueue.PushItemTransformNotification(Harness.LocalBody.master, missile.itemIndex,
                shrimp.itemIndex, type);
            yield return h.Until(() => SevenUIChecks.HudRoot(h).GetComponentsInChildren<GenericNotification>()
                    .Any(shown => shown.titleText && shown.titleText.token == shrimp.nameToken),
                $"the {shrimp.nameToken} transformation showing", NotificationShownTimeout);
            yield return h.Wait(NotificationFadeInSeconds);
            yield return h.Screenshot(screenshot);
            yield return h.ExpectTransformationAsCanvas(shrimp.nameToken, Palette.CountUncommon, Palette.CountVoid);
        }

        // Foreign Fruit with a Fuel Cell, so the slot shows its stock, and a Backup Magazine, so the secondary skill
        // shows its own, taken away at cleanup. Server methods; the runner is the host.
        // With a Functional Coupler and an equipment in each set, the next one shows as a badge centered on the
        // equipment slot's top-left corner, on the screen, copying the game's own (hidden) alternate slot. Swapping
        // plays the trade and leaves the badge on the equipment that was in use; with the panels off the game's slot
        // shows again and the badge is gone.
        private static IEnumerator NextEquipmentBadgeSwaps(Harness h)
        {
            Inventory inventory = Harness.LocalBody.inventory;
            ItemDef coupler = DLC3Content.Items.ExtraEquipment;
            h.OnCleanup(() =>
            {
                if (inventory && inventory.GetItemCountPermanent(coupler) > 0)
                {
                    inventory.RemoveItemPermanent(coupler);
                }
            });
            inventory.GiveItemPermanent(coupler);
            yield return h.Wait(BadgeSettleSeconds);
            if (inventory.accessibleEquipmentSetCount < 2) throw h.Skip("the Functional Coupler needs Alloyed Collective");
            EquipmentLocation active = inventory.activeEquipmentLocation;
            EquipmentLocation other = active;
            other.set = (active.set + 1) % inventory.accessibleEquipmentSetCount;
            EquipmentIndex activeBefore = inventory.GetEquipment(in active).equipmentIndex;
            EquipmentIndex otherBefore = inventory.GetEquipment(in other).equipmentIndex;
            h.OnCleanup(() =>
            {
                if (!inventory) return;
                inventory.SetEquipmentIndex(activeBefore, in active);
                inventory.SetEquipmentIndex(otherBefore, in other);
            });
            inventory.SetEquipmentIndex(RoR2Content.Equipment.Fruit.equipmentIndex, in active);
            inventory.SetEquipmentIndex(RoR2Content.Equipment.Blackhole.equipmentIndex, in other);

            HUD hud = SevenUIChecks.HudRoot(h).GetComponent<HUD>();
            EquipmentIcon main = hud.equipmentIcons.FirstOrDefault(icon => icon && !icon.displayAlternateEquipment);
            EquipmentIcon alternate = hud.equipmentIcons.FirstOrDefault(icon => icon && icon.displayAlternateEquipment);
            if (!main || !alternate) throw h.Fail("the HUD has no alternate equipment slot");
            yield return h.Until(() => BadgeProblem(h, main, alternate) == null, () => $"{BadgeProblem(h, main, alternate)} {h.Diagnosis()}");
            yield return h.Screenshot("badge");

            Texture inUse = main.iconImage.texture;
            inventory.DispatchSwitchToNextEquipmentInSet();
            yield return h.Until(() => h.FindUnderHud(EquipmentBadge.SwapName) is Transform swap && swap.gameObject.activeSelf,
                "the swap playing");
            yield return h.Wait(EquipmentBadge.SwapSeconds / 2f);
            yield return h.Screenshot("swapping");
            yield return h.Until(() => BadgeProblem(h, main, alternate) == null && BadgeIcon(h).texture == inUse &&
                                       !h.FindUnderHud(EquipmentBadge.SwapName).gameObject.activeSelf,
                () => $"after the swap, {BadgeProblem(h, main, alternate) ?? "the badge doesn't show the equipment swapped out"} " +
                      h.Diagnosis());

            h.SetConfig(SevenUIPlugin.Settings.FlatPanels, false);
            yield return h.Until(() => !h.FindUnderHud(EquipmentBadge.Name) && !alternate.GetComponent<CanvasGroup>(),
                () => $"with the panels off, the badge is still there or the game's slot hidden {h.Diagnosis()}");
            h.SetConfig(SevenUIPlugin.Settings.FlatPanels, true);
            yield return h.Until(() => BadgeProblem(h, main, alternate) == null,
                () => $"with the panels back on, {BadgeProblem(h, main, alternate)} {h.Diagnosis()}");
        }

        private static RawImage BadgeIcon(Harness h)
        {
            Transform badge = h.FindUnderHud(EquipmentBadge.Name);
            return badge ? badge.GetComponentInChildren<RawImage>(true) : null;
        }

        // Null when the badge shows the game's alternate slot's icon, centered on the main slot's top-left corner,
        // all of it on the screen.
        private static string BadgeProblem(Harness h, EquipmentIcon main, EquipmentIcon alternate)
        {
            Transform badge = h.FindUnderHud(EquipmentBadge.Name);
            if (!badge || !badge.gameObject.activeInHierarchy) return "no badge shows";
            RawImage icon = BadgeIcon(h);
            if (!alternate.iconImage.texture) return "the game's alternate slot has no icon yet";
            if (!icon || icon.texture != alternate.iconImage.texture) return "the badge's icon isn't the alternate slot's";
            Camera camera = CanvasLayoutChecks.HudCamera(SevenUIChecks.HudRoot(h));
            Rect screen = CanvasLayoutChecks.ScreenOf(camera);
            Rect box = CanvasLayoutChecks.OnScreen(camera, (RectTransform)badge);
            Rect slot = CanvasLayoutChecks.OnScreen(camera, (RectTransform)main.displayRoot.transform);
            Vector2 corner = new Vector2(slot.xMin, slot.yMax);
            if (Vector2.Distance(box.center, corner) > BadgeTolerance * screen.height / BadgeCanvasHeight)
            {
                return $"the badge's middle is at {box.center}, not the slot's top-left corner {corner}";
            }
            if (!screen.Contains(box.min) || !screen.Contains(box.max)) return $"the badge {box} runs off the screen {screen}";
            return null;
        }

        private static IEnumerator StocksInSlotCorners(Harness h)
        {
            Inventory inventory = Harness.LocalBody.inventory;
            EquipmentLocation slot = inventory.activeEquipmentLocation;
            EquipmentIndex before = inventory.currentEquipmentIndex;
            h.OnCleanup(() =>
            {
                if (!inventory) return;
                inventory.RemoveItemPermanent(RoR2Content.Items.EquipmentMagazine);
                inventory.RemoveItemPermanent(RoR2Content.Items.SecondarySkillMagazine);
                inventory.SetEquipmentIndex(before, in slot);
            });
            inventory.SetEquipmentIndex(RoR2Content.Equipment.Fruit.equipmentIndex, in slot);
            inventory.GiveItemPermanent(RoR2Content.Items.EquipmentMagazine);
            inventory.GiveItemPermanent(RoR2Content.Items.SecondarySkillMagazine);
            yield return h.ExpectStocksAsCanvas();
        }

        private static IEnumerator NtormatPagesFollowKeyItem(Harness h)
        {
            yield return h.GiveNtormat();
            yield return h.ExpectKeyItemDividedAsCanvas();
            yield return h.ExpectNtormatPagesFollowSlot();
        }

        // Money changes the amount's width, so its panel has to be refitted.
        private static IEnumerator TopIsFlat(Harness h)
        {
            yield return h.ExpectTopFlat();
            yield return h.ExpectTopBarsAtCanvasWidth();
            yield return h.ExpectBossBarAsCanvas();
            yield return h.ExpectCurrenciesInRow();
            h.Cmd($"{Commands.GiveMoney} {ChestMoney}");
            yield return h.Wait(CountUpSeconds);
            yield return h.ExpectCurrenciesInRow();
        }

        // The lunar coins' sign alone, so a profile without coins skips only this.
        private static IEnumerator LunarCoinFlat(Harness h)
        {
            yield return h.ExpectLunarCoinFlat();
        }

        // A stack and a single item, then more of the single item, so a count appears on an icon already restyled.
        private static IEnumerator ItemIconsAreFlat(Harness h)
        {
            h.Cmd($"{Commands.GiveItem} {GameNames.Syringe} 3");
            h.Cmd($"{Commands.GiveItem} {GameNames.Hoof} 1");
            yield return h.Wait(0.5f);
            yield return h.ExpectItemIconsFlat();
            h.Cmd($"{Commands.GiveItem} {GameNames.Hoof} 1");
            yield return h.Wait(0.5f);
            yield return h.ExpectItemIconsFlat();
        }

        private static IEnumerator PromptIsFlat(Harness h)
        {
            yield return h.ExpectPromptFlat();
        }

        // The game shows no action for a chest the player can't afford, only Inspect.
        private static IEnumerator PromptAsCanvas(Harness h)
        {
            h.Cmd($"{Commands.GiveMoney} {ChestMoney}");
            PurchaseInteraction chest = null;
            yield return h.Spawned<PurchaseInteraction>($"{Commands.SpawnInteractable} {GameNames.SmallChest}", found => chest = found);
            yield return h.StandBefore(chest.gameObject);
            yield return h.ExpectPromptAsCanvas();
            yield return h.ExpectPromptKeys();
            yield return h.ExpectPromptStrip(Palette.Money, "money");
        }

        // The Shrine of Blood costs health, in the health's red.
        private static IEnumerator PromptStripHealth(Harness h)
        {
            PurchaseInteraction shrine = null;
            yield return h.Spawned<PurchaseInteraction>($"{Commands.SpawnInteractable} {GameNames.ShrineOfBlood}", found => shrine = found);
            yield return h.StandBefore(shrine.gameObject);
            yield return h.ExpectPromptStrip(Palette.Debuff, "health");
        }

        // An equipment on the ground is named in plain text ("Get Foreign Fruit"); the strip takes its tier's orange.
        // The player holds one first: walking into an equipment picks it up while the equipment slot is empty
        // (GenericPickupController.OnTriggerStay), and the prompt only shows for one left on the ground.
        private static IEnumerator PromptStripEquipment(Harness h)
        {
            h.Cmd($"{Commands.GiveEquipment} {GameNames.Fruit}");
            yield return h.Until(() => Harness.LocalBody.inventory.EquipmentSetFull(), "the player holding an equipment");
            GenericPickupController pickup = h.SpawnPickup(GameNames.Fruit);
            yield return h.StandBefore(pickup.gameObject);
            yield return h.ExpectPromptStrip(Palette.Equipment, "equipment");
            yield return h.ExpectPromptKeys();
        }

        // A Lunar Pod's cost: the flat coin before the number, in the coins' blue, as is the strip. Lunar coins are kept
        // in the player's profile, so the test uses the profile's own and skips without one rather than adding any.
        private static IEnumerator PromptLunarCost(Harness h)
        {
            NetworkUser user = NetworkUser.readOnlyLocalPlayersList.FirstOrDefault();
            if (!user || user.lunarCoins < 1) throw h.Skip("the profile has no lunar coin, which the Lunar Pod's prompt needs");
            PurchaseInteraction pod = null;
            yield return h.Spawned<PurchaseInteraction>($"{Commands.SpawnInteractable} {GameNames.LunarPod}", found => pod = found);
            yield return h.StandBefore(pod.gameObject);
            yield return h.ExpectPromptStrip(Palette.LunarCoin, "lunar coins");
            yield return h.ExpectPromptCoin();
        }

        // Activating the teleporter costs nothing, so the strip is the accent.
        private static IEnumerator PromptStripFree(Harness h)
        {
            TeleporterInteraction teleporter = TeleporterInteraction.instance;
            if (!teleporter) throw h.Skip("no teleporter on this stage");
            yield return h.StandBefore(teleporter.gameObject);
            yield return h.ExpectPromptStrip(Palette.Accent, "a free action");
        }

        // At full health one-shot protection covers a tenth of the player's bar: in green stripes under the barrier, and
        // in the game's own with the mod's look off, as the game draws it.
        private static IEnumerator ProtectionStripedOnPlayer(Harness h)
        {
            HUD hud = SevenUIChecks.HudRoot(h).GetComponent<HUD>();
            // A barrier, drawn over the stripes. A server method; the runner is the host.
            HealthComponent health = Harness.LocalBody.healthComponent;
            health.AddBarrier(health.fullCombinedHealth * ProtectionBarrierShare);
            yield return ProtectionBothLooks(h, () => new[] { hud.healthBar }, "the player's");
        }

        // Infusion turns the game's health red; the stripes follow it rather than staying green.
        private static IEnumerator ProtectionTintedWithInfusion(Harness h)
        {
            HUD hud = SevenUIChecks.HudRoot(h).GetComponent<HUD>();
            h.Cmd($"{Commands.GiveItem} {GameNames.Infusion} 1");
            yield return h.ExpectProtectionTinted(hud.healthBar, "the player's");
            yield return h.Screenshot("infusion");
        }

        private static IEnumerator ProtectionStripedOnAllies(Harness h)
        {
            yield return Allies.BuyDrones(h, ProtectionAllyCount);
            yield return ProtectionBothLooks(h,
                () => SevenUIChecks.HudRoot(h).GetComponentsInChildren<AllyCardController>().Select(card => card.healthBar),
                "the ally cards'");
        }

        // The ally cards keep above the chat: at the game's size while they fit, as compact rows once the next card would
        // reach it, and once even the rows would, the rows that fit and a "+N more" row for the rest. With the panels
        // off every card shows at the game's size; dropping allies brings the cards back to it too. Allies left by
        // earlier scenarios count, so the drones summoned are however many each step needs.
        private static IEnumerator AllyCardsKeepAboveChat(Harness h)
        {
            // Counted before any drone is summoned: a new ally's card comes a frame or more after its body.
            int before = AllyCardChecks.Cards(h).Count;
            List<CharacterMaster> drones = new List<CharacterMaster>();
            int Total() => before + drones.Count;
            yield return Allies.SummonDrones(h, Allies.GunnerDroneMaster, 1, drones);
            yield return h.Until(() => AllyCardChecks.Cards(h).Count == Total(), () => $"{Total()} ally cards {h.Diagnosis()}");
            float full = AllyCardChecks.FullHeight(h);
            if (full <= AllyCompactCard.RowHeight) throw h.Fail($"the first ally card is {full:0.#} units tall, already compact");
            int fullFit = AllyCardChecks.Fitting(h, full);
            bool roomy = Total() <= fullFit;
            if (roomy)
            {
                yield return Allies.SummonDrones(h, Allies.GunnerDroneMaster, fullFit - Total(), drones);
                yield return h.Until(() => AllyCardChecks.AllShown(h, full, Total()) == null,
                    () => $"{AllyCardChecks.AllShown(h, full, Total())} {h.Diagnosis()}");
            }

            yield return Allies.SummonDrones(h, Allies.GunnerDroneMaster, 1, drones);
            float row = AllyCompactCard.RowHeight;
            yield return h.Until(() => AllyCardChecks.AllShown(h, row, Total()) == null,
                () => $"{AllyCardChecks.AllShown(h, row, Total())} {h.Diagnosis()}");
            yield return h.Screenshot("compact");

            int gunners = AllyCardChecks.Fitting(h, row) + 2 - OverflowHealingDrones - Total();
            yield return Allies.SummonDrones(h, Allies.GunnerDroneMaster, Mathf.Max(0, gunners), drones);
            yield return Allies.SummonDrones(h, Allies.HealingDroneMaster, OverflowHealingDrones, drones);
            yield return h.Until(() => AllyCardChecks.Overflowing(h, Total()) == null,
                () => $"{AllyCardChecks.Overflowing(h, Total())} {h.Diagnosis()}");
            yield return h.Screenshot("more");

            h.SetConfig(SevenUIPlugin.Settings.FlatPanels, false);
            yield return h.Until(() => AllyCardChecks.GameLook(h, Total(), full) == null,
                () => $"with the panels off, {AllyCardChecks.GameLook(h, Total(), full)} {h.Diagnosis()}");
            h.SetConfig(SevenUIPlugin.Settings.FlatPanels, true);
            yield return h.Until(() => AllyCardChecks.Overflowing(h, Total()) == null,
                () => $"with the panels back on, {AllyCardChecks.Overflowing(h, Total())} {h.Diagnosis()}");

            if (!roomy) yield break;
            List<CharacterMaster> dropped = drones.Skip(fullFit - before).ToList();
            Allies.Remove(dropped);
            drones.RemoveAll(dropped.Contains);
            yield return h.Until(() => AllyCardChecks.AllShown(h, full, Total()) == null,
                () => $"with the drones dropped, {AllyCardChecks.AllShown(h, full, Total())} {h.Diagnosis()}");
        }

        private static IEnumerator ProtectionBothLooks(Harness h, System.Func<IEnumerable<HealthBar>> bars, string whose)
        {
            yield return h.ExpectProtectionStriped(bars, whose, true);
            h.SetConfig(SevenUIPlugin.Settings.FlatPanels, false);
            yield return h.ExpectProtectionStriped(bars, whose, false);
        }

        private static IEnumerator ScoreboardAndChatAreFlat(Harness h)
        {
            yield return h.ExpectOverlaysFlat();
        }

        private static IEnumerator HudSettles(Harness h)
        {
            yield return h.ExpectHudSettles();
        }

        // The chat's input, opened as the chat key does: outlined with the accent strip while typing, and the outline
        // and strip gone with the field once it closes.
        private static IEnumerator ChatInputOutlined(Harness h)
        {
            SevenUIController controller = SevenUIChecks.Controller;
            ChatBox chat = controller ? controller.GetComponentInChildren<ChatBox>(true) : null;
            if (!chat) throw h.Fail($"the HUD has no chat box {h.Diagnosis()}");
            chat.SetShowInput(true);
            h.OnCleanup(() =>
            {
                if (chat)
                {
                    chat.SetShowInput(false);
                }
            });
            yield return h.ExpectChatInputOutlined(true);
            yield return h.Wait(ChatSettleSeconds);
            yield return h.Screenshot("typing");
            chat.SetShowInput(false);
            yield return h.ExpectChatInputOutlined(false);
        }

        // The pause menu, opened as Escape does: the title and the run's line over one centered stack of outlined
        // buttons, the quits side by side under it; and gone again once it closes.
        private static IEnumerator PauseMenuAsCanvas(Harness h)
        {
            PauseManager.TogglePauseScreen();
            h.OnCleanup(() =>
            {
                if (PauseManager.PauseScreenIsOpen())
                {
                    PauseManager.ClosePauseScreen(true);
                }
            });
            string difficulty = RunInfoText.Name(DifficultyCatalog.GetDifficultyDef(Run.instance.selectedDifficulty));
            yield return h.Until(() => PauseMenuLook(difficulty) == null, () => $"{PauseMenuLook(difficulty)} {h.Diagnosis()}");
            yield return h.Wait(PauseSettleSeconds);
            yield return h.Screenshot("paused");
            PauseManager.ClosePauseScreen(true);
            yield return h.Until(() => PauseScreenController.instancesList.Count == 0, () => $"the pause menu is still open {h.Diagnosis()}");
        }

        // Null once the menu is laid out as the design's, otherwise what isn't.
        private static string PauseMenuLook(string difficulty)
        {
            PauseScreenController menu = PauseScreenController.instancesList.Count > 0 ? PauseScreenController.instancesList[0] : null;
            Transform options = menu && menu.mainPanel ? menu.mainPanel.Find(PauseScreenStyler.OptionsName) : null;
            if (!options) return "no pause menu is open";
            TMP_Text title = options.Find(PauseScreenStyler.TitleName)?.GetComponent<TMP_Text>();
            TMP_Text run = options.Find(PauseScreenStyler.RunLineName)?.GetComponent<TMP_Text>();
            if (!title || string.IsNullOrEmpty(title.text)) return "the pause menu has no title";
            if (!run || !run.text.Contains(difficulty)) return $"the pause menu's run line is '{(run ? run.text : "")}', without '{difficulty}'";
            List<RectTransform> stacked = new List<RectTransform>();
            HashSet<float> widths = new HashSet<float>();
            List<RectTransform> quits = new List<RectTransform>();
            foreach (Transform child in options)
            {
                HGButton button = child.GetComponent<HGButton>();
                if (!button || !child.gameObject.activeSelf) continue;
                Image face = button.image;
                if (!face || face.sprite || !Near(face.color, Palette.Panel) && !Near(face.color, Palette.HoverFace))
                    return $"{child.name} keeps the game's face";
                if (!child.Find("SevenUIPauseOutline")) return $"{child.name} has no outline";
                foreach (PixelOutline outline in child.GetComponentsInChildren<PixelOutline>().Where(outline => outline.enabled))
                {
                    string uneven = CanvasLayoutChecks.PixelOutlineProblem(outline);
                    if (uneven != null) return $"{child.name}'s outline: {uneven}";
                    widths.Add(outline.DrawnWidth);
                }
                LanguageTextMeshController language = child.GetComponent<LanguageTextMeshController>();
                bool quit = language && System.Array.IndexOf(PauseScreenStyler.QuitTokens, language.token) >= 0;
                (quit ? quits : stacked).Add((RectTransform)child);
            }
            if (stacked.Count == 0) return "the pause menu shows no buttons";
            if (widths.Count > 1) return $"the buttons' outlines are {string.Join(", ", widths)} pixels wide, not all one width";
            for (int i = 0; i < stacked.Count; i++)
            {
                RectTransform rect = stacked[i];
                if (Mathf.Abs(rect.rect.width - PauseScreenStyler.ButtonWidth) > PauseTolerance) return $"{rect.name} is {rect.rect.width:0} wide";
                if (Mathf.Abs(rect.anchoredPosition.x) > PauseTolerance) return $"{rect.name} isn't centered";
                if (i > 0 && rect.anchoredPosition.y >= stacked[i - 1].anchoredPosition.y) return $"{rect.name} isn't under {stacked[i - 1].name}";
            }
            float lastBottom = stacked[stacked.Count - 1].anchoredPosition.y - stacked[stacked.Count - 1].rect.height;
            foreach (RectTransform quit in quits)
            {
                if (Mathf.Abs(quit.anchoredPosition.y - quits[0].anchoredPosition.y) > PauseTolerance) return "the quits aren't in one row";
                if (quit.anchoredPosition.y > lastBottom) return $"{quit.name} isn't under the stack";
            }
            float titleBottom = title.rectTransform.anchoredPosition.y - title.rectTransform.rect.height;
            if (titleBottom < run.rectTransform.anchoredPosition.y - PauseTolerance) return "the run line overlaps the title";
            return null;
        }

        private static bool Near(Color a, Color b) =>
            Mathf.Abs(a.r - b.r) < 0.01f && Mathf.Abs(a.g - b.g) < 0.01f && Mathf.Abs(a.b - b.b) < 0.01f && Mathf.Abs(a.a - b.a) < 0.01f;

        private static IEnumerator ObjectivePanelIsFlat(Harness h)
        {
            yield return h.ExpectObjectivePanelFlat();
            yield return h.ExpectObjectiveRowsAsCanvas();
            yield return h.ExpectObjectiveTextCentered();
            yield return h.ExpectObjectiveIconsDropped();
            yield return h.ExpectObjectiveBoxesFlat();
        }

        private static IEnumerator TurningOffRestoresPanels(Harness h)
        {
            h.Cmd($"{Commands.GiveItem} {GameNames.Syringe} 2");
            yield return h.Wait(0.5f);
            yield return h.ExpectRunInfoFlat();
            yield return h.ExpectObjectivePanelFlat();
            yield return h.ExpectPlayerBarsFlat();
            yield return h.ExpectSkillSlotsFlat();
            yield return h.ExpectHudLevel();
            yield return h.ExpectTopFlat();
            yield return h.ExpectItemIconsFlat();
            yield return h.ExpectPromptFlat();
            yield return h.ExpectOverlaysFlat();
            yield return h.ExpectTopBarsAtCanvasWidth();
            h.SetConfig(SevenUIPlugin.Settings.FlatPanels, false);
            yield return h.ExpectRunInfoVanilla();
            yield return h.ExpectObjectivePanelVanilla();
            yield return h.ExpectPlayerBarsVanilla();
            yield return h.ExpectSkillSlotsVanilla();
            yield return h.ExpectHudTilted();
            yield return h.ExpectTopVanilla();
            yield return h.ExpectItemIconsVanilla();
            yield return h.ExpectPromptVanilla();
            yield return h.ExpectOverlaysVanilla();
            yield return h.ExpectTopBarsAtGameWidth();
            h.SetConfig(SevenUIPlugin.Settings.FlatPanels, true);
            yield return h.ExpectRunInfoFlat();
            yield return h.ExpectObjectivePanelFlat();
            yield return h.ExpectPlayerBarsFlat();
            yield return h.ExpectSkillSlotsFlat();
            yield return h.ExpectHudLevel();
            yield return h.ExpectTopFlat();
            yield return h.ExpectItemIconsFlat();
            yield return h.ExpectPromptFlat();
            yield return h.ExpectOverlaysFlat();
            yield return h.ExpectTopBarsAtCanvasWidth();
        }

        // LookingGlass copies the objective panel for its stats, header strip and all if the copy is made after the
        // restyle, so the copy must lose the strip and the header's spaced capitals.
        private static IEnumerator LookingGlassStatsInColumns(Harness h)
        {
            Transform panel = null;
            yield return h.WaitForLookingGlassPanel(found => panel = found);
            yield return h.ExpectStatsInColumns(panel);
        }
    }
}
