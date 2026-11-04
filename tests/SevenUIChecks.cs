using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Bootstrap;
using ModTestRunner;
using RoR2;
using RoR2.Items;
using RoR2.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SevenUI.Tests
{
    // The SevenUI checks, as Harness extension methods so scenarios call them like the runner's own helpers. They read
    // the HUD's texts directly; the mod itself is read only through SevenUIController.Describe.
    internal static class SevenUIChecks
    {
        // How long LookingGlass may take to add its stats panel; it builds it once the body's stats are known.
        private const float LookingGlassPanelTimeout = 10f;
        private const float ColorTolerance = 0.01f;
        // A canvas pixel, in HUD units: how far a key's letters may sit from its face's middle, and the rows' actions
        // from one x.
        private const float CenterTolerance = 1f;
        // A share of a skill slot's size, for comparing slots' sizes and edges.
        private const float SlotTolerance = 0.02f;

        public static SevenUIController Controller =>
            HUD.readOnlyInstanceList.Where(hud => hud).Select(hud => hud.GetComponent<SevenUIController>())
                .FirstOrDefault(controller => controller);

        private static IEnumerable<TMP_Text> ActiveTexts(Transform root)
        {
            return root.GetComponentsInChildren<TMP_Text>(false).Where(text => text.font);
        }

        private static bool IsGameFont(TMP_Text text) => text.font.name.StartsWith(GameNames.GameFontPrefix);

        private static bool IsBarlow(TMP_Text text) => text.font.name.StartsWith(Texts.BarlowFontPrefix);

        // The texts still in the game's font, by path, for failure messages.
        private static string GameFontTexts(Transform root)
        {
            List<string> paths = ActiveTexts(root).Where(IsGameFont).Select(text => PathOf(text.transform, root)).ToList();
            return paths.Count == 0 ? "none" : $"{paths.Count}: {string.Join(", ", paths.Take(8))}";
        }

        private static string PathOf(Transform transform, Transform root)
        {
            List<string> names = new List<string>();
            for (Transform t = transform; t && t != root; t = t.parent)
            {
                names.Add(t.name);
            }
            names.Reverse();
            return string.Join("/", names);
        }

        internal static Transform HudRoot(Harness h)
        {
            SevenUIController controller = Controller;
            if (!controller) throw h.Fail("no SevenUI controller on the HUD");
            return controller.transform.root;
        }

        // Waits until every active text under the root (the HUD, or a panel in it) is drawn in Barlow, and at least
        // one is.
        public static IEnumerator ExpectAllBarlow(this Harness h, Transform root, string what)
        {
            yield return h.Until(
                () => ActiveTexts(root).Any(IsBarlow) && !ActiveTexts(root).Any(IsGameFont),
                () => $"{what} in Barlow (still in the game's font: {GameFontTexts(root)}) {h.Diagnosis()}");
        }

        public static IEnumerator ExpectHudBarlow(this Harness h)
        {
            yield return h.ExpectAllBarlow(HudRoot(h), "all HUD text");
        }

        // Waits until no active HUD text is in Barlow and the game's font is back.
        public static IEnumerator ExpectHudGameFont(this Harness h)
        {
            Transform root = HudRoot(h);
            yield return h.Until(
                () => ActiveTexts(root).Any(IsGameFont) && !ActiveTexts(root).Any(IsBarlow),
                () => $"all HUD text back in the game's font ({ActiveTexts(root).Count(IsBarlow)} still in Barlow) {h.Diagnosis()}");
        }

        public static Transform FindUnderHud(this Harness h, string name)
        {
            return HudRoot(h).GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);
        }

        // Waits for LookingGlass's stats panel, skipping the scenario if LookingGlass is absent or doesn't add it.
        public static IEnumerator WaitForLookingGlassPanel(this Harness h, Action<Transform> found)
        {
            if (!Chainloader.PluginInfos.ContainsKey(GameNames.LookingGlassGUID)) throw h.Skip("LookingGlass isn't installed");

            Transform panel = null;
            float deadline = Time.time + LookingGlassPanelTimeout;
            while (!panel && Time.time < deadline)
            {
                panel = h.FindUnderHud(GameNames.LookingGlassPanel);
                yield return null;
            }
            if (!panel) throw h.Skip("LookingGlass's stats panel didn't appear (its stats display may be off or detached)");
            found(panel);
        }

        internal static bool Near(Color a, Color b) =>
            Mathf.Abs(a.r - b.r) < ColorTolerance && Mathf.Abs(a.g - b.g) < ColorTolerance &&
            Mathf.Abs(a.b - b.b) < ColorTolerance && Mathf.Abs(a.a - b.a) < ColorTolerance;

        private static bool IsFlat(Image image) => image && image.enabled && !image.sprite && Near(image.color, Palette.Panel);

        private static bool IsHeader(TMP_Text label) => label && (label.fontStyle & FontStyles.UpperCase) != 0;

        private static TMP_Text Title(Transform panel)
        {
            Transform title = panel.Find(GameNames.ObjectiveTitle);
            return title ? title.GetComponent<TMP_Text>() : null;
        }

        private static string Look(Transform panel)
        {
            if (!panel) return "no objective panel";
            Image image = panel.GetComponent<Image>();
            TMP_Text title = Title(panel);
            return $"image sprite={(image && image.sprite ? image.sprite.name : "none")} color={(image ? image.color.ToString() : "-")}, " +
                   $"header strip={(bool)panel.Find(Texts.HeaderStripName)}, title style={(title ? title.fontStyle.ToString() : "-")}";
        }

        public static IEnumerator ExpectObjectivePanelFlat(this Harness h)
        {
            Transform panel = null;
            yield return h.Until(
                () =>
                {
                    panel = h.FindUnderHud(GameNames.ObjectivePanel);
                    return panel && IsFlat(panel.GetComponent<Image>()) && panel.Find(Texts.HeaderStripName) &&
                           IsHeader(Title(panel));
                },
                () => $"a flat objective panel with a header strip ({Look(panel)}) {h.Diagnosis()}");
        }

        public static IEnumerator ExpectObjectivePanelVanilla(this Harness h)
        {
            Transform panel = null;
            yield return h.Until(
                () =>
                {
                    panel = h.FindUnderHud(GameNames.ObjectivePanel);
                    Image image = panel ? panel.GetComponent<Image>() : null;
                    return image && image.sprite && !panel.Find(Texts.HeaderStripName) && !IsHeader(Title(panel));
                },
                () => $"the game's objective panel back ({Look(panel)}) {h.Diagnosis()}");
        }

        // A flat panel with no header strip and a title in its normal case and spacing.
        // LookingGlass's stats under a header strip, read into two columns, with LookingGlass's own text hidden and the
        // panel as tall as the columns.
        public static IEnumerator ExpectStatsInColumns(this Harness h, Transform panel)
        {
            string look = null;
            yield return h.Until(
                () =>
                {
                    StatsGrid grid = panel ? panel.GetComponentInChildren<StatsGrid>() : null;
                    TMP_Text source = Title(panel);
                    if (!grid || !source) return false;
                    RectTransform title = source.rectTransform;
                    look = $"entries {grid.ShownEntries}, title '{grid.Title.text}', names '{grid.Names[0].text.Length}/" +
                           $"{grid.Names[1].text.Length}' chars, source alpha {source.alpha}, height {title.rect.height:0.#} " +
                           $"(columns {grid.Height.preferredHeight:0.#})";
                    return IsFlat(panel.GetComponent<Image>()) && panel.Find(Texts.HeaderStripName) && grid.ShownEntries >= 2 &&
                           grid.Title.text.Length > 0 && grid.Names[0].text.Length > 0 && grid.Names[1].text.Length > 0 &&
                           source.alpha == 0f && Mathf.Abs(title.rect.height - grid.Height.preferredHeight) < 1f;
                },
                () => $"LookingGlass's stats in two columns under a header strip ({look}) {h.Diagnosis()}");
        }

        private static DifficultyBarController DifficultyBar(Harness h) =>
            HudRoot(h).GetComponentsInChildren<DifficultyBarController>(true).FirstOrDefault();

        // Whether the timer sits left of the stage and level, by their centers on screen.
        private static bool TimerFirst(Transform panel)
        {
            Transform timer = panel.Find(GameNames.TimerPanel);
            Transform stage = panel.Find(GameNames.StageAndLevel);
            return timer && stage && CenterX(timer) < CenterX(stage);
        }

        private static float CenterX(Transform transform)
        {
            Vector3[] corners = new Vector3[4];
            ((RectTransform)transform).GetWorldCorners(corners);
            return (corners[0].x + corners[2].x) / 2f;
        }

        private static string RunInfoLook(DifficultyBarController bar)
        {
            if (!bar) return "no difficulty bar";
            Transform panel = bar.transform.parent;
            Transform background = panel.Find(Texts.RunInfoBackgroundName);
            Image timerBox = panel.Find(GameNames.TimerPanel)?.GetComponent<Image>();
            CanvasGroup segments = bar.transform.Find(GameNames.DifficultySegments)?.GetComponent<CanvasGroup>();
            DifficultyReadout readout = bar.GetComponentInChildren<DifficultyReadout>();
            return $"background={(bool)background}, timer first={TimerFirst(panel)}, timer box image={(timerBox ? timerBox.enabled.ToString() : "-")}, " +
                   $"segments alpha={(segments ? segments.alpha.ToString() : "no group")}, " +
                   $"readout={(readout ? $"'{readout.ShownName}' {readout.ShownProgress:P0}" : "none")}";
        }

        // One flat panel behind the run info, the timer first in its row, the game's segments hidden, and the mod's bar showing one of the game's
        // difficulty names.
        public static IEnumerator ExpectRunInfoFlat(this Harness h)
        {
            DifficultyBarController bar = null;
            yield return h.Until(
                () =>
                {
                    bar = DifficultyBar(h);
                    if (!bar) return false;
                    Transform panel = bar.transform.parent;
                    Transform background = panel.Find(Texts.RunInfoBackgroundName);
                    Image timerBox = panel.Find(GameNames.TimerPanel)?.GetComponent<Image>();
                    CanvasGroup segments = bar.transform.Find(GameNames.DifficultySegments)?.GetComponent<CanvasGroup>();
                    DifficultyReadout readout = bar.GetComponentInChildren<DifficultyReadout>();
                    return background && IsFlat(background.GetComponent<Image>()) && TimerFirst(panel) && timerBox &&
                           !timerBox.enabled &&
                           segments && segments.alpha == 0f && readout && !string.IsNullOrEmpty(readout.ShownName) &&
                           bar.labels.Any(label => label && label.text == readout.ShownName);
                },
                () => $"a flat run info panel with the mod's difficulty bar ({RunInfoLook(bar)}) {h.Diagnosis()}");
        }

        public static IEnumerator ExpectRunInfoVanilla(this Harness h)
        {
            DifficultyBarController bar = null;
            yield return h.Until(
                () =>
                {
                    bar = DifficultyBar(h);
                    if (!bar) return false;
                    Transform panel = bar.transform.parent;
                    Image timerBox = panel.Find(GameNames.TimerPanel)?.GetComponent<Image>();
                    return !panel.Find(Texts.RunInfoBackgroundName) && !TimerFirst(panel) && timerBox && timerBox.enabled &&
                           !bar.transform.Find(GameNames.DifficultySegments).GetComponent<CanvasGroup>() &&
                           !bar.GetComponentInChildren<DifficultyReadout>();
                },
                () => $"the game's run info back ({RunInfoLook(bar)}) {h.Diagnosis()}");
        }

        public static IEnumerator ExpectTimerCondensed(this Harness h)
        {
            TMP_Text timer = null;
            yield return h.Until(
                () =>
                {
                    TimerText timerText = HudRoot(h).GetComponentsInChildren<TimerText>(true).FirstOrDefault();
                    timer = timerText ? timerText.GetComponent<TMP_Text>() : null;
                    return timer && timer.font && timer.font.name.StartsWith(Texts.CondensedFontPrefix);
                },
                () => $"the run timer in Barlow Condensed (font {(timer && timer.font ? timer.font.name : "none")}) {h.Diagnosis()}");
        }

        private static HealthBar PlayerHealthBar(Harness h) =>
            HudRoot(h).Find(GameNames.PlayerHealthBar)?.GetComponent<HealthBar>();

        // The barrier is a strip along the top that stays within the bar, not the game's frame drawn larger than it.
        private static bool UsesFlatStyle(HealthBar bar) =>
            bar._style && bar._style.name.EndsWith(Texts.FlatStyleSuffix) &&
            (!bar._allyStyle || bar._allyStyle.name.EndsWith(Texts.FlatStyleSuffix)) &&
            bar._style.barrierBarStyle.sprite && bar._style.barrierBarStyle.sprite.name == Texts.BarrierStripName &&
            bar._style.barrierBarStyle.sizeDelta <= 0f;

        private static string HealthLook(HealthBar bar)
        {
            if (!bar) return "no player health bar";
            Transform digits = bar.transform.Find(GameNames.HealthSpriteDigits);
            return $"style={(bar._style ? bar._style.name : "none")}, " +
                   $"barrier sprite={(bar._style && bar._style.barrierBarStyle.sprite ? bar._style.barrierBarStyle.sprite.name : "-")} " +
                   $"size delta={(bar._style ? bar._style.barrierBarStyle.sizeDelta.ToString() : "-")}, sprite digits field={(bool)bar.spriteAsNumberManager}, " +
                   $"digits active={(digits ? digits.gameObject.activeSelf.ToString() : "-")}, " +
                   $"text={(bar.currentHealthText ? bar.currentHealthText.text : "-")}/{(bar.fullHealthText ? bar.fullHealthText.text : "-")}, " +
                   $"readout={(bar.GetComponentInChildren<PlayerHealthReadout>() is PlayerHealthReadout readout ? $"'{readout.Health.text}' '{readout.Regen.text}'" : "none")}, " +
                   $"health={(bar.source ? $"{Mathf.Ceil(bar.source.combinedHealth)}/{Mathf.Ceil(bar.source.fullHealth)}" : "-")}";
        }

        // Flat bars, and the health numbers as text that matches the body's health.
        public static IEnumerator ExpectPlayerBarsFlat(this Harness h)
        {
            HealthBar bar = null;
            yield return h.Until(
                () =>
                {
                    bar = PlayerHealthBar(h);
                    if (!bar || !bar.source || !bar.currentHealthText) return false;
                    Transform digits = bar.transform.Find(GameNames.HealthSpriteDigits);
                    PlayerHealthReadout readout = bar.GetComponentInChildren<PlayerHealthReadout>();
                    string current = Mathf.Ceil(bar.source.combinedHealth).ToString("N0");
                    string full = Mathf.Ceil(bar.source.fullHealth).ToString("N0");
                    return UsesFlatStyle(bar) && !bar.spriteAsNumberManager && digits && !digits.gameObject.activeSelf &&
                           readout && readout.Health.isActiveAndEnabled && readout.Health.text.Contains($">{current}<") &&
                           readout.Health.text.EndsWith($"/ {full}</color>") && readout.Regen.text.EndsWith("/s");
                },
                () => $"flat player bars with the health as text above them ({HealthLook(bar)}) {h.Diagnosis()}");
        }

        public static IEnumerator ExpectPlayerBarsVanilla(this Harness h)
        {
            HealthBar bar = null;
            yield return h.Until(
                () =>
                {
                    bar = PlayerHealthBar(h);
                    if (!bar) return false;
                    Transform digits = bar.transform.Find(GameNames.HealthSpriteDigits);
                    return bar._style && !UsesFlatStyle(bar) && bar.spriteAsNumberManager && digits &&
                           digits.gameObject.activeSelf;
                },
                () => $"the game's player bars back ({HealthLook(bar)}) {h.Diagnosis()}");
        }

        public static IEnumerator ExpectLevelCondensed(this Harness h)
        {
            TMP_Text level = null;
            yield return h.Until(
                () =>
                {
                    LevelText levelText = HudRoot(h).GetComponentsInChildren<LevelText>(true).FirstOrDefault();
                    level = levelText ? levelText.targetText : null;
                    return level && level.font && level.font.name.StartsWith(Texts.CondensedFontPrefix);
                },
                () => $"the level in Barlow Condensed (font {(level && level.font ? level.font.name : "none")}) {h.Diagnosis()}");
        }

        private static List<SkillIcon> SkillSlots(Harness h) =>
            HudRoot(h).GetComponentsInChildren<SkillIcon>(true).Where(skill => skill.cooldownRemapPanel).ToList();

        private static bool IsSlotFlat(SkillIcon skill)
        {
            Image slot = skill.GetComponent<Image>();
            CanvasGroup overlay = skill.cooldownRemapPanel.GetComponent<CanvasGroup>();
            return slot && !slot.sprite && Near(slot.color, Palette.Trough) && overlay && overlay.alpha == 0f &&
                   skill.GetComponentInChildren<CooldownCover>(true);
        }

        private static bool IsSlotVanilla(SkillIcon skill)
        {
            Image slot = skill.GetComponent<Image>();
            return slot && slot.sprite && !skill.cooldownRemapPanel.GetComponent<CanvasGroup>() &&
                   !skill.GetComponentInChildren<CooldownCover>(true);
        }

        private static string SlotsLook(List<SkillIcon> skills)
        {
            return skills.Count == 0
                ? "no skill slots"
                : string.Join(", ", skills.Select(skill =>
                {
                    Image slot = skill.GetComponent<Image>();
                    return $"{skill.name}: sprite={(slot && slot.sprite ? slot.sprite.name : "none")} " +
                           $"overlay group={(bool)skill.cooldownRemapPanel.GetComponent<CanvasGroup>()} " +
                           $"cover={(bool)skill.GetComponentInChildren<CooldownCover>(true)} " +
                           $"cooldown font={(skill.cooldownText && skill.cooldownText.font ? skill.cooldownText.font.name : "-")}";
                }));
        }

        // Every skill slot flat, with the mod's cooldown cover in place of the radial overlay, and its cooldown in
        // Barlow Condensed.
        public static IEnumerator ExpectSkillSlotsFlat(this Harness h)
        {
            List<SkillIcon> skills = new List<SkillIcon>();
            yield return h.Until(
                () =>
                {
                    skills = SkillSlots(h);
                    return skills.Count > 0 && skills.All(IsSlotFlat) && skills.All(skill =>
                        skill.cooldownText && skill.cooldownText.font &&
                        skill.cooldownText.font.name.StartsWith(Texts.CondensedFontPrefix));
                },
                () => $"flat skill slots with cooldown covers ({SlotsLook(skills)}) {h.Diagnosis()}");
        }

        private static bool IsKeyPlain(TMP_Text label)
        {
            Image plate = label.transform.parent ? label.transform.parent.GetComponent<Image>() : null;
            return (label.fontStyle & FontStyles.UpperCase) != 0 && Near(label.color, Palette.HeaderText) &&
                   !(plate && plate.enabled);
        }

        // The inventory and sprint buttons' and the key item's key labels, in the skills' style: grey capitals with
        // no plate.
        public static IEnumerator ExpectExtraKeysPlain(this Harness h)
        {
            List<TMP_Text> labels = new List<TMP_Text>();
            yield return h.Until(
                () =>
                {
                    Transform buttons = HudRoot(h).Find(GameNames.ExtraButtons);
                    labels = buttons
                        ? buttons.GetComponentsInChildren<InputBindingDisplayController>()
                            .Select(key => key.GetComponent<TMP_Text>()).Where(label => label).ToList()
                        : new List<TMP_Text>();
                    return labels.Count > 0 && labels.All(IsKeyPlain);
                },
                () => $"plain key labels by the inventory and sprint buttons (" +
                      (labels.Count == 0
                          ? "none found"
                          : string.Join(", ", labels.Select(label =>
                              $"'{label.text}': style={label.fontStyle} color={label.color} plain={IsKeyPlain(label)}"))) +
                      $") {h.Diagnosis()}");
        }

        // The scoreboard and sprint buttons' icons, and the descend icon that stands in for sprint while flying a
        // remote-operated drone, drawn as the canvas's flat strokes in the text white.
        public static IEnumerator ExpectExtraIconsFlat(this Harness h)
        {
            string look = "no buttons";
            yield return h.Until(
                () =>
                {
                    Transform buttons = HudRoot(h).Find(GameNames.ExtraButtons);
                    if (!buttons) return false;
                    Image scoreboard = buttons.Find(GameNames.ScoreboardIcon)?.GetComponent<Image>();
                    Image sprint = buttons.Find(GameNames.SprintIcon)?.GetComponent<Image>();
                    Image descend = buttons.Find(GameNames.DescendIcon)?.GetComponent<Image>();
                    look = $"scoreboard {IconLook(scoreboard)}, sprint {IconLook(sprint)}, descend {IconLook(descend)}";
                    return IsIcon(scoreboard, FlatIcons.Scoreboard) && IsIcon(sprint, FlatIcons.Sprint) &&
                           IsIcon(descend, FlatIcons.Descend);
                },
                () => $"flat scoreboard, sprint and descend icons ({look}) {h.Diagnosis()}");
        }

        // The tutorial popups that the game hasn't destroyed (it does once their tip is used), each a dark panel with
        // an accent strip, white text and an arrow in the panel's color. They are styled whether or not they show.
        public static IEnumerator ExpectTutorialPopupsFlat(this Harness h)
        {
            HUDTutorialController tutorials = HudRoot(h).GetComponentInChildren<HUDTutorialController>(true);
            if (!tutorials) throw h.Skip("the HUD has no tutorial popups");
            GameObject[] popups =
                { tutorials.equipmentTutorialObject, tutorials.sprintTutorialObject, tutorials.remoteOpTutorialObject };
            string look = "";
            yield return h.Until(
                () =>
                {
                    List<GameObject> present = popups.Where(popup => popup).ToList();
                    look = string.Join("; ", present.Select(PopupLook));
                    return present.Count > 0 && present.All(IsPopupFlat);
                },
                () => $"flat tutorial popups ({look}) {h.Diagnosis()}");
        }

        private static bool IsPopupFlat(GameObject popup)
        {
            Image panel = popup.GetComponent<Image>();
            Image strip = popup.transform.Find(TutorialPopups.StripName)?.GetComponent<Image>();
            Image arrow = popup.transform.Find("TutorialArrow")?.GetComponent<Image>();
            TMP_Text text = popup.GetComponentInChildren<TMP_Text>(true);
            return panel && Near(panel.color, Palette.TooltipPanel) && strip && Near(strip.color, Palette.Accent) &&
                   (!arrow || Near(arrow.color, Palette.TooltipPanel)) && text && Near(text.color, Palette.Text);
        }

        private static string PopupLook(GameObject popup)
        {
            Image panel = popup.GetComponent<Image>();
            TMP_Text text = popup.GetComponentInChildren<TMP_Text>(true);
            return $"{popup.name}: panel {(panel ? panel.color.ToString() : "none")}, " +
                   $"strip {(popup.transform.Find(TutorialPopups.StripName) ? "yes" : "no")}, " +
                   $"text {(text ? text.color.ToString() : "none")}";
        }

        private static bool IsIcon(Image icon, Sprite sprite) => icon && icon.sprite == sprite && Near(icon.color, Palette.Text);

        private static string IconLook(Image icon) =>
            icon ? $"sprite {(icon.sprite ? icon.sprite.name : "none")} color {icon.color}" : "missing";

        // A rect's bottom-left and top-right corners in world space.
        private static (Vector3 min, Vector3 max) WorldBox(RectTransform rect) =>
            (rect.TransformPoint(rect.rect.min), rect.TransformPoint(rect.rect.max));

        private static string BoxText(RectTransform rect)
        {
            (Vector3 min, Vector3 max) = WorldBox(rect);
            return $"{rect.name} {min.x:F1},{min.y:F1} to {max.x:F1},{max.y:F1}";
        }

        // Drawn at the skill's size, with the same top and bottom.
        private static bool MatchesSkill(RectTransform display, RectTransform skill)
        {
            (Vector3 min, Vector3 max) = WorldBox(display);
            (Vector3 skillMin, Vector3 skillMax) = WorldBox(skill);
            float tolerance = (skillMax.y - skillMin.y) * SlotTolerance;
            return Mathf.Abs((max.x - min.x) - (skillMax.x - skillMin.x)) < tolerance &&
                   Mathf.Abs(min.y - skillMin.y) < tolerance && Mathf.Abs(max.y - skillMax.y) < tolerance;
        }

        // A key label's height on screen and the height of its plate's center, in world units.
        private static (float size, float center) KeyLook(InputBindingDisplayController key)
        {
            TMP_Text label = key.GetComponent<TMP_Text>();
            RectTransform plate = (RectTransform)key.transform.parent;
            return (label ? label.fontSize * label.transform.lossyScale.y : 0f, plate.TransformPoint(plate.rect.center).y);
        }

        private static bool KeyMatchesSkill(GameObject displayRoot, RectTransform skill)
        {
            InputBindingDisplayController key = displayRoot.GetComponentInChildren<InputBindingDisplayController>(true);
            InputBindingDisplayController skillKey = skill.GetComponentInChildren<InputBindingDisplayController>(true);
            if (!key || !skillKey) return false;
            (float size, float center) = KeyLook(key);
            (float skillSize, float skillCenter) = KeyLook(skillKey);
            (Vector3 skillMin, Vector3 skillMax) = WorldBox(skill);
            float tolerance = (skillMax.y - skillMin.y) * SlotTolerance;
            return Mathf.Abs(size - skillSize) < tolerance && Mathf.Abs(center - skillCenter) < tolerance;
        }

        // The equipment and key item slots drawn at a skill slot's size, level with the skills, with their keys at the
        // skills' size and height.
        public static IEnumerator ExpectSlotsSkillSized(this Harness h)
        {
            HUD hud = null;
            List<RectTransform> displays = new List<RectTransform>();
            yield return h.Until(
                () =>
                {
                    hud = HudRoot(h) ? Controller.GetComponent<HUD>() : null;
                    if (!hud || hud.skillIcons.Length == 0 || !hud.skillIcons[0]) return false;
                    displays = hud.equipmentIcons.Where(slot => slot && !slot.displayAlternateEquipment && slot.displayRoot &&
                                                                slot.displayRoot.transform.Find(GameNames.SlotBackground))
                        .Select(slot => (RectTransform)slot.displayRoot.transform).ToList();
                    if (hud.keyItemIcon && hud.keyItemIcon.displayRoot)
                    {
                        displays.Add((RectTransform)hud.keyItemIcon.displayRoot.transform);
                    }
                    RectTransform skill = (RectTransform)hud.skillIcons[0].transform;
                    return displays.Count > 0 &&
                           displays.All(display => MatchesSkill(display, skill) && KeyMatchesSkill(display.gameObject, skill));
                },
                () => $"the equipment and key item slots and their keys at a skill's size (" +
                      (hud && hud.skillIcons.Length > 0 && hud.skillIcons[0]
                          ? $"skill {BoxText((RectTransform)hud.skillIcons[0].transform)}; " +
                            string.Join("; ", displays.Where(display => display).Select(display =>
                                $"{display.parent.name} {BoxText(display)} key matches={KeyMatchesSkill(display.gameObject, (RectTransform)hud.skillIcons[0].transform)}"))
                          : "no skill slots") +
                      $") {h.Diagnosis()}");
        }

        public static IEnumerator ExpectSkillSlotsVanilla(this Harness h)
        {
            List<SkillIcon> skills = new List<SkillIcon>();
            yield return h.Until(
                () =>
                {
                    skills = SkillSlots(h);
                    return skills.Count > 0 && skills.All(IsSlotVanilla);
                },
                () => $"the game's skill slots back ({SlotsLook(skills)}) {h.Diagnosis()}");
        }

        private static List<Transform> TiltedClusters(Harness h) =>
            GameNames.TiltedClusters.Select(path => HudRoot(h).Find(path)).Where(cluster => cluster).ToList();

        private static string TiltLook(List<Transform> clusters) =>
            clusters.Count == 0
                ? "no clusters"
                : string.Join(", ", clusters.Select(cluster => $"{cluster.name} {cluster.localEulerAngles.y:0.#}"));

        public static IEnumerator ExpectHudLevel(this Harness h)
        {
            List<Transform> clusters = new List<Transform>();
            yield return h.Until(
                () =>
                {
                    clusters = TiltedClusters(h);
                    return clusters.Count == GameNames.TiltedClusters.Length &&
                           clusters.All(cluster => cluster.localRotation == Quaternion.identity);
                },
                () => $"the corner clusters facing the screen (y angles: {TiltLook(clusters)}) {h.Diagnosis()}");
        }

        public static IEnumerator ExpectHudTilted(this Harness h)
        {
            List<Transform> clusters = new List<Transform>();
            yield return h.Until(
                () =>
                {
                    clusters = TiltedClusters(h);
                    return clusters.Count == GameNames.TiltedClusters.Length &&
                           clusters.All(cluster => cluster.localRotation != Quaternion.identity);
                },
                () => $"the game's tilt back on the corner clusters (y angles: {TiltLook(clusters)}) {h.Diagnosis()}");
        }

        private static HUDBossHealthBarController BossBar(Harness h) =>
            HudRoot(h).GetComponentsInChildren<HUDBossHealthBarController>(true).FirstOrDefault();

        // The panel that holds the boss bar's fills, and the wider backdrop around it.
        private static Image BossTrough(HUDBossHealthBarController boss) =>
            boss && boss.fillRectImage ? boss.fillRectImage.transform.parent.GetComponent<Image>() : null;

        private static Image BossBackdrop(HUDBossHealthBarController boss)
        {
            Image trough = BossTrough(boss);
            return trough && trough.transform.parent ? trough.transform.parent.GetComponent<Image>() : null;
        }

        // The trough fits the fills: it is drawn on their panel, and the wider backdrop is off.
        private static bool BossTroughFitsFills(HUDBossHealthBarController boss)
        {
            Image trough = BossTrough(boss);
            Image backdrop = BossBackdrop(boss);
            return trough && trough.enabled && !trough.sprite && Near(trough.color, Palette.Trough) && backdrop && !backdrop.enabled;
        }

        private static List<RawImage> CurrencyStrips(Harness h)
        {
            Transform cluster = HudRoot(h).Find(GameNames.MoneyCluster);
            if (!cluster) return new List<RawImage>();
            return cluster.GetComponentsInChildren<MoneyText>(true)
                .Select(currency => currency.transform.Find(GameNames.CurrencyBackground)?.GetComponent<RawImage>())
                .Where(strip => strip).ToList();
        }

        private static string TopLook(Harness h)
        {
            Transform cluster = HudRoot(h).Find(GameNames.MoneyCluster);
            Image frame = cluster ? cluster.GetComponent<Image>() : null;
            HUDBossHealthBarController boss = BossBar(h);
            return $"money frame on={(frame ? frame.enabled.ToString() : "-")}, " +
                   $"currency strips={string.Join("/", CurrencyStrips(h).Select(strip => strip.texture ? strip.texture.name : "flat"))}, " +
                   $"boss fill={(boss && boss.fillRectImage && boss.fillRectImage.sprite ? boss.fillRectImage.sprite.name : "-")}, " +
                   $"boss trough on={(BossTrough(boss) ? BossTrough(boss).enabled.ToString() : "-")}, " +
                   $"boss backdrop on={(BossBackdrop(boss) ? BossBackdrop(boss).enabled.ToString() : "-")}";
        }

        public static IEnumerator ExpectTopFlat(this Harness h)
        {
            yield return h.Until(
                () =>
                {
                    Transform cluster = HudRoot(h).Find(GameNames.MoneyCluster);
                    Image frame = cluster ? cluster.GetComponent<Image>() : null;
                    List<RawImage> strips = CurrencyStrips(h);
                    HUDBossHealthBarController boss = BossBar(h);
                    return frame && !frame.enabled && strips.Count > 0 &&
                           strips.All(strip => !strip.texture && Near(strip.color, Palette.Panel)) &&
                           boss && boss.fillRectImage.sprite && boss.fillRectImage.sprite.name == Texts.FlatSpriteName &&
                           BossTroughFitsFills(boss);
                },
                () => $"flat currency panels and boss bar ({TopLook(h)}) {h.Diagnosis()}");
        }

        public static IEnumerator ExpectTopVanilla(this Harness h)
        {
            yield return h.Until(
                () =>
                {
                    Transform cluster = HudRoot(h).Find(GameNames.MoneyCluster);
                    Image frame = cluster ? cluster.GetComponent<Image>() : null;
                    HUDBossHealthBarController boss = BossBar(h);
                    return frame && frame.enabled && CurrencyStrips(h).All(strip => strip.texture) &&
                           boss && boss.fillRectImage.sprite && boss.fillRectImage.sprite.name != Texts.FlatSpriteName &&
                           BossBackdrop(boss) && BossBackdrop(boss).enabled && BossTrough(boss) && !BossTrough(boss).enabled;
                },
                () => $"the game's currency panels and boss bar back ({TopLook(h)}) {h.Diagnosis()}");
        }

        // The boss's name is inactive until a boss appears, and wakes up then; it must already be in Barlow and not
        // take the language's font when it wakes.
        public static IEnumerator ExpectSleepingTextBarlow(this Harness h)
        {
            TMP_Text name = null;
            yield return h.Until(
                () =>
                {
                    HUDBossHealthBarController boss = BossBar(h);
                    name = boss ? boss.bossNameLabel : null;
                    return name && name.font && name.font.name.StartsWith(Texts.BarlowFontPrefix) &&
                           name is HGTextMeshProUGUI languageText && !languageText.useLanguageDefaultFont;
                },
                () => $"the boss name in Barlow, set to keep it on waking (font {(name && name.font ? name.font.name : "none")}, " +
                      $"wakes to language font={(name as HGTextMeshProUGUI)?.useLanguageDefaultFont}) {h.Diagnosis()}");
        }

        private static List<ItemIcon> BarItemIcons(Harness h) =>
            HudRoot(h).GetComponentsInChildren<ItemInventoryDisplay>(true).Take(1)
                .SelectMany(display => display.GetComponentsInChildren<ItemIcon>(false)).ToList();

        private static string ItemsLook(List<ItemIcon> icons) =>
            icons.Count == 0
                ? "no item icons"
                : string.Join(", ", icons.Take(6).Select(icon =>
                {
                    ItemSlot slot = icon.GetComponentInChildren<ItemSlot>(true);
                    return $"{icon.itemIndex} x{icon.itemCount}: slot={(bool)slot} " +
                           $"text='{(icon.stackText && icon.stackText.isActiveAndEnabled ? icon.stackText.text : "-")}'";
                }));

        // Every item icon on a slot, with its stack count as text: the count for stacks, nothing for single items.
        public static IEnumerator ExpectItemIconsFlat(this Harness h)
        {
            List<ItemIcon> icons = new List<ItemIcon>();
            yield return h.Until(
                () =>
                {
                    icons = BarItemIcons(h);
                    return icons.Count > 0 && icons.All(icon =>
                        icon.GetComponentInChildren<ItemSlot>(true) && icon.stackText && icon.stackText.isActiveAndEnabled &&
                        icon.stackText.text == (icon.itemCount > 1 ? icon.itemCount.ToString() : ""));
                },
                () => $"item icons on slots with text counts ({ItemsLook(icons)}) {h.Diagnosis()}");
        }

        public static IEnumerator ExpectItemIconsVanilla(this Harness h)
        {
            List<ItemIcon> icons = new List<ItemIcon>();
            yield return h.Until(
                () =>
                {
                    icons = BarItemIcons(h);
                    return icons.Count > 0 && icons.All(icon =>
                        !icon.GetComponentInChildren<ItemSlot>(true) && (!icon.stackText || !icon.stackText.gameObject.activeSelf));
                },
                () => $"the game's item icons back ({ItemsLook(icons)}) {h.Diagnosis()}");
        }

        private static ContextManager Prompt(Harness h) =>
            HudRoot(h).GetComponentsInChildren<ContextManager>(true).FirstOrDefault();

        private static RawImage PromptStrip(ContextManager prompt) =>
            prompt && prompt.contextDisplay ? prompt.contextDisplay.GetComponent<RawImage>() : null;

        private static RectTransform PromptPanel(ContextManager prompt) =>
            prompt ? prompt.transform.Find(Texts.PromptPanelName) as RectTransform : null;

        private static string PromptLook(ContextManager prompt)
        {
            if (!prompt) return "no prompt";
            RawImage strip = PromptStrip(prompt);
            RectTransform panel = PromptPanel(prompt);
            return $"strip={(strip ? $"enabled={strip.enabled} texture={(strip.texture ? strip.texture.name : "none")}" : "none")}, " +
                   $"panel={(panel ? $"active={panel.gameObject.activeSelf} size={panel.rect.size}" : "none")}, " +
                   $"prompt width={((RectTransform)prompt.transform).rect.width:0}, " +
                   $"text='{(prompt.descriptionTMP ? prompt.descriptionTMP.text : "-")}'";
        }

        // The game's strips are hidden for the mod's one panel, which only shows with a prompt.
        public static IEnumerator ExpectPromptFlat(this Harness h)
        {
            ContextManager prompt = null;
            yield return h.Until(
                () =>
                {
                    prompt = Prompt(h);
                    RawImage strip = PromptStrip(prompt);
                    RectTransform panel = PromptPanel(prompt);
                    return strip && !strip.enabled && panel && panel.GetComponent<Image>() &&
                           Near(panel.GetComponent<Image>().color, Palette.Panel);
                },
                () => $"the interaction prompt's strips hidden for a flat panel ({PromptLook(prompt)}) {h.Diagnosis()}");
        }

        // With a chest's prompt showing: the game's text hidden, and the panel, far narrower than the game's strips,
        // holding the action and its cost without brackets, its right edge at the run info's.
        public static IEnumerator ExpectPromptAsCanvas(this Harness h)
        {
            ContextManager prompt = null;
            string look = null;
            yield return h.Until(
                () =>
                {
                    prompt = Prompt(h);
                    RectTransform panel = PromptPanel(prompt);
                    if (!panel || !panel.gameObject.activeSelf || !prompt.descriptionTMP) return false;
                    TMP_Text action = panel.Find("Action")?.GetComponent<TMP_Text>();
                    TMP_Text cost = panel.Find("Cost")?.GetComponent<TMP_Text>();
                    RectTransform runInfo = HudRoot(h).GetComponentsInChildren<RectTransform>(true)
                        .FirstOrDefault(rect => rect.name == Texts.RunInfoBackgroundName);
                    if (!action || !cost || !runInfo) return false;
                    float promptWidth = ((RectTransform)prompt.transform).rect.width;
                    Vector3[] corners = new Vector3[4];
                    panel.GetWorldCorners(corners);
                    float panelRight = runInfo.InverseTransformPoint(corners[2]).x;
                    look = $"action '{action.text}', cost '{cost.text}', game alpha {prompt.descriptionTMP.alpha}, " +
                           $"right edge {panelRight - runInfo.rect.xMax:0.##} from the run info's";
                    return prompt.descriptionTMP.alpha == 0f && action.text.Length > 0 && cost.text.Length > 0 &&
                           !cost.text.Contains("(") && !action.text.Contains("(") && panel.rect.width < promptWidth * 0.8f &&
                           Covers(panel, action) && Covers(panel, cost) && Mathf.Abs(panelRight - runInfo.rect.xMax) < 1f;
                },
                () => $"the prompt laid out as on the canvas ({look}; {PromptLook(prompt)}) {h.Diagnosis()}");
        }

        // The shown rows' keys and actions, top to bottom.
        private static List<(RectTransform key, TMP_Text action)> PromptRows(RectTransform panel)
        {
            List<RectTransform> keys = new List<RectTransform>();
            List<TMP_Text> actions = new List<TMP_Text>();
            foreach (Transform child in panel)
            {
                if (!child.gameObject.activeSelf) continue;
                if (child.name == Texts.PromptKeyName) keys.Add((RectTransform)child);
                if (child.name == Texts.PromptActionName) actions.Add(child.GetComponent<TMP_Text>());
            }
            return keys.Zip(actions, (key, action) => (key, action)).ToList();
        }

        // With a prompt showing: each key a keycap on the slot grey, its label in Barlow without the HUD text's drop
        // shadow and with its letters centered on the keycap's face, and the actions starting at one x, however wide
        // their keys.
        public static IEnumerator ExpectPromptKeys(this Harness h)
        {
            ContextManager prompt = null;
            string look = "no rows";
            yield return h.Until(
                () =>
                {
                    prompt = Prompt(h);
                    RectTransform panel = PromptPanel(prompt);
                    if (!panel || !panel.gameObject.activeSelf) return false;
                    List<(RectTransform key, TMP_Text action)> rows = PromptRows(panel);
                    if (rows.Count == 0) return false;
                    List<string> parts = new List<string>();
                    bool good = true;
                    float actionX = rows[0].action.rectTransform.anchoredPosition.x;
                    foreach ((RectTransform key, TMP_Text action) in rows)
                    {
                        TMP_Text glyph = key.Find(Texts.PromptKeyGlyphName)?.GetComponent<TMP_Text>();
                        Image face = key.Find(Texts.PromptKeyFacePath)?.GetComponent<Image>();
                        if (!glyph || !face) return false;
                        Bounds letters = glyph.textBounds;
                        float lettersMiddle = face.rectTransform.InverseTransformPoint(glyph.transform.TransformPoint(letters.center)).y;
                        float offCenter = lettersMiddle - face.rectTransform.rect.center.y;
                        string font = glyph.font ? glyph.font.name : "none";
                        Material material = glyph.fontSharedMaterial;
                        bool shadow = material && material.IsKeywordEnabled(ShaderUtilities.Keyword_Underlay);
                        parts.Add($"'{glyph.text}' font {font} shadow {shadow} face {face.color} {offCenter:0.##} off center, action '{action.text}' at x {action.rectTransform.anchoredPosition.x:0.#}");
                        good &= Near(face.color, Palette.ItemSlot) && font.Contains("Barlow") && !shadow && Mathf.Abs(offCenter) < CenterTolerance &&
                                Mathf.Abs(action.rectTransform.anchoredPosition.x - actionX) < CenterTolerance;
                    }
                    look = string.Join("; ", parts);
                    return good;
                },
                () => $"the prompt's keys as keycaps ({look}; {PromptLook(prompt)}) {h.Diagnosis()}");
        }

        // With a prompt for something bought with lunar coins: the flat coin between the action and the number, in the
        // coins' blue and centered on the row, and the game's coin sprite gone from the number.
        public static IEnumerator ExpectPromptCoin(this Harness h)
        {
            ContextManager prompt = null;
            string look = "no coin";
            yield return h.Until(
                () =>
                {
                    prompt = Prompt(h);
                    RectTransform panel = PromptPanel(prompt);
                    if (!panel || !panel.gameObject.activeSelf) return false;
                    Image coin = panel.Find(Texts.PromptCoinName)?.GetComponent<Image>();
                    TMP_Text action = panel.Find(Texts.PromptActionName)?.GetComponent<TMP_Text>();
                    TMP_Text cost = panel.Find(Texts.PromptCostName)?.GetComponent<TMP_Text>();
                    RectTransform key = panel.Find(Texts.PromptKeyName) as RectTransform;
                    if (!coin || !action || !cost || !key) return false;
                    Rect coinRect = coin.rectTransform.rect;
                    float coinLeft = coin.rectTransform.anchoredPosition.x;
                    float coinMiddle = coin.rectTransform.anchoredPosition.y - coinRect.height / 2f;
                    float rowMiddle = key.anchoredPosition.y - key.rect.height / 2f;
                    float actionRight = action.rectTransform.anchoredPosition.x + action.rectTransform.rect.width;
                    float costLeft = cost.rectTransform.anchoredPosition.x;
                    look = $"coin active {coin.gameObject.activeSelf}, color {coin.color}, x {coinLeft:0.#}-{coinLeft + coinRect.width:0.#}, " +
                           $"{coinMiddle - rowMiddle:0.##} off the row's middle, action ends {actionRight:0.#}, cost '{cost.text}' at {costLeft:0.#}";
                    return coin.gameObject.activeSelf && coin.sprite == FlatSprite.LunarCoin && Near(coin.color, Palette.LunarCoin) &&
                           !cost.text.Contains("<sprite") && coinLeft > actionRight && coinLeft + coinRect.width <= costLeft &&
                           Mathf.Abs(coinMiddle - rowMiddle) < CenterTolerance;
                },
                () => $"the prompt's cost with the flat lunar coin ({look}; {PromptLook(prompt)}) {h.Diagnosis()}");
        }

        // With a prompt showing: the strip along the panel's top in the color named.
        public static IEnumerator ExpectPromptStrip(this Harness h, Color expected, string what)
        {
            ContextManager prompt = null;
            string look = "no strip";
            yield return h.Until(
                () =>
                {
                    prompt = Prompt(h);
                    RectTransform panel = PromptPanel(prompt);
                    Image strip = panel ? panel.Find(Texts.PromptStripName)?.GetComponent<Image>() : null;
                    if (!strip || !panel.gameObject.activeSelf) return false;
                    TMP_Text action = panel.Find(Texts.PromptActionName)?.GetComponent<TMP_Text>();
                    look = $"strip {strip.color} for '{(action ? action.text : "-")}'";
                    return Near(strip.color, expected);
                },
                () => $"the prompt's strip in {what}'s color {expected} ({look}; {PromptLook(prompt)}) {h.Diagnosis()}");
        }

        // Whether the panel's rectangle holds the text's drawn characters.
        private static bool Covers(RectTransform panel, TMP_Text text)
        {
            Bounds bounds = text.textBounds;
            Vector3 min = panel.InverseTransformPoint(text.transform.TransformPoint(bounds.min));
            Vector3 max = panel.InverseTransformPoint(text.transform.TransformPoint(bounds.max));
            Rect rect = panel.rect;
            const float Slack = 1f;
            return Mathf.Min(min.x, max.x) >= rect.xMin - Slack && Mathf.Max(min.x, max.x) <= rect.xMax + Slack &&
                   Mathf.Min(min.y, max.y) >= rect.yMin - Slack && Mathf.Max(min.y, max.y) <= rect.yMax + Slack;
        }

        public static IEnumerator ExpectPromptVanilla(this Harness h)
        {
            ContextManager prompt = null;
            yield return h.Until(
                () =>
                {
                    prompt = Prompt(h);
                    RawImage strip = PromptStrip(prompt);
                    return strip && strip.enabled && strip.texture && !PromptPanel(prompt);
                },
                () => $"the game's interaction prompt back ({PromptLook(prompt)}) {h.Diagnosis()}");
        }

        // The scoreboard is closed during a run, but its board is already in the HUD, so it is checked closed.
        private static Image ScoreboardBoard(Harness h)
        {
            ScoreboardController board = HudRoot(h).GetComponentsInChildren<ScoreboardController>(true).FirstOrDefault();
            return board ? board.transform.Find(GameNames.ScoreboardBoard)?.GetComponent<Image>() : null;
        }

        private static Image ChatInput(Harness h)
        {
            ChatBox chat = HudRoot(h).GetComponentsInChildren<ChatBox>(true).FirstOrDefault();
            return chat ? chat.transform.Find(GameNames.ChatInputField)?.GetComponent<Image>() : null;
        }

        private static string OverlaysLook(Image board, Image input) =>
            $"board sprite={(board ? (board.sprite ? board.sprite.name : "none") : "-")}, " +
            $"chat input sprite={(input ? (input.sprite ? input.sprite.name : "none") : "-")} " +
            $"color={(input ? input.color.ToString() : "-")}, {ChatInputLook(input)}";

        private static string ChatInputLook(Image input)
        {
            Image outline = input ? input.transform.Find(ChatStyler.OutlineName)?.GetComponent<Image>() : null;
            Image edge = input ? input.transform.Find(ChatStyler.EdgeName)?.GetComponent<Image>() : null;
            return $"field {(input ? (input.enabled ? "shown" : "hidden") : "-")}, " +
                   $"outline {(outline ? (outline.enabled ? "on " : "off ") + outline.color : "missing")}, " +
                   $"edge {(edge ? (edge.enabled ? "on " : "off ") + edge.color + " " + edge.rectTransform.rect.width.ToString("0.#") + " wide" : "missing")}";
        }

        // While typing: the field is shown with its grey outline and the accent strip down its left edge, 2 canvas pixels
        // wide; once the input closes, the outline and the strip go with the field.
        public static IEnumerator ExpectChatInputOutlined(this Harness h, bool typing)
        {
            Image input = null;
            yield return h.Until(
                () =>
                {
                    input = ChatInput(h);
                    Image outline = input ? input.transform.Find(ChatStyler.OutlineName)?.GetComponent<Image>() : null;
                    Image edge = input ? input.transform.Find(ChatStyler.EdgeName)?.GetComponent<Image>() : null;
                    if (!outline || !edge) return false;
                    bool shown = input.enabled && input.gameObject.activeInHierarchy;
                    if (!typing) return !shown || (!outline.enabled && !edge.enabled);
                    return shown && outline.enabled && edge.enabled && Near(outline.color, Palette.Divider) && outline.sprite &&
                           Near(edge.color, Palette.Accent) &&
                           Mathf.Abs(edge.rectTransform.rect.width - 2f * HudUnits.PerCanvasPixel) < ChatEdgeTolerance;
                },
                () => $"the chat input {(typing ? "outlined while typing" : "hidden with its outline once closed")} " +
                      $"({ChatInputLook(input)}) {h.Diagnosis()}");
        }

        private const float ChatEdgeTolerance = 0.1f;

        // Long enough for a restyle that adds objects on every pass to add them dozens of times: each one changes the
        // HUD's object count, which restyles the HUD on the next frame. A stray buff or notification adds far fewer.
        private const float SettleSeconds = 2f;
        private const int SettleTolerance = 20;

        // Once styled, the HUD stops growing on its own, and the chat's field has one outline and one edge.
        public static IEnumerator ExpectHudSettles(this Harness h)
        {
            Transform root = HudRoot(h);
            int before = root.hierarchyCount;
            yield return h.Wait(SettleSeconds);
            int after = root.hierarchyCount;
            if (after - before > SettleTolerance)
            {
                throw h.Fail($"the HUD grew from {before} to {after} objects in {SettleSeconds} s while idle {h.Diagnosis()}");
            }
            Image input = ChatInput(h);
            if (!input) throw h.Fail($"the HUD has no chat input field {h.Diagnosis()}");
            int outlines = 0;
            int edges = 0;
            foreach (Transform child in input.transform)
            {
                outlines += child.name == ChatStyler.OutlineName ? 1 : 0;
                edges += child.name == ChatStyler.EdgeName ? 1 : 0;
            }
            if (outlines != 1 || edges != 1)
            {
                throw h.Fail($"the chat input has {outlines} outlines and {edges} edges, not one each {h.Diagnosis()}");
            }
        }

        public static IEnumerator ExpectOverlaysFlat(this Harness h)
        {
            Image board = null;
            Image input = null;
            yield return h.Until(
                () =>
                {
                    board = ScoreboardBoard(h);
                    input = ChatInput(h);
                    return board && !board.sprite && Near(board.color, Palette.ScoreboardPanel) &&
                           input && !input.sprite && Near(input.color, Palette.GhostButton) &&
                           input.transform.Find(ChatStyler.OutlineName) && input.transform.Find(ChatStyler.EdgeName);
                },
                () => $"a flat scoreboard and chat input ({OverlaysLook(board, input)}) {h.Diagnosis()}");
        }

        public static IEnumerator ExpectOverlaysVanilla(this Harness h)
        {
            Image board = null;
            Image input = null;
            yield return h.Until(
                () =>
                {
                    board = ScoreboardBoard(h);
                    input = ChatInput(h);
                    return board && board.sprite && input && input.sprite &&
                           !input.transform.Find(ChatStyler.OutlineName) && !input.transform.Find(ChatStyler.EdgeName);
                },
                () => $"the game's scoreboard and chat input back ({OverlaysLook(board, input)}) {h.Diagnosis()}");
        }

        private static Texture DamageNumberSheet()
        {
            DamageNumberManager manager = DamageNumberManager.instance;
            ParticleSystemRenderer renderer = manager ? manager.GetComponent<ParticleSystemRenderer>() : null;
            return renderer && renderer.sharedMaterial ? renderer.sharedMaterial.mainTexture : null;
        }

        // A particle's width over its height, in characters: the game's are square, the mod's narrowed.
        private const float WidthTolerance = 0.01f;

        private static float ExpectedWidth(bool monospace) =>
            GameNames.DamageNumberCharacters * (monospace ? DamageNumberFont.CellAspect : 1f);

        // Each particle system drawn with the damage numbers' material, live, pooled or a loaded prefab (the lenses'
        // execute effect's among them), with its width in characters.
        private static List<(string name, float width)> DamageNumberWidths()
        {
            DamageNumberManager manager = DamageNumberManager.instance;
            ParticleSystemRenderer numbers = manager ? manager.GetComponent<ParticleSystemRenderer>() : null;
            if (!numbers || !numbers.sharedMaterial) return new List<(string, float)>();
            return Resources.FindObjectsOfTypeAll<ParticleSystemRenderer>()
                .Where(renderer => renderer.sharedMaterial == numbers.sharedMaterial)
                .Select(renderer => renderer.GetComponent<ParticleSystem>())
                .Where(particles => particles)
                .Select(particles =>
                {
                    ParticleSystem.MainModule main = particles.main;
                    return (particles.name, main.startSizeXMultiplier / main.startSizeYMultiplier);
                })
                .ToList();
        }

        // The mod's sheet, on particles narrowed for its narrower characters, or the game's sheet on the game's
        // particles: every system drawn with the material, the manager's and the lenses' fake numbers' alike.
        public static IEnumerator ExpectDamageNumbersMonospace(this Harness h, bool monospace)
        {
            Texture sheet = null;
            List<(string name, float width)> widths = new List<(string, float)>();
            float expected = ExpectedWidth(monospace);
            yield return h.Until(
                () =>
                {
                    sheet = DamageNumberSheet();
                    widths = DamageNumberWidths();
                    return sheet && (sheet.name == Texts.DamageNumberSheetName) == monospace &&
                           widths.Any(shown => shown.name == GameNames.LensesFakeNumbers) &&
                           widths.All(shown => Mathf.Abs(shown.width - expected) < WidthTolerance);
                },
                () => $"the damage numbers {(monospace ? "in the mod's monospace sheet, on narrowed particles" : "back on the game's sheet and particles")} " +
                      $"{expected:0.00} characters wide (sheet {(sheet ? sheet.name : "none")}, particles " +
                      $"{string.Join(", ", widths.Select(shown => $"{shown.name} {shown.width:0.00}"))}) {h.Diagnosis()}");
        }

        // Lost Seer's Lenses' execute effect's burst of fake crit numbers as it plays, each emitted as wide as the
        // real numbers.
        public static IEnumerator ExpectFakeNumbersFit(this Harness h, bool monospace)
        {
            float expected = ExpectedWidth(monospace);
            string seen = "none playing";
            yield return h.Until(
                () =>
                {
                    ParticleSystem burst = Resources.FindObjectsOfTypeAll<ParticleSystem>().FirstOrDefault(particles =>
                        particles.name == GameNames.LensesFakeNumbers && particles.gameObject.scene.IsValid() &&
                        particles.particleCount > 0);
                    if (!burst) return false;
                    ParticleSystem.Particle[] shown = new ParticleSystem.Particle[burst.particleCount];
                    int count = burst.GetParticles(shown);
                    List<float> widths = shown.Take(count)
                        .Select(particle => particle.startSize3D.x / particle.startSize3D.y).ToList();
                    seen = $"{count} numbers {string.Join(", ", widths.Distinct().Select(width => width.ToString("0.00")))} characters wide";
                    return widths.All(width => Mathf.Abs(width - expected) < WidthTolerance);
                },
                () => $"Lost Seer's Lenses' fake crit numbers {expected:0.00} characters wide ({seen}) {h.Diagnosis()}");
        }

        // How long to let the HUD switch between looks before the next screenshot.
        internal const float LookSwitchSeconds = 0.5f;

        // Two screenshots of the screen as it is: in SevenUI's look, then with all of SevenUI's settings off. The
        // settings come back on afterwards, and at cleanup as the runner puts config back.
        // Gives the player the N'tormat (Hallowed Concepts' key item) and a few of its pages, which the key item slot
        // shows as a row of page icons with a selector, and takes them away at cleanup. Server methods; the runner is
        // the host. Skips without the expansion.
        public static IEnumerator GiveNtormat(this Harness h)
        {
            KeyItemDef book = DLC4Content.KeyItems.CursedBook;
            if (!book || book.keyItemIndex == KeyItemIndex.None || Run.instance.IsKeyItemExpansionLocked(book.keyItemIndex))
            {
                throw h.Skip("the N'tormat needs Hallowed Concepts");
            }
            Inventory inventory = Harness.LocalBody.inventory;
            CursedBookSpellDef[] pages = NtormatPages.Where(page => page && page.itemDef).ToArray();
            h.OnCleanup(() =>
            {
                if (!inventory) return;
                foreach (CursedBookSpellDef page in pages)
                {
                    inventory.RemoveItemPermanent(page.itemDef, inventory.GetItemCountPermanent(page.itemDef));
                }
                inventory.RemoveKeyItem(book.keyItemIndex);
            });
            inventory.GiveKeyItem(book.keyItemIndex);
            foreach (CursedBookSpellDef page in pages)
            {
                inventory.GiveItemPermanent(page.itemDef);
            }
            CursedBookExtraDisplay display = null;
            yield return h.Until(
                () =>
                {
                    display = NtormatDisplay(h);
                    return display && NtormatPageIcons(display).Count == pages.Length;
                },
                () => $"the N'tormat's {pages.Length} pages beside the key item " +
                      $"({(display ? NtormatPageIcons(display).Count + " shown" : "no page display")}) {h.Diagnosis()}");
        }

        private static CursedBookSpellDef[] NtormatPages =>
            new[] { DLC4Content.CursedBookSpells.Clarity, DLC4Content.CursedBookSpells.Purify, DLC4Content.CursedBookSpells.WarpGate };

        private static CursedBookExtraDisplay NtormatDisplay(Harness h)
        {
            HUD hud = HudRoot(h) ? Controller.GetComponent<HUD>() : null;
            return hud && hud.keyItemIcon && hud.keyItemIcon.extraDisplayHolder
                ? hud.keyItemIcon.extraDisplayHolder.GetComponentInChildren<CursedBookExtraDisplay>()
                : null;
        }

        private static List<CursedBookSpellSlotIcon> NtormatPageIcons(CursedBookExtraDisplay display) =>
            display.spellSlotHolder
                ? display.spellSlotHolder.GetComponentsInChildren<CursedBookSpellSlotIcon>().ToList()
                : new List<CursedBookSpellSlotIcon>();

        // Where each page icon sits against the key item slot as drawn: in the slot's units, from the drawn box's
        // bottom-left corner, over the box's width.
        private static List<Vector2> NtormatLayout(Harness h)
        {
            CursedBookExtraDisplay display = NtormatDisplay(h);
            KeyItemIcon slot = display ? Controller.GetComponent<HUD>().keyItemIcon : null;
            if (!slot || !slot.displayRoot) return new List<Vector2>();
            Transform space = slot.transform;
            RectTransform box = (RectTransform)slot.displayRoot.transform;
            Vector2 min = space.InverseTransformPoint(box.TransformPoint(box.rect.min));
            Vector2 max = space.InverseTransformPoint(box.TransformPoint(box.rect.max));
            float width = max.x - min.x;
            return NtormatPageIcons(display)
                .Select(icon => ((Vector2)space.InverseTransformPoint(icon.transform.position) - min) / width).ToList();
        }

        private static string LayoutText(List<Vector2> layout) =>
            layout.Count == 0 ? "none" : string.Join(" ", layout.Select(point => $"({point.x:F2},{point.y:F2})"));

        // The page icons keep the place and spacing against the slot they have in the game's look, though the slot is
        // drawn smaller; and the selector moves to the page picked, as scrolling through the pages does.
        public static IEnumerator ExpectNtormatPagesFollowSlot(this Harness h)
        {
            PluginConfig settings = SevenUIPlugin.Settings;
            h.SetConfig(settings.FlatPanels, false);
            yield return h.Wait(LookSwitchSeconds);
            List<Vector2> vanilla = NtormatLayout(h);
            if (vanilla.Count == 0) throw h.Fail("no N'tormat pages in the game's look");
            h.SetConfig(settings.FlatPanels, true);
            List<Vector2> styled = new List<Vector2>();
            yield return h.Until(
                () =>
                {
                    styled = NtormatLayout(h);
                    return styled.Count == vanilla.Count &&
                           styled.Zip(vanilla, (a, b) => (a - b).magnitude).All(distance => distance < SlotTolerance);
                },
                () => $"the pages placed against the slot as in the game's look (game {LayoutText(vanilla)}, " +
                      $"SevenUI {LayoutText(styled)}) {h.Diagnosis()}");

            CursedBookBodyBehavior book = Harness.LocalBody.GetComponent<CursedBookBodyBehavior>();
            if (!book) throw h.Fail("no N'tormat on the player's body");
            for (int page = 0; page < book.GetSpellSlotCount(); page++)
            {
                book.SelectSpell(page);
                int picked = page;
                CursedBookExtraDisplay display = null;
                yield return h.Until(
                    () =>
                    {
                        display = NtormatDisplay(h);
                        List<CursedBookSpellSlotIcon> icons = display ? NtormatPageIcons(display) : null;
                        return icons != null && picked < icons.Count && display.indicatorTransform &&
                               (display.indicatorTransform.position - icons[picked].transform.position).sqrMagnitude < 1e-4f;
                    },
                    () => $"the selector on page {picked + 1} {h.Diagnosis()}");
            }
        }

        public static IEnumerator CaptureLooks(this Harness h)
        {
            yield return h.Screenshot("sevenui");
            PluginConfig settings = SevenUIPlugin.Settings;
            h.SetConfig(settings.FlatPanels, false);
            h.SetConfig(settings.BarlowFont, false);
            h.SetConfig(settings.MonospaceDamageNumbers, false);
            yield return h.Wait(LookSwitchSeconds);
            yield return h.Screenshot("vanilla");
            h.SetConfig(settings.FlatPanels, true);
            h.SetConfig(settings.BarlowFont, true);
            h.SetConfig(settings.MonospaceDamageNumbers, true);
            yield return h.Wait(LookSwitchSeconds);
        }
    }
}
