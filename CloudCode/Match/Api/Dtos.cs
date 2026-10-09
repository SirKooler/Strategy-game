using Newtonsoft.Json;

namespace Match.Api;

public static class MatchLimits
{
    public const int DeckSize = 9;
    public const int BoardColumns = 6;
    public const int BoardRows = 6;
    public const int MaxOrders = 16;
    public const int MaxSpawns = 9;
    public const int MaxUnspawns = 9;
    public const int MaxCharacterIdLength = 32;
    public const int MaxPlayerIdLength = 64;
}

public static class MatchPhases
{
    public const string WaitingForOpponent = "WaitingForOpponent";
    public const string Plan = "Plan";
    public const string Resolving = "Resolving";
    public const string Won = "Won";
}

public class CellDto
{
    [JsonProperty("column")]
    public int Column { get; set; }

    [JsonProperty("row")]
    public int Row { get; set; }
}

public class OrderDto
{
    [JsonProperty("unitId")]
    public string UnitId { get; set; }

    [JsonProperty("characterId")]
    public string CharacterId { get; set; }

    [JsonProperty("type")]
    public string Type { get; set; }

    [JsonProperty("column")]
    public int Column { get; set; }

    [JsonProperty("row")]
    public int Row { get; set; }

    [JsonProperty("abilityId")]
    public int AbilityId { get; set; }
}

public class SpawnDto
{
    [JsonProperty("characterId")]
    public string CharacterId { get; set; }

    [JsonProperty("column")]
    public int Column { get; set; }

    [JsonProperty("row")]
    public int Row { get; set; }
}

public class SubmitPlansRequest
{
    [JsonProperty("matchId")]
    public string MatchId { get; set; }

    [JsonProperty("turn")]
    public int Turn { get; set; }

    [JsonProperty("orders")]
    public List<OrderDto> Orders { get; set; }

    [JsonProperty("spawns")]
    public List<SpawnDto> Spawns { get; set; }

    [JsonProperty("unspawnUnitIds")]
    public List<string> UnspawnUnitIds { get; set; }
}

public class SeatState
{
    [JsonProperty("playerId")]
    public string PlayerId { get; set; }

    [JsonProperty("deck")]
    public List<string> Deck { get; set; } = new();

    [JsonProperty("spawned")]
    public List<string> Spawned { get; set; } = new();

    [JsonProperty("dead")]
    public List<string> Dead { get; set; } = new();

    [JsonProperty("paidSpawnCount")]
    public int PaidSpawnCount { get; set; }

    [JsonProperty("spawnEnergySpent")]
    public int SpawnEnergySpent { get; set; }

    [JsonProperty("plansSubmitted")]
    public bool PlansSubmitted { get; set; }

    [JsonProperty("pending")]
    public PendingTurn Pending { get; set; }
}

public class PendingTurn
{
    [JsonProperty("orders")]
    public List<OrderDto> Orders { get; set; } = new();

    [JsonProperty("spawns")]
    public List<SpawnDto> Spawns { get; set; } = new();

    [JsonProperty("unspawnUnitIds")]
    public List<string> UnspawnUnitIds { get; set; } = new();
}

public class UnitState
{
    [JsonProperty("unitId")]
    public string UnitId { get; set; }

    [JsonProperty("ownerPlayerId")]
    public string OwnerPlayerId { get; set; }

    [JsonProperty("characterId")]
    public string CharacterId { get; set; }

    [JsonProperty("column")]
    public int Column { get; set; }

    [JsonProperty("row")]
    public int Row { get; set; }

    [JsonProperty("health")]
    public int Health { get; set; }

    [JsonProperty("maxHealth")]
    public int MaxHealth { get; set; }

    [JsonProperty("damage")]
    public int Damage { get; set; }

    [JsonProperty("isOpeningSpawn")]
    public bool IsOpeningSpawn { get; set; }

    [JsonProperty("hasEnteredBattle")]
    public bool HasEnteredBattle { get; set; }

    [JsonProperty("useSpeedsterMove")]
    public bool UseSpeedsterMove { get; set; }

    [JsonIgnore]
    public PlannedActionState Plan { get; set; }

    [JsonIgnore]
    public bool HasResolved { get; set; }

    [JsonIgnore]
    public bool IsAlive => Health > 0;
}

public class PlannedActionState
{
    public string Type { get; set; }
    public List<CellDto> Targets { get; set; } = new();
}

public class FlagState
{
    [JsonProperty("column")]
    public int Column { get; set; }

    [JsonProperty("row")]
    public int Row { get; set; }

    [JsonProperty("ownerPlayerId")]
    public string OwnerPlayerId { get; set; }
}

public class MatchState
{
    [JsonProperty("matchId")]
    public string MatchId { get; set; }

    [JsonProperty("columns")]
    public int Columns { get; set; } = MatchLimits.BoardColumns;

    [JsonProperty("rows")]
    public int Rows { get; set; } = MatchLimits.BoardRows;

    [JsonProperty("turn")]
    public int Turn { get; set; } = 1;

    [JsonProperty("phase")]
    public string Phase { get; set; } = MatchPhases.WaitingForOpponent;

    [JsonProperty("host")]
    public SeatState Host { get; set; } = new();

    [JsonProperty("guest")]
    public SeatState Guest { get; set; } = new();

    [JsonProperty("units")]
    public List<UnitState> Units { get; set; } = new();

    [JsonProperty("flags")]
    public List<FlagState> Flags { get; set; } = new();

