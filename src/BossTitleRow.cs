using RoR2;
using RoR2.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SevenUI
{
    // The boss bar laid out as on the design canvas: an 800 by 14 bar, and above it one row with the boss's name and
    // then its subtitle at the left, and its health, "28,410 / 45,800", at the right. The game stacks the name and the
    // subtitle under a wider bar and writes the health across the bar. The bar's container now lays out only the bar,
    // with room above it for the row; the name, the subtitle and the health are placed by hand in that room, again
    // whenever the name, the bar's place or a font changes.
    internal sealed class BossTitleRow
    {
        public const string HealthName = "SevenUIBossHealth";
        // In the top cluster's units, which match the design canvas's pixels.
        private const float BarWidth = 800f;
        private const float BarHeight = 14f;
        private const float RowGap = 6f;
        private const float SubtitleGap = 12f;
        private const float SmallFontSize = 16f;
        private const string Separator = " / ";

        private readonly HUDBossHealthBarController boss;
        private readonly RectTransform container;
        private readonly VerticalLayoutGroup layout;
        private readonly RectTransform cell;
        private readonly int topPadding;
        private readonly TMP_Text name;
        private readonly TMP_Text subtitle;
        private readonly TMP_Text health;
        private string shownName;
        private Rect shownCell;
        private TMP_FontAsset shownNameFont;
        private TMP_FontAsset shownSmallFont;
        private int shownHealth = -1;
        private int shownMaxHealth = -1;

        private BossTitleRow(HUDBossHealthBarController boss, RectTransform container, VerticalLayoutGroup layout,
            RectTransform cell, int topPadding, TMP_Text name, TMP_Text subtitle, TMP_Text health)
        {
            this.boss = boss;
            this.container = container;
            this.layout = layout;
            this.cell = cell;
            this.topPadding = topPadding;
            this.name = name;
            this.subtitle = subtitle;
            this.health = health;
        }

        // Null when the boss bar isn't laid out as the game ships it.
        public static BossTitleRow Style(HUDBossHealthBarController boss, StyleRecorder recorder)
        {
            RectTransform container = boss.container ? boss.container.transform as RectTransform : null;
            VerticalLayoutGroup layout = container ? container.GetComponent<VerticalLayoutGroup>() : null;
            TMP_Text name = boss.bossNameLabel;
            TMP_Text subtitle = boss.bossSubtitleLabel;
            RectTransform bar = boss.fillRectImage ? boss.fillRectImage.transform.parent as RectTransform : null;
            RectTransform cell = bar ? bar.parent as RectTransform : null;
            LayoutElement barCell = cell ? cell.GetComponent<LayoutElement>() : null;
            if (!layout || !name || !subtitle || !barCell || name.transform.parent != container ||
                subtitle.transform.parent != container || cell.parent != container) return null;

            // The bar at its own width, centered, with no side padding, so it fits the top cluster's canvas width.
            bool expandWidth = layout.childForceExpandWidth;
            int topPadding = layout.padding.top;
            recorder.Record(layout);
            recorder.OnRestore(() =>
            {
                if (!layout) return;
                layout.childForceExpandWidth = expandWidth;
            });
            layout.childForceExpandWidth = false;
            layout.padding = new RectOffset(0, 0, topPadding, layout.padding.bottom);

            float cellWidth = barCell.preferredWidth;
            float cellFlexible = barCell.flexibleWidth;
            recorder.Record(barCell);
            recorder.OnRestore(() =>
            {
                if (!barCell) return;
                barCell.preferredWidth = cellWidth;
                barCell.flexibleWidth = cellFlexible;
            });
            barCell.preferredWidth = BarWidth;
            barCell.flexibleWidth = 0f;
            barCell.minHeight = BarHeight;
            barCell.preferredHeight = BarHeight;
            // The bar's panel is inset in its cell, whose faded backdrop the restyle turns off.
            recorder.RecordRect(bar);
            bar.offsetMin = Vector2.zero;
            bar.offsetMax = Vector2.zero;

            // The name and subtitle leave the container's layout, to be placed above the bar.
            foreach (TMP_Text label in new[] { name, subtitle })
            {
                recorder.Add(label.gameObject.AddComponent<LayoutIgnorer>());
                recorder.RecordRect((RectTransform)label.transform);
                recorder.Record(label);
                label.alignment = TextAlignmentOptions.BottomLeft;
                KeepOnOneLine(label, recorder);
            }
            subtitle.fontSize = SmallFontSize;

            // The game's health text, written across the bar, gives way to the mod's above it.
            recorder.Disable(boss.healthLabel);
            GameObject created = new GameObject(HealthName, typeof(RectTransform), typeof(CanvasRenderer));
            created.layer = bar.gameObject.layer;
            created.transform.SetParent(bar, false);
            TextMeshProUGUI healthText = created.AddComponent<TextMeshProUGUI>();
            // In the game label's font, so the font swap gives it Barlow like the rest of the HUD.
            if (boss.healthLabel)
            {
                healthText.font = boss.healthLabel.font;
                healthText.fontSharedMaterial = boss.healthLabel.fontSharedMaterial;
            }
            healthText.fontSize = SmallFontSize;
            healthText.fontStyle = FontStyles.Bold;
            healthText.color = Palette.Text;
            healthText.alignment = TextAlignmentOptions.BottomRight;
            healthText.enableWordWrapping = false;
            healthText.overflowMode = TextOverflowModes.Overflow;
            healthText.raycastTarget = false;
            recorder.Add(created);
            return new BossTitleRow(boss, container, layout, cell, topPadding, name, subtitle, healthText);
        }

        private static void KeepOnOneLine(TMP_Text label, StyleRecorder recorder)
        {
            bool wrapping = label.enableWordWrapping;
            recorder.OnRestore(() =>
            {
                if (!label) return;
                label.enableWordWrapping = wrapping;
            });
            label.enableWordWrapping = false;
        }

        public bool Alive => boss && container && layout && cell && name && subtitle && health;

        public string ShownHealth => health.text;

        // While a boss bar shows: two ints, a string, a rect and two fonts compared when nothing changed.
        public void Update()
        {
            if (!boss.container.activeInHierarchy) return;
            BossGroup group = boss.currentBossGroup;
            if (group)
            {
                int current = Mathf.FloorToInt(group.totalObservedHealth);
                int max = Mathf.FloorToInt(group.totalMaxObservedMaxHealth);
                if (current != shownHealth || max != shownMaxHealth)
                {
                    shownHealth = current;
                    shownMaxHealth = max;
                    health.text = TextSerialization.ToStringNumeric(current) + Separator + TextSerialization.ToStringNumeric(max);
                }
            }

            Rect bar = cell.rect;
            bar.position += (Vector2)cell.localPosition;
            if (string.Equals(name.text, shownName, System.StringComparison.Ordinal) && bar == shownCell &&
                name.font == shownNameFont && health.font == shownSmallFont) return;
            if (!Place(bar)) return;
            shownName = name.text;
            shownCell = bar;
            shownNameFont = name.font;
            shownSmallFont = health.font;
        }

        // The bar's cell is in the container's units. The container's top padding makes room for the row, as tall as
        // the name; the smaller texts are raised so their baselines meet the name's, as each sits on its box's bottom
        // with its font's descender below the baseline.
        // False when the room had to change first.
        private bool Place(Rect bar)
        {
            float rowHeight = Mathf.Max(name.preferredHeight, name.fontSize);
            int room = topPadding + Mathf.CeilToInt(rowHeight + RowGap);
            if (layout.padding.top != room)
            {
                // The layout runs again and moves the bar; the row is placed once it has.
                layout.padding = new RectOffset(0, 0, room, layout.padding.bottom);
                return false;
            }

            float nameDescent = Descent(name);
            float rowBottom = bar.yMax + RowGap;
            RectTransform nameRect = (RectTransform)name.transform;
            nameRect.anchorMin = nameRect.anchorMax = new Vector2(0.5f, 0.5f);
            nameRect.pivot = Vector2.zero;
            nameRect.sizeDelta = new Vector2(BarWidth, rowHeight);
            nameRect.localPosition = new Vector3(bar.xMin, rowBottom, 0f);

            RectTransform subtitleRect = (RectTransform)subtitle.transform;
            subtitleRect.anchorMin = subtitleRect.anchorMax = new Vector2(0.5f, 0.5f);
            subtitleRect.pivot = Vector2.zero;
            subtitleRect.sizeDelta = new Vector2(BarWidth, rowHeight);
            subtitleRect.localPosition = new Vector3(bar.xMin + name.preferredWidth + SubtitleGap,
                rowBottom + nameDescent - Descent(subtitle), 0f);

            RectTransform healthRect = (RectTransform)health.transform;
            healthRect.anchorMin = new Vector2(0f, 1f);
            healthRect.anchorMax = Vector2.one;
            healthRect.pivot = new Vector2(0.5f, 0f);
            healthRect.sizeDelta = new Vector2(0f, rowHeight);
            healthRect.anchoredPosition = new Vector2(0f, RowGap + nameDescent - Descent(health));
            return true;
        }

        // How far below its baseline a text's font reaches at the text's size.
        private static float Descent(TMP_Text text)
        {
            if (!text.font || text.font.faceInfo.pointSize <= 0f) return 0f;
            return -text.font.faceInfo.descentLine / text.font.faceInfo.pointSize * text.fontSize;
        }
    }
}
