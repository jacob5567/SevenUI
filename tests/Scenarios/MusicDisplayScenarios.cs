using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Bootstrap;
using ModTestRunner;
using RoR2;
using RoR2.UI;
using TMPro;
using UnityEngine;

namespace SevenUI.Tests
{
    // The MusicDisplay mod's now-playing text, which SevenUI hides with the panels on and shows as the last panel of
    // the right info bar, under the objectives. Skipped when MusicDisplay isn't installed or its text was never made.
    // The scenarios set the text themselves, so they don't wait for a track, and put it back after.
    internal static class MusicDisplayScenarios
    {
        // How long to wait for the mod to make its text.
        private const float TextTimeout = 30f;
        private const float SettleSeconds = 0.5f;
        private const float EdgeTolerance = 1f;
        // In the bar's units, which match the panel gap's.
        private const float LayoutTolerance = 0.5f;
        private const string PanelName = "SevenUINowPlayingPanel";
        private const string HeaderName = "SevenUIHeader";
        private const string NameName = "SevenUITrackName";
        private const string AuthorName = "SevenUITrackAuthor";
        private const string HeaderText = "NOW PLAYING";
        private const string TrackName = "Test Track";
        private const string TrackAuthor = "Test Author";
        private const string OtherName = "Another Tune";
        private const string OtherAuthor = "Someone Else";
        private const string ObjectiveToken = "OBJECTIVE_FIND_TELEPORTER";
        private const string OtherObjectiveToken = "OBJECTIVE_CHARGE_TELEPORTER";

        public static IEnumerable<Scenario> All()
        {
            yield return new Scenario(1, "musicdisplay_panel_layout", Stages.TitanicPlains, PanelLayout);
            yield return new Scenario(1, "musicdisplay_panel_follows_track", Stages.TitanicPlains, FollowsTrack);
            yield return new Scenario(1, "musicdisplay_text_hidden", Stages.TitanicPlains, TextHidden);
            yield return new Scenario(1, "musicdisplay_follows_flat_panels", Stages.TitanicPlains, FollowsFlatPanels);
            yield return new Scenario(1, "musicdisplay_panel_stays_last", Stages.TitanicPlains, StaysLast);
        }

        private static TMP_Text FindText()
        {
            LocalUser user = LocalUserManager.GetFirstLocalUser();
            Canvas canvas = user != null ? AchievementNotificationPanel.GetUserCanvas(user) : null;
            Transform found = canvas ? canvas.transform.Find(GameNames.MusicDisplayText) : null;
            return found ? found.GetComponent<TMP_Text>() : null;
        }

        // The mod's text once made, skipping if MusicDisplay is absent or never makes it. Its string is put back at
        // cleanup, and what it says is set by the scenario.
        private static IEnumerator WaitForText(Harness h, System.Action<TMP_Text> found)
        {
            if (!Chainloader.PluginInfos.ContainsKey(GameNames.MusicDisplayGUID)) throw h.Skip("MusicDisplay isn't installed");

            TMP_Text text = null;
            float deadline = Time.time + TextTimeout;
            while (Time.time < deadline && !text)
            {
                text = FindText();
                yield return null;
            }
            if (!text) throw h.Skip("MusicDisplay didn't make its text (its display may be off)");
            string original = text.text;
            h.OnCleanup(() =>
            {
                if (text) text.text = original;
            });
            found(text);
        }

        // The objective panel needs an objective to have a place in the bar.
        private static IEnumerator ShowObjective(Harness h, string token, System.Action<System.Action> hide)
        {
            (List<TestObjectiveSource> sources, System.Action remove) = TestObjectives.Show(h,
                new[] { (TestObjectives.Text(token), typeof(TestObjectiveTracker), 0f) });
            hide(remove);
            yield return TestObjectives.SkipStripAnimations(h, sources);
            yield return h.Wait(SettleSeconds);
        }

        private static Transform Panel(Harness h) => h.FindUnderHud(PanelName);

        private static TMP_Text Part(Transform panel, string name)
        {
            Transform part = panel ? panel.Find(name) : null;
            return part ? part.GetComponent<TMP_Text>() : null;
        }

