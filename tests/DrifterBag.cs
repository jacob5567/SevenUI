using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using EntityStates.Drifter;
using ModTestRunner;
using RoR2;
using RoR2.Skills;
using RoR2.UI;
using UnityEngine;
using UnityEngine.Networking;

namespace SevenUI.Tests
{
    // Drifter's real grab: the Repossess skill is run on the host (as a press of the skill without holding it, which the
    // game takes as a release at once), it leaps at the interactable in front of her and bags it, which shows the bag
    // display. Then the contextual skill that replaces it while bagged empties the bag. Server methods; the runner is the host.
    internal static class DrifterBag
    {
        // Meters from the interactable's edge she stands, well within the skill's search range.
        private const float StandOff = 3f;
        private const float SpawnTimeout = 5f;
        private const float GrabTimeout = 6f;
        private const float EmptyTimeout = 6f;
        private const float ReadyTimeout = 8f;
        private const float CardTimeout = 5f;
        private const float SettleSeconds = 0.5f;
        private const float AfterEmptySeconds = 0.5f;
        // Her primary is a combo while the bag is full, and only its last hit slams; an interactable takes up to 20
        // slams (a Beetle Queen's hull), so this is room for that many combos.
        private const int MaxPresses = 60;
        private const float SwingTimeout = 6f;
        private const float BarTimeout = 2f;
        private const float CardGoneTimeout = 5f;
        // At this durability or less the game shows its low-health marks on a carried interactable's bar
        // (HealthBar: altSource.durability <= 2).
        public const int LowDurability = 2;
        // HealthBar keeps its bars in a private struct; the low-health marks are two of them.
        private const string BarInfoField = "barInfoCollection";
        private const string LowHealthUnder = "lowHealthUnderBarInfo";
        private const string LowHealthOver = "lowHealthOverBarInfo";

        // What gets grabbed, one at a time: kinds that differ in name and portrait.
        public static readonly string[] Kinds =
        {
            GameNames.SmallChest, GameNames.Barrel, GameNames.ShrineOfChance, GameNames.BrokenDrone, GameNames.LunarPod
        };

        // The skill that bags things, from the loadout, or from the skill's family when the loadout holds another
        // variant (put back at cleanup).
        public static GenericSkill FindRepossess(Harness h, CharacterBody body)
        {
            GenericSkill[] skills = body.skillLocator.allSkills;
            GenericSkill found = skills.FirstOrDefault(skill => IsRepossess(skill.skillDef));
            if (found) return found;

            foreach (GenericSkill skill in skills)
            {
                SkillDef variant = skill.skillFamily?.variants.Select(entry => entry.skillDef).FirstOrDefault(IsRepossess);
                if (!variant) continue;

                object source = typeof(DrifterBag);
                skill.SetSkillOverride(source, variant, GenericSkill.SkillOverridePriority.Replacement);
                h.OnCleanup(() =>
                {
                    if (skill)
                    {
                        skill.UnsetSkillOverride(source, variant, GenericSkill.SkillOverridePriority.Replacement);
                    }
                });
                h.Log($"Repossess is not in the loadout; using the variant {variant.skillName} of {skill.skillName}");
                return skill;
            }
            string have = string.Join(", ", skills.Select(skill =>
                $"{skill.skillName}: {skill.skillDef?.skillName} ({skill.skillDef?.activationState.stateType?.Name})"));
            throw h.Fail($"none of the Drifter's skills or their variants runs {nameof(AimRepossess)}; she has {have}");
        }

        private static bool IsRepossess(SkillDef def) => def && def.activationState.stateType == typeof(AimRepossess);

        public static BaggedCardController FindCard(Harness h) =>
            SevenUIChecks.HudRoot(h).GetComponentInChildren<BaggedCardController>(true);

