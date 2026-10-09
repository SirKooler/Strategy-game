using Match.Api;
using Match.Rules;
using Xunit;

namespace Match.Tests;

public class BattleSimulatorTests
{
    [Fact]
    public void ContestedMoveIsBlocked()
    {
        var match = EmptyMatch();
        match.Units.Add(Unit("a", match.Host.PlayerId, 2, 2, PlanTypes.Move, 3, 2));
        match.Units.Add(Unit("b", match.Guest.PlayerId, 4, 2, PlanTypes.Move, 3, 2));

        List<BattleTickDto> ticks = BattleSimulator.Resolve(match);
        BattleMoveDto moveA = FindMove(ticks, "a");
        BattleMoveDto moveB = FindMove(ticks, "b");
        Assert.True(moveA.Blocked);
        Assert.True(moveB.Blocked);
        Assert.Equal(2, match.Units.Find(u => u.UnitId == "a").Column);
        Assert.Equal(4, match.Units.Find(u => u.UnitId == "b").Column);
    }

    [Fact]
    public void AttackDamagesEnemyNotAlly()
    {
        var match = EmptyMatch();
        match.Units.Add(Unit("ally", match.Host.PlayerId, 2, 1, null, 0, 0, health: 10));
        var attacker = Unit("atk", match.Host.PlayerId, 2, 2, PlanTypes.Attack, 2, 3, damage: 5);
        attacker.Plan.Targets = new List<CellDto>
        {
            new() { Column = 2, Row = 1 },
            new() { Column = 2, Row = 3 }
        };
        match.Units.Add(attacker);
        match.Units.Add(Unit("enemy", match.Guest.PlayerId, 2, 3, null, 0, 0, health: 8));

        BattleSimulator.Resolve(match);

        Assert.Equal(10, match.Units.Find(u => u.UnitId == "ally").Health);
        Assert.Equal(3, match.Units.Find(u => u.UnitId == "enemy").Health);
    }

    [Fact]
    public void CreateAndJoinValidatesDecksAndSpawnsOpeners()
    {
        string[] deck = { "Tank", "Ranger", "Octopus", "Sniper", "SwordMan", "Healer", "Speedster", "Laser", "Controller" };
        MatchState match = MatchEngine.Create("player-host-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", deck);
        Assert.Equal(MatchPhases.WaitingForOpponent, match.Phase);
        Assert.Single(match.Units);
        Assert.Equal(0, match.Units[0].Row);

        MatchEngine.Join(match, "player-guest-bbbbbbbbbbbbbbbbbbbbbbbbbbbbbb", deck);
        Assert.Equal(MatchPhases.Plan, match.Phase);
        Assert.Equal(2, match.Units.Count);
        Assert.Contains(match.Units, u => u.Row == match.Rows - 1);
        Assert.Equal(match.Host.PlayerId, OwnerAt(match, 0, 0));
        Assert.Equal(match.Host.PlayerId, OwnerAt(match, match.Columns - 1, 0));
        Assert.Equal(match.Guest.PlayerId, OwnerAt(match, 0, match.Rows - 1));
        Assert.Equal(match.Guest.PlayerId, OwnerAt(match, match.Columns - 1, match.Rows - 1));
    }

    [Fact]
    public void SpawnOnFlagIsRejected()
    {
        string[] deck = { "Tank", "Ranger", "Octopus", "Sniper", "SwordMan", "Healer", "Speedster", "Laser", "Controller" };
        const string host = "player-host-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        const string guest = "player-guest-bbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
        MatchState match = MatchEngine.Create(host, deck);
        MatchEngine.Join(match, guest, deck);

        string characterId = deck.First(id => !match.Host.Spawned.Contains(id));
        var onFlag = new SubmitPlansRequest
        {
            Turn = match.Turn,
            Spawns = new List<SpawnDto>
            {
                new() { CharacterId = characterId, Column = 0, Row = 0 }
            }
        };

        MatchException rejected = Assert.Throws<MatchException>(() => PlanValidator.ValidateTurn(match, host, onFlag));
        Assert.Equal("Cannot spawn on a flag.", rejected.Message);

        var onOpenTile = new SubmitPlansRequest
        {
            Turn = match.Turn,
            Spawns = new List<SpawnDto>
            {
                new() { CharacterId = characterId, Column = 1, Row = 0 }
            }
        };
        PlanValidator.ValidateTurn(match, host, onOpenTile);
    }

    [Fact]
    public void MoveOntoCornerCapturesFlag()
    {
        MatchState match = FlagMatch();
        match.Units.Add(Unit("host", match.Host.PlayerId, 5, 4, PlanTypes.Move, 5, 5));

        BattleSimulator.Resolve(match);

        Assert.Equal(match.Host.PlayerId, OwnerAt(match, 5, 5));
        Assert.Equal(5, match.Units[0].Row);
        Assert.Null(match.WinnerPlayerId);
    }

