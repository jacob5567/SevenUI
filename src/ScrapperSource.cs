using System;
using System.Collections.ObjectModel;
using RoR2;
using RoR2.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static SevenUI.PickerUi;

namespace SevenUI
{
    // The two scrappers' panels: the item scrapper's (ScrapperPickerPanel, a PickupPickerPanel laid out its own way) and
    // the Drone Scrapper's (DLC3, a class of its own). Both are built alike: under the content (MainPanel/Juice) sit the
    // title (Label), the legend and Cancel, and the game's art, and ScrapperDetailsVertical holds a scroll view of the
    // buttons beside the inspect panel that shows the hovered option, what it scraps into and the game's name and
    // description of it. Both are styled the same (J2 design E, Jacob's pick, 2026-11-23): a grid of slots that scrolls
    // when long, the hovered option's description under it, and the exchange strip (PickerExchange) across the bottom;
    // the game's inspect panel is hidden, as the strip says what it did.
    internal sealed class ScrapperSource : PickerSource
    {
        // The prefabs' parts, by path: under the panel, under the content and on each button.
        private const string JuicePath = PickerPanel.MainPanelName + "/Juice";
        private const string DetailsPath = "ScrapperDetailsVertical";
        private const string InspectName = "InspectPanel";
        private const string AreaPath = "InspectPanelArea";
        private static readonly string[] ScrollbarNames = { "Scrollbar Vertical", "Scrollbar Horizontal" };
        // The vertical bar's width, in the grid's right padding.
        private const float ScrollbarWidth = 6f * PickerPanel.Canvas;
        private const string ScrollHandlePath = "Sliding Area/Handle";
        // The item scrapper's panel, by its prefab's name.
        private const string ItemScrapperName = "ScrapperPickerPanel";
        // The game's hover and selection art on each button: its outlines, and the Drone Scrapper's yellow square and
        // flash (HGButton.hoverHighlightGameObject, turned on every frame while hovered).
        private const string HoverHighlightName = "HoverHighlight";
        internal static readonly string[] SelectionParts =
        {
            PickerPanel.BaseOutlineName, PickerPanel.HoverOutlineName, HoverHighlightName,
            HoverHighlightName + "/ButtonFlashPanel", HoverHighlightName + "/Highlight",
        };
        // The item scrapper's own count of the stack on each button, which the slot's count already shows.
        internal const string QuantityName = "Quantity";
        // The title is the scrapper's name and, on a second smaller line, "Turn anything into recyclable scrap!", in one
        // text; the header strip shows the first line.
        private const int TitleLines = 1;
        // The narrowest a scrapper's panel is; the exchange strip widens it further to fit its widest option.
        private const float ScrapperMinWidth = 440f * PickerPanel.Canvas;
        internal const int ItemColumns = 6;

        private readonly Component panel;
        private readonly PickupPickerPanel itemPanel;
        private readonly DroneScrapperPickerPanel dronePanel;
        private readonly Transform contentRoot;
        private readonly RectTransform inspect;
        private StyleRecorder recorder;
        private RectTransform verticalBar;
        private bool placed;

        private ScrapperSource(Component panel, RectTransform mainPanel, RectTransform juice, Transform contentRoot)
        {
            this.panel = panel;
            itemPanel = panel as PickupPickerPanel;
            dronePanel = panel as DroneScrapperPickerPanel;
            this.contentRoot = contentRoot;
            MainPanel = mainPanel;
            Juice = juice;
            inspect = contentRoot.Find(InspectName) as RectTransform;
        }

        public override Component Component => panel;

        // The item scrapper's panel; null for the Drone Scrapper's.
        public PickupPickerPanel ItemPanel => itemPanel;

        public bool Drones => dronePanel;

        public override GridLayoutGroup Grid => itemPanel ? itemPanel.gridlayoutGroup : dronePanel.gridlayoutGroup;

        public override RectTransform ButtonContainer => itemPanel ? itemPanel.buttonContainer : dronePanel.buttonContainer;

        public override int MaxColumnCount => itemPanel ? itemPanel.maxColumnCount : dronePanel.maxColumnCount;

        // The Drone Scrapper's SetPickupOptions sets each button's explicit navigation; the item scrapper's panel says.
        public override bool AutomaticNavigation => itemPanel && itemPanel.automaticButtonNavigation;

        public override bool AlwaysGrid => true;

        public override float MaxContentHeight => PickerPanel.MaxScrollHeight;

        public override float MinWidth => ScrapperMinWidth;

        public override bool Exchanges => true;

        // The item scrapper's game grid is one row of up to a dozen; it wraps at six, as the Drone Scrapper's mock-up.
        public override int ColumnCap => itemPanel ? ItemColumns : 0;

        public override Transform ContentRoot => contentRoot;

        public static ScrapperSource For(DroneScrapperPickerPanel panel) =>
            panel && panel.buttonContainer && panel.gridlayoutGroup ? Find(panel, panel.buttonContainer) : null;

