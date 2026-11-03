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
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace SevenUI.Tests
{
    // The pick-an-item panels, each checked and shot in SevenUI's look and the game's: a Void Potential's three items
    // as cards; Command for every group of items or equipment it offers, the common items as a grid with the hovered
    // item's details under it, and an item outside any group, which Command offers alone; and every other panel in
    // the game, opened by a Command cube in its place. The pickers suite (SevenUITests.Suites). The pointer can't be
    // moved, so hovering is the game's own hover state, set on a button.
    internal static class PickerScenarios
    {
        // Long enough for the panel's opening transition and the switch between looks to settle.
        private const float SettleSeconds = 0.6f;
        private const float OpenTimeout = 5f;
        // How long a panel the mod would restyle takes to be restyled: it is done in the frame the panel opens.
        private const float StyleTimeout = 1f;
        // Close enough to stay within the picker's reach.
        private const float StandDistance = 2f;
        // The slot hovered in the grid: one past the first row, so it is not the details' starting item.
        private const int HoveredSlot = 6;
        // In HUD units: how far a part's size may be from the canvas's.
        private const float SizeTolerance = 0.5f;
        // The options given to the other panels: a few for the ones that offer a choice of rewards, and a whole
        // inventory's worth for the ones that offer the player's own items.
        private const int FewOptions = 3;
        private const int InventoryOptions = 12;
        // How far ahead the player's starting look is kept, and how long a teleport takes to land.
        private const float HomeLookDistance = 10f;
        private const float TeleportSettleSeconds = 0.2f;

        private static Stage homeStage;
        private static Vector3 home;
        private static Vector3 homeLook;

        // Command's groups (PickupTransmutationManager.RebuildPickupGroups), each by what drops for it. The common
        // items are picker_command_white, which also hovers.
        private static readonly (string Name, Func<PickupDef, bool> Drops)[] CommandGroups =
        {
            ("green", pickup => IsGroupItem(pickup, ItemTier.Tier2, false)),
            ("red", pickup => IsGroupItem(pickup, ItemTier.Tier3, false)),
            ("boss", pickup => IsGroupItem(pickup, ItemTier.Boss, false)),
            ("lunar", pickup => IsGroupItem(pickup, ItemTier.Lunar, false)),
            ("void_white", pickup => IsGroupItem(pickup, ItemTier.VoidTier1, false)),
            ("void_green", pickup => IsGroupItem(pickup, ItemTier.VoidTier2, false)),
            ("void_red", pickup => IsGroupItem(pickup, ItemTier.VoidTier3, false)),
            ("void_boss", pickup => IsGroupItem(pickup, ItemTier.VoidBoss, false)),
            ("power_shape", pickup => IsGroupItem(pickup, ItemTier.Boss, true)),
            ("cursed", pickup => IsGroupItem(pickup, ItemTier.Cursed, false)),
            ("page", pickup => IsGroupItem(pickup, ItemTier.Page, false)),
            ("hex", pickup => IsGroupItem(pickup, ItemTier.Hex, false)),
            ("equipment", pickup => IsGroupEquipment(pickup, false, false)),
            ("lunar_equipment", pickup => IsGroupEquipment(pickup, false, true)),
            ("boss_equipment", pickup => IsGroupEquipment(pickup, true, false)),
        };

        // The other panels' options where they are not a few common items: the player's inventory, or the tier the
        // panel is for.
        private static readonly Dictionary<string, (int Count, ItemTier Tier)> PanelOptions = new Dictionary<string, (int, ItemTier)>
        {
            ["scrapper"] = (InventoryOptions, ItemTier.Tier1),
            ["lemurian_egg"] = (InventoryOptions, ItemTier.Tier1),
            ["rebirth"] = (InventoryOptions, ItemTier.Tier1),
            ["meal_prep"] = (InventoryOptions, ItemTier.Tier1),
            ["solus_vendor"] = (InventoryOptions, ItemTier.Tier1),
            ["cursed_book"] = (FewOptions, ItemTier.Page),
            ["cursed_potential"] = (FewOptions, ItemTier.Cursed),
            ["page_potential"] = (FewOptions, ItemTier.Page),
            ["hallowed_potential"] = (FewOptions, ItemTier.Hex),
        };

        public static IEnumerable<Scenario> All()
        {
            yield return new Scenario(1, "picker_void_potential", Stages.TitanicPlains, VoidPotential);
            yield return new Scenario(1, "picker_command_white", Stages.TitanicPlains, CommandWhite);
            foreach ((string name, Func<PickupDef, bool> drops) in CommandGroups)
            {
                yield return new Scenario(1, "picker_command_" + name, Stages.TitanicPlains, h => Command(h, name, drops));
            }
            yield return new Scenario(1, "picker_command_single", Stages.TitanicPlains, CommandSingle);
            foreach ((string name, string key) in GameNames.PickerPanels)
            {
                yield return new Scenario(1, "picker_panel_" + name, Stages.TitanicPlains, h => OtherPanel(h, name, key));
            }
        }

        private static IEnumerator VoidPotential(Harness h)
        {
            UniquePickup[] voids = GameNames.VoidItems.Select(name => ItemCatalog.FindItemIndex(name))
                .Select(item => new UniquePickup(item != ItemIndex.None ? PickupCatalog.FindPickupIndex(item) : PickupIndex.none)).ToArray();
            if (voids.Any(pickup => pickup.pickupIndex == PickupIndex.none)) throw h.Skip("the void items need Survivors of the Void");
            yield return GoHome(h);
            PickupPickerController picker = h.SpawnPrefab<PickupPickerController>(GameNames.VoidPotential);
            picker.SetOptionsServer(PickupPickerController.GenerateOptionsFromList(voids));
            PickerPanel panel = null;
            yield return OpenStyled(h, picker, found => panel = found);
            yield return CheckStyled(h, picker, panel);
        }

        private static IEnumerator CommandWhite(Harness h)
        {
            yield return GoHome(h);
            PickupPickerController picker = h.SpawnPrefab<PickupPickerController>(GameNames.CommandCube);
            picker.SetOptionsFromPickupForCommandArtifact(new UniquePickup(PickupCatalog.FindPickupIndex(ItemCatalog.FindItemIndex(GameNames.Syringe))));
            if (picker.options.Length <= PickerPanel.CardLimit) throw h.Fail($"Command offers only {picker.options.Length} items");
            PickerPanel panel = null;
            yield return OpenStyled(h, picker, found => panel = found);
            if (panel.Cards) throw h.Fail($"{panel.Buttons.Count} options are cards, not a grid");
            yield return CheckStyled(h, picker, panel);
            // Switching the looks restyles the panel anew.
            panel = OpenPanel(panel.Panel);

            // Hovered, the slot is outlined and the details show its item.
            PickerButton hovered = panel.Buttons[HoveredSlot];
            hovered.Hover.hovering = true;
            h.OnCleanup(() =>
            {
                if (hovered.Hover)
                {
                    hovered.Hover.hovering = false;
                }
            });
            yield return h.Until(() => DetailsShow(panel, HoveredSlot) && hovered.Outline.enabled,
                () => $"the details show \"{panel.DetailsNameText.text}\" and the outline is {(hovered.Outline.enabled ? "on" : "off")} " +
                      $"for the hovered slot {HoveredSlot}");
            yield return h.Wait(SettleSeconds);
            yield return h.Screenshot("hovered");
        }

        // Command offers every pickup in the dropped one's group, as cards or a grid by how many there are. Some are
        // unavailable in this run, and dimmed.
        private static IEnumerator Command(Harness h, string group, Func<PickupDef, bool> drops)
        {
            PickupDef dropped = PickupCatalog.allPickups.FirstOrDefault(pickup => drops(pickup) &&
                PickupTransmutationManager.GetGroupFromPickupIndex(pickup.pickupIndex) != null);
            if (dropped == null) throw h.Skip($"the game has no {group} pickups in a Command group");
            yield return GoHome(h);
            PickupPickerController picker = h.SpawnPrefab<PickupPickerController>(GameNames.CommandCube);
            picker.SetOptionsFromPickupForCommandArtifact(new UniquePickup(dropped.pickupIndex));
            if (!picker.options.Any(option => option.available)) throw h.Skip($"no {group} pickup is available in this run");
            h.Log($"Command for {Language.GetString(dropped.nameToken)} offers {picker.options.Length}, " +
                  $"{picker.options.Count(option => option.available)} available");
            PickerPanel panel = null;
            yield return OpenStyled(h, picker, found => panel = found);
            yield return CheckStyled(h, picker, panel);
        }

        // An item in no group, such as a world-unique one like Halcyon Seed: Command offers only that item.
        private static IEnumerator CommandSingle(Harness h)
        {
            PickupDef dropped = PickupCatalog.allPickups.Where(IsVisibleItem)
                .Where(pickup => PickupTransmutationManager.GetGroupFromPickupIndex(pickup.pickupIndex) == null)
                .OrderByDescending(pickup => ItemCatalog.GetItemDef(pickup.itemIndex).ContainsTag(ItemTag.WorldUnique))
                .FirstOrDefault();
            if (dropped == null) throw h.Skip("every item is in a Command group");
            yield return GoHome(h);
            PickupPickerController picker = h.SpawnPrefab<PickupPickerController>(GameNames.CommandCube);
            picker.SetOptionsFromPickupForCommandArtifact(new UniquePickup(dropped.pickupIndex));
            if (picker.options.Length != 1) throw h.Fail($"Command for {dropped.internalName} offers {picker.options.Length} options, not 1");
            h.Log($"Command for {Language.GetString(dropped.nameToken)} offers it alone");
            PickerPanel panel = null;
            yield return OpenStyled(h, picker, found => panel = found);
            yield return CheckStyled(h, picker, panel);
        }

        // Another of the game's panels, opened by a Command cube given it and items like the ones it offers. A panel
        // the mod restyles is checked as Command's are; one it leaves to the game must have none of its parts.
        private static IEnumerator OtherPanel(Harness h, string name, string key)
        {
            GameObject prefab;
            try
            {
                prefab = Addressables.LoadAssetAsync<GameObject>(key).WaitForCompletion();
            }
            catch (Exception exception)
            {
                throw h.Skip($"could not load {key}: {exception.Message}");
            }
            if (!prefab) throw h.Skip($"{key} loaded as nothing");
            PickupPickerPanel prefabPanel = prefab.GetComponent<PickupPickerPanel>();
            if (!prefabPanel) throw h.Skip($"{key} is not a pick-an-item panel");

            (int count, ItemTier tier) = PanelOptions.TryGetValue(name, out (int, ItemTier) options) ? options : (FewOptions, ItemTier.Tier1);
            PickupPickerController.Option[] items = ItemCatalog.allItemDefs
                .Where(item => item.tier == tier && !item.hidden && !item.ContainsTag(ItemTag.WorldUnique))
                .Select(item => PickupCatalog.FindPickupIndex(item.itemIndex))
                .Where(pickup => pickup != PickupIndex.none)
                .Take(count)
                .Select(pickup => new PickupPickerController.Option { pickup = new UniquePickup(pickup), available = true })
                .ToArray();
            if (items.Length == 0) throw h.Skip($"the game has no {tier} items");

            yield return GoHome(h);
            PickupPickerController picker = h.SpawnPrefab<PickupPickerController>(GameNames.CommandCube);
            picker.panelPrefab = prefab;
            picker.SetOptionsServer(items);
            h.Log($"{name}: {prefabPanel.GetType().Name} with {items.Length} {tier} items");
            PickupPickerPanel opened = null;
            yield return Open(h, picker, found => opened = found, true);
            PickerPanel panel = null;
            float until = Time.unscaledTime + StyleTimeout;
            while (panel == null && Time.unscaledTime < until)
            {
                panel = OpenPanel(opened);
                if (panel != null && !panel.Styled)
                {
                    panel = null;
                }
                yield return null;
            }
            if (panel != null)
            {
                yield return CheckStyled(h, picker, panel);
                yield break;
            }

            // As the lemurian egg's and Ultima's sometimes are, opened from a cube rather than their own interactables.
            if (!opened) throw h.Skip("the game closed the panel as it opened, from a Command cube");
            h.Log($"{name}: SevenUI leaves {opened.name} in the game's look");
            string parts = string.Join(", ", opened.GetComponentsInChildren<Transform>(true)
                .Where(part => part.name.StartsWith("SevenUI")).Select(part => part.name).Distinct());
            if (parts.Length > 0) throw h.Fail($"the unstyled panel has the mod's {parts}");
            yield return h.Wait(SettleSeconds);
            yield return h.Screenshot("game");
        }

        // As cards or a grid by how many options there are, laid out as the canvas's, with the details under a grid
        // showing its first item, in both looks.
        private static IEnumerator CheckStyled(Harness h, PickupPickerController picker, PickerPanel panel)
        {
            int count = picker.options.Length;
            bool cards = count <= PickerPanel.CardLimit;
            if (panel.Cards != cards) throw h.Fail($"{count} options are {(panel.Cards ? "cards" : "a grid")}");
            if (cards)
            {
                yield return h.Until(() => CardsAsCanvas(panel) == null, () => CardsAsCanvas(panel));
            }
            else
            {
                yield return h.Until(() => GridAsCanvas(panel) == null, () => GridAsCanvas(panel));
                yield return h.Until(() => DetailsShow(panel, 0), () => $"the details show \"{panel.DetailsNameText.text}\", not the first item");
            }
            yield return h.Wait(SettleSeconds);
            yield return CaptureLooks(h, panel);
        }

        private static bool IsVisibleItem(PickupDef pickup)
        {
            ItemDef item = pickup.itemIndex != ItemIndex.None ? ItemCatalog.GetItemDef(pickup.itemIndex) : null;
            return item && !item.hidden && item.tier != ItemTier.NoTier;
        }

        // As the game picks a group's items: the tier, power shapes apart, and never a world-unique item.
        private static bool IsGroupItem(PickupDef pickup, ItemTier tier, bool powerShape)
        {
            ItemDef item = pickup.itemIndex != ItemIndex.None ? ItemCatalog.GetItemDef(pickup.itemIndex) : null;
            return item && item.tier == tier && item.ContainsTag(ItemTag.PowerShape) == powerShape && !item.ContainsTag(ItemTag.WorldUnique);
        }

        private static bool IsGroupEquipment(PickupDef pickup, bool boss, bool lunar)
        {
            EquipmentDef equipment = pickup.equipmentIndex != EquipmentIndex.None ? EquipmentCatalog.GetEquipmentDef(pickup.equipmentIndex) : null;
            return equipment && equipment.canDrop && equipment.isBoss == boss && equipment.isLunar == lunar;
        }

        // The player uses the interactable, and the game opens its panel under the HUD. The cube and the potential are
        // dropped with physics, and spawned bare they fall through the ground; the game closes the panel once the
        // player is out of reach of it, so they are held where they are. Opened as the server does on an interaction
        // (the runner is the host), since a spawned cube may not be under the crosshair.
        // A panel opened by a cube in place of its own interactable may need that interactable's state, which the game
        // throws on: a skip, since the panel can't be shown that way.
        private static IEnumerator Open(Harness h, PickupPickerController picker, Action<PickupPickerPanel> found, bool borrowed = false)
        {
            foreach (Rigidbody body in picker.GetComponentsInChildren<Rigidbody>())
            {
                body.velocity = Vector3.zero;
                body.isKinematic = true;
            }
            yield return h.StandBefore(picker.gameObject, StandDistance);
            Interactor interactor = Harness.LocalBody.GetComponent<Interactor>();
            if (!interactor) throw h.Fail("the player body has no Interactor");
            try
            {
                picker.OnInteractionBegin(interactor);
            }
            catch (Exception exception) when (borrowed)
            {
                throw h.Skip($"the game could not open the panel from a Command cube: {exception.GetType().Name} {exception.Message}");
            }
            yield return h.Until(() => picker.panelInstanceController, "the picker's panel", OpenTimeout);
            PickupPickerPanel opened = picker.panelInstanceController;
            h.OnCleanup(() =>
            {
                if (!opened) return;
                // The game's tooltip follows the selected button, and reads it after it is destroyed unless deselected.
                foreach (MPEventSystem events in MPEventSystem.readOnlyInstancesList)
                {
                    if (events && events.currentSelectedGameObject && events.currentSelectedGameObject.transform.IsChildOf(opened.transform))
                    {
                        events.SetSelectedGameObject(null);
                    }
                }
                Object.Destroy(opened.gameObject);
            });
            found(opened);
        }

        private static IEnumerator OpenStyled(Harness h, PickupPickerController picker, Action<PickerPanel> found)
        {
            PickupPickerPanel opened = null;
            yield return Open(h, picker, found => opened = found);
            PickerPanel panel = null;
            yield return h.Until(() => (panel = OpenPanel(opened)) != null && panel.Styled, "a restyled pick-an-item panel", OpenTimeout);
            found(panel);
        }

        // The mod's restyling of this scenario's panel, not of one an earlier scenario left closing.
        private static PickerPanel OpenPanel(PickupPickerPanel opened)
        {
            SevenUIController controller = SevenUIChecks.Controller;
            return controller ? controller.Pickers.Panels.FirstOrDefault(panel => panel.Alive && panel.Panel == opened) : null;
        }

        // Back where the first picker scenario on this stage stood, facing the same way: each cube is spawned ahead
        // of the player and the player then stands before it, so without this they walk on into the stage's walls.
        private static IEnumerator GoHome(Harness h)
        {
            CharacterBody body = Harness.LocalBody;
            if (!body || !body.inputBank) throw h.Fail("the player has no body");
            if (homeStage != Stage.instance)
            {
                homeStage = Stage.instance;
                home = body.footPosition;
                homeLook = body.inputBank.aimOrigin + body.inputBank.aimDirection * HomeLookDistance;
                yield break;
            }
            h.Teleport(home);
            yield return h.Wait(TeleportSettleSeconds);
            yield return h.AimAt(homeLook);
        }

        private static bool DetailsShow(PickerPanel panel, int index) =>
            panel.DetailsNameText && panel.DetailsNameText.text == Language.GetString(panel.Buttons[index].Pickup.nameToken);

        // Each card the canvas's width, side by side without overlapping, with its item's name, inside the screen.
        private static string CardsAsCanvas(PickerPanel panel)
        {
            if (!panel.Alive) return "the panel closed";
            for (int i = 0; i < panel.Buttons.Count; i++)
            {
                PickerButton card = panel.Buttons[i];
                RectTransform rect = (RectTransform)card.Button.transform;
                if (Mathf.Abs(rect.rect.width - PickerPanel.CardWidth) > SizeTolerance)
                    return $"card {i} is {rect.rect.width:0.#} wide, not {PickerPanel.CardWidth:0.#}";
                if (!card.Name || card.Name.text.Length == 0) return $"card {i} has no name";
                if (!card.Description || card.Description.text.Length == 0) return $"card {i} has no description";
                if (i > 0)
                {
                    RectTransform previous = (RectTransform)panel.Buttons[i - 1].Button.transform;
                    if (rect.anchoredPosition.x < previous.anchoredPosition.x + previous.rect.width)
                        return $"card {i} at {rect.anchoredPosition} overlaps card {i - 1} at {previous.anchoredPosition}";
                    if (Mathf.Abs(rect.anchoredPosition.y - previous.anchoredPosition.y) > SizeTolerance)
                        return $"card {i} at {rect.anchoredPosition} is not in a row with card {i - 1} at {previous.anchoredPosition}";
                }
            }
            return OnScreen(panel);
        }

        // Each slot the canvas's size on the flat panel, with the game's frame gone, in rows of the mod's columns, all
        // within the grid's area so none run into the details under it.
        private static string GridAsCanvas(PickerPanel panel)
        {
            if (!panel.Alive) return "the panel closed";
            if (!panel.MainPanel.Find("Juice/" + PickerPanel.BackdropName)) return "the panel has no backdrop";
            int columns = Mathf.Min(panel.Buttons.Count, PickerPanel.GridColumns);
            Rect area = panel.Panel.buttonContainer.rect;
            for (int i = 0; i < panel.Buttons.Count; i++)
            {
                RectTransform rect = (RectTransform)panel.Buttons[i].Button.transform;
                Vector2 first = ((RectTransform)panel.Buttons[i - i % columns].Button.transform).anchoredPosition;
                if (Mathf.Abs(rect.anchoredPosition.y - first.y) > SizeTolerance)
                    return $"slot {i} at {rect.anchoredPosition} is not in the row of slot {i - i % columns} at {first}, with {columns} columns";
                Vector2 low = rect.anchoredPosition - Vector2.Scale(rect.pivot, rect.rect.size);
                if (low.y < -area.height - SizeTolerance)
                    return $"slot {i} reaches {-low.y:0.#} down, past the grid's {area.height:0.#}";
                if (Mathf.Abs(rect.rect.width - PickerPanel.SlotSize) > SizeTolerance ||
                    Mathf.Abs(rect.rect.height - PickerPanel.SlotSize) > SizeTolerance)
                    return $"slot {i} is {rect.rect.size}, not {PickerPanel.SlotSize:0.#} square";
                Image frame = panel.Buttons[i].Button.image;
                if (frame && frame.sprite) return $"slot {i} still has the game's frame {frame.sprite.name}";
            }
            return OnScreen(panel);
        }

        private static string OnScreen(PickerPanel panel)
        {
            Canvas canvas = panel.MainPanel.GetComponentInParent<Canvas>().rootCanvas;
            Camera camera = canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            Rect screen = camera ? camera.pixelRect : new Rect(0f, 0f, Screen.width, Screen.height);
            Vector3[] corners = new Vector3[4];
            panel.MainPanel.GetWorldCorners(corners);
            foreach (Vector3 corner in corners)
            {
                Vector2 point = RectTransformUtility.WorldToScreenPoint(camera, corner);
                if (!screen.Contains(point)) return $"the panel's corner {point} is off the screen {screen}";
            }
            return null;
        }

        // Both looks: with the mod's settings off the panel is the game's again, without the mod's parts.
        private static IEnumerator CaptureLooks(Harness h, PickerPanel panel)
        {
            yield return h.Screenshot("sevenui");
            PluginConfig settings = SevenUIPlugin.Settings;
            h.SetConfig(settings.FlatPanels, false);
            h.SetConfig(settings.BarlowFont, false);
            yield return h.Wait(SettleSeconds);
            Transform panelRoot = panel.Panel.transform;
            string left = string.Join(", ", panelRoot.GetComponentsInChildren<Transform>(true)
                .Where(part => part.name.StartsWith("SevenUI")).Select(part => part.name).Distinct());
            if (left.Length > 0) throw h.Fail($"the game's look still has the mod's {left}");
            yield return h.Screenshot("vanilla");
            h.SetConfig(settings.FlatPanels, true);
            h.SetConfig(settings.BarlowFont, true);
            yield return h.Until(() => OpenPanel(panel.Panel) is PickerPanel again && again.Styled, "the panel restyled again", OpenTimeout);
            yield return h.Wait(SettleSeconds);
        }
    }
}
