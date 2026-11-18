using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ModTestRunner;
using RoR2;
using RoR2.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SevenUI.Tests
{
    // The game's simple dialog boxes: a flat dark panel with Barlow text and outlined buttons, the accent outline on
    // the selected button, the game's look back while FlatPanels is off, and nothing left behind once a dialog closes.
    // Dialogs from the main menu can't be reached from a run, but they come from the same prefab.
    internal static class DialogScenarios
    {
        private const string HeaderToken = "QUIT_RUN_CONFIRM_DIALOG_TITLE";
        private const string ShortBodyToken = "QUIT_RUN_CONFIRM_DIALOG_BODY_SINGLEPLAYER";
        private const string LongBodyToken = "QUIT_RUN_CONFIRM_DIALOG_BODY_HOST";
        private const string CancelToken = "CANCEL";
        private const string YesToken = "DIALOG_OPTION_YES";
        private const string NoToken = "USER_PROFILE_DELETE_NO";
        private const int MostButtons = 3;
        // In the dialog canvas's units: how far text may stick out of its panel or button, and how far the panel's
        // middle may be from the canvas's.
        private const float FitTolerance = 1f;
        private const float CenterTolerance = 2f;
        private const float ColorTolerance = 0.01f;
        private const float SettleSeconds = 0.4f;

        // The game's look of one dialog, taken before the mod styled it.
        private sealed class Vanilla
        {
            public Sprite PanelSprite;
            public Color PanelColor;
            public string HeaderFont;
            public Color HeaderColor;
            public string BodyFont;
            public Color BodyColor;
            public readonly List<(Sprite Sprite, Color Color, string Font, bool Skin)> Buttons = new List<(Sprite, Color, string, bool)>();
        }

        public static IEnumerable<Scenario> All()
        {
            yield return new Scenario(1, "dialog_one_button", Stages.TitanicPlains, h => Styled(h, 1));
            yield return new Scenario(1, "dialog_two_buttons", Stages.TitanicPlains, h => Styled(h, 2));
            yield return new Scenario(1, "dialog_three_buttons", Stages.TitanicPlains, h => Styled(h, MostButtons));
            yield return new Scenario(1, "dialog_button_added_late", Stages.TitanicPlains, ButtonAddedLate);
            yield return new Scenario(1, "dialog_quit_confirmation", Stages.TitanicPlains, QuitConfirmation);
            yield return new Scenario(1, "dialog_follows_flat_panels", Stages.TitanicPlains, FollowsFlatPanels);
            yield return new Scenario(1, "dialog_opened_while_off", Stages.TitanicPlains, OpenedWhileOff);
            yield return new Scenario(1, "dialog_close_leaves_nothing", Stages.TitanicPlains, CloseLeavesNothing);
        }

        // Dialogs with one, two and three buttons: styled, and the selected button gets the accent outline.
        private static IEnumerator Styled(Harness h, int buttons)
        {
            h.SetConfig(SevenUIPlugin.Settings.DebugLogging, true);
            SimpleDialogBox box = Open(h, buttons, buttons == 1 ? LongBodyToken : ShortBodyToken);
            yield return h.Until(() => DialogLook(box, buttons, null) == null, () => $"{DialogLook(box, buttons, null)} {h.Diagnosis()}");
            yield return h.Wait(SettleSeconds);
            yield return h.Screenshot($"dialog_{buttons}");
            yield return SelectEach(h, box, buttons);
            Close(box);
            yield return h.Until(() => SimpleDialogBox.instancesList.Count == 0, () => $"the dialog is still open {h.Diagnosis()}");
        }

        // The game adds a dialog's buttons after making it, so one added later must be styled too.
        private static IEnumerator ButtonAddedLate(Harness h)
        {
            SimpleDialogBox box = Open(h, 1, ShortBodyToken);
            yield return h.Until(() => DialogLook(box, 1, null) == null, () => $"{DialogLook(box, 1, null)} {h.Diagnosis()}");
            yield return h.Wait(SettleSeconds);
            box.AddActionButton(() => { }, YesToken, true);
            yield return h.Until(() => DialogLook(box, 2, null) == null, () => $"after a second button, {DialogLook(box, 2, null)} {h.Diagnosis()}");
            box.AddCancelButton(NoToken);
            yield return h.Until(() => DialogLook(box, MostButtons, null) == null, () => $"after a third button, {DialogLook(box, MostButtons, null)} {h.Diagnosis()}");
            Close(box);
            yield return h.Until(() => SimpleDialogBox.instancesList.Count == 0, () => $"the dialog is still open {h.Diagnosis()}");
        }

        // The game's own quit confirmation, opened as the pause menu's quit buttons do; its Yes does nothing here and
        // only Cancel is pressed.
        private static IEnumerator QuitConfirmation(Harness h)
        {
            if (!Run.instance) throw h.Skip("no run");
            QuitConfirmationHelper.IssueQuitCommand(() => { });
            h.OnCleanup(DestroyAll);
            yield return h.Until(() => SimpleDialogBox.instancesList.Count == 1, () => $"the quit confirmation didn't open {h.Diagnosis()}");
            SimpleDialogBox box = SimpleDialogBox.instancesList[0];
            yield return h.Until(() => DialogLook(box, 2, null) == null, () => $"{DialogLook(box, 2, null)} {h.Diagnosis()}");
            yield return h.Wait(SettleSeconds);
            yield return h.Screenshot("dialog_quit");
            MPButton cancel = box.buttonContainer.GetComponentsInChildren<MPButton>(false).Last();
            cancel.onClick.Invoke();
            yield return h.Until(() => SimpleDialogBox.instancesList.Count == 0, () => $"Cancel didn't close the dialog {h.Diagnosis()}");
        }

        // Left open while FlatPanels goes off and on: the game's look comes back, then the mod's, on the same dialog.
        private static IEnumerator FollowsFlatPanels(Harness h)
        {
            PluginConfig settings = SevenUIPlugin.Settings;
            SimpleDialogBox box = Open(h, 2, ShortBodyToken);
            yield return h.Until(() => DialogLook(box, 2, null) == null, () => $"{DialogLook(box, 2, null)} {h.Diagnosis()}");
            // The game's look is read from a second dialog made while the panels are off, as the first is styled.
            h.SetConfig(settings.FlatPanels, false);
            SimpleDialogBox plain = Open(h, 2, ShortBodyToken);
            yield return h.Until(() => ShownButtons(plain) == 2, () => $"the plain dialog has no buttons {h.Diagnosis()}");
            yield return h.Wait(SettleSeconds);
            Vanilla vanilla = Read(plain, 2);
            yield return h.Until(() => VanillaProblem(box, vanilla) == null, () => $"with the panels off, {VanillaProblem(box, vanilla)} {h.Diagnosis()}");
            yield return h.Screenshot("dialog_off");
            Close(plain);
            h.SetConfig(settings.FlatPanels, true);
            yield return h.Until(() => DialogLook(box, 2, null) == null, () => $"with the panels back on, {DialogLook(box, 2, null)} {h.Diagnosis()}");
            Close(box);
            yield return h.Until(() => SimpleDialogBox.instancesList.Count == 0, () => $"the dialogs are still open {h.Diagnosis()}");
        }

        // A dialog made while FlatPanels is off keeps the game's look, with no mod objects, until the panels go on.
        private static IEnumerator OpenedWhileOff(Harness h)
        {
            PluginConfig settings = SevenUIPlugin.Settings;
            h.SetConfig(settings.FlatPanels, false);
            SimpleDialogBox box = Open(h, 2, ShortBodyToken);
            yield return h.Until(() => ShownButtons(box) == 2, () => $"the dialog has no buttons {h.Diagnosis()}");
            Vanilla vanilla = Read(box, 2);
            yield return h.Holds(() => VanillaProblem(box, vanilla) == null, () => $"the dialog changed with the panels off: {VanillaProblem(box, vanilla)} {h.Diagnosis()}");
            h.SetConfig(settings.FlatPanels, true);
            yield return h.Until(() => DialogLook(box, 2, null) == null, () => $"with the panels on, {DialogLook(box, 2, null)} {h.Diagnosis()}");
            Close(box);
            yield return h.Until(() => SimpleDialogBox.instancesList.Count == 0, () => $"the dialog is still open {h.Diagnosis()}");
        }

        // Closing a styled dialog leaves none of the mod's objects, and the styler forgets it; the next one is styled.
        private static IEnumerator CloseLeavesNothing(Harness h)
        {
            DialogBoxStyler styler = Object.FindObjectOfType<DialogBoxStyler>();
            if (!styler) throw h.Fail("the dialog styler isn't on the plugin");
            SimpleDialogBox box = Open(h, 2, ShortBodyToken);
            yield return h.Until(() => DialogLook(box, 2, null) == null && styler.StyledCount == 1, () => $"{DialogLook(box, 2, null)} ({styler.StyledCount} styled) {h.Diagnosis()}");
            Close(box);
            yield return h.Until(() => SimpleDialogBox.instancesList.Count == 0 && styler.StyledCount == 0,
                () => $"{SimpleDialogBox.instancesList.Count} dialogs open, {styler.StyledCount} still tracked {h.Diagnosis()}");
            yield return h.Until(() => Object.FindObjectsOfType<PixelOutline>().All(outline => outline.name != DialogBoxStyler.OutlineName),
                () => $"a dialog outline is left in the scene {h.Diagnosis()}");
            SimpleDialogBox next = Open(h, 1, ShortBodyToken);
            yield return h.Until(() => DialogLook(next, 1, null) == null && styler.StyledCount == 1, () => $"the next dialog: {DialogLook(next, 1, null)} {h.Diagnosis()}");
            Close(next);
            yield return h.Until(() => SimpleDialogBox.instancesList.Count == 0, () => $"the dialog is still open {h.Diagnosis()}");
        }

        private static SimpleDialogBox Open(Harness h, int buttons, string bodyToken)
        {
            SimpleDialogBox box = SimpleDialogBox.Create();
            h.OnCleanup(DestroyAll);
            box.headerToken = new SimpleDialogBox.TokenParamsPair(HeaderToken);
            box.descriptionToken = new SimpleDialogBox.TokenParamsPair(bodyToken);
            string[] tokens = { YesToken, CancelToken, NoToken };
            for (int i = 0; i < buttons; i++)
            {
                // Harmless actions, and never clicked.
                box.AddActionButton(() => { }, tokens[i], true);
            }
            return box;
        }

        // The container also holds the prefab's inactive button templates.
        private static int ShownButtons(SimpleDialogBox box) =>
            box.buttonContainer.GetComponentsInChildren<MPButton>(false).Length;

        private static void Close(SimpleDialogBox box)
        {
            if (box && box.rootObject)
            {
                Object.Destroy(box.rootObject);
            }
        }

        private static void DestroyAll()
        {
            foreach (SimpleDialogBox box in SimpleDialogBox.instancesList.ToArray())
            {
                Close(box);
            }
        }

        // Selects each button as a gamepad does: it gets the accent outline and the others keep the grey one.
        private static IEnumerator SelectEach(Harness h, SimpleDialogBox box, int buttons)
        {
            h.OnCleanup(() =>
            {
                if (box)
                {
                    foreach (MPButton button in box.buttonContainer.GetComponentsInChildren<MPButton>(false))
                    {
                        if (button.eventSystem)
                        {
                            button.eventSystem.SetSelectedGameObject(null);
                        }
                    }
                }
            });
            MPButton[] all = box.buttonContainer.GetComponentsInChildren<MPButton>(false);
            for (int i = 0; i < buttons; i++)
            {
                MPButton selected = all[i];
                selected.eventSystem.SetSelectedGameObject(selected.gameObject);
                yield return h.Until(() => DialogLook(box, buttons, selected) == null, () => $"with button {i} selected, {DialogLook(box, buttons, selected)} {h.Diagnosis()}");
            }
        }

        // Null once the dialog is as the mod styles it: a flat dark panel with Barlow text inside it, centered, and
        // outlined buttons with their labels inside, the selected one (if any) in the accent.
        private static string DialogLook(SimpleDialogBox box, int buttons, MPButton selected)
        {
            if (!box || !box.rootObject) return "the dialog is gone";
            Image panel = DialogBoxStyler.PanelImage(box);
            if (!panel) return "the dialog has no panel image";
            if (panel.sprite) return $"the panel still has the sprite {panel.sprite.name}";
            if (!Near(panel.color, Palette.Panel)) return $"the panel is {panel.color}, not the flat panel color";
            string text = TextProblem(box.headerLabel, panel, Palette.Text) ?? TextProblem(box.descriptionLabel, panel, Palette.HeaderText);
            if (text != null) return text;
            Canvas root = panel.canvas ? panel.canvas.rootCanvas : null;
            if (root)
            {
                RectTransform canvasRect = (RectTransform)root.transform;
                Vector2 middle = canvasRect.InverseTransformPoint(panel.rectTransform.TransformPoint(panel.rectTransform.rect.center));
                if (Vector2.Distance(middle, canvasRect.rect.center) > CenterTolerance) return $"the panel is centered at {middle}, not {canvasRect.rect.center}";
            }
            MPButton[] all = box.buttonContainer.GetComponentsInChildren<MPButton>(false);
            if (all.Length != buttons) return $"the dialog has {all.Length} buttons, not {buttons}";
            HashSet<float> widths = new HashSet<float>();
            foreach (MPButton button in all)
            {
                string problem = ButtonProblem(button, panel, button == selected, selected != null, widths);
                if (problem != null) return problem;
            }
            if (widths.Count > 1) return $"the buttons' outlines are {string.Join(", ", widths)} pixels wide, not all one width";
            return null;
        }

        private static string ButtonProblem(MPButton button, Image panel, bool isSelected, bool someSelected, HashSet<float> widths)
        {
            string name = button.name;
            Image face = button.image;
            if (!face || face.sprite) return $"{name} keeps the game's face";
            Color want = isSelected ? Palette.HoverFace : Palette.Panel;
            if (!Near(face.color, want)) return $"{name}'s face is {face.color}, not {want}";
            PixelOutline[] outlines = button.GetComponentsInChildren<PixelOutline>(true).Where(o => o.name == DialogBoxStyler.OutlineName).ToArray();
            if (outlines.Length != 2) return $"{name} has {outlines.Length} outlines, not 2";
            PixelOutline[] shown = outlines.Where(o => o.enabled).ToArray();
            if (shown.Length != 1) return $"{name} shows {shown.Length} outlines";
            Color outlineColor = isSelected ? Palette.Accent : Palette.Divider;
            if (!Near(shown[0].color, outlineColor)) return $"{name}'s outline is {shown[0].color}, not {(isSelected ? "the accent" : "the divider grey")}";
            string uneven = CanvasLayoutChecks.PixelOutlineProblem(shown[0]);
            if (uneven != null) return $"{name}'s outline: {uneven}";
            widths.Add(shown[0].DrawnWidth);
            foreach (TMP_Text label in button.GetComponentsInChildren<TMP_Text>(true))
            {
                string problem = FontProblem(label);
                if (problem != null) return $"{name}'s label {problem}";
                if (!Inside(label, button.GetComponent<RectTransform>())) return $"{name}'s label doesn't fit its button";
            }
            if (button.GetComponent<RoR2.UI.SkinControllers.ButtonSkinController>() is Behaviour skin && skin.enabled)
                return $"{name}'s skin controller is still on";
            return null;
        }

        private static string TextProblem(TMP_Text label, Image panel, Color color)
        {
            if (!label || string.IsNullOrEmpty(label.text)) return $"{(label ? label.name : "a dialog text")} has no text";
            string font = FontProblem(label);
            if (font != null) return $"{label.name} {font}";
            if (!Near(label.color, color)) return $"{label.name} is {label.color}, not {color}";
            if (!Inside(label, panel.rectTransform)) return $"{label.name} doesn't fit inside the panel";
            return null;
        }

        private static string FontProblem(TMP_Text label) =>
            label.font && label.font.name.StartsWith(Texts.BarlowFontPrefix) ? null : $"is in {(label.font ? label.font.name : "no font")}, not Barlow";

        // Whether the text as drawn lies within the rect, in its own units.
        private static bool Inside(TMP_Text label, RectTransform rect)
        {
            label.ForceMeshUpdate();
            Bounds bounds = label.textBounds;
            Rect area = rect.rect;
            foreach (Vector3 corner in new[] { bounds.min, bounds.max })
            {
                Vector3 local = rect.InverseTransformPoint(label.transform.TransformPoint(corner));
                if (local.x < area.xMin - FitTolerance || local.x > area.xMax + FitTolerance ||
                    local.y < area.yMin - FitTolerance || local.y > area.yMax + FitTolerance) return false;
            }
            return true;
        }

        private static Vanilla Read(SimpleDialogBox box, int buttons)
        {
            Image panel = DialogBoxStyler.PanelImage(box);
            Vanilla look = new Vanilla
            {
                PanelSprite = panel ? panel.sprite : null,
                PanelColor = panel ? panel.color : default,
                HeaderFont = box.headerLabel.font.name,
                HeaderColor = box.headerLabel.color,
                BodyFont = box.descriptionLabel.font.name,
                BodyColor = box.descriptionLabel.color,
            };
            foreach (MPButton button in box.buttonContainer.GetComponentsInChildren<MPButton>(false))
            {
                TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
                look.Buttons.Add((button.image ? button.image.sprite : null, button.image ? button.image.color : default,
                    label ? label.font.name : "", button.GetComponent<RoR2.UI.SkinControllers.ButtonSkinController>()?.enabled ?? false));
            }
            return look;
        }

        // Null once the dialog is the game's again, as read from a dialog made with the panels off, with no mod objects.
        private static string VanillaProblem(SimpleDialogBox box, Vanilla vanilla)
        {
            if (!box || !box.rootObject) return "the dialog is gone";
            Image panel = DialogBoxStyler.PanelImage(box);
            if (panel && panel.sprite != vanilla.PanelSprite) return $"the panel's sprite is {(panel.sprite ? panel.sprite.name : "none")}";
            if (panel && !Near(panel.color, vanilla.PanelColor)) return $"the panel is {panel.color}, not the game's {vanilla.PanelColor}";
            if (!Near(box.headerLabel.color, vanilla.HeaderColor)) return $"the title is {box.headerLabel.color}, not the game's {vanilla.HeaderColor}";
            if (!Near(box.descriptionLabel.color, vanilla.BodyColor)) return $"the body is {box.descriptionLabel.color}, not the game's {vanilla.BodyColor}";
            if (box.headerLabel.font.name != vanilla.HeaderFont) return $"the title is in {box.headerLabel.font.name}, not {vanilla.HeaderFont}";
            if (box.descriptionLabel.font.name != vanilla.BodyFont) return $"the body is in {box.descriptionLabel.font.name}, not {vanilla.BodyFont}";
            if (box.rootObject.GetComponentsInChildren<PixelOutline>(true).Any(o => o.name == DialogBoxStyler.OutlineName)) return "the dialog still has the mod's outlines";
            MPButton[] buttons = box.buttonContainer.GetComponentsInChildren<MPButton>(false);
            if (buttons.Length != vanilla.Buttons.Count) return $"the dialog has {buttons.Length} buttons, not {vanilla.Buttons.Count}";
            for (int i = 0; i < buttons.Length; i++)
            {
                (Sprite sprite, Color color, string font, bool skin) = vanilla.Buttons[i];
                Image face = buttons[i].image;
                if (face && face.sprite != sprite) return $"button {i}'s face has {(face.sprite ? face.sprite.name : "no sprite")}";
                TMP_Text label = buttons[i].GetComponentInChildren<TMP_Text>(true);
                if (label && label.font.name != font) return $"button {i}'s label is in {label.font.name}, not {font}";
                RoR2.UI.SkinControllers.ButtonSkinController controller = buttons[i].GetComponent<RoR2.UI.SkinControllers.ButtonSkinController>();
                if (controller && controller.enabled != skin) return $"button {i}'s skin controller is {controller.enabled}";
            }
            return null;
        }

        private static bool Near(Color a, Color b) =>
            Mathf.Abs(a.r - b.r) < ColorTolerance && Mathf.Abs(a.g - b.g) < ColorTolerance && Mathf.Abs(a.b - b.b) < ColorTolerance &&
            Mathf.Abs(a.a - b.a) < ColorTolerance;
    }
}