    [JsonProperty("winnerPlayerId")]
    public string WinnerPlayerId { get; set; }

    [JsonProperty("lastTicks")]
    public List<BattleTickDto> LastTicks { get; set; } = new();

    [JsonIgnore]
    public int BoardSize => Math.Max(Columns, Rows);

    public SeatState SeatOf(string playerId)
    {
        if (Host != null && Host.PlayerId == playerId)
            return Host;
        if (Guest != null && Guest.PlayerId == playerId)
            return Guest;
        return null;
    }

    public SeatState OpponentOf(string playerId)
    {
        if (Host != null && Host.PlayerId == playerId)
            return Guest;
        if (Guest != null && Guest.PlayerId == playerId)
            return Host;
        return null;
    }

    public int HomeRow(string playerId)
    {
        return Host != null && Host.PlayerId == playerId ? 0 : Rows - 1;
    }
}

public class BattleMoveDto
{
    [JsonProperty("unitId")]
    public string UnitId { get; set; }

    [JsonProperty("column")]
    public int Column { get; set; }

    [JsonProperty("row")]
    public int Row { get; set; }

    [JsonProperty("blocked")]
    public bool Blocked { get; set; }
}

public class BattleAbilityDto
{
    [JsonProperty("unitId")]
    public string UnitId { get; set; }

    [JsonProperty("kind")]
    public string Kind { get; set; }

    [JsonProperty("damage")]
    public int Damage { get; set; }

    [JsonProperty("targets")]
    public List<CellDto> Targets { get; set; } = new();
}

public class BattleSpawnDto
{
    [JsonProperty("unitId")]
    public string UnitId { get; set; }

    [JsonProperty("ownerPlayerId")]
    public string OwnerPlayerId { get; set; }

    [JsonProperty("characterId")]
    public string CharacterId { get; set; }

    [JsonProperty("column")]
    public int Column { get; set; }

    [JsonProperty("row")]
    public int Row { get; set; }

    [JsonProperty("health")]
    public int Health { get; set; }

    [JsonProperty("maxHealth")]
    public int MaxHealth { get; set; }

    [JsonProperty("damage")]
    public int Damage { get; set; }
}

public class BattleTickDto
{
    [JsonProperty("ring")]
    public int Ring { get; set; }

    [JsonProperty("isEmpty")]
    public bool IsEmpty { get; set; }

    [JsonProperty("isSpawn")]
    public bool IsSpawn { get; set; }

    [JsonProperty("spawns")]
    public List<BattleSpawnDto> Spawns { get; set; } = new();

    [JsonProperty("ringCells")]
    public List<CellDto> RingCells { get; set; } = new();

    [JsonProperty("moves")]
    public List<BattleMoveDto> Moves { get; set; } = new();

    [JsonProperty("abilities")]
    public List<BattleAbilityDto> Abilities { get; set; } = new();
}

public class MatchSnapshot
{
    [JsonProperty("matchId")]
    public string MatchId { get; set; }

    [JsonProperty("turn")]
    public int Turn { get; set; }

    [JsonProperty("phase")]
    public string Phase { get; set; }

    [JsonProperty("columns")]
    public int Columns { get; set; }

    [JsonProperty("rows")]
    public int Rows { get; set; }

    [JsonProperty("homeRow")]
    public int HomeRow { get; set; }

    [JsonProperty("localPlayerId")]
    public string LocalPlayerId { get; set; }

    [JsonProperty("opponentPlayerId")]
    public string OpponentPlayerId { get; set; }

    [JsonProperty("local")]
    public SeatPublic Local { get; set; }

    [JsonProperty("opponent")]
    public SeatPublic Opponent { get; set; }

    [JsonProperty("units")]
    public List<UnitState> Units { get; set; } = new();

    [JsonProperty("flags")]
    public List<FlagState> Flags { get; set; } = new();

    [JsonProperty("winnerPlayerId")]
    public string WinnerPlayerId { get; set; }

    [JsonProperty("lastTicks")]
    public List<BattleTickDto> LastTicks { get; set; } = new();

    [JsonProperty("waitingForOpponent")]
    public bool WaitingForOpponent { get; set; }

    [JsonProperty("waitingForOpponentPlans")]
    public bool WaitingForOpponentPlans { get; set; }
}

public class SeatPublic
{
    [JsonProperty("playerId")]
    public string PlayerId { get; set; }

    [JsonProperty("deck")]
    public List<string> Deck { get; set; } = new();

    [JsonProperty("spawned")]
    public List<string> Spawned { get; set; } = new();

    [JsonProperty("dead")]
    public List<string> Dead { get; set; } = new();

    [JsonProperty("paidSpawnCount")]
    public int PaidSpawnCount { get; set; }

    [JsonProperty("energyRemaining")]
    public int EnergyRemaining { get; set; }

    [JsonProperty("plansSubmitted")]
    public bool PlansSubmitted { get; set; }
}

public class SubmitPlansResult
{
    [JsonProperty("accepted")]
    public bool Accepted { get; set; }

    [JsonProperty("waitingForOpponent")]
    public bool WaitingForOpponent { get; set; }

    [JsonProperty("ticks")]
    public List<BattleTickDto> Ticks { get; set; } = new();

    [JsonProperty("match")]
    public MatchSnapshot Match { get; set; }
}

public class QueueResult
{
    [JsonProperty("status")]
    public string Status { get; set; }

    [JsonProperty("trophies")]
    public int Trophies { get; set; }

    [JsonProperty("match")]
    public MatchSnapshot Match { get; set; }
}
