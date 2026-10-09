using Match.Api;

namespace Match.Rules;

/// <summary>
/// Corner flags. Each seat starts with the two corners on its home row.
/// A living unit standing on a corner captures it. Owning every flag wins.
/// </summary>
public static class FlagRules
{
    public static void Place(MatchState match)
    {
        string hostId = match.Host != null ? match.Host.PlayerId : null;
        var flags = new List<FlagState>();
        Add(flags, 0, 0, hostId);
        Add(flags, match.Columns - 1, 0, hostId);
        Add(flags, 0, match.Rows - 1, null);
        Add(flags, match.Columns - 1, match.Rows - 1, null);
        match.Flags = flags;
        match.WinnerPlayerId = null;
    }

    public static void AssignGuest(MatchState match)
    {
        if (match.Flags == null || match.Guest == null || string.IsNullOrEmpty(match.Guest.PlayerId))
            return;

        int far = match.Rows - 1;
        for (int i = 0; i < match.Flags.Count; i++)
        {
            FlagState flag = match.Flags[i];
            if (flag.Row == far && string.IsNullOrEmpty(flag.OwnerPlayerId))
                flag.OwnerPlayerId = match.Guest.PlayerId;
        }
    }

    /// <summary>
    /// Sets each occupied corner to the unit standing there. Empty corners keep their owner.
    /// </summary>
    public static void Capture(MatchState match)
    {
        if (match.Flags == null || match.Flags.Count == 0 || !string.IsNullOrEmpty(match.WinnerPlayerId))
            return;

        for (int i = 0; i < match.Flags.Count; i++)
        {
            FlagState flag = match.Flags[i];
            UnitState unit = Occupant(match, flag.Column, flag.Row);
            if (unit != null)
                flag.OwnerPlayerId = unit.OwnerPlayerId;
        }

        match.WinnerPlayerId = Winner(match);
    }

    public static bool HasFlag(MatchState match, int column, int row)
    {
        if (match.Flags == null)
            return false;

        for (int i = 0; i < match.Flags.Count; i++)
        {
            FlagState flag = match.Flags[i];
            if (flag.Column == column && flag.Row == row)
                return true;
        }

        return false;
    }

    public static string Winner(MatchState match)
    {
        if (match.Flags == null || match.Flags.Count == 0)
            return null;

        string owner = match.Flags[0].OwnerPlayerId;
        if (string.IsNullOrEmpty(owner))
            return null;

        for (int i = 1; i < match.Flags.Count; i++)
        {
            if (match.Flags[i].OwnerPlayerId != owner)
                return null;
        }

        return owner;
    }

    static void Add(List<FlagState> flags, int column, int row, string ownerPlayerId)
    {
        if (column < 0 || row < 0)
            return;
        for (int i = 0; i < flags.Count; i++)
        {
            if (flags[i].Column == column && flags[i].Row == row)
                return;
        }

        flags.Add(new FlagState
        {
            Column = column,
            Row = row,
            OwnerPlayerId = ownerPlayerId
        });
    }

    static UnitState Occupant(MatchState match, int column, int row)
    {
        for (int i = 0; i < match.Units.Count; i++)
        {
            UnitState unit = match.Units[i];
            if (unit.IsAlive && unit.Column == column && unit.Row == row)
                return unit;
        }

        return null;
    }
}
