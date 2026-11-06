using System.Collections.Generic;
using System.Text;
using RoR2;
using RoR2.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SevenUI
{
    // Restyles the scoreboard (Tab) and its inspect panel as on the design canvas: a dark flat board without the
    // game's headers, and each player's row with the portrait on a flat square, the name in bold over the survivor and
    // level and the item counts (ScoreboardRow), the key item on a flat slot, the equipment on a slot with its orange
    // outline, and the money in gold. The game keeps building and filling the rows; its parts are resized and moved,
    // and the board's own component (ScoreboardItemsBoard) lays the items out and describes the one pointed at, in the
    // layout the ScoreboardLayout setting picks:
    // - Rows: one line per player, split by thin lines, the viewer's tinted in the accent: the portrait, the name block
    //   with the money at the right of the name, the key item, the equipment, then the items.
    // - Tabs: each row is a line in a list of the players down the board's left: the portrait, the name block with the
    //   money at the right of the name, then the key item and the equipment, smaller; the shown player's items sit in
    //   the board's middle.
    internal sealed class ScoreboardStyler
    {
        // Under the scoreboard panel.
        private const string BoardPath = "Container/StripContainer";
        // The game's column headers, which the rows' columns no longer line up with.
        private const string HeadersPath = "Container/HeaderGroup";
        private const string InspectPanelPath = "Container/InspectPanel/InspectPanelArea/InspectionPanel";
        private const string InspectVisualPath = InspectPanelPath + "/HorizontalBox/InspectIconContainer/InspectVisualBackground";
        private const string SidecarPanelPath = "InspectSidecar/SidecarItemBackground";
        private const string SidecarVisualPath = SidecarPanelPath + "/HorizontalBox/SidecarIconContainer/SidecarVisualBackground";
        // Under each row, and its headers' names.
        public const string RowBackgroundName = "LongBackground";
        private const string ClassName = "ClassBackground";
        // Under the portrait's cell: the game's picture, and the mod's square behind it.
        private const string PortraitPictureName = "ClassIcon";
        private const string PortraitSquareName = "SevenUIPortraitSquare";
        private const string NameObjectName = "NameObject";
        private const string MoneyName = "TotalTextContainer";
        private const string KeyItemName = "KeyItemBackground";
        private const string EquipmentName = "EquipmentBackground";
        private const string PlayersName = "PlayersBackground";
        public const string DividerName = "SevenUIRowDivider";
        private const string RowTextsName = "SevenUIRowTexts";
        private const string OutlineName = "SevenUIEquipmentOutline";

        // The design canvas's sizes, in its pixels, which the scoreboard's units match: rows at least 92px tall with
        // 16px by 20px of padding and 16px between columns; 52px slots; the player's name block 176px wide, its lines
        // 19px, 14px and 13px, 3px apart; and the money's text 20px.
        public const float RowHeight = 92f;
        public const int RowPaddingX = 20;
        public const int RowPaddingY = 16;
        private const float RowColumnGap = 16f;
        // Between the portrait and the name block; the row lays its columns out a column gap apart, so the block's
        // texts reach back into the gap by the difference.
        private const float NameGap = 12f;
        public const float SlotSize = 52f;
        private const float NameWidth = 176f;
        private const float NameFontSize = 19f;
        private const float SurvivorFontSize = 14f;
        private const float CountsFontSize = 13f;
        private const float LineGap = 3f;
        private const float LineHeight = 1.2f;
        // The name block's three lines, which the portrait beside it is centered on, as on the canvas.
        public const float NameBlockHeight = (NameFontSize + SurvivorFontSize + CountsFontSize) * LineHeight + 2f * LineGap;
        private const float MoneyFontSize = 20f;
        // In a row, where the items start: after the portrait, the name block, the key item and the equipment.
        public const float RowItemsLeft = RowPaddingX + SlotSize + RowColumnGap + NameWidth + RowColumnGap + SlotSize +
                                          RowColumnGap + SlotSize + RowColumnGap;
        private const float RowNameLeft = RowPaddingX + SlotSize + RowColumnGap;
        // A line in the tabs layout's list: 10px by 14px of padding, after the shown line's 3px bar; a 40px portrait,
        // 10px to the name block, and 10px after it the key item and the equipment, 36px, 6px apart, centered on it.
        // The slots are the rows' drawn smaller, so their insides keep the rows' proportions.
        private const int ListPaddingLeft = 17;
        private const int ListPaddingRight = 14;
        private const int ListPaddingY = 10;
        public const float ListRowHeight = 2f * ListPaddingY + NameBlockHeight;
        private const float ListPortraitSize = 40f;
        private const float ListGap = 10f;
        private const float ListSlotSize = 36f;
        private const float ListSlotGap = 6f;
        private const float ListSlotScale = ListSlotSize / SlotSize;
        private const float ListSlotsLeft = ScoreboardItemsBoard.ListWidth - ListPaddingRight - 2f * ListSlotSize - ListSlotGap;
        private const float ListSlotTop = ListPaddingY + (NameBlockHeight - ListSlotSize) / 2f;
        private const float ListNameLeft = ListPaddingLeft + ListPortraitSize + ListGap;
        private const float ListNameWidth = ListSlotsLeft - ListGap - ListNameLeft;
        // The canvas's item slots: 40px, 4px apart, wrapping (ScoreboardItemGrid).
        private const float ItemSlotSize = 40f;
        private const float ItemGap = 4f;
        // The game's limit on how tall its items may grow before it shrinks them; the board sizes the items instead.
        private const float UnboundedHeight = 100000f;
        // The canvas's board, rgba(0,0,0,0.62) over its #1a1d24 background: what its header strip and viewer's row tint
        // were chosen over.
        public static readonly Color CanvasBoard = new Color(0.039f, 0.043f, 0.054f, 1f);
        private const string ContainerName = "Container";
        private const float DividerHeight = 1f;
        private const float OutlineEdge = 2f;
        // The outline sprite's edge, in its pixels (FlatSprite.Outline).
        private const float OutlineEdgePixels = 2f;

        private readonly HUD hud;
        private readonly Transform root;
        private readonly StyleRecorder recorder = new StyleRecorder();
        private readonly List<ScoreboardController> boards = new List<ScoreboardController>();
        private readonly List<ScoreboardStrip> rows = new List<ScoreboardStrip>();
        private readonly HashSet<ScoreboardStrip> styled = new HashSet<ScoreboardStrip>();
        private ScoreboardPlacement placement;
        private ScoreboardItemsBoard itemsBoard;
        // The layout the rows are styled in. Changing the setting restores the scoreboard first
        // (SevenUIController), so it is read afresh only over a board in the game's look.
        private ScoreboardLayout layout;

        public ScoreboardStyler(HUD hud, Transform root)
        {
            this.hud = hud;
            this.root = root;
        }

        public void Apply()
        {
            layout = SevenUIPlugin.Settings.ScoreboardLayout.Value;
            root.GetComponentsInChildren(true, boards);
            foreach (ScoreboardController board in boards)
            {
                StyleBoard(board.transform);
            }
            boards.Clear();
        }

        public void Restore()
        {
            // Destroyed only at the frame's end, it would otherwise lay the items out again over the game's.
            if (itemsBoard)
            {
                itemsBoard.enabled = false;
            }
            recorder.Restore();
            styled.Clear();
            placement = null;
            itemsBoard = null;
        }

        // While the scoreboard is open: a few projected points. The items board keeps the items itself.
        public void Update()
        {
            if (placement != null && placement.Alive)
            {
                placement.Update(SevenUIPlugin.Settings.ScoreboardBesideRunInfo.Value);
            }
            styled.RemoveWhere(row => !row);
        }

        private void StyleBoard(Transform board)
        {
            Fill(Find<Image>(board, BoardPath), Palette.ScoreboardPanel);
            Fill(Find<Image>(board, InspectPanelPath), Palette.ScoreboardPanel);
            Fill(Find<Image>(board, InspectVisualPath), Palette.ItemSlot);
            Fill(Find<Image>(board, SidecarPanelPath), Palette.ScoreboardPanel);
            Fill(Find<Image>(board, SidecarVisualPath), Palette.ItemSlot);
            VerticalLayoutGroup container = Find<VerticalLayoutGroup>(board, ContainerName);
            if ((placement == null || !placement.Alive) && container)
            {
                placement = new ScoreboardPlacement((RectTransform)board, container, root, recorder);
            }

            Transform headers = board.Find(HeadersPath);
            RectTransform strips = board.Find(BoardPath) as RectTransform;
            if (headers && headers.gameObject.activeSelf)
            {
                recorder.SetActive(headers.gameObject, false);
            }
            // Before the rows, which the tabs layout points at it.
            if (strips && !strips.GetComponent<ScoreboardItemsBoard>())
            {
                AddItemsBoard(board, strips, headers);
            }

            board.GetComponentsInChildren(true, rows);
            foreach (ScoreboardStrip row in rows)
            {
                if (styled.Add(row))
                {
                    StyleRow(row);
                }
            }
            rows.Clear();
        }

        // The tabs layout places the rows itself, and sizes their container to the tabs and what is under them. The
        // headers' first text is the style of the board's own texts.
        private void AddItemsBoard(Transform board, RectTransform strips, Transform headers)
        {
            recorder.RecordRect(strips);
            if (layout == ScoreboardLayout.Tabs)
            {
                recorder.Disable(strips.GetComponent<VerticalLayoutGroup>());
                recorder.Disable(strips.GetComponent<ContentSizeFitter>());
            }
            itemsBoard = strips.gameObject.AddComponent<ScoreboardItemsBoard>();
            recorder.Add(itemsBoard);
            TMP_Text style = headers ? headers.GetComponentInChildren<TMP_Text>(true) : null;
            itemsBoard.Init(layout, recorder, styled, hud, (RectTransform)board, style);
        }

        private void StyleRow(ScoreboardStrip row)
        {
            Transform background = row.transform.Find(RowBackgroundName);
            Image backgroundImage = background ? background.GetComponent<Image>() : null;
            HorizontalLayoutGroup columns = background ? background.GetComponent<HorizontalLayoutGroup>() : null;
            if (!backgroundImage || !columns) return;
            bool tabs = layout == ScoreboardLayout.Tabs;
            Fill(backgroundImage, Color.clear);
            // The game's outline around the selected row; the viewer's row is tinted instead.
            recorder.Disable(row.GetComponent<RawImage>());

            // The rows' heights follow their items (ScoreboardItemsBoard); this is a row without any.
            float height = tabs ? ListRowHeight : RowHeight;
            float gap = tabs ? ListGap : RowColumnGap;
            LayoutElement rowElement = row.GetComponent<LayoutElement>();
            if (rowElement)
            {
                recorder.Record(rowElement);
                rowElement.minHeight = height;
                rowElement.preferredHeight = height;
            }
            RectTransform rowRect = (RectTransform)row.transform;
            recorder.RecordRect(rowRect);
            rowRect.sizeDelta = new Vector2(rowRect.sizeDelta.x, height);

            recorder.Record(columns);
            bool expandWidth = columns.childForceExpandWidth;
            bool expandHeight = columns.childForceExpandHeight;
            TextAnchor alignment = columns.childAlignment;
            recorder.OnRestore(() =>
            {
                if (!columns) return;
                columns.childForceExpandWidth = expandWidth;
                columns.childForceExpandHeight = expandHeight;
                columns.childAlignment = alignment;
            });
            columns.padding = tabs
                ? new RectOffset(ListPaddingLeft, ListPaddingRight, ListPaddingY, ListPaddingY)
                : new RectOffset(RowPaddingX, RowPaddingX, RowPaddingY, RowPaddingY);
            columns.spacing = gap;
            // The spare width is left at a row's right, not given to every column.
            columns.childForceExpandWidth = false;
            columns.childForceExpandHeight = false;
            columns.childAlignment = TextAnchor.UpperLeft;

            CenterPortrait(background.Find(ClassName), tabs ? ListPortraitSize : SlotSize);
            Transform keyItem = background.Find(KeyItemName);
            Slot(keyItem, Palette.Trough);
            Transform equipment = background.Find(EquipmentName);
            Slot(equipment, Palette.Trough);
            if (equipment && !equipment.Find(OutlineName))
            {
                // Its edge drawn wider in the list, where the slot is drawn smaller, so it shows as wide.
                AddEquipmentOutline(equipment, tabs ? OutlineEdge / ListSlotScale : OutlineEdge);
            }
            if (row.itemInventoryDisplay)
            {
                FitItems(row.itemInventoryDisplay);
                // Placed by the board, under the row's top line or under the tabs.
                TakeOutOfLayout(row.itemInventoryDisplay.transform);
            }
            if (tabs)
            {
                TakeOutOfLayout(keyItem);
                TakeOutOfLayout(equipment);
                PlaceListSlot(keyItem, ListSlotsLeft);
                PlaceListSlot(equipment, ListSlotsLeft + ListSlotSize + ListSlotGap);
            }
            // An empty picture of the game's, which would still take a column gap from the name.
            Transform players = background.Find(PlayersName);
            if (players && !players.GetComponent<LayoutIgnorer>())
            {
                recorder.Add(players.gameObject.AddComponent<LayoutIgnorer>());
            }
            // The items keep their gamepad outline, shown only while lit.
            Image items = row.itemInventoryDisplay ? row.itemInventoryDisplay.GetComponent<Image>() : null;
            Outline(items);
            Outline(row.nameFocusHighlight);

            Transform money = background.Find(MoneyName);
            if (money)
            {
                // At the right of the name's line, the name cut short before it (ScoreboardRow).
                TakeOutOfLayout(money);
                StatsSection.TopLeft((RectTransform)money, tabs ? ListNameLeft : RowNameLeft, tabs ? ListPaddingY : RowPaddingY,
                    tabs ? ListNameWidth : NameWidth, MoneyFontSize * LineHeight);
            }
            if (row.moneyText)
            {
                recorder.Record(row.moneyText);
                recorder.RecordRect(row.moneyText.rectTransform);
                RectTransform rect = row.moneyText.rectTransform;
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = Vector2.one;
                rect.pivot = new Vector2(1f, 1f);
                rect.anchoredPosition = Vector2.zero;
                rect.sizeDelta = new Vector2(0f, MoneyFontSize * LineHeight);
                row.moneyText.fontSize = MoneyFontSize;
                row.moneyText.fontStyle |= FontStyles.Bold;
                row.moneyText.color = Palette.Money;
                row.moneyText.alignment = TextAlignmentOptions.TopRight;
            }
            if (row.itemCountText)
            {
                recorder.Record(row.itemCountText);
                row.itemCountText.alpha = 0f;
            }

            Transform nameObject = background.Find(NameObjectName);
            LayoutElement nameElement = nameObject ? nameObject.GetComponent<LayoutElement>() : null;
            Size(nameElement, tabs ? ListNameWidth : NameWidth, NameBlockHeight);
            if (nameObject && row.nameLabel && !nameObject.Find(RowTextsName))
            {
                StyleName(row, nameObject, backgroundImage, items, tabs, (tabs ? ListGap : NameGap) - gap);
            }
            if (tabs)
            {
                AddTabBar(background);
                ScoreboardTab tab = row.gameObject.AddComponent<ScoreboardTab>();
                tab.Board = itemsBoard;
                tab.Strip = row;
                recorder.Add(tab);
                return;
            }
            if (!background.Find(DividerName))
            {
                AddDivider(background);
            }
        }

        // Out of the row's columns, for the board to place.
        private void TakeOutOfLayout(Transform part)
        {
            LayoutElement element = part ? part.GetComponent<LayoutElement>() : null;
            if (!element) return;
            recorder.RecordRect((RectTransform)part);
            if (element.ignoreLayout) return;
            recorder.OnRestore(() =>
            {
                if (element) element.ignoreLayout = false;
            });
            element.ignoreLayout = true;
        }

        // In the list, at x from the row's left, centered on the name block, drawn smaller. Its size stays the rows' slot.
        private void PlaceListSlot(Transform slot, float x)
        {
            if (!slot) return;
            RectTransform rect = (RectTransform)slot;
            Vector3 scale = rect.localScale;
            recorder.OnRestore(() =>
            {
                if (rect) rect.localScale = scale;
            });
            StatsSection.TopLeft(rect, x, ListSlotTop, SlotSize, SlotSize);
            rect.localScale = new Vector3(ListSlotScale, ListSlotScale, 1f);
        }

        // The shown player's accent line down the left of their line in the list, which the board turns on.
        private void AddTabBar(Transform background)
        {
            if (background.Find(ScoreboardItemsBoard.TabBarName)) return;
            GameObject bar = NewUIObject(ScoreboardItemsBoard.TabBarName, background);
            bar.AddComponent<LayoutIgnorer>();
            RectTransform rect = (RectTransform)bar.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(ScoreboardItemsBoard.TabBarWidth, 0f);
            Image image = bar.AddComponent<Image>();
            image.color = Palette.Accent;
            image.raycastTarget = false;
            bar.SetActive(false);
            recorder.Add(bar);
        }

        // The name on the first of three lines; the survivor and level, and the counts, on the mod's. The texts start
        // inset into the block, or reach back out of it when negative.
        private void StyleName(ScoreboardStrip row, Transform nameObject, Image background, Image items, bool tabs, float inset)
        {
            TMP_Text name = row.nameLabel;
            recorder.Record(name);
            recorder.RecordRect(name.rectTransform);
            name.fontSize = NameFontSize;
            name.fontStyle |= FontStyles.Bold;
            name.alignment = TextAlignmentOptions.TopLeft;
            float y = 0f;
            Line(name.rectTransform, ref y, NameFontSize);

            GameObject holder = NewUIObject(RowTextsName, nameObject);
            Stretch((RectTransform)holder.transform);
            ((RectTransform)holder.transform).offsetMin = new Vector2(inset, 0f);
            Vector2 nameOffset = name.rectTransform.offsetMin;
            name.rectTransform.offsetMin = new Vector2(inset, nameOffset.y);
            TMP_Text survivor = AddText("Survivor", holder.transform, name, SurvivorFontSize, Palette.HeaderText);
            Line(survivor.rectTransform, ref y, SurvivorFontSize);
            TMP_Text counts = AddText("Counts", holder.transform, name, CountsFontSize, Palette.HeaderText);
            Line(counts.rectTransform, ref y, CountsFontSize);

            ScoreboardRow follower = holder.AddComponent<ScoreboardRow>();
            follower.Strip = row;
            follower.Hud = root.GetComponent<HUD>();
            follower.Background = background;
            follower.TintOwnRow = !tabs;
            follower.Name = name;
            follower.Money = row.moneyText;
            follower.Survivor = survivor;
            follower.Counts = counts;
            follower.FocusOutlines = new[] { row.nameFocusHighlight, items };
            foreach (Image outline in follower.FocusOutlines)
            {
                if (outline)
                {
                    recorder.SetEnabled(outline, outline.enabled);
                }
            }
            recorder.Add(holder);
        }

        // A line across the name block, under the ones above it.
        private static void Line(RectTransform rect, ref float y, float size)
        {
            float height = size * LineHeight;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(0f, y - height);
            rect.offsetMax = new Vector2(0f, y);
            y -= height + LineGap;
        }

        private static TMP_Text AddText(string name, Transform parent, TMP_Text like, float size, Color color)
        {
            GameObject line = NewUIObject(name, parent);
            TextMeshProUGUI text = line.AddComponent<TextMeshProUGUI>();
            text.font = like.font;
            text.fontSharedMaterial = like.fontSharedMaterial;
            text.fontSize = size;
            text.color = color;
            text.alignment = TextAlignmentOptions.TopLeft;
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.raycastTarget = false;
            return text;
        }

        // The game sizes its icons to fit the items cell, shrinking them once they fill its height. The board lays the
        // icons out itself (ScoreboardItemGrid), so the game's limit is lifted, its icons keep the canvas's size, and
        // its steps are the icons' own size plus the gap: the game rounds the step's width to whole units when it
        // scales the icons, so it is set whole. Restoring puts the icons' pivots back, which the board moves to their
        // corners, before the game lays them out again.
        private void FitItems(ItemInventoryDisplay display)
        {
            RectTransform prefab = display.itemIconPrefab ? display.itemIconPrefab.transform as RectTransform : null;
            float iconWidth = prefab ? prefab.rect.width : 0f;
            if (iconWidth <= 0f) return;
            float stepWidth = display.itemIconPrefabWidth;
            float maxWidth = display.maxIconWidth;
            float maxHeight = display.maxHeight;
            Vector2 pivot = prefab.pivot;
            recorder.OnRestore(() =>
            {
                if (!display) return;
                display.itemIconPrefabWidth = stepWidth;
                display.maxIconWidth = maxWidth;
                display.maxHeight = maxHeight;
                foreach (ItemIcon icon in display.itemIcons)
                {
                    if (icon && icon.rectTransform)
                    {
                        icon.rectTransform.pivot = pivot;
                    }
                }
                // Reads the inventory and draws, then places every icon at the game's size.
                display.UpdateDisplay();
                display.LayoutAllIcons();
            });
            float step = ItemSlotSize + ItemGap;
            display.itemIconPrefabWidth = Mathf.Round(iconWidth * step / ItemSlotSize);
            display.maxIconWidth = step;
            display.maxHeight = UnboundedHeight;
            display.UpdateDisplay();
        }

        // The row's columns line up at the top, as on the canvas, which centers only the portrait, on the name block
        // beside it. The portrait's cell stays the row's slot at the top, and its square and picture are drawn lower in
        // it by half the name block's extra height.
        private void CenterPortrait(Transform portrait, float size)
        {
            if (!portrait) return;
            Image cell = portrait.GetComponent<Image>();
            Fill(cell, Color.clear);
            Size(portrait.GetComponent<LayoutElement>(), size, size);
            if (portrait.Find(PortraitSquareName)) return;
            float down = (NameBlockHeight - size) / 2f;
            GameObject square = NewUIObject(PortraitSquareName, portrait);
            square.transform.SetAsFirstSibling();
            RectTransform squareRect = (RectTransform)square.transform;
            Stretch(squareRect);
            squareRect.offsetMin = new Vector2(0f, -down);
            squareRect.offsetMax = new Vector2(0f, -down);
            Image squareImage = square.AddComponent<Image>();
            squareImage.color = Palette.Trough;
            squareImage.raycastTarget = false;
            recorder.Add(square);
            RectTransform picture = portrait.Find(PortraitPictureName) as RectTransform;
            if (picture)
            {
                recorder.RecordRect(picture);
                picture.offsetMin += new Vector2(0f, -down);
                picture.offsetMax += new Vector2(0f, -down);
            }
        }

        private void Slot(Transform slot, Color color)
        {
            if (!slot) return;
            Fill(slot.GetComponent<Image>(), color);
            Size(slot.GetComponent<LayoutElement>(), SlotSize, SlotSize);
        }

        // Width and height, as the canvas's; LookingGlass doesn't size these.
        private void Size(LayoutElement element, float width, float height)
        {
            if (!element) return;
            recorder.Record(element);
            float minWidth = element.minWidth;
            float preferredWidth = element.preferredWidth;
            recorder.OnRestore(() =>
            {
                if (!element) return;
                element.minWidth = minWidth;
                element.preferredWidth = preferredWidth;
            });
            element.minWidth = width;
            element.preferredWidth = width;
            element.minHeight = height;
            element.preferredHeight = height;
        }

        private void AddEquipmentOutline(Transform equipment, float edge)
        {
            GameObject outline = NewUIObject(OutlineName, equipment);
            Stretch((RectTransform)outline.transform);
            Image image = outline.AddComponent<Image>();
            image.sprite = FlatSprite.Outline;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = OutlineEdgePixels / edge;
            image.color = Palette.Equipment;
            image.raycastTarget = false;
            recorder.Add(outline);
        }

        // A thin line along the row's top, outside its layout.
        private void AddDivider(Transform background)
        {
            GameObject divider = NewUIObject(DividerName, background);
            divider.AddComponent<LayoutIgnorer>();
            RectTransform rect = (RectTransform)divider.transform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(0f, -DividerHeight);
            rect.offsetMax = Vector2.zero;
            Image image = divider.AddComponent<Image>();
            image.color = Palette.RowDivider;
            image.raycastTarget = false;
            recorder.Add(divider);
        }

        private void Fill(Image image, Color color)
        {
            if (!image) return;
            recorder.Record(image);
            image.sprite = null;
            image.type = Image.Type.Simple;
            image.color = color;
        }

        // On a gamepad the game tints these yellow while focused and black after, so they become thin outlines that
        // keep that tint.
        private void Outline(Image image)
        {
            if (!image) return;
            recorder.Record(image);
            image.sprite = FlatSprite.Outline;
            image.type = Image.Type.Sliced;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private static GameObject NewUIObject(string name, Transform parent)
        {
            GameObject created = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
            created.layer = parent.gameObject.layer;
            created.transform.SetParent(parent, false);
            return created;
        }

        private static T Find<T>(Transform parent, string path) where T : Component
        {
            Transform found = parent.Find(path);
            return found ? found.GetComponent<T>() : null;
        }

        public void Describe(StringBuilder output)
        {
            output.AppendLine($"  restyled scoreboard values: {recorder.Count}, layout {layout}, " +
                              $"rows laid out {(itemsBoard ? itemsBoard.Grids.Count : 0)}");
        }
    }
}