        private static bool Shown(Transform panel) => panel && panel.gameObject.activeInHierarchy;

        private static bool Showing(Harness h, string name, string author)
        {
            Transform panel = Panel(h);
            TMP_Text nameText = Part(panel, NameName);
            TMP_Text authorText = Part(panel, AuthorName);
            if (!Shown(panel) || !nameText || !authorText) return false;
            bool authorShown = authorText.gameObject.activeSelf;
            return nameText.text == name && authorShown == (author.Length > 0) && (!authorShown || authorText.text == author);
        }

        private static string Look(Harness h)
        {
            Transform panel = Panel(h);
            if (!panel) return "no panel";
            TMP_Text nameText = Part(panel, NameName);
            TMP_Text authorText = Part(panel, AuthorName);
            return $"active={panel.gameObject.activeSelf}, name '{(nameText ? nameText.text : "none")}', " +
                   $"author '{(authorText ? authorText.text : "none")}' active={(authorText && authorText.gameObject.activeSelf)}";
        }

        // The rect's vertical and horizontal extents in the bar's own units.
        private static Rect InBar(Transform bar, RectTransform rect)
        {
            Vector3[] corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Vector3 low = bar.InverseTransformPoint(corners[0]);
            Vector3 high = bar.InverseTransformPoint(corners[2]);
            return Rect.MinMaxRect(Mathf.Min(low.x, high.x), Mathf.Min(low.y, high.y), Mathf.Max(low.x, high.x), Mathf.Max(low.y, high.y));
        }

        // Null when the panel is the bar's last child, the lowest of its panels, a panel gap under the one above and as
        // wide as the objective panel.
        private static string LayoutProblem(Harness h)
        {
            Transform panel = Panel(h);
            Transform objective = h.FindUnderHud(GameNames.ObjectivePanel);
            if (!Shown(panel)) return "the panel isn't shown";
            if (!objective || !objective.gameObject.activeInHierarchy) return "the objective panel isn't shown";
            Transform bar = panel.parent;
            if (objective.parent != bar) return $"the panel is under {bar.name}, the objective panel under {objective.parent.name}";
            if (panel.GetSiblingIndex() != bar.childCount - 1) return "the panel isn't the bar's last child";

            Rect mine = InBar(bar, (RectTransform)panel);
            Rect objectiveBox = InBar(bar, (RectTransform)objective);
            float lowestAbove = float.MaxValue;
            foreach (Transform other in bar)
            {
                if (other == panel || !other.gameObject.activeInHierarchy) continue;
                lowestAbove = Mathf.Min(lowestAbove, InBar(bar, (RectTransform)other).yMin);
            }
            if (mine.yMax > objectiveBox.yMin + LayoutTolerance) return $"the panel at {mine} isn't below the objective panel at {objectiveBox}";
            float gap = lowestAbove - mine.yMax;
            if (Mathf.Abs(gap - RightInfoBarStyler.PanelGap) > LayoutTolerance)
            {
                return $"the gap above the panel is {gap}, not {RightInfoBarStyler.PanelGap}";
            }
            if (Mathf.Abs(mine.width - objectiveBox.width) > LayoutTolerance)
            {
                return $"the panel is {mine.width} wide, the objective panel {objectiveBox.width}";
            }
            return null;
        }

        private static string ScreenProblem(Harness h)
        {
            Transform panel = Panel(h);
            Camera camera = CanvasLayoutChecks.HudCamera(SevenUIChecks.HudRoot(h));
            Rect box = CanvasLayoutChecks.OnScreen(camera, (RectTransform)panel);
            Rect screen = CanvasLayoutChecks.ScreenOf(camera);
            bool inside = box.xMin >= screen.xMin - EdgeTolerance && box.xMax <= screen.xMax + EdgeTolerance &&
                          box.yMin >= screen.yMin - EdgeTolerance && box.yMax <= screen.yMax + EdgeTolerance;
            return inside ? null : $"the panel at {box} isn't inside the screen at {screen}";
        }

