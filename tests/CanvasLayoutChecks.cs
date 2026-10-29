using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ModTestRunner;
using RoR2;
using RoR2.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SevenUI.Tests
{
    // Checks that parts of the HUD sit and size as on the design canvas: the corners at its margins (TightMargins),
    // the top bars at its width, the currencies in a row, the boss bar's title row and trail, and the run info and
    // objective rows at its proportions. Measured in world space, on screen or in the HUD's own units, so they hold at
    // any resolution.
    internal static class CanvasLayoutChecks
    {
        // The canvas's margins, in its pixels on a 1080-pixel-high screen (HudMargins), to within a pixel.
        private const float CanvasHeight = 1080f;
        private const float SideMargin = 28f;
        private const float TopMargin = 24f;
        private const float MarginTolerance = 1f;
        // The run info is 300px wide for a 34px timer (RunInfoStyler), and an objective's box is 14px for 16px text.
        private const float ColumnPerTimerFont = 300f / 34f;
        private const float BoxPerObjectiveFont = 14f / 16f;
        private const float ProportionTolerance = 0.05f;
        private const float MinLabelShare = 0.75f;
        // In the money cluster's units.
        private const float RowTolerance = 0.5f;
        // The boss bar is 800 of the top cluster's units wide, which match the canvas's pixels; its title row's ends
        // meet the bar's within this, as glyphs sit a little inside their boxes.
        private const float BossBarWidth = 800f;
        private const float BossEdgeTolerance = 4f;
        // A share of the boss bar.
        private const float TrailMinimum = 0.02f;

        private const string SpringCanvasPath = "MainContainer/MainUIArea/SpringCanvas";
        private const string UpperLeftPath = SpringCanvasPath + "/UpperLeftCluster";
        private const string UpperRightPath = SpringCanvasPath + "/UpperRightCluster";
        private const string BottomLeftPath = SpringCanvasPath + "/BottomLeftCluster";
        private const string HealthBarPath = BottomLeftPath + "/BarRoots/HealthbarRoot";
        private const string TopCenterPath = SpringCanvasPath + "/TopCenterCluster";
        // 900 of the canvas's 1920 pixels (TopBarsWidth).
        private const float TopCenterCanvasShare = 900f / 1920f;
        private const string StripContainer = "StripContainer";
        private const string StripLabel = "Label";
        private const string StripCheckbox = "Checkbox";

        private static Bounds In(RectTransform space, RectTransform rect)
        {
            Vector3[] corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Bounds bounds = new Bounds(space.InverseTransformPoint(corners[0]), Vector3.zero);
            foreach (Vector3 corner in corners)
            {
                bounds.Encapsulate(space.InverseTransformPoint(corner));
            }
            return bounds;
        }

        private static HUD Hud(Harness h) => SevenUIChecks.HudRoot(h).GetComponent<HUD>();

        private static Camera HudCamera(Transform root)
        {
            Canvas canvas = root.GetComponent<Canvas>();
            return canvas && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        }

        private static Rect ScreenOf(Camera camera) =>
            camera ? camera.pixelRect : new Rect(0f, 0f, Screen.width, Screen.height);

        // Through the HUD's camera, which draws the HUD's main area in perspective.
        private static Rect OnScreen(Camera camera, RectTransform rect)
        {
            Vector3[] corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Vector2 min = RectTransformUtility.WorldToScreenPoint(camera, corners[0]);
            Vector2 max = min;
            foreach (Vector3 corner in corners)
            {
                Vector2 point = RectTransformUtility.WorldToScreenPoint(camera, corner);
                min = Vector2.Min(min, point);
                max = Vector2.Max(max, point);
            }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        // Each corner's outermost panel and its gaps to the screen's sides, in canvas pixels: left or right, then top
        // or bottom.
        private static List<(string name, Vector2 gaps, bool found)> CornerGaps(Harness h)
        {
            Transform root = SevenUIChecks.HudRoot(h);
            HUD hud = Hud(h);
            Camera camera = HudCamera(root);
            Rect screen = ScreenOf(camera);
            float unit = screen.height / CanvasHeight;
            List<(string, Vector2, bool)> gaps = new List<(string, Vector2, bool)>();

            RectTransform upperLeft = root.Find(UpperLeftPath) as RectTransform;
            RectTransform runInfo = root.Find(UpperRightPath)?.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(t => t.name == Texts.RunInfoBackgroundName) as RectTransform;
            RectTransform healthBar = root.Find(HealthBarPath) as RectTransform;
            InputBindingDisplayController key = hud.skillIcons.Length > 0 && hud.skillIcons[0]
                ? hud.skillIcons[0].GetComponentInChildren<InputBindingDisplayController>(true)
                : null;
            RectTransform keyPlate = key ? key.transform.parent as RectTransform : null;
            EquipmentIcon equipment = hud.equipmentIcons.FirstOrDefault(icon => icon && !icon.displayAlternateEquipment &&
                                                                                icon.displayRoot);
            RectTransform rowEnd = equipment ? equipment.displayRoot.transform as RectTransform : null;

            void Add(string name, RectTransform across, RectTransform upDown, bool right, bool top)
            {
                if (!across || !upDown)
                {
                    gaps.Add((name, Vector2.zero, false));
                    return;
                }
                Rect x = OnScreen(camera, across);
                Rect y = OnScreen(camera, upDown);
                float gapX = right ? screen.xMax - x.xMax : x.xMin - screen.xMin;
                float gapY = top ? screen.yMax - y.yMax : y.yMin - screen.yMin;
                gaps.Add((name, new Vector2(gapX / unit, gapY / unit), true));
            }

            Add("money", upperLeft, upperLeft, false, true);
            Add("run info", runInfo, runInfo, true, true);
            Add("health bar", healthBar, healthBar, false, false);
            Add("skills", rowEnd, keyPlate, true, false);
            return gaps;
        }

        private static string GapsLook(List<(string name, Vector2 gaps, bool found)> gaps) =>
            gaps == null
                ? "-"
                : string.Join(", ", gaps.Select(g => g.found ? $"{g.name} {g.gaps.x:0.#}/{g.gaps.y:0.#}" : $"{g.name} missing"));

        // With TightMargins on.
        public static IEnumerator ExpectHudAtCanvasMargins(this Harness h)
        {
            List<(string name, Vector2 gaps, bool found)> gaps = null;
            yield return h.Until(
                () =>
                {
                    gaps = CornerGaps(h);
                    return gaps.Count == 4 && gaps.All(g => g.found &&
                        Mathf.Abs(g.gaps.x - SideMargin) < MarginTolerance &&
                        Mathf.Abs(g.gaps.y - (g.name == "money" || g.name == "run info" ? TopMargin : SideMargin)) <
                        MarginTolerance);
                },
                () => $"the corners {SideMargin} canvas pixels from the sides and bottom and {TopMargin} from the top " +
                      $"(gaps: {GapsLook(gaps)}) {h.Diagnosis()}");
        }

        // With TightMargins off: the area that holds the corners back inside the game's main area.
        public static IEnumerator ExpectHudAtGameMargins(this Harness h)
        {
            RectTransform spring = null;
            yield return h.Until(
                () =>
                {
                    spring = SevenUIChecks.HudRoot(h).Find(SpringCanvasPath) as RectTransform;
                    return spring && spring.anchorMin == Vector2.zero && spring.anchorMax == Vector2.one;
                },
                () => $"the game's HUD area back (anchors {(spring ? $"{spring.anchorMin}-{spring.anchorMax}" : "none")}, " +
                      $"corner gaps {GapsLook(CornerGaps(h))}) {h.Diagnosis()}");
        }

        private static RectTransform TopCenter(Harness h) => SevenUIChecks.HudRoot(h).Find(TopCenterPath) as RectTransform;

        // The item and boss bars' cluster's share of the screen's width.
        private static float TopCenterShare(Harness h)
        {
            Transform root = SevenUIChecks.HudRoot(h);
            RectTransform topCenter = TopCenter(h);
            Camera camera = HudCamera(root);
            return topCenter ? OnScreen(camera, topCenter).width / ScreenOf(camera).width : 0f;
        }

        public static IEnumerator ExpectTopBarsAtCanvasWidth(this Harness h)
        {
            yield return h.Until(
                () => Mathf.Abs(TopCenterShare(h) / TopCenterCanvasShare - 1f) < ProportionTolerance,
                () => $"the item and boss bars {TopCenterCanvasShare:P0} of the screen's width " +
                      $"(shown {TopCenterShare(h):P1}) {h.Diagnosis()}");
        }

        public static IEnumerator ExpectTopBarsAtGameWidth(this Harness h)
        {
            yield return h.Until(
                () => TopCenter(h) && Mathf.Approximately(TopCenter(h).sizeDelta.x, GameNames.TopCenterWidthDelta),
                () => $"the item and boss bars back at the game's width (size delta " +
                      $"{(TopCenter(h) ? TopCenter(h).sizeDelta.x : 0f)}, game {GameNames.TopCenterWidthDelta}) {h.Diagnosis()}");
        }

        // The shown currencies left to right, in the money cluster's units.
        private static List<(MoneyText money, Bounds panel, Bounds amount)> Currencies(Harness h)
        {
            HUD hud = Hud(h);
            RectTransform cluster = SevenUIChecks.HudRoot(h).Find(UpperLeftPath) as RectTransform;
            if (!cluster) return new List<(MoneyText, Bounds, Bounds)>();
            return new[] { hud.moneyText, hud.voidCoinText, hud.lunarCoinText }
                .Where(money => money && money.gameObject.activeInHierarchy && money.targetText)
                .Select(money => (money, In(cluster, (RectTransform)money.transform), AmountBounds(cluster, money.targetText)))
                .OrderBy(c => c.Item2.min.x).ToList();
        }

        // The amount's drawn characters, not its box.
        private static Bounds AmountBounds(RectTransform cluster, TMP_Text amount)
        {
            Bounds local = amount.textBounds;
            Bounds bounds = new Bounds(cluster.InverseTransformPoint(amount.transform.TransformPoint(local.min)), Vector3.zero);
            bounds.Encapsulate(cluster.InverseTransformPoint(amount.transform.TransformPoint(local.max)));
            return bounds;
        }

        private static string CurrencyLook(List<(MoneyText money, Bounds panel, Bounds amount)> currencies) =>
            string.Join(", ", currencies.Select(c =>
                $"{c.money.name} panel x {c.panel.min.x:0.#}-{c.panel.max.x:0.#} top {c.panel.max.y:0.#}, " +
                $"amount x {c.amount.min.x:0.#}-{c.amount.max.x:0.#}, color {c.money.targetText.color}"));

        // Side by side with tops level, each panel holding its amount, and the money and lunar coins in the canvas's
        // colors.
        public static IEnumerator ExpectCurrenciesInRow(this Harness h)
        {
            HUD hud = Hud(h);
            List<(MoneyText money, Bounds panel, Bounds amount)> currencies = null;
            yield return h.Until(
                () =>
                {
                    currencies = Currencies(h);
                    if (currencies.Count == 0) return false;
                    for (int i = 0; i < currencies.Count; i++)
                    {
                        Bounds panel = currencies[i].panel;
                        Bounds amount = currencies[i].amount;
                        if (Mathf.Abs(panel.max.y - currencies[0].panel.max.y) > RowTolerance) return false;
                        if (i > 0 && panel.min.x < currencies[i - 1].panel.max.x) return false;
                        if (amount.min.x < panel.min.x || amount.max.x > panel.max.x + RowTolerance) return false;
                    }
                    return SevenUIChecks.Near(hud.moneyText.targetText.color, Palette.Money) &&
                           (!hud.lunarCoinText || SevenUIChecks.Near(hud.lunarCoinText.targetText.color, Palette.LunarCoin));
                },
                () => $"the currencies in a row on fitted panels ({CurrencyLook(currencies)}) {h.Diagnosis()}");
        }

        private static DifficultyBarController DifficultyBar(Harness h) =>
            SevenUIChecks.HudRoot(h).GetComponentsInChildren<DifficultyBarController>(true).FirstOrDefault();

        private static string RunInfoLook(DifficultyBarController bar, RunInfoText text, float ratio) =>
            $"width per timer font {ratio:0.##} (expected {ColumnPerTimerFont:0.##}), " +
            $"difficulty '{(text && text.Difficulty ? text.Difficulty.text : "-")}', " +
            $"stage line '{(text && text.StageAndLevel ? text.StageAndLevel.text : "-")}'";

        // The column at the mod's width for its timer, the run's difficulty by name, and the game's stage and
        // level on one line.
        public static IEnumerator ExpectRunInfoAsCanvas(this Harness h)
        {
            DifficultyBarController bar = null;
            RunInfoText text = null;
            float ratio = 0f;
            yield return h.Until(
                () =>
                {
                    bar = DifficultyBar(h);
                    if (!bar || !Run.instance) return false;
                    Transform panel = bar.transform.parent;
                    RectTransform background = panel.Find(Texts.RunInfoBackgroundName) as RectTransform;
                    TimerText timerText = panel.GetComponentInChildren<TimerText>(true);
                    TMP_Text timer = timerText ? timerText.GetComponent<TMP_Text>() : null;
                    text = panel.GetComponentInChildren<RunInfoText>(true);
                    if (!background || !timer || !text) return false;
                    ratio = background.rect.width * background.lossyScale.x / (timer.fontSize * timer.transform.lossyScale.x);
                    DifficultyDef difficulty = DifficultyCatalog.GetDifficultyDef(Run.instance.selectedDifficulty);
                    TMP_Text stage = text.GameStage;
                    TMP_Text level = text.GameLevel;
                    return Mathf.Abs(ratio / ColumnPerTimerFont - 1f) < ProportionTolerance &&
                           difficulty != null && text.Difficulty.text == Language.GetString(difficulty.nameToken) &&
                           stage && level && !stage.enabled && !level.enabled &&
                           text.StageAndLevel.text.Contains(stage.text) && text.StageAndLevel.text.Contains(level.text);
                },
                () => $"the run info laid out as on the canvas ({RunInfoLook(bar, text, ratio)}) {h.Diagnosis()}");
        }

        private static List<(TMP_Text label, RectTransform box)> ObjectiveRows(Harness h)
        {
            Transform panel = h.FindUnderHud(GameNames.ObjectivePanel);
            Transform container = panel ? panel.Find(StripContainer) : null;
            if (!container) return new List<(TMP_Text, RectTransform)>();
            return container.Cast<Transform>().Where(strip => strip.gameObject.activeInHierarchy)
                .Select(strip => strip.GetComponent<ChildLocator>()).Where(locator => locator)
                .Select(locator => (locator.FindChildComponent<TMP_Text>(StripLabel), locator.FindChild(StripCheckbox) as RectTransform))
                .Where(row => row.Item1).ToList();
        }

        // The label's width over its row's, which the checkbox and the insets take a little of.
        private static float LabelShare(TMP_Text label)
        {
            RectTransform row = label.transform.parent as RectTransform;
            return row && row.rect.width > 0f ? ((RectTransform)label.transform).rect.width / row.rect.width : 0f;
        }

        private static float BoxPerFont((TMP_Text label, RectTransform box) row) =>
            row.box
                ? row.box.rect.height * row.box.lossyScale.y / (row.label.fontSize * row.label.transform.lossyScale.y)
                : 0f;

        private static string RowsLook(List<(TMP_Text label, RectTransform box)> rows) =>
            rows.Count == 0
                ? "no rows"
                : string.Join(", ", rows.Select(row =>
                    $"'{row.label.text}' {row.label.alignment} box/font {BoxPerFont(row):0.##} width share {LabelShare(row.label):0.##}"));

        // Each shown objective left-aligned after a checkbox at the canvas's size for its text.
        public static IEnumerator ExpectObjectiveRowsAsCanvas(this Harness h)
        {
            List<(TMP_Text label, RectTransform box)> rows = null;
            yield return h.Until(
                () =>
                {
                    rows = ObjectiveRows(h);
                    return rows.Count > 0 && rows.All(row =>
                        row.label.alignment == TextAlignmentOptions.Left &&
                        Mathf.Abs(BoxPerFont(row) / BoxPerObjectiveFont - 1f) < ProportionTolerance &&
                        LabelShare(row.label) > MinLabelShare);
                },
                () => $"the objectives left-aligned after small checkboxes (canvas box/font {BoxPerObjectiveFont:0.##}; " +
                      $"{RowsLook(rows)}) {h.Diagnosis()}");
        }
        // The boss bar's title row and trail in place, with or without a boss: the bar the canvas's width, the name
        // and subtitle out of the container's layout to sit above it, the mod's health text and trail, and the game's
        // trailing bar off.
        public static IEnumerator ExpectBossBarAsCanvas(this Harness h)
        {
            HUDBossHealthBarController boss = null;
            yield return h.Until(
                () =>
                {
                    boss = SevenUIChecks.HudRoot(h).GetComponentsInChildren<HUDBossHealthBarController>(true).FirstOrDefault();
                    return boss && BossLook(boss) == null;
                },
                () => $"the boss bar laid out as on the canvas ({(boss ? BossLook(boss) : "no boss bar")}) {h.Diagnosis()}");
        }

        private static bool IgnoresLayout(Component component) =>
            component && component.GetComponents<ILayoutIgnorer>().Any(ignorer => ignorer.ignoreLayout);

        // Null when the boss bar is laid out as on the canvas, or what isn't.
        private static string BossLook(HUDBossHealthBarController boss)
        {
            RectTransform bar = boss.fillRectImage ? boss.fillRectImage.transform.parent as RectTransform : null;
            LayoutElement cell = bar && bar.parent ? bar.parent.GetComponent<LayoutElement>() : null;
            if (!bar || !cell) return "no bar";
            if (!Mathf.Approximately(cell.preferredWidth, BossBarWidth)) return $"bar width {cell.preferredWidth}";
            if (!IgnoresLayout(boss.bossNameLabel) || !IgnoresLayout(boss.bossSubtitleLabel))
            {
                return "the name or subtitle still in the container's layout";
            }
            if (!bar.Find(BossTitleRow.HealthName)) return "no health text above the bar";
            if (!bar.Find(BossDamageTrail.TrailName)) return "no trail";
            if (boss.delayRectImage.enabled) return "the game's trailing bar on";
            return null;
        }

        private static float TrailFill(HUDBossHealthBarController boss)
        {
            Transform trail = boss.fillRectImage.transform.parent.Find(BossDamageTrail.TrailName);
            Image image = trail ? trail.GetComponent<Image>() : null;
            return image ? image.fillAmount : -1f;
        }

        // With a boss showing: its health written as the canvas's "28,410 / 45,800", the title row above the bar with
        // the subtitle after the name and the health at the bar's right end.
        public static IEnumerator ExpectBossRowFilled(this Harness h, HUDBossHealthBarController boss)
        {
            string look = null;
            yield return h.Until(
                () =>
                {
                    RectTransform bar = boss.fillRectImage.transform.parent as RectTransform;
                    TMP_Text health = bar.Find(BossTitleRow.HealthName)?.GetComponent<TMP_Text>();
                    RectTransform container = boss.container.transform as RectTransform;
                    if (!health || !container) return false;
                    Bounds barBounds = In(container, bar);
                    Bounds healthBounds = TextIn(container, health);
                    Bounds nameBounds = TextIn(container, boss.bossNameLabel);
                    Bounds subtitleBounds = TextIn(container, boss.bossSubtitleLabel);
                    look = $"health '{health.text}', bar x {barBounds.min.x:0}-{barBounds.max.x:0} top {barBounds.max.y:0}, " +
                           $"name x {nameBounds.min.x:0}-{nameBounds.max.x:0} bottom {nameBounds.min.y:0}, " +
                           $"subtitle x {subtitleBounds.min.x:0}, health x {healthBounds.max.x:0} bottom {healthBounds.min.y:0}";
                    return health.text.Contains(" / ") && nameBounds.min.y > barBounds.max.y &&
                           healthBounds.min.y > barBounds.max.y && subtitleBounds.min.x > nameBounds.max.x &&
                           Mathf.Abs(nameBounds.min.x - barBounds.min.x) < BossEdgeTolerance &&
                           Mathf.Abs(healthBounds.max.x - barBounds.max.x) < BossEdgeTolerance;
                },
                () => $"the boss's title row above its bar ({look}) {h.Diagnosis()}");
        }

        // The drawn characters, not the text's box.
        private static Bounds TextIn(RectTransform space, TMP_Text text)
        {
            Bounds local = text.textBounds;
            Bounds bounds = new Bounds(space.InverseTransformPoint(text.transform.TransformPoint(local.min)), Vector3.zero);
            bounds.Encapsulate(space.InverseTransformPoint(text.transform.TransformPoint(local.max)));
            return bounds;
        }

        // Right after a hit the lost health stays drawn beyond the fill; once it has drained it meets the fill.
        public static IEnumerator ExpectBossTrail(this Harness h, HUDBossHealthBarController boss, bool trailing)
        {
            yield return h.Until(
                () => trailing
                    ? TrailFill(boss) > boss.fillRectImage.fillAmount + TrailMinimum
                    : Mathf.Abs(TrailFill(boss) - boss.fillRectImage.fillAmount) < TrailMinimum / 10f,
                () => $"the boss's lost health {(trailing ? "drawn after the fill" : "drained to the fill")} " +
                      $"(trail {TrailFill(boss):0.###}, fill {boss.fillRectImage.fillAmount:0.###}) {h.Diagnosis()}");
        }

    }
}
