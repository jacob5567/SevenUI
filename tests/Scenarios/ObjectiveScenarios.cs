using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ModTestRunner;
using RoR2;
using RoR2.UI;
using UnityEngine.UI;

namespace SevenUI.Tests
{
    // Every objective text the game has, shown on the objective panel on the strip the game draws it on, alone and a
    // batch at a time, and the Winnowing trials whose strips have a progress bar, each checked against the design
    // canvas's rows (ObjectiveChecks). Alone, a row is checked as the panel's only extra row; in a batch, next to
    // others. The objectives are the tests' own, given the game's texts, since most come from a stage's
    // mission, a trial or a compulsion that can't be set up on demand.
    internal static class ObjectiveScenarios
    {
        // Enough rows to fill the column a few times over, without the panel reaching far off the screen.
        private const int BatchSize = 8;
        private static readonly float[] Progress = { 0.4f, 0.7f };

        public static IEnumerable<Scenario> All()
        {
            yield return new Scenario(1, "each_objective_alone_as_canvas", Stages.TitanicPlains, EachObjectiveAlone);
            yield return new Scenario(1, "every_objective_as_canvas", Stages.TitanicPlains, EveryObjective);
            yield return new Scenario(1, "trial_progress_as_canvas", Stages.TitanicPlains, TrialProgress);
            yield return new Scenario(1, "objective_done_and_failed_boxes", Stages.TitanicPlains, DoneAndFailedBoxes);
        }

        private static IEnumerator EachObjectiveAlone(Harness h)
        {
            var objectives = TestObjectives.Tokens.Select(token => (TestObjectives.Text(token), typeof(TestObjectiveTracker), 0f))
                .Concat(TrialObjectives());
            foreach (var objective in objectives)
            {
                (List<TestObjectiveSource> sources, System.Action hide) = TestObjectives.Show(h, new[] { objective });
                yield return h.ExpectObjectivesAsCanvas(sources);
                hide();
            }
        }

        private static IEnumerator EveryObjective(Harness h)
        {
            string[] tokens = TestObjectives.Tokens;
            for (int start = 0; start < tokens.Length; start += BatchSize)
            {
                var batch = tokens.Skip(start).Take(BatchSize)
                    .Select(token => (TestObjectives.Text(token), typeof(TestObjectiveTracker), 0f));
                (List<TestObjectiveSource> sources, System.Action hide) = TestObjectives.Show(h, batch);
                yield return h.ExpectObjectivesAsCanvas(sources);
                hide();
            }
        }

        // An open, a done and a failed objective, the states set as the game sets them (ObjectiveTracker.SetState): each
        // box as design H draws it; with FlatPanels off the game's pictures and text colors back, and on again restyled.
        private static IEnumerator DoneAndFailedBoxes(Harness h)
        {
            string[] tokens = TestObjectives.Tokens;
            (List<TestObjectiveSource> sources, System.Action hide) = TestObjectives.Show(h, tokens.Take(3)
                .Select(token => (TestObjectives.Text(token), typeof(TestObjectiveTracker), 0f)));
            h.OnCleanup(hide);
            ObjectivePanelController controller = null;
            yield return h.Until(() => (controller = SevenUIChecks.HudRoot(h).GetComponentInChildren<ObjectivePanelController>()) &&
                                       sources.All(source => controller.objectiveTrackers.Any(shown => shown.sourceDescriptor.source == source)),
                "the test objectives on the panel");
            yield return TestObjectives.SkipStripAnimations(h, sources);
            ObjectivePanelController.ObjectiveTracker Tracker(int i) =>
                controller.objectiveTrackers.First(shown => shown.sourceDescriptor.source == sources[i]);
            Tracker(1).SetState(ObjectivePanelController.ObjectiveTracker.ObjectiveState.Completed);
            Tracker(2).SetState(ObjectivePanelController.ObjectiveTracker.ObjectiveState.Failure);
            yield return h.ExpectObjectiveBoxesFlat();
            if (SevenUIChecks.HudRoot(h).GetComponentsInChildren<ObjectiveCheckbox>().Count(box => box.Mark && box.Mark.enabled) < 2)
            {
                throw h.Fail("fewer than two boxes marked done or failed");
            }
            yield return h.Screenshot("objective_boxes");

            h.SetConfig(SevenUIPlugin.Settings.FlatPanels, false);
            h.OnCleanup(() => h.SetConfig(SevenUIPlugin.Settings.FlatPanels, true));
            yield return h.Until(() => !SevenUIChecks.HudRoot(h).GetComponentInChildren<ObjectiveCheckbox>() &&
                                       sources.Select((_, i) => Tracker(i).stripObject.GetComponent<ChildLocator>()
                                           .FindChildComponent<Image>("Checkbox")).All(image => image && image.enabled),
                "the game's checkbox pictures back with FlatPanels off");
            h.SetConfig(SevenUIPlugin.Settings.FlatPanels, true);
            yield return h.ExpectObjectiveBoxesFlat();
        }

        private static IEnumerator TrialProgress(Harness h)
        {
            (List<TestObjectiveSource> sources, System.Action _) = TestObjectives.Show(h, TrialObjectives());
            yield return h.ExpectObjectivesAsCanvas(sources);
        }

        // The trials with a progress bar, filled part way.
        public static IEnumerable<(string, System.Type, float)> TrialObjectives() =>
            TestObjectives.ProgressTokens.Select((trial, i) => (TestObjectives.Text(trial.token), trial.tracker, Progress[i % Progress.Length]));
    }
}
