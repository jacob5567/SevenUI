using System.Collections;
using System.Collections.Generic;
using BepInEx.Bootstrap;
using ModTestRunner;
using RoR2;
using RoR2.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SevenUI.Tests
{
    // The MusicDisplay mod's now-playing text, which SevenUI gives Barlow and a flat panel behind it when the panels are
    // on. Skipped when MusicDisplay isn't installed, or its text doesn't show (it fades out after a while, and its
    // config can hide it).
    internal static class MusicDisplayScenarios
    {
        // How long to wait for the text to be made and show a track's name.
        private const float TextTimeout = 30f;
        private const float RestyleSeconds = 0.5f;
        private const float EdgeTolerance = 1f;

        public static IEnumerable<Scenario> All()
        {
            yield return new Scenario(1, "musicdisplay_text_styled", Stages.TitanicPlains, TextStyled);
            yield return new Scenario(1, "musicdisplay_panel_in_view", Stages.TitanicPlains, PanelInView);
            yield return new Scenario(1, "musicdisplay_follows_flat_panels", Stages.TitanicPlains, FollowsFlatPanels);
        }

        private static TMP_Text FindText()
        {
            LocalUser user = LocalUserManager.GetFirstLocalUser();
            Canvas canvas = user != null ? AchievementNotificationPanel.GetUserCanvas(user) : null;
            Transform found = canvas ? canvas.transform.Find(GameNames.MusicDisplayText) : null;
            return found ? found.GetComponent<TMP_Text>() : null;
        }

        private static Image Background(TMP_Text text)
        {
            Transform background = text ? text.transform.parent.Find(MusicDisplayStyler.BackgroundName) : null;
            return background ? background.GetComponent<Image>() : null;
        }

        // Waits for the text to show something, skipping if MusicDisplay is absent or never shows it.
        private static IEnumerator WaitForText(Harness h, System.Action<TMP_Text> found)
        {
            if (!Chainloader.PluginInfos.ContainsKey(GameNames.MusicDisplayGUID)) throw h.Skip("MusicDisplay isn't installed");

            TMP_Text text = null;
            float deadline = Time.time + TextTimeout;
            while (Time.time < deadline)
            {
                text = FindText();
                if (text && text.gameObject.activeInHierarchy && !string.IsNullOrWhiteSpace(text.text) && text.alpha > 0.5f) break;
                text = null;
                yield return null;
            }
            if (!text) throw h.Skip("MusicDisplay's text didn't show (its display may be off, or the track's name has faded)");
            found(text);
        }

        private static IEnumerator TextStyled(Harness h)
        {
            TMP_Text text = null;
            yield return WaitForText(h, found => text = found);
            yield return h.Until(
                () => { Image bg = Background(text); return bg && bg.enabled && !bg.sprite && bg.color.r == Palette.Panel.r && bg.color.g == Palette.Panel.g && bg.color.b == Palette.Panel.b && text.font && text.font.name.StartsWith("Barlow"); },
                () => $"MusicDisplay's text in Barlow ({(text.font ? text.font.name : "no font")}) over a flat panel ({(Background(text) ? "present" : "missing")}) {h.Diagnosis()}");
            yield return h.Screenshot("musicdisplay");
        }

        // The text lies inside its panel, and the panel is on screen and clear of the run info and the objective panel.
        private static IEnumerator PanelInView(Harness h)
        {
            TMP_Text text = null;
            yield return WaitForText(h, found => text = found);
            yield return h.Until(() => Background(text) && Background(text).enabled, () => $"a panel behind MusicDisplay's text {h.Diagnosis()}");

            Canvas canvas = text.canvas.rootCanvas;
            Camera camera = CanvasLayoutChecks.HudCamera(canvas.transform);
            Rect screen = CanvasLayoutChecks.ScreenOf(camera);
            Rect panel = CanvasLayoutChecks.OnScreen(camera, (RectTransform)Background(text).transform);
            Rect words = TextBox(camera, text);
            if (words.xMin < panel.xMin - EdgeTolerance || words.xMax > panel.xMax + EdgeTolerance ||
                words.yMin < panel.yMin - EdgeTolerance || words.yMax > panel.yMax + EdgeTolerance)
            {
                throw h.Fail($"the text at {words} isn't inside its panel at {panel}");
            }
            if (panel.xMin < screen.xMin || panel.xMax > screen.xMax || panel.yMin < screen.yMin || panel.yMax > screen.yMax)
            {
                throw h.Fail($"the panel at {panel} isn't on the screen at {screen}");
            }
            Camera hudCamera = CanvasLayoutChecks.HudCamera(SevenUIChecks.HudRoot(h));
            foreach (string name in new[] { Texts.RunInfoBackgroundName, GameNames.ObjectivePanel })
            {
                Transform other = h.FindUnderHud(name);
                if (!other || !other.gameObject.activeInHierarchy) continue;
                Rect box = CanvasLayoutChecks.OnScreen(hudCamera, (RectTransform)other);
                if (panel.Overlaps(box)) throw h.Fail($"the music panel at {panel} overlaps {name} at {box}");
            }
        }

        // The text's own box, through the canvas's camera.
        private static Rect TextBox(Camera camera, TMP_Text text)
        {
            Bounds bounds = text.textBounds;
            Vector2 min = RectTransformUtility.WorldToScreenPoint(camera, text.transform.TransformPoint(bounds.min));
            Vector2 max = RectTransformUtility.WorldToScreenPoint(camera, text.transform.TransformPoint(bounds.max));
            return Rect.MinMaxRect(Mathf.Min(min.x, max.x), Mathf.Min(min.y, max.y), Mathf.Max(min.x, max.x), Mathf.Max(min.y, max.y));
        }

        // Off, the text has the game's font and no panel; on again, it is restyled.
        private static IEnumerator FollowsFlatPanels(Harness h)
        {
            TMP_Text text = null;
            yield return WaitForText(h, found => text = found);
            yield return h.Until(() => Background(text), () => $"a panel behind MusicDisplay's text {h.Diagnosis()}");

            h.SetConfig(SevenUIPlugin.Settings.FlatPanels, false);
            yield return h.Wait(RestyleSeconds);
            if (Background(text)) throw h.Fail("the panel is still there with FlatPanels off");
            if (!text.font || !text.font.name.StartsWith(GameNames.GameFontPrefix)) throw h.Fail($"the text is in {(text.font ? text.font.name : "no font")}, not the game's font, with FlatPanels off");

            h.SetConfig(SevenUIPlugin.Settings.FlatPanels, true);
            yield return h.Until(
                () => Background(text) && text.font && text.font.name.StartsWith("Barlow"),
                () => $"MusicDisplay's text restyled with FlatPanels back on {h.Diagnosis()}");
        }
    }
}
