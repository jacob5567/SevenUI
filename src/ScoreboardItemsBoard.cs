using System.Collections.Generic;
using RoR2;
using RoR2.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SevenUI
{
    // One per scoreboard, on its rows' container: lays every row's items out as a grid (ScoreboardItemGrid) in the
    // chosen layout, and keeps the docked item pane (ScoreboardDetailPane) on the item the pointer is on, hiding the
    // tooltip that would cover the items.
    //
    // Rows: each row's items under its name, across the row's width, the row as tall as they need; the item pointed at
    // lit in every row that holds it and the rest dimmed, and described under the rows. Tabs: the rows become tabs
    // along the board's top, and the shown player's key item, equipment and items sit under them, described in a pane
    // at their right. Pointing at a tab, or selecting anything in it with a gamepad, shows that player; the board opens
    // on the viewer's own. Either way, if the board would run past the bottom of the scoreboard's panel, every grid's
    // slots shrink together, down to 24px.
    internal sealed class ScoreboardItemsBoard : MonoBehaviour
    {
        public const string PaneHolderName = "SevenUIItemPaneHolder";
        public const string TabsDividerName = "SevenUITabsDivider";
        public const string TabBarName = "SevenUITabBar";

        // In the scoreboard's units, the design canvas's pixels. Rows: the items 12px under the line of the name.
        private const float ItemsGap = 12f;
        public const float ItemsTop = ScoreboardStyler.RowPaddingY + ScoreboardStyler.TopLineHeight + ItemsGap;
        private const float PaneTop = 16f;
        private const float PaneBottom = 20f;
        // Tabs: up to 380px each, 8px apart, 14px under the board's top; under them, 18px down, the key item and the
        // equipment, then the items, and the pane 24px to their right, 356px wide.
        public const int TabPaddingX = 14;
        public const int TabPaddingTop = 10;
        public const int TabPaddingBottom = 12;
        public const float TabHeight = TabPaddingTop + ScoreboardStyler.NameBlockHeight + TabPaddingBottom;
        public const float TabBarHeight = 3f;
        private const float TabsTop = 14f;
        private const float TabGap = 8f;
        private const float TabMaxWidth = 380f;
        private const float BodyGap = 18f;
        private const float BodyBottom = 20f;
        private const float SlotGap = 12f;
        public const float PaneWidth = 356f;
        private const float PaneGap = 24f;
        private const float DividerHeight = 1f;
        private static readonly float[] SlotSizes = { 40f, 36f, 32f, 28f, 24f };
        // Moving between two slots, the pointer leaves one before it reaches the next; the light holds over the gap.
        private const float HoldSeconds = 0.15f;

        private readonly List<ScoreboardItemGrid> grids = new List<ScoreboardItemGrid>();
        private readonly List<ScoreboardItemGrid> previous = new List<ScoreboardItemGrid>();
        private readonly List<ScoreboardStrip> found = new List<ScoreboardStrip>();
        private ScoreboardLayout layout;
        private StyleRecorder recorder;
        private HashSet<ScoreboardStrip> styled;
        private HUD hud;
        private RectTransform panel;
        private TMP_Text style;
        private RectTransform strips;
        private RectTransform paneHolder;
        private LayoutElement paneHolderElement;
        private LayoutElement stripsElement;
        private RectTransform tabsDivider;
        private ScoreboardDetailPane pane;
        private bool stale = true;
        private float laidOutLimit;
        private float laidOutWidth;
        private ScoreboardStrip shown;
        private ScoreboardItemGrid shownGrid;
        private float bodyTop;
        private float itemsWidth;
        private float bodyHeight;
        private float containerHeight = -1f;
        private float holderHeight = -1f;
        private TooltipProvider seenProvider;
        private TooltipController hiddenTooltip;
        private GameObject seenSelection;
        private ItemIcon pointed;
        private ItemIcon described;
        private float holdUntil;
        private ItemIndex highlighted = ItemIndex.None;

        // Read by the SevenUI.Tests plugin.
        public ScoreboardLayout Layout => layout;
        public ScoreboardDetailPane Pane => pane;
        public IReadOnlyList<ScoreboardItemGrid> Grids => grids;
        public ScoreboardStrip ShownStrip => shown;
        public ItemIndex Highlighted => highlighted;

        // 'styled' holds the rows the styler has restyled for this layout, the only ones laid out.
        public void Init(ScoreboardLayout layout, StyleRecorder recorder, HashSet<ScoreboardStrip> styled, HUD hud,
            RectTransform panel, TMP_Text style)
        {
            this.layout = layout;
            this.recorder = recorder;
            this.styled = styled;
            this.hud = hud;
            this.panel = panel;
            this.style = style;
            strips = (RectTransform)transform;
            if (layout == ScoreboardLayout.Rows)
            {
                paneHolder = NewObject(PaneHolderName, strips);
                recorder.Add(paneHolder.gameObject);
                paneHolderElement = paneHolder.gameObject.AddComponent<LayoutElement>();
                RectTransform line = NewObject("Divider", paneHolder);
                line.anchorMin = new Vector2(0f, 1f);
                line.anchorMax = Vector2.one;
                line.offsetMin = new Vector2(0f, -DividerHeight);
                line.offsetMax = Vector2.zero;
                Image lineImage = line.gameObject.AddComponent<Image>();
                lineImage.color = Palette.RowDivider;
                lineImage.raycastTarget = false;
                pane = new ScoreboardDetailPane(paneHolder, style, recorder);
                paneHolder.gameObject.SetActive(false);
            }
            else
            {
                stripsElement = gameObject.AddComponent<LayoutElement>();
                recorder.Add(stripsElement);
                tabsDivider = NewObject(TabsDividerName, strips);
                recorder.Add(tabsDivider.gameObject);
                tabsDivider.gameObject.AddComponent<LayoutIgnorer>();
                Image divider = tabsDivider.gameObject.AddComponent<Image>();
                divider.color = Palette.RowDivider;
                divider.raycastTarget = false;
                pane = new ScoreboardDetailPane(strips, style, recorder);
            }
        }

        // Each time the scoreboard opens: on the viewer's own tab, with nothing described yet.
        private void OnEnable()
        {
            shown = null;
            stale = true;
            seenProvider = null;
            pointed = null;
            described = null;
            highlighted = ItemIndex.None;
            pane?.Hide();
        }

        // In the tabs layout, shows this player's items.
        public void Show(ScoreboardStrip strip)
        {
            if (layout != ScoreboardLayout.Tabs || !strip || strip == shown) return;
            shown = strip;
            stale = true;
        }

        // While the scoreboard is open: a few reads per row and icon, and the layout again only when the rows, their
        // items, the room or the shown tab change.
        private void LateUpdate()
        {
            if (recorder == null) return;
            bool changed = Gather();
            foreach (ScoreboardItemGrid grid in grids)
            {
                if (grid.Refresh()) changed = true;
            }
            if (layout == ScoreboardLayout.Tabs)
            {
                FollowSelection();
            }
            float limit = AvailableHeight();
            float width = Width();
            if (changed || stale || !Mathf.Approximately(limit, laidOutLimit) || !Mathf.Approximately(width, laidOutWidth))
            {
                stale = false;
                laidOutLimit = limit;
                laidOutWidth = width;
                if (layout == ScoreboardLayout.Rows)
                {
                    LayOutRows(width, limit);
                }
                else
                {
                    LayOutTabs(width, limit);
                }
            }
            foreach (ScoreboardItemGrid grid in grids)
            {
                grid.Apply();
            }
            Point(changed);
            foreach (ScoreboardItemGrid grid in grids)
            {
                grid.Highlight(highlighted, layout == ScoreboardLayout.Rows);
            }
            PlacePane();
        }

        // The restyled rows shown, in order; true if they changed. A row stays laid out as it was while it shows.
        private bool Gather()
        {
            found.Clear();
            for (int i = 0; i < strips.childCount; i++)
            {
                Transform child = strips.GetChild(i);
                if (!child.gameObject.activeSelf) continue;
                ScoreboardStrip strip = child.GetComponent<ScoreboardStrip>();
                if (strip && strip.itemInventoryDisplay && styled.Contains(strip))
                {
                    found.Add(strip);
                }
            }
            bool same = found.Count == grids.Count;
            for (int i = 0; same && i < found.Count; i++)
            {
                same = grids[i].Strip == found[i];
            }
            if (same) return false;
            previous.Clear();
            previous.AddRange(grids);
            grids.Clear();
            foreach (ScoreboardStrip strip in found)
            {
                ScoreboardItemGrid grid = previous.Find(each => each.Strip == strip) ??
                                          new ScoreboardItemGrid(strip, recorder);
                grids.Add(grid);
            }
            previous.Clear();
            return true;
        }

        // From the rows' top down to the bottom of the scoreboard's panel, which the game keeps above the skills.
        private float AvailableHeight()
        {
            if (!panel) return float.PositiveInfinity;
            Vector3 top = panel.InverseTransformPoint(strips.TransformPoint(new Vector3(0f, strips.rect.yMax, 0f)));
            return top.y - panel.rect.yMin;
        }

        // What the layout is as wide as: the board for the tabs, a row for the rows, which their container pads a little
        // narrower than the board.
        private float Width() =>
            layout == ScoreboardLayout.Rows && grids.Count > 0
                ? ((RectTransform)grids[0].Strip.transform).rect.width
                : strips.rect.width;

        private void LayOutRows(float width, float limit)
        {
            float across = Mathf.Max(ScoreboardItemGrid.FullSlot, width - 2f * ScoreboardStyler.RowPaddingX);
            float size = SlotSizes[SlotSizes.Length - 1];
            foreach (float candidate in SlotSizes)
            {
                float total = 0f;
                foreach (ScoreboardItemGrid grid in grids)
                {
                    total += RowHeight(grid, candidate, across);
                }
                if (total <= limit)
                {
                    size = candidate;
                    break;
                }
            }
            foreach (ScoreboardItemGrid grid in grids)
            {
                grid.SetLayout(size, across);
                float items = grid.HeightAt(size, across);
                SetRowHeight(grid.Strip, RowHeight(grid, size, across));
                RectTransform cell = grid.Cell;
                cell.anchorMin = new Vector2(0f, 1f);
                cell.anchorMax = Vector2.one;
                cell.pivot = new Vector2(0f, 1f);
                cell.offsetMin = new Vector2(ScoreboardStyler.RowPaddingX, -(ItemsTop + items));
                cell.offsetMax = new Vector2(-ScoreboardStyler.RowPaddingX, -ItemsTop);
            }
        }

        // A row without items keeps the plain row's height.
        private static float RowHeight(ScoreboardItemGrid grid, float size, float across) =>
            grid.Slots == 0
                ? ScoreboardStyler.RowHeight
                : ItemsTop + grid.HeightAt(size, across) + ScoreboardStyler.RowPaddingY;

        private static void SetRowHeight(ScoreboardStrip strip, float height)
        {
            LayoutElement element = strip.GetComponent<LayoutElement>();
            if (element && !Mathf.Approximately(element.preferredHeight, height))
            {
                element.minHeight = height;
                element.preferredHeight = height;
            }
            RectTransform rect = (RectTransform)strip.transform;
            if (!Mathf.Approximately(rect.sizeDelta.y, height))
            {
                rect.sizeDelta = new Vector2(rect.sizeDelta.x, height);
            }
        }

        private void LayOutTabs(float width, float limit)
        {
            shownGrid = null;
            foreach (ScoreboardItemGrid grid in grids)
            {
                if (grid.Strip == shown) shownGrid = grid;
            }
            if (shownGrid == null)
            {
                shownGrid = OwnGrid();
                shown = shownGrid?.Strip;
            }
            if (shownGrid == null) return;

            float inner = width - 2f * ScoreboardStyler.RowPaddingX;
            int count = grids.Count;
            float tabWidth = Mathf.Min(TabMaxWidth, (inner - (count - 1) * TabGap) / count);
            bodyTop = TabsTop + TabHeight + BodyGap;
            itemsWidth = Mathf.Max(ScoreboardItemGrid.FullSlot, inner - PaneGap - PaneWidth);
            float room = limit - bodyTop - BodyBottom - ScoreboardStyler.SlotSize - SlotGap;
            float size = SlotSizes[SlotSizes.Length - 1];
            foreach (float candidate in SlotSizes)
            {
                if (shownGrid.HeightAt(candidate, itemsWidth) <= room)
                {
                    size = candidate;
                    break;
                }
            }

            for (int i = 0; i < count; i++)
            {
                ScoreboardItemGrid grid = grids[i];
                float x = ScoreboardStyler.RowPaddingX + i * (tabWidth + TabGap);
                StatsSection.TopLeft((RectTransform)grid.Strip.transform, x, TabsTop, tabWidth, TabHeight);
                bool isShown = grid == shownGrid;
                if (grid.Background)
                {
                    grid.Background.color = isShown ? ScoreboardRow.OwnRowColor : Color.clear;
                }
                Transform bar = grid.Background ? grid.Background.transform.Find(TabBarName) : null;
                if (bar && bar.gameObject.activeSelf != isShown)
                {
                    bar.gameObject.SetActive(isShown);
                }
                Transform keyItem = grid.Strip.keyItemIcon ? grid.Strip.keyItemIcon.transform : null;
                Transform equipment = grid.Strip.equipmentIcon ? grid.Strip.equipmentIcon.transform : null;
                ShowPart(keyItem, isShown);
                ShowPart(equipment, isShown);
                ShowPart(grid.Cell, isShown);
                grid.SetLayout(size, itemsWidth);
                if (!isShown) continue;
                // From the row's own top left, which is the tab's.
                float left = ScoreboardStyler.RowPaddingX - x;
                float top = TabHeight + BodyGap;
                float slot = ScoreboardStyler.SlotSize;
                if (keyItem)
                {
                    StatsSection.TopLeft((RectTransform)keyItem, left, top, slot, slot);
                }
                if (equipment)
                {
                    StatsSection.TopLeft((RectTransform)equipment, left + slot + SlotGap, top, slot, slot);
                }
                StatsSection.TopLeft(grid.Cell, left, top + slot + SlotGap, itemsWidth, grid.HeightAt(size, itemsWidth));
            }
            StatsSection.TopLeft(tabsDivider, 0f, TabsTop + TabHeight, width, DividerHeight);
            bodyHeight = ScoreboardStyler.SlotSize + SlotGap + shownGrid.HeightAt(size, itemsWidth);
        }

        // The viewer's own row, or the first.
        private ScoreboardItemGrid OwnGrid()
        {
            CharacterMaster viewer = hud && hud.localUserViewer != null ? hud.localUserViewer.cachedMaster : null;
            foreach (ScoreboardItemGrid grid in grids)
            {
                if (viewer && grid.Strip.master == viewer) return grid;
            }
            return grids.Count > 0 ? grids[0] : null;
        }

        // A hidden tab's parts are faded out through a group of the mod's, which also keeps the pointer and the
        // gamepad off them; the game shows and hides the parts by their objects, which stay its own.
        private void ShowPart(Transform part, bool show)
        {
            if (!part) return;
            CanvasGroup group = part.GetComponent<CanvasGroup>();
            if (!group)
            {
                if (show) return;
                group = part.gameObject.AddComponent<CanvasGroup>();
                recorder.Add(group);
            }
            group.alpha = show ? 1f : 0f;
            group.interactable = show;
            group.blocksRaycasts = show;
        }

        // A gamepad's selection in a tab shows that tab.
        private void FollowSelection()
        {
            MPEventSystem events = Events();
            GameObject selected = events ? events.currentSelectedGameObject : null;
            if (selected == seenSelection) return;
            seenSelection = selected;
            if (!selected) return;
            foreach (ScoreboardItemGrid grid in grids)
            {
                if (selected.transform.IsChildOf(grid.Strip.transform))
                {
                    Show(grid.Strip);
                    return;
                }
            }
        }

        // The item the pointer (or a gamepad's selection) is on, from the tooltip it opened: lit and described, and
        // the tooltip itself hidden, the pane saying what it would.
        private void Point(bool changed)
        {
            MPEventSystem events = Events();
            TooltipProvider provider = events ? events.currentTooltipProvider : null;
            if (provider != seenProvider)
            {
                seenProvider = provider;
                ItemIcon icon = provider ? provider.GetComponent<ItemIcon>() : null;
                pointed = icon && icon.transform.IsChildOf(strips) ? icon : null;
            }
            if (pointed)
            {
                holdUntil = Time.unscaledTime + HoldSeconds;
                highlighted = pointed.itemIndex;
                if (changed || pointed != described || pane.Item != pointed.itemIndex)
                {
                    described = pointed;
                    pane.Show(pointed, grids);
                }
                HideTooltip(events);
                return;
            }
            if (Time.unscaledTime > holdUntil)
            {
                highlighted = ItemIndex.None;
            }
            if (changed && pane.Shown)
            {
                Redescribe();
            }
        }

        // After the items changed, the described item again, from any row still holding it.
        private void Redescribe()
        {
            ItemIcon icon = described && described.itemIndex == pane.Item ? described : null;
            foreach (ScoreboardItemGrid grid in grids)
            {
                if (icon) break;
                icon = grid.Find(pane.Item);
            }
            if (icon)
            {
                described = icon;
                pane.Show(icon, grids);
            }
            else
            {
                described = null;
                pane.Hide();
            }
        }

        // The game makes the tooltip as its own canvas when the pointer reaches the item, and destroys it after.
        private void HideTooltip(MPEventSystem events)
        {
            TooltipController tooltip = events ? events.currentTooltip : null;
            if (!tooltip || tooltip == hiddenTooltip) return;
            hiddenTooltip = tooltip;
            Canvas canvas = tooltip.GetComponent<Canvas>();
            if (canvas)
            {
                canvas.enabled = false;
            }
        }

        private void PlacePane()
        {
            if (layout == ScoreboardLayout.Rows)
            {
                if (paneHolder.gameObject.activeSelf != pane.Shown)
                {
                    paneHolder.gameObject.SetActive(pane.Shown);
                }
                if (!pane.Shown) return;
                // The game adds rows at the container's end, so the pane holder is moved back after them.
                if (paneHolder.GetSiblingIndex() != strips.childCount - 1)
                {
                    paneHolder.SetAsLastSibling();
                }
                float width = Width();
                pane.Place(ScoreboardStyler.RowPaddingX, PaneTop, width - 2f * ScoreboardStyler.RowPaddingX);
                float height = PaneTop + pane.Height + PaneBottom;
                if (!Mathf.Approximately(height, holderHeight) || !Mathf.Approximately(paneHolder.sizeDelta.x, width))
                {
                    holderHeight = height;
                    paneHolderElement.minHeight = height;
                    paneHolderElement.preferredHeight = height;
                    paneHolder.sizeDelta = new Vector2(width, height);
                }
                return;
            }
            if (pane.Shown)
            {
                pane.Place(ScoreboardStyler.RowPaddingX + itemsWidth + PaneGap, bodyTop, PaneWidth);
            }
            float container = bodyTop + Mathf.Max(bodyHeight, pane.Shown ? pane.Height : 0f) + BodyBottom;
            if (Mathf.Approximately(container, containerHeight)) return;
            containerHeight = container;
            stripsElement.minHeight = container;
            stripsElement.preferredHeight = container;
            strips.sizeDelta = new Vector2(strips.sizeDelta.x, container);
        }

        private MPEventSystem Events() =>
            hud && hud.eventSystemProvider ? hud.eventSystemProvider.resolvedEventSystem : null;

        private static RectTransform NewObject(string name, Transform parent)
        {
            GameObject made = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
            made.layer = parent.gameObject.layer;
            made.transform.SetParent(parent, false);
            return (RectTransform)made.transform;
        }
    }
}
