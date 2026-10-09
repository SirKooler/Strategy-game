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

    public int MaxIteration => (_board.Size - 1) / 2;

    /// <summary>
    /// Characters placed this plan, before any ring. Opening units are already on the board.
    /// </summary>
    public void CollectNewSpawns(List<Unit> spawns)
    {
        spawns.Clear();
        foreach (Unit unit in _runtime.Units)
        {
            if (unit == null || !unit.IsAlive || unit.behaviour == null)
                continue;
            if (unit.behaviour.IsOpeningSpawn || unit.behaviour.HasEnteredBattle)
                continue;
            spawns.Add(unit);
        }
    }

    /// <summary>Clears per-unit resolved flags before the first ring plays.</summary>
    public void Begin()
    {
        foreach (Unit unit in _runtime.Units)
            unit.behaviour.ClearResolved();
    }

    /// <summary>
    /// Builds the snapshot for ring <paramref name="iteration"/> from current occupancy.
    /// A ring is empty when nobody on it moves, attacks, or uses a utility.
    /// </summary>
    public bool TryBuildTick(int iteration, out BattleTick tick)
    {
        tick = new BattleTick();
        if (iteration < 0 || iteration > MaxIteration)
            return false;

        CollectRingCells(iteration, tick.RingCells);
        BattleIteration step = GetBattleIteration(iteration);
        foreach (Unit unit in step.AllUnits)
            tick.AllUnits.Add(unit);

        tick.IsEmpty = step.MovingUnits.Count == 0 && step.AbilityUnits.Count == 0;
        if (tick.IsEmpty)
            return true;

        BuildMoves(step.MovingUnits, tick);
        BuildAbilities(step.AbilityUnits, tick);
        return true;
    }

    public void ApplyMoves(BattleTick tick)
    {
        if (tick == null)
            return;
        for (int i = 0; i < tick.Moves.Count; i++)
        {
            BattleMove move = tick.Moves[i];
            if (move.Blocked || move.Unit == null || !move.Unit.IsAlive)
                continue;
            move.Unit.behaviour.MoveTo(_board);
        }
    }

    public void ApplyAbilities(BattleTick tick, UnitPlanKind kind)
    {
        if (tick == null)
            return;

        var abilityUnits = new HashSet<Unit>();
        for (int i = 0; i < tick.Abilities.Count; i++)
        {
            BattleAbility ability = tick.Abilities[i];
            if (ability.Kind == kind && ability.Unit != null)
                abilityUnits.Add(ability.Unit);
        }

        ExecuteAbilities(abilityUnits, kind);
    }

    public void FinishTick(BattleTick tick)
    {
        if (tick == null)
            return;
        for (int i = 0; i < tick.AllUnits.Count; i++)
        {
            Unit unit = tick.AllUnits[i];
            if (unit == null || unit.behaviour == null)
                continue;
            unit.behaviour.MarkResolved();
            unit.HideArrow();
            unit.behaviour.ClearPlan();
        }
    }

    /// <summary>
    /// Units standing on the ring <paramref name="iteration"/> from the board center.
    /// </summary>
    BattleIteration GetBattleIteration(int iteration)
    {
        var movingUnits = new HashSet<Unit>();
        var abilityUnits = new HashSet<Unit>();
        var allUnits = new HashSet<Unit>();
        ForEachRingCell(iteration, (column, row) =>
            TryCollectUnitAt(column, row, movingUnits, abilityUnits, allUnits));

        return new BattleIteration
        {
            MovingUnits = movingUnits,
            AbilityUnits = abilityUnits,
            AllUnits = allUnits
        };
    }

    void CollectRingCells(int iteration, List<Position> cells)
    {
        var seen = new HashSet<Position>();
        ForEachRingCell(iteration, (column, row) =>
        {
            BoardCell cell = _board.GetCell(column, row);
            if (cell == null || !seen.Add(cell.Position))
                return;
            cells.Add(cell.Position);
        });
    }

    void ForEachRingCell(int iteration, System.Action<int, int> visit)
    {
        int lowFrame = (_board.Size - 1) / 2 - iteration;
        int highFrame = _board.Size / 2 + iteration;
        for (int i = 0; i <= highFrame - lowFrame; i++)
        {
            visit(lowFrame, lowFrame + i);
            visit(lowFrame + i, lowFrame);
            visit(highFrame - i, highFrame);
            visit(highFrame, highFrame - i);
        }
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

    void BuildMoves(HashSet<Unit> movingUnits, BattleTick tick)
    {
        var destTiles = new Dictionary<Position, HashSet<Unit>>();
        foreach (Unit unit in movingUnits)
        {
            Position to = unit.behaviour.Plan.GetSoleTarget();
            Position from = unit.behaviour.Position.Copy();
            AddDestUnit(destTiles, from, unit);
            AddDestUnit(destTiles, to, unit);
        }

        foreach (Unit unit in movingUnits)
        {
            Position to = unit.behaviour.Plan.GetSoleTarget();
            BoardCell fromCell = _board.GetCell(unit.behaviour.Position);
            BoardCell toCell = _board.GetCell(to);
            Vector3 fromWorld = fromCell != null ? fromCell.transform.position : unit.transform.position;
            Vector3 toWorld = toCell != null ? toCell.transform.position : fromWorld;
            bool contested = destTiles.TryGetValue(to, out HashSet<Unit> claimants)
                && claimants.Count > 1;
            bool occupied = toCell == null
                || (toCell.Occupant != null && toCell.Occupant != unit);

            tick.Moves.Add(new BattleMove
            {
                Unit = unit,
                Destination = to,
                FromWorld = fromWorld,
                ToWorld = toWorld,
                Blocked = contested || occupied
            });
        }
    }

    static void AddDestUnit(Dictionary<Position, HashSet<Unit>> destTiles, Position cell, Unit unit)
    {
        if (!destTiles.TryGetValue(cell, out HashSet<Unit> units))
        {
            units = new HashSet<Unit>();
            destTiles[cell] = units;
        }

        units.Add(unit);
    }

    void BuildAbilities(HashSet<Unit> abilityUnits, BattleTick tick)
    {
        AddAbilities(abilityUnits, tick, UnitPlanKind.Utility);
        AddAbilities(abilityUnits, tick, UnitPlanKind.Attack);
    }

    static void AddAbilities(HashSet<Unit> abilityUnits, BattleTick tick, UnitPlanKind kind)
    {
        foreach (Unit unit in abilityUnits)
        {
            if (unit.behaviour.PlanKind != kind)
                continue;
            PlannedAction plan = unit.behaviour.Plan;
            var ability = new BattleAbility
            {
                Unit = unit,
                Kind = kind,
                Damage = unit.behaviour.CurrentStats != null
                    ? unit.behaviour.CurrentStats.damage
                    : 0
            };
            if (plan != null && plan.TargetPositions != null)
            {
                foreach (Position target in plan.TargetPositions)
                    ability.TargetPositions.Add(target);
            }

            tick.Abilities.Add(ability);
        }
    }

    void ExecuteAbilities(HashSet<Unit> abilityUnits, UnitPlanKind kind)
    {
        if (kind == UnitPlanKind.Utility)
        {
            for (int i = 0; i < _runtime.Units.Count; i++)
            {
                Unit unit = _runtime.Units[i];
                if (!abilityUnits.Contains(unit) || !unit.IsAlive)
                    continue;
                if (unit.behaviour.PlanKind != UnitPlanKind.Utility)
                    continue;

                UtilityAbility utility = unit.behaviour.CurrentStats.utility;
                if (utility == null || !utility.HasEffect)
                    continue;

                List<Unit> allies = FindUtilityOccupants(unit);
                AbilitySubject caster = AbilitySubject.Read(unit);
                for (int a = 0; a < allies.Count; a++)
                {
                    AbilitySubject ally = AbilitySubject.Read(allies[a]);
                    utility.Apply(caster, ally);
                    ally.WriteTo(allies[a]);
                }

                caster.WriteTo(unit);
            }

            return;
        }

        var dead = new List<Unit>();
        for (int i = 0; i < _runtime.Units.Count; i++)
        {
            Unit unit = _runtime.Units[i];
            if (!abilityUnits.Contains(unit) || !unit.IsAlive || unit.behaviour.PlanKind != UnitPlanKind.Attack)
                continue;

            List<Unit> defenders = FindAttackOccupants(unit);
            AttackAbility attack = unit.behaviour.CurrentStats.attack;
            AbilitySubject activator = AbilitySubject.Read(unit);
            for (int d = 0; d < defenders.Count; d++)
            {
                Unit defender = defenders[d];
                if (!defender.IsAlive)
                    continue;
                AbilitySubject hit = AbilitySubject.Read(defender);
                if (attack != null)
                    attack.Apply(activator, hit);
                else
                {
                    hit.Health -= unit.behaviour.CurrentStats.damage;
                    if (hit.Health < 0)
                        hit.Health = 0;
                }

                hit.WriteTo(defender);
                if (!defender.IsAlive && !dead.Contains(defender))
                    dead.Add(defender);
            }

            activator.WriteTo(unit);
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
        if (_runtime.PlayerUnits.Contains(unit))
        {
            UnitStats source = unit.behaviour.SourceDefinition;
            if (source != null)
                _runtime.DeadDefinitions.Add(source);
        }

        _runtime.Units.Remove(unit);
        _runtime.PlayerUnits.Remove(unit);
        if (_runtime.Selected == unit)
            _runtime.Selected = null;
        Object.Destroy(unit.gameObject);
    }
}