        // Only the item scrapper's panel; the other item pickers are PickupSource's.
        public static ScrapperSource For(PickupPickerPanel panel)
        {
            if (!panel || panel.GetType() != typeof(PickupPickerPanel) || !panel.buttonContainer || !panel.gridlayoutGroup) return null;
            if (!panel.name.StartsWith(ItemScrapperName, StringComparison.Ordinal)) return null;
            return Find(panel, panel.buttonContainer);
        }

        private static ScrapperSource Find(Component panel, RectTransform buttons)
        {
            Transform mainPanel = panel.transform.Find(PickerPanel.MainPanelName);
            Transform juice = panel.transform.Find(JuicePath);
            if (!mainPanel || !juice) return null;
            Transform details = juice.Find(DetailsPath);
            // The part of the content that holds the buttons, should it be renamed: the one the buttons are under.
            if (!details || !buttons.IsChildOf(details))
            {
                details = buttons;
                while (details && details.parent != juice)
                {
                    details = details.parent;
                }
            }
            if (!details) return null;
            return new ScrapperSource(panel, (RectTransform)mainPanel, (RectTransform)juice, details);
        }

        public override bool TryGetOptions(out object options, out int count, out ReadOnlyCollection<MPButton> elements)
        {
            if (itemPanel)
            {
                PickupPickerController.Option[] items = itemPanel.pickupOptions;
                options = items;
                elements = itemPanel.buttonAllocator?.elements;
                count = items != null ? items.Length : 0;
                return items != null && elements != null && elements.Count >= items.Length;
            }
            DroneScrapperPickerController.Option[] drones = dronePanel.droneOptions;
            options = drones;
            elements = dronePanel.buttonAllocator?.elements;
            count = drones != null ? drones.Length : 0;
            return drones != null && elements != null && elements.Count >= drones.Length;
        }

        public override PickerEntry Entry(int index)
        {
            if (itemPanel)
            {
                UniquePickup pickup = itemPanel.pickupOptions[index].pickup;
                return PickerEntry.ForPickup(PickupCatalog.GetPickupDef(pickup.pickupIndex), StackChange.Lose,
                    !pickup.isTempItem && !pickup.isRental);
            }
            return PickerEntry.ForDrone(DroneCatalog.GetDroneDef(dronePanel.droneOptions[index].droneIndex));
        }

        // What scrapping the option gives, as the game works it out (PickupCatalog.FindScrapIndexForItemTier, a lookup in
        // the catalog): one scrap of the item's tier, or for a drone one of its tier per drone combined into it
        // (DroneUpgradeUtils.GetDroneCountFromUpgradeCount, 2 to the power of its upgrades). Reads only.
        public override Exchange ExchangeOf(int index)
        {
            Exchange exchange = new Exchange();
            if (itemPanel)
            {
                PickupPickerController.Option[] items = itemPanel.pickupOptions;
                if (items == null || index < 0 || index >= items.Length) return null;
                PickupDef pickup = PickupCatalog.GetPickupDef(items[index].pickup.pickupIndex);
                if (pickup == null) return null;
                exchange.Scrap = PickupCatalog.GetPickupDef(PickupCatalog.FindScrapIndexForItemTier(pickup.itemTier));
                exchange.ScrapCount = 1;
                exchange.Item = pickup.itemIndex;
                return exchange;
            }
            DroneScrapperPickerController.Option[] drones = dronePanel.droneOptions;
            if (drones == null || index < 0 || index >= drones.Length) return null;
            DroneDef drone = DroneCatalog.GetDroneDef(drones[index].droneIndex);
            if (!drone) return null;
            exchange.Scrap = PickupCatalog.GetPickupDef(PickupCatalog.FindScrapIndexForItemTier(drone.tier));
            exchange.ScrapCount = DroneUpgradeUtils.GetDroneCountFromUpgradeCount(drones[index].droneUpgradeCount);
            exchange.DroneUpgrades = drones[index].droneUpgradeCount;
            exchange.CarryingItems = drones[index].isCarryingItems;
            return exchange;
        }

        // The scroll view and the layout around it are the game's, which sized them to its panel; they are placed by hand
        // now, so their layout groups step aside. The title, legend, Cancel and the game's art are the content's own
        // children, as in the other pickers, which PickerPanel styles and hides.
        public override void Prepare(StyleRecorder recorder)
        {
            this.recorder = recorder;
            RecordArea();
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
            // Cancel fits itself to the game's preferred size for it (200 by 48), which would undo the pickers' size.
            Transform cancel = Juice.Find(PickerPanel.CancelName);
            recorder.Disable(cancel ? cancel.GetComponent<ContentSizeFitter>() : null);
            StyleTitle();
            StyleScrollbars(scrollView);
            HideInspect();
            if (SevenUIPlugin.Settings.DebugLogging.Value)
            {
                Transform label = Juice.Find(PickerPanel.TitleName);
                TMP_Text title = label ? label.GetComponent<TMP_Text>() : null;
                SevenUIPlugin.Log.LogInfo($"{(Drones ? "Drone Scrapper" : "Scrapper")} parts: content {Found(Juice)}, title {Found(label)}" +
                                          (title ? $" text \"{title.text}\"" : " without a text") +
                                          $", buttons in {Found(contentRoot)}, scroll view {Found(scrollView)}, inspect {Found(inspect)}, " +
                                          $"legend {Found(Juice.Find(PickerPanel.LegendName))}, cancel {Found(Juice.Find(PickerPanel.CancelName))}");
            }
        }

