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
        private const string DeleteHeaderToken = "USER_PROFILE_DELETE_HEADER";
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
            public string Backings;
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
            yield return new Scenario(1, "dialog_ending_in_red", Stages.TitanicPlains, EndingInRed);
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

        // A dialog that ends something has its title and first button in red: deleting a profile here, the host's quit
        // in dialog_one_button. The quit confirmation as single player (dialog_two_buttons) stays in the accent.
        private static IEnumerator EndingInRed(Harness h)
        {
            SimpleDialogBox box = SimpleDialogBox.Create();
            h.OnCleanup(DestroyAll);
            box.headerToken = new SimpleDialogBox.TokenParamsPair(DeleteHeaderToken);
            box.descriptionToken = new SimpleDialogBox.TokenParamsPair(ShortBodyToken);
            box.AddActionButton(() => { }, YesToken, true);
            box.AddCancelButton(NoToken);
            yield return h.Until(() => DialogBoxStyler.IsEnding(box.headerLabel.text, box.descriptionLabel.text) && DialogLook(box, 2, null) == null,
                () => $"{DialogLook(box, 2, null) ?? "the dialog isn't taken as ending something"} {h.Diagnosis()}");
            if (!Near(box.headerLabel.color, Palette.CountLegendary)) throw h.Fail($"the title is {box.headerLabel.color}, not red");
            yield return h.Wait(SettleSeconds);
            yield return h.Screenshot("dialog_ending");
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
            yield return h.Until(() => Object.FindObjectsOfType<Image>().All(image => image.name != DialogBoxStyler.BandName &&
                                                                                       image.name != DialogBoxStyler.UnderlineName),
                () => $"a dialog band or underline is left in the scene {h.Diagnosis()}");
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

        // Selects each button as a gamepad does: its underline goes full and the others' stay faint.
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

        // Null once the dialog is as the mod styles it (design E): the game's panel see-through, a dark band across the
        // whole canvas behind it, the title large and centered and the text under it, in Barlow, and the buttons as
        // words with an underline, the first in the accent (red when the dialog ends something), the selected one's
        // underline full and the others' faint.
        private static string DialogLook(SimpleDialogBox box, int buttons, MPButton selected)
        {
            if (!box || !box.rootObject) return "the dialog is gone";
            Image panel = DialogBoxStyler.PanelImage(box);
            if (!panel) return "the dialog has no panel image";
            if (panel.sprite) return $"the panel still has the sprite {panel.sprite.name}";
            if (panel.color.a > ColorTolerance) return $"the panel is {panel.color}, not see-through";
            foreach (Image image in panel.GetComponentsInChildren<Image>(false))
            {
                if (image != panel && image.enabled && !DialogBoxStyler.InButton(box, image.transform))
                    return $"{image.name} still draws a {image.color} backing";
            }
            string band = BandProblem(panel);
            if (band != null) return band;
            bool ending = DialogBoxStyler.IsEnding(box.headerLabel.text, box.descriptionLabel.text);
            string text = TextProblem(box.headerLabel, panel, ending ? Palette.CountLegendary : Palette.Text) ??
                          TextProblem(box.descriptionLabel, panel, Palette.TooltipBody);
            if (text != null) return text;
            if (Mathf.Abs(box.headerLabel.fontSize - DialogBoxStyler.TitleSize) > 0.01f)
                return $"the title is {box.headerLabel.fontSize}, not {DialogBoxStyler.TitleSize}";
            Canvas root = panel.canvas ? panel.canvas.rootCanvas : null;
            if (root)
            {
                RectTransform canvasRect = (RectTransform)root.transform;
                Vector2 middle = canvasRect.InverseTransformPoint(panel.rectTransform.TransformPoint(panel.rectTransform.rect.center));
                if (Vector2.Distance(middle, canvasRect.rect.center) > CenterTolerance) return $"the panel is centered at {middle}, not {canvasRect.rect.center}";
            }
            MPButton[] all = box.buttonContainer.GetComponentsInChildren<MPButton>(false);
            if (all.Length != buttons) return $"the dialog has {all.Length} buttons, not {buttons}";
            for (int i = 0; i < all.Length; i++)
            {
                string problem = ButtonProblem(all[i], DialogBoxStyler.WordColor(i == 0, ending), all[i] == selected);
                if (problem != null) return problem;
            }
            return null;
        }

        // The band spans the canvas's width and reaches past the panel above and below by the padding.
        private static string BandProblem(Image panel)
        {
            Canvas canvas = panel.canvas ? panel.canvas.rootCanvas : null;
            if (!canvas) return "the dialog has no canvas";
            RectTransform root = (RectTransform)canvas.transform;
            Image band = root.GetComponentsInChildren<Image>(false).FirstOrDefault(image => image.name == DialogBoxStyler.BandName);
            if (!band || !band.enabled) return "the dialog has no band";
            if (!Near(band.color, Palette.DialogBand)) return $"the band is {band.color}, not the band color";
            Rect bandRect = RectIn(root, band.rectTransform);
            Rect panelRect = RectIn(root, panel.rectTransform);
            if (Mathf.Abs(bandRect.width - root.rect.width) > FitTolerance) return $"the band is {bandRect.width} wide, not the canvas's {root.rect.width}";
            float above = bandRect.yMax - panelRect.yMax;
            float below = panelRect.yMin - bandRect.yMin;
            if (Mathf.Abs(above - DialogBoxStyler.BandPadding) > FitTolerance || Mathf.Abs(below - DialogBoxStyler.BandPadding) > FitTolerance)
                return $"the band reaches {above} above and {below} below the panel, not {DialogBoxStyler.BandPadding}";
            if (band.transform.GetSiblingIndex() > 0 && panel.transform.IsChildOf(band.transform)) return "the panel is inside the band";
            return null;
        }

        private static Rect RectIn(RectTransform root, RectTransform rect)
        {
            Vector3[] corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Vector3 min = root.InverseTransformPoint(corners[0]);
            Vector3 max = root.InverseTransformPoint(corners[2]);
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private static string ButtonProblem(MPButton button, Color words, bool isSelected)
        {
            string name = button.name;
            Image face = button.image;
            if (!face || face.sprite) return $"{name} keeps the game's face";
            if (face.color.a > ColorTolerance) return $"{name}'s face is {face.color}, not see-through";
            if (button.GetComponentsInChildren<PixelOutline>(true).Any()) return $"{name} still has an outline";
            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
            if (!label) return $"{name} has no label";
            string font = FontProblem(label);
            if (font != null) return $"{name}'s label {font}";
            if (!Near(label.color, words)) return $"{name}'s words are {label.color}, not {words}";
            if (!Inside(label, button.GetComponent<RectTransform>())) return $"{name}'s label doesn't fit its button";
            Image line = button.GetComponentsInChildren<Image>(false).FirstOrDefault(image => image.name == DialogBoxStyler.UnderlineName);
            if (!line || !line.enabled) return $"{name} has no underline";
            Color want = new Color(words.r, words.g, words.b, isSelected ? 1f : DialogBoxStyler.UnderlineRest);
            if (!Near(line.color, want)) return $"{name}'s underline is {line.color}, not {want}";
            RectTransform buttonRect = button.GetComponent<RectTransform>();
            Rect lineRect = RectIn(buttonRect, line.rectTransform);
            Bounds bounds = label.textBounds;
            float textLeft = buttonRect.InverseTransformPoint(label.transform.TransformPoint(bounds.min)).x;
            float textRight = buttonRect.InverseTransformPoint(label.transform.TransformPoint(bounds.max)).x;
            float textBottom = buttonRect.InverseTransformPoint(label.transform.TransformPoint(bounds.min)).y;
            if (Mathf.Abs(lineRect.xMin - textLeft) > FitTolerance || Mathf.Abs(lineRect.xMax - textRight) > FitTolerance)
                return $"{name}'s underline runs {lineRect.xMin} to {lineRect.xMax}, not under its words ({textLeft} to {textRight})";
            if (lineRect.yMax > textBottom) return $"{name}'s underline is at {lineRect.yMax}, over its words' bottom {textBottom}";
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
                Backings = Backings(box, panel),
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
            string backings = Backings(box, panel);
            if (backings != vanilla.Backings) return $"the drawn backings are [{backings}], not the game's [{vanilla.Backings}]";
            if (box.rootObject.GetComponentsInChildren<Image>(true).Any(o => o.name == DialogBoxStyler.BandName || o.name == DialogBoxStyler.UnderlineName))
                return "the dialog still has the mod's band or underlines";
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

        // The enabled images in the dialog other than its buttons' (the header, the frame, the band behind the buttons).
        private static string Backings(SimpleDialogBox box, Image panel) => !panel ? "" : string.Join(", ",
            panel.GetComponentsInChildren<Image>(true).Where(i => i != panel && i.enabled && !DialogBoxStyler.InButton(box, i.transform))
                .Select(i => i.name));

        private static bool Near(Color a, Color b) =>
            Mathf.Abs(a.r - b.r) < ColorTolerance && Mathf.Abs(a.g - b.g) < ColorTolerance && Mathf.Abs(a.b - b.b) < ColorTolerance &&
            Mathf.Abs(a.a - b.a) < ColorTolerance;
    }
}
