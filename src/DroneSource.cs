using System.Collections.ObjectModel;
using RoR2;
using RoR2.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static SevenUI.PickerUi;

namespace SevenUI
{
    // The Drone Scrapper's panel (DLC3), which is a class of its own in the game, not a PickupPickerPanel. Under its
    // content (MainPanel/Juice) sit, as in the other pickers, the title (Label), the legend and Cancel, and the game's art;
    // its buttons are in a scroll view in ScrapperDetailsVertical, beside the inspect panel that shows the hovered drone,
    // what it scraps into and the game's name and description of it. It is styled as a grid of slots whatever their
    // number, with the mod's name and description under it, then the game's icon and scrap arrow.
    internal sealed class DroneSource : PickerSource
    {
        // The prefab's parts, by path: under the panel, under the content, under the inspect panel and on each button.
        private const string JuicePath = PickerPanel.MainPanelName + "/Juice";
        private const string DetailsPath = "ScrapperDetailsVertical";
        private const string InspectName = "InspectPanel";
        private static readonly string[] ScrollbarNames = { "Scrollbar Vertical", "Scrollbar Horizontal" };
        private const string ScrollHandlePath = "Sliding Area/Handle";
        // The part of the inspect panel the game shows for the selected drone. Its CanvasGroup is held opaque while the
        // flat look is on (KeepShown), as the scoreboard's inspect panel's is: left alone, the icon and scrap arrow row
        // can stay faded out.
        private const string AreaPath = "InspectPanelArea";
        private const string BoxPath = AreaPath + "/InspectionPanel";
        private const string RowPath = BoxPath + "/HorizontalBox";
        private const string IconContainerPath = RowPath + "/InspectIconContainer";
        private const string IconBackgroundPath = IconContainerPath + "/InspectVisualBackground";
        private const string VisualPath = IconBackgroundPath + "/InspectedVisual";
        // The game's name (InsepctTitle, its spelling) and description of the drone, which the mod's details replace.
        private const string InspectTextsPath = RowPath + "/InspectDescriptionContainer";
        private static readonly string[] InspectTexts = { InspectTextsPath + "/InsepctTitle", InspectTextsPath + "/InspectDescription" };
        // The game's hover and selection art on each button: its outlines, and the yellow square and flash it shows
        // (HGButton.hoverHighlightGameObject, turned on every frame while hovered).
        private const string HoverHighlightName = "HoverHighlight";
        internal static readonly string[] SelectionParts =
        {
            PickerPanel.BaseOutlineName, PickerPanel.HoverOutlineName, HoverHighlightName,
            HoverHighlightName + "/ButtonFlashPanel", HoverHighlightName + "/Highlight",
        };
        // The title is the drone scrapper's name and, on a second smaller line, "Turn Drones into recyclable scrap!", in
        // one text; the header strip shows the first line.
        private const int TitleLines = 1;

        private readonly DroneScrapperPickerPanel panel;
        private readonly Transform contentRoot;
        private readonly RectTransform inspect;
        private StyleRecorder recorder;
        private float rowHeight;
        private CanvasGroup areaGroup;
        private bool areaAlphaRecorded;

