using Match.Api;
using Newtonsoft.Json;

namespace Match.Rules;

public static class QueueStatus
{
    public const string Waiting = "waiting";
    public const string Matched = "matched";
    public const string Pair = "pair";
    public const string Cancelled = "cancelled";
}

public class QueueTicket
{
    [JsonProperty("playerId")]
    public string PlayerId { get; set; }

    [JsonProperty("deck")]
    public List<string> Deck { get; set; } = new();

    [JsonProperty("enqueuedAt")]
    public long EnqueuedAt { get; set; }

    [JsonProperty("lastSeen")]
    public long LastSeen { get; set; }

    [JsonProperty("status")]
    public string Status { get; set; } = QueueStatus.Waiting;

    [JsonProperty("matchId")]
    public string MatchId { get; set; }
}

public class QueueStep
{
    public const int Trophies = 0;

    public string Status { get; set; }
    public string MatchId { get; set; }
    public string HostPlayerId { get; set; }
    public List<string> HostDeck { get; set; }
    public string GuestPlayerId { get; set; }
    public List<string> GuestDeck { get; set; }

    /// <summary>Player who must poll to receive the match. The caller already has the result.</summary>
    public string WaitingPlayerId { get; set; }
}

public static class MatchmakingQueue
{
    public const long StaleAfterSeconds = 45;

    public static QueueStep Enqueue(List<QueueTicket> tickets, string playerId, List<string> deck, long now)
    {
        DropStale(tickets, now);
        QueueTicket mine = Find(tickets, playerId);
        if (mine != null && mine.Status == QueueStatus.Matched)
            return TakeMatch(tickets, mine);

        if (mine == null)
        {
            mine = new QueueTicket
            {
                PlayerId = playerId,
                Deck = CopyDeck(deck),
                EnqueuedAt = now,
                LastSeen = now,
                Status = QueueStatus.Waiting
            };
            tickets.Add(mine);
        }
        else
        {
            mine.Deck = CopyDeck(deck);
            mine.LastSeen = now;
            mine.Status = QueueStatus.Waiting;
            mine.MatchId = null;
        }

        return TryPair(tickets, mine);
    }

    public static QueueStep Poll(List<QueueTicket> tickets, string playerId, long now)
    {
        DropStale(tickets, now);
        QueueTicket mine = Find(tickets, playerId);
        if (mine == null)
            return Cancelled();
        if (mine.Status == QueueStatus.Matched)
            return TakeMatch(tickets, mine);

        mine.LastSeen = now;
        return TryPair(tickets, mine);
    }

    public static QueueStep Cancel(List<QueueTicket> tickets, string playerId)
    {
        QueueTicket mine = Find(tickets, playerId);
        if (mine == null)
            return Cancelled();
        if (mine.Status == QueueStatus.Matched)
            return TakeMatch(tickets, mine);

        tickets.Remove(mine);
        return Cancelled();
    }

    public static void RememberMatched(List<QueueTicket> tickets, string playerId, string matchId)
    {
        QueueTicket ticket = Find(tickets, playerId);
        if (ticket == null)
        {
            tickets.Add(new QueueTicket
            {
                PlayerId = playerId,
                Status = QueueStatus.Matched,
                MatchId = matchId
            });
            return;
        }

        ticket.Status = QueueStatus.Matched;
        ticket.MatchId = matchId;
    }

    static QueueStep TryPair(List<QueueTicket> tickets, QueueTicket caller)
    {
        QueueTicket oldest = null;
        for (int i = 0; i < tickets.Count; i++)
        {
            QueueTicket ticket = tickets[i];
            if (ticket.PlayerId == caller.PlayerId || ticket.Status != QueueStatus.Waiting)
                continue;
            if (oldest == null || Earlier(ticket, oldest))
                oldest = ticket;
        }

        if (oldest == null)
            return new QueueStep { Status = QueueStatus.Waiting };

        bool callerIsHost = caller.EnqueuedAt < oldest.EnqueuedAt;
        QueueTicket host = callerIsHost ? caller : oldest;
        QueueTicket guest = callerIsHost ? oldest : caller;
        tickets.Remove(caller);
        return new QueueStep
        {
            Status = QueueStatus.Pair,
            HostPlayerId = host.PlayerId,
            HostDeck = CopyDeck(host.Deck),
            GuestPlayerId = guest.PlayerId,
            GuestDeck = CopyDeck(guest.Deck),
            WaitingPlayerId = oldest.PlayerId
        };
    }

    static bool Earlier(QueueTicket ticket, QueueTicket other)
    {
        if (ticket.EnqueuedAt != other.EnqueuedAt)
            return ticket.EnqueuedAt < other.EnqueuedAt;
        return string.CompareOrdinal(ticket.PlayerId, other.PlayerId) < 0;
    }

    static void DropStale(List<QueueTicket> tickets, long now)
    {
        for (int i = tickets.Count - 1; i >= 0; i--)
        {
            QueueTicket ticket = tickets[i];
            if (ticket.Status == QueueStatus.Waiting && now - ticket.LastSeen >= StaleAfterSeconds)
                tickets.RemoveAt(i);
        }
    }

    static QueueStep TakeMatch(List<QueueTicket> tickets, QueueTicket mine)
    {
        string matchId = mine.MatchId;
        tickets.Remove(mine);
        return new QueueStep
        {
            Status = QueueStatus.Matched,
            MatchId = matchId
        };
    }

    static QueueStep Cancelled()
    {
        return new QueueStep { Status = QueueStatus.Cancelled };
    }

    static QueueTicket Find(List<QueueTicket> tickets, string playerId)
    {
        for (int i = 0; i < tickets.Count; i++)
        {
            if (tickets[i].PlayerId == playerId)
                return tickets[i];
        }

        return null;
    }

    static List<string> CopyDeck(List<string> deck)
    {
        return deck == null ? new List<string>() : new List<string>(deck);
    }
}
