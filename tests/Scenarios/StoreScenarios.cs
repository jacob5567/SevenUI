using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ModTestRunner;
using RoR2;
using RoR2.UI;
using UnityEngine;

namespace SevenUI.Tests
{
    // Screenshots for the store page and README: each sets up a run as a player would see it partway through, with
    // DebugToolkit's midgame or lategame preset (the run's time, stages cleared, the team's level, a spread of random
    // items and a random equipment) applied on the stage itself rather than in the Bazaar the macros end in, enemies
    // cleared and kept away so nothing crowds the shot, and then what the shot shows. Each takes SevenUI's look, and the
    // HUD shot the game's too, for the side by side. The presets change the run for good, so these run last, and only
    // when the suite or one of them is named.
    internal static class StoreScenarios
    {
        // DebugToolkit's midgame and lategame macros (Macros.Midgame and LateGame): seconds into the run, stages
        // cleared, the player team's level and how many random items, drawn from its default droptable.
        private const string FixedTime = "fixed_time";
        private const string StagesCleared = "run_set_stages_cleared";
        private const string TeamLevel = "team_set_level";
        private const string RandomItems = "random_items";
        private const string NoEnemies = "no_enemies";
        private const string PlayerTeam = "1";
        private const string DropTable = "\"Tier1:100,Tier2:60,Tier3:4\"";
        private const string RandomEquipment = "random";

        private sealed class Preset
        {
            public int Seconds;
            public int Cleared;
            public int Level;
            public int Items;
        }

        private static readonly Preset Midgame = new Preset { Seconds = 1325, Cleared = 5, Level = 15, Items = 23 };
        private static readonly Preset Lategame = new Preset { Seconds = 3420, Cleared = 8, Level = 24, Items = 75 };

        // Money enough to read as a run's, and other players with as many items as a midgame or lategame player.
        private const int Money = 1240;
        private const int OtherPlayers = 3;
        // A few drones sharing a card, and a turret beside them.
        private const int GunnerDrones = 3;
        private const int Couplers = 2;
        // Enough lone allies, each with a card of its own, that the cards go compact beside the shared one.
        private const int LoneAllies = 8;
        private const float SettleSeconds = 1.5f;

        public static IEnumerable<Scenario> All()
        {
            yield return Store("store_hud", Stages.SkyMeadow, Hud);
            yield return Store("store_scoreboard", Stages.TitanicPlains, Scoreboard);
            yield return Store("store_scoreboard_crowded", Stages.TitanicPlains, CrowdedScoreboard);
            yield return Store("store_equipment_deck", Stages.TitanicPlains, EquipmentDeck);
            yield return Store("store_allies", Stages.SkyMeadow, Allied);
        }

        private static Scenario Store(string name, string stage, System.Func<Harness, IEnumerator> run) =>
            new Scenario(1, name, stage, run, runsLast: true);

        // The whole HUD mid-run: items, an equipment, money, allies sharing a card, and the game's look beside it.
        private static IEnumerator Hud(Harness h)
        {
            yield return SetUp(h, Midgame);
            List<CharacterMaster> drones = new List<CharacterMaster>();
            yield return Allies.SummonDrones(h, Allies.GunnerDroneMaster, GunnerDrones, drones);
            yield return Allies.SummonDrones(h, Allies.HealingDroneMaster, 1, drones);
            yield return Settle(h);
            yield return h.CaptureLooks();
        }

        // Four players at midgame, as rows, with the item pointed at described in the board's panel.
        private static IEnumerator Scoreboard(Harness h)
        {
            yield return SetUp(h, Midgame);
            yield return ScoreboardSetUp.AddPlayers(h, OtherPlayers, Midgame.Items);
            yield return PointedScoreboard(h);
        }

        // Four players at lategame, too many items for rows: the board collapses to your tab.
        private static IEnumerator CrowdedScoreboard(Harness h)
        {
            yield return SetUp(h, Lategame);
            yield return ScoreboardSetUp.AddPlayers(h, OtherPlayers, Lategame.Items);
            yield return PointedScoreboard(h);
        }

        private static IEnumerator PointedScoreboard(Harness h)
        {
            yield return ScoreboardSetUp.Open(h);
            yield return Settle(h);
            ScoreboardRow row = SevenUIChecks.HudRoot(h).GetComponentsInChildren<ScoreboardRow>()
                .FirstOrDefault(each => each.Strip && each.Strip.master == Harness.LocalBody.master);
            ItemIcon icon = row ? ScoreboardScenarios.FirstIcon(row.Strip.itemInventoryDisplay.transform) : null;
            if (!icon) throw h.Fail("no item in your row of the scoreboard");
            ScoreboardScenarios.PointAt(h, icon.rectTransform, "an item in the scoreboard");
            yield return h.ExpectItemPane(icon);
            yield return h.Wait(SettleSeconds);
            yield return h.Screenshot("sevenui");
        }

        // Two Functional Couplers and an equipment in each of the three sets: the next one over the equipment slot with
        // the keys, the last fading behind it.
        private static IEnumerator EquipmentDeck(Harness h)
        {
            yield return SetUp(h, Midgame);
            Inventory inventory = Harness.LocalBody.inventory;
            inventory.GiveItemPermanent(DLC3Content.Items.ExtraEquipment, Couplers);
            yield return h.Wait(SettleSeconds);
            if (inventory.accessibleEquipmentSetCount < Couplers + 1) throw h.Skip("the Functional Coupler needs Alloyed Collective");
            EquipmentDef[] held = { RoR2Content.Equipment.Fruit, RoR2Content.Equipment.CommandMissile, RoR2Content.Equipment.Blackhole };
            EquipmentLocation active = inventory.activeEquipmentLocation;
            for (int i = 0; i < held.Length; i++)
            {
                EquipmentLocation place = active;
                place.set = (uint)((active.set + i) % inventory.accessibleEquipmentSetCount);
                inventory.SetEquipmentIndex(held[i].equipmentIndex, in place);
            }
            yield return Settle(h);
            yield return h.Screenshot("sevenui");
        }

        // A crowd of allies: Gunner Drones sharing a card, and Healing Drones each following a drone of their own, as
        // compact rows.
        private static IEnumerator Allied(Harness h)
        {
            yield return SetUp(h, Midgame);
            List<CharacterMaster> allies = new List<CharacterMaster>();
            yield return Allies.SummonDrones(h, Allies.GunnerDroneMaster, GunnerDrones, allies);
            yield return Allies.SummonLoneAllies(h, LoneAllies, allies);
            yield return Settle(h);
            yield return h.Screenshot("sevenui");
        }

        // The preset's run, items and equipment on this stage, with money, and no enemies until cleanup.
        private static IEnumerator SetUp(Harness h, Preset preset)
        {
            h.Cmd($"{FixedTime} {preset.Seconds}");
            h.Cmd($"{StagesCleared} {preset.Cleared}");
            h.Cmd($"{TeamLevel} {PlayerTeam} {preset.Level}");
            h.Cmd($"{RandomItems} {preset.Items} {DropTable}");
            h.Cmd($"{Commands.GiveEquipment} {RandomEquipment}");
            h.Cmd($"{Commands.GiveMoney} {Money}");
            h.Cmd($"{NoEnemies} 1");
            h.OnCleanup(() => h.Cmd($"{NoEnemies} 0"));
            h.Cmd(Commands.KillAll);
            yield return h.Wait(SettleSeconds);
        }

        // The HUD settled and the camera off anything the crosshair would light.
        private static IEnumerator Settle(Harness h)
        {
            yield return h.AimAway();
            yield return h.Wait(SettleSeconds);
        }
    }
}
