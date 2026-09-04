using System;
using System.Collections.Generic;

/// <summary>
/// Tiles a unit can move to, relative to where it stands.
/// The middle cell is the unit's own tile. A cell is true if that tile can be targeted.
/// The grid is square and odd-sized so there is a single center.
/// Stored as a flat row-major array so Unity can serialize it.
/// </summary>
[Serializable]
public class MovementPattern
{
    public bool[] cells;

    public MovementPattern()
    {
    }

    public MovementPattern(bool[] cells)
    {
        this.cells = cells;
    }

    public int size
    {
        get
        {
            if (cells == null || cells.Length == 0)
                return 0;
            int side = (int)Math.Sqrt(cells.Length);
            return side * side == cells.Length ? side : 0;
        }
    }

    public bool HasCells => size > 0;

    public static MovementPattern Empty()
    {
        return new MovementPattern(new bool[1]);
    }

    /// <summary>Filled square. Range 1 is the 8 neighbors.</summary>
    public static MovementPattern Square(int range, bool includeCenter = false)
    {
        return Build(range, includeCenter, (column, row) => true);
    }

    /// <summary>Orthogonal arms only. Range 1 is up, down, left, right.</summary>
    public static MovementPattern Plus(int range, bool includeCenter = false)
    {
        return Build(range, includeCenter, (column, row) => column == 0 || row == 0);
    }

    /// <summary>Diagonal arms only. Range 1 is the four corners.</summary>
    public static MovementPattern X(int range, bool includeCenter = false)
    {
        return Build(range, includeCenter, (column, row) => Math.Abs(column) == Math.Abs(row));
    }

    public bool Includes(int row, int column)
    {
        if (!HasCells || row < 0 || column < 0 || row >= size || column >= size)
            return false;
        return cells[row * size + column];
    }

    /// <summary>Board cells this pattern covers when the unit stands on <paramref name="origin"/>.</summary>
    public void CollectBoardCells(Position origin, List<Position> result)
    {
        if (origin == null || result == null || !HasCells)
            return;

        int side = size;
        int center = side / 2;
        for (int row = 0; row < side; row++)
        {
            for (int col = 0; col < side; col++)
            {
                if (!Includes(row, col))
                    continue;
                result.Add(new Position(origin.Column + (col - center), origin.Row + (row - center)));
            }
        }
    }

    static MovementPattern Build(int range, bool includeCenter, Func<int, int, bool> include)
    {
        if (range < 0)
            range = 0;

        int size = range * 2 + 1;
        var cells = new bool[size * size];
        for (int row = 0; row < size; row++)
        {
            for (int col = 0; col < size; col++)
            {
                int column = col - range;
                int offsetRow = row - range;
                int dist = Math.Max(Math.Abs(column), Math.Abs(offsetRow));
                bool center = dist == 0;
                cells[row * size + col] = dist <= range
                    && include(column, offsetRow)
                    && (includeCenter || !center);
            }
        }

        return new MovementPattern(cells);
    }
}
