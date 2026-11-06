using System.Collections.Generic;
using RoR2;
using RoR2.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SevenUI
{
    // One per scoreboard, on its rows' container: lays every row's items out as a grid (ScoreboardItemGrid), and keeps
    // the docked pane (ScoreboardDetailPane) on the item, equipment or key item the pointer is on, hiding the tooltip
    // that would cover the items. The pane is in a column at the board's right, which the board grows to hold.
    //
    // Each row's items sit after its equipment, wrapping under themselves, the row as tall as they need; the item
    // pointed at lit in every row that holds it and the rest dimmed. If the board would run past the bottom of the
    // scoreboard's panel, the slots shrink, down to 24px. In the tabs setting, once the rows' slots would have to be
    // smaller than 32px, the same rows collapse instead: each row is its plain height without its items, but for the
    // shown player's, whose items stay in their place after the equipment and run down from the first row's top, past
    // the collapsed rows. Pointing at a player's row, or selecting anything in it with a gamepad, shows that player;
    // the collapsed rows open on the viewer's own.
    internal sealed class ScoreboardItemsBoard : MonoBehaviour
    {
        public const string PaneColumnName = "SevenUIItemPaneColumn";
        public const string TabBarName = "SevenUITabBar";

        private const float Canvas = HudUnits.PerCanvasPixel;

        // The design canvas's pixels, scaled to the HUD's units (HudUnits). The pane's column 340px wide, a line at its
        // left and the pane 16px in from its edges; the shown player's row marked by a 3px accent line at its left.
        public const float PaneColumnWidth = 340f * Canvas;
        private const float PaneInset = 16f * Canvas;
        public const float TabBarWidth = 3f * Canvas;
        private const float DividerHeight = 1f * Canvas;
        // In the tabs setting, the smallest slot the rows keep before they collapse.
        private const float SmallestRowsSlot = 32f * Canvas;
        private static readonly float[] SlotSizes = { 40f * Canvas, 36f * Canvas, SmallestRowsSlot, 28f * Canvas, 24f * Canvas };
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
        private VerticalLayoutGroup rowsGroup;
        private RectOffset rowsPadding;
        private float rowsHeight;
        // Collapsed: the shown player's items, from the first row's top, which the board grows to hold.
        private bool collapsed;
        private float itemsHeight;
        private ScoreboardDetailPane pane;
        private bool stale = true;
        private float laidOutLimit;
        private float laidOutWidth;
        private ScoreboardStrip shown;
        private TooltipProvider seenProvider;
        private TooltipController hiddenTooltip;
        private GameObject seenSelection;
        private ItemIcon pointed;
        // The row whose equipment, or key item, the pointer is on.
        private ScoreboardStrip pointedLoadout;
        private bool pointedKeyItem;
        private ItemIcon described;
        private float holdUntil;
        private ItemIndex highlighted = ItemIndex.None;

        // Read by the SevenUI.Tests plugin.
        public ScoreboardLayout Layout => layout;
        public bool Collapsed => collapsed;
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
            // The rows' container keeps laying the rows out, narrower by the column, which takes its right.
            rowsGroup = GetComponent<VerticalLayoutGroup>();
            RectOffset padding = rowsGroup ? rowsGroup.padding : new RectOffset();
            rowsPadding = new RectOffset(padding.left, padding.right + Mathf.RoundToInt(PaneColumnWidth), padding.top,
                padding.bottom);
            if (rowsGroup)
            {
                recorder.Record(rowsGroup);
                rowsGroup.padding = new RectOffset(rowsPadding.left, rowsPadding.right, rowsPadding.top, rowsPadding.bottom);
            }
            RectTransform column = NewObject(PaneColumnName, strips);
            recorder.Add(column.gameObject);
            column.gameObject.AddComponent<LayoutIgnorer>();
            column.anchorMin = new Vector2(1f, 0f);
            column.anchorMax = Vector2.one;
            column.pivot = Vector2.one;
            column.anchoredPosition = Vector2.zero;
            column.sizeDelta = new Vector2(PaneColumnWidth + padding.right, 0f);
            RectTransform line = NewObject("Divider", column);
            line.gameObject.AddComponent<LayoutIgnorer>();
            Image image = line.gameObject.AddComponent<Image>();
            image.color = Palette.RowDivider;
            image.raycastTarget = false;
            line.anchorMin = Vector2.zero;
            line.anchorMax = new Vector2(0f, 1f);
            line.offsetMin = Vector2.zero;
            line.offsetMax = new Vector2(DividerHeight, 0f);
            pane = new ScoreboardDetailPane(column, style, recorder);
        }

        // Each time the scoreboard opens: on the viewer's own row, with nothing described yet.
        private void OnEnable()
        {
            shown = null;
            stale = true;
            seenProvider = null;
            pointed = null;
            pointedLoadout = null;
            described = null;
            highlighted = ItemIndex.None;
            pane?.Hide();
        }

        // While the rows are collapsed, shows this player's items.
        public void Show(ScoreboardStrip strip)
        {
            if (!collapsed || !strip || strip == shown) return;
            shown = strip;
            stale = true;
        }

        // While the scoreboard is open: a few reads per row and icon, and the layout again only when the rows, their
        // items, the room or the shown player change.
        private void LateUpdate()
        {
            if (recorder == null) return;
            bool changed = Gather();
            foreach (ScoreboardItemGrid grid in grids)
            {
                if (grid.Refresh()) changed = true;
            }
            if (collapsed)
            {
                FollowSelection();
            }
            float limit = AvailableHeight();
            float width = strips.rect.width;
            if (changed || stale || !Mathf.Approximately(limit, laidOutLimit) || !Mathf.Approximately(width, laidOutWidth))
            {
                stale = false;
                laidOutLimit = limit;
                laidOutWidth = width;
                LayOut(width, limit);
            }
            foreach (ScoreboardItemGrid grid in grids)
            {
                grid.Apply();
            }
            Point(changed);
            foreach (ScoreboardItemGrid grid in grids)
            {
                grid.Highlight(highlighted, !collapsed);
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

        // The rows, each with its items or collapsed. Whether they collapse is decided from the items and the room
        // alone, not from how they are drawn, so it doesn't flip back and forth.
        private void LayOut(float width, float limit)
        {
            // How wide a row's items may be: the row is as wide as its container less the padding, which takes the
            // pane's column and a little more.
            float across = Mathf.Max(ScoreboardItemGrid.FullSlot, width - rowsPadding.left - rowsPadding.right -
                ScoreboardStyler.RowItemsLeft - ScoreboardStyler.RowPaddingX);
            float spacing = rowsGroup ? rowsGroup.spacing : 0f;
            float size = SlotSize(candidate => ExpandedHeight(candidate, across), limit);
            bool collapse = layout == ScoreboardLayout.Tabs && size < SmallestRowsSlot;
            if (collapse != collapsed)
            {
                collapsed = collapse;
                shown = null;
            }
            ScoreboardItemGrid open = collapsed ? ShownGrid() : null;
            if (open != null)
            {
                size = SlotSize(candidate => CollapsedHeight(open, candidate, across), limit);
            }
            rowsHeight = Mathf.Max(0, grids.Count - 1) * spacing;
            itemsHeight = 0f;
            for (int i = 0; i < grids.Count; i++)
            {
                ScoreboardItemGrid grid = grids[i];
                bool withItems = !collapsed || grid == open;
                grid.SetLayout(size, across);
                float items = grid.HeightAt(size, across);
                float height = collapsed ? ScoreboardStyler.RowHeight : RowHeight(grid, size, across);
                rowsHeight += height;
                SetRowHeight(grid.Strip, height);
                ShowDivider(grid, i > 0);
                ShowTabBar(grid, grid == open);
                ShowPart(grid.Cell, withItems);
                // Collapsed, the background ends where the items start, so the shown items have none under them.
                grid.FitBackground(collapsed ? ScoreboardStyler.RowItemsLeft : (float?)null);
                if (!withItems) continue;
                // Collapsed, the shown items start as far up as the first row's would, from their own row's top.
                float up = collapsed ? i * (ScoreboardStyler.RowHeight + spacing) : 0f;
                StatsSection.TopLeft(grid.Cell, ScoreboardStyler.RowItemsLeft, ScoreboardStyler.RowPaddingY - up, across, items);
                if (collapsed)
                {
                    itemsHeight = 2f * ScoreboardStyler.RowPaddingY + items;
                }
            }
        }

        // The largest slot the rows fit the room with, or the smallest.
        private static float SlotSize(System.Func<float, float> height, float limit)
        {
            foreach (float candidate in SlotSizes)
            {
                if (height(candidate) <= limit) return candidate;
            }
            return SlotSizes[SlotSizes.Length - 1];
        }

        private float ExpandedHeight(float size, float across)
        {
            float total = 0f;
            foreach (ScoreboardItemGrid grid in grids)
            {
                total += RowHeight(grid, size, across);
            }
            return total;
        }

        private float CollapsedHeight(ScoreboardItemGrid open, float size, float across) =>
            Mathf.Max(grids.Count * ScoreboardStyler.RowHeight, 2f * ScoreboardStyler.RowPaddingY + open.HeightAt(size, across));

        // The line between rows, none over the first.
        private static void ShowDivider(ScoreboardItemGrid grid, bool show)
        {
            Transform divider = grid.Background ? grid.Background.transform.Find(ScoreboardStyler.DividerName) : null;
            if (divider && divider.gameObject.activeSelf != show)
            {
                divider.gameObject.SetActive(show);
            }
        }

        // Only in the tabs setting, and lit only on the shown player's collapsed row.
        private static void ShowTabBar(ScoreboardItemGrid grid, bool show)
        {
            Transform bar = grid.Background ? grid.Background.transform.Find(TabBarName) : null;
            if (bar && bar.gameObject.activeSelf != show)
            {
                bar.gameObject.SetActive(show);
            }
        }

        // The items from the row's top line down, the row at least the plain row's height.
        private static float RowHeight(ScoreboardItemGrid grid, float size, float across) =>
            Mathf.Max(ScoreboardStyler.RowHeight, 2f * ScoreboardStyler.RowPaddingY + grid.HeightAt(size, across));

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

        // The shown player's row, or the viewer's own.
        private ScoreboardItemGrid ShownGrid()
        {
            foreach (ScoreboardItemGrid grid in grids)
            {
                if (grid.Strip == shown) return grid;
            }
            ScoreboardItemGrid own = OwnGrid();
            shown = own?.Strip;
            return own;
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

        // A collapsed row's items are faded out through a group of the mod's, which also keeps the pointer and the
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

        // A gamepad's selection in a collapsed row shows that row's player.
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

        // The item, equipment or key item the pointer (or a gamepad's selection) is on, from the tooltip it opened:
        // described, an item also lit, and the tooltip itself hidden, the pane saying what it would.
        private void Point(bool changed)
        {
            MPEventSystem events = Events();
            TooltipProvider provider = events ? events.currentTooltipProvider : null;
            if (provider != seenProvider)
            {
                seenProvider = provider;
                Resolve(provider);
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
            if (pointedLoadout && DescribeLoadout(changed))
            {
                highlighted = ItemIndex.None;
                HideTooltip(events);
                return;
            }
            if (Time.unscaledTime > holdUntil)
            {
                highlighted = ItemIndex.None;
            }
            if (changed && pane.Item != ItemIndex.None)
            {
                Redescribe();
            }
        }

        // What the tooltip's provider is: an item in a row, or a row's equipment or key item.
        private void Resolve(TooltipProvider provider)
        {
            pointed = null;
            pointedLoadout = null;
            if (!provider || !provider.transform.IsChildOf(strips)) return;
            pointed = provider.GetComponent<ItemIcon>();
            if (pointed) return;
            foreach (ScoreboardItemGrid grid in grids)
            {
                ScoreboardStrip strip = grid.Strip;
                if (strip.equipmentIcon && strip.equipmentIcon.tooltipProvider == provider)
                {
                    pointedLoadout = strip;
                    pointedKeyItem = false;
                    return;
                }
                if (strip.keyItemIcon && strip.keyItemIcon.tooltipProvider == provider)
                {
                    pointedLoadout = strip;
                    pointedKeyItem = true;
                    return;
                }
            }
        }

        // The pointed row's equipment or key item in the pane, again when it or the rows changed; false when the slot
        // is empty. Reads only.
        private bool DescribeLoadout(bool changed)
        {
            Inventory inventory = pointedLoadout.master ? pointedLoadout.master.inventory : null;
            if (!inventory) return false;
            if (pointedKeyItem)
            {
                KeyItemDef keyItem = inventory.keyItem;
                if (!keyItem || !pointedLoadout.keyItemIcon) return false;
                if (changed || pane.KeyItem != keyItem.keyItemIndex)
                {
                    pane.Show(keyItem, pointedLoadout.keyItemIcon, grids);
                }
            }
            else
            {
                EquipmentDef equipment = EquipmentCatalog.GetEquipmentDef(inventory.currentEquipmentIndex);
                if (!equipment || !pointedLoadout.equipmentIcon) return false;
                if (changed || pane.Equipment != equipment.equipmentIndex)
                {
                    pane.Show(equipment, pointedLoadout.equipmentIcon, grids);
                }
            }
            described = null;
            return true;
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

        // In its column, the board growing under the rows to hold a pane taller than them.
        private void PlacePane()
        {
            float paneHeight = 0f;
            if (pane.Shown)
            {
                pane.Place(DividerHeight + PaneInset, PaneInset, PaneColumnWidth - DividerHeight - 2f * PaneInset);
                paneHeight = 2f * PaneInset + pane.Height;
            }
            // The container grows under the rows to hold the pane, or the collapsed rows' shown items.
            float extra = Mathf.Max(paneHeight - rowsPadding.top - rowsPadding.bottom, itemsHeight) - rowsHeight;
            int bottom = rowsPadding.bottom + Mathf.CeilToInt(Mathf.Max(0f, extra));
            if (rowsGroup && rowsGroup.padding.bottom != bottom)
            {
                rowsGroup.padding = new RectOffset(rowsPadding.left, rowsPadding.right, rowsPadding.top, bottom);
            }
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
