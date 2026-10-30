using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ModTestRunner;
using RoR2;
using RoR2.UI;
using TMPro;
using UnityEngine;

namespace SevenUI.Tests
{
    // Checks that parts of the HUD draw at the design canvas's sizes on screen, in its pixels on a 1080-pixel-high
    // screen: the run info's height and the gap under it, the level's block beside the bars, the health and experience
    // bars' heights, the skill slots and the gaps between them, the divider before the equipment, and the buffs'
    // counts in their squares' top-right corners. Also the run timer's and the objective rows' texts.
    internal static class CanvasSizeChecks
    {
        private const float CanvasHeight = 1080f;
        // Within a pixel or so: the clusters are drawn in perspective, and turned slightly before the mod levels them.
        private const float SizeTolerance = 1.5f;
        private const float HealthHeight = 22f;
        private const float ExpHeight = 4f;
        // The level's 52px block and the 12px gap after it.
        private const float BarsInset = 64f;
        private const float SkillSize = 68f;
        private const float SkillGap = 8f;
        // The equipment sits past the divider: 8 + 4 + 2 + 4 + 8.
        private const float EquipmentGap = 26f;
        // 8 + 34 + 8 + 14.4 + 10, and the column 8 under it.
        private const float RunInfoHeight = 74.4f;
        private const float ColumnGap = 8f;
        private const string UpperRightPath = "MainContainer/MainUIArea/SpringCanvas/UpperRightCluster";
        // A 500px notification, its middle 758px down.
        private const float NotificationWidth = 500f;
        private const float NotificationMiddleDown = 758f;

        // Canvas pixels per screen pixel, and a rect's box on screen in canvas pixels.
        private static (Camera camera, float unit) Screen(Harness h)
        {
            Camera camera = CanvasLayoutChecks.HudCamera(SevenUIChecks.HudRoot(h));
            return (camera, CanvasLayoutChecks.ScreenOf(camera).height / CanvasHeight);
        }

        private static Rect Box(Harness h, RectTransform rect)
        {
            (Camera camera, float unit) = Screen(h);
            Rect box = CanvasLayoutChecks.OnScreen(camera, rect);
            return new Rect(box.x / unit, box.y / unit, box.width / unit, box.height / unit);
        }

        private static bool Near(float value, float expected) => Mathf.Abs(value - expected) < SizeTolerance;

        private static string BoxText(Rect box) => $"x {box.xMin:0.#}-{box.xMax:0.#} y {box.yMin:0.#}-{box.yMax:0.#}";

        public static IEnumerator ExpectPlayerBarsAsCanvas(this Harness h)
        {
            string look = null;
            yield return h.Until(
                () =>
                {
                    RectTransform bars = SevenUIChecks.HudRoot(h).Find(GameNames.PlayerBars) as RectTransform;
                    RectTransform health = bars ? bars.Find("HealthbarRoot") as RectTransform : null;
                    RectTransform exp = bars ? bars.Find(GameNames.ExpBar) as RectTransform : null;
                    RectTransform level = bars ? bars.Find(GameNames.LevelRoot) as RectTransform : null;
                    if (!health || !exp || !level) return false;
                    Rect all = Box(h, bars);
                    Rect healthBox = Box(h, health);
                    Rect expBox = Box(h, exp);
                    Rect levelBox = Box(h, level);
                    look = $"bars {BoxText(all)}, health {BoxText(healthBox)}, experience {BoxText(expBox)}, level {BoxText(levelBox)}";
                    return Near(healthBox.height, HealthHeight) && Near(expBox.height, ExpHeight) &&
                           Near(healthBox.xMin - all.xMin, BarsInset) && Near(expBox.xMin, healthBox.xMin) &&
                           Near(expBox.yMin, all.yMin) && healthBox.yMin > expBox.yMax && levelBox.xMax <= healthBox.xMin;
                },
                () => $"the level beside a {HealthHeight}px health bar over a {ExpHeight}px experience bar, " +
                      $"{BarsInset}px in ({look}) {h.Diagnosis()}");
        }

