using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ModTestRunner;
using RoR2;
using UnityEngine;

namespace SevenUI.Tests
{
    // The FlatPanels setting: the top-right panels are flat, the run info shows the mod's difficulty bar, the objective
    // panel has a header strip, the top bars take the design canvas's width, TightMargins moves the corners to the
    // canvas's margins, and turning it off brings back the game's panels, widths and margins. LookingGlass's copy of the
    // objective panel shows its stats in two columns.
    internal static class PanelScenarios
    {
        private const string Suite = "panels";
        // Plenty for a small chest at any difficulty or stage count the tests reach.
        private const int ChestMoney = 1000;
        // The game counts the shown amount up over about this long.
        private const float CountUpSeconds = 3f;
        // The health bar fits about a dozen buff slots across.
        private const int ManyBuffs = 20;
        private const int StackedBuffCount = 3;

        public static IEnumerable<Scenario> All()
        {
            yield return new Scenario(1, Suite, "hud_faces_screen", Stages.TitanicPlains, HudFacesScreen);
            yield return new Scenario(1, Suite, "tight_margins_switch", Stages.TitanicPlains, TightMarginsSwitch);
            yield return new Scenario(1, Suite, "run_info_is_flat", Stages.TitanicPlains, RunInfoIsFlat);
            yield return new Scenario(1, Suite, "eclipse_and_karma_shown", Stages.TitanicPlains, EclipseAndKarmaShown);
            yield return new Scenario(1, Suite, "objective_panel_is_flat", Stages.TitanicPlains, ObjectivePanelIsFlat);
            yield return new Scenario(1, Suite, "player_bars_are_flat", Stages.TitanicPlains, PlayerBarsAreFlat);
            yield return new Scenario(1, Suite, "buffs_wrap_in_rows", Stages.TitanicPlains, BuffsWrapInRows);
            yield return new Scenario(1, Suite, "skill_slots_are_flat", Stages.TitanicPlains, SkillSlotsAreFlat);
            yield return new Scenario(1, Suite, "equipment_stock_in_slot", Stages.TitanicPlains, EquipmentStockInSlot);
            yield return new Scenario(1, Suite, "ntormat_pages_follow_key_item", Stages.TitanicPlains, NtormatPagesFollowKeyItem);
            yield return new Scenario(1, Suite, "top_is_flat", Stages.TitanicPlains, TopIsFlat);
            yield return new Scenario(1, Suite, "item_icons_are_flat", Stages.TitanicPlains, ItemIconsAreFlat);
            yield return new Scenario(1, Suite, "prompt_is_flat", Stages.TitanicPlains, PromptIsFlat);
            yield return new Scenario(1, Suite, "prompt_as_canvas", Stages.TitanicPlains, PromptAsCanvas);
            yield return new Scenario(1, Suite, "scoreboard_and_chat_are_flat", Stages.TitanicPlains, ScoreboardAndChatAreFlat);
            yield return new Scenario(1, Suite, "turning_off_restores_panels", Stages.TitanicPlains, TurningOffRestoresPanels);
            yield return new Scenario(1, Suite, "lookingglass_stats_in_columns", Stages.TitanicPlains, LookingGlassStatsInColumns);
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

        // Each Eclipse level by name, the longest with Karma at its highest, and Karma turned off again.
        private static IEnumerator EclipseAndKarmaShown(Harness h)
        {
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

        private static IEnumerator RunInfoIsFlat(Harness h)
        {
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
            yield return h.ExpectSlotsSkillSized();
            yield return h.ExpectSkillRowAsCanvas();
            yield return h.ExpectUtilityAreaFollowsRow();
        }

        // Foreign Fruit with a Fuel Cell, so the slot shows its stock, taken away at cleanup. Server methods; the runner
        // is the host.
        private static IEnumerator EquipmentStockInSlot(Harness h)
        {
            Inventory inventory = Harness.LocalBody.inventory;
            EquipmentLocation slot = inventory.activeEquipmentLocation;
            EquipmentIndex before = inventory.currentEquipmentIndex;
            h.OnCleanup(() =>
            {
                if (!inventory) return;
                inventory.RemoveItemPermanent(RoR2Content.Items.EquipmentMagazine);
                inventory.SetEquipmentIndex(before, in slot);
            });
            inventory.SetEquipmentIndex(RoR2Content.Equipment.Fruit.equipmentIndex, in slot);
            inventory.GiveItemPermanent(RoR2Content.Items.EquipmentMagazine);
            yield return h.ExpectEquipmentStockAsCanvas();
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
        }

        private static IEnumerator ScoreboardAndChatAreFlat(Harness h)
        {
            yield return h.ExpectOverlaysFlat();
        }

        private static IEnumerator ObjectivePanelIsFlat(Harness h)
        {
            yield return h.ExpectObjectivePanelFlat();
            yield return h.ExpectObjectiveRowsAsCanvas();
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