    [Fact]
    public void BlockedMoveDoesNotCapture()
    {
        MatchState match = FlagMatch();
        match.Units.Add(Unit("host", match.Host.PlayerId, 5, 4, PlanTypes.Move, 5, 5));
        match.Units.Add(Unit("guest", match.Guest.PlayerId, 4, 5, PlanTypes.Move, 5, 5));

        BattleSimulator.Resolve(match);

        Assert.Equal(match.Guest.PlayerId, OwnerAt(match, 5, 5));
        Assert.Equal(4, match.Units.Find(u => u.UnitId == "host").Row);
        Assert.Equal(4, match.Units.Find(u => u.UnitId == "guest").Column);
    }

    [Fact]
    public void LeavingFlagDoesNotReleaseIt()
    {
        MatchState match = FlagMatch();
        match.Units.Add(Unit("host", match.Host.PlayerId, 0, 5, PlanTypes.Move, 1, 5));

        BattleSimulator.Resolve(match);

        Assert.Equal(match.Host.PlayerId, OwnerAt(match, 0, 5));
        Assert.Equal(1, match.Units[0].Column);
        Assert.Null(match.WinnerPlayerId);
    }

    [Fact]
    public void SameRingCapturesBothApplyBeforeWinCheck()
    {
        MatchState match = FlagMatch();
        match.Units.Add(Unit("host", match.Host.PlayerId, 5, 4, PlanTypes.Move, 5, 5));
        match.Units.Add(Unit("guest", match.Guest.PlayerId, 0, 1, PlanTypes.Move, 0, 0));

        BattleSimulator.Resolve(match);

        Assert.Equal(match.Host.PlayerId, OwnerAt(match, 5, 5));
        Assert.Equal(match.Guest.PlayerId, OwnerAt(match, 0, 0));
        Assert.Equal(5, match.Units.Find(u => u.UnitId == "host").Row);
        Assert.Equal(0, match.Units.Find(u => u.UnitId == "guest").Row);
        Assert.Null(match.WinnerPlayerId);
    }

    [Fact]
    public void OwningAllFlagsStopsLaterRings()
    {
        MatchState match = FlagMatch();
        FlagAt(match, 0, 5).OwnerPlayerId = match.Host.PlayerId;
        match.Units.Add(Unit("holder", match.Host.PlayerId, 5, 5, null, 0, 0));
        match.Units.Add(Unit("later", match.Guest.PlayerId, 0, 4, PlanTypes.Move, 1, 4));

        List<BattleTickDto> ticks = BattleSimulator.Resolve(match);

        Assert.Equal(match.Host.PlayerId, match.WinnerPlayerId);
        Assert.Equal(0, match.Units.Find(u => u.UnitId == "later").Column);
        Assert.DoesNotContain(ticks, tick => tick.Ring > 0);
    }

    static MatchState EmptyMatch()
    {
        return new MatchState
        {
            MatchId = Guid.NewGuid().ToString("D"),
            Phase = MatchPhases.Plan,
            Host = new SeatState { PlayerId = "host" },
            Guest = new SeatState { PlayerId = "guest" }
        };
    }

    static MatchState FlagMatch()
    {
        MatchState match = EmptyMatch();
        FlagRules.Place(match);
        FlagRules.AssignGuest(match);
        return match;
    }

    static string OwnerAt(MatchState match, int column, int row)
    {
        return FlagAt(match, column, row).OwnerPlayerId;
    }

    static FlagState FlagAt(MatchState match, int column, int row)
    {
        for (int i = 0; i < match.Flags.Count; i++)
        {
            FlagState flag = match.Flags[i];
            if (flag.Column == column && flag.Row == row)
                return flag;
        }

        throw new InvalidOperationException($"No flag at {column},{row}.");
    }

    static UnitState Unit(
        string id,
        string owner,
        int column,
        int row,
        string planType,
        int toColumn,
        int toRow,
        int health = 10,
        int damage = 3)
    {
        var unit = new UnitState
        {
            UnitId = id,
            OwnerPlayerId = owner,
            CharacterId = "Tank",
            Column = column,
            Row = row,
            Health = health,
            MaxHealth = 10,
            Damage = damage
        };
        if (planType != null)
        {
            unit.Plan = new PlannedActionState
            {
                Type = planType,
                Targets = new List<CellDto> { new() { Column = toColumn, Row = toRow } }
            };
        }

        return unit;
    }

    static BattleMoveDto FindMove(List<BattleTickDto> ticks, string unitId)
    {
        for (int t = 0; t < ticks.Count; t++)
        {
            for (int m = 0; m < ticks[t].Moves.Count; m++)
            {
                if (ticks[t].Moves[m].UnitId == unitId)
                    return ticks[t].Moves[m];
            }
        }

        return null;
    }
}
