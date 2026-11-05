using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using ModTestRunner;
using RoR2;
using RoR2.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SevenUI.Tests
{
    // Checks that parts of the HUD draw at the design canvas's sizes on screen, in its pixels on a 1080-pixel-high
    // screen: the run info's height and the gap under it, the level's block beside the bars, the health and experience
    // bars' heights, the skill slots and the gaps between them, the dividers before the equipment and after the key
    // item, the skills' and the equipment's stocks in their slots' corners, the slot above the skills keeping its place over them, and the
    // buffs' counts in their squares' top-right corners. Also the run timer's and the objective rows' texts.
    internal static class CanvasSizeChecks
    {
        private const float CanvasHeight = 1080f;
        // Within a pixel or so: the clusters are drawn in perspective, and turned slightly before the mod levels them.
        private const float SizeTolerance = 1.5f;
        // Added to the zone's percentage to write one that doesn't match it.
        private const int MismatchedPercentStep = 7;
        // The canvas's item slots on the scoreboard, and the items' place: 20px in from the row's or the board's left.
        private const float ScoreboardItemSlot = 40f;
        private const float ScoreboardItemsInset = 20f;
        // The canvas's board: 1220px wide, 110px from the top; its count line names the total's items.
        private const float ScoreboardWidth = 1220f;
        private const float ScoreboardTop = 110f;
        private const string ScoreboardCountsWord = " items";
        private const string ScoreboardStripsPath = "Container/StripContainer";
        private const string ScoreboardHeadersPath = "Container/HeaderGroup";
        private const string ScoreboardPortraitPath = "ClassBackground/SevenUIPortraitSquare";
        private const string ScoreboardNamePath = "NameObject";
        // The ally cards' place on the canvas.
        private const string AllyClusterPath = "MainContainer/MainUIArea/SpringCanvas/LeftCluster";
        private const float AllySideMargin = 28f;
        private const float AllyTop = 300f;
        private const string ScoreboardEquipmentName = "EquipmentBackground";
        private const float HealthHeight = 22f;
        private const float ExpHeight = 4f;
        // The level's 52px block and the 12px gap after it.
        private const float BarsInset = 64f;
        private const float SkillSize = 68f;
        private const float SkillGap = 8f;
        // The equipment and key item sit past their dividers: 8 + 4 + 2 + 4 + 8.
        private const float DividedGap = 26f;
        // 8 + 34 + 8 + 14.4 + 10, and the column 8 under it.
        private const float RunInfoHeight = 74.4f;
        // The artifacts' row: the rows' 8px gap and its 22px icons.
        private const float ArtifactRowSpace = 8f + 22f;
        private const float ColumnGap = 8f;
        private const string UpperRightPath = "MainContainer/MainUIArea/SpringCanvas/UpperRightCluster";
        // A 500px notification, its middle 758px down.
        private const float NotificationWidth = 500f;
        private const float NotificationMiddleDown = 758f;
        // Its icon 44px in the middle of a 56px slot.
        private const float NotificationSlotSize = 56f;
        private const float NotificationIconSize = 44f;
        private const float TransformationSlotWidth = 136f;
        private const float TransformationIconOffset = 40f;
        private const float TransformationArrowSize = 28f;
        // A transformation notification waits behind any pickups still queued, each shown for six seconds.
        private const float TransformationTimeout = 30f;
        // A slot's stock 4px from its right, and 3px from its bottom in an 18px line box, which puts Barlow's baseline
        // a tenth of the size above the box's bottom.
        private const float StockRightInset = 4f;
        private const float StockBaseline = 3f + 18f * 0.1f;
        // In shares of a slot; the game's look is drawn turned, so its shares come out a little off.
        private const float UtilityAreaTolerance = 0.06f;
        // The timer's tags hold dots of their own, "<mspace=0.46em>".
        private static readonly Regex Tags = new Regex("<[^>]*>");

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

        // Null when the portrait's square is centered on the name block beside it, as on the canvas.
        private static string PortraitProblem(Harness h, Transform background)
        {
            RectTransform square = background.Find(ScoreboardPortraitPath) as RectTransform;
            RectTransform name = background.Find(ScoreboardNamePath) as RectTransform;
            if (!square || !name) return "no portrait or name block";
            Rect squareBox = Box(h, square);
            Rect nameBox = Box(h, name);
            return Near(squareBox.center.y, nameBox.center.y)
                ? null
                : $"square's middle at {squareBox.center.y:0.#}, the name block's at {nameBox.center.y:0.#}";
        }

        // With TightMargins, the ally cards' cluster 28px in from the screen's left edge and 300px down, as on the canvas.
        public static IEnumerator ExpectAllyCardsAsCanvas(this Harness h)
        {
            string look = null;
            yield return h.Until(
                () =>
                {
                    RectTransform cluster = SevenUIChecks.HudRoot(h).Find(AllyClusterPath) as RectTransform;
                    if (!cluster) return false;
                    Rect box = Box(h, cluster);
                    (Camera camera, float unit) = Screen(h);
                    Rect screen = CanvasLayoutChecks.ScreenOf(camera);
                    float left = box.xMin - screen.xMin / unit;
                    float down = screen.yMax / unit - box.yMax;
                    look = $"{left:0.#} in, {down:0.#} down";
                    return !SevenUIPlugin.Settings.TightMargins.Value || Near(left, AllySideMargin) && Near(down, AllyTop);
                },
                () => $"the ally cards {AllySideMargin}px in and {AllyTop}px down ({look}) {h.Diagnosis()}");
        }

        // Null when the board is the canvas's width and in its place: with ScoreboardBesideRunInfo, its top 110px down
        // and as far from the screen's left edge as from the run info's panel; without, centered on the screen.
        private static string PlacementProblem(Harness h, ScoreboardController board)
        {
            RectTransform strips = board.transform.Find(ScoreboardStripsPath) as RectTransform;
            RectTransform headers = board.transform.Find(ScoreboardHeadersPath) as RectTransform;
            if (!strips || !headers) return "no board";
            Rect box = Box(h, strips);
            float top = headers.gameObject.activeInHierarchy ? Box(h, headers).yMax : box.yMax;
            (Camera camera, float unit) = Screen(h);
            Rect screen = CanvasLayoutChecks.ScreenOf(camera);
            float left = box.xMin - screen.xMin / unit;
            float fromTop = screen.yMax / unit - top;
            string look = $"x {left:0.#} to {box.xMax - screen.xMin / unit:0.#}, {box.width:0.#} wide, top {fromTop:0.#} down";
            if (!Near(box.width, ScoreboardWidth)) return look;
            if (!SevenUIPlugin.Settings.ScoreboardBesideRunInfo.Value)
            {
                return Near(box.center.x, screen.center.x / unit) ? null : look;
            }
            RectTransform column = SevenUIChecks.HudRoot(h).GetComponentsInChildren<RectTransform>()
                .FirstOrDefault(rect => rect.name == RunInfoStyler.BackgroundName);
            if (!column) return "no run info panel";
            float toColumn = Box(h, column).xMin - box.xMax;
            float barBottom = ItemBarBottom(h, screen.yMax / unit);
            float wantedTop = Mathf.Max(ScoreboardTop, barBottom + ScoreboardPlacement.ItemBarGap);
            return Near(fromTop, wantedTop) && Near(left, toColumn)
                ? null
                : $"{look}, {toColumn:0.#} from the run info, item bar down to {barBottom:0.#}, wanted top {wantedTop:0.#}";
        }

        // How far down the screen the HUD's item bar reaches, from the screen's top; 0 without items.
        private static float ItemBarBottom(Harness h, float screenTop)
        {
            Transform topCenter = SevenUIChecks.HudRoot(h).Find(TopBarsWidth.TopCenterPath);
            ItemInventoryDisplay bar = topCenter ? topCenter.GetComponentInChildren<ItemInventoryDisplay>() : null;
            List<ItemIcon> icons = bar ? bar.GetComponentsInChildren<ItemIcon>().Where(icon => icon.isActiveAndEnabled).ToList() : null;
            if (icons == null || icons.Count == 0) return 0f;
            return screenTop - icons.Min(icon => Box(h, icon.rectTransform).yMin);
        }

        // Null when a row's items are drawn as the canvas's grid: slots of one size, at most 40px, none over another,
        // sorted into tier runs, the first at the items' top left, and no counts among them. In the
        // rows layout the items sit 20px in from the row's left, under its name block; in the tabs layout, 20px in from
        // the board's left, under the tabs.
        private static string GridProblem(Harness h, ScoreboardItemsBoard board, ScoreboardStrip strip, Transform background)
        {
            List<ItemIcon> icons = strip.itemInventoryDisplay.itemIcons.Where(icon => icon && icon.isActiveAndEnabled).ToList();
            if (icons.Count == 0) return "no icons";
            RectTransform cellRect = (RectTransform)strip.itemInventoryDisplay.transform;
            Rect cell = Box(h, cellRect);
            List<(ItemIcon icon, Rect box)> slots = icons.Select(icon => (icon, Box(h, icon.rectTransform))).ToList();
            float size = slots[0].box.width;
            bool sameSize = slots.All(slot => Near(slot.box.width, size) && Near(slot.box.height, size));
            bool overlap = slots.Select((a, i) => slots.Skip(i + 1).Any(b =>
                a.box.xMin < b.box.xMax - SizeTolerance && b.box.xMin < a.box.xMax - SizeTolerance &&
                a.box.yMin < b.box.yMax - SizeTolerance && b.box.yMin < a.box.yMax - SizeTolerance)).Any(any => any);
            List<int> groups = slots.OrderByDescending(slot => Mathf.Round(slot.box.yMax)).ThenBy(slot => slot.box.xMin)
                .Select(slot => TierGroups.Of(slot.icon.itemIndex)).ToList();
            bool sorted = groups.Zip(groups.Skip(1), (a, b) => a <= b).All(inOrder => inOrder);
            List<string> texts = cellRect.GetComponentsInChildren<TMP_Text>()
                .Where(text => text.isActiveAndEnabled && !text.GetComponentInParent<ItemIcon>() && text.text.Length > 0)
                .Select(text => text.text).ToList();
            Rect first = slots.OrderByDescending(slot => Mathf.Round(slot.box.yMax)).ThenBy(slot => slot.box.xMin).First().box;
            bool cornered = Near(first.xMin, cell.xMin) && Near(first.yMax, cell.yMax);
            string place;
            if (board.Layout == ScoreboardLayout.Rows)
            {
                Rect row = Box(h, (RectTransform)background);
                Rect name = Box(h, (RectTransform)background.Find(ScoreboardNamePath));
                place = Near(cell.xMin - row.xMin, ScoreboardItemsInset) && cell.yMax < name.yMin
                    ? null
                    : $"cell from x {cell.xMin - row.xMin:0.#} in its row, top {cell.yMax:0.#} under a name block ending at {name.yMin:0.#}";
            }
            else
            {
                Rect strips = Box(h, (RectTransform)board.transform);
                Rect tab = Box(h, (RectTransform)strip.transform);
                place = Near(cell.xMin - strips.xMin, ScoreboardItemsInset) && cell.yMax < tab.yMin
                    ? null
                    : $"cell from x {cell.xMin - strips.xMin:0.#} in the board, top {cell.yMax:0.#} under a tab ending at {tab.yMin:0.#}";
            }
            if (!sameSize || size > ScoreboardItemSlot + SizeTolerance || overlap || !sorted || texts.Count > 0 || !cornered || place != null)
            {
                return $"{icons.Count} icons {size:0.#} wide, all one size {sameSize}, overlapping {overlap}, tier order " +
                       $"[{string.Join(" ", groups)}], texts among them [{string.Join(" ", texts)}], " +
                       $"first item at the corner {cornered} (first at {BoxText(first)}, cell {BoxText(cell)}), {place ?? "in place"}";
            }
            return null;
        }

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
                           Near(equipmentBox.xMin - boxes.Last().xMax, DividedGap) && divider && divider.gameObject.activeInHierarchy;
                },
                () => $"{SkillSize}px skill slots {SkillGap}px apart, the equipment {DividedGap}px past the last with a " +
                      $"divider between ({look}) {h.Diagnosis()}");
        }

        // The key item a divided gap left of the first skill, at its height, with the divider between them, and its
        // outline in the game's key item color shown while it holds a key item that is ready.
        public static IEnumerator ExpectKeyItemDividedAsCanvas(this Harness h)
        {
            string look = null;
            yield return h.Until(
                () =>
                {
                    HUD hud = SevenUIChecks.HudRoot(h).GetComponent<HUD>();
                    if (!hud || hud.skillIcons == null || hud.skillIcons.Length == 0 || !hud.skillIcons[0] || !hud.keyItemIcon ||
                        !hud.keyItemIcon.displayRoot) return false;
                    Rect skillBox = Box(h, (RectTransform)hud.skillIcons[0].transform);
                    Transform display = hud.keyItemIcon.displayRoot.transform;
                    Rect keyItemBox = Box(h, (RectTransform)display);
                    Transform divider = display.Find(SkillRow.DividerName);
                    Image outline = display.Find(GameNames.KeyItemOutline)?.GetComponent<Image>();
                    KeyItemIcon.DisplayData shown = hud.keyItemIcon.currentDisplayData;
                    bool ready = shown.keyItemDef && shown.isReady;
                    look = $"key item {BoxText(keyItemBox)}, first skill {BoxText(skillBox)}, divider {(bool)divider}, " +
                           $"ready {ready}, outline {(outline ? $"shown={outline.gameObject.activeSelf} color={outline.color}" : "none")}";
                    return Near(keyItemBox.width, SkillSize) && Near(skillBox.xMin - keyItemBox.xMax, DividedGap) &&
                           Near(keyItemBox.yMin, skillBox.yMin) && divider && divider.gameObject.activeInHierarchy &&
                           outline && outline.gameObject.activeSelf == ready &&
                           SevenUIChecks.Near(outline.color, ColorCatalog.GetColor(ColorCatalog.ColorIndex.KeyItem));
                },
                () => $"the key item {DividedGap}px left of the first skill with a divider between, outlined in the key " +
                      $"item color while ready ({look}) {h.Diagnosis()}");
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
        // The canvas's run info has no artifacts' row; while an artifact the tests leave alone (Rebirth) is on, the
        // panel is taller by the row and its gap.
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
                    float height = RunInfoHeight + (RunSetUp.LeftAloneOn() > 0 ? ArtifactRowSpace : 0f);
                    return Near(panel.height, height) && Near(panel.yMin - below.yMax, ColumnGap) &&
                           !text.Contains("<voffset") && !Tags.Replace(text, "").Contains(".");
                },
                () => $"the run info {RunInfoHeight}px tall (and the artifacts' row while Rebirth is on) with the column " +
                      $"{ColumnGap}px under it, and the timer without " +
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

            // The game's text written with another percentage than the zone's, as the Pillar of Mass's objective can be
            // for a frame: the copy still leaves it out.
            ChargeRow charge = SevenUIChecks.HudRoot(h).GetComponentInChildren<ChargeRow>();
            int other = (charge.Zone.displayChargePercent + MismatchedPercentStep) % 100;
            charge.GameLabel.text = charge.Label.text + $" ({other}%)";
            yield return null;
            look = $"text '{charge.Label.text}', game '{charge.GameLabel.text}'";
            if (charge.Label.text.Contains("%"))
            {
                throw h.Fail($"the charge's text without a percentage that isn't the zone's ({look}) {h.Diagnosis()}");
            }
        }

        // The equipment's stock inside its slot's bottom-right corner, as the canvas draws a slot's stock: the last
        // digit's advance at the right inset, which right alignment puts it on, and the digits' baseline just above the
        // bottom inset.
        // The equipment's stock and every skill's that shows, with at least one skill's showing.
        public static IEnumerator ExpectStocksAsCanvas(this Harness h)
        {
            List<string> looks = new List<string>();
            yield return h.Until(
                () =>
                {
                    looks.Clear();
                    HUD hud = SevenUIChecks.HudRoot(h).GetComponent<HUD>();
                    EquipmentIcon equipment = hud && hud.equipmentIcons != null
                        ? hud.equipmentIcons.FirstOrDefault(icon => icon && !icon.displayAlternateEquipment && icon.displayRoot)
                        : null;
                    if (!equipment || hud.skillIcons == null) return false;
                    bool placed = StockInCorner(h, "equipment", equipment.stockText, (RectTransform)equipment.displayRoot.transform, looks);
                    int skills = 0;
                    foreach (SkillIcon skill in hud.skillIcons)
                    {
                        if (!skill || !skill.stockText || !skill.stockText.gameObject.activeInHierarchy) continue;
                        skills++;
                        placed &= StockInCorner(h, skill.name, skill.stockText, (RectTransform)skill.transform, looks);
                    }
                    if (skills == 0) looks.Add("no skill's stock showing");
                    return placed && skills > 0;
                },
                () => $"the stocks {StockRightInset}px from their slots' right, baselines {StockBaseline:0.#}px up " +
                      $"({string.Join("; ", looks)}) {h.Diagnosis()}");
        }

        private static bool StockInCorner(Harness h, string name, TMP_Text stock, RectTransform slotRect, List<string> looks)
        {
            if (!stock || !stock.gameObject.activeInHierarchy || stock.textInfo.characterCount == 0)
            {
                looks.Add($"{name}'s stock not showing");
                return false;
            }
            Rect slot = Box(h, slotRect);
            TMP_CharacterInfo last = stock.textInfo.characterInfo[stock.textInfo.characterCount - 1];
            Vector2 end = OnCanvas(h, stock.transform.TransformPoint(new Vector3(last.xAdvance, last.baseLine, 0f)));
            float right = slot.xMax - end.x;
            float baseline = end.y - slot.yMin;
            looks.Add($"{name} '{stock.text}', slot {BoxText(slot)}, {right:0.#}px from the right, baseline {baseline:0.#}px up");
            return Near(right, StockRightInset) && Near(baseline, StockBaseline);
        }

        private static Vector2 OnCanvas(Harness h, Vector3 world)
        {
            (Camera camera, float unit) = Screen(h);
            return RectTransformUtility.WorldToScreenPoint(camera, world) / unit;
        }

        // The slot some survivors' meters hang from keeps its place over the skill row from the game's look, in shares
        // of a slot: the gap under it, its middle's distance right of the row's, and its height. Measured in the
        // cluster's plane, which the game's look turns slightly away from the screen, so on screen its row's left
        // slots sit higher than its right ones; SevenUI's look draws the same plane facing the screen.
        public static IEnumerator ExpectUtilityAreaFollowsRow(this Harness h)
        {
            PluginConfig settings = SevenUIPlugin.Settings;
            h.SetConfig(settings.FlatPanels, false);
            yield return h.Wait(SevenUIChecks.LookSwitchSeconds);
            Vector3? vanilla = UtilityAreaPlace(h);
            if (vanilla == null) throw h.Fail($"no utility area over the skills in the game's look {h.Diagnosis()}");
            h.SetConfig(settings.FlatPanels, true);
            Vector3? styled = null;
            yield return h.Until(
                () =>
                {
                    styled = UtilityAreaPlace(h);
                    Vector3 difference = styled.GetValueOrDefault() - vanilla.Value;
                    return styled != null && Mathf.Abs(difference.x) < UtilityAreaTolerance &&
                           Mathf.Abs(difference.y) < UtilityAreaTolerance && Mathf.Abs(difference.z) < UtilityAreaTolerance;
                },
                () => $"the utility area over the skill row as in the game's look (gap, offset, height in slots: game " +
                      $"{PlaceText(vanilla)}, SevenUI {PlaceText(styled)}) {h.Diagnosis()}");
        }

        private static Vector3? UtilityAreaPlace(Harness h)
        {
            Transform root = SevenUIChecks.HudRoot(h);
            HUD hud = root.GetComponent<HUD>();
            RectTransform area = root.Find(GameNames.UtilityArea) as RectTransform;
            if (!hud || !area || hud.skillIcons == null || hud.skillIcons.Length == 0 || hud.skillIcons.Any(skill => !skill)) return null;
            Transform plane = area.parent;
            List<Rect> boxes = hud.skillIcons.Select(skill => InPlane(plane, (RectTransform)skill.transform)).ToList();
            Rect row = Rect.MinMaxRect(boxes.Min(box => box.xMin), boxes.Min(box => box.yMin), boxes.Max(box => box.xMax),
                boxes.Max(box => box.yMax));
            float slot = boxes.Average(box => box.height);
            Rect place = InPlane(plane, area);
            if (slot <= 0f) return null;
            return new Vector3((place.yMin - row.yMax) / slot, (place.center.x - row.center.x) / slot, place.height / slot);
        }

        // A rect's box in a transform it's drawn in.
        private static Rect InPlane(Transform plane, RectTransform rect)
        {
            Rect local = rect.rect;
            Vector2 min = plane.InverseTransformPoint(rect.TransformPoint(local.min));
            Vector2 max = plane.InverseTransformPoint(rect.TransformPoint(local.max));
            return Rect.MinMaxRect(Mathf.Min(min.x, max.x), Mathf.Min(min.y, max.y), Mathf.Max(min.x, max.x), Mathf.Max(min.y, max.y));
        }

        private static string PlaceText(Vector3? place) =>
            place is Vector3 value ? $"{value.x:F2}, {value.y:F2}, {value.z:F2}" : "none";

        // The notification showing, or the one whose title is the token's; with its icon in the middle of its slot,
        // whichever of the game's notifications it is (the drone's and the trial's hold their icon in a frame), and
        // its strip in the color given.
        public static IEnumerator ExpectNotificationAsCanvas(this Harness h, Color strip, string titleToken = null)
        {
            string look = null;
            yield return h.Until(
                () =>
                {
                    GenericNotification notification = SevenUIChecks.HudRoot(h).GetComponentsInChildren<GenericNotification>()
                        .FirstOrDefault(shown => titleToken == null || shown.titleText && shown.titleText.token == titleToken);
                    if (!notification || !notification.canvasGroup || !notification.titleTMP || !notification.iconImage) return false;
                    Transform backdrop = notification.canvasGroup.transform.Find("Backdrop");
                    RectTransform iconArea = notification.canvasGroup.transform.Find("IconArea") as RectTransform;
                    if (!backdrop || !iconArea) return false;
                    Rect slot = Box(h, iconArea);
                    Rect icon = Box(h, notification.iconImage.rectTransform);
                    bool iconCentered = Near(slot.width, NotificationSlotSize) && Near(slot.height, NotificationSlotSize) &&
                                        Near(icon.width, NotificationIconSize) && Near(icon.height, NotificationIconSize) &&
                                        (icon.center - slot.center).magnitude < SizeTolerance;
                    Rect box = Box(h, (RectTransform)backdrop);
                    (Camera camera, float unit) = Screen(h);
                    Rect screen = CanvasLayoutChecks.ScreenOf(camera);
                    float middleDown = screen.height / unit - box.center.y;
                    float offCenter = box.center.x - screen.center.x / unit;
                    Image stripImage = backdrop.Find(PromptsStyler.NotificationStripName)?.GetComponent<Image>();
                    look = $"panel {BoxText(box)}, middle {middleDown:0.#} down, {offCenter:0.#} off center, " +
                           $"strip {(stripImage ? stripImage.color.ToString() : "none")}, title color {notification.titleTMP.color}, " +
                           $"slot {BoxText(slot)}, icon {BoxText(icon)}";
                    return iconCentered && Near(box.width, NotificationWidth) && Near(middleDown, NotificationMiddleDown) &&
                           Mathf.Abs(offCenter) < SizeTolerance && stripImage && SevenUIChecks.Near(stripImage.color, strip) &&
                           SevenUIChecks.Near(notification.titleTMP.color, Palette.Text);
                },
                () => $"the {titleToken ?? "pickup"} notification {NotificationWidth}px wide, {NotificationMiddleDown}px down, " +
                      $"with a strip in {strip}, a white title and its icon {NotificationIconSize}px in the middle of its " +
                      $"{NotificationSlotSize}px slot ({look}) {h.Diagnosis()}");
        }

        // A transformation notification as on the canvas: a 136px slot with the old icon and the new one 44px, one at
        // each end, the game's arrow hidden, and the mod's arrow between them and strip along the slot's foot, both
        // blended from the old tier's color to the new one's.
        public static IEnumerator ExpectTransformationAsCanvas(this Harness h, string titleToken, Color from, Color to)
        {
            string look = null;
            yield return h.Until(
                () =>
                {
                    GenericNotification notification = SevenUIChecks.HudRoot(h).GetComponentsInChildren<GenericNotification>()
                        .FirstOrDefault(shown => shown.titleText && shown.titleText.token == titleToken);
                    if (!notification || !notification.canvasGroup || !notification.iconImage || !notification.previousIconImage) return false;
                    RectTransform iconArea = notification.canvasGroup.transform.Find("IconArea") as RectTransform;
                    if (!iconArea) return false;
                    Rect slot = Box(h, iconArea);
                    Rect previous = Box(h, notification.previousIconImage.rectTransform);
                    Rect icon = Box(h, notification.iconImage.rectTransform);
                    Graphic gameArrow = iconArea.Find("Arrow")?.GetComponent<Graphic>();
                    Transform arrowObject = iconArea.Find(PromptsStyler.TransformationArrowName);
                    Transform stripObject = iconArea.Find(PromptsStyler.TransformationStripName);
                    TierBlend arrow = arrowObject ? arrowObject.GetComponent<TierBlend>() : null;
                    TierBlend strip = stripObject ? stripObject.GetComponent<TierBlend>() : null;
                    Rect arrowBox = arrow ? Box(h, (RectTransform)arrow.transform) : default;
                    Rect stripBox = strip ? Box(h, (RectTransform)strip.transform) : default;
                    look = $"slot {BoxText(slot)}, old {BoxText(previous)}, new {BoxText(icon)}, " +
                           $"game arrow {(gameArrow ? gameArrow.enabled.ToString() : "none")}, " +
                           $"arrow {(arrow ? BoxText(arrowBox) + $" {arrow.From}-{arrow.To}" : "none")}, " +
                           $"strip {(strip ? BoxText(stripBox) + $" {strip.From}-{strip.To}" : "none")}";
                    bool slotSized = Near(slot.width, TransformationSlotWidth) && Near(slot.height, NotificationSlotSize);
                    bool iconsPlaced = Near(previous.width, NotificationIconSize) && Near(icon.width, NotificationIconSize) &&
                                       Near(previous.center.x, slot.center.x - TransformationIconOffset) &&
                                       Near(icon.center.x, slot.center.x + TransformationIconOffset) &&
                                       Near(previous.center.y, slot.center.y) && Near(icon.center.y, slot.center.y);
                    bool arrowPlaced = arrow && Near(arrowBox.width, TransformationArrowSize) &&
                                       (arrowBox.center - slot.center).magnitude < SizeTolerance;
                    bool stripPlaced = strip && Near(stripBox.width, slot.width) && Near(stripBox.yMin, slot.yMin);
                    bool blended = arrow && strip && SevenUIChecks.Near(arrow.From, from) && SevenUIChecks.Near(arrow.To, to) &&
                                   SevenUIChecks.Near(strip.From, from) && SevenUIChecks.Near(strip.To, to);
                    return slotSized && iconsPlaced && (!gameArrow || !gameArrow.enabled) && arrowPlaced && stripPlaced && blended;
                },
                () => $"the {titleToken} transformation in a {TransformationSlotWidth}px slot, its icons " +
                      $"{NotificationIconSize}px {TransformationIconOffset}px either side of the middle, a {TransformationArrowSize}px " +
                      $"arrow between them and a strip along the foot, blended {from} to {to} ({look}) {h.Diagnosis()}",
                TransformationTimeout);
        }

        // The open scoreboard as wide as the canvas's and where the setting puts it, and the viewer's row: its money
        // last, the survivor and level under the name and the item counts under those, the equipment outlined, and its
        // items as the canvas's grid (GridProblem). In the rows layout, the header strip with each header at its column
        // and the viewer's row tinted; in the tabs layout, no headers, and the viewer's tab shown, tinted and underlined.
        public static IEnumerator ExpectScoreboardAsCanvas(this Harness h)
        {
            string look = null;
            yield return h.Until(
                () =>
                {
                    ScoreboardController board = SevenUIChecks.HudRoot(h).GetComponentInChildren<ScoreboardController>();
                    ScoreboardItemsBoard items = board ? board.GetComponentInChildren<ScoreboardItemsBoard>() : null;
                    ScoreboardRow row = board ? board.GetComponentsInChildren<ScoreboardRow>()
                        .FirstOrDefault(r => r.Strip && r.Strip.master == Harness.LocalBody.master) : null;
                    look = $"board {(bool)board}, items board {(items ? items.Layout.ToString() : "none")}, " +
                           $"viewer's row {(bool)row}";
                    if (!items || !row || items.Layout != SevenUIPlugin.Settings.ScoreboardLayout.Value) return false;
                    bool tabs = items.Layout == ScoreboardLayout.Tabs;
                    Transform background = row.Strip.transform.Find(ScoreboardStyler.RowBackgroundName);
                    List<string> misplaced = new List<string>();
                    string headed;
                    if (tabs)
                    {
                        Transform headerGroup = board.transform.Find(ScoreboardHeadersPath);
                        Transform bar = background.Find(ScoreboardItemsBoard.TabBarName);
                        headed = headerGroup && headerGroup.gameObject.activeInHierarchy ? "headers shown" :
                            items.ShownStrip != row.Strip ? "another tab shown" :
                            !bar || !bar.gameObject.activeSelf ? "no line under the tab" : null;
                    }
                    else
                    {
                        ScoreboardHeaders headers = board.GetComponentInChildren<ScoreboardHeaders>();
                        if (!headers) return false;
                        for (int i = 0; i < headers.Headers.Length; i++)
                        {
                            RectTransform column = background.Find(headers.Columns[i]) as RectTransform;
                            if (!column || !column.gameObject.activeSelf) continue;
                            Rect columnBox = Box(h, column);
                            Rect headerBox = Box(h, headers.Headers[i].rectTransform);
                            float gap = headers.RightAligned[i] ? headerBox.xMax - columnBox.xMax : headerBox.xMin - columnBox.xMin;
                            if (!Near(gap, 0f))
                            {
                                misplaced.Add($"{headers.Columns[i]} {gap:0.#}");
                            }
                        }
                        headed = !headers.transform.Find(ScoreboardStyler.HeaderStripName) ? "no header strip" :
                            misplaced.Count > 0 ? $"misplaced headers [{string.Join(", ", misplaced)}]" : null;
                    }
                    Transform money = background.Find("TotalTextContainer");
                    Transform equipment = background.Find("EquipmentBackground");
                    bool moneyLast = money && equipment && money.GetSiblingIndex() > equipment.GetSiblingIndex();
                    string grid = GridProblem(h, items, row.Strip, background);
                    string placed = PlacementProblem(h, board);
                    string portrait = PortraitProblem(h, background);
                    look = $"{items.Layout}, {headed ?? "headed"}, row color {row.Background.color}, " +
                           $"survivor '{row.Survivor.text}', counts '{row.Counts.text}', money after the equipment {moneyLast}, " +
                           $"items {grid ?? "as the grid"}, board {placed ?? "in place"}, portrait {portrait ?? "centered"}";
                    return headed == null && grid == null && placed == null && portrait == null &&
                           row.Counts.text.Contains(ScoreboardCountsWord) &&
                           SevenUIChecks.Near(row.Background.color, ScoreboardRow.OwnRowColor) && row.Survivor.text.Contains(" · ") &&
                           moneyLast && equipment.Find("SevenUIEquipmentOutline");
                },
                () => $"the scoreboard laid out as on the canvas ({look}) {h.Diagnosis()}");
        }

        // With the pointer on the icon: the pane describing its item under its name, with the viewer among its holders
        // at the icon's stack, the item's tooltip hidden, the icon outlined, and in the rows layout every other item
        // dimmed.
        public static IEnumerator ExpectItemPane(this Harness h, ItemIcon icon)
        {
            string look = null;
            yield return h.Until(
                () =>
                {
                    if (!icon) return false;
                    ScoreboardItemsBoard board = icon.GetComponentInParent<ScoreboardItemsBoard>();
                    ScoreboardDetailPane pane = board ? board.Pane : null;
                    if (pane == null) return false;
                    MPEventSystem events = EventSystem.current as MPEventSystem;
                    Canvas tooltip = events && events.currentTooltip ? events.currentTooltip.GetComponent<Canvas>() : null;
                    Transform highlight = icon.transform.parent.Find(ScoreboardItemGrid.HighlightName);
                    Rect slot = Box(h, icon.rectTransform);
                    bool outlined = highlight && highlight.gameObject.activeSelf &&
                                    Near(Box(h, (RectTransform)highlight).center.x, slot.center.x) &&
                                    Near(Box(h, (RectTransform)highlight).center.y, slot.center.y);
                    List<ItemIcon> others = icon.transform.parent.GetComponentsInChildren<ItemIcon>().Where(other => other != icon).ToList();
                    bool dimmed = others.All(other => other.GetComponent<CanvasGroup>() && other.GetComponent<CanvasGroup>().alpha < 1f);
                    bool undimmed = !icon.GetComponent<CanvasGroup>() || Mathf.Approximately(icon.GetComponent<CanvasGroup>().alpha, 1f);
                    string stack = "×" + icon.itemCount;
                    string holders = string.Join(", ", Enumerable.Range(0, pane.HolderCount).Select(pane.HolderText));
                    string title = icon.tooltipProvider ? icon.tooltipProvider.titleText : "";
                    look = $"pane on {(pane.Shown ? pane.Title : "nothing")} held by [{holders}], tooltip " +
                           $"{(tooltip ? (tooltip.enabled ? "shown" : "hidden") : "none")}, outlined {outlined}, " +
                           $"{others.Count} others dimmed {dimmed}, this one undimmed {undimmed}";
                    return pane.Shown && pane.Item == icon.itemIndex && pane.Title == title && pane.Root.gameObject.activeInHierarchy &&
                           Enumerable.Range(0, pane.HolderCount).Any(i => pane.HolderText(i).Contains(stack)) &&
                           (!tooltip || !tooltip.enabled) && outlined && undimmed &&
                           (board.Layout == ScoreboardLayout.Tabs || dimmed);
                },
                () => $"the item described in the scoreboard's pane, {icon.tooltipProvider?.titleText} at its stack ({look}) " +
                      $"{h.Diagnosis()}");
        }
    }
}
