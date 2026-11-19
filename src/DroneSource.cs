using System.Collections.ObjectModel;
using RoR2;
using RoR2.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static SevenUI.PickerUi;

namespace SevenUI
{
    // The Drone Scrapper's panel (DLC3), which is a class of its own in the game, not a PickupPickerPanel. Its buttons
    // sit in a scroll view, under a "details" part that also holds the game's description of the hovered drone and the
    // scrap it gives; those stay the game's, moved under the buttons.
    internal sealed class DroneSource : PickerSource
    {
        private const string InspectPanelName = "InspectPanel";
        // The game's spelling.
        private const string InspectTitleName = "InsepctTitle";

        private readonly DroneScrapperPickerPanel panel;
        private readonly Transform contentRoot;
        private StyleRecorder recorder;
        private RectTransform inspect;
        private float inspectHeight;
        private TMP_Text inspectTitle;

        private DroneSource(DroneScrapperPickerPanel panel, RectTransform mainPanel, RectTransform juice, Transform contentRoot)
        {
            this.panel = panel;
            this.contentRoot = contentRoot;
            MainPanel = mainPanel;
            Juice = juice;
        }

        public override Component Component => panel;

        public override GridLayoutGroup Grid => panel.gridlayoutGroup;

        public override RectTransform ButtonContainer => panel.buttonContainer;

        public override int MaxColumnCount => panel.maxColumnCount;

        // SetPickupOptions sets each button's explicit navigation.
        public override bool AutomaticNavigation => false;

        public override bool OwnsDetails => true;

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

        // The scroll view and the layout around it are the game's, which sized them to its panel; they are placed by
        // hand now, so their layout groups step aside. The scroll view itself is left as it is.
        public override void Prepare(StyleRecorder recorder)
        {
            this.recorder = recorder;
            Transform viewport = ButtonContainer.parent;
            Transform scrollView = viewport ? viewport.parent : null;
            foreach (Transform part in new[] { contentRoot, scrollView, viewport })
            {
                if (!part || part == Juice) continue;
                recorder.RecordRect((RectTransform)part);
                recorder.Disable(part.GetComponent<LayoutGroup>());
                recorder.Disable(part.GetComponent<ContentSizeFitter>());
            }
            Transform found = FindDeep(contentRoot, InspectPanelName);
            inspect = found ? (RectTransform)found : null;
            inspectTitle = null;
            if (inspect)
            {
                recorder.RecordRect(inspect);
                inspectHeight = inspect.sizeDelta.y;
                Transform title = FindDeep(inspect, InspectTitleName);
                inspectTitle = title ? title.GetComponent<TMP_Text>() : null;
            }
            PickerPanel.Stretch((RectTransform)contentRoot);
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

        public override float PlaceDetails(float x, float y, float width)
        {
            if (!inspect) return 0f;
            TopLeft(inspect, x, y, width, inspectHeight);
            return inspectHeight;
        }

        // The game's title of the hovered drone, in its tier's color as the other pickers' names are.
        public override void Hovered(PickerEntry entry)
        {
            if (!inspectTitle || entry == null) return;
            recorder.Record(inspectTitle);
            inspectTitle.color = entry.Color;
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