        public static IEnumerator ExpectSkillRowAsCanvas(this Harness h)
        {
            string look = null;
            yield return h.Until(
                () =>
                {
                    HUD hud = SevenUIChecks.HudRoot(h).GetComponent<HUD>();
                    if (!hud || hud.skillIcons == null || hud.skillIcons.Length < 2 || hud.skillIcons.Any(skill => !skill)) return false;
                    List<Rect> boxes = hud.skillIcons.Select(skill => Box(h, (RectTransform)skill.transform)).ToList();
                    EquipmentIcon equipment = hud.equipmentIcons.FirstOrDefault(icon => icon && !icon.displayAlternateEquipment &&
                                                                                        icon.displayRoot);
                    Rect equipmentBox = equipment ? Box(h, (RectTransform)equipment.displayRoot.transform) : new Rect();
                    Transform divider = hud.skillIcons.Last().transform.Find(SkillRow.DividerName);
                    look = string.Join(", ", boxes.Select(BoxText)) + $"; equipment {BoxText(equipmentBox)}; divider {(bool)divider}";
                    bool sized = boxes.All(box => Near(box.width, SkillSize) && Near(box.height, SkillSize));
                    bool spaced = boxes.Zip(boxes.Skip(1), (a, b) => Near(b.xMin - a.xMax, SkillGap)).All(ok => ok);
                    return sized && spaced && equipment && Near(equipmentBox.width, SkillSize) &&
                           Near(equipmentBox.xMin - boxes.Last().xMax, EquipmentGap) && divider && divider.gameObject.activeInHierarchy;
                },
                () => $"{SkillSize}px skill slots {SkillGap}px apart, the equipment {EquipmentGap}px past the last with a " +
                      $"divider between ({look}) {h.Diagnosis()}");
        }

        // Each stacking buff with more than one stack shows its count in its square's top-right quarter, and the game's
        // stack text is hidden.
        public static IEnumerator ExpectBuffCountsInCorner(this Harness h)
        {
            string look = null;
            yield return h.Until(
                () =>
                {
                    BuffDisplay display = CanvasLayoutChecks.PlayerBuffs(h);
                    Transform labels = display ? display.transform.Find(BuffRows.LabelsName) : null;
                    if (!labels) return false;
                    List<BuffIcon> stacked = display.buffIconDisplayData
                        .Select(data => data.buffIconComponent)
                        .Where(icon => icon && icon.gameObject.activeSelf && icon.buffDef && icon.buffDef.canStack &&
                                       icon.buffCount + icon.buffDef.displayOffset > 1)
                        .ToList();
                    List<TMP_Text> counts = labels.Cast<Transform>().Where(child => child.name == "Count" && child.gameObject.activeSelf)
                        .Select(child => child.GetComponent<TMP_Text>()).ToList();
                    look = $"stacked {string.Join(" ", stacked.Select(icon => icon.buffCount))}, counts " +
                           string.Join(" ", counts.Select(count => $"'{count.text}'"));
                    if (stacked.Count == 0) return false;
                    return stacked.All(icon =>
                    {
                        string expected = (icon.buffCount + icon.buffDef.displayOffset).ToString();
                        Rect square = Box(h, icon.rectTransform);
                        return icon.stackCount.alpha == 0f && counts.Any(count =>
                        {
                            if (count.text != expected) return false;
                            Bounds text = count.textBounds;
                            Vector3 center = count.transform.TransformPoint(text.center);
                            (Camera camera, float unit) = Screen(h);
                            Vector2 point = RectTransformUtility.WorldToScreenPoint(camera, center) / unit;
                            return point.x > square.center.x && point.y > square.center.y && point.x < square.xMax + SizeTolerance * 4f;
                        });
                    });
                },
                () => $"the stack counts in the buffs' top-right corners ({look}) {h.Diagnosis()}");
        }
        public static IEnumerator ExpectRunInfoCanvasHeight(this Harness h)
        {
            string look = null;
            yield return h.Until(
                () =>
                {
                    Transform cluster = SevenUIChecks.HudRoot(h).Find(UpperRightPath);
                    RectTransform background = cluster ? cluster.GetComponentsInChildren<RectTransform>(true)
                        .FirstOrDefault(rect => rect.name == Texts.RunInfoBackgroundName) : null;
                    RectTransform column = background ? background.parent.Find("RightInfoBar") as RectTransform : null;
                    TimerText timer = background ? background.parent.GetComponentInChildren<TimerText>(true) : null;
                    if (!column || !timer || !timer.targetLabel) return false;
                    Rect panel = Box(h, background);
                    Rect below = Box(h, column);
                    string text = timer.targetLabel.text;
                    look = $"panel {BoxText(panel)}, column {BoxText(below)}, timer '{text}'";
                    return Near(panel.height, RunInfoHeight) && Near(panel.yMin - below.yMax, ColumnGap) &&
                           !text.Contains("<voffset") && !text.Contains(".");
                },
                () => $"the run info {RunInfoHeight}px tall with the column {ColumnGap}px under it, and the timer without " +
                      $"hundredths ({look}) {h.Diagnosis()}");
        }