        // The inspect area as the game had it before the flat look, put back with the game's look: the game turns it on and
        // off as options are hovered, and can turn it off while the panel is hidden. Recorded before Prepare forces the
        // layout.
        private void RecordArea()
        {
            Transform area = inspect ? inspect.Find(AreaPath) : null;
            if (!area) return;
            recorder.SetActive(area.gameObject, area.gameObject.activeSelf);
            recorder.RecordRect((RectTransform)area);
        }

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

        // The scroll bars, shown only when the options run past the scroll view, as a flat handle without a track. The
        // grid scrolls only up and down. The game's scroll view narrows its viewport to make room for the vertical bar
        // (ScrollbarVisibility.AutoHideAndExpandViewport), which cut off the last column and brought up the horizontal
        // bar; the bar sits in the grid's right padding instead, and the viewport keeps the grid's width.
        private void StyleScrollbars(Transform scrollView)
        {
            if (!scrollView) return;
            ScrollRect scroll = scrollView.GetComponent<ScrollRect>();
            if (scroll)
            {
                bool horizontal = scroll.horizontal;
                Scrollbar horizontalBar = scroll.horizontalScrollbar;
                ScrollRect.ScrollbarVisibility verticalVisibility = scroll.verticalScrollbarVisibility;
                recorder.OnRestore(() =>
                {
                    if (scroll)
                    {
                        scroll.horizontal = horizontal;
                        scroll.horizontalScrollbar = horizontalBar;
                        scroll.verticalScrollbarVisibility = verticalVisibility;
                    }
                });
                scroll.horizontal = false;
                scroll.horizontalScrollbar = null;
                if (horizontalBar)
                {
                    recorder.SetActive(horizontalBar.gameObject, false);
                }
                if (verticalVisibility == ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport)
                {
                    scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
                }
                verticalBar = scroll.verticalScrollbar ? (RectTransform)scroll.verticalScrollbar.transform : null;
                if (verticalBar)
                {
                    recorder.RecordRect(verticalBar);
                }
            }
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

        // The game's inspect panel (the hovered option's icon, the scrap it gives, and the game's name and description)
        // goes: the details and the exchange strip say it. The game keeps filling it in as options are hovered, which is
        // left alone; it is only kept from drawing. Put back with the game's look.
        private void HideInspect()
        {
            if (!inspect) return;
            recorder.SetActive(inspect.gameObject, false);
        }

        // The game turns parts of the inspect panel on as an option is hovered; the panel itself stays off.
        public override void KeepShown()
        {
            if (inspect && inspect.gameObject.activeSelf)
            {
                inspect.gameObject.SetActive(false);
            }
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
                if (verticalBar)
                {
                    // Just outside the scroll view's right edge, centered in the padding, as tall as the scroll view.
                    verticalBar.anchorMin = new Vector2(1f, 0f);
                    verticalBar.anchorMax = Vector2.one;
                    verticalBar.pivot = new Vector2(0f, 0.5f);
                    verticalBar.sizeDelta = new Vector2(ScrollbarWidth, 0f);
                    verticalBar.anchoredPosition = new Vector2((PickerPanel.PaddingX - ScrollbarWidth) / 2f, 0f);
                }
            }
            else
            {
                TopLeft(viewport, x, y, width, height);
            }
            // The grid starts at its top. After that the scroll view owns how far down it is: the panel is laid out again
            // as options are hovered, and putting it back at the top each time undid every scroll.
            if (!placed)
            {
                placed = true;
                ButtonContainer.anchoredPosition = Vector2.zero;
            }
            else
            {
                ButtonContainer.anchoredPosition = new Vector2(0f, ButtonContainer.anchoredPosition.y);
            }
            ButtonContainer.sizeDelta = new Vector2(width, Mathf.Max(height, contentHeight));
        }

        // The game's outlines, yellow square and flash on the hovered and selected button give way to the mod's outline,
        // and the item scrapper's own stack count to the slot's. The icon and the Drone Scrapper's "carrying items" mark
        // on it (Icon/ItemIndicator) stay. The buttons swap their face's sprite for the game's framed square while hovered
        // or selected (Selectable.Transition.SpriteSwap), which would cover the flat slot; with no transition the face
        // keeps its flat color. Put back, the transition shows the button's state again.
        public override void HideSelection(MPButton button)
        {
            Selectable.Transition transition = button.transition;
            recorder.OnRestore(() =>
            {
                if (button)
                {
                    button.transition = transition;
                }
            });
            button.transition = Selectable.Transition.None;
            if (button.image)
            {
                button.image.overrideSprite = null;
            }
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
            Transform quantity = button.transform.Find(QuantityName);
            if (quantity)
            {
                recorder.SetActive(quantity.gameObject, false);
            }
        }
    }
}
