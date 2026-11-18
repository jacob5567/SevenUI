using System.Collections;
using ModTestRunner;
using RoR2;
using UnityEngine.Networking;

namespace SevenUI.Tests
{
    // Scraps a drone as a player does at a Drone Scrapper: the scrapper's own BeginScrapping, which the picker panel calls
    // on the host once the player picks the drone. It destroys the drone's body and master in the same frame, with no
    // death, so the drone's ally card goes and the game hands the cards below it on. Server methods; the runner is the host.
    internal static class DroneScrapping
    {
        private const string DroneScrapper = "iscDroneScrapper";

        // A scrapper of its own for each drone, so none is still busy with the last; destroyed at cleanup.
        public static IEnumerator Scrap(Harness h, CharacterMaster drone)
        {
            CharacterBody body = drone ? drone.GetBody() : null;
            if (!body) throw h.Fail("no living drone to scrap");
            DroneScrapperController scrapper = null;
            yield return h.Spawned<DroneScrapperController>($"{Commands.SpawnInteractable} {DroneScrapper}", found => scrapper = found);
            h.OnCleanup(() =>
            {
                if (scrapper)
                {
                    NetworkServer.Destroy(scrapper.gameObject);
                }
            });
            // The player who picked the drone, as the interaction would have set.
            scrapper.AssignPotentialInteractor(Harness.LocalBody.GetComponent<Interactor>());
            scrapper.BeginScrapping(body.netId, DroneCatalog.GetDroneIndexFromBodyIndex(body.bodyIndex));
            h.Log($"scrapped {body.name}; its body and master go at the frame's end");
        }
    }
}