        // Each open objective's box drawn as the mod's outline, and a done one's as the game's check.
        public static IEnumerator ExpectObjectiveBoxesFlat(this Harness h)
        {
            string look = null;
            yield return h.Until(
                () =>
                {
                    List<ObjectiveCheckbox> boxes = SevenUIChecks.HudRoot(h).GetComponentsInChildren<ObjectiveCheckbox>()
                        .Where(box => box.Checkbox && box.Checkbox.gameObject.activeInHierarchy).ToList();
                    look = string.Join(", ", boxes.Select(box =>
                        $"open={box.Checkbox.sprite == box.OpenSprite} outline={box.Box.enabled} game={box.Checkbox.enabled}"));
                    return boxes.Count > 0 && boxes.All(box =>
                        (box.Checkbox.sprite == box.OpenSprite) == (box.Box.enabled && !box.Checkbox.enabled));
                },
                () => $"the open objectives' boxes as flat outlines ({look}) {h.Diagnosis()}");
        }

        // The charge's percentage at the right, the game's, and out of the text.
        public static IEnumerator ExpectChargeRow(this Harness h)
        {
            string look = null;
            yield return h.Until(
                () =>
                {
                    ChargeRow row = SevenUIChecks.HudRoot(h).GetComponentInChildren<ChargeRow>();
                    if (!row || !row.Zone) return false;
                    int percent = row.Zone.displayChargePercent;
                    look = $"text '{row.Label.text}', percent '{row.Percent.text}', zone {percent}%, game '{row.GameLabel.text}'";
                    return row.Percent.text == percent + "%" && !row.Label.text.Contains(percent + "%") &&
                           row.Label.text.Length > 0 && row.GameLabel.alpha == 0f;
                },
                () => $"the teleporter's charge as a row with its percentage at the right ({look}) {h.Diagnosis()}");
        }
        public static IEnumerator ExpectNotificationAsCanvas(this Harness h)
        {
            string look = null;
            yield return h.Until(
                () =>
                {
                    GenericNotification notification = SevenUIChecks.HudRoot(h).GetComponentInChildren<GenericNotification>();
                    if (!notification || !notification.canvasGroup || !notification.titleTMP) return false;
                    Transform backdrop = notification.canvasGroup.transform.Find("Backdrop");
                    if (!backdrop) return false;
                    Rect box = Box(h, (RectTransform)backdrop);
                    (Camera camera, float unit) = Screen(h);
                    Rect screen = CanvasLayoutChecks.ScreenOf(camera);
                    float middleDown = screen.height / unit - box.center.y;
                    float offCenter = box.center.x - screen.center.x / unit;
                    look = $"panel {BoxText(box)}, middle {middleDown:0.#} down, {offCenter:0.#} off center, " +
                           $"strip {(bool)backdrop.Find(PromptsStyler.NotificationStripName)}, title color {notification.titleTMP.color}";
                    return Near(box.width, NotificationWidth) && Near(middleDown, NotificationMiddleDown) &&
                           Mathf.Abs(offCenter) < SizeTolerance && backdrop.Find(PromptsStyler.NotificationStripName) &&
                           SevenUIChecks.Near(notification.titleTMP.color, Palette.Text);
                },
                () => $"the pickup notification {NotificationWidth}px wide, {NotificationMiddleDown}px down, with a strip " +
                      $"and a white title ({look}) {h.Diagnosis()}");
        }
    }
}
