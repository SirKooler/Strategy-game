using System.Collections.Generic;

/// <summary>
/// Local match phases. Plan assigns orders; Battle resolves them.
/// There is no win check yet.
/// </summary>
public enum MatchPhase
{
    Plan,
    Battle
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

    public Unit Selected;
    public UnitPlanKind ChosenAction = UnitPlanKind.None;
    public MatchPhase Phase = MatchPhase.Plan;
    public int Turn = 1;

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
}
