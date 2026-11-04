using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ModTestRunner;
using RoR2;
using RoR2.UI;
using UnityEngine;

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
        // Long enough for the chat's input to fade in before its screenshot.
        private const float ChatSettleSeconds = 0.3f;
        // The Trial of Infernal Greed's texts.
        private const string TrialNameToken = "QUEST_TRIAL_SADISMMONEY_NAME";
        private const string TrialDescriptionToken = "QUEST_TRIAL_SADISMMONEY_DESC";
        private const float TrialNotificationSeconds = 2f;
        // The health bar fits about a dozen buff slots across.
        private const int ManyBuffs = 20;
        private const int StackedBuffCount = 3;
        private const int AllyCount = 2;
        // Barrier decays, so plenty for both looks' checks.
        private const float ProtectionBarrierShare = 0.8f;

        public static IEnumerable<Scenario> All()
        {
            yield return new Scenario(1, "hud_faces_screen", Stages.TitanicPlains, HudFacesScreen);
            yield return new Scenario(1, "tight_margins_switch", Stages.TitanicPlains, TightMarginsSwitch);
            yield return new Scenario(1, "run_info_is_flat", Stages.TitanicPlains, RunInfoIsFlat);
            yield return new Scenario(1, "eclipse_and_karma_shown", Stages.TitanicPlains, EclipseAndKarmaShown);
            yield return new Scenario(1, "objective_panel_is_flat", Stages.TitanicPlains, ObjectivePanelIsFlat);
            yield return new Scenario(1, "player_bars_are_flat", Stages.TitanicPlains, PlayerBarsAreFlat);
            yield return new Scenario(1, "buffs_wrap_in_rows", Stages.TitanicPlains, BuffsWrapInRows);
            yield return new Scenario(1, "skill_slots_are_flat", Stages.TitanicPlains, SkillSlotsAreFlat);
            yield return new Scenario(1, "notification_icons_in_slot", Stages.TitanicPlains, NotificationIconsInSlot);
            yield return new Scenario(1, "stocks_in_slot_corners", Stages.TitanicPlains, StocksInSlotCorners);
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
            yield return new Scenario(1, "hud_settles", Stages.TitanicPlains, HudSettles);
            yield return new Scenario(1, "protection_striped_on_player", Stages.TitanicPlains, ProtectionStripedOnPlayer);
            yield return new Scenario(1, "protection_tinted_with_infusion", Stages.TitanicPlains, ProtectionTintedWithInfusion);
            yield return new Scenario(1, "protection_striped_on_allies", Stages.TitanicPlains, ProtectionStripedOnAllies);
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

        // The canvas's run info has no Karma row; a run started with the lobby's Karma rule set would show one.
        private static IEnumerator RunInfoIsFlat(Harness h)
        {
            RunSetUp.SetKarma(h, RunSetUp.KarmaOff);
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
        // the icon in a frame; the trial's strip green, the drone's in its kind's color. Pushed straight to the local player's queue, which shows them in turn; the trial's for
        // a short while, so the drone's follows soon.
        private static IEnumerator NotificationIconsInSlot(Harness h)
        {
            CharacterMaster master = Harness.LocalBody.master;
            CharacterMasterNotificationQueue.PushTrialNotification(master, new TrialVariantEventRelay.TrialVariantNotificationInfo
            {
                nameToken = TrialNameToken,
                descriptionToken = TrialDescriptionToken,
            }, TrialNotificationSeconds);
            yield return h.ExpectNotificationAsCanvas(Palette.CountUncommon, TrialNameToken);
            DroneIndex drone = DroneCatalog.GetDroneIndexFromBodyIndex(BodyCatalog.FindBodyIndex(GameNames.GunnerDroneBody));
            if (drone == DroneIndex.None) throw h.Skip("no drone for the Gunner Drone's body");
            CharacterMasterNotificationQueue.PushDroneNotification(master, drone);
            DroneDef droneDef = DroneCatalog.GetDroneDef(drone);
            yield return h.ExpectNotificationAsCanvas(GameColors.ToCanvas(ColorCatalog.GetColor(droneDef.color)), droneDef.nameToken);
        }

        // Foreign Fruit with a Fuel Cell, so the slot shows its stock, and a Backup Magazine, so the secondary skill
        // shows its own, taken away at cleanup. Server methods; the runner is the host.
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
            yield return Allies.BuyDrones(h, AllyCount);
            yield return ProtectionBothLooks(h,
                () => SevenUIChecks.HudRoot(h).GetComponentsInChildren<AllyCardController>().Select(card => card.healthBar),
                "the ally cards'");
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
