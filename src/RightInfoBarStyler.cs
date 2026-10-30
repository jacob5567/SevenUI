using System.Collections.Generic;
using RoR2.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SevenUI
{
    // Restyles the panels under the run info in the top right (the artifact panel, the objective panel, and panels
    // other mods add there, such as LookingGlass's stats) as flat translucent panels, with a header strip on the
    // objective panel and its rows laid out as on the design canvas (ObjectiveRow).
    internal sealed class RightInfoBarStyler
    {
        // Vanilla object names. LookingGlass looks up ObjectivePanel and its Label by these, so they are never renamed.
        private const string UpperRightClusterPath = "MainContainer/MainUIArea/SpringCanvas/UpperRightCluster";
        private const string RightInfoBarName = "RightInfoBar";
        private const string ObjectivePanelName = "ObjectivePanel";
        private const string TitleName = "Label";
        private const string StripContainerName = "StripContainer";
        private const string HeaderStripName = "SevenUIHeaderStrip";

        // The design canvas's sizes, in its pixels, scaled to the run info's units. The rows' own height holds most
        // of the 10px above and below them.
        private const float Canvas = RunInfoUnits.PerCanvasPixel;
        private const float PanelGap = 8f * Canvas;
        private const int RowsPadding = 4;
        private const float HeaderHeight = 26f * Canvas;
        private const float HeaderInset = 14f * Canvas;
        private const float HeaderFontSize = 12f * Canvas;

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
                    // A copy of the objective panel made after it was restyled: LookingGlass's stats panel. It shows
                    // its own text in the copied title, so it gets a plain panel and the title's body look back.
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

        private void StyleCopiedPanel(Transform panel, Transform objectivePanel)
        {
            // Immediately, so the copy isn't taken for one again before the end of the frame.
            Object.DestroyImmediate(panel.Find(HeaderStripName).gameObject);

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
                StyleRecorder.Apply(layout, layoutLook);
            }

            // LookingGlass sets its text's size, color and alignment itself; the header's case, weight, tracking
            // and inset are put back.
            Transform title = panel.Find(TitleName);
            Transform originalTitle = objectivePanel.Find(TitleName);
            TMP_Text label = title ? title.GetComponent<TMP_Text>() : null;
            TMP_Text originalLabel = originalTitle ? originalTitle.GetComponent<TMP_Text>() : null;
            if (label && originalLabel && recorder.TryGet(originalLabel, out StyleRecorder.LabelLook labelLook))
            {
                label.fontStyle = labelLook.FontStyle;
                label.characterSpacing = labelLook.CharacterSpacing;
                label.margin = labelLook.Margin;
            }
            LayoutElement element = title ? title.GetComponent<LayoutElement>() : null;
            LayoutElement originalElement = originalTitle ? originalTitle.GetComponent<LayoutElement>() : null;
            if (element && originalElement && recorder.TryGet(originalElement, out Vector2 heights))
            {
                element.minHeight = heights.x;
            }
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
