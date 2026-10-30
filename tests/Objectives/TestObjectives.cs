using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ModTestRunner;
using RoR2;
using RoR2.UI;
using UnityEngine;

namespace SevenUI.Tests
{
    // Puts objectives of the tests' own on the local player's objective panel, the way the game's objective sources
    // do, and every objective text the game has for them: most objectives come from a stage's mission controller, a
    // trial or a compulsion that can't be set up on demand, but all of them are drawn on the panel's standard strip
    // with their text, except the two trials whose strips have a progress bar.
    internal static class TestObjectives
    {
        // The objective tokens the game's trackers write, from their code and the stages' objective providers.
        public static readonly string[] Tokens =
        {
            "OBJECTIVE_FIND_TELEPORTER", "OBJECTIVE_CHARGE_TELEPORTER", "OBJECTIVE_CHARGE_TELEPORTER_OOB",
            "OBJECTIVE_FINISH_TELEPORTER", "OBJECTIVE_GO_TO_TREASURE_ROOM", "OBJECTIVE_DEFEAT_BOSS",
            "OBJECTIVE_CLEAR_ARENA", "OBJECTIVE_ARENA_CHARGE_CELL", "OBJECTIVE_GOLDSHORES_ACTIVATE_BEACONS",
            "OBJECTIVE_GOLDSHORES_DEFEAT_BOSS", "OBJECTIVE_WEEKLYRUN_DESTROY_CRYSTALS", "OBJECTIVE_FRACTION_PROGRESS_FORMAT",
            "OBJECTIVE_MOON_BATTERY_MISSION", "OBJECTIVE_MOON_BATTERY_BLOOD", "OBJECTIVE_MOON_BATTERY_BLOOD_OOB",
            "OBJECTIVE_MOON_BATTERY_DESIGN", "OBJECTIVE_MOON_BATTERY_DESIGN_OOB", "OBJECTIVE_MOON_BATTERY_MASS",
            "OBJECTIVE_MOON_BATTERY_MASS_OOB", "OBJECTIVE_MOON_BATTERY_SOUL", "OBJECTIVE_MOON_BATTERY_SOUL_OOB",
            "OBJECTIVE_MOON_CHARGE_DROPSHIP", "OBJECTIVE_MOON_CHARGE_DROPSHIP_OOB", "OBJECTIVE_MOON_DROPSHIP_WAIT_FOR_ALLIES",
            "OBJECTIVE_MOON_RELEASE_DROPSHIP", "OBJECTIVE_VOID_BATTERY_MISSION", "OBJECTIVE_VOID_BATTERY",
            "OBJECTIVE_VOID_BATTERY_OOB", "OBJECTIVE_VOID_DEEP_PORTAL", "OBJECTIVE_VOID_OUTRO_PORTAL", "OBJECTIVE_VOIDCAMP",
            "OBJECTIVE_SELFDESTRUCT_ESCAPE", "OBJECTIVE_COLOSSUS_EVENT_BEGIN", "OBJECTIVE_MERIDIAN_REACH_ARENA",
            "OBJECTIVE_MERIDIAN_REACH_TEMPLE", "OBJECTIVE_REBIRTH_OUTRO_SHRINE_1", "OBJECTIVE_REBIRTH_OUTRO_SHRINE_2",
            "OBJECTIVE_PREWAKE", "OBJECTIVE_PREWAKE_INFOG", "OBJECTIVE_WAKE", "OBJECTIVE_WAKE_DEFENDTOWERS",
            "OBJECTIVE_POSTWAKE", "OBJECTIVE_POSTWAKE2", "OBJECTIVE_REAPER_HUNT", "OBJECTIVE_HOLLOWING",
            "OBJECTIVE_HOLLOWING_FULL", "OBJECTIVE_CHAMPRUSH_BEGIN", "OBJECTIVE_CHAMPRUSH_WAVE",
            "OBJECTIVE_CHAMPRUSH_WAVE_ENEMIES_LEFT", "OBJECTIVE_CHAMPRUSH_END", "OBJECTIVE_CHAMPRUSH_EXIT1",
            "OBJECTIVE_CHAMPRUSH_EXIT2", "OBJECTIVE_COMPLETE_PEDESTALS", "OBJECTIVE_EXPOSE_SHIELDTANK",
            "OBJECTIVE_DESTROY_SHIELDTANK", "OBJECTIVE_EXPOSE_SOLUSWING", "OBJECTIVE_DAMAGE_SOLUSWING",
            "OBJECTIVE_DESTROY_SOLUSWING_INV", "OBJECTIVE_CONFRONT_SOLUSWING", "OBJECTIVE_SOLUSWING_SURVIVE",
            "OBJECTIVE_SOLUSWING_ESCAPE", "OBJECTIVE_SOLUSWING_GOTOSOLUSWEB", "OBJECTIVE_SOLUSHEART_CHALLENGE",
            "OBJECTIVE_SOLUSHEART_KILL", "OBJECTIVE_SOLUSHEART_SPARE",
            // The Winnowing's trials.
            "QUEST_TRIAL_SADISMBREAKTARGETS", "QUEST_TRIAL_SADISMOPENCHESTS", "QUEST_TRIAL_SOULSPEEDRUN",
            "QUEST_TRIAL_SURVIVORMONSTERSINTORIFTS", "QUEST_TRIAL_SURVIVORMONSTERSOFFMAP", "QUEST_TRIAL_SURVIVORUMBRA",
            // Hallowed Concepts' compulsions.
            "QUEST_COMPULSION_BLINDPEST", "QUEST_COMPULSION_CLOAKEDCHEST", "QUEST_COMPULSION_DEALDAMAGE",
            "QUEST_COMPULSION_DRINKSODAS", "QUEST_COMPULSION_HEALTOTALHEALTH", "QUEST_COMPULSION_HOLDGOLD",
            "QUEST_COMPULSION_KILLEPICMONSTERS", "QUEST_COMPULSION_KILLEPICMONSTERS_MULTI",
            "QUEST_COMPULSION_LEMURIANPOLYMORPH", "QUEST_COMPULSION_LOWERENEMYHEALTH", "QUEST_COMPULSION_METEORSHOWER",
            "QUEST_COMPULSION_NOHITRUN", "QUEST_COMPULSION_ORDER", "QUEST_COMPULSION_REACHAREA", "QUEST_COMPULSION_REVIVE",
            "QUEST_COMPULSION_SCRAPORPRINTITEMS",
        };

