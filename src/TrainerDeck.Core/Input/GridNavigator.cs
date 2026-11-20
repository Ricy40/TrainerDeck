namespace TrainerDeck.Core.Input;


public static class GridNavigator
{
    public static int Move(int index, int count, int columns, InputAction action, int pageRows = 2)
    {
        if (count <= 0)
        {
            return -1;
        }

        columns = Math.Max(1, columns);
        pageRows = Math.Max(1, pageRows);
        index = Math.Clamp(index, 0, count - 1);
        int col = index % columns;
        int lastRow = (count - 1) / columns;

        switch (action)
        {
            case InputAction.Left:
                return col > 0 ? index - 1 : index;
            case InputAction.Right:
                return col < columns - 1 && index + 1 < count ? index + 1 : index;
            case InputAction.Up:
                return index - columns >= 0 ? index - columns : index;
            case InputAction.Down:
                if (index + columns < count)
                {
                    return index + columns;
                }

                return index / columns < lastRow ? count - 1 : index;
            case InputAction.PageUp:
            {
                int target = index - (columns * pageRows);
                return target >= 0 ? target : col;
            }

            case InputAction.PageDown:
            {
                int target = index + (columns * pageRows);
                if (target < count)
                {
                    return target;
                }

                int sameColumnInLastRow = (lastRow * columns) + col;
                return Math.Max(index, Math.Min(sameColumnInLastRow, count - 1));
            }

            case InputAction.Home:
                return 0;
            case InputAction.End:
                return count - 1;
            default:
                return index;
        }
    }
}
