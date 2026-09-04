using System;
using System.Collections.Generic;

/// <summary>
/// Identifiers on one attack-pattern tile. Empty list means the tile is uncovered.
/// </summary>
[Serializable]
public class AttackPatternCell
{
    public List<byte> identifiers = new List<byte>();

    public int Count => identifiers == null ? 0 : identifiers.Count;

    public bool Contains(byte identifier)
    {
        return identifiers != null && identifiers.Contains(identifier);
    }

    public void Add(byte identifier)
    {
        if (identifiers == null)
            identifiers = new List<byte>();
        if (!identifiers.Contains(identifier))
            identifiers.Add(identifier);
    }
}

/// <summary>
/// Attack tiles relative to where the unit stands, stored row-major so Unity can serialize them.
/// </summary>
[Serializable]
public class AttackPattern
{
    public int width;
    public int height;
    public AttackPatternCell[] cells;
    public bool hasSingleAttack;

    public AttackPattern()
    {
    }

    AttackPattern(int width, int height, bool hasSingleAttack)
    {
        this.width = width;
        this.height = height;
        this.hasSingleAttack = hasSingleAttack;
        cells = new AttackPatternCell[width * height];
        for (int i = 0; i < cells.Length; i++)
            cells[i] = new AttackPatternCell();
    }

    public static AttackPattern Empty()
    {
        return new AttackPattern(1, 1, false);
    }

    /// <summary>True if any attack identifier is on this cell.</summary>
    public bool Covers(int row, int column)
    {
        return IdentifierCount(row, column) > 0;
    }

    public int IdentifierCount(int row, int column)
    {
        AttackPatternCell cell = CellAt(row, column);
        return cell == null ? 0 : cell.Count;
    }

    public bool ContainsIdentifier(int row, int column, byte identifier)
    {
        AttackPatternCell cell = CellAt(row, column);
        return cell != null && cell.Contains(identifier);
    }

    public bool TryGetSoleIdentifier(int row, int column, out byte identifier)
    {
        identifier = 0;
        AttackPatternCell cell = CellAt(row, column);
        if (cell == null || cell.Count != 1)
            return false;
        identifier = cell.identifiers[0];
        return true;
    }

    /// <summary>True when <see cref="hasSingleAttack"/> and the shared identifier is found.</summary>
    public bool TryGetSingleIdentifier(out byte identifier)
    {
        identifier = 0;
        if (!hasSingleAttack || cells == null)
            return false;
        for (int row = 0; row < height; row++)
        {
            for (int col = 0; col < width; col++)
            {
                AttackPatternCell cell = CellAt(row, col);
                if (cell == null || cell.Count == 0)
                    continue;
                identifier = cell.identifiers[0];
                return true;
            }
        }

        return false;
    }

    AttackPatternCell CellAt(int row, int column)
    {
        if (cells == null || width <= 0 || height <= 0)
            return null;
        if (row < 0 || column < 0 || row >= height || column >= width)
            return null;
        return cells[row * width + column];
    }

    /// <summary>Maps a board cell onto this pattern, centered on <paramref name="origin"/>.</summary>
    public bool TryMapBoardToLocal(Position origin, Position cell, out int row, out int column)
    {
        row = 0;
        column = 0;
        if (cell == null || origin == null || cells == null || width <= 0 || height <= 0)
            return false;

        int centerRow = height / 2;
        int centerCol = width / 2;
        column = cell.Column - origin.Column + centerCol;
        row = cell.Row - origin.Row + centerRow;
        return row >= 0 && column >= 0 && row < height && column < width;
    }

    public void CollectCoveredBoardCells(Position origin, List<Position> result)
    {
        CollectBoardCells(origin, result, coverOnly: true, identifier: 0);
    }

    public void CollectBoardCellsWithIdentifier(Position origin, byte identifier, List<Position> result)
    {
        CollectBoardCells(origin, result, coverOnly: false, identifier: identifier);
    }

