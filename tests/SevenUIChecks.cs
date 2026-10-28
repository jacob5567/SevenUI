using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Bootstrap;
using ModTestRunner;
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

        private static Transform HudRoot(Harness h)
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

        private static bool Near(Color a, Color b) =>
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
        public static IEnumerator ExpectPlainPanel(this Harness h, Transform panel, string what)
        {
            yield return h.Until(
                () => panel && IsFlat(panel.GetComponent<Image>()) && !panel.Find(Texts.HeaderStripName) &&
                      Title(panel) && !IsHeader(Title(panel)) && Title(panel).characterSpacing == 0f,
                () => $"{what} flat, without the header style ({Look(panel)}) {h.Diagnosis()}");
        }

        private static DifficultyBarController DifficultyBar(Harness h) =>
            HudRoot(h).GetComponentsInChildren<DifficultyBarController>(true).FirstOrDefault();

        private static string RunInfoLook(DifficultyBarController bar)
        {
            if (!bar) return "no difficulty bar";
            Transform panel = bar.transform.parent;
            Transform background = panel.Find(Texts.RunInfoBackgroundName);
            Image timerBox = panel.Find(GameNames.TimerPanel)?.GetComponent<Image>();
            CanvasGroup segments = bar.transform.Find(GameNames.DifficultySegments)?.GetComponent<CanvasGroup>();
            DifficultyReadout readout = bar.GetComponentInChildren<DifficultyReadout>();
            return $"background={(bool)background}, timer box image={(timerBox ? timerBox.enabled.ToString() : "-")}, " +
                   $"segments alpha={(segments ? segments.alpha.ToString() : "no group")}, " +
                   $"readout={(readout ? $"'{readout.ShownName}' {readout.ShownProgress:P0}" : "none")}";
        }

        // One flat panel behind the run info, the game's segments hidden, and the mod's bar showing one of the game's
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
                    return background && IsFlat(background.GetComponent<Image>()) && timerBox && !timerBox.enabled &&
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
                    return !panel.Find(Texts.RunInfoBackgroundName) && timerBox && timerBox.enabled &&
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

        private static bool UsesFlatStyle(HealthBar bar) =>
            bar._style && bar._style.name.EndsWith(Texts.FlatStyleSuffix) &&
            (!bar._allyStyle || bar._allyStyle.name.EndsWith(Texts.FlatStyleSuffix));

        private static string HealthLook(HealthBar bar)
        {
            if (!bar) return "no player health bar";
            Transform digits = bar.transform.Find(GameNames.HealthSpriteDigits);
            return $"style={(bar._style ? bar._style.name : "none")}, sprite digits field={(bool)bar.spriteAsNumberManager}, " +
                   $"digits active={(digits ? digits.gameObject.activeSelf.ToString() : "-")}, " +
                   $"text={(bar.currentHealthText ? bar.currentHealthText.text : "-")}/{(bar.fullHealthText ? bar.fullHealthText.text : "-")}, " +
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
                    return UsesFlatStyle(bar) && !bar.spriteAsNumberManager && digits && !digits.gameObject.activeSelf &&
                           bar.currentHealthText.isActiveAndEnabled &&
                           bar.currentHealthText.text == Mathf.Ceil(bar.source.combinedHealth).ToString() &&
                           bar.fullHealthText && bar.fullHealthText.text == Mathf.Ceil(bar.source.fullHealth).ToString();
                },
                () => $"flat player bars with the health as text ({HealthLook(bar)}) {h.Diagnosis()}");
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
                   $"boss fill={(boss && boss.fillRectImage && boss.fillRectImage.sprite ? boss.fillRectImage.sprite.name : "-")}";
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
                           boss && boss.fillRectImage.sprite && boss.fillRectImage.sprite.name == Texts.FlatSpriteName;
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
                           boss && boss.fillRectImage.sprite && boss.fillRectImage.sprite.name != Texts.FlatSpriteName;
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

        private static RawImage PromptStrip(Harness h)
        {
            ContextManager prompt = HudRoot(h).GetComponentsInChildren<ContextManager>(true).FirstOrDefault();
            return prompt && prompt.contextDisplay ? prompt.contextDisplay.GetComponent<RawImage>() : null;
        }

        private static string PromptLook(RawImage strip) =>
            strip ? $"texture={(strip.texture ? strip.texture.name : "none")} color={strip.color}" : "no prompt";

        public static IEnumerator ExpectPromptFlat(this Harness h)
        {
            RawImage strip = null;
            yield return h.Until(
                () =>
                {
                    strip = PromptStrip(h);
                    return strip && !strip.texture && Near(strip.color, Palette.Panel);
                },
                () => $"the interaction prompt on a flat panel ({PromptLook(strip)}) {h.Diagnosis()}");
        }

        public static IEnumerator ExpectPromptVanilla(this Harness h)
        {
            RawImage strip = null;
            yield return h.Until(
                () =>
                {
                    strip = PromptStrip(h);
                    return strip && strip.texture;
                },
                () => $"the game's interaction prompt back ({PromptLook(strip)}) {h.Diagnosis()}");
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
            $"chat input sprite={(input ? (input.sprite ? input.sprite.name : "none") : "-")}";

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
                           input && !input.sprite && Near(input.color, Palette.ScoreboardPanel);
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
                    return board && board.sprite && input && input.sprite;
                },
                () => $"the game's scoreboard and chat input back ({OverlaysLook(board, input)}) {h.Diagnosis()}");
        }
    }
}
