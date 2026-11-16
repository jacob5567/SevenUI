using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BepInEx.Configuration;
using ModTestRunner;
using UnityEngine;

namespace SevenUI.Tests
{
    // The SevenUI scenarios, and what the runner needs to know about the mod: when they can run, when a stage is ready,
    // and what the mod saw at a failure.
    internal sealed class SevenUITests : ModTests
    {
        // Longer than the runner's settle after a mod's Cleanup, so the next scenario's Precondition gets in first.
        private const float RestoreDelaySeconds = 2f;
        private const int SettleFrames = 1;

        // Each setting and the value the scenarios need it at.
        private static readonly (Func<PluginConfig, ConfigEntry<bool>> Entry, bool Value)[] Needed =
        {
            (settings => settings.BarlowFont, true),
            (settings => settings.FlatPanels, true),
            (settings => settings.MonospaceDamageNumbers, true),
            (settings => settings.EquipmentDeck, true),
            (settings => settings.RestyleItemPickers, true),
            (settings => settings.CompactAllyCards, true),
            (settings => settings.GroupAllyCards, true),
            (settings => settings.LevelHud, true),
            (settings => settings.ScoreboardDetailPanel, true),
            (settings => settings.ScoreboardTabs, true),
            (settings => settings.RestylePauseMenu, true),
            (settings => settings.RestyleTooltips, true),
            (settings => settings.TimerHundredths, false),
            (settings => settings.ShowHealthRegen, true),
            (settings => settings.TightMargins, true),
            (settings => settings.ScoreboardBesideRunInfo, true),
        };

        private static Dictionary<int, bool> saved;
        private static int generation;
        private static int appliedFrame;

        private readonly MonoBehaviour host;

        public SevenUITests(MonoBehaviour host)
        {
            this.host = host;
        }

        public override string Name => SevenUIPlugin.PluginName;

        // Every scenario runs in a plain mod_test; a suite picks a group to run on its own with --suite.
        public override IEnumerable<Suite> Suites()
        {
            yield return new Suite("font", FontScenarios.All());
            yield return new Suite("panels", PanelScenarios.All().Concat(ScoreboardScenarios.Checks()));
            yield return new Suite("objectives", ObjectiveScenarios.All());
            yield return new Suite("scoreboard", ScoreboardScenarios.All());
            yield return new Suite("scoreboard_rows", ScoreboardScenarios.All(ScoreboardScenarios.Board.Rows));
            yield return new Suite("scoreboard_tabs", ScoreboardScenarios.All(ScoreboardScenarios.Board.Tabs));
            yield return new Suite("pickers", PickerScenarios.All());
            yield return new Suite("ally_portraits", AllyPortraitScenarios.All());
            yield return new Suite("scoreboard_leave", ScoreboardLeaveScenarios.All());
            yield return new Suite("screenshots", ScreenshotScenarios.All().Append(ScoreboardScenarios.SpreadScreenshot()));
            // Changes the run for good, so only when named.
            yield return new Suite("store", true, StoreScenarios.All());
        }

        // Sets the settings the scenarios assume, once, on the first scenario; the user's values come back a moment after
        // the last one. Returns null always: a setting never skips a scenario.
        public override string Precondition(Scenario scenario)
        {
            generation++;
            if (saved != null)
            {
                return null;
            }

            saved = new Dictionary<int, bool>();
            bool changed = false;
            for (int i = 0; i < Needed.Length; i++)
            {
                ConfigEntry<bool> entry = Needed[i].Entry(SevenUIPlugin.Settings);
                saved[i] = entry.Value;
                // Assigning fires SettingChanged, so only touch what differs.
                if (entry.Value != Needed[i].Value)
                {
                    entry.Value = Needed[i].Value;
                    changed = true;
                }
            }

            if (changed)
            {
                appliedFrame = Time.frameCount;
            }

            return null;
        }

        // The controller attaches when the HUD first gets a body, a little after the stage loads. After a setting
        // changed, the mod's own handlers get a few frames to settle the look before the first step reads it.
        public override bool Ready()
        {
            return SevenUIChecks.Controller && Time.frameCount > appliedFrame + SettleFrames;
        }

        // The mod can't tell which scenario is last, so the user's values go back after a pause, unless another scenario
        // began in the meantime (its Precondition bumps the generation).
        public override IEnumerator Cleanup(Harness h)
        {
            generation++;
            host.StartCoroutine(RestoreLater(generation));
            yield break;
        }

        private static IEnumerator RestoreLater(int expected)
        {
            yield return new WaitForSecondsRealtime(RestoreDelaySeconds);
            if (generation != expected || saved == null)
            {
                yield break;
            }

            for (int i = 0; i < Needed.Length; i++)
            {
                ConfigEntry<bool> entry = Needed[i].Entry(SevenUIPlugin.Settings);
                if (entry.Value != saved[i])
                {
                    entry.Value = saved[i];
                }
            }

            saved = null;
        }

        // The mod's dump-key lines for the HUD.
        public override string Diagnose()
        {
            SevenUIController controller = SevenUIChecks.Controller;
            if (!controller) return null;
            StringBuilder output = new StringBuilder();
            controller.Describe(output);
            return output.ToString();
        }
    }
}
