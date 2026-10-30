using RoR2.UI;
using UnityEngine.UI;

namespace SevenUI.Tests
{
    // A test source's objective on the game's standard strip, or on a trial's strip in a subclass, which fills the
    // strip's progress bar as the trials' trackers do. The panel makes it from its type, as it does the game's.
    internal class TestObjectiveTracker : ObjectivePanelController.ObjectiveTracker
    {
        // The trials' strips' bar, in the strip's ChildLocator.
        private const string ProgressBarName = "ProgressBar";

        private Image progressBar;

        private TestObjectiveSource Source => sourceDescriptor.source as TestObjectiveSource;

        public override string GenerateString() => Source ? Source.Text : "";

        public override void OnStripSet()
        {
            progressBar = stripObjectChildLocator.FindChildComponent<Image>(ProgressBarName);
        }

        public override void UpdateStrip()
        {
            base.UpdateStrip();
            if (progressBar && Source)
            {
                progressBar.fillAmount = Source.Progress;
            }
        }
    }
}
