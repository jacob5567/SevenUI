using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ModTestRunner;
using RoR2;
using RoR2.UI;
using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.EventSystems;
using UnityEngine.Networking;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace SevenUI.Tests
{
    // The Drone Scrapper's panel (DLC3), restyled as the other pick-an-item panels are. The player uses a scrapper, and
    // the options it offers are then replaced by drones made up here (SetOptionsServer, as the game does when the
    // player's drones change), so the panel shows a set number of them without the player owning any. Part of the
    // pickers suite (PickerScenarios.All).
    internal static class DroneScrapperScenarios
    {
        private const string PanelKey = "RoR2/DLC3/DroneScrapper/DroneScrapperPickerPanel.prefab";
        private const string Interactable = "iscDroneScrapper";
        private const string SevenUIPrefix = "SevenUI";
        private const float OpenTimeout = 5f;
        private const float SettleSeconds = 0.6f;
        private const float StandDistance = 2f;
        private const int FewOptions = 3;
        private const int ManyOptions = 9;
        // The game's inspect panel, under the panel's buttons, and its parts.
        private const string InspectName = "InspectPanel";
        private const string InspectAreaPath = "InspectPanelArea";
        private const string InspectBoxPath = InspectAreaPath + "/InspectionPanel";
        private const string InspectBackgroundPath = InspectBoxPath + "/HorizontalBox/InspectIconContainer/InspectVisualBackground";
        private const string InspectVisualPath = InspectBackgroundPath + "/InspectedVisual";
        private const string TargetScrapName = "TargetScrap";
        private const string ConversionGlyphName = "ConversionGlyph";
        // In HUD units: how far outside the panel a part may reach.
        private const float EdgeTolerance = 1f;

        public static IEnumerable<Scenario> All()
        {
            yield return new Scenario(1, "picker_drone_scrapper_few", Stages.TitanicPlains, h => Opened(h, FewOptions));
            yield return new Scenario(1, "picker_drone_scrapper_many", Stages.TitanicPlains, h => Opened(h, ManyOptions));
            yield return new Scenario(1, "picker_drone_scrapper_options_change", Stages.TitanicPlains, OptionsChange);
            yield return new Scenario(1, "picker_drone_scrapper_flat_panels_off", Stages.TitanicPlains, FlatPanelsOff);
        }

        // As many options as asked for: the drones the game can scrap, over again if there are fewer.
        private static DroneScrapperPickerController.Option[] Options(Harness h, int count)
        {
            List<DroneDef> drones = DroneCatalog.allDroneDefs.Where(drone => drone && drone.canScrap && !string.IsNullOrEmpty(drone.nameToken)).ToList();
            if (drones.Count == 0) throw h.Skip("the game has no drone to scrap");
            return Enumerable.Range(0, count).Select(i => new DroneScrapperPickerController.Option
            {
                networkID = new NetworkInstanceId((uint)(i + 1)),
                isCarryingItems = i % 2 == 1,
                droneIndex = drones[i % drones.Count].droneIndex,
            }).ToArray();
        }

        // Uses a scrapper and waits for its panel, which is then given this many options.
        private static IEnumerator Open(Harness h, int count, Action<DroneScrapperPickerController, DroneScrapperPickerPanel> found)
        {
            try
            {
                if (!Addressables.LoadAssetAsync<GameObject>(PanelKey).WaitForCompletion()) throw h.Skip($"{PanelKey} loaded as nothing");
            }
            catch (Exception exception) when (!(exception is ScenarioSkipped))
            {
                throw h.Skip($"could not load {PanelKey}: {exception.Message} (the Drone Scrapper needs Alloyed Collective's content)");
            }
            DroneScrapperPickerController picker = null;
            yield return PickerScenarios.GoHome(h);
            yield return h.Spawned<DroneScrapperPickerController>($"{Commands.SpawnInteractable} {Interactable}", made => picker = made);
            h.OnCleanup(() =>
            {
                if (picker)
                {
                    NetworkServer.Destroy(picker.gameObject);
                }
            });
            yield return h.StandBefore(picker.gameObject, StandDistance);
            Interactor interactor = Harness.LocalBody.GetComponent<Interactor>();
            if (!interactor) throw h.Fail("the player body has no Interactor");
            HashSet<DroneScrapperPickerPanel> before = new HashSet<DroneScrapperPickerPanel>(Object.FindObjectsOfType<DroneScrapperPickerPanel>());
            h.OnCleanup(() =>
            {
                foreach (DroneScrapperPickerPanel made in Object.FindObjectsOfType<DroneScrapperPickerPanel>())
                {
                    if (!before.Contains(made))
                    {
                        Close(made);
                    }
                }
            });
            picker.OnInteractionBegin(interactor);
            DroneScrapperPickerPanel opened = null;
            yield return h.Until(() => (opened = Object.FindObjectsOfType<DroneScrapperPickerPanel>().FirstOrDefault(panel => !before.Contains(panel))) != null,
                "the Drone Scrapper's panel", OpenTimeout);
            picker.SetOptionsServer(Options(h, count));
            found(picker, opened);
        }

        private static void Close(DroneScrapperPickerPanel panel)
        {
            if (!panel) return;
            // The game's tooltip follows the selected button, and reads it after it is destroyed unless deselected.
            foreach (MPEventSystem events in MPEventSystem.readOnlyInstancesList)
            {
                if (events && events.currentSelectedGameObject && events.currentSelectedGameObject.transform.IsChildOf(panel.transform))
                {
                    events.SetSelectedGameObject(null);
                }
            }
            Object.Destroy(panel.gameObject);
        }

        // The mod's restyling of this scenario's panel.
        private static PickerPanel Styled(DroneScrapperPickerPanel opened)
        {
            SevenUIController controller = SevenUIChecks.Controller;
            return controller ? controller.DroneScrapper.Panels.FirstOrDefault(panel => panel.Alive && panel.Component == opened) : null;
        }

        private static IEnumerator Opened(Harness h, int count)
        {
            DroneScrapperPickerPanel opened = null;
            yield return Open(h, count, (picker, panel) => opened = panel);
            yield return h.Until(() => Styled(opened) is PickerPanel panel && panel.Styled && panel.Buttons.Count == count && AsDesigned(panel, count) == null,
                () => Styled(opened) is PickerPanel panel ? AsDesigned(panel, count) ?? "the panel is not restyled yet" : "the panel is not restyled", OpenTimeout);
            PickerPanel styled = Styled(opened);
            yield return h.Wait(SettleSeconds);
            string shown = AsDesigned(styled, count);
            if (shown != null) throw h.Fail(shown);
            yield return HoverShowsColor(h, styled);
            yield return IconRowShows(h, styled);
            // The first option selected and the last hovered, each of which the game marks with its own art.
            string slots = Slots(styled);
            if (slots != null) throw h.Fail($"with option 0 selected and option {styled.Buttons.Count - 1} hovered, {slots}");
            yield return h.Screenshot("sevenui");
        }

        // The options change while the panel is open, as the game re-sends them when the player's drones change: more of
        // them, then none, then a few again, each as the panel's look for that many.
        private static IEnumerator OptionsChange(Harness h)
        {
            DroneScrapperPickerController picker = null;
            DroneScrapperPickerPanel opened = null;
            yield return Open(h, FewOptions, (made, panel) => { picker = made; opened = panel; });
            foreach (int count in new[] { ManyOptions, 0, FewOptions })
            {
                picker.SetOptionsServer(Options(h, count));
                int expected = count;
                yield return h.Until(() => Styled(opened) is PickerPanel panel && panel.Styled && panel.Buttons.Count == expected &&
                                           (expected == 0 ? EmptyAsDesigned(panel) : AsDesigned(panel, expected)) == null,
                    () => Styled(opened) is PickerPanel panel ? (expected == 0 ? EmptyAsDesigned(panel) : AsDesigned(panel, expected)) ?? "not restyled" : "the panel is not restyled",
                    OpenTimeout);
                yield return h.Screenshot($"options_{count}");
            }
        }

        // With no drones the panel shows its message and Cancel, and the game's scroll view and details are turned off.
        private static string EmptyAsDesigned(PickerPanel panel)
        {
            if (!panel.Alive) return "the panel closed";
            if (!panel.Empty) return "the panel with no options is not the empty look";
            if (!panel.EmptyMessage || panel.EmptyMessage.text != PickerPanel.EmptyMessageText) return "the empty message is missing";
            if (panel.Source.ContentRoot.gameObject.activeSelf) return "the game's scroll view is still turned on, taking space";
            return null;
        }

        // What differs from the design, or null: the look as the other pickers' (cards for a few, a grid for more, the
        // game's art hidden, the mod's parts on each option), and the game's own buttons, navigation and scrolling working.
        private static string AsDesigned(PickerPanel panel, int count)
        {
            if (!panel.Alive) return "the panel closed";
            if (panel.Buttons.Count != count) return $"{panel.Buttons.Count} options are styled, not {count}";
            // The scrapper is a grid whatever the number of options.
            const bool cards = false;
            if (panel.Cards) return $"{count} options are cards, not a grid";
            DroneScrapperPickerPanel game = (DroneScrapperPickerPanel)panel.Component;
            RectTransform main = panel.MainPanel;
            if (main.rect.width <= 0f || main.rect.height <= 0f) return $"the panel is {main.rect.size} in size";
            string slots = Slots(panel);
            if (slots != null) return slots;
            for (int i = 0; i < panel.Buttons.Count; i++)
            {
                PickerButton option = panel.Buttons[i];
                if (!option.Button || !option.Button.interactable) return $"option {i} can't be pressed";
                string part = cards ? PickerPanel.CardName : PickerPanel.SlotName;
                if (!option.Button.GetComponentsInChildren<Transform>(true).Any(child => child.name == part)) return $"option {i} has no {part}";
                if (option.Button.navigation.mode != Navigation.Mode.Explicit) return $"option {i}'s gamepad navigation is {option.Button.navigation.mode}, not the game's explicit";
                if (option.Entry == null || option.Entry.Drone == null) return $"option {i} has no drone";
                // Of a grid, the rows the scroll view shows, the first at least.
                if (i < panel.Columns)
                {
                    string outside = Outside(main, (RectTransform)option.Button.transform);
                    if (outside != null) return $"option {i} {outside}";
                }
            }
            string frame = Frame(panel);
            if (frame != null) return frame;
            if (!panel.Cancel?.Button) return "the cancel button isn't styled";
            RectTransform cancel = (RectTransform)panel.Cancel.Button.transform;
            string outsideCancel = Outside(main, cancel, true);
            if (outsideCancel != null)
            {
                RectTransform up = cancel.parent as RectTransform;
                return $"the cancel button {outsideCancel}: anchors {cancel.anchorMin}-{cancel.anchorMax}, pivot {cancel.pivot}, " +
                       $"position {cancel.anchoredPosition}, size {cancel.sizeDelta}, parent '{(up ? up.name : "none")}' " +
                       $"{(up ? up.rect.ToString() : "")} anchors {(up ? up.anchorMin + "-" + up.anchorMax + " offsets " + up.offsetMin + "/" + up.offsetMax : "")}, " +
                       $"main anchors {main.anchorMin}-{main.anchorMax} size {main.sizeDelta}";
            }
            Vector3[] panelCorners = new Vector3[4];
            Vector3[] cancelCorners = new Vector3[4];
            main.GetWorldCorners(panelCorners);
            cancel.GetWorldCorners(cancelCorners);
            // Corners 0 and 1 are the bottom left and top left; under the panel, the cancel button's top is at or below its
            // bottom, and its right edge lines up with the panel's as the other pickers' do.
            float unit = main.lossyScale.x > 0f ? main.lossyScale.x : 1f;
            if (cancelCorners[1].y > panelCorners[0].y + EdgeTolerance * unit) return "the cancel button is not under the panel";
            if (Mathf.Abs(cancelCorners[2].x - panelCorners[2].x) > EdgeTolerance * unit) return "the cancel button is not at the panel's right edge";
            string cancelLook = CancelAsPickers(main, cancel);
            if (cancelLook != null) return cancelLook;
            string details = Details(panel);
            if (details != null) return details;
            string art = string.Join(", ", panel.Source.Juice.Cast<Transform>()
                .Where(child => child.gameObject.activeSelf && child != panel.Source.ContentRoot && !child.name.StartsWith(SevenUIPrefix) &&
                                child.name != "Label" && child.name != "SubmenuLegend" && child.name != "CancelButton")
                .Select(child => child.name));
            if (art.Length > 0) return $"the game's art is showing: {art}";
            ScrollRect scroll = game.GetComponentInChildren<ScrollRect>(true);
            if (!scroll || !scroll.enabled || !scroll.isActiveAndEnabled) return "the scroll view is turned off";
            if (scroll.content != game.buttonContainer) return "the scroll view no longer scrolls the options";
            if (!scroll.viewport || !scroll.viewport.gameObject.activeInHierarchy) return "the scroll view's viewport is turned off";
            if (!cards && game.buttonContainer.rect.height <= 0f) return "the grid has no height";
            return null;
        }

        // The slots as PickerGrid lays out the other pickers' grids: the game's grid in the pickers' cell and spacing, each
        // button that size and that far from the next, its icon inset by the pickers' margin, and nothing of the game's
        // drawn on it but the icon (and the "carrying items" mark on it): no sprite on the flat face, which the game swaps
        // in while an option is hovered or selected, and no outline or highlight.
        private static string Slots(PickerPanel panel)
        {
            DroneScrapperPickerPanel game = (DroneScrapperPickerPanel)panel.Component;
            GridLayoutGroup grid = game.gridlayoutGroup;
            Vector2 cell = new Vector2(PickerPanel.SlotSize, PickerPanel.SlotSize);
            Vector2 spacing = new Vector2(PickerPanel.SlotGap, PickerPanel.SlotGap);
            if (!Near(grid.cellSize, cell)) return $"the grid's cells are {grid.cellSize}, not the pickers' {cell}";
            if (!Near(grid.spacing, spacing)) return $"the grid's spacing is {grid.spacing}, not the pickers' {spacing}";
            for (int i = 0; i < panel.Buttons.Count; i++)
            {
                PickerButton option = panel.Buttons[i];
                if (!option.Button) return $"option {i} has no button";
                RectTransform slot = (RectTransform)option.Button.transform;
                if (!Near(slot.rect.size, cell)) return $"option {i}'s slot is {slot.rect.size}, not the pickers' {cell}";
                if (i % panel.Columns != 0)
                {
                    RectTransform before = (RectTransform)panel.Buttons[i - 1].Button.transform;
                    float step = slot.localPosition.x - before.localPosition.x;
                    float pitch = PickerPanel.SlotSize + PickerPanel.SlotGap;
                    if (Mathf.Abs(step - pitch) > EdgeTolerance) return $"option {i} is {step:0.##} across from option {i - 1}, not the pickers' {pitch:0.##} (a {PickerPanel.SlotGap:0.##} gap)";
                }
                string inset = IconInset(i, slot, option.Icon);
                if (inset != null) return inset;
                Image face = option.Button.image;
                if (!face || !face.isActiveAndEnabled) return $"option {i}'s flat face is not drawn";
                if (face.overrideSprite) return $"option {i}'s face draws the game's sprite '{face.overrideSprite.name}' (transition {option.Button.transition}), not the flat slot";
                if (!SevenUIChecks.Near(face.color, Palette.ItemSlot)) return $"option {i}'s face is {face.color}, not the pickers' slot color {Palette.ItemSlot}";
                string selection = SelectionShowing(option.Button);
                if (selection != null) return $"option {i} still shows the game's selection graphic {selection}";
            }
            return null;
        }

        // The icon inside its slot by the pickers' margin on each side.
        private static string IconInset(int index, RectTransform slot, RectTransform icon)
        {
            if (!icon) return $"option {index} has no icon";
            Image image = icon.GetComponent<Image>();
            if (!image || !image.isActiveAndEnabled) return $"option {index}'s icon is not drawn";
            Vector3[] corners = new Vector3[4];
            icon.GetWorldCorners(corners);
            // Corners 0 and 2 are the bottom left and top right, here in the slot's units.
            Vector3 low = slot.InverseTransformPoint(corners[0]);
            Vector3 high = slot.InverseTransformPoint(corners[2]);
            Rect area = slot.rect;
            float left = low.x - area.xMin;
            float bottom = low.y - area.yMin;
            float right = area.xMax - high.x;
            float top = area.yMax - high.y;
            float want = PickerPanel.IconInset;
            if (new[] { left, bottom, right, top }.Any(inset => Mathf.Abs(inset - want) > EdgeTolerance))
            {
                return $"option {index}'s icon is inset {left:0.#} left, {bottom:0.#} bottom, {right:0.#} right, {top:0.#} top from its slot, not the pickers' {want:0.#}";
            }
            return null;
        }

        // Cancel as the other pickers have it: the pickers' size, the legend's gap under the panel.
        private static string CancelAsPickers(RectTransform main, RectTransform cancel)
        {
            Vector2 size = new Vector2(PickerPanel.CancelWidth, PickerPanel.CancelHeight);
            if (!Near(cancel.rect.size, size)) return $"the cancel button is {cancel.rect.size}, not the pickers' {size}";
            Vector3[] corners = new Vector3[4];
            cancel.GetWorldCorners(corners);
            // Corner 1 is the top left, in the panel's units.
            float gap = main.rect.yMin - main.InverseTransformPoint(corners[1]).y;
            if (Mathf.Abs(gap - PickerPanel.LegendGap) > EdgeTolerance) return $"the cancel button's top is {gap:0.#} under the panel, not the pickers' {PickerPanel.LegendGap:0.#}";
            return null;
        }

        private static bool Near(Vector2 value, Vector2 expected) =>
            Mathf.Abs(value.x - expected.x) <= EdgeTolerance && Mathf.Abs(value.y - expected.y) <= EdgeTolerance;

        // The flat face behind the options, and the title inside the header strip across its top.
        private static string Frame(PickerPanel panel)
        {
            Transform juice = panel.Source.Juice;
            Transform backdrop = juice.Find(PickerPanel.BackdropName);
            if (!backdrop || !backdrop.gameObject.activeInHierarchy) return "the panel has no flat face";
            Transform strip = juice.Find("SevenUIPickerStrip");
            if (!strip || !strip.gameObject.activeInHierarchy) return "the panel has no header strip";
            RectTransform face = (RectTransform)backdrop;
            foreach (PickerButton option in panel.Buttons.Take(1))
            {
                string outside = Outside(face, (RectTransform)option.Button.transform);
                if (outside != null) return $"the flat face does not hold option 0: {outside}";
            }
            TMP_Text title = panel.Title;
            if (!title || !title.isActiveAndEnabled) return $"the title is not showing: {DescribeTitle(panel)}";
            string inStrip = Outside((RectTransform)strip, title.rectTransform);
            if (inStrip != null) return $"the title is not in the header strip: {inStrip}";
            if (title.transform.parent != juice) return $"the title is not in the panel's content: {DescribeTitle(panel)}";
            // The game's one text holds the name and, on a second line, the subtitle, which would run onto the grid.
            title.ForceMeshUpdate();
            if (title.textInfo.lineCount > 1 && title.maxVisibleLines > 1) return $"the title's subtitle shows under the strip: {DescribeTitle(panel)}";
            return null;
        }

        // The title the panel uses and the game's Label, as found, so a failure says what is there.
        private static string DescribeTitle(PickerPanel panel)
        {
            TMP_Text title = panel.Title;
            string used = title
                ? $"'{title.name}' under '{(title.transform.parent ? title.transform.parent.name : "nothing")}', active {title.gameObject.activeInHierarchy}, " +
                  $"enabled {title.enabled}, lines {title.textInfo?.lineCount} of {title.maxVisibleLines} shown, text \"{title.text}\""
                : "none";
            Transform label = panel.Source.Juice.Find(PickerPanel.TitleName);
            string game = label
                ? $"'{label.name}' active {label.gameObject.activeInHierarchy} with {string.Join(", ", label.GetComponents<Component>().Select(part => part.GetType().Name))}"
                : $"no {PickerPanel.TitleName} under '{panel.Source.Juice.name}' (children: {string.Join(", ", panel.Source.Juice.Cast<Transform>().Select(child => child.name))})";
            return $"title {used}; game's {game}";
        }

        // The game's own description of the hovered drone is hidden (its icon and scrap arrow stay), the mod's name and
        // description show, and the name takes the tier's color.
        private static string Details(PickerPanel panel)
        {
            DroneScrapperPickerPanel game = (DroneScrapperPickerPanel)panel.Component;
            Transform inspect = game.GetComponentsInChildren<Transform>(true).FirstOrDefault(part => part.name == "InspectPanel");
            if (inspect)
            {
                foreach (TMP_Text text in inspect.GetComponentsInChildren<TMP_Text>(true))
                {
                    if (text.isActiveAndEnabled) return $"the game's {text.name} text in the inspect panel is showing, repeating the details";
                }
                Transform box = inspect.Find(InspectBoxPath);
                Image boxImage = box ? box.GetComponent<Image>() : null;
                if (boxImage && boxImage.isActiveAndEnabled) return $"the game's blue box behind the inspect panel ({InspectBoxPath}) is showing";
            }
            if (!panel.DetailsNameText || !panel.DetailsNameText.isActiveAndEnabled) return "the mod's name of the hovered drone is not showing";
            if (!panel.DetailsDescriptionText || !panel.DetailsDescriptionText.isActiveAndEnabled) return "the mod's description of the hovered drone is not showing";
            return null;
        }

        // A game image on a button that is still drawn, other than its face, its icon and the "carrying items" mark on the
        // icon (Icon/ItemIndicator), and the mod's own parts: the game's outlines and its yellow hover square and flash.
        private static string SelectionShowing(MPButton button)
        {
            Transform icon = button.transform.Find(PickerPanel.IconName);
            Image face = button.image ? button.image : button.GetComponent<Image>();
            foreach (Image image in button.GetComponentsInChildren<Image>(true))
            {
                if (image == face || !image.isActiveAndEnabled || icon && image.transform.IsChildOf(icon)) continue;
                if (InModPart(button.transform, image.transform)) continue;
                return NamePath(button.transform, image.transform);
            }
            HGButton hg = button as HGButton;
            if (hg && (hg.imageOnHover || hg.showImageOnHover)) return "imageOnHover";
            if (hg && hg.hoverHighlightGameObject) return $"hoverHighlightGameObject ({hg.hoverHighlightGameObject.name})";
            return null;
        }

        private static bool InModPart(Transform root, Transform part)
        {
            for (Transform up = part; up && up != root; up = up.parent)
            {
                if (up.name.StartsWith(SevenUIPrefix)) return true;
            }
            return false;
        }

        private static string NamePath(Transform root, Transform part)
        {
            string path = part.name;
            for (Transform up = part.parent; up && up != root; up = up.parent)
            {
                path = up.name + "/" + path;
            }
            return path;
        }

        // Whether a part's corners lie in the panel, in the panel's units; the cancel button hangs under it instead.
        private static string Outside(RectTransform panel, RectTransform part, bool below = false)
        {
            Vector3[] corners = new Vector3[4];
            part.GetWorldCorners(corners);
            float scale = panel.lossyScale.x > 0f ? panel.lossyScale.x : 1f;
            Rect area = panel.rect;
            foreach (Vector3 world in corners)
            {
                Vector3 local = panel.InverseTransformPoint(world);
                bool inX = local.x >= area.xMin - EdgeTolerance && local.x <= area.xMax + EdgeTolerance;
                bool inY = below || local.y >= area.yMin - EdgeTolerance && local.y <= area.yMax + EdgeTolerance;
                if (!inX || !inY) return $"reaches outside the panel ({local.x:0.#}, {local.y:0.#} against {area})";
            }
            return null;
        }

        // With a drone selected, as a gamepad selects it, the game shows its icon and the scrap it gives in the inspect
        // panel: drawn at full alpha (the mod holds the area's CanvasGroup opaque), inside the flat panel, and under the
        // mod's description.
        private static IEnumerator IconRowShows(Harness h, PickerPanel panel)
        {
            DroneScrapperPickerPanel game = (DroneScrapperPickerPanel)panel.Component;
            Transform inspect = game.GetComponentsInChildren<Transform>(true).FirstOrDefault(part => part.name == InspectName);
            if (!inspect) throw h.Fail($"the panel has no {InspectName}");
            MPEventSystem events = EventSystem.current as MPEventSystem;
            if (!events) throw h.Fail("there is no event system to select an option with");
            events.SetSelectedGameObject(panel.Buttons[0].Button.gameObject);
            yield return h.Until(() => IconRow(panel, inspect) == null, () => IconRow(panel, inspect), OpenTimeout);
        }

        private static string IconRow(PickerPanel panel, Transform inspect)
        {
            if (!panel.Alive) return "the panel closed";
            Transform area = inspect.Find(InspectAreaPath);
            if (!area) return $"the inspect panel has no {InspectAreaPath}";
            if (!area.gameObject.activeInHierarchy) return $"the inspect panel's {InspectAreaPath} is off with option 0 selected";
            float alpha = 1f;
            for (Transform part = area; part; part = part.parent)
            {
                CanvasGroup group = part.GetComponent<CanvasGroup>();
                if (!group) continue;
                alpha *= group.alpha;
                if (group.ignoreParentGroups) break;
            }
            if (alpha < 0.99f) return $"the inspect panel's icon row is drawn at alpha {alpha:0.##}";
            Transform visual = inspect.Find(InspectVisualPath);
            Image icon = visual ? visual.GetComponent<Image>() : null;
            if (!icon || !icon.isActiveAndEnabled) return $"the drone's icon ({InspectVisualPath}) is not showing";
            Transform scrap = visual.Find(TargetScrapName);
            if (!scrap || !scrap.gameObject.activeInHierarchy) return $"the scrap it gives ({TargetScrapName}) is not showing";
            RectTransform iconRect = (RectTransform)visual;
            if (iconRect.rect.width <= 0f || iconRect.rect.height <= 0f) return $"the drone's icon is {iconRect.rect.size} in size";
            Transform background = inspect.Find(InspectBackgroundPath);
            Image brackets = background ? background.GetComponent<Image>() : null;
            if (brackets && brackets.isActiveAndEnabled) return $"the game's corner brackets around the drone's icon ({InspectBackgroundPath}) are showing";
            // The whole row in the flat face, with the scrap and arrow that hang off the icon's corner.
            Transform backdrop = panel.Source.Juice.Find(PickerPanel.BackdropName);
            if (!backdrop) return "the panel has no flat face";
            foreach (Transform part in new[] { visual, scrap, visual.Find(ConversionGlyphName) })
            {
                if (!part || !part.gameObject.activeInHierarchy) continue;
                string outside = Outside((RectTransform)backdrop, (RectTransform)part);
                if (outside != null) return $"the icon row's {part.name} {outside.Replace("the panel", "the flat face")}";
            }
            TMP_Text description = panel.DetailsDescriptionText;
            if (description)
            {
                Vector3[] iconCorners = new Vector3[4];
                Vector3[] textCorners = new Vector3[4];
                iconRect.GetWorldCorners(iconCorners);
                description.rectTransform.GetWorldCorners(textCorners);
                float unit = panel.MainPanel.lossyScale.y > 0f ? panel.MainPanel.lossyScale.y : 1f;
                // Corner 1 is the top left, corner 0 the bottom left.
                if (iconCorners[1].y > textCorners[0].y + EdgeTolerance * unit) return "the drone's icon is not under the mod's description";
            }
            return null;
        }

        // The game's title of the hovered drone takes its tier's color, with the pointer's event sent to the option as
        // the game would get it.
        private static IEnumerator HoverShowsColor(Harness h, PickerPanel panel)
        {
            TMP_Text text = panel.DetailsNameText;
            if (!text) throw h.Fail("the panel shows no name of the hovered drone");
            MPEventSystem events = EventSystem.current as MPEventSystem;
            if (!events) throw h.Fail("there is no event system to send the pointer's events with");
            int index = panel.Buttons.Count - 1;
            GameObject option = panel.Buttons[index].Button.gameObject;
            PointerEventData pointer = new PointerEventData(events);
            ExecuteEvents.Execute(option, pointer, ExecuteEvents.pointerEnterHandler);
            h.OnCleanup(() =>
            {
                if (option)
                {
                    ExecuteEvents.Execute(option, pointer, ExecuteEvents.pointerExitHandler);
                }
            });
            Color want = panel.Buttons[index].Entry.Color;
            yield return h.Until(() => SevenUIChecks.Near(text.color, want), () => $"the details' name is {text.color} for option {index}, not its tier's {want}", OpenTimeout);
        }

        // With FlatPanels off, a panel opened is the game's, and the look the mod gave it goes back as it was when the
        // setting goes off again: every part's place, state and color is what the game's panel had.
        private static IEnumerator FlatPanelsOff(Harness h)
        {
            PluginConfig settings = SevenUIPlugin.Settings;
            h.SetConfig(settings.FlatPanels, false);
            h.OnCleanup(() => h.SetConfig(settings.FlatPanels, true));
            DroneScrapperPickerPanel opened = null;
            yield return Open(h, FewOptions, (picker, panel) => opened = panel);
            yield return h.Wait(SettleSeconds);
            if (!opened) throw h.Fail("the game closed the panel as it opened");
            string marks = Marks(opened);
            if (marks.Length > 0) throw h.Fail($"with FlatPanels off, the panel has the mod's {marks}");
            if (Styled(opened) != null) throw h.Fail("with FlatPanels off, the panel is restyled");
            Dictionary<string, string> game = Snapshot(opened);
            yield return h.Screenshot("off");

            h.SetConfig(settings.FlatPanels, true);
            yield return h.Until(() => Styled(opened) is PickerPanel panel && panel.Styled && AsDesigned(panel, FewOptions) == null,
                () => Styled(opened) is PickerPanel panel ? $"with FlatPanels on: {AsDesigned(panel, FewOptions) ?? "not restyled"}" : "with FlatPanels on the panel is not restyled",
                OpenTimeout);
            yield return h.Wait(SettleSeconds);

            h.SetConfig(settings.FlatPanels, false);
            yield return h.Wait(SettleSeconds);
            if (!opened) throw h.Fail("the game closed the panel as it went back to its own look");
            marks = Marks(opened);
            if (marks.Length > 0) throw h.Fail($"back in the game's look the panel still has the mod's {marks}");
            Dictionary<string, string> back = Snapshot(opened);
            string differs = string.Join("; ", game.Keys.Where(key => !back.TryGetValue(key, out string now) || now != game[key])
                .Take(8).Select(key => $"{key}: {game[key]} became {(back.TryGetValue(key, out string value) ? value : "missing")}"));
            if (differs.Length > 0) throw h.Fail($"back in the game's look, parts differ from the game's own: {differs}");
            if (back.Count != game.Count) throw h.Fail($"the panel has {back.Count} parts, the game's look had {game.Count}");
        }

        private static string Marks(DroneScrapperPickerPanel panel) => string.Join(", ", panel.GetComponentsInChildren<Transform>(true)
            .Where(part => part.name.StartsWith(SevenUIPrefix)).Select(part => part.name).Distinct());

        // Every part's state: whether it is on, where it is, and for what draws or lays out, whether it is enabled and in
        // what color. Colors in the buttons are left out, which the game fades as it hovers and selects them, as are
        // the hover art's on and off and the inspect panel's laid-out places.
        private static readonly string[] HoverArt = { "HoverHighlight", "ButtonFlashPanel", "Highlight", "HoverOutline" };
        private const string InspectPanelName = "InspectPanel";

        private static Dictionary<string, string> Snapshot(DroneScrapperPickerPanel panel)
        {
            Dictionary<string, string> state = new Dictionary<string, string>();
            foreach (Transform part in panel.GetComponentsInChildren<Transform>(true))
            {
                string path = Path(panel.transform, part);
                RectTransform rect = part as RectTransform;
                // The game turns a button's hover art on and off as the pointer or the selection moves, and lays out
                // the inspect panel once a drone is shown in it, so those states depend on input, not on the mod.
                bool hoverArt = System.Array.IndexOf(HoverArt, part.name) >= 0;
                bool laidOutByShow = part != panel.transform && part.GetComponentsInParent<Transform>(true)
                    .Any(up => up.name == InspectPanelName);
                string look = hoverArt ? "" : $"{part.gameObject.activeSelf}";
                if (rect && !laidOutByShow)
                {
                    look += $" {rect.anchorMin} {rect.anchorMax} {rect.pivot} {rect.anchoredPosition} {rect.sizeDelta}";
                }
                bool inButton = part.GetComponentInParent<MPButton>(true);
                foreach (Behaviour behaviour in part.GetComponents<Behaviour>())
                {
                    if (behaviour is Graphic graphic)
                    {
                        look += $" {behaviour.GetType().Name}:{behaviour.enabled}";
                        Image image = graphic as Image;
                        look += $" {(image ? image.sprite : null)}";
                        if (!inButton)
                        {
                            look += $" {graphic.color}";
                        }
                    }
                    else if (behaviour is Selectable || behaviour is LayoutGroup || behaviour is ContentSizeFitter)
                    {
                        look += $" {behaviour.GetType().Name}:{behaviour.enabled}";
                    }
                }
                Selectable selectable = part.GetComponent<Selectable>();
                if (selectable)
                {
                    Navigation navigation = selectable.navigation;
                    look += $" nav:{navigation.mode},{navigation.selectOnLeft},{navigation.selectOnRight},{navigation.selectOnUp},{navigation.selectOnDown}";
                    look += $" can:{selectable.interactable}";
                }
                state[path] = look;
            }
            return state;
        }

        private static string Path(Transform root, Transform part)
        {
            string path = part.name + part.GetSiblingIndex();
            for (Transform up = part.parent; up && up != root; up = up.parent)
            {
                path = up.name + up.GetSiblingIndex() + "/" + path;
            }
            return path;
        }
    }
}