    void CollectBoardCells(Position origin, List<Position> result, bool coverOnly, byte identifier)
    {
        if (origin == null || result == null || cells == null || width <= 0 || height <= 0)
            return;

        int centerRow = height / 2;
        int centerCol = width / 2;
        for (int row = 0; row < height; row++)
        {
            for (int col = 0; col < width; col++)
            {
                bool include = coverOnly
                    ? Covers(row, col)
                    : ContainsIdentifier(row, col, identifier);
                if (!include)
                    continue;
                result.Add(new Position(origin.Column + (col - centerCol), origin.Row + (row - centerRow)));
            }
        }
    }

    /// <summary>Orthogonal arms. Range 1 is the four adjacent tiles.</summary>
    public static AttackPattern Plus(int range, bool coverAllDirections = false)
    {
        return GenerateFromDirections(range, new[]
        {
            new[] { 0, 1 },
            new[] { 1, 0 },
            new[] { 0, -1 },
            new[] { -1, 0 },
        }, coverAllDirections);
    }

    /// <summary>Diagonal arms. Range 1 is the four corners.</summary>
    public static AttackPattern X(int range, bool coverAllDirections = false)
    {
        return GenerateFromDirections(range, new[]
        {
            new[] { 1, 1 },
            new[] { 1, -1 },
            new[] { -1, 1 },
            new[] { -1, -1 },
        }, coverAllDirections);
    }

    /// <summary>Eight arms (plus and X). Range 1 is every neighbor.</summary>
    public static AttackPattern Octopus(int range, bool coverAllDirections = false)
    {
        return GenerateFromDirections(range, new[]
        {
            new[] { 0, 1 },
            new[] { 1, 0 },
            new[] { 0, -1 },
            new[] { -1, 0 },
            new[] { 1, 1 },
            new[] { 1, -1 },
            new[] { -1, 1 },
            new[] { -1, -1 },
        }, coverAllDirections);
    }

    /// <summary>
    /// One identifier per arm unless <paramref name="coverAllDirections"/> is true, then every arm shares one identifier.
    /// The stand cell is empty.
    /// </summary>
    static AttackPattern GenerateFromDirections(int range, int[][] directions, bool coverAllDirections = false)
    {
        if (range < 0)
            range = 0;

        int size = range * 2 + 1;
        var pattern = new AttackPattern(size, size, coverAllDirections && range > 0);
        byte identifier = 0;
        int center = range;
        foreach (int[] direction in directions)
        {
            int row = center;
            int col = center;
            for (int i = 0; i < range; i++)
            {
                row += direction[0];
                col += direction[1];
                pattern.CellAt(row, col).Add(identifier);
            }

            if (!coverAllDirections)
                identifier++;
        }

        return pattern;
    }

    /// <summary>
    /// Filled square. Range 1 is the 8 neighbors. The stand cell is empty.
    /// When <paramref name="coverAllDirections"/> is false, each tile has its own identifier.
    /// </summary>
    public static AttackPattern Square(int range, bool coverAllDirections = false)
    {
        if (range < 0)
            range = 0;

        int size = range * 2 + 1;
        var pattern = new AttackPattern(size, size, coverAllDirections && range > 0);
        byte identifier = 0;
        for (int row = 0; row < size; row++)
        {
            for (int col = 0; col < size; col++)
            {
                if (row == range && col == range)
                    continue;
                pattern.CellAt(row, col).Add(identifier);
                if (!coverAllDirections)
                    identifier++;
            }
        }

        return pattern;
    }

    /// <summary>
    /// Builds a grid from identifier lists. Empty or null lists are uncovered tiles.
    /// Row 0 is the top of the pattern.
    /// </summary>
    public static AttackPattern FromGrid(byte[][][] source)
    {
        int height = source.Length;
        int width = source[0].Length;
        var unique = new HashSet<byte>();
        for (int row = 0; row < height; row++)
        {
            for (int col = 0; col < width; col++)
            {
                byte[] ids = source[row][col];
                if (ids == null)
                    continue;
                for (int i = 0; i < ids.Length; i++)
                    unique.Add(ids[i]);
            }
        }

        var pattern = new AttackPattern(width, height, unique.Count == 1);
        for (int row = 0; row < height; row++)
        {
            for (int col = 0; col < width; col++)
            {
                byte[] ids = source[row][col];
                if (ids == null)
                    continue;
                for (int i = 0; i < ids.Length; i++)
                    pattern.CellAt(row, col).Add(ids[i]);
            }
        }

        return pattern;
    }
}
