using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ModTestRunner;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace SevenUI.Tests
{
    // Upgrades a drone as a player does at a Drone Combiner: the station's own interaction picks two of the player's drones
    // of one type and tier, destroys one at once (no death), seats the other, and after a few seconds gives it a
    // DroneUpgradeHidden and ejects it a tier higher. Server methods; the runner is the host.
    internal static class DroneCombining
    {
        private const string DroneCombiner = "iscDroneCombinerStation";
        // The station's delays add up to a few seconds.
        private const float CombineTimeout = 15f;
        private static readonly WaitForEndOfFrame EndOfFrame = new WaitForEndOfFrame();

        public static int Tier(CharacterMaster drone) =>
            drone && drone.inventory ? drone.inventory.GetItemCountEffective(DLC3Content.Items.DroneUpgradeHidden) : 0;

        // Combines two of the drones, calling check every frame until the upgraded one is out of the station, and takes
        // the destroyed one out of the list. A station of its own each time; destroyed at cleanup.
        public static IEnumerator Combine(Harness h, List<CharacterMaster> drones, System.Action check)
        {
            Dictionary<CharacterMaster, int> before = drones.Where(drone => drone).ToDictionary(drone => drone, Tier);
            DroneCombinerController station = null;
            yield return h.Spawned<DroneCombinerController>($"{Commands.SpawnInteractable} {DroneCombiner}", found => station = found);
            h.OnCleanup(() =>
            {
                if (station)
                {
                    NetworkServer.Destroy(station.gameObject);
                }
            });
            Interactor interactor = Harness.LocalBody.GetComponent<Interactor>();
            EntityStateMachine machine = station.GetComponent<EntityStateMachine>();
            // Its state machine starts in its first frame; an interaction before that is lost to the initial state.
            yield return h.Until(() => machine && machine.state != null && !(machine.state is EntityStates.Uninitialized),
                "the combiner's first state");
            h.Log($"combiner {station.GetInteractability(interactor)} for the player, in {machine.state.GetType().Name}");
            station.OnInteractionBegin(interactor);
            yield return null;
            h.Log($"combiner after the interaction: {machine?.state?.GetType().Name}");
            CharacterMaster upgraded = null;
            float end = Time.realtimeSinceStartup + CombineTimeout;
            while (Time.realtimeSinceStartup < end)
            {
                // As drawn: the game hands its cards on in Update and SevenUI restyles them in LateUpdate.
                yield return EndOfFrame;
                check();
                upgraded = before.Keys.FirstOrDefault(drone => drone && Tier(drone) > before[drone]);
                if (upgraded && upgraded.GetBody() && !upgraded.GetBody().currentVehicle) break;
                upgraded = null;
            }
            if (!upgraded)
            {
                throw h.Fail($"the combiner upgraded none of the {before.Count} drones (tiers {string.Join(", ", before.Values)}) " +
                             $"{h.Diagnosis()}");
            }
            int gone = before.Keys.Count(drone => !drone);
            if (gone != 1) throw h.Fail($"the combiner destroyed {gone} drones, not 1 {h.Diagnosis()}");
            drones.RemoveAll(drone => !drone);
            h.Log($"combined two tier {before[upgraded] + 1} drones into a tier {Tier(upgraded) + 1}");
        }
    }
}
