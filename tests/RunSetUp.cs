using System;
using ModTestRunner;
using RoR2;
using RoR2.UI;
using Object = UnityEngine.Object;

namespace SevenUI.Tests
{
    // Run settings the scenarios change as host, and put back at cleanup: the selected difficulty, chosen in the lobby,
    // and Karma, a lobby rule. The runner is the host, so it may call the server's methods.
    internal static class RunSetUp
    {
        public const int KarmaOff = -1;

        public static void SetDifficulty(Harness h, DifficultyIndex difficulty)
        {
            Run run = Run.instance;
            if (!run) throw h.Fail("no run");
            DifficultyIndex original = run.selectedDifficulty;
            h.OnCleanup(() =>
            {
                if (!run) return;
                run.selectedDifficulty = original;
            });
            run.selectedDifficulty = difficulty;
        }

        // A level from 0 to KarmaManager.maxKarma, or KarmaOff for a run without Karma.
        public static void SetKarma(Harness h, int level)
        {
            KarmaManager karma = KarmaManager.instance;
            if (!karma) throw h.Skip("the run has no Karma manager");
            bool enabled = karma.IsKarmaEnabled();
            int original = (int)karma.NetworkactualKarma;
            h.OnCleanup(() =>
            {
                if (!karma) return;
                ApplyKarma(karma, enabled, original);
            });
            ApplyKarma(karma, level != KarmaOff, Math.Max(level, 0));
        }

        private static void ApplyKarma(KarmaManager karma, bool enabled, int level)
        {
            karma.NetworkisKarmaEnabled = enabled;
            karma.SetKarmaServer(level);
            // The level counts only while Karma is on, and SetKarmaServer updates it only when the level changes.
            karma.UpdateEffectiveKarma();
            // The HUD's Karma icon shows or hides itself only as it is enabled, as when the HUD is made at the start
            // of a run with Karma on, so it is enabled again to read the new setting.
            foreach (KarmaIcon icon in Object.FindObjectsOfType<KarmaIcon>())
            {
                icon.enabled = false;
                icon.enabled = true;
            }
        }
    }
}
