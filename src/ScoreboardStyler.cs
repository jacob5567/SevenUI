using System.Collections.Generic;
using System.Text;
using RoR2.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SevenUI
{
    // Restyles the scoreboard (Tab) and its inspect panel: a dark flat board with grey capital headers, faint flat rows,
    // and flat slots for the portrait, key item and equipment. The game keeps building the rows.
    internal sealed class ScoreboardStyler
    {
        // Under the scoreboard panel.
        private const string BoardPath = "Container/StripContainer";
        private const string HeadersPath = "Container/HeaderGroup";
        private const string InspectPanelPath = "Container/InspectPanel/InspectPanelArea/InspectionPanel";
        private const string InspectVisualPath = InspectPanelPath + "/HorizontalBox/InspectIconContainer/InspectVisualBackground";
        private const string SidecarPanelPath = "InspectSidecar/SidecarItemBackground";
        private const string SidecarVisualPath = SidecarPanelPath + "/HorizontalBox/SidecarIconContainer/SidecarVisualBackground";
        // Under each row.
        private const string RowBackgroundName = "LongBackground";
        private static readonly string[] RowSlotPaths =
        {
            "LongBackground/ClassBackground",
            "LongBackground/KeyItemBackground",
            "LongBackground/EquipmentBackground",
        };

        // In HUD canvas units, where the game draws the headers at 24.
        private const float HeaderFontSize = 16f;

        private readonly Transform root;
        private readonly StyleRecorder recorder = new StyleRecorder();
        private readonly List<ScoreboardController> boards = new List<ScoreboardController>();
        private readonly List<ScoreboardStrip> rows = new List<ScoreboardStrip>();

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
        }

        private void StyleBoard(Transform board)
        {
            Fill(Find<Image>(board, BoardPath), Palette.ScoreboardPanel);
            Fill(Find<Image>(board, InspectPanelPath), Palette.ScoreboardPanel);
            Fill(Find<Image>(board, InspectVisualPath), Palette.ItemSlot);
            Fill(Find<Image>(board, SidecarPanelPath), Palette.ScoreboardPanel);
            Fill(Find<Image>(board, SidecarVisualPath), Palette.ItemSlot);

            Transform headers = board.Find(HeadersPath);
            if (headers)
            {
                foreach (TMP_Text header in headers.GetComponentsInChildren<TMP_Text>(true))
                {
                    recorder.Record(header);
                    HeaderStyle.Apply(header);
                    header.fontSize = HeaderFontSize;
                }
            }

            board.GetComponentsInChildren(true, rows);
            foreach (ScoreboardStrip row in rows)
            {
                StyleRow(row);
            }
            rows.Clear();
        }

        private void StyleRow(ScoreboardStrip row)
        {
            Fill(Find<Image>(row.transform, RowBackgroundName), Palette.ScoreboardRow);
            foreach (string path in RowSlotPaths)
            {
                Fill(Find<Image>(row.transform, path), Palette.ItemSlot);
            }
            // On a gamepad the game tints these yellow while focused and black after, so they become thin outlines
            // that keep that tint, instead of filled panels that would turn solid black.
            Outline(row.itemInventoryDisplay ? row.itemInventoryDisplay.GetComponent<Image>() : null);
            Outline(row.nameFocusHighlight);
            if (row.nameLabel)
            {
                recorder.Record(row.nameLabel);
                row.nameLabel.fontStyle |= FontStyles.Bold;
            }
        }

        private void Fill(Image image, Color color)
        {
            if (!image) return;
            recorder.Record(image);
            image.sprite = null;
            image.type = Image.Type.Simple;
            image.color = color;
        }

        private void Outline(Image image)
        {
            if (!image) return;
            recorder.Record(image);
            image.sprite = FlatSprite.Outline;
            image.type = Image.Type.Sliced;
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
