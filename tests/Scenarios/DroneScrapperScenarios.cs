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
        private const string InspectTitleName = "InsepctTitle";
        private const string SevenUIPrefix = "SevenUI";
        private const float OpenTimeout = 5f;
        private const float SettleSeconds = 0.6f;
        private const float StandDistance = 2f;
        private const int FewOptions = 3;
        private const int ManyOptions = 9;
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
                throw h.Skip($"could not load {PanelKey}: {exception.Message} (the Drone Scrapper needs Seekers of the Storm's content)");
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
            bool cards = count <= PickerPanel.CardLimit;
            if (panel.Cards != cards) return $"{count} options are {(panel.Cards ? "cards" : "a grid")}";
            DroneScrapperPickerPanel game = (DroneScrapperPickerPanel)panel.Component;
            RectTransform main = panel.MainPanel;
            if (main.rect.width <= 0f || main.rect.height <= 0f) return $"the panel is {main.rect.size} in size";
            for (int i = 0; i < panel.Buttons.Count; i++)
            {
                PickerButton option = panel.Buttons[i];
                if (!option.Button || !option.Button.interactable) return $"option {i} can't be pressed";
                string part = cards ? PickerPanel.CardName : PickerPanel.SlotName;
                if (!option.Button.GetComponentsInChildren<Transform>(true).Any(child => child.name == part)) return $"option {i} has no {part}";
                if (option.Button.navigation.mode != Navigation.Mode.Explicit) return $"option {i}'s gamepad navigation is {option.Button.navigation.mode}, not the game's explicit";
                if (option.Entry == null || option.Entry.Drone == null) return $"option {i} has no drone";
                if (cards && option.Name.text != Language.GetString(option.Entry.Drone.nameToken)) return $"card {i} shows \"{option.Name.text}\", not its drone's name";
                // Cards all show; of a grid, the rows the scroll view shows, the first at least.
                if (cards || i < panel.Columns)
                {
                    string outside = Outside(main, (RectTransform)option.Button.transform);
                    if (outside != null) return $"option {i} {outside}";
                }
            }
            if (cards)
            {
                for (int i = 0; i < count - 1; i++)
                {
                    if (panel.Buttons[i].Button.navigation.selectOnRight != panel.Buttons[i + 1].Button) return $"card {i}'s right is not card {i + 1}";
                }
            }
            if (!panel.Cancel?.Button) return "the cancel button isn't styled";
            string outsideCancel = Outside(main, (RectTransform)panel.Cancel.Button.transform, true);
            if (outsideCancel != null) return $"the cancel button {outsideCancel}";
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

        // The game's title of the hovered drone takes its tier's color, with the pointer's event sent to the option as
        // the game would get it.
        private static IEnumerator HoverShowsColor(Harness h, PickerPanel panel)
        {
            Transform title = panel.Component.GetComponentsInChildren<Transform>(true).FirstOrDefault(part => part.name == InspectTitleName);
            TMP_Text text = title ? title.GetComponent<TMP_Text>() : null;
            if (!text)
            {
                h.Log($"the scrapper has no {InspectTitleName}; the hovered drone's name color is not checked");
                yield break;
            }
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
            yield return h.Until(() => SevenUIChecks.Near(text.color, want), () => $"the title is {text.color} for option {index}, not its tier's {want}", OpenTimeout);
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
                "the panel restyled with FlatPanels on", OpenTimeout);
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
        // what color. Colors in the buttons are left out, which the game fades as it hovers and selects them.
        private static Dictionary<string, string> Snapshot(DroneScrapperPickerPanel panel)
        {
            Dictionary<string, string> state = new Dictionary<string, string>();
            foreach (Transform part in panel.GetComponentsInChildren<Transform>(true))
            {
                string path = Path(panel.transform, part);
                RectTransform rect = part as RectTransform;
                string look = $"{part.gameObject.activeSelf}";
                if (rect)
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
