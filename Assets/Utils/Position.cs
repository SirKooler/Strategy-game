using System;

/// <summary>
/// A cell on the board: column and row.
/// Use this instead of passing the two numbers separately.
/// </summary>
[Serializable]
public class Position
{
    public int Column;
    public int Row;

    public Position()
    {
    }

    public Position(int column, int row)
    {
        Set(column, row);
    }

    public void Set(int column, int row)
    {
        Column = column;
        Row = row;
    }

    public void Set(Position other)
    {
        if (other == null)
            return;
        Column = other.Column;
        Row = other.Row;
    }

    public Position Copy()
    {
        return new Position(Column, Row);
    }

    public bool SameCell(Position other)
    {
        return other != null && Column == other.Column && Row == other.Row;
    }

    public override bool Equals(object obj)
    {
        return obj is Position other && SameCell(other);
    }

    public override int GetHashCode()
    {
        return (Column * 397) ^ Row;
    }

    public override string ToString()
    {
        return $"{Column},{Row}";
    }
}
