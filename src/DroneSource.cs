using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using RoR2;
using RoR2.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static SevenUI.PickerUi;

namespace SevenUI
{
    // The Drone Scrapper's panel (DLC3), which is a class of its own in the game, not a PickupPickerPanel. Its buttons
    // sit in a scroll view, in a part that also holds the title, the details of the hovered drone, the legend and Cancel.
    // It is styled as a grid of slots whatever their number, with the mod's name and description of the hovered drone;
    // the game's icon and scrap arrow for it stay, under them.
    internal sealed class DroneSource : PickerSource
    {
        private const string InspectPanelName = "InspectPanel";
        // Images the game puts on a button that are information, not selection: the icon, and its "carrying items" mark.
        private const string CarryingMark = "arry";

        private readonly DroneScrapperPickerPanel panel;
        private readonly Transform contentRoot;
        private readonly RectTransform inspect;
        private readonly TMP_Text title;
        private StyleRecorder recorder;
        private float inspectHeight;

        private DroneSource(DroneScrapperPickerPanel panel, RectTransform mainPanel, RectTransform juice, Transform contentRoot)
        {
            this.panel = panel;
            this.contentRoot = contentRoot;
            MainPanel = mainPanel;
            Juice = juice;
            Transform found = FindDeep(juice, InspectPanelName);
            inspect = found ? (RectTransform)found : null;
            title = Texts().FirstOrDefault(text => text.name == PickerPanel.TitleName) ?? Texts().FirstOrDefault();
        }

        public override Component Component => panel;

        public override GridLayoutGroup Grid => panel.gridlayoutGroup;

        public override RectTransform ButtonContainer => panel.buttonContainer;

        public override int MaxColumnCount => panel.maxColumnCount;

        // SetPickupOptions sets each button's explicit navigation.
        public override bool AutomaticNavigation => false;

        public override bool AlwaysGrid => true;

        public override float MaxContentHeight => PickerPanel.MaxScrollHeight;

        public override Transform ContentRoot => contentRoot;

        public static DroneSource For(DroneScrapperPickerPanel panel)
        {
            if (!panel || !panel.buttonContainer || !panel.gridlayoutGroup) return null;
            Transform mainPanel = panel.transform.Find(PickerPanel.MainPanelName);
            if (!mainPanel) return null;
            // The content under the panel's rect, and the part of it that holds the buttons.
            Transform content = panel.buttonContainer;
            Transform root = null;
            while (content && content.parent != mainPanel)
            {
                root = content;
                content = content.parent;
            }
            if (!content || !root) return null;
            return new DroneSource(panel, (RectTransform)mainPanel, (RectTransform)content, root);
        }

        public override bool TryGetOptions(out object options, out int count, out ReadOnlyCollection<MPButton> elements)
        {
            DroneScrapperPickerController.Option[] given = panel.droneOptions;
            options = given;
            elements = panel.buttonAllocator?.elements;
            count = given != null ? given.Length : 0;
            return given != null && elements != null && elements.Count >= given.Length;
        }

        public override PickerEntry Entry(int index) =>
            PickerEntry.ForDrone(DroneCatalog.GetDroneDef(panel.droneOptions[index].droneIndex));

        public override TMP_Text FindTitle() => title;

        // The game's texts in the panel that aren't a button's, the inspect part's, or the legend's: its title first, then
        // its subtitle.
        private IEnumerable<TMP_Text> Texts() => Juice.GetComponentsInChildren<TMP_Text>(true)
            .Where(text => !text.GetComponentInParent<MPButton>(true) && !IsIn(text.transform, inspect) &&
                           !text.GetComponentsInParent<Transform>(true).Any(up => up.name == PickerPanel.LegendName));

        private static bool IsIn(Transform part, Transform holder) => holder && part.IsChildOf(holder);

