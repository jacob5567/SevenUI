using System.Collections.ObjectModel;
using RoR2.UI;
using UnityEngine;
using UnityEngine.UI;
using static SevenUI.PickerPanel;

namespace SevenUI
{
    // A picker panel's use of the game's grid: its cell and spacing, the columns it lays the options out in, and the
    // gamepad navigation for the cards' row.
    internal sealed class PickerGrid
    {
        private readonly PickerPanel owner;
        private readonly PickerUi ui;

        public PickerGrid(PickerPanel owner, PickerUi ui)
        {
            this.owner = owner;
            this.ui = ui;
        }

        // The options per row: all of them as cards, the game's columns as a grid.
        public int Columns { get; set; }

        // The game's grid, which places the buttons: its cell and spacing become the slots' or the cards'.
        public void StyleGrid()
        {
            GridLayoutGroup grid = ui.Panel.gridlayoutGroup;
            Vector2 cell = grid.cellSize;
            Vector2 spacing = grid.spacing;
            RectOffset padding = grid.padding;
            TextAnchor alignment = grid.childAlignment;
            GridLayoutGroup.Constraint constraint = grid.constraint;
            int constraintCount = grid.constraintCount;
            ui.Recorder.OnRestore(() =>
            {
                if (!grid) return;
                grid.cellSize = cell;
                grid.spacing = spacing;
                grid.padding = padding;
                grid.childAlignment = alignment;
                grid.constraint = constraint;
                grid.constraintCount = constraintCount;
            });
            KeepColumns();
            grid.padding = new RectOffset(0, 0, 0, 0);
            grid.childAlignment = TextAnchor.UpperLeft;
            grid.spacing = owner.Cards ? new Vector2(CardGap, CardGap) : new Vector2(SlotGap, SlotGap);
            RectTransform container = ui.Panel.buttonContainer;
            ui.Recorder.RecordRect(container);
            container.anchorMin = container.anchorMax = container.pivot = new Vector2(0f, 1f);
        }

        // Every frame too, since another mod may set the grid's columns after the panel is styled: the cards are held
        // to one row, and a grid is laid out again in the game's columns when they change.
        public void KeepColumns()
        {
            GridLayoutGroup grid = ui.Panel.gridlayoutGroup;
            if (!owner.Cards)
            {
                // The options, not the buttons, which are styled after the grid.
                int game = GameColumns(owner.ShownOptions.Length);
                if (game != Columns)
                {
                    Columns = game;
                    owner.Layout();
                }
                return;
            }
            if (grid.constraint != GridLayoutGroup.Constraint.FixedColumnCount)
            {
                grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            }
            if (grid.constraintCount != Columns)
            {
                grid.constraintCount = Columns;
            }
        }

        // The columns the game's grid lays this many options out in. PickupPickerPanel fixes the columns at
        // maxColumnCount as it wakes, but LookingGlass's larger Command window fixes the rows instead (and sets
        // maxColumnCount to the same number), so there the columns are the options over the rows.
        public int GameColumns(int options)
        {
            GridLayoutGroup grid = ui.Panel.gridlayoutGroup;
            int count = grid.constraintCount;
            int game;
            if (grid.constraint == GridLayoutGroup.Constraint.FixedColumnCount && count > 0)
            {
                game = count;
            }
            else if (grid.constraint == GridLayoutGroup.Constraint.FixedRowCount && count > 0)
            {
                game = (options + count - 1) / count;
            }
            else
            {
                game = ui.Panel.maxColumnCount;
            }
            return Mathf.Clamp(game, 1, Mathf.Max(1, options));
        }

        // The game's explicit gamepad navigation (PickupPickerPanel.SetPickupOptions) for the cards' one row, so up,
        // down, left and right move as the cards are laid out.
        public void Navigate(ReadOnlyCollection<MPButton> elements, int count)
        {
            if (ui.Panel.automaticButtonNavigation) return;
            for (int i = 0; i < count; i++)
            {
                MPButton button = elements[i];
                Navigation navigation = button.navigation;
                ui.Recorder.OnRestore(() =>
                {
                    if (button)
                    {
                        button.navigation = navigation;
                    }
                });
                int column = i % Columns;
                Navigation flat = navigation;
                flat.mode = Navigation.Mode.Explicit;
                flat.selectOnLeft = column > 0 ? elements[i - 1] : null;
                flat.selectOnRight = column < Columns - 1 && i + 1 < count ? elements[i + 1] : null;
                flat.selectOnUp = i - Columns >= 0 ? elements[i - Columns] : null;
                flat.selectOnDown = i + Columns < count ? elements[i + Columns] : null;
                button.navigation = flat;
            }
        }
    }
}
