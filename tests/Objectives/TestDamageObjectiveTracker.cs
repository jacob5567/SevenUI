using RoR2.Quests.TrialQuests;

namespace SevenUI.Tests
{
    // On the Trial of Causality's strip: an attack sign on a badge left of the text, and a progress bar.
    internal sealed class TestDamageObjectiveTracker : TestObjectiveTracker
    {
        public TestDamageObjectiveTracker()
        {
            customPrefab = TrialQuestSurvivorProcCity.customObjectiveStripPrefab;
        }
    }
}
