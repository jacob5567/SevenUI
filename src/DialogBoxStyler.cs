using System.Collections.Generic;
using System.Text;
using RoR2.UI;
using RoR2.UI.SkinControllers;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SevenUI
{
    // Restyles the game's simple dialog boxes (the quit confirmation, a settings change's "keep these?" prompt, the
    // menus' messages) as a flat dark panel with Barlow text and outlined buttons, as in the pause menu. The game
    // makes each one from one prefab, in a run or in the menus, so this lives on the plugin and watches the list of
    // open dialogs. It never calls the dialog's own methods: it reads the list and restyles what is there, and the
    // game adds each button after the dialog is made, so the buttons are looked at again whenever their count changes.
    internal sealed class DialogBoxStyler : MonoBehaviour
    {
        public const string OutlineName = "SevenUIDialogOutline";
        private const float OutlineEdge = 1f;
        private const string LogPrefix = "Dialog box styled: ";

        // The strip's tint as the design draws it over the dark dialog; the game blends in linear light, where the plain
        // tint came out as a solid grey block. Read by the SevenUI.Tests plugin.
        internal static Color HeaderColor => CanvasBlend.Over(Palette.HeaderStrip, ScoreboardStyler.CanvasBoard);

        private readonly Dictionary<SimpleDialogBox, Dialog> dialogs = new Dictionary<SimpleDialogBox, Dialog>();
        private readonly List<SimpleDialogBox> gone = new List<SimpleDialogBox>();
        private readonly List<Dialog> restoring = new List<Dialog>();

        // The number of open dialogs this has restyled, for the dump. Read by the SevenUI.Tests plugin.
        public int StyledCount => dialogs.Count;

        private sealed class Entry
        {
            public MPButton Button;
            public Image Face;
            public PixelOutline Rest;
            public PixelOutline Hover;
            public bool ShownHover;
        }

        // What the styling of one dialog changed, put back by Restore.
        private sealed class Dialog
        {
            public readonly StyleRecorder Recorder = new StyleRecorder();
            public readonly HashSet<MPButton> Seen = new HashSet<MPButton>();
            public readonly List<Entry> Buttons = new List<Entry>();
            public HudFontSwapper Fonts;
            public int ChildCount = -1;
            public bool FontsApplied;
        }

        private void Update()
        {
            if (!SevenUIPlugin.Settings.FlatPanels.Value)
            {
                RestoreAll();
                return;
            }
            bool barlow = SevenUIPlugin.Settings.BarlowFont.Value;
            foreach (SimpleDialogBox box in SimpleDialogBox.instancesList)
            {
                if (!box) continue;
                if (!dialogs.TryGetValue(box, out Dialog dialog))
                {
                    dialog = new Dialog();
                    dialogs[box] = dialog;
                    StyleDialog(box, dialog);
                }
                UpdateDialog(box, dialog, barlow);
            }
            Forget();
        }

        // A dialog the game destroyed takes its styling with it: only the state kept here is dropped.
        private void Forget()
        {
            if (dialogs.Count == 0) return;
            gone.Clear();
            foreach (SimpleDialogBox box in dialogs.Keys)
            {
                if (!box || !SimpleDialogBox.instancesList.Contains(box))
                {
                    gone.Add(box);
                }
            }
            foreach (SimpleDialogBox box in gone)
            {
                dialogs.Remove(box);
            }
            gone.Clear();
        }

        // Puts every open dialog back as the game made it, and forgets it, so turning the panels on again restyles it.
        private void RestoreAll()
        {
            if (dialogs.Count == 0) return;
            restoring.Clear();
            restoring.AddRange(dialogs.Values);
            dialogs.Clear();
            foreach (Dialog dialog in restoring)
            {
                dialog.Recorder.Restore();
                RestoreFonts(dialog);
            }
            restoring.Clear();
        }

        private void StyleDialog(SimpleDialogBox box, Dialog dialog)
        {
            StyleRecorder recorder = dialog.Recorder;
            Image panel = PanelImage(box);
            if (panel)
            {
                Flatten(recorder, panel, Palette.Panel);
            }
            // The title sits on a panel of its own, which becomes the header strip.
            Image header = box.headerLabel ? box.headerLabel.transform.parent.GetComponent<Image>() : null;
            if (header && header != panel)
            {
                Flatten(recorder, header, HeaderColor);
            }
            if (panel)
            {
                // The prefab's frame, and the lavender backings behind the buttons and the (unused) description panel,
                // would show through the flat panel as bands: only the panel, the header strip and the buttons draw.
                foreach (Image image in panel.GetComponentsInChildren<Image>(true))
                {
                    if (image != panel && image != header && !InButton(box, image.transform))
                    {
                        recorder.Disable(image);
                    }
                }
            }
            StyleLabel(recorder, box.headerLabel, Palette.Text);
            StyleLabel(recorder, box.descriptionLabel, Palette.HeaderText);
            if (SevenUIPlugin.Settings.DebugLogging.Value)
            {
                SevenUIPlugin.Log.LogInfo(LogPrefix + (panel ? panel.name : "(no panel image)") + " in " + box.rootObject.name);
            }
        }

        // The nearest image that holds the title, the text and the buttons: the dialog's panel (DialogPanel), not the
        // title's own header panel. Read by the SevenUI.Tests plugin.
        internal static Image PanelImage(SimpleDialogBox box)
        {
            Transform root = box.rootObject ? box.rootObject.transform : null;
            Transform at = box.headerLabel ? box.headerLabel.transform.parent : null;
            while (at)
            {
                Image image = at.GetComponent<Image>();
                if (image && Holds(at, box.descriptionLabel ? box.descriptionLabel.transform : null) &&
                    Holds(at, box.buttonContainer)) return image;
                if (at == root) break;
                at = at.parent;
            }
            return null;
        }

        private static bool Holds(Transform holder, Transform part) => !part || part.IsChildOf(holder);

        // Whether the image is part of a button (or a button template) rather than the container behind them.
        // Read by the SevenUI.Tests plugin.
        internal static bool InButton(SimpleDialogBox box, Transform image)
        {
            Transform container = box.buttonContainer;
            return container && image != container && image.IsChildOf(container);
        }

        private static void Flatten(StyleRecorder recorder, Image image, Color color)
        {
            Material originalMaterial = image.material;
            recorder.Record(image);
            recorder.OnRestore(() =>
            {
                if (image)
                {
                    image.material = originalMaterial;
                }
            });
            image.sprite = null;
            image.type = Image.Type.Simple;
            image.material = null;
            image.color = color;
        }

        private static int ActiveChildren(Transform container)
        {
            int count = 0;
            for (int i = 0; i < container.childCount; i++)
            {
                if (container.GetChild(i).gameObject.activeSelf)
                {
                    count++;
                }
            }
            return count;
        }

        private static void StyleLabel(StyleRecorder recorder, TMP_Text label, Color color)
        {
            if (!label) return;
            recorder.Record(label);
            label.color = color;
        }

        private void UpdateDialog(SimpleDialogBox box, Dialog dialog, bool barlow)
        {
            bool added = false;
            RectTransform container = box.buttonContainer;
            // The container holds the prefab's inactive button templates, which the game clones for each button: only
            // the shown buttons are styled, so the clones don't carry this styling over.
            int shown = container ? ActiveChildren(container) : 0;
            if (container && shown != dialog.ChildCount)
            {
                dialog.ChildCount = shown;
                foreach (MPButton button in container.GetComponentsInChildren<MPButton>(false))
                {
                    if (dialog.Seen.Add(button))
                    {
                        dialog.Buttons.Add(StyleButton(dialog, button));
                        added = true;
                    }
                }
            }
            if (barlow)
            {
                // Swapping walks the dialog's texts, so it runs when the font turns on or a button was added.
                if (!dialog.FontsApplied || added)
                {
                    dialog.Fonts = dialog.Fonts ?? new HudFontSwapper(null, box.rootObject.transform);
                    dialog.Fonts.Apply();
                    dialog.FontsApplied = true;
                }
            }
            else if (dialog.FontsApplied)
            {
                RestoreFonts(dialog);
            }
            foreach (Entry entry in dialog.Buttons)
            {
                UpdateButton(entry);
            }
        }

        // The skin controllers come back with Restore and set their button's font every frame unless the label takes
        // the language's font, so with BarlowFont on the flag is set again, as the game has it; with it off the swap is
        // undone, as the skin would have done.
        private static void RestoreFonts(Dialog dialog)
        {
            if (dialog.Fonts == null) return;
            dialog.Fonts.Restore();
            dialog.Fonts = null;
            dialog.FontsApplied = false;
        }

        private Entry StyleButton(Dialog dialog, MPButton button)
        {
            StyleRecorder recorder = dialog.Recorder;
            Entry entry = new Entry { Button = button };
            // The game's skin puts its own look and text style back on the button every frame.
            recorder.Disable(button.GetComponent<ButtonSkinController>());
            ColorBlock originalColors = button.colors;
            recorder.OnRestore(() =>
            {
                if (button)
                {
                    button.colors = originalColors;
                }
            });
            ColorBlock colors = originalColors;
            colors.normalColor = colors.highlightedColor = colors.pressedColor = colors.selectedColor = Color.white;
            button.colors = colors;
            entry.Face = button.image ? button.image : button.GetComponent<Image>();
            if (entry.Face)
            {
                Image face = entry.Face;
                Material originalMaterial = face.material;
                recorder.Record(face);
                recorder.OnRestore(() =>
                {
                    if (face)
                    {
                        face.material = originalMaterial;
                    }
                });
                face.sprite = null;
                face.type = Image.Type.Simple;
                face.material = null;
                face.color = Palette.Panel;
            }
            DisableImage(recorder, button.transform.Find("BaseOutline"));
            DisableImage(recorder, button.transform.Find("HoverOutline"));
            HGButton hg = button as HGButton;
            if (hg)
            {
                // The button turns this image back on every frame while it can be pressed.
                Image originalInteractable = hg.imageOnInteractable;
                recorder.OnRestore(() =>
                {
                    if (hg)
                    {
                        hg.imageOnInteractable = originalInteractable;
                    }
                });
                hg.imageOnInteractable = null;
            }
            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
            if (label)
            {
                // Its size is the game's, which fits the label to the button.
                recorder.Record(label);
                label.fontStyle = FontStyles.Normal;
                label.characterSpacing = 0f;
                label.color = Palette.Text;
            }
            entry.Rest = AddOutline(recorder, button.transform, Palette.Divider);
            entry.Rest.enabled = true;
            entry.Hover = AddOutline(recorder, button.transform, Palette.Accent);
            return entry;
        }

        private static PixelOutline AddOutline(StyleRecorder recorder, Transform button, Color color)
        {
            PixelOutline outline = PixelOutline.Add(button, OutlineName, color, OutlineEdge);
            recorder.Add(outline.gameObject);
            return outline;
        }

        private static void DisableImage(StyleRecorder recorder, Transform holder)
        {
            recorder.Disable(holder ? holder.GetComponent<Image>() : null);
        }

        // The hovered button (the pointer's, or a gamepad's selection) gets the accent outline and a faint accent face.
        private static void UpdateButton(Entry entry)
        {
            if (!entry.Button) return;
            bool hovered = IsHovered(entry.Button);
            if (hovered == entry.ShownHover) return;
            entry.ShownHover = hovered;
            entry.Hover.enabled = hovered;
            entry.Rest.enabled = !hovered;
            if (entry.Face)
            {
                entry.Face.color = hovered ? Palette.HoverFace : Palette.Panel;
            }
        }

        // The game's own hover state where the button keeps one; otherwise whether the button is the selected one.
        internal static bool IsHovered(MPButton button)
        {
            HGButton hg = button as HGButton;
            if (hg) return hg.hovering;
            return button.eventSystem && button.eventSystem.currentSelectedGameObject == button.gameObject;
        }

        private void OnDestroy()
        {
            RestoreAll();
        }

        public void Describe(StringBuilder output)
        {
            output.AppendLine($"  restyled dialog boxes: {dialogs.Count}");
        }
    }
}