        // Last in the bar, a panel gap under the objectives, as wide as they are and on the screen, with its header
        // strip, name and author.
        private static IEnumerator PanelLayout(Harness h)
        {
            TMP_Text text = null;
            yield return WaitForText(h, found => text = found);
            yield return ShowObjective(h, ObjectiveToken, remove => h.OnCleanup(remove));
            text.text = TrackName + "\n" + TrackAuthor;
            yield return h.Until(() => Showing(h, TrackName, TrackAuthor) && LayoutProblem(h) == null,
                () => $"the panel last, a gap under the objectives and as wide ({LayoutProblem(h)}; {Look(h)}) {h.Diagnosis()}");
            yield return h.Wait(SettleSeconds);
            string problem = LayoutProblem(h) ?? ScreenProblem(h);
            if (problem != null) throw h.Fail(problem);

            Transform panel = Panel(h);
            if (!panel.Find(Texts.HeaderStripName)) throw h.Fail("the panel has no header strip");
            TMP_Text header = Part(panel, HeaderName);
            if (!header || header.text != HeaderText) throw h.Fail($"the header says '{(header ? header.text : "nothing")}', not '{HeaderText}'");
            TMP_Text nameText = Part(panel, NameName);
            TMP_Text authorText = Part(panel, AuthorName);
            if (nameText.color != Palette.Text) throw h.Fail($"the name is {nameText.color}, not the text color");
            if (authorText.color != Palette.HeaderText) throw h.Fail($"the author is {authorText.color}, not the header color");
            if (nameText.enableWordWrapping || authorText.enableWordWrapping ||
                nameText.overflowMode != TextOverflowModes.Ellipsis || authorText.overflowMode != TextOverflowModes.Ellipsis)
            {
                throw h.Fail("the name and author aren't on one line with an ellipsis");
            }
            yield return h.Screenshot("musicdisplay_panel");
        }

        // The panel follows the text: another track replaces it, a name alone has no author line and a shorter panel,
        // and no track hides the panel and its room, without moving the panels above.
        private static IEnumerator FollowsTrack(Harness h)
        {
            TMP_Text text = null;
            yield return WaitForText(h, found => text = found);
            yield return ShowObjective(h, ObjectiveToken, remove => h.OnCleanup(remove));

            text.text = TrackName + "\n" + TrackAuthor;
            yield return h.Until(() => Showing(h, TrackName, TrackAuthor), () => $"the panel on the first track ({Look(h)}) {h.Diagnosis()}");
            yield return h.Wait(SettleSeconds);
            Transform panel = Panel(h);
            float tall = ((RectTransform)panel).rect.height;
            Transform objective = h.FindUnderHud(GameNames.ObjectivePanel);
            Vector3 objectiveAt = objective.position;

            text.text = OtherName + "\n" + OtherAuthor;
            yield return h.Until(() => Showing(h, OtherName, OtherAuthor), () => $"the panel on another track ({Look(h)}) {h.Diagnosis()}");

            text.text = OtherName;
            yield return h.Until(() => Showing(h, OtherName, string.Empty), () => $"a name alone and no author line ({Look(h)}) {h.Diagnosis()}");
            yield return h.Wait(SettleSeconds);
            float shorter = ((RectTransform)panel).rect.height;
            if (shorter >= tall - LayoutTolerance) throw h.Fail($"the panel is {shorter} high with a name alone, over {tall} with an author");

            text.text = string.Empty;
            yield return h.Until(() => !Shown(Panel(h)), () => $"the panel hidden without a track ({Look(h)}) {h.Diagnosis()}");
            yield return h.Wait(SettleSeconds);
            if ((objective.position - objectiveAt).sqrMagnitude > LayoutTolerance * LayoutTolerance * 0.01f)
            {
                throw h.Fail($"the objective panel moved from {objectiveAt} to {objective.position} when the panel was hidden");
            }
            text.text = TrackName + "\n" + TrackAuthor;
            yield return h.Until(() => Showing(h, TrackName, TrackAuthor) && LayoutProblem(h) == null,
                () => $"the panel back with a track ({LayoutProblem(h)}) {h.Diagnosis()}");
        }

