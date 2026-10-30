using System.Collections.Generic;
using RoR2.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SevenUI
{
    // Restyles the panels under the run info in the top right (the artifact panel, the objective panel, and panels
    // other mods add there, such as LookingGlass's stats) as flat translucent panels, with a header strip on the
    // objective panel and its rows laid out as on the design canvas (ObjectiveRow), and LookingGlass's stats in two
    // columns under a header strip of their own (StatsGrid).
    internal sealed class RightInfoBarStyler
    {
        // Vanilla object names. LookingGlass looks up ObjectivePanel and its Label by these, so they are never renamed.
        private const string UpperRightClusterPath = "MainContainer/MainUIArea/SpringCanvas/UpperRightCluster";
        private const string RightInfoBarName = "RightInfoBar";
        private const string ObjectivePanelName = "ObjectivePanel";
        private const string TitleName = "Label";
        private const string StripContainerName = "StripContainer";
        private const string HeaderStripName = "SevenUIHeaderStrip";
        private const string StatsTitleName = "SevenUIStatsTitle";
        public const string StatsGridName = "SevenUIStatsGrid";
        private const string StatsSource = "LookingGlass";
        // Above a LayoutElement's default of 1.
        private const int StatsLayoutPriority = 2;

        // The design canvas's sizes, in its pixels, scaled to the run info's units. The rows' own height holds most
        // of the 10px above and below them.
        private const float Canvas = RunInfoUnits.PerCanvasPixel;
        private const float PanelGap = 8f * Canvas;
        private const int RowsPadding = 4;
        private const float HeaderHeight = 26f * Canvas;
        private const float HeaderInset = 14f * Canvas;
        private const float HeaderFontSize = 12f * Canvas;
        // The stats' 14px rows 4px apart, 8px under the strip and 10px above the panel's bottom, in columns 16px apart.
        private const float StatsFontSize = 14f * Canvas;
        private const float StatsRowGap = 4f * Canvas;
        private const float StatsRowPitch = 14f * 1.2f * Canvas + StatsRowGap;
        private const float StatsTop = 8f * Canvas;
        private const float StatsBottom = 10f * Canvas;
        private const float StatsColumnGap = 16f * Canvas;

        private readonly Transform root;
        private readonly StyleRecorder recorder = new StyleRecorder();
        private readonly List<Transform> scratch = new List<Transform>();
        private readonly List<Transform> bars = new List<Transform>();
        private readonly HashSet<Transform> styledStrips = new HashSet<Transform>();

        public RightInfoBarStyler(Transform root)
        {
            this.root = root;
        }

        public int RestyledCount => recorder.Count;

        // Restyles anything not yet restyled. Call it when the HUD's hierarchy changed.
        public void Apply()
        {
            Transform cluster = root.Find(UpperRightClusterPath);
            if (!cluster) return;
            // The bars are collected first, since styling one can destroy objects under it.
            cluster.GetComponentsInChildren(true, scratch);
            foreach (Transform transform in scratch)
            {
                if (transform.name == RightInfoBarName)
                {
                    bars.Add(transform);
                }
            }
            scratch.Clear();
            foreach (Transform bar in bars)
            {
                StyleBar(bar);
            }
            bars.Clear();
        }

        public void Restore()
        {
            recorder.Restore();
            styledStrips.Clear();
        }

        private void StyleBar(Transform bar)
        {
            VerticalLayoutGroup barLayout = bar.GetComponent<VerticalLayoutGroup>();
            if (barLayout)
            {
                recorder.Record(barLayout);
                barLayout.spacing = PanelGap;
            }

            Transform objectivePanel = bar.Find(ObjectivePanelName);
            foreach (Transform panel in bar)
            {
                if (panel == objectivePanel)
                {
                    StyleObjectivePanel(panel);
                }
                else if (panel.Find(HeaderStripName) && objectivePanel)
                {
                    // A copy of the objective panel made after it was restyled: LookingGlass's stats panel.
                    StyleCopiedPanel(panel, objectivePanel);
                }
                else
                {
                    StylePanel(panel);
                }
            }
        }

        private void StylePanel(Transform panel)
        {
            Image image = panel.GetComponent<Image>();
            if (!image) return;
            recorder.Record(image);
            Flatten(image);
        }

        private void StyleObjectivePanel(Transform panel)
        {
            StylePanel(panel);

            // The header strip runs the full width, so the panel's own padding goes; the title is inset instead.
            VerticalLayoutGroup layout = panel.GetComponent<VerticalLayoutGroup>();
            if (layout)
            {
                recorder.Record(layout);
                layout.padding = new RectOffset(0, 0, 0, RowsPadding);
            }

            Transform title = panel.Find(TitleName);
            TMP_Text label = title ? title.GetComponent<TMP_Text>() : null;
            if (label)
            {
                recorder.Record(label);
                recorder.RecordText(label);
                HeaderStyle.Apply(label);
                label.fontSize = HeaderFontSize;
                label.alignment = TextAlignmentOptions.MidlineLeft;
                label.margin = new Vector4(HeaderInset, 0f, HeaderInset, 0f);
                // "Objective:" reads as a label; as a header strip it doesn't need the colon.
                string text = label.text;
                if (text.EndsWith(":") || text.EndsWith("："))
                {
                    label.text = text.Substring(0, text.Length - 1);
                }

                LayoutElement element = title.GetComponent<LayoutElement>();
                if (element)
                {
                    recorder.Record(element);
                    element.minHeight = HeaderHeight;
                    element.preferredHeight = HeaderHeight;
                }
            }
            // The rows, their container and the artifact icons' container have images of their own, but the game
            // ships them turned off, so the rows already sit straight on the panel.
            if (!panel.Find(HeaderStripName))
            {
                AddHeaderStrip(panel);
            }
            StyleRows(panel);
        }

        // Every strip, the game's hidden template too: LookingGlass sizes its stats text from the template's label.
        private void StyleRows(Transform panel)
        {
            Transform container = panel.Find(StripContainerName);
            if (!container) return;
            VerticalLayoutGroup layout = container.GetComponent<VerticalLayoutGroup>();
            if (layout)
            {
                recorder.Record(layout);
                layout.padding = new RectOffset(layout.padding.left, layout.padding.right, RowsPadding, layout.padding.bottom);
            }
            ObjectivePanelController controller = panel.GetComponent<ObjectivePanelController>();
            foreach (Transform strip in container)
            {
                if (styledStrips.Add(strip))
                {
                    ObjectiveRow.Style(strip, controller, recorder);
                }
            }
        }

        // LookingGlass's stats panel, a copy of the objective panel made after it was restyled, so it already has the
        // flat panel, the header strip and the title's header look, and writes its stats into the copied title. The
        // strip gets the panel's title and "LOOKINGGLASS", and the stats are laid out in two columns under it
        // (StatsGrid), in place of the hidden title.
        private void StyleCopiedPanel(Transform panel, Transform objectivePanel)
        {
            Transform strip = panel.Find(HeaderStripName);
            Transform title = panel.Find(TitleName);
            TMP_Text label = title ? title.GetComponent<TMP_Text>() : null;
            if (!label || strip.Find(StatsTitleName)) return;
            // The copied strip is the mod's, and goes when the look is restored.
            recorder.Add(strip.gameObject);

            Image image = panel.GetComponent<Image>();
            if (image)
            {
                recorder.RecordAs(image, objectivePanel.GetComponent<Image>());
                Flatten(image);
            }
            VerticalLayoutGroup layout = panel.GetComponent<VerticalLayoutGroup>();
            VerticalLayoutGroup originalLayout = objectivePanel.GetComponent<VerticalLayoutGroup>();
            if (layout && originalLayout && recorder.TryGet(originalLayout, out StyleRecorder.LayoutLook layoutLook))
            {
                recorder.OnRestore(() =>
                {
                    if (layout)
                    {
                        StyleRecorder.Apply(layout, layoutLook);
                    }
                });
            }

            // LookingGlass sets its text's size, color and alignment itself; once the look is restored, the header's
            // case, weight, tracking and inset go, and the text shows again.
            Transform originalTitle = objectivePanel.Find(TitleName);
            TMP_Text originalLabel = originalTitle ? originalTitle.GetComponent<TMP_Text>() : null;
            if (originalLabel && recorder.TryGet(originalLabel, out StyleRecorder.LabelLook labelLook))
            {
                recorder.OnRestore(() =>
                {
                    if (!label) return;
                    label.fontStyle = labelLook.FontStyle;
                    label.characterSpacing = labelLook.CharacterSpacing;
                    label.margin = labelLook.Margin;
                    label.alpha = 1f;
                });
            }
            label.alpha = 0f;
            LayoutElement element = title.GetComponent<LayoutElement>();
            LayoutElement originalElement = originalTitle ? originalTitle.GetComponent<LayoutElement>() : null;
            if (element && originalElement && recorder.TryGet(originalElement, out Vector2 heights))
            {
                recorder.OnRestore(() =>
                {
                    if (element)
                    {
                        element.minHeight = heights.x;
                    }
                });
            }
            AddStatsGrid(title, label, (RectTransform)strip);
        }

        private void AddStatsGrid(Transform title, TMP_Text source, RectTransform strip)
        {
            TMP_Text name = AddText(StatsTitleName, strip, TextAlignmentOptions.MidlineLeft);
            HeaderStyle.Apply(name);
            name.fontSize = HeaderFontSize;
            name.margin = new Vector4(HeaderInset, 0f, HeaderInset, 0f);
            TMP_Text by = AddText("SevenUIStatsSource", strip, TextAlignmentOptions.MidlineRight);
            HeaderStyle.Apply(by);
            by.fontSize = HeaderFontSize;
            by.margin = name.margin;
            by.color = Palette.HeaderSubtle;
            by.text = StatsSource;

            GameObject holder = NewUIObject(StatsGridName, title);
            RectTransform holderRect = (RectTransform)holder.transform;
            holderRect.anchorMin = Vector2.zero;
            holderRect.anchorMax = Vector2.one;
            holderRect.offsetMin = new Vector2(HeaderInset, StatsBottom);
            holderRect.offsetMax = new Vector2(-HeaderInset, -(HeaderHeight + StatsTop));
            StatsGrid grid = holder.AddComponent<StatsGrid>();
            grid.Source = source;
            grid.Title = name;
            grid.Names = new TMP_Text[2];
            grid.Values = new TMP_Text[2];
            for (int column = 0; column < 2; column++)
            {
                Vector2 min = new Vector2(column * 0.5f, 0f);
                Vector2 max = new Vector2(column * 0.5f + 0.5f, 1f);
                Vector2 inset = new Vector2(column == 0 ? 0f : StatsColumnGap / 2f, 0f);
                Vector2 outset = new Vector2(column == 0 ? -StatsColumnGap / 2f : 0f, 0f);
                grid.Names[column] = AddText("Names", holder.transform, TextAlignmentOptions.TopLeft);
                grid.Values[column] = AddText("Values", holder.transform, TextAlignmentOptions.TopRight);
                foreach (TMP_Text text in new[] { grid.Names[column], grid.Values[column] })
                {
                    RectTransform rect = text.rectTransform;
                    rect.anchorMin = min;
                    rect.anchorMax = max;
                    rect.offsetMin = inset;
                    rect.offsetMax = outset;
                    text.fontSize = StatsFontSize;
                }
                grid.Names[column].color = Palette.HeaderText;
                grid.Values[column].color = Palette.Text;
                grid.Values[column].fontStyle = FontStyles.Bold;
            }
            // Over LookingGlass's own element on the title, which it sizes to its single column.
            LayoutElement height = title.gameObject.AddComponent<LayoutElement>();
            height.layoutPriority = StatsLayoutPriority;
            grid.Height = height;
            grid.RowPitch = StatsRowPitch;
            grid.Around = HeaderHeight + StatsTop + StatsBottom - StatsRowGap;
            recorder.Add(holder);
            recorder.Add(height);
        }

        // In the game's language font, so the font swap gives it Barlow like the rest of the HUD.
        private static TMP_Text AddText(string name, Transform parent, TextAlignmentOptions alignment)
        {
            GameObject line = NewUIObject(name, parent);
            RectTransform rect = (RectTransform)line.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            TextMeshProUGUI text = line.AddComponent<TextMeshProUGUI>();
            TMP_FontAsset font = HGTextMeshProUGUI.defaultLanguageFont;
            if (font)
            {
                text.font = font;
                text.fontSharedMaterial = font.material;
            }
            text.alignment = alignment;
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false;
            return text;
        }

        private static GameObject NewUIObject(string name, Transform parent)
        {
            GameObject created = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
            created.layer = parent.gameObject.layer;
            created.transform.SetParent(parent, false);
            return created;
        }

        private void AddHeaderStrip(Transform panel)
        {
            GameObject strip = new GameObject(HeaderStripName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image),
                typeof(LayoutIgnorer));
            strip.layer = panel.gameObject.layer;
            RectTransform rect = (RectTransform)strip.transform;
            rect.SetParent(panel, false);
            // First child, so it draws behind the title.
            rect.SetAsFirstSibling();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(0f, HeaderHeight);
            Image image = strip.GetComponent<Image>();
            image.color = Palette.HeaderStrip;
            image.raycastTarget = false;
            recorder.Add(strip);
        }

        private static void Flatten(Image image)
        {
            image.sprite = null;
            image.type = Image.Type.Simple;
            image.color = Palette.Panel;
        }

        public void Describe(System.Text.StringBuilder output)
        {
            output.AppendLine($"  restyled panel values: {recorder.Count}");
            Transform cluster = root.Find(UpperRightClusterPath);
            if (!cluster) return;
            foreach (Transform bar in cluster.GetComponentsInChildren<Transform>(true))
            {
                if (bar.name != RightInfoBarName) continue;
                foreach (Transform panel in bar)
                {
                    Image image = panel.GetComponent<Image>();
                    string look = image
                        ? $"image {(image.enabled ? "on" : "off")} sprite={(image.sprite ? image.sprite.name : "none")} color={image.color}"
                        : "no image";
                    output.AppendLine($"  {bar.name}/{panel.name} active={panel.gameObject.activeInHierarchy} {look}" +
                                      (panel.Find(HeaderStripName) ? " +header strip" : ""));
                }
            }
        }
    }
}
