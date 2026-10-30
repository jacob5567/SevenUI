using RoR2.Quests.TrialQuests;

namespace SevenUI.Tests
{
    // On the Trial of Infernal Greed's strip: a dollar sign on a badge left of the text, and a progress bar.
    internal sealed class TestMoneyObjectiveTracker : TestObjectiveTracker
    {
        public TestMoneyObjectiveTracker()
        {
            customPrefab = TrialQuestSadismMoney.customObjectiveStripPrefab;
        }
    }
}
