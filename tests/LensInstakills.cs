using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ModTestRunner;
using RoR2;
using UnityEngine;

namespace SevenUI.Tests
{
    // Lost Seer's Lenses' instakills on Lemurians: enough lenses for every hit that procs to instakill, given to the
    // player until cleanup, Lemurians to hit, and the hit. Each instakill plays the lenses' execute effect, with its
    // burst of fake crit numbers. Server methods; the runner is the host.
    internal static class LensInstakills
    {
        // The lenses instakill on a roll of half a percent a stack, times the hit's proc coefficient.
        private const int SureLenses = 200;
        private const float ProcCoefficient = 1f;
        // A hit takes at most this share of the monster's health, so the lenses kill it and not the damage.
        private const float HealthShare = 0.9f;
        // Meters from a Lemurian's middle, toward the player, to where it is hit.
        private const float HitInFront = 0.5f;

        public static void GiveLenses(Harness h)
        {
            Inventory inventory = Harness.LocalBody.inventory;
            ItemDef lenses = DLC1Content.Items.CritGlassesVoid;
            h.OnCleanup(() =>
            {
                if (inventory)
                {
                    inventory.RemoveItemPermanent(lenses, SureLenses);
                }
            });
            inventory.GiveItemPermanent(lenses, SureLenses);
            if (inventory.GetItemCountEffective(lenses) < SureLenses)
            {
                throw h.Skip("Lost Seer's Lenses have no effect without Survivors of the Void");
            }
        }

        // Lemurians with their inventories, which a new body gets from its master a moment after it appears.
        public static IEnumerator SpawnLemurians(Harness h, int count, List<CharacterBody> into)
        {
            for (int i = 0; i < count; i++)
            {
                yield return h.Spawned<CharacterBody>($"{Commands.SpawnAi} {GameNames.ExecutableMonster} 1", into.Add);
            }
            // The director's own spawns are new bodies too, and could be taken for one.
            CharacterBody other = into.FirstOrDefault(monster =>
                monster && BodyCatalog.GetBodyName(monster.bodyIndex) != GameNames.ExecutableMonsterBody);
            if (other) throw h.Fail($"spawned {other.name}, not a {GameNames.ExecutableMonsterBody}");
            yield return h.Until(() => into.All(monster => !monster || monster.inventory), "the Lemurians' inventories");
            CharacterBody immune = into.FirstOrDefault(monster =>
                monster && (monster.bodyFlags & CharacterBody.BodyFlags.ImmuneToExecutes) != 0);
            if (immune) throw h.Skip($"{immune.name} can't be instakilled");
        }

        // A hit that procs, so the lenses roll, on the monster's side toward the player.
        public static void Hit(CharacterBody monster, float damage, bool crit)
        {
            if (!monster || !monster.healthComponent) return;
            Vector3 eye = Harness.LocalBody.inputBank.aimOrigin;
            Vector3 toward = Vector3.ProjectOnPlane(eye - monster.corePosition, Vector3.up).normalized;
            monster.healthComponent.TakeDamage(new DamageInfo
            {
                attacker = Harness.LocalBody.gameObject,
                damage = Mathf.Min(damage, monster.healthComponent.combinedHealth * HealthShare),
                crit = crit,
                position = monster.corePosition + toward * (monster.radius + HitInFront),
                procCoefficient = ProcCoefficient,
                damageColorIndex = DamageColorIndex.Default,
            });
        }
    }
}