        private DroneSource(DroneScrapperPickerPanel panel, RectTransform mainPanel, RectTransform juice, Transform contentRoot)
        {
            this.panel = panel;
            this.contentRoot = contentRoot;
            MainPanel = mainPanel;
            Juice = juice;
            inspect = contentRoot.Find(InspectName) as RectTransform;
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
            Transform juice = panel.transform.Find(JuicePath);
            if (!mainPanel || !juice) return null;
            Transform details = juice.Find(DetailsPath);
            // The part of the content that holds the buttons, should it be renamed: the one the buttons are under.
            if (!details || !panel.buttonContainer.IsChildOf(details))
            {
                details = panel.buttonContainer;
                while (details && details.parent != juice)
                {
                    details = details.parent;
                }
            }
            if (!details) return null;
            return new DroneSource(panel, (RectTransform)mainPanel, (RectTransform)juice, details);
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

        // The scroll view and the layout around it are the game's, which sized them to its panel; they are placed by hand
        // now, so their layout groups step aside. The title, legend, Cancel and the game's art are the content's own
        // children, as in the other pickers, which PickerPanel styles and hides.
        public override void Prepare(StyleRecorder recorder)
        {
            this.recorder = recorder;
            // The game's layout settles after the panel opens; what is recorded is what it settles to.
            LayoutRebuilder.ForceRebuildLayoutImmediate(MainPanel);
            Transform viewport = ButtonContainer.parent;
            Transform scrollView = viewport ? viewport.parent : null;
            foreach (Transform part in new[] { contentRoot, scrollView, viewport })
            {
                if (!part) continue;
                recorder.RecordRect((RectTransform)part);
                recorder.Disable(part.GetComponent<LayoutGroup>());
                recorder.Disable(part.GetComponent<ContentSizeFitter>());
            }
            PickerPanel.Stretch((RectTransform)contentRoot);
            StyleTitle();
            StyleScrollbars(scrollView);
            StyleInspect();
            if (SevenUIPlugin.Settings.DebugLogging.Value)
            {
                Transform label = Juice.Find(PickerPanel.TitleName);
                TMP_Text title = label ? label.GetComponent<TMP_Text>() : null;
                SevenUIPlugin.Log.LogInfo($"Drone Scrapper parts: content {Found(Juice)}, title {Found(label)}" +
                                          (title ? $" text \"{title.text}\"" : " without a text") +
                                          $", buttons in {Found(contentRoot)}, scroll view {Found(scrollView)}, inspect {Found(inspect)}, " +
                                          $"area {FoundIn(AreaPath)}, icon {FoundIn(VisualPath)}, " +
                                          $"inspect title {FoundIn(InspectTexts[0])}, inspect description {FoundIn(InspectTexts[1])}, " +
                                          $"legend {Found(Juice.Find(PickerPanel.LegendName))}, cancel {Found(Juice.Find(PickerPanel.CancelName))}");
            }
        }

        private string FoundIn(string path) => Found(inspect ? inspect.Find(path) : null);

        private static string Found(Transform part) => part ? $"'{part.name}'" + (part.gameObject.activeInHierarchy ? "" : " (off)") : "missing";

        // Its first line, the name, in the header strip; the subtitle under it would run onto the grid.
        private void StyleTitle()
        {
            Transform label = Juice.Find(PickerPanel.TitleName);
            TMP_Text title = label ? label.GetComponent<TMP_Text>() : null;
            if (!title) return;
            int lines = title.maxVisibleLines;
            recorder.OnRestore(() =>
            {
                if (title)
                {
                    title.maxVisibleLines = lines;
                }
            });
            title.maxVisibleLines = TitleLines;
        }

        // The scroll bars, shown only when the drones run past the scroll view, as a flat handle without a track.
        private void StyleScrollbars(Transform scrollView)
        {
            if (!scrollView) return;
            foreach (string name in ScrollbarNames)
            {
                Transform bar = scrollView.Find(name);
                if (!bar) continue;
                recorder.Disable(bar.GetComponent<Image>());
                Transform handle = bar.Find(ScrollHandlePath);
                Image image = handle ? handle.GetComponent<Image>() : null;
                if (!image) continue;
                recorder.Record(image);
                image.sprite = null;
                image.type = Image.Type.Simple;
                image.color = Palette.Divider;
            }
        }

        // The inspect panel keeps the hovered drone's icon and the scrap it gives, as a row under the mod's details: the
        // game's name and description and its blue box behind them are hidden, and the row loses the box's padding.
        private void StyleInspect()
        {
            rowHeight = 0f;
            if (!inspect) return;
            recorder.RecordRect(inspect);
            foreach (string path in InspectTexts)
            {
                Transform text = inspect.Find(path);
                recorder.Disable(text ? text.GetComponent<TMP_Text>() : null);
            }
            Transform area = inspect.Find(AreaPath);
            areaGroup = area ? area.GetComponent<CanvasGroup>() : null;
            areaAlphaRecorded = false;
            foreach (string path in new[] { BoxPath, IconContainerPath })
            {
                Transform part = inspect.Find(path);
                if (part)
                {
                    recorder.Disable(part.GetComponent<Image>());
                }
            }
            foreach (string path in new[] { BoxPath, RowPath })
            {
                Transform part = inspect.Find(path);
                HorizontalOrVerticalLayoutGroup group = part ? part.GetComponent<HorizontalOrVerticalLayoutGroup>() : null;
                if (!group) continue;
                recorder.Record(group);
                group.padding = new RectOffset(0, 0, 0, 0);
            }
            rowHeight = RowHeight();
        }

        // The icon's own height and its slot's padding, read from the prefab's layout: the area that holds it can be off
        // until a drone is selected, when its layout can't be measured.
        private float RowHeight()
        {
            Transform visual = inspect.Find(VisualPath);
            LayoutElement element = visual ? visual.GetComponent<LayoutElement>() : null;
            float height = element && element.preferredHeight > 0f ? element.preferredHeight : PickerPanel.SlotSize;
            foreach (string path in new[] { IconContainerPath, IconBackgroundPath })
            {
                Transform part = inspect.Find(path);
                LayoutGroup group = part ? part.GetComponent<LayoutGroup>() : null;
                if (group)
                {
                    height += group.padding.vertical;
                }
            }
            return height;
        }

        // The inspect area's CanvasGroup at full alpha while the area is shown; its alpha when first held comes back with
        // the game's look. The game hides the area with SetActive, which this leaves alone.
        public override void KeepShown()
        {
            if (!areaGroup || !areaGroup.gameObject.activeInHierarchy || areaGroup.alpha >= 1f) return;
            if (!areaAlphaRecorded)
            {
                areaAlphaRecorded = true;
                CanvasGroup group = areaGroup;
                float alpha = group.alpha;
                recorder.OnRestore(() =>
                {
                    if (group)
                    {
                        group.alpha = alpha;
                    }
                });
                if (SevenUIPlugin.Settings.DebugLogging.Value)
                {
                    SevenUIPlugin.Log.LogInfo($"Drone Scrapper: '{group.name}' CanvasGroup alpha {alpha:0.##} held at 1");
                }
            }
            areaGroup.alpha = 1f;
        }

        public override void Restored()
        {
            areaGroup = null;
            areaAlphaRecorded = false;
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
            if (!inspect || rowHeight <= 0f) return 0f;
            TopLeft(inspect, x, y, width, rowHeight);
            return rowHeight;
        }

        // The game's outlines, yellow square and flash on the hovered and selected button give way to the mod's outline.
        // The icon and the "carrying items" mark on it (Icon/ItemIndicator) stay.
        public override void HideSelection(MPButton button)
        {
            HGButton hg = button as HGButton;
            if (hg)
            {
                Image onHover = hg.imageOnHover;
                bool showOnHover = hg.showImageOnHover;
                GameObject highlight = hg.hoverHighlightGameObject;
                recorder.OnRestore(() =>
                {
                    if (hg)
                    {
                        hg.imageOnHover = onHover;
                        hg.showImageOnHover = showOnHover;
                        hg.hoverHighlightGameObject = highlight;
                    }
                });
                hg.imageOnHover = null;
                hg.showImageOnHover = false;
                hg.hoverHighlightGameObject = null;
            }
            foreach (string path in SelectionParts)
            {
                Transform part = button.transform.Find(path);
                if (!part) continue;
                recorder.Disable(part.GetComponent<Image>());
                if (part.name == HoverHighlightName)
                {
                    recorder.SetActive(part.gameObject, false);
                }
            }
        }
    }
}