        // Spawns the interactable the way the console does and finds what it adds that the bag takes. Null when none
        // appears, so the caller can skip it.
        public static IEnumerator Spawn(Harness h, string card, Action<SpecialObjectAttributes> found)
        {
            HashSet<SpecialObjectAttributes> before = new HashSet<SpecialObjectAttributes>(
                UnityEngine.Object.FindObjectsOfType<SpecialObjectAttributes>());
            h.Cmd($"{Commands.SpawnInteractable} {card}");
            float end = Time.realtimeSinceStartup + SpawnTimeout;
            while (Time.realtimeSinceStartup < end)
            {
                SpecialObjectAttributes added = UnityEngine.Object.FindObjectsOfType<SpecialObjectAttributes>()
                    .FirstOrDefault(attributes => !before.Contains(attributes));
                if (added)
                {
                    found(added);
                    yield break;
                }
                yield return null;
            }
            found(null);
        }

        public static void Discard(SpecialObjectAttributes attributes)
        {
            if (!attributes) return;

            GameObject root = attributes.transform.root.gameObject;
            if (root.GetComponent<NetworkIdentity>())
            {
                NetworkServer.Destroy(root);
            }
            else
            {
                UnityEngine.Object.Destroy(root);
            }
        }

        // Stands before the object, grabs it with the skill and waits for the bag display to name it (name, when given,
        // is what the display should show instead of the object's bestName, as for a body it names the body). Reports null for that,
        // or the reason when the game doesn't let her grab it (it dodges, is out of reach, or something else gets bagged).
        public static IEnumerator Grab(Harness h, GenericSkill repossess, DrifterBagController bag, SpecialObjectAttributes target,
            Action<string> problem, string name = null)
        {
            string expected = name ?? target.bestName;
            yield return h.StandBefore(target.transform.root.gameObject, StandOff);
            yield return h.AimAt(target.indicatorOffset ? target.indicatorOffset.position : target.transform.position);
            float end = Time.realtimeSinceStartup + ReadyTimeout;
            while (!(repossess.CanExecute() && !bag.bagFull) && Time.realtimeSinceStartup < end)
            {
                yield return null;
            }
            if (!repossess.ExecuteIfReady())
            {
                throw h.Fail($"the Repossess skill is not ready ({repossess.stock} stock, bag full: {bag.bagFull}) {h.Diagnosis()}");
            }

            end = Time.realtimeSinceStartup + GrabTimeout;
            while (!bag.bagFull && Time.realtimeSinceStartup < end)
            {
                yield return null;
            }
            if (!bag.bagFull)
            {
                problem("the skill bagged nothing");
                yield break;
            }
            if (bag.baggedAttributes != target)
            {
                problem($"the skill bagged {bag.baggedObject.name} instead");
                yield break;
            }

            BaggedCardController card = null;
            end = Time.realtimeSinceStartup + CardTimeout;
            while (Time.realtimeSinceStartup < end)
            {
                card = FindCard(h);
                if (card && !string.IsNullOrEmpty(expected) && card.nameLabel.text == expected) break;
                yield return null;
            }
            if (!card || card.nameLabel.text != expected)
            {
                throw h.Fail($"the bag display shows \"{(card ? card.nameLabel.text : "no card")}\", not \"{expected}\" " +
                             $"{h.Diagnosis()}");
            }
            yield return h.Wait(SettleSeconds);
            problem(null);
        }

        // Empties the bag with the skill that replaces Repossess while it is full, and waits for it to be empty. If the throw
        // doesn't empty it in time, the bag is reset so the next grab starts clean.
        public static IEnumerator Empty(Harness h, GenericSkill repossess, DrifterBagController bag)
        {
            if (!bag.bagFull) yield break;

            if (repossess.activationState.stateType != typeof(EmptyBag))
            {
                throw h.Fail($"with the bag full the skill runs {repossess.activationState.stateType?.Name}, not {nameof(EmptyBag)} " +
                             $"{h.Diagnosis()}");
            }
            float end = Time.realtimeSinceStartup + ReadyTimeout;
            while (!repossess.CanExecute() && Time.realtimeSinceStartup < end)
            {
                yield return null;
            }
            repossess.ExecuteIfReady();
            end = Time.realtimeSinceStartup + EmptyTimeout;
            while (bag.bagFull && Time.realtimeSinceStartup < end)
            {
                yield return null;
            }
            if (bag.bagFull)
            {
                h.Log("the empty-bag skill did not empty the bag in time; resetting it");
                bag.ResetBaggedObject();
            }
            yield return h.Wait(AfterEmptySeconds);
        }
    
