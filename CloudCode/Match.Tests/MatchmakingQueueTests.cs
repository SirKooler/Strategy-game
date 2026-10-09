using Match.Rules;
using Xunit;

namespace Match.Tests;

public class MatchmakingQueueTests
{
    static List<string> Deck()
    {
        return new List<string> { "Tank" };
    }

    static QueueTicket Waiting(string playerId, long at)
    {
        return new QueueTicket
        {
            PlayerId = playerId,
            Deck = Deck(),
            EnqueuedAt = at,
            LastSeen = at,
            Status = QueueStatus.Waiting
        };
    }

    [Fact]
    public void TwoPlayersPairAndTheOlderOneIsHost()
    {
        var tickets = new List<QueueTicket>();
        QueueStep first = MatchmakingQueue.Enqueue(tickets, "player-a", Deck(), 1_000);
        Assert.Equal(QueueStatus.Waiting, first.Status);
        Assert.Equal(0, QueueStep.Trophies);

        QueueStep second = MatchmakingQueue.Enqueue(tickets, "player-b", Deck(), 1_010);
        Assert.Equal(QueueStatus.Pair, second.Status);
        Assert.Equal(0, QueueStep.Trophies);
        Assert.Equal("player-a", second.HostPlayerId);
        Assert.Equal("player-b", second.GuestPlayerId);
        Assert.Single(tickets);
        Assert.Equal("player-a", tickets[0].PlayerId);

        MatchmakingQueue.RememberMatched(tickets, second.WaitingPlayerId, "11111111-1111-1111-1111-111111111111");
        QueueStep polled = MatchmakingQueue.Poll(tickets, "player-a", 1_020);
        Assert.Equal(QueueStatus.Matched, polled.Status);
        Assert.Equal(0, QueueStep.Trophies);
        Assert.Equal("11111111-1111-1111-1111-111111111111", polled.MatchId);
        Assert.Empty(tickets);
    }

    [Fact]
    public void ThirdPlayerMatchesTheOldestAndLeavesTheNewerWaiting()
    {
        var tickets = new List<QueueTicket>
        {
            Waiting("player-a", 1_000),
            Waiting("player-b", 1_010)
        };

        QueueStep step = MatchmakingQueue.Enqueue(tickets, "player-c", Deck(), 1_020);
        Assert.Equal(QueueStatus.Pair, step.Status);
        Assert.Equal(0, QueueStep.Trophies);
        Assert.Equal("player-a", step.HostPlayerId);
        Assert.Equal("player-c", step.GuestPlayerId);

        MatchmakingQueue.RememberMatched(tickets, step.WaitingPlayerId, "22222222-2222-2222-2222-222222222222");
        Assert.Equal(2, tickets.Count);
        Assert.Contains(tickets, ticket => ticket.PlayerId == "player-a" && ticket.Status == QueueStatus.Matched);
        Assert.Contains(tickets, ticket => ticket.PlayerId == "player-b" && ticket.Status == QueueStatus.Waiting);
        Assert.DoesNotContain(tickets, ticket => ticket.PlayerId == "player-c");
    }

    [Fact]
    public void PlayerDoesNotMatchThemselves()
    {
        var tickets = new List<QueueTicket>();
        MatchmakingQueue.Enqueue(tickets, "player-a", Deck(), 1_000);
        QueueStep again = MatchmakingQueue.Enqueue(tickets, "player-a", Deck(), 1_030);
        Assert.Equal(QueueStatus.Waiting, again.Status);
        Assert.Equal(0, QueueStep.Trophies);
        Assert.Single(tickets);
        Assert.Equal(1_000, tickets[0].EnqueuedAt);
        Assert.Equal(1_030, tickets[0].LastSeen);
    }

    [Fact]
    public void StaleTicketIsDropped()
    {
        var tickets = new List<QueueTicket> { Waiting("player-a", 1_000) };
        QueueStep step = MatchmakingQueue.Enqueue(tickets, "player-b", Deck(), 1_000 + MatchmakingQueue.StaleAfterSeconds);
        Assert.Equal(QueueStatus.Waiting, step.Status);
        Assert.Equal(0, QueueStep.Trophies);
        Assert.Single(tickets);
        Assert.Equal("player-b", tickets[0].PlayerId);
    }

    [Fact]
    public void CancelRemovesOnlyTheWaitingTicket()
    {
        var tickets = new List<QueueTicket>
        {
            Waiting("player-a", 1_000),
            Waiting("player-b", 1_010),
            new QueueTicket
            {
                PlayerId = "player-c",
                Status = QueueStatus.Matched,
                MatchId = "33333333-3333-3333-3333-333333333333"
            }
        };

        QueueStep cancelled = MatchmakingQueue.Cancel(tickets, "player-a");
        Assert.Equal(QueueStatus.Cancelled, cancelled.Status);
        Assert.Equal(0, QueueStep.Trophies);
        Assert.DoesNotContain(tickets, ticket => ticket.PlayerId == "player-a");
        Assert.Contains(tickets, ticket => ticket.PlayerId == "player-b" && ticket.Status == QueueStatus.Waiting);
        Assert.Contains(tickets, ticket => ticket.PlayerId == "player-c" && ticket.Status == QueueStatus.Matched);

        QueueStep matched = MatchmakingQueue.Cancel(tickets, "player-c");
        Assert.Equal(QueueStatus.Matched, matched.Status);
        Assert.Equal(0, QueueStep.Trophies);
        Assert.Equal("33333333-3333-3333-3333-333333333333", matched.MatchId);
        Assert.DoesNotContain(tickets, ticket => ticket.PlayerId == "player-c");
        Assert.Single(tickets);
    }
}
