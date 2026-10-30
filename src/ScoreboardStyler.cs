using System.Collections.Generic;
using System.Text;
using RoR2;
using RoR2.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SevenUI
{
    // Restyles the scoreboard (Tab) and its inspect panel as on the design canvas: a dark flat board under a header
    // strip of grey capitals lined up with the columns (ScoreboardHeaders), and flat rows split by thin lines, the
    // viewer's tinted in the accent (ScoreboardRow). In each row the portrait sits on a flat square, the name in bold
    // over the survivor and level and the item counts, the key item on a flat slot, the items without a box, the
    // equipment on a slot with its orange outline, and the money last, in gold at the right. The game keeps building
    // and filling the rows; its parts are resized and its money moved to the row's end.
    internal sealed class ScoreboardStyler
    {
        // Under the scoreboard panel.
        private const string BoardPath = "Container/StripContainer";
        private const string HeadersPath = "Container/HeaderGroup";
        private const string InspectPanelPath = "Container/InspectPanel/InspectPanelArea/InspectionPanel";
        private const string InspectVisualPath = InspectPanelPath + "/HorizontalBox/InspectIconContainer/InspectVisualBackground";
        private const string SidecarPanelPath = "InspectSidecar/SidecarItemBackground";
        private const string SidecarVisualPath = SidecarPanelPath + "/HorizontalBox/SidecarIconContainer/SidecarVisualBackground";
        // Under each row, and its headers' names.
        public const string RowBackgroundName = "LongBackground";
        private const string ClassName = "ClassBackground";
        private const string NameObjectName = "NameObject";
        private const string MoneyName = "TotalTextContainer";
        private const string KeyItemName = "KeyItemBackground";
        private const string ItemsName = "ItemsBackground";
        private const string EquipmentName = "EquipmentBackground";
        private static readonly (string header, string column, bool right)[] HeaderColumns =
        {
            ("Player Header", ClassName, false),
            ("Key Item Header", KeyItemName, false),
            ("Item Header", ItemsName, false),
            ("Equipment Header", EquipmentName, false),
        };
        public const string HeaderStripName = "SevenUIScoreboardHeader";
        private const string MoneyHeaderName = "SevenUIMoneyHeader";
        // The game has no word for its money to translate, so the money's header shows in English only.
        private const string MoneyHeader = "Money";
        private const string English = "en";
        private const string DividerName = "SevenUIRowDivider";
        private const string RowTextsName = "SevenUIRowTexts";
        private const string OutlineName = "SevenUIEquipmentOutline";

        // The design canvas's sizes, in its pixels, which the scoreboard's units match: rows 92px tall with 16px by 20px
        // of padding and 24px between columns; 52px slots; the player's name block 224px wide, its lines 19px, 14px
        // and 13px, 3px apart; the money's column 120px with 20px text; and the headers 12px.
        private const float RowHeight = 92f;
        private const int RowPaddingX = 20;
        private const int RowPaddingY = 16;
        private const float ColumnGap = 24f;
        private const float SlotSize = 52f;
        private const float NameWidth = 224f;
        private const float NameFontSize = 19f;
        private const float SurvivorFontSize = 14f;
        private const float CountsFontSize = 13f;
        private const float LineGap = 3f;
        private const float LineHeight = 1.2f;
        private const float MoneyWidth = 120f;
        private const float MoneyFontSize = 20f;
        private const float HeaderFontSize = 12f;
        private const float DividerHeight = 1f;
        private const float OutlineEdge = 2f;
        // The outline sprite's edge, in its pixels (FlatSprite.Outline).
        private const float OutlineEdgePixels = 2f;

        private readonly Transform root;
        private readonly StyleRecorder recorder = new StyleRecorder();
        private readonly List<ScoreboardController> boards = new List<ScoreboardController>();
        private readonly List<ScoreboardStrip> rows = new List<ScoreboardStrip>();
        private readonly HashSet<ScoreboardStrip> styled = new HashSet<ScoreboardStrip>();

        public ScoreboardStyler(Transform root)
        {
            this.root = root;
        }

        public void Apply()
        {
            root.GetComponentsInChildren(true, boards);
            foreach (ScoreboardController board in boards)
            {
                StyleBoard(board.transform);
            }
            boards.Clear();
        }

        public void Restore()
        {
            recorder.Restore();
            styled.Clear();
        }

        private void StyleBoard(Transform board)
        {
            Fill(Find<Image>(board, BoardPath), Palette.ScoreboardPanel);
            Fill(Find<Image>(board, InspectPanelPath), Palette.ScoreboardPanel);
            Fill(Find<Image>(board, InspectVisualPath), Palette.ItemSlot);
            Fill(Find<Image>(board, SidecarPanelPath), Palette.ScoreboardPanel);
            Fill(Find<Image>(board, SidecarVisualPath), Palette.ItemSlot);

            Transform headers = board.Find(HeadersPath);
            if (headers && !headers.Find(HeaderStripName))
            {
                StyleHeaders(headers, board.Find(BoardPath) as RectTransform);
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

        // The strip behind the headers joins the board under it; the game's headers become grey capitals, and a money
        // header is added for the money's column.
        private void StyleHeaders(Transform headers, RectTransform board)
        {
            GameObject strip = NewUIObject(HeaderStripName, headers);
            strip.transform.SetAsFirstSibling();
            Stretch((RectTransform)strip.transform);
            Image panel = strip.AddComponent<Image>();
            panel.color = Palette.ScoreboardPanel;
            panel.raycastTarget = false;
            GameObject tint = NewUIObject("Tint", strip.transform);
            Stretch((RectTransform)tint.transform);
            Image tintImage = tint.AddComponent<Image>();
            tintImage.color = Palette.HeaderStrip;
            tintImage.raycastTarget = false;
            recorder.Add(strip);

            List<TMP_Text> labels = new List<TMP_Text>();
            List<string> columns = new List<string>();
            List<bool> right = new List<bool>();
            TMP_Text first = null;
            foreach ((string header, string column, bool rightAligned) in HeaderColumns)
            {
                TMP_Text label = headers.Find(header)?.GetComponent<TMP_Text>();
                if (!label) continue;
                first = first ? first : label;
                recorder.Record(label);
                recorder.RecordRect(label.rectTransform);
                StyleHeader(label, rightAligned);
                labels.Add(label);
                columns.Add(column);
                right.Add(rightAligned);
            }
            if (first)
            {
                GameObject money = NewUIObject(MoneyHeaderName, headers);
                TextMeshProUGUI label = money.AddComponent<TextMeshProUGUI>();
                label.font = first.font;
                label.fontSharedMaterial = first.fontSharedMaterial;
                label.text = Language.currentLanguageName == English ? MoneyHeader : "";
                label.enableWordWrapping = false;
                label.raycastTarget = false;
                label.rectTransform.sizeDelta = first.rectTransform.sizeDelta;
                StyleHeader(label, true);
                recorder.Add(money);
                labels.Add(label);
                columns.Add(MoneyName);
                right.Add(true);
            }
            ScoreboardHeaders follower = headers.gameObject.AddComponent<ScoreboardHeaders>();
            follower.Rows = board;
            follower.Headers = labels.ToArray();
            follower.Columns = columns.ToArray();
            follower.RightAligned = right.ToArray();
            recorder.Add(follower);
        }

        private static void StyleHeader(TMP_Text label, bool right)
        {
            HeaderStyle.Apply(label);
            label.fontSize = HeaderFontSize;
            label.alignment = right ? TextAlignmentOptions.MidlineRight : TextAlignmentOptions.MidlineLeft;
        }

        private void StyleRow(ScoreboardStrip row)
        {
            Transform background = row.transform.Find(RowBackgroundName);
            Image backgroundImage = background ? background.GetComponent<Image>() : null;
            HorizontalLayoutGroup layout = background ? background.GetComponent<HorizontalLayoutGroup>() : null;
            if (!backgroundImage || !layout) return;
            Fill(backgroundImage, Color.clear);
            // The game's outline around the selected row; the viewer's row is tinted instead.
            recorder.Disable(row.GetComponent<RawImage>());

            LayoutElement rowElement = row.GetComponent<LayoutElement>();
            if (rowElement)
            {
                recorder.Record(rowElement);
                rowElement.minHeight = RowHeight;
                rowElement.preferredHeight = RowHeight;
            }
            RectTransform rowRect = (RectTransform)row.transform;
            recorder.RecordRect(rowRect);
            rowRect.sizeDelta = new Vector2(rowRect.sizeDelta.x, RowHeight);

            recorder.Record(layout);
            bool expandHeight = layout.childForceExpandHeight;
            TextAnchor alignment = layout.childAlignment;
            recorder.OnRestore(() =>
            {
                if (!layout) return;
                layout.childForceExpandHeight = expandHeight;
                layout.childAlignment = alignment;
            });
            layout.padding = new RectOffset(RowPaddingX, RowPaddingX, RowPaddingY, RowPaddingY);
            layout.spacing = ColumnGap;
            layout.childForceExpandHeight = false;
            layout.childAlignment = TextAnchor.UpperLeft;

            Slot(background.Find(ClassName), Palette.Trough);
            Slot(background.Find(KeyItemName), Palette.Trough);
            Transform equipment = background.Find(EquipmentName);
            Slot(equipment, Palette.Trough);
            if (equipment && !equipment.Find(OutlineName))
            {
                AddEquipmentOutline(equipment);
            }
            // The items keep their gamepad outline, shown only while lit.
            Image items = row.itemInventoryDisplay ? row.itemInventoryDisplay.GetComponent<Image>() : null;
            Outline(items);
            Outline(row.nameFocusHighlight);

            Transform money = background.Find(MoneyName);
            if (money)
            {
                int index = money.GetSiblingIndex();
                recorder.OnRestore(() =>
                {
                    if (money)
                    {
                        money.SetSiblingIndex(index);
                    }
                });
                money.SetAsLastSibling();
                Size(money.GetComponent<LayoutElement>(), MoneyWidth, MoneyFontSize * LineHeight);
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
            Size(nameObject ? nameObject.GetComponent<LayoutElement>() : null, NameWidth,
                (NameFontSize + SurvivorFontSize + CountsFontSize) * LineHeight + 2f * LineGap);
            if (nameObject && row.nameLabel && !nameObject.Find(RowTextsName))
            {
                StyleName(row, nameObject, backgroundImage, items);
            }
            if (!background.Find(DividerName))
            {
                AddDivider(background);
            }
        }

        // The name on the first of three lines; the survivor and level, and the counts, on the mod's.
        private void StyleName(ScoreboardStrip row, Transform nameObject, Image background, Image items)
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
            TMP_Text survivor = AddText("Survivor", holder.transform, name, SurvivorFontSize, Palette.HeaderText);
            Line(survivor.rectTransform, ref y, SurvivorFontSize);
            TMP_Text counts = AddText("Counts", holder.transform, name, CountsFontSize, Palette.HeaderText);
            Line(counts.rectTransform, ref y, CountsFontSize);

            ScoreboardRow follower = holder.AddComponent<ScoreboardRow>();
            follower.Strip = row;
            follower.Hud = root.GetComponent<HUD>();
            follower.Background = background;
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

        private void AddEquipmentOutline(Transform equipment)
        {
            GameObject outline = NewUIObject(OutlineName, equipment);
            Stretch((RectTransform)outline.transform);
            Image image = outline.AddComponent<Image>();
            image.sprite = FlatSprite.Outline;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = OutlineEdgePixels / OutlineEdge;
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
            output.AppendLine($"  restyled scoreboard values: {recorder.Count}");
        }
    }
}
