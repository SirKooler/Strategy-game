using Match.Api;
using Match.Rules;
using Match.Store;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;

namespace Match;

public class MatchModule
{
    [CloudCodeFunction("CreateMatch")]
    public async Task<MatchSnapshot> CreateMatch(
        IExecutionContext context,
        IGameApiClient gameApi,
        List<string> deck)
    {
        try
        {
            string playerId = RequirePlayer(context);
            MatchState state = MatchEngine.Create(playerId, deck);
            await new MatchStore(gameApi).Save(context, state, null);
            return Snapshot.For(state, playerId);
        }
        catch (MatchException ex)
        {
            throw new Exception(ex.Message);
        }
    }

    [CloudCodeFunction("JoinMatch")]
    public async Task<MatchSnapshot> JoinMatch(
        IExecutionContext context,
        IGameApiClient gameApi,
        string matchId,
        List<string> deck)
    {
        try
        {
            string playerId = RequirePlayer(context);
            var store = new MatchStore(gameApi);
            LoadedMatch loaded = await store.Load(context, matchId);
            if (MatchEngine.Join(loaded.State, playerId, deck))
                await store.Save(context, loaded.State, loaded.WriteLock);
            return Snapshot.For(loaded.State, playerId);
        }
        catch (MatchException ex)
        {
            throw new Exception(ex.Message);
        }
    }

    [CloudCodeFunction("SubmitPlans")]
    public async Task<SubmitPlansResult> SubmitPlans(
        IExecutionContext context,
        IGameApiClient gameApi,
        string matchId,
        int turn,
        List<OrderDto> orders,
        List<SpawnDto> spawns,
        List<string> unspawnUnitIds)
    {
        try
        {
            string playerId = RequirePlayer(context);
            var store = new MatchStore(gameApi);
            LoadedMatch loaded = await store.Load(context, matchId);
            store.EnsureMember(loaded.State, playerId);
            var request = new SubmitPlansRequest
            {
                MatchId = loaded.State.MatchId,
                Turn = turn,
                Orders = orders,
                Spawns = spawns,
                UnspawnUnitIds = unspawnUnitIds
            };
            SubmitPlansResult result = MatchEngine.Submit(loaded.State, playerId, request);
            await store.Save(context, loaded.State, loaded.WriteLock);
            return result;
        }
        catch (MatchException ex)
        {
            throw new Exception(ex.Message);
        }
    }

    [CloudCodeFunction("WithdrawPlans")]
    public async Task<MatchSnapshot> WithdrawPlans(
        IExecutionContext context,
        IGameApiClient gameApi,
        string matchId)
    {
        try
        {
            string playerId = RequirePlayer(context);
            var store = new MatchStore(gameApi);
            LoadedMatch loaded = await store.Load(context, matchId);
            store.EnsureMember(loaded.State, playerId);
            MatchSnapshot snapshot = MatchEngine.Withdraw(loaded.State, playerId);
            await store.Save(context, loaded.State, loaded.WriteLock);
            return snapshot;
        }
        catch (MatchException ex)
        {
            throw new Exception(ex.Message);
        }
    }

    [CloudCodeFunction("Enqueue")]
    public async Task<QueueResult> Enqueue(
        IExecutionContext context,
        IGameApiClient gameApi,
        List<string> deck)
    {
        try
        {
            string playerId = RequirePlayer(context);
            List<string> clean = DeckValidator.Validate(deck);
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            return await RunQueue(context, gameApi, playerId, tickets => MatchmakingQueue.Enqueue(tickets, playerId, clean, now));
        }
        catch (MatchException ex)
        {
            throw new Exception(ex.Message);
        }
    }

    [CloudCodeFunction("PollQueue")]
    public async Task<QueueResult> PollQueue(IExecutionContext context, IGameApiClient gameApi)
    {
        try
        {
            string playerId = RequirePlayer(context);
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            return await RunQueue(context, gameApi, playerId, tickets => MatchmakingQueue.Poll(tickets, playerId, now));
        }
        catch (MatchException ex)
        {
            throw new Exception(ex.Message);
        }
    }

    [CloudCodeFunction("CancelQueue")]
    public async Task<QueueResult> CancelQueue(IExecutionContext context, IGameApiClient gameApi)
    {
        try
        {
            string playerId = RequirePlayer(context);
            return await RunQueue(context, gameApi, playerId, tickets => MatchmakingQueue.Cancel(tickets, playerId));
        }
        catch (MatchException ex)
        {
            throw new Exception(ex.Message);
        }
    }

    async Task<QueueResult> RunQueue(
        IExecutionContext context,
        IGameApiClient gameApi,
        string playerId,
        Func<List<QueueTicket>, QueueStep> apply)
    {
        var queue = new QueueStore(gameApi);
        var matches = new MatchStore(gameApi);
        Exception last = null;
        for (int attempt = 0; attempt < 4; attempt++)
        {
            try
            {
                LoadedQueue loaded = await queue.Load(context);
                QueueStep step = apply(loaded.Tickets);
                MatchSnapshot snapshot = null;
                if (step.Status == QueueStatus.Pair)
                {
                    MatchState created = MatchEngine.Create(step.HostPlayerId, step.HostDeck);
                    if (!MatchEngine.Join(created, step.GuestPlayerId, step.GuestDeck))
                        throw new MatchException("Could not match players.");
                    await matches.Save(context, created, null);
                    MatchmakingQueue.RememberMatched(loaded.Tickets, step.WaitingPlayerId, created.MatchId);
                    snapshot = Snapshot.For(created, playerId);
                    step = new QueueStep { Status = QueueStatus.Matched, MatchId = created.MatchId };
                }
                else if (step.Status == QueueStatus.Matched)
                {
                    LoadedMatch existing = await matches.Load(context, step.MatchId);
                    snapshot = Snapshot.For(existing.State, playerId);
                }

                await queue.Save(context, loaded);
                return new QueueResult
                {
                    Status = step.Status,
                    Trophies = QueueStep.Trophies,
                    Match = snapshot
                };
            }
            catch (MatchException ex)
            {
                throw new Exception(ex.Message);
            }
            catch (Exception ex)
            {
                last = ex;
            }
        }

        throw new Exception(last != null ? last.Message : "Queue is busy.");
    }

    [CloudCodeFunction("GetMatch")]
    public async Task<MatchSnapshot> GetMatch(
        IExecutionContext context,
        IGameApiClient gameApi,
        string matchId)
    {
        try
        {
            string playerId = RequirePlayer(context);
            var store = new MatchStore(gameApi);
            LoadedMatch loaded = await store.Load(context, matchId);
            store.EnsureMember(loaded.State, playerId);
            return Snapshot.For(loaded.State, playerId);
        }
        catch (MatchException ex)
        {
            throw new Exception(ex.Message);
        }
    }

    static string RequirePlayer(IExecutionContext context)
    {
        return InputGuard.PlayerId(context.PlayerId);
    }
}
