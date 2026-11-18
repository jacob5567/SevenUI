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
    // The MusicDisplay mod's now-playing text, which SevenUI hides with the panels on and shows as a footer line along
    // the scoreboard's bottom, in rows and tabs. Skipped when MusicDisplay isn't installed or its text was never made.
    // The scenarios set the text themselves, so they don't wait for a track, and put it back after.
    internal static class MusicDisplayScenarios
    {
        // How long to wait for the mod to make its text.
        private const float TextTimeout = 30f;
        private const float SettleSeconds = 0.5f;
        private const float EdgeTolerance = 1f;
        private const int CrowdPlayers = 3;
        private const int CrowdItems = 100;
        private const int ItemsAlone = 10;
        private const string TrackName = "Test Track";
        private const string TrackAuthor = "Test Author";
        private const string OtherName = "Another Tune";
        private const string OtherAuthor = "Someone Else";
        private const string Separator = "·";

        public static IEnumerable<Scenario> All()
        {
            yield return new Scenario(1, "musicdisplay_footer", Stages.TitanicPlains, h => Footer(h, false));
            yield return new Scenario(1, "musicdisplay_tabs_footer", Stages.TitanicPlains, h => Footer(h, true));
            yield return new Scenario(1, "musicdisplay_footer_follows_track", Stages.TitanicPlains, h => FollowsTrack(h, false));
            yield return new Scenario(1, "musicdisplay_tabs_footer_follows_track", Stages.TitanicPlains, h => FollowsTrack(h, true));
            yield return new Scenario(1, "musicdisplay_text_hidden", Stages.TitanicPlains, TextHidden);
            yield return new Scenario(1, "musicdisplay_follows_flat_panels", Stages.TitanicPlains, FollowsFlatPanels);
        }

        // Also in the scoreboard's suite.
        public static IEnumerable<Scenario> Scoreboard() => All().Where(scenario => scenario.Name != "musicdisplay_text_hidden");

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

        private static IEnumerator OpenBoard(Harness h, bool tabs)
        {
            if (tabs)
            {
                yield return ScoreboardSetUp.AddPlayers(h, CrowdPlayers, CrowdItems);
            }
            ScoreboardSetUp.GiveDifferentItems(h, ItemsAlone);
            yield return ScoreboardSetUp.Open(h);
            yield return h.ExpectScoreboardAsCanvas(tabs);
        }

        private static ScoreboardItemsBoard ItemsBoard(Harness h) =>
            SevenUIChecks.HudRoot(h).GetComponentInChildren<ScoreboardItemsBoard>();

        private static bool FooterIs(ScoreboardItemsBoard board, string name, string author)
        {
            if (!board || !board.Footer || !board.Footer.gameObject.activeInHierarchy) return false;
            string line = board.FooterString;
            return line.Contains(name) && line.Contains(author) && line.Contains(Separator) == (author.Length > 0);
        }

        private static bool FooterGone(ScoreboardItemsBoard board) =>
            board && (!board.Footer || !board.Footer.gameObject.activeSelf) && board.FooterReserved == 0f;

        private static bool Shown(Transform part) =>
            part.gameObject.activeInHierarchy && (!part.GetComponent<CanvasGroup>() || part.GetComponent<CanvasGroup>().alpha > 0f);

        // The footer is on the scoreboard's panel, inside it and the screen, and clear of the rows, the shown items, the
        // tab and the docked pane's column.
        private static IEnumerator Footer(Harness h, bool tabs)
        {
            TMP_Text text = null;
            yield return WaitForText(h, found => text = found);
            yield return OpenBoard(h, tabs);
            ScoreboardItemsBoard board = ItemsBoard(h);
            text.text = TrackName + "\n" + TrackAuthor;
            yield return h.Until(() => FooterIs(board, TrackName, TrackAuthor),
                () => $"a footer with '{TrackName}' and '{TrackAuthor}' ('{(board && board.Footer ? board.FooterString : "none")}') {h.Diagnosis()}");
            yield return h.Wait(SettleSeconds);

            RectTransform panel = (RectTransform)board.Footer.parent;
            if (!panel.GetComponent<ScoreboardController>()) throw h.Fail($"the footer is on {panel.name}, not the scoreboard's panel");
            Camera camera = CanvasLayoutChecks.HudCamera(SevenUIChecks.HudRoot(h));
            Rect footer = CanvasLayoutChecks.OnScreen(camera, board.Footer);
            Rect panelBox = CanvasLayoutChecks.OnScreen(camera, panel);
            Rect screen = CanvasLayoutChecks.ScreenOf(camera);
            if (footer.xMin < panelBox.xMin - EdgeTolerance || footer.xMax > panelBox.xMax + EdgeTolerance ||
                footer.yMin < panelBox.yMin - EdgeTolerance || footer.yMax > panelBox.yMax + EdgeTolerance)
            {
                throw h.Fail($"the footer at {footer} isn't inside the scoreboard's panel at {panelBox}");
            }
            if (panelBox.xMin < screen.xMin - EdgeTolerance || panelBox.xMax > screen.xMax + EdgeTolerance ||
                panelBox.yMin < screen.yMin - EdgeTolerance || panelBox.yMax > screen.yMax + EdgeTolerance)
            {
                throw h.Fail($"the scoreboard's panel at {panelBox} isn't on the screen at {screen}");
            }

            List<(string, RectTransform)> others = new List<(string, RectTransform)>();
            foreach (ScoreboardItemGrid grid in board.Grids)
            {
                others.Add(("a row", (RectTransform)grid.Strip.transform));
                if (Shown(grid.Cell)) others.Add(("a row's items", grid.Cell));
            }
            Transform column = board.transform.Find(ScoreboardItemsBoard.PaneColumnName);
            if (column && column.gameObject.activeInHierarchy) others.Add(("the pane's column", (RectTransform)column));
            if (board.Pane != null && board.Pane.Shown) others.Add(("the pane", board.Pane.Root));
            if (board.Tab && board.Tab.gameObject.activeInHierarchy) others.Add(("the tab", (RectTransform)board.Tab.transform));
            foreach ((string what, RectTransform rect) in others)
            {
                Rect box = CanvasLayoutChecks.OnScreen(camera, rect);
                if (footer.Overlaps(box)) throw h.Fail($"the footer at {footer} overlaps {what} at {box}");
            }
            yield return h.Screenshot(tabs ? "musicdisplay_tabs_footer" : "musicdisplay_footer");
        }

        // The footer follows the text: another track replaces it, a name alone has no dot and author, and no track takes
        // the footer and its room away.
        private static IEnumerator FollowsTrack(Harness h, bool tabs)
        {
            TMP_Text text = null;
            yield return WaitForText(h, found => text = found);
            yield return OpenBoard(h, tabs);
            ScoreboardItemsBoard board = ItemsBoard(h);
            RectTransform strips = (RectTransform)board.transform;

            text.text = TrackName + "\n" + TrackAuthor;
            yield return h.Until(() => FooterIs(board, TrackName, TrackAuthor), () => $"the footer on the first track {h.Diagnosis()}");
            yield return h.Wait(SettleSeconds);
            float withFooter = strips.rect.height;
            float reserved = board.FooterReserved;
            if (reserved <= 0f) throw h.Fail("the footer keeps no room");

            text.text = OtherName + "\n" + OtherAuthor;
            yield return h.Until(() => FooterIs(board, OtherName, OtherAuthor) && !board.FooterString.Contains(TrackName),
                () => $"the footer on another track ('{board.FooterString}') {h.Diagnosis()}");

            text.text = OtherName;
            yield return h.Until(() => FooterIs(board, OtherName, string.Empty) && !board.FooterString.Contains(OtherAuthor),
                () => $"the footer with a name alone ('{board.FooterString}') {h.Diagnosis()}");

            text.text = string.Empty;
            yield return h.Until(() => FooterGone(board), () => $"no footer, and no room kept, without a track {h.Diagnosis()}");
            yield return h.Wait(SettleSeconds);
            float without = strips.rect.height;
            if (without > withFooter + EdgeTolerance) throw h.Fail($"the board is {without} high without the footer, over {withFooter} with it");
            if (board.FooterReserved != 0f) throw h.Fail("the board keeps room for a footer that is gone");

            text.text = TrackName + "\n" + TrackAuthor;
            yield return h.Until(() => FooterIs(board, TrackName, TrackAuthor) && board.FooterReserved > 0f,
                () => $"the footer back with a track {h.Diagnosis()}");
        }

        // With the panels on, the mod's text isn't drawn, and is where and as big as it was.
        private static IEnumerator TextHidden(Harness h)
        {
            TMP_Text text = null;
            yield return WaitForText(h, found => text = found);
            yield return h.Until(() => !text.enabled, () => $"MusicDisplay's text not drawn {h.Diagnosis()}");
            Vector3 position = text.transform.localPosition;
            Vector3 scale = text.transform.localScale;
            bool active = text.gameObject.activeSelf;
            text.text = TrackName + "\n" + TrackAuthor;
            yield return h.Wait(SettleSeconds);
            if (text.enabled) throw h.Fail("MusicDisplay's text is drawn with FlatPanels on");
            if (text.transform.localPosition != position || text.transform.localScale != scale || text.gameObject.activeSelf != active)
            {
                throw h.Fail($"MusicDisplay's text moved, scaled or switched off: position {position} to {text.transform.localPosition}, " +
                             $"scale {scale} to {text.transform.localScale}, active {active} to {text.gameObject.activeSelf}");
            }
        }

        // Off, no footer and the mod's text is drawn again where it was; on again, the footer is back and the text hidden.
        private static IEnumerator FollowsFlatPanels(Harness h)
        {
            TMP_Text text = null;
            yield return WaitForText(h, found => text = found);
            yield return OpenBoard(h, false);
            ScoreboardItemsBoard board = ItemsBoard(h);
            text.text = TrackName + "\n" + TrackAuthor;
            yield return h.Until(() => FooterIs(board, TrackName, TrackAuthor) && !text.enabled, () => $"the footer, and the text hidden {h.Diagnosis()}");
            Vector3 position = text.transform.localPosition;
            Vector3 scale = text.transform.localScale;

            h.SetConfig(SevenUIPlugin.Settings.FlatPanels, false);
            yield return h.Wait(SettleSeconds);
            if (!text) throw h.Fail("MusicDisplay's text is gone with FlatPanels off");
            if (!text.enabled) throw h.Fail("MusicDisplay's text isn't drawn with FlatPanels off");
            if (text.transform.localPosition != position || text.transform.localScale != scale)
            {
                throw h.Fail($"MusicDisplay's text is at {text.transform.localPosition}, scale {text.transform.localScale}, not {position}, {scale}, with FlatPanels off");
            }
            if (ItemsBoard(h) && ItemsBoard(h).Footer && ItemsBoard(h).Footer.gameObject.activeInHierarchy) throw h.Fail("the footer is still there with FlatPanels off");

            h.SetConfig(SevenUIPlugin.Settings.FlatPanels, true);
            yield return h.Until(() =>
                {
                    ScoreboardItemsBoard again = ItemsBoard(h);
                    return FooterIs(again, TrackName, TrackAuthor) && !text.enabled;
                },
                () => $"the footer back, and the text hidden, with FlatPanels on again {h.Diagnosis()}");
        }
    }
}