        // The scroll view and the layout around it are the game's, which sized them to its panel; they are placed by
        // hand now, so their layout groups step aside. The title, legend and Cancel button, which the game keeps inside
        // them, move up to the panel's content, where the other pickers have them.
        public override void Prepare(StyleRecorder recorder)
        {
            this.recorder = recorder;
            // The game's layout settles after the panel opens; what is recorded is what it settles to.
            LayoutRebuilder.ForceRebuildLayoutImmediate(MainPanel);
            Transform viewport = ButtonContainer.parent;
            Transform scrollView = viewport ? viewport.parent : null;
            foreach (Transform part in new[] { Juice, contentRoot, scrollView, viewport })
            {
                if (!part) continue;
                recorder.RecordRect((RectTransform)part);
                recorder.Disable(part.GetComponent<LayoutGroup>());
                recorder.Disable(part.GetComponent<ContentSizeFitter>());
            }
            PickerPanel.Stretch(Juice);
            PickerPanel.Stretch((RectTransform)contentRoot);
            foreach (TMP_Text text in Texts().ToList())
            {
                if (text == title)
                {
                    Adopt(text.transform);
                }
                else
                {
                    recorder.Disable(text);
                }
            }
            foreach (string name in new[] { PickerPanel.LegendName, PickerPanel.CancelName })
            {
                Transform part = FindDeep(Juice, name);
                if (part)
                {
                    Adopt(part);
                }
            }
            // Art in the scroll view and around it (a spinning selection ring), which the mod's outlines replace. A mask's
            // image stays, or the scroll view would show everything.
            foreach (Image image in contentRoot.GetComponentsInChildren<Image>(true))
            {
                if (image.GetComponentInParent<MPButton>(true) || IsIn(image.transform, inspect) || image.GetComponent<Mask>()) continue;
                recorder.Disable(image);
            }
            if (!inspect) return;
            foreach (RectTransform part in inspect.GetComponentsInChildren<RectTransform>(true))
            {
                recorder.RecordRect(part);
            }
            // The mod shows the name and description; the icon and the scrap arrow stay.
            foreach (TMP_Text text in inspect.GetComponentsInChildren<TMP_Text>(true))
            {
                recorder.Disable(text);
            }
            inspectHeight = inspect.rect.height > 0f ? inspect.rect.height : LayoutUtility.GetPreferredHeight(inspect);
            inspectHeight = Mathf.Max(inspectHeight, PickerPanel.SlotSize);
        }

        // Moves a part up to the panel's content, and back as it was when the game's look returns.
        private void Adopt(Transform part)
        {
            if (part.parent == Juice) return;
            RectTransform rect = (RectTransform)part;
            recorder.RecordRect(rect);
            Transform parent = part.parent;
            int index = part.GetSiblingIndex();
            recorder.OnRestore(() =>
            {
                if (part && parent)
                {
                    part.SetParent(parent, false);
                    part.SetSiblingIndex(index);
                }
            });
            part.SetParent(Juice, false);
        }

        public override void Restored()
        {
            if (MainPanel)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(MainPanel);
            }
        }

        public override void PlaceContent(float x, float y, float width, float height, float contentHeight)
        {
            RectTransform viewport = (RectTransform)ButtonContainer.parent;
            RectTransform scrollView = (RectTransform)viewport.parent;
            // The scroll view shows the area; the buttons' container, as tall as the buttons, scrolls in it.
            if (scrollView != contentRoot)
            {
                TopLeft(scrollView, x, y, width, height);
                PickerPanel.Stretch(viewport);
                viewport.pivot = new Vector2(0f, 1f);
            }
            else
            {
                TopLeft(viewport, x, y, width, height);
            }
            ButtonContainer.anchoredPosition = Vector2.zero;
            ButtonContainer.sizeDelta = new Vector2(width, Mathf.Max(height, contentHeight));
        }

        public override float PlaceExtras(float x, float y, float width)
        {
            if (!inspect) return 0f;
            TopLeft(inspect, x, y, width, inspectHeight);
            return inspectHeight;
        }

        // The game draws a yellow square on the hovered and selected button and a spinning ring; the mod's outline shows
        // that instead.
        public override void HideSelection(MPButton button)
        {
            HGButton hg = button as HGButton;
            if (hg)
            {
                Image onHover = hg.imageOnHover;
                bool showOnHover = hg.showImageOnHover;
                recorder.OnRestore(() =>
                {
                    if (hg)
                    {
                        hg.imageOnHover = onHover;
                        hg.showImageOnHover = showOnHover;
                    }
                });
                hg.imageOnHover = null;
                hg.showImageOnHover = false;
            }
            Transform icon = button.transform.Find(PickerPanel.IconName);
            Image face = button.image ? button.image : button.GetComponent<Image>();
            foreach (Image image in button.GetComponentsInChildren<Image>(true))
            {
                if (image == face || !IsSelectionImage(button.transform, image, icon)) continue;
                recorder.Disable(image);
            }
        }

        // Whether an image on a button is selection art: not the face, the icon and what is on it, a mark of the game's
        // that tells something, or the mod's own.
        internal static bool IsSelectionImage(Transform button, Image image, Transform icon)
        {
            if (icon && image.transform.IsChildOf(icon)) return false;
            if (image.name.Contains(CarryingMark)) return false;
            for (Transform up = image.transform; up && up != button; up = up.parent)
            {
                if (up.name.StartsWith(PickerPanel.SevenUIPrefix)) return false;
            }
            return true;
        }

        private static Transform FindDeep(Transform root, string name)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == name) return child;
            }
            return null;
        }
    }
}
