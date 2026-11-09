using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ModTestRunner;
using RoR2;
using RoR2.CharacterAI;
using UnityEngine;
using UnityEngine.Networking;

namespace SevenUI.Tests
{
    // Drones for the player, so ally cards show: bought, or summoned straight in as the player's when a test needs many.
    internal static class Allies
    {
        // Enough for the drones at any difficulty or stage count the tests reach.
        private const int Money = 2000;
        // The AI masters of a Gunner Drone and a Healing Drone, as bought drones have.
        public const string GunnerDroneMaster = "Drone1Master";
        public const string HealingDroneMaster = "Drone2Master";
        // Summoned drones hover in a row behind the player, this far apart.
        private const float DroneSpacing = 2f;
        private const float DroneHeight = 3f;

        public static IEnumerator BuyDrones(Harness h, int count)
        {
            h.Cmd($"{Commands.GiveMoney} {Money}");
            for (int i = 0; i < count; i++)
            {
                PurchaseInteraction drone = null;
                yield return h.Spawned<PurchaseInteraction>($"{Commands.SpawnInteractable} {GameNames.BrokenDrone}",
                    found => drone = found);
                yield return h.Interact(drone.gameObject);
            }
        }

        // That many drones of the master's kind, the player's minions like bought ones (or the leader's, if given), their
        // AI off so they stay put. They are added to the list and destroyed at cleanup unless removed first (Remove).
        public static IEnumerator SummonDrones(Harness h, string masterName, int count, List<CharacterMaster> made,
            CharacterBody leader = null)
        {
            GameObject prefab = MasterCatalog.FindMasterPrefab(masterName);
            if (!prefab) throw h.Fail($"no {masterName} to summon");
            CharacterBody player = Harness.LocalBody;
            List<CharacterMaster> drones = new List<CharacterMaster>();
            h.OnCleanup(() => Remove(drones));
            for (int i = 0; i < count; i++)
            {
                CharacterMaster drone = new MasterSummon
                {
                    masterPrefab = prefab,
                    position = player.footPosition - player.transform.forward * (DroneSpacing * (made.Count + 1)) +
                               Vector3.up * DroneHeight,
                    rotation = player.transform.rotation,
                    summonerBodyObject = (leader ? leader : player).gameObject,
                    teamIndexOverride = TeamIndex.Player,
                    ignoreTeamMemberLimit = true,
                    preSpawnSetupCallback = StandStill,
                }.Perform();
                if (!drone) throw h.Fail($"the {masterName} wasn't summoned");
                drones.Add(drone);
                made.Add(drone);
            }
            yield return h.Until(() => drones.All(drone => drone && drone.GetBody()), $"{count} summoned {masterName} with bodies");
        }

        // Allies that each keep an ally card of their own: a Healing Drone following a Gunner Drone of its own, so no
        // two share a leader. The Gunner Drones all follow the player, so they share one card. Both are added to the list.
        public static IEnumerator SummonLoneAllies(Harness h, int count, List<CharacterMaster> made)
        {
            for (int i = 0; i < count; i++)
            {
                List<CharacterMaster> leader = new List<CharacterMaster>();
                yield return SummonDrones(h, GunnerDroneMaster, 1, leader);
                made.AddRange(leader);
                yield return SummonDrones(h, HealingDroneMaster, 1, made, leader[0].GetBody());
            }
        }

        // Destroys the drones, bodies and all.
        public static void Remove(IEnumerable<CharacterMaster> drones)
        {
            foreach (CharacterMaster drone in drones.Where(drone => drone).ToList())
            {
                drone.DestroyBody();
                NetworkServer.Destroy(drone.gameObject);
            }
        }

        private static void StandStill(CharacterMaster master)
        {
            foreach (BaseAI ai in master.GetComponents<BaseAI>())
            {
                ai.enabled = false;
            }
        }
    }
}
