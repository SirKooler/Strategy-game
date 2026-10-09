using Match.Api;

namespace Match.Rules;

public static class DeckValidator
{
    public static List<string> Validate(IReadOnlyList<string> deck)
    {
        if (deck == null)
            throw new MatchException("Deck is required.");
        if (deck.Count != MatchLimits.DeckSize)
            throw new MatchException("Deck must contain exactly 9 characters.");

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var clean = new List<string>(MatchLimits.DeckSize);
        for (int i = 0; i < deck.Count; i++)
        {
            string id = InputGuard.CharacterId(deck[i]);
            if (id == CharacterRoster.DummyId)
                throw new MatchException("Dummy is not a playable character.");
            if (!CharacterRoster.IsPlayable(id))
                throw new MatchException("Deck contains an unknown character.");
            if (!seen.Add(id))
                throw new MatchException("Deck characters must be unique.");
            clean.Add(id);
        }

        return clean;
    }
}

public static class InputGuard
{
    public static string PlayerId(string playerId)
    {
        if (string.IsNullOrWhiteSpace(playerId) || playerId.Length > MatchLimits.MaxPlayerIdLength)
            throw new MatchException("Not signed in.");
        if (playerId.IndexOfAny(new[] { '\n', '\r', '\0' }) >= 0)
            throw new MatchException("Not signed in.");
        return playerId;
    }

    public static string MatchId(string matchId)
    {
        if (!Guid.TryParse(matchId, out Guid parsed))
            throw new MatchException("Invalid match.");
        return parsed.ToString("D");
    }

    public static string CharacterId(string id)
    {
        if (string.IsNullOrWhiteSpace(id) || id.Length > MatchLimits.MaxCharacterIdLength)
            throw new MatchException("Unknown character.");
        if (id.IndexOfAny(new[] { '\n', '\r', '\0', '/', '\\', ' ', '\t' }) >= 0)
            throw new MatchException("Unknown character.");
        return id;
    }

    public static string UnitId(string id)
    {
        if (!Guid.TryParse(id, out Guid parsed))
            throw new MatchException("Invalid unit.");
        return parsed.ToString("D");
    }

    public static void EnsureInBounds(int column, int row, int columns, int rows)
    {
        if (column < 0 || row < 0 || column >= columns || row >= rows)
            throw new MatchException("Cell is off the board.");
    }

    public static List<T> CappedList<T>(IReadOnlyList<T> items, int max, string error)
    {
        if (items == null)
            return new List<T>();
        if (items.Count > max)
            throw new MatchException(error);
        return new List<T>(items);
    }
}

public class MatchException : Exception
{
    public MatchException(string message) : base(message)
    {
    }
}
