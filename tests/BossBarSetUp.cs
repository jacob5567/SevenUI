using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ModTestRunner;
using RoR2;
using RoR2.UI;
using UnityEngine;

namespace SevenUI.Tests
{
    // A boss group of the test's own on the stage: Lemurians (not champions, so a frozen one can be executed) put in a
    // BossGroup that nothing else drives, with their AI off so they stand still and the numbers hold. Server methods
    // throughout; the runner is the host.
    internal static class BossBarSetUp
    {
        private const string NoEnemies = "no_enemies";
        private const float ClearSeconds = 1f;
        private const float BarTimeout = 15f;
        // Each Personal Shield Generator adds a share of max health as shield; this many make a wide segment.
        public const int ShieldStacks = 5;

        public static IEnumerator Group(Harness h, int count, List<CharacterBody> bodies, System.Action<BossGroup> found)
        {
            // The stage's own monsters would join the bar or hurt the numbers.
            h.Cmd($"{NoEnemies} 1");
            h.OnCleanup(() => h.Cmd($"{NoEnemies} 0"));
            h.Cmd(Commands.KillAll);
            yield return h.Wait(ClearSeconds);
            yield return LensInstakills.SpawnLemurians(h, count, bodies);
            foreach (CharacterBody body in bodies)
            {
                foreach (RoR2.CharacterAI.BaseAI ai in body.master.GetComponents<RoR2.CharacterAI.BaseAI>())
                {
                    ai.enabled = false;
                }
            }

            GameObject made = new GameObject("SevenUITestBossGroup");
            CombatSquad squad = made.AddComponent<CombatSquad>();
            BossGroup group = made.AddComponent<BossGroup>();
            group.shouldCreateObjectiveEntry = false;
            h.OnCleanup(() =>
            {
                if (made)
                {
                    Object.Destroy(made);
                }
            });
            foreach (CharacterBody body in bodies)
            {
                squad.AddMember(body.master);
            }

            HUDBossHealthBarController boss = null;
            yield return h.Until(
                () =>
                {
                    boss = Bar(h);
                    return boss && boss.container && boss.container.activeInHierarchy && boss.currentBossGroup == group &&
                           group.totalMaxObservedMaxHealth > 0f && group.totalObservedHealth > 0f;
                },
                () => $"the boss bar showing the test's boss group (group seen by the HUD: {(boss ? boss.currentBossGroup : null)}) " +
                      h.Diagnosis(),
                BarTimeout);
            found(group);
        }

        public static HUDBossHealthBarController Bar(Harness h) =>
            SevenUIChecks.HudRoot(h).GetComponentsInChildren<HUDBossHealthBarController>(true).FirstOrDefault();

        // Gives a body Personal Shield Generators and fills the shield they allow once the body has recalculated.
        public static IEnumerator Shielded(Harness h, CharacterBody body, int stacks = ShieldStacks, bool voided = false)
        {
            yield return h.Until(() => body && body.inventory, "the monster's inventory");
            body.inventory.GiveItemPermanent(RoR2Content.Items.PersonalShield, stacks);
            if (voided)
            {
                body.inventory.GiveItemPermanent(DLC1Content.Items.MissileVoid);
            }
            HealthComponent health = body.healthComponent;
            yield return h.Until(
                () => health.fullShield > 0f && (!voided || health.GetHealthBarValues().hasVoidShields),
                () => $"the monster's shield maximum (shield {health.fullShield}, void shields " +
                      $"{health.GetHealthBarValues().hasVoidShields}) {h.Diagnosis()}");
            health.Networkshield = health.fullShield;
        }
    }
}
