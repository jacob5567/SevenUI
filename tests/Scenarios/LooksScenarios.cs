using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ModTestRunner;
using RoR2;
using RoR2.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SevenUI.Tests
{
    // Screenshots of how parts of the HUD look now, for the design doc that settles a task's look before it is built
    // (Phase H in docs/ISSUES-PLAN.md). They check nothing; run them with --suite looks.
    internal static class LooksScenarios
    {
        private const float SettleSeconds = 0.6f;
        private const string MoonPrefab = "Prefabs/UI/HudModules/HudCountdownPanel";
        private const double CountdownSeconds = 125.0;
        private const int Stacks = 3;
        // A pale sky, as the moon's after Mithrix, and plain white.
        private static readonly Color Sky = new Color(0.86f, 0.9f, 0.95f, 1f);

        public static IEnumerable<Scenario> All()
        {
            yield return new Scenario(1, "looks_tier_colors", Stages.TitanicPlains, TierColors);
            yield return new Scenario(1, "looks_moon_countdown", Stages.TitanicPlains, MoonCountdown);
            yield return new Scenario(1, "looks_item_count_marks", Stages.TitanicPlains, ItemCountMarks);
            yield return new Scenario(1, "looks_buffs", Stages.TitanicPlains, Buffs);
            yield return new Scenario(1, "looks_objective_boxes", Stages.TitanicPlains, ObjectiveBoxes);
        }

        // One item of each tier, each shown at once in the three places that color it: the item bar's tooltip, the
        // scoreboard's docked panel (it stays on the last item pointed at) and the inspect panel. Logs the colors each
        // place's code reads, for comparison.
        private static IEnumerator TierColors(Harness h)
        {
            ItemDef[] items = ScoreboardInspectScenarios.TierSamples(h);
            yield return ScoreboardSetUp.Open(h);
            yield return h.Wait(SettleSeconds);
            HUD hud = SevenUIChecks.HudRoot(h).GetComponent<HUD>();
            foreach (ItemDef item in items)
            {
                PickupDef pickup = PickupCatalog.GetPickupDef(PickupCatalog.FindPickupIndex(item.itemIndex));
                Color32 catalog = ColorCatalog.GetColor(ItemTierCatalog.GetItemTierDef(item.tier).colorIndex);
                h.Log($"{item.name} ({item.tier}): tooltip strip (PickupDef.baseColor) #{Hex(pickup.baseColor)}, " +
                      $"pane strip (GameColors.OfTier) #{Hex(GameColors.OfTier(item.tier, Palette.HeaderText))}, " +
                      $"catalog #{Hex(catalog)}, canvas shade (GameColors.ToCanvas) #{Hex(GameColors.ToCanvas(catalog))}, " +
                      $"dark #{Hex(pickup.darkColor)}");

                ItemIcon onBoard = SevenUIChecks.HudRoot(h).GetComponentsInChildren<ScoreboardRow>()
                    .Where(row => row.Strip && row.Strip.master == Harness.LocalBody.master)
                    .SelectMany(row => row.Strip.itemInventoryDisplay.GetComponentsInChildren<ItemIcon>())
                    .FirstOrDefault(icon => icon.itemIndex == item.itemIndex);
                if (!onBoard) throw h.Fail($"no {item.name} in the viewer's scoreboard row");
                PointerEventData boardPointer = null;
                ScoreboardScenarios.PointAt(h, onBoard.rectTransform, item.name + " on the scoreboard", made => boardPointer = made);
                yield return h.Wait(SettleSeconds);
                ExecuteEvents.ExecuteHierarchy(onBoard.gameObject, boardPointer, ExecuteEvents.pointerExitHandler);

                yield return ScoreboardInspectScenarios.Inspect(h, item);

                ItemIcon inBar = hud.itemInventoryDisplay.GetComponentsInChildren<ItemIcon>()
                    .FirstOrDefault(icon => icon.itemIndex == item.itemIndex);
                if (!inBar) throw h.Fail($"no {item.name} in the item bar");
                PointerEventData barPointer = null;
                ScoreboardScenarios.PointAt(h, inBar.rectTransform, item.name + " in the item bar", made => barPointer = made);
                yield return h.Wait(SettleSeconds);
                yield return h.Screenshot("tier_" + item.tier.ToString().ToLowerInvariant());
                ExecuteEvents.ExecuteHierarchy(inBar.gameObject, barPointer, ExecuteEvents.pointerExitHandler);
            }
        }

        // The moon's escape countdown from its prefab, over the plain stage, a pale sky and white: a screen-filling image
        // drawn under the HUD stands in for the sky after Mithrix.
        private static IEnumerator MoonCountdown(Harness h)
        {
            GameObject prefab = Resources.Load<GameObject>(MoonPrefab);
            if (!prefab) throw h.Skip($"{MoonPrefab} loaded as nothing");
            Transform hud = SevenUIChecks.HudRoot(h);
            Transform cluster = hud.Find(HudPaths.TopCenterCluster);
            if (!cluster) throw h.Skip("the HUD has no top cluster");
            GameObject panel = Object.Instantiate(prefab, cluster);
            h.OnCleanup(() => Object.Destroy(panel));
            panel.GetComponentInChildren<TimerText>(true).seconds = CountdownSeconds;
            yield return h.Wait(1.5f);
            yield return h.Screenshot("moon_stage");

            Canvas hudCanvas = hud.GetComponentInParent<Canvas>().rootCanvas;
            GameObject backdrop = new GameObject("SevenUITestSky", typeof(RectTransform));
            h.OnCleanup(() => Object.Destroy(backdrop));
            // The UI camera draws only its own layer.
            backdrop.layer = hudCanvas.gameObject.layer;
            Canvas canvas = backdrop.AddComponent<Canvas>();
            canvas.renderMode = hudCanvas.renderMode;
            canvas.worldCamera = hudCanvas.worldCamera;
            canvas.planeDistance = hudCanvas.planeDistance + 1f;
            h.Log($"HUD canvas: {hudCanvas.renderMode}, camera {(hudCanvas.worldCamera ? hudCanvas.worldCamera.name : "none")}, " +
                  $"layer {hudCanvas.gameObject.layer}, order {hudCanvas.sortingOrder}, plane {hudCanvas.planeDistance}");
            canvas.sortingOrder = hudCanvas.sortingOrder - 1;
            Image sky = new GameObject("Sky", typeof(RectTransform)).AddComponent<Image>();
            sky.gameObject.layer = backdrop.layer;
            sky.transform.SetParent(backdrop.transform, false);
            RectTransform rect = sky.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            sky.raycastTarget = false;
            sky.color = Sky;
            yield return h.Wait(SettleSeconds);
            yield return h.Screenshot("moon_pale_sky");
            sky.color = Color.white;
            yield return h.Wait(SettleSeconds);
            yield return h.Screenshot("moon_white");

            // The options for the design doc, each over the pale sky and white: A a darker see-through panel, B the
            // tooltips' panel fully opaque (the game blends in linear light, so even 92% comes out mid-grey over white),
            // C the same see-through panel with a soft dark shadow under the texts.
            Transform holder = panel.GetComponentsInChildren<Transform>(true).First(each => each.name == CountdownPanel.HolderName);
            Image background = holder.Find("Background").GetComponent<Image>();
            Image strip = holder.Find("HeaderStrip").GetComponent<Image>();
            TMP_Text label = holder.Find("Label").GetComponent<TMP_Text>();
            TMP_Text time = holder.Find("Time").GetComponent<TMP_Text>();
            Color backgroundWas = background.color;
            Color stripWas = strip.color;
            (string name, System.Action apply)[] options =
            {
                ("a_darker", () => background.color = new Color(0f, 0f, 0f, 0.75f)),
                ("b_solid", () =>
                {
                    background.color = new Color(Palette.TooltipPanel.r, Palette.TooltipPanel.g, Palette.TooltipPanel.b, 1f);
                    strip.color = CanvasBlend.Over(Palette.TooltipHeader, Palette.TooltipPanel);
                }),
                ("c_shadow", () =>
                {
                    Outline(label);
                    Outline(time);
                }),
            };
            foreach ((string name, System.Action apply) in options)
            {
                background.color = backgroundWas;
                strip.color = stripWas;
                apply();
                sky.color = Sky;
                yield return h.Wait(SettleSeconds);
                yield return h.Screenshot("moon_" + name + "_pale_sky");
                sky.color = Color.white;
                yield return h.Wait(SettleSeconds);
                yield return h.Screenshot("moon_" + name + "_white");
            }
        }

        // A soft dark shadow under a text, on its own copy of its material.
        private static void Outline(TMP_Text text)
        {
            Material material = new Material(text.fontSharedMaterial);
            material.EnableKeyword(ShaderUtilities.Keyword_Underlay);
            material.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(0.02f, 0.03f, 0.05f, 0.85f));
            material.SetFloat(ShaderUtilities.ID_UnderlayDilate, 0.5f);
            material.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0.6f);
            text.fontSharedMaterial = material;
        }

        // Permanent, temporary and Ethereal (rented) stacks side by side in the item bar and the scoreboard, in both looks.
        private static IEnumerator ItemCountMarks(Harness h)
        {
            Inventory inventory = Harness.LocalBody.inventory;
            ItemIndex permanent = RoR2Content.Items.Syringe.itemIndex;
            ItemIndex temporary = RoR2Content.Items.Hoof.itemIndex;
            ItemIndex rented = RoR2Content.Items.Mushroom.itemIndex;
            ItemIndex oneTemporary = RoR2Content.Items.Crowbar.itemIndex;
            ItemIndex oneRented = RoR2Content.Items.Tooth.itemIndex;
            inventory.GiveItemPermanent(permanent, Stacks);
            inventory.GiveItemTemp(temporary, Stacks);
            inventory.GiveItemRented(rented, Stacks);
            inventory.GiveItemTemp(oneTemporary, 1f);
            inventory.GiveItemRented(oneRented, 1);
            h.OnCleanup(() =>
            {
                if (!inventory) return;
                inventory.RemoveItemPermanent(permanent, Stacks);
                inventory.RemoveItemTemp(temporary, Stacks);
                inventory.RemoveItemRented(rented, Stacks);
                inventory.RemoveItemTemp(oneTemporary, 1f);
                inventory.RemoveItemRented(oneRented, 1);
            });
            yield return h.Wait(1f);
            yield return h.Screenshot("item_bar.sevenui");
            yield return CountOptions(h);
            yield return ScoreboardSetUp.Open(h);
            yield return h.Wait(SettleSeconds);
            yield return h.Screenshot("scoreboard.sevenui");
            h.SetConfig(SevenUIPlugin.Settings.FlatPanels, false);
            h.OnCleanup(() => h.SetConfig(SevenUIPlugin.Settings.FlatPanels, true));
            yield return h.Wait(1f);
            yield return h.Screenshot("scoreboard.vanilla");
            LocalUser user = LocalUserManager.GetFirstLocalUser();
            HUD.SetScoreboardActive(user, false, EScoreboardDeactivateReturnContext.Game);
            yield return h.Wait(SettleSeconds);
            yield return h.Screenshot("item_bar.vanilla");
        }

        // The item bar's slots rearranged for the design doc's options, each shot, then put back: A the count at the top
        // right, as the game has it; B the marks at the top left; C no marks, the tier strip turned cyan (temporary,
        // drained to the time left) or gold (Ethereal).
        private static IEnumerator CountOptions(Harness h)
        {
            HUD hud = SevenUIChecks.HudRoot(h).GetComponent<HUD>();
            ItemIcon[] icons = hud.itemInventoryDisplay.GetComponentsInChildren<ItemIcon>();
            List<System.Action> undo = new List<System.Action>();
            void Keep(RectTransform rect)
            {
                Vector2 min = rect.anchorMin, max = rect.anchorMax, pivot = rect.pivot, position = rect.anchoredPosition;
                undo.Add(() =>
                {
                    rect.anchorMin = min;
                    rect.anchorMax = max;
                    rect.pivot = pivot;
                    rect.anchoredPosition = position;
                });
            }
            void Undo()
            {
                foreach (System.Action step in undo) step();
                undo.Clear();
            }

            foreach (ItemIcon icon in icons)
            {
                TMP_Text count = icon.stackText;
                TextAlignmentOptions alignment = count.alignment;
                Vector4 margin = count.margin;
                undo.Add(() =>
                {
                    count.alignment = alignment;
                    count.margin = margin;
                });
                count.alignment = TextAlignmentOptions.TopRight;
                count.margin = new Vector4(0f, 1f, margin.z, 0f);
            }
            yield return h.Wait(SettleSeconds);
            yield return h.Screenshot("count_a_top_right");
            Undo();

            foreach (ItemIcon icon in icons)
            {
                foreach (Image mark in new[] { icon.durationImage, icon.rentalImage })
                {
                    if (!mark) continue;
                    // The mark may sit in a holder of its own that the game places; move the holder under the icon.
                    Transform top = mark.transform;
                    while (top.parent && top.parent != icon.transform)
                    {
                        top = top.parent;
                    }
                    h.Log($"mark {mark.name}: moved {top.name}, path {Path(mark.transform, icon.transform)}");
                    RectTransform rect = (RectTransform)top;
                    Keep(rect);
                    Vector2 size = rect.rect.size;
                    rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
                    rect.anchoredPosition = new Vector2(3f, -3f);
                    rect.sizeDelta = size;
                }
            }
            yield return h.Wait(SettleSeconds);
            yield return h.Screenshot("count_b_marks_top_left");
            Undo();

            Color cyan = new Color(0.12f, 0.94f, 0.94f, 1f);
            Color gold = new Color(1f, 0.84f, 0.54f, 1f);
            foreach (ItemIcon icon in icons)
            {
                Transform stripObject = icon.transform.Find("SevenUIItemSlot/TierStrip");
                Image tierStrip = stripObject ? stripObject.GetComponent<Image>() : null;
                bool temporary = icon.durationImage && icon.durationImage.isActiveAndEnabled;
                bool rented = icon.rentalImage && icon.rentalImage.isActiveAndEnabled;
                if (!tierStrip || !temporary && !rented) continue;
                foreach (Image mark in new[] { icon.durationImage, icon.rentalImage })
                {
                    if (!mark) continue;
                    Color was = mark.color;
                    mark.color = Color.clear;
                    undo.Add(() => mark.color = was);
                }
                Color stripWas = tierStrip.color;
                RectTransform rect = tierStrip.rectTransform;
                Vector2 maxWas = rect.anchorMax;
                undo.Add(() =>
                {
                    tierStrip.color = stripWas;
                    rect.anchorMax = maxWas;
                });
                tierStrip.color = temporary ? cyan : gold;
                if (temporary)
                {
                    rect.anchorMax = new Vector2(0.6f, rect.anchorMax.y);
                }
            }
            yield return h.Wait(SettleSeconds);
            yield return h.Screenshot("count_c_strip_marks");
            Undo();
        }

        // A row of buffs with and without stacks, in both looks.
        private static IEnumerator Buffs(Harness h)
        {
            CharacterBody body = Harness.LocalBody;
            BuffDef[] buffs =
            {
                RoR2Content.Buffs.Warbanner, RoR2Content.Buffs.CloakSpeed, RoR2Content.Buffs.FullCrit,
                RoR2Content.Buffs.ElephantArmorBoost, RoR2Content.Buffs.Slow50, RoR2Content.Buffs.BeetleJuice,
            };
            foreach (BuffDef buff in buffs)
            {
                body.AddBuff(buff);
                BuffDef given = buff;
                h.OnCleanup(() =>
                {
                    if (body) body.SetBuffCount(given.buffIndex, 0);
                });
            }
            BuffDef stacking = RoR2Content.Buffs.AttackSpeedOnCrit;
            for (int i = 0; i < 4; i++)
            {
                body.AddBuff(stacking);
            }
            h.OnCleanup(() =>
            {
                if (body) body.SetBuffCount(stacking.buffIndex, 0);
            });
            yield return h.Wait(1f);
            yield return h.Screenshot("sevenui");
            h.SetConfig(SevenUIPlugin.Settings.FlatPanels, false);
            h.OnCleanup(() => h.SetConfig(SevenUIPlugin.Settings.FlatPanels, true));
            yield return h.Wait(1f);
            yield return h.Screenshot("vanilla");
        }

        // An open, a done and a failed objective, in both looks.
        private static IEnumerator ObjectiveBoxes(Harness h)
        {
            (List<TestObjectiveSource> sources, System.Action hide) = TestObjectives.Show(h, new[]
            {
                ("Find and activate the Teleporter", typeof(TestObjectiveTracker), 0f),
                ("Defeat the boss", typeof(TestObjectiveTracker), 0f),
                ("Pick up the item", typeof(TestObjectiveTracker), 0f),
            });
            h.OnCleanup(hide);
            ObjectivePanelController controller = null;
            yield return h.Until(() => (controller = SevenUIChecks.HudRoot(h).GetComponentInChildren<ObjectivePanelController>()) &&
                                       sources.All(source => controller.objectiveTrackers.Any(shown => shown.sourceDescriptor.source == source)),
                "the test objectives on the panel");
            yield return TestObjectives.SkipStripAnimations(h, sources);
            ObjectivePanelController.ObjectiveTracker done =
                controller.objectiveTrackers.First(shown => shown.sourceDescriptor.source == sources[1]);
            ObjectivePanelController.ObjectiveTracker failed =
                controller.objectiveTrackers.First(shown => shown.sourceDescriptor.source == sources[2]);
            done.SetState(ObjectivePanelController.ObjectiveTracker.ObjectiveState.Completed);
            failed.SetState(ObjectivePanelController.ObjectiveTracker.ObjectiveState.Failure);
            yield return h.Wait(1f);
            yield return h.Screenshot("sevenui");
            h.SetConfig(SevenUIPlugin.Settings.FlatPanels, false);
            h.OnCleanup(() => h.SetConfig(SevenUIPlugin.Settings.FlatPanels, true));
            yield return h.Wait(1f);
            done.SetState(ObjectivePanelController.ObjectiveTracker.ObjectiveState.Completed);
            failed.SetState(ObjectivePanelController.ObjectiveTracker.ObjectiveState.Failure);
            yield return h.Wait(SettleSeconds);
            yield return h.Screenshot("vanilla");
        }

        private static string Path(Transform from, Transform to)
        {
            string path = from.name;
            for (Transform each = from.parent; each && each != to; each = each.parent)
            {
                path = each.name + "/" + path;
            }
            return path;
        }

        private static string Hex(Color color) => ColorUtility.ToHtmlStringRGB(color);
    }
}