        // The trials whose trackers draw them on strips of their own, with a bar filled to their progress.
        public static readonly (string token, Type tracker)[] ProgressTokens =
        {
            ("QUEST_TRIAL_SADISMMONEY", typeof(TestMoneyObjectiveTracker)),
            ("QUEST_TRIAL_SURVIVORPROCCITY", typeof(TestDamageObjectiveTracker)),
        };

        // For a text's counts, names and seconds.
        private static readonly object[] SampleArguments = { 3, 10, 7 };

        // The token's text with sample numbers in it; as written if it takes none, or none that fit.
        public static string Text(string token)
        {
            string text = Language.GetString(token);
            try
            {
                return string.Format(text, SampleArguments);
            }
            catch (FormatException)
            {
                return text;
            }
        }

        // The panel fades a new strip in over its first 3.5 seconds, and keeps a removed one at full height for 5 more
        // before shrinking it away. Ending those animations once the sources' strips exist shows the strips as they
        // settle, without waiting, and drops the removed ones, so the strips checked are the ones shown last.
        public static IEnumerator SkipStripAnimations(Harness h, List<TestObjectiveSource> sources)
        {
            ObjectivePanelController controller = null;
            yield return h.Until(
                () =>
                {
                    Transform panel = h.FindUnderHud(GameNames.ObjectivePanel);
                    controller = panel ? panel.GetComponent<ObjectivePanelController>() : null;
                    return controller && sources.All(source =>
                        controller.objectiveTrackers.Any(shown => shown.sourceDescriptor.source == source));
                },
                $"{sources.Count} test objectives on the objective panel");
            foreach (var animation in controller.objectiveStripAnimations)
            {
                animation.SetPercentComplete(1f);
            }
            // The panel destroys the removed strips in its next update, and the layout follows by the frame after.
            yield return null;
            yield return null;
        }

        // Shows the objectives until the returned action, or the scenario's cleanup, takes them off.
        public static (List<TestObjectiveSource> sources, Action hide) Show(Harness h,
            IEnumerable<(string text, Type tracker, float progress)> objectives)
        {
            GameObject holder = new GameObject("SevenUITestObjectives");
            List<TestObjectiveSource> sources = objectives.Select(objective =>
            {
                TestObjectiveSource source = holder.AddComponent<TestObjectiveSource>();
                source.Text = objective.text;
                source.Tracker = objective.tracker;
                source.Progress = objective.progress;
                return source;
            }).ToList();
            void Collect(CharacterMaster master, List<ObjectivePanelController.ObjectiveSourceDescriptor> output)
            {
                foreach (TestObjectiveSource source in sources)
                {
                    if (!source) continue;
                    output.Add(new ObjectivePanelController.ObjectiveSourceDescriptor
                    {
                        source = source,
                        master = master,
                        objectiveType = source.Tracker,
                    });
                }
            }
            ObjectivePanelController.collectObjectiveSources += Collect;
            bool shown = true;
            void Hide()
            {
                if (!shown) return;
                shown = false;
                ObjectivePanelController.collectObjectiveSources -= Collect;
                UnityEngine.Object.Destroy(holder);
            }
            h.OnCleanup(Hide);
            return (sources, Hide);
        }
    }
}