        // With the panels on, the mod's text isn't drawn, and is where and as big as it was.
        private static IEnumerator TextHidden(Harness h)
        {
            TMP_Text text = null;
            yield return WaitForText(h, found => text = found);
            Vector3 position = text.transform.localPosition;
            Vector3 scale = text.transform.localScale;
            bool active = text.gameObject.activeSelf;
            text.text = TrackName + "\n" + TrackAuthor;
            yield return h.Until(() => Showing(h, TrackName, TrackAuthor) && !text.enabled,
                () => $"the panel shown and MusicDisplay's text not drawn ({Look(h)}, text enabled {text.enabled}) {h.Diagnosis()}");
            yield return h.Wait(SettleSeconds);
            if (text.enabled) throw h.Fail("MusicDisplay's text is drawn with FlatPanels on");
            if (text.transform.localPosition != position || text.transform.localScale != scale || text.gameObject.activeSelf != active)
            {
                throw h.Fail($"MusicDisplay's text moved, scaled or switched off: position {position} to {text.transform.localPosition}, " +
                             $"scale {scale} to {text.transform.localScale}, active {active} to {text.gameObject.activeSelf}");
            }
        }

        // Off, no panel and the mod's text is drawn again where it was; on again, the panel is back and the text hidden.
        private static IEnumerator FollowsFlatPanels(Harness h)
        {
            TMP_Text text = null;
            yield return WaitForText(h, found => text = found);
            text.text = TrackName + "\n" + TrackAuthor;
            yield return h.Until(() => Showing(h, TrackName, TrackAuthor) && !text.enabled,
                () => $"the panel, and the text hidden ({Look(h)}) {h.Diagnosis()}");
            Vector3 position = text.transform.localPosition;
            Vector3 scale = text.transform.localScale;

            h.SetConfig(SevenUIPlugin.Settings.FlatPanels, false);
            yield return h.Until(() => !Panel(h) && text && text.enabled, () => $"the panel gone and the text drawn with FlatPanels off ({Look(h)}) {h.Diagnosis()}");
            yield return h.Wait(SettleSeconds);
            if (text.transform.localPosition != position || text.transform.localScale != scale)
            {
                throw h.Fail($"MusicDisplay's text is at {text.transform.localPosition}, scale {text.transform.localScale}, not {position}, {scale}, with FlatPanels off");
            }

            h.SetConfig(SevenUIPlugin.Settings.FlatPanels, true);
            yield return h.Until(() => Showing(h, TrackName, TrackAuthor) && !text.enabled,
                () => $"the panel back, and the text hidden, with FlatPanels on again ({Look(h)}) {h.Diagnosis()}");
        }

        // Objectives come and go above it, and it stays last and below them.
        private static IEnumerator StaysLast(Harness h)
        {
            TMP_Text text = null;
            yield return WaitForText(h, found => text = found);
            text.text = TrackName + "\n" + TrackAuthor;
            System.Action removeFirst = null;
            yield return ShowObjective(h, ObjectiveToken, remove => removeFirst = remove);
            yield return h.Until(() => Showing(h, TrackName, TrackAuthor) && LayoutProblem(h) == null,
                () => $"the panel last under one objective ({LayoutProblem(h)}) {h.Diagnosis()}");

            System.Action removeSecond = null;
            yield return ShowObjective(h, OtherObjectiveToken, remove => removeSecond = remove);
            yield return h.Until(() => LayoutProblem(h) == null, () => $"the panel last under two objectives ({LayoutProblem(h)}) {h.Diagnosis()}");

            removeFirst();
            removeSecond();
            yield return h.Wait(SettleSeconds);
            yield return ShowObjective(h, ObjectiveToken, remove => h.OnCleanup(remove));
            yield return h.Until(() => LayoutProblem(h) == null, () => $"the panel last after the objectives changed ({LayoutProblem(h)}) {h.Diagnosis()}");
        }
    }
}
