using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Resolves stored orders from the board center outward.
/// Each iteration moves its ring, then those units attack or use utilities, then the next ring starts.
/// A move is cancelled if the destination is occupied, or another unit in this iteration starts there.
/// </summary>
public class BattleResolver
{
    struct BattleIteration
    {
        public HashSet<Unit> MovingUnits;
        public HashSet<Unit> AbilityUnits;
        public HashSet<Unit> AllUnits;
    }

    readonly MatchRuntime _runtime;
    readonly PlaceholderBoard _board;

    public BattleResolver(MatchRuntime runtime, PlaceholderBoard board)
    {
        _runtime = runtime;
        _board = board;
    }

    /// <summary>
    /// Units standing on the ring <paramref name="iteration"/> from the board center.
    /// </summary>
    BattleIteration GetBattleIteration(int iteration)
    {
        var movingUnits = new HashSet<Unit>();
        var abilityUnits = new HashSet<Unit>();
        var allUnits = new HashSet<Unit>();
        int lowFrame = (_board.Size - 1) / 2 - iteration;
        int highFrame = _board.Size / 2 + iteration;
        for (int i = 0; i <= highFrame - lowFrame; i++)
        {
            TryCollectUnitAt(lowFrame, lowFrame + i, movingUnits, abilityUnits, allUnits);
            TryCollectUnitAt(lowFrame + i, lowFrame, movingUnits, abilityUnits, allUnits);
            TryCollectUnitAt(highFrame - i, highFrame, movingUnits, abilityUnits, allUnits);
            TryCollectUnitAt(highFrame, highFrame - i, movingUnits, abilityUnits, allUnits);
        }

        return new BattleIteration
        {
            MovingUnits = movingUnits,
            AbilityUnits = abilityUnits,
            AllUnits = allUnits
        };
    }

    void TryCollectUnitAt(
        int column,
        int row,
        HashSet<Unit> movingUnits,
        HashSet<Unit> abilityUnits,
        HashSet<Unit> allUnits)
    {
        BoardCell cell = _board.GetCell(column, row);
        if (cell == null)
            return;
        Unit unit = cell.Occupant;
        if (unit == null || !unit.IsAlive || unit.behaviour.HasResolved)
            return;
        allUnits.Add(unit);
        if (unit.behaviour.PlanKind == UnitPlanKind.Move)
            movingUnits.Add(unit);
        else if (unit.behaviour.PlanKind == UnitPlanKind.Attack
            || unit.behaviour.PlanKind == UnitPlanKind.Utility)
            abilityUnits.Add(unit);
    }

    public void Execute()
    {
        foreach (Unit unit in _runtime.Units)
            unit.behaviour.ClearResolved();

        int maxIteration = (_board.Size - 1) / 2;
        for (int iteration = 0; iteration <= maxIteration; iteration++)
        {
            BattleIteration step = GetBattleIteration(iteration);
            ExecuteMoves(step.MovingUnits);
            ExecuteAbilities(step.AbilityUnits);
            foreach (Unit unit in step.AllUnits)
            {
                unit.behaviour.MarkResolved();
                unit.HideArrow();
                unit.behaviour.ClearPlan();
            }
        }
    }

    void ExecuteMoves(HashSet<Unit> movingUnits)
    {
        var destTiles = new Dictionary<Position, HashSet<Unit>>();
        foreach (Unit unit in movingUnits)
        {
            Position to = unit.behaviour.Plan.GetSoleTarget();
            Position from = unit.behaviour.Position.Copy();
            foreach (Position p in new[] { from, to }){
                if (!destTiles.ContainsKey(p))
                    destTiles[p] = new HashSet<Unit>();
                destTiles[p].Add(unit);
            }
        }

        foreach (Unit unit in movingUnits)
        {
            if (destTiles[unit.behaviour.Plan.GetSoleTarget()].Count > 1)
                continue;
            unit.behaviour.MoveTo(_board);
        }
    }

    void ExecuteAbilities(HashSet<Unit> abilityUnits)
    {
        var dead = new List<Unit>();
        for (int i = 0; i < _runtime.Units.Count; i++)
        {
            Unit unit = _runtime.Units[i];
            if (!abilityUnits.Contains(unit) || !unit.IsAlive)
                continue;

            if (unit.behaviour.PlanKind == UnitPlanKind.Attack)
            {
                List<Unit> defenders = FindAttackOccupants(unit);
                for (int d = 0; d < defenders.Count; d++)
                {
                    Unit defender = defenders[d];
                    defender.behaviour.ApplyDamage(unit.behaviour.CurrentStats.damage);
                    if (!defender.IsAlive && !dead.Contains(defender))
                        dead.Add(defender);
                }
                continue;
            }

            if (unit.behaviour.PlanKind != UnitPlanKind.Utility)
                continue;

            UtilityEffect effect = unit.behaviour.CurrentStats.utility != null
                ? unit.behaviour.CurrentStats.utility.Effect
                : null;
            if (effect == null)
                continue;

            List<Unit> allies = FindUtilityOccupants(unit);
            for (int a = 0; a < allies.Count; a++)
                effect(allies[a]);
        }

        for (int i = 0; i < dead.Count; i++)
            RemoveUnit(dead[i]);
    }

    /// <summary>
    /// Enemies standing on any planned attack cell after this iteration's moves resolve.
    /// Empty tiles and friendly units deal no damage.
    /// </summary>
    List<Unit> FindAttackOccupants(Unit attacker)
    {
        return FindOccupants(attacker, playerOwned: false);
    }

    /// <summary>
    /// Player units standing on any planned utility cell after this iteration's moves resolve.
    /// Empty tiles and enemies are ignored.
    /// </summary>
    List<Unit> FindUtilityOccupants(Unit caster)
    {
        return FindOccupants(caster, playerOwned: true);
    }

    List<Unit> FindOccupants(Unit source, bool playerOwned)
    {
        var found = new List<Unit>();
        PlannedAction plan = source.behaviour.Plan;
        if (plan == null || plan.TargetPositions == null)
            return found;

        foreach (Position target in plan.TargetPositions)
        {
            BoardCell cell = _board.GetCell(target);
            if (cell == null || cell.Occupant == null || !cell.Occupant.IsAlive)
                continue;
            if (_runtime.PlayerUnits.Contains(cell.Occupant) != playerOwned)
                continue;
            if (!found.Contains(cell.Occupant))
                found.Add(cell.Occupant);
        }

        return found;
    }

    void RemoveUnit(Unit unit)
    {
        BoardCell cell = _board.GetCell(unit.behaviour.Position);
        if (cell != null && cell.Occupant == unit)
            cell.Occupant = null;

        unit.behaviour.ClearPlan();
        _runtime.Units.Remove(unit);
        _runtime.PlayerUnits.Remove(unit);
        if (_runtime.Selected == unit)
            _runtime.Selected = null;
        Object.Destroy(unit.gameObject);
    }
}