        // Presses her primary until reached holds: while the bag is full the game swaps it for a combo whose slam
        // (SuffocateSlam) hits what she carries, an interactable losing a point of durability a slam and breaking at
        // the slam from 1, which empties the bag, and a body taking damage. The slam only lands on the ground, so each
        // press waits for her to stand. Fails, naming the states the presses ran, if the presses run out.
        public static IEnumerator Slam(Harness h, CharacterBody body, Func<bool> reached, string what)
        {
            GenericSkill primary = body.skillLocator.primary;
            List<string> ran = new List<string>();
            for (int i = 0; i < MaxPresses && !reached(); i++)
            {
                float end = Time.realtimeSinceStartup + ReadyTimeout;
                while (!(primary.CanExecute() && body.characterMotor && body.characterMotor.isGrounded) &&
                       Time.realtimeSinceStartup < end)
                {
                    yield return null;
                }
                string state = primary.activationState.stateType?.Name;
                if (!primary.ExecuteIfReady()) throw h.Fail($"her primary ({state}) isn't ready for {what} {h.Diagnosis()}");
                if (ran.Count == 0 || ran[ran.Count - 1] != state)
                {
                    ran.Add(state);
                }
                end = Time.realtimeSinceStartup + SwingTimeout;
                yield return null;
                while (!reached() && primary.stateMachine && !primary.stateMachine.IsInMainState() &&
                       Time.realtimeSinceStartup < end)
                {
                    yield return null;
                }
            }
            if (!reached()) throw h.Fail($"{MaxPresses} presses of her primary ({string.Join(", ", ran)}) didn't reach {what} {h.Diagnosis()}");
            h.Log($"reached {what}; her primary ran {string.Join(", ", ran)}");
        }

        // The bag display's bar follows the carried interactable, or body, and shows the game's low-health marks only
        // when low is true.
        public static IEnumerator ExpectBar(Harness h, SpecialObjectAttributes target, CharacterBody targetBody, bool low,
            string when)
        {
            string Problem()
            {
                BaggedCardController card = FindCard(h);
                if (!card || !card.isActiveAndEnabled) return "no bag display shows";
                HealthBar bar = card.healthBar;
                if (!bar) return "the bag display has no health bar";
                if (targetBody)
                {
                    if (bar.source != targetBody.healthComponent) return $"the bar follows {Describe(bar)}, not {targetBody.name}";
                }
                else if (bar.altSource != target)
                {
                    return $"the bar follows {Describe(bar)}, not {target.name}";
                }
                bool? shown = LowHealthMarks(bar);
                if (shown == null) return "the bar's low-health marks can't be read (the game's HealthBar changed)";
                if (shown != low) return $"the low-health marks are {(shown.Value ? "on" : "off")}";
                return null;
            }

            yield return h.Until(() => Problem() == null, () => $"{when}: {Problem()} {h.Diagnosis()}", BarTimeout);
        }

        private static string Describe(HealthBar bar) =>
            bar.source ? bar.source.name : bar.altSource ? bar.altSource.name : "nothing";

        // Either of the game's two low-health marks (under and over the bar) is drawn; null if they can't be read.
        private static bool? LowHealthMarks(HealthBar bar)
        {
            FieldInfo collectionField = typeof(HealthBar).GetField(BarInfoField, BindingFlags.Instance | BindingFlags.NonPublic);
            object collection = collectionField?.GetValue(bar);
            FieldInfo under = collection?.GetType().GetField(LowHealthUnder);
            FieldInfo over = collection?.GetType().GetField(LowHealthOver);
            if (under == null || over == null) return null;
            return ((HealthBar.BarInfo)under.GetValue(collection)).enabled || ((HealthBar.BarInfo)over.GetValue(collection)).enabled;
        }

        // Once the bag empties, its display goes.
        public static IEnumerator ExpectCardGone(Harness h, string when)
        {
            yield return h.Until(() =>
            {
                BaggedCardController card = FindCard(h);
                return !card || !card.isActiveAndEnabled;
            }, () => $"{when}: the bag display still shows {h.Diagnosis()}", CardGoneTimeout);
        }
    }
}
