using System.Collections.Generic;

/// <summary>
/// Local match phases. Plan assigns orders; Battle resolves them.
/// A winner ends the match after battle.
/// </summary>
public enum MatchPhase
{
    Plan,
    Battle
}

public enum MatchWinner
{
    None,
    Local,
    Opponent
}

/// <summary>
/// Runtime-mutable match state for one local match.
/// Authoring (deck, dummy, start cells) stays on <see cref="MatchController"/>.
/// Spawning is applied by <see cref="MatchSpawner"/>.
/// </summary>
public class MatchRuntime
{
    public readonly List<Unit> Units = new List<Unit>();
    public readonly HashSet<Unit> PlayerUnits = new HashSet<Unit>();
    public readonly HashSet<UnitStats> SpawnedDefinitions = new HashSet<UnitStats>();
    public readonly HashSet<UnitStats> DeadDefinitions = new HashSet<UnitStats>();

    public Unit Selected;
    public UnitStats PendingSpawn;
    public UnitPlanKind ChosenAction = UnitPlanKind.None;
    public MatchPhase Phase = MatchPhase.Plan;
    public int Turn = 1;
    public MatchWinner Winner = MatchWinner.None;

    /// <summary>Player id for this seat. Cloud matches use the server id.</summary>
    public string LocalPlayerId = Flag.LocalSeat;

    /// <summary>The other seat. Cloud matches use the server id.</summary>
    public string OpponentPlayerId = Flag.OpponentSeat;

    /// <summary>Row this seat may spawn on. Guest uses the opposite edge in a cloud match.</summary>
    public int HomeRow;

    /// <summary>True while battle rings are still playing. Next Turn waits until this is false.</summary>
    public bool IsResolving;

    /// <summary>True after this seat submitted plans and the opponent has not.</summary>
    public bool IsWaitingForOpponent;

    /// <summary>Paid extra spawns still on the board. The opening unit is free and is not counted.</summary>
    public int PaidPlayerSpawnCount;

    /// <summary>Energy spent on spawns during the current plan phase. Resets each turn.</summary>
    public int SpawnEnergySpent;

    public int EnergyPool => Energy.PoolForTurn(Turn);

    public int NextSpawnCost =>
        PlayerUnits.Count == 0 ? 0 : Energy.SpawnCost(PaidPlayerSpawnCount);

    public bool SpawnCostIsInfinite =>
        PlayerUnits.Count > 0 && Energy.SpawnCostIsInfinite(PaidPlayerSpawnCount);

    public int EnergyRemaining => EnergyPool - EnergySpentOnPlans() - SpawnEnergySpent;

    public bool CanAffordSpawn =>
        Phase == MatchPhase.Plan && (NextSpawnCost == 0 || (!SpawnCostIsInfinite && EnergyRemaining >= NextSpawnCost));

    public bool CanIssueOrders(Unit unit)
    {
        return unit != null && unit.IsAlive && PlayerUnits.Contains(unit);
    }

    public bool CanUnspawn(Unit unit)
    {
        return Phase == MatchPhase.Plan
            && unit != null
            && unit.IsAlive
            && PlayerUnits.Contains(unit)
            && unit.behaviour != null
            && !unit.behaviour.IsOpeningSpawn
            && !unit.behaviour.HasEnteredBattle;
    }

    public bool CanAfford(Unit unit, UnitPlanKind action)
    {
        if (unit == null)
            return false;
        if (action == UnitPlanKind.Utility
            && (unit.behaviour == null || !unit.behaviour.HasUtility))
            return false;
        if (action != UnitPlanKind.Move && action != UnitPlanKind.Attack && action != UnitPlanKind.Utility)
            return false;
        return EnergyRemaining + EnergyCostOf(unit) >= Energy.Cost(action);
    }

    public int EnergySpentOnPlans()
    {
        int spent = 0;
        foreach (Unit unit in PlayerUnits)
        {
            if (unit == null || unit.behaviour == null || unit.behaviour.Plan == null)
                continue;
            spent += Energy.Cost(unit.behaviour.Plan.Type);
        }

        return spent;
    }

    public int EnergyCostOf(Unit unit)
    {
        if (unit == null || unit.behaviour == null || unit.behaviour.Plan == null)
            return 0;
        return Energy.Cost(unit.behaviour.Plan.Type);
    }

    public bool IsPickingAction(Unit unit)
    {
        return unit != null && unit == Selected && ChosenAction != UnitPlanKind.None;
    }

    public bool IsDefinitionOnBoard(UnitStats definition)
    {
        if (definition == null)
            return false;
        foreach (Unit unit in PlayerUnits)
        {
            if (unit == null || !unit.IsAlive || unit.behaviour == null)
                continue;
            if (unit.behaviour.SourceDefinition == definition)
                return true;
        }

        return false;
    }

    public bool IsDefinitionAvailable(UnitStats definition)
    {
        return definition != null
            && !SpawnedDefinitions.Contains(definition)
            && !DeadDefinitions.Contains(definition);
    }

    public void ClearPendingSpawn()
    {
        PendingSpawn = null;
    }

    /// <summary>
    /// After a ring's moves, each corner with a living unit is owned by that unit's player
    /// Yours uses the friendly health-bar blue. Theirs uses the enemy red. Empty corners keep their owner. One owner of every flag wins.
    /// </summary>
    public void CaptureFlags(PlaceholderBoard board)
    {
        if (Winner != MatchWinner.None || board == null)
            return;

        var flags = new List<Flag>();
        board.CollectFlags(flags);
        if (flags.Count == 0)
            return;

        for (int i = 0; i < flags.Count; i++)
        {
            Flag flag = flags[i];
            BoardCell cell = board.GetCell(flag.Column, flag.Row);
            Unit occupant = cell != null ? cell.Occupant : null;
            if (occupant == null || !occupant.IsAlive)
                continue;
            string captor = OwnerIdOf(occupant);
            flag.SetOwner(captor, Flag.ForPlayer(captor, LocalPlayerId));
        }

        string owner = flags[0].OwnerId;
        if (string.IsNullOrEmpty(owner))
            return;
        for (int i = 1; i < flags.Count; i++)
        {
            if (flags[i].OwnerId != owner)
                return;
        }

        Winner = owner == LocalPlayerId ? MatchWinner.Local : MatchWinner.Opponent;
    }

    public string OwnerIdOf(Unit unit)
    {
        if (unit != null && unit.behaviour != null && !string.IsNullOrEmpty(unit.behaviour.OwnerId))
            return unit.behaviour.OwnerId;
        return PlayerUnits.Contains(unit) ? LocalPlayerId : OpponentPlayerId;
    }
}
