using RoR2;
using RoR2.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SevenUI
{
    // The run's artifacts in the run info, in place of the game's artifact panel under it: a row of the game's own icons
    // under the difficulty bar (and Karma's row, while it shows), with "ARTIFACTS" at its right where the bar has the
    // difficulty's name. The game's icon container is moved into the row and its grid set to one line, so the game
    // still makes an icon per artifact, loads its picture and shows its tooltip; the icons shrink to share the line as
    // more artifacts are on. The game's panel stays, empty, out of the column's layout. RunInfoStyler shows the row
    // and makes room for it while any artifact is on.
    //
    // A pointer reaches a graphic only through a raycaster on its own canvas, and the game gives its artifact panel a
    // canvas and a raycaster of its own for the icons' tooltips. The difficulty bar is a canvas without one, so the row
    // gets its own as well.
    internal sealed class ArtifactRow
    {
        public const string RowName = "SevenUIArtifacts";
        public const string IconsName = "Icons";
        public const string LabelName = "Label";
        // The game's word for artifacts, from its rules.
        private const string ArtifactsToken = "RULE_HEADER_ARTIFACTS";
        // The design's sizes, in canvas pixels: 22px icons 2px apart, the label 10px past them.
        private const float Canvas = RunInfoUnits.PerCanvasPixel;
        public const float Height = 22f * Canvas;
        private const float IconGap = 2f * Canvas;
        private const float LabelGap = 10f * Canvas;

        public readonly RectTransform Root;
        private readonly RectTransform icons;
        private readonly TMP_Text label;
        private readonly RectTransform container;
        private readonly GridLayoutGroup grid;
        private int shownCount = -1;
        private float shownWidth = -1f;
        private TMP_FontAsset shownFont;

        // 'inset' is how far the row's ends stay inside the bar's.
        public ArtifactRow(RectTransform bar, ArtifactDisplayPanelController panel, TMP_Text fontFrom, float inset,
            float labelSize, StyleRecorder recorder)
        {
            Root = NewObject(RowName, bar);
            Root.anchorMin = new Vector2(0f, 0.5f);
            Root.anchorMax = new Vector2(1f, 0.5f);
            Root.pivot = new Vector2(0.5f, 0.5f);
            Root.offsetMin = new Vector2(inset, -Height / 2f);
            Root.offsetMax = new Vector2(-inset, Height / 2f);
            Root.gameObject.AddComponent<Canvas>();
            Root.gameObject.AddComponent<GraphicRaycaster>();

            icons = NewObject(IconsName, Root);
            icons.anchorMin = Vector2.zero;
            icons.anchorMax = new Vector2(0f, 1f);
            icons.pivot = new Vector2(0f, 0.5f);
            icons.anchoredPosition = Vector2.zero;

            RectTransform labelRect = NewObject(LabelName, Root);
            TextMeshProUGUI text = labelRect.gameObject.AddComponent<TextMeshProUGUI>();
            // Starts in the game's font, so the font swap gives it Barlow like the rest of the HUD.
            if (fontFrom)
            {
                text.font = fontFrom.font;
                text.fontSharedMaterial = fontFrom.fontSharedMaterial;
            }
            HeaderStyle.Apply(text);
            text.fontSize = labelSize;
            text.alignment = TextAlignmentOptions.MidlineRight;
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false;
            label = text;
            Root.gameObject.SetActive(false);

            container = panel.iconContainer;
            grid = container ? container.GetComponent<GridLayoutGroup>() : null;
            if (!container || !grid) return;
            HideGamePanel(panel.panelObject, recorder);
            MoveContainer(recorder);
        }

        public bool Alive => Root && container && grid;

        // The artifacts that are on: the game keeps one icon per artifact in the container.
        public int Count => Alive && container.gameObject.activeSelf ? container.childCount : 0;

        // Each icon's width and height in the bar's units, as last laid out.
        public float IconSize => grid ? grid.cellSize.x : 0f;

        // Its middle this far below the bar's.
        public void Place(float rowOffset)
        {
            Root.offsetMin = new Vector2(Root.offsetMin.x, -rowOffset - Height / 2f);
            Root.offsetMax = new Vector2(Root.offsetMax.x, -rowOffset + Height / 2f);
        }

        // While shown: laid out again only when the count, the row's width or the label's font changes. The label is
        // measured once the row is active, as a text measures far too narrow before.
        public void Update()
        {
            if (!Alive || !Root.gameObject.activeInHierarchy) return;
            int count = Count;
            float width = Root.rect.width;
            if (count == shownCount && Mathf.Approximately(width, shownWidth) && label.font == shownFont) return;
            shownCount = count;
            shownWidth = width;
            shownFont = label.font;
            Layout(count, width);
        }

        // The label at the right, as wide as its text; the icons share the rest, each at most the row's height.
        private void Layout(int count, float width)
        {
            label.text = Language.GetString(ArtifactsToken);
            float labelWidth = label.GetPreferredValues(label.text, float.PositiveInfinity, float.PositiveInfinity).x;
            RectTransform labelRect = label.rectTransform;
            labelRect.anchorMin = new Vector2(1f, 0f);
            labelRect.anchorMax = Vector2.one;
            labelRect.pivot = new Vector2(1f, 0.5f);
            labelRect.anchoredPosition = Vector2.zero;
            labelRect.sizeDelta = new Vector2(labelWidth, 0f);

            float room = Mathf.Max(0f, width - labelWidth - LabelGap);
            icons.sizeDelta = new Vector2(room, 0f);
            float cell = count > 0 ? Mathf.Min(Height, (room - IconGap * (count - 1)) / count) : Height;
            grid.cellSize = new Vector2(cell, cell);
        }

        // The game's panel keeps turning itself on and off with the artifacts, but holds nothing once the icons move,
        // so it is left out of the column's layout and drawn clear.
        private static void HideGamePanel(GameObject gamePanel, StyleRecorder recorder)
        {
            if (!gamePanel) return;
            recorder.Disable(gamePanel.GetComponent<Image>());
            LayoutElement element = gamePanel.GetComponent<LayoutElement>();
            if (element)
            {
                bool ignored = element.ignoreLayout;
                recorder.OnRestore(() =>
                {
                    if (element)
                    {
                        element.ignoreLayout = ignored;
                    }
                });
            }
            else
            {
                element = gamePanel.AddComponent<LayoutElement>();
                recorder.Add(element);
            }
            element.ignoreLayout = true;
        }

        // Into the row, filling the icons' share of it, with the grid set to one line from the left. Put back where it
        // was when the panels are restored; the rect's own values are put back first, while it is still in the row.
        private void MoveContainer(StyleRecorder recorder)
        {
            Transform parent = container.parent;
            int index = container.GetSiblingIndex();
            GridLayoutGroup.Constraint constraint = grid.constraint;
            int constraintCount = grid.constraintCount;
            Vector2 cellSize = grid.cellSize;
            Vector2 spacing = grid.spacing;
            TextAnchor alignment = grid.childAlignment;
            GridLayoutGroup.Corner corner = grid.startCorner;
            GridLayoutGroup.Axis axis = grid.startAxis;
            recorder.RecordRect(container);
            recorder.OnRestore(() =>
            {
                if (grid)
                {
                    grid.constraint = constraint;
                    grid.constraintCount = constraintCount;
                    grid.cellSize = cellSize;
                    grid.spacing = spacing;
                    grid.childAlignment = alignment;
                    grid.startCorner = corner;
                    grid.startAxis = axis;
                }
                if (container && parent)
                {
                    container.SetParent(parent, false);
                    container.SetSiblingIndex(index);
                }
            });

            container.SetParent(icons, false);
            container.anchorMin = Vector2.zero;
            container.anchorMax = Vector2.one;
            container.pivot = new Vector2(0.5f, 0.5f);
            container.offsetMin = container.offsetMax = Vector2.zero;
            grid.constraint = GridLayoutGroup.Constraint.FixedRowCount;
            grid.constraintCount = 1;
            grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            grid.childAlignment = TextAnchor.MiddleLeft;
            grid.spacing = new Vector2(IconGap, 0f);
            grid.cellSize = new Vector2(Height, Height);
        }

        private static RectTransform NewObject(string name, Transform holder)
        {
            GameObject created = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
            created.layer = holder.gameObject.layer;
            created.transform.SetParent(holder, false);
            return (RectTransform)created.transform;
        }
    }
}
