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
    // menus' messages) as a dark band across the whole screen (J1 design E, Jacob's pick, 2026-11-23): the title large
    // and centered, the text under it, and the buttons as words with a thin underline, the first (the game's default
    // choice) in the accent. A dialog that ends the game for other players or deletes a profile has its title and first
    // button in red. The game makes each dialog from one prefab, in a run or in the menus, so this lives on the plugin
    // and watches the list of open dialogs. It never calls the dialog's own methods: it reads the list and restyles
    // what is there, and the game adds each button after the dialog is made, so the buttons are looked at again
    // whenever their count changes.
    internal sealed class DialogBoxStyler : MonoBehaviour
    {
        public const string BandName = "SevenUIDialogBand";
        public const string UnderlineName = "SevenUIDialogUnderline";
        private const string LogPrefix = "Dialog box styled: ";
        // How far the band reaches above and below the dialog's own panel, and its thin edges: root canvas units.
        public const float BandPadding = 18f;
        private const float BandEdge = 1f;
        // The title, larger than the game's, and the gap between the buttons' words and their underlines.
        public const float TitleSize = 34f;
        private const float UnderlineGap = 3f;
        private const float UnderlineHeight = 1f;
        // The underline's opacity at rest; the hovered or selected button's is full.
        public const float UnderlineRest = 0.4f;
        // What marks a dialog as one that ends something: the host's quit confirmation (quitting ends the game for
        // everyone) and deleting a profile. The game keeps no token on the dialog, only the text, so it is compared.
        private const string HostQuitToken = "QUIT_RUN_CONFIRM_DIALOG_BODY_HOST";
        private const string DeleteProfileToken = "USER_PROFILE_DELETE_HEADER";

        private readonly Dictionary<SimpleDialogBox, Dialog> dialogs = new Dictionary<SimpleDialogBox, Dialog>();
        private readonly List<SimpleDialogBox> gone = new List<SimpleDialogBox>();
        private readonly List<Dialog> restoring = new List<Dialog>();

        // The number of open dialogs this has restyled, for the dump. Read by the SevenUI.Tests plugin.
        public int StyledCount => dialogs.Count;

        private sealed class Entry
        {
            public MPButton Button;
            public Image Face;
            public TMP_Text Label;
            public Image Underline;
            public bool First;
            public bool? ShownHover;
            public Color ShownColor;
            public Bounds ShownBounds;
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
            public Image Panel;
            public RectTransform Band;
            public Rect ShownBand;
            public string ShownHeader;
            public string ShownDescription;
            public bool Ending;
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
            dialog.Panel = panel;
            if (panel)
            {
                // The panel stays, see-through, so it still takes the pointer as the game's does; the band draws instead.
                Flatten(recorder, panel, Color.clear);
                dialog.Band = AddBand(recorder, panel);
                // The prefab's frame, the title's header panel, and the lavender backings behind the buttons and the
                // (unused) description panel: only the band and the words draw.
                foreach (Image image in panel.GetComponentsInChildren<Image>(true))
                {
                    if (image != panel && !InButton(box, image.transform))
                    {
                        recorder.Disable(image);
                    }
                }
            }
            if (box.headerLabel)
            {
                recorder.Record(box.headerLabel);
                box.headerLabel.fontSize = TitleSize;
                box.headerLabel.fontStyle = FontStyles.Bold;
                box.headerLabel.alignment = TextAlignmentOptions.Center;
            }
            if (box.descriptionLabel)
            {
                recorder.Record(box.descriptionLabel);
                box.descriptionLabel.color = Palette.TooltipBody;
                box.descriptionLabel.alignment = TextAlignmentOptions.Center;
            }
            if (SevenUIPlugin.Settings.DebugLogging.Value)
            {
                SevenUIPlugin.Log.LogInfo(LogPrefix + (panel ? panel.name : "(no panel image)") + " in " + box.rootObject.name);
            }
        }

        // The band: a dark strip across the whole of the dialog's canvas, under the panel and its text (just before
        // the panel's branch of the canvas, so after any dimming the prefab draws over the screen), with a thin edge
        // along its top and bottom. Placed to the panel's height in UpdateBand.
        private static RectTransform AddBand(StyleRecorder recorder, Image panel)
        {
            Canvas canvas = panel.canvas ? panel.canvas.rootCanvas : null;
            Transform root = canvas ? canvas.transform : panel.transform.parent;
            Transform branch = panel.transform;
            while (branch.parent && branch.parent != root)
            {
                branch = branch.parent;
            }
            Image band = UiFactory.NewImage(BandName, root, Palette.DialogBand);
            recorder.Add(band.gameObject);
            band.gameObject.AddComponent<LayoutIgnorer>();
            band.transform.SetSiblingIndex(branch.parent == root ? branch.GetSiblingIndex() : 0);
            RectTransform rect = band.rectTransform;
            rect.pivot = new Vector2(0.5f, 0.5f);
            foreach (bool top in new[] { true, false })
            {
                RectTransform edge = UiFactory.NewImage(top ? "TopEdge" : "BottomEdge", rect, Palette.Divider).rectTransform;
                edge.anchorMin = new Vector2(0f, top ? 1f : 0f);
                edge.anchorMax = new Vector2(1f, top ? 1f : 0f);
                edge.pivot = new Vector2(0.5f, top ? 1f : 0f);
                edge.anchoredPosition = Vector2.zero;
                edge.sizeDelta = new Vector2(0f, BandEdge);
            }
            return rect;
        }

        // Spans the canvas's width and the panel's height plus the padding; worked out again only when the panel moves
        // or grows (it grows as the game adds the text and buttons).
        private static void UpdateBand(Dialog dialog)
        {
            if (!dialog.Band || !dialog.Panel) return;
            RectTransform root = dialog.Band.parent as RectTransform;
            if (!root) return;
            Rect panel = dialog.Panel.rectTransform.rect;
            Vector3 bottom = root.InverseTransformPoint(dialog.Panel.rectTransform.TransformPoint(new Vector3(0f, panel.yMin)));
            Vector3 top = root.InverseTransformPoint(dialog.Panel.rectTransform.TransformPoint(new Vector3(0f, panel.yMax)));
            Rect area = root.rect;
            if (area.height <= 0f) return;
            Rect band = new Rect(0f, (bottom.y - BandPadding - area.yMin) / area.height, 1f,
                (top.y - bottom.y + 2f * BandPadding) / area.height);
            if (band == dialog.ShownBand) return;
            dialog.ShownBand = band;
            dialog.Band.anchorMin = new Vector2(0f, band.yMin);
            dialog.Band.anchorMax = new Vector2(1f, band.yMax);
            dialog.Band.offsetMin = dialog.Band.offsetMax = Vector2.zero;
        }

        // Whether the dialog ends something for good (see HostQuitToken), from its text, checked again when the text
        // changes; the title is red then, and in the text color otherwise.
        private static void UpdateEnding(SimpleDialogBox box, Dialog dialog)
        {
            string header = box.headerLabel ? box.headerLabel.text : null;
            string description = box.descriptionLabel ? box.descriptionLabel.text : null;
            if (header == dialog.ShownHeader && description == dialog.ShownDescription) return;
            dialog.ShownHeader = header;
            dialog.ShownDescription = description;
            dialog.Ending = IsEnding(header, description);
            if (box.headerLabel)
            {
                box.headerLabel.color = dialog.Ending ? Palette.CountLegendary : Palette.Text;
            }
        }

        // Read by the SevenUI.Tests plugin.
        internal static bool IsEnding(string header, string description) =>
            (!string.IsNullOrEmpty(description) && description == RoR2.Language.GetString(HostQuitToken)) ||
            (!string.IsNullOrEmpty(header) && header == RoR2.Language.GetString(DeleteProfileToken));

        // The first button's words: red for a dialog that ends something, the accent otherwise; the others in the text
        // color. Read by the SevenUI.Tests plugin.
        internal static Color WordColor(bool first, bool ending) => !first ? Palette.Text : ending ? Palette.CountLegendary : Palette.Accent;

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
                        dialog.Buttons.Add(StyleButton(dialog, button, dialog.Buttons.Count == 0));
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
            UpdateEnding(box, dialog);
            UpdateBand(dialog);
            foreach (Entry entry in dialog.Buttons)
            {
                UpdateButton(entry, dialog.Ending);
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

        private Entry StyleButton(Dialog dialog, MPButton button, bool first)
        {
            StyleRecorder recorder = dialog.Recorder;
            Entry entry = new Entry { Button = button, First = first };
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
                // See-through, so the button still takes the pointer over its whole rect.
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
                face.color = Color.clear;
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
            entry.Label = label;
            if (label)
            {
                // Its size is the game's, which fits the label to the button.
                recorder.Record(label);
                label.fontStyle = FontStyles.Bold;
                label.characterSpacing = 0f;
                entry.Underline = UiFactory.NewImage(UnderlineName, button.transform, Color.clear);
                recorder.Add(entry.Underline.gameObject);
                entry.Underline.gameObject.AddComponent<LayoutIgnorer>();
                RectTransform line = entry.Underline.rectTransform;
                line.anchorMin = line.anchorMax = line.pivot = new Vector2(0.5f, 0.5f);
            }
            return entry;
        }

        private static void DisableImage(StyleRecorder recorder, Transform holder)
        {
            recorder.Disable(holder ? holder.GetComponent<Image>() : null);
        }

        // The words in their color, and the underline under them, faint at rest and full when the button is hovered
        // (the pointer's, or a gamepad's selection); the underline follows the words as the game lays them out.
        private static void UpdateButton(Entry entry, bool ending)
        {
            if (!entry.Button || !entry.Label) return;
            bool hovered = IsHovered(entry.Button);
            Color color = WordColor(entry.First, ending);
            if (hovered != entry.ShownHover || color != entry.ShownColor)
            {
                entry.ShownHover = hovered;
                entry.ShownColor = color;
                entry.Label.color = color;
                entry.Underline.color = new Color(color.r, color.g, color.b, hovered ? 1f : UnderlineRest);
            }
            Bounds bounds = entry.Label.textBounds;
            if (bounds == entry.ShownBounds || bounds.size.x <= 0f) return;
            entry.ShownBounds = bounds;
            RectTransform button = (RectTransform)entry.Button.transform;
            Vector3 left = button.InverseTransformPoint(entry.Label.transform.TransformPoint(new Vector3(bounds.min.x, bounds.min.y)));
            Vector3 right = button.InverseTransformPoint(entry.Label.transform.TransformPoint(new Vector3(bounds.max.x, bounds.min.y)));
            RectTransform line = entry.Underline.rectTransform;
            Vector2 middle = button.rect.center;
            line.anchoredPosition = new Vector2((left.x + right.x) / 2f - middle.x, left.y - UnderlineGap - UnderlineHeight / 2f - middle.y);
            line.sizeDelta = new Vector2(Mathf.Abs(right.x - left.x), UnderlineHeight);
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
