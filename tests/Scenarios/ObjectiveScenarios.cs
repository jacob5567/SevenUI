using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ModTestRunner;

namespace SevenUI.Tests
{
    // Every objective text the game has, shown on the objective panel on the strip the game draws it on, alone and a
    // batch at a time, and the Winnowing trials whose strips have a progress bar, each checked against the design
    // canvas's rows (ObjectiveChecks). Alone, a row is checked as the panel's only extra row; in a batch, next to
    // others. The objectives are the tests' own, given the game's texts, since most come from a stage's
    // mission, a trial or a compulsion that can't be set up on demand.
    internal static class ObjectiveScenarios
    {
        private const string Suite = "objectives";
        // Enough rows to fill the column a few times over, without the panel reaching far off the screen.
        private const int BatchSize = 8;
        private static readonly float[] Progress = { 0.4f, 0.7f };

        public static IEnumerable<Scenario> All()
        {
            yield return new Scenario(1, Suite, "each_objective_alone_as_canvas", Stages.TitanicPlains, EachObjectiveAlone);
            yield return new Scenario(1, Suite, "every_objective_as_canvas", Stages.TitanicPlains, EveryObjective);
            yield return new Scenario(1, Suite, "trial_progress_as_canvas", Stages.TitanicPlains, TrialProgress);
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
