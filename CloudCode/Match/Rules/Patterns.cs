namespace Match.Rules;

public readonly struct Cell : IEquatable<Cell>
{
    public readonly int Column;
    public readonly int Row;

    public Cell(int column, int row)
    {
        Column = column;
        Row = row;
    }

    public bool Equals(Cell other) => Column == other.Column && Row == other.Row;
    public override bool Equals(object obj) => obj is Cell other && Equals(other);
    public override int GetHashCode() => (Column * 397) ^ Row;
}

public class MovementPattern
{
    public readonly bool[] Cells;

    public MovementPattern(bool[] cells)
    {
        Cells = cells ?? Array.Empty<bool>();
    }

    public int Size
    {
        get
        {
            if (Cells.Length == 0)
                return 0;
            int side = (int)Math.Sqrt(Cells.Length);
            return side * side == Cells.Length ? side : 0;
        }
    }

    public bool HasCells => Size > 0;

    public static MovementPattern Empty() => new(new bool[1]);

    public static MovementPattern Square(int range, bool includeCenter = false)
    {
        return Build(range, includeCenter, (_, _) => true);
    }

    public static MovementPattern Plus(int range, bool includeCenter = false)
    {
        return Build(range, includeCenter, (column, row) => column == 0 || row == 0);
    }

    public static MovementPattern X(int range, bool includeCenter = false)
    {
        return Build(range, includeCenter, (column, row) => Math.Abs(column) == Math.Abs(row));
    }

    public static MovementPattern FromGrid(bool[][] source)
    {
        if (source == null || source.Length == 0)
            return Empty();

        int height = source.Length;
        int width = 0;
        for (int row = 0; row < height; row++)
        {
            if (source[row] != null && source[row].Length > width)
                width = source[row].Length;
        }

        if (width <= 0)
            return Empty();

        int side = Math.Max(height, width);
        if ((side & 1) == 0)
            side++;

        int rowOffset = (side - height) / 2;
        int colOffset = (side - width) / 2;
        var cells = new bool[side * side];
        for (int row = 0; row < height; row++)
        {
            bool[] line = source[row];
            if (line == null)
                continue;
            for (int col = 0; col < line.Length; col++)
            {
                if (!line[col])
                    continue;
                cells[(row + rowOffset) * side + (col + colOffset)] = true;
            }
        }

        return new MovementPattern(cells);
    }

    public bool Includes(int row, int column)
    {
        if (!HasCells || row < 0 || column < 0 || row >= Size || column >= Size)
            return false;
        return Cells[row * Size + column];
    }

    public void CollectBoardCells(Cell origin, List<Cell> result)
    {
        if (result == null || !HasCells)
            return;

        int side = Size;
        int center = side / 2;
        for (int row = 0; row < side; row++)
        {
            for (int col = 0; col < side; col++)
            {
                if (!Includes(row, col))
                    continue;
                result.Add(new Cell(origin.Column + (col - center), origin.Row + (row - center)));
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

public class AttackPatternCell
{
    public readonly List<byte> Identifiers = new();

    public int Count => Identifiers.Count;

    public bool Contains(byte identifier) => Identifiers.Contains(identifier);

    public void Add(byte identifier)
    {
        if (!Identifiers.Contains(identifier))
            Identifiers.Add(identifier);
    }
}

public class AttackPattern
{
    public readonly int Width;
    public readonly int Height;
    public readonly AttackPatternCell[] Cells;
    public readonly bool HasSingleAttack;

    AttackPattern(int width, int height, bool hasSingleAttack)
    {
        Width = width;
        Height = height;
        HasSingleAttack = hasSingleAttack;
        Cells = new AttackPatternCell[width * height];
        for (int i = 0; i < Cells.Length; i++)
            Cells[i] = new AttackPatternCell();
    }

    public static AttackPattern Empty() => new(1, 1, false);

    public bool Covers(int row, int column) => IdentifierCount(row, column) > 0;

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
        identifier = cell.Identifiers[0];
        return true;
    }

    AttackPatternCell CellAt(int row, int column)
    {
        if (Cells == null || Width <= 0 || Height <= 0)
            return null;
        if (row < 0 || column < 0 || row >= Height || column >= Width)
            return null;
        return Cells[row * Width + column];
    }

    public bool TryMapBoardToLocal(Cell origin, Cell cell, out int row, out int column)
    {
        row = 0;
        column = 0;
        if (Cells == null || Width <= 0 || Height <= 0)
            return false;

        int centerRow = Height / 2;
        int centerCol = Width / 2;
        column = cell.Column - origin.Column + centerCol;
        row = cell.Row - origin.Row + centerRow;
        return row >= 0 && column >= 0 && row < Height && column < Width;
    }

    public void CollectBoardCellsWithIdentifier(Cell origin, byte identifier, List<Cell> result)
    {
        if (result == null || Cells == null || Width <= 0 || Height <= 0)
            return;

        int centerRow = Height / 2;
        int centerCol = Width / 2;
        for (int row = 0; row < Height; row++)
        {
            for (int col = 0; col < Width; col++)
            {
                if (!ContainsIdentifier(row, col, identifier))
                    continue;
                result.Add(new Cell(origin.Column + (col - centerCol), origin.Row + (row - centerRow)));
            }
        }
    }

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

    static AttackPattern GenerateFromDirections(int range, int[][] directions, bool coverAllDirections)
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

    public static AttackPattern Square(int range, bool coverAllDirections = false, bool includeCenter = false)
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
                if (row == range && col == range && !includeCenter)
                    continue;
                pattern.CellAt(row, col).Add(identifier);
                if (!coverAllDirections)
                    identifier++;
            }
        }

        return pattern;
    }

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
