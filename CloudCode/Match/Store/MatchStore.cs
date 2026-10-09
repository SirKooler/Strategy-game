using Match.Api;
using Match.Rules;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;
using Unity.Services.CloudSave.Model;

namespace Match.Store;

public class MatchStore
{
    public const string StateKey = "state";
    public const string StatusKey = "status";
    public const string BoardKey = "board";
    public const string DecksKey = "decks";
    public const string PendingPlansKey = "pendingPlans";
    public const string LastTicksKey = "lastTicks";

    static readonly JsonSerializerSettings JsonSettings = new()
    {
        NullValueHandling = NullValueHandling.Ignore
    };

    readonly IGameApiClient _gameApi;

    public MatchStore(IGameApiClient gameApi)
    {
        _gameApi = gameApi;
    }

    public async Task<LoadedMatch> Load(IExecutionContext context, string matchId)
    {
        matchId = InputGuard.MatchId(matchId);
        var response = await _gameApi.CloudSaveData.GetCustomItemsAsync(
            context,
            context.ServiceToken,
            context.ProjectId,
            matchId,
            new List<string> { StateKey });

        var results = response?.Data?.Results;
        if (results == null || results.Count == 0)
            throw new MatchException("Match not found.");

        var item = results.Find(r => r.Key == StateKey);
        if (item == null)
            throw new MatchException("Match not found.");

        string json = ReadString(item.Value);
        if (string.IsNullOrEmpty(json))
            throw new MatchException("Match not found.");

        MatchState state = JsonConvert.DeserializeObject<MatchState>(json, JsonSettings);
        if (state == null || state.MatchId != matchId)
            throw new MatchException("Match not found.");

        return new LoadedMatch
        {
            State = state,
            WriteLock = item.WriteLock
        };
    }

    public async Task Save(IExecutionContext context, MatchState state, string writeLock)
    {
        string matchId = InputGuard.MatchId(state.MatchId);
        string stateJson = JsonConvert.SerializeObject(state, JsonSettings);
        var items = new List<SetItemBody>
        {
            LockedItem(StateKey, stateJson, writeLock),
            new(StatusKey, StatusPayload(state)),
            new(BoardKey, BoardPayload(state)),
            new(DecksKey, DecksPayload(state)),
            new(PendingPlansKey, PendingPayload(state)),
            new(LastTicksKey, state.LastTicks ?? new List<BattleTickDto>())
        };

        try
        {
            await _gameApi.CloudSaveData.SetCustomItemBatchAsync(
                context,
                context.ServiceToken,
                context.ProjectId,
                matchId,
                new SetItemBatchBody(items));
        }
        catch (Exception)
        {
            if (string.IsNullOrEmpty(writeLock))
                throw;
            await _gameApi.CloudSaveData.SetCustomItemBatchAsync(
                context,
                context.ServiceToken,
                context.ProjectId,
                matchId,
                new SetItemBatchBody(new List<SetItemBody>
                {
                    new(StateKey, stateJson),
                    new(StatusKey, StatusPayload(state)),
                    new(BoardKey, BoardPayload(state)),
                    new(DecksKey, DecksPayload(state)),
                    new(PendingPlansKey, PendingPayload(state)),
                    new(LastTicksKey, state.LastTicks ?? new List<BattleTickDto>())
                }));
        }
    }

    public void EnsureMember(MatchState state, string playerId)
    {
        if (state.SeatOf(playerId) == null)
            throw new MatchException("Not in this match.");
    }

    static SetItemBody LockedItem(string key, object value, string writeLock)
    {
        var item = new SetItemBody(key, value);
        if (!string.IsNullOrEmpty(writeLock))
            item.WriteLock = writeLock;
        return item;
    }

    static object StatusPayload(MatchState state)
    {
        return new
        {
            phase = state.Phase,
            turn = state.Turn,
            hostPlayerId = state.Host?.PlayerId,
            guestPlayerId = state.Guest?.PlayerId,
            hostPlansSubmitted = state.Host != null && state.Host.PlansSubmitted,
            guestPlansSubmitted = state.Guest != null && state.Guest.PlansSubmitted,
            hostPaidSpawnCount = state.Host?.PaidSpawnCount ?? 0,
            guestPaidSpawnCount = state.Guest?.PaidSpawnCount ?? 0
        };
    }

    static object BoardPayload(MatchState state)
    {
        var cells = new List<object>();
        for (int i = 0; i < state.Units.Count; i++)
        {
            UnitState unit = state.Units[i];
            cells.Add(new
            {
                unitId = unit.UnitId,
                ownerPlayerId = unit.OwnerPlayerId,
                characterId = unit.CharacterId,
                column = unit.Column,
                row = unit.Row,
                health = unit.Health,
                maxHealth = unit.MaxHealth,
                damage = unit.Damage,
                alive = unit.IsAlive
            });
        }

        return new { columns = state.Columns, rows = state.Rows, units = cells };
    }

    static object DecksPayload(MatchState state)
    {
        return new
        {
            host = SeatDeck(state.Host),
            guest = SeatDeck(state.Guest)
        };
    }

    static object SeatDeck(SeatState seat)
    {
        if (seat == null)
            return null;
        return new
        {
            playerId = seat.PlayerId,
            deck = seat.Deck,
            spawned = seat.Spawned,
            dead = seat.Dead
        };
    }

    static object PendingPayload(MatchState state)
    {
        return new
        {
            hostSubmitted = state.Host != null && state.Host.PlansSubmitted,
            guestSubmitted = state.Guest != null && state.Guest.PlansSubmitted
        };
    }

    static string ReadString(object value)
    {
        if (value == null)
            return null;
        if (value is string text)
            return text;
        if (value is JToken token)
        {
            if (token.Type == JTokenType.String)
                return token.ToObject<string>();
            return token.ToString(Formatting.None);
        }

        return value.ToString();
    }
}

public class LoadedMatch
{
    public MatchState State { get; set; }
    public string WriteLock { get; set; }
}
