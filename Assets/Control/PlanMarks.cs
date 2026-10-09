using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Plan-phase board presentation: attack overlays, utility shine, move tiles, and order arrows.
/// Does not change plans or occupancy.
/// </summary>
public class PlanMarks
{
    static readonly Color MoveArrowColor = new Color(0.25f, 0.55f, 1f, 1f);

    readonly MatchRuntime _runtime;
    readonly PlaceholderBoard _board;

    public PlanMarks(MatchRuntime runtime, PlaceholderBoard board)
    {
        _runtime = runtime;
        _board = board;
    }

    public void Refresh()
    {
        _board.ClearMarks();
        if (_runtime.Phase != MatchPhase.Plan)
            return;

        MarkPlannedAttacks();
        MarkPlannedUtilities();
        RefreshPlanArrows();

        if (_runtime.PendingSpawn != null)
        {
            MarkSpawnRow();
            return;
        }

        Unit selected = _runtime.Selected;
        if (selected == null || !selected.IsAlive)
            return;

        if (_runtime.ChosenAction == UnitPlanKind.Attack)
            MarkAttackCoverage(selected);
        else if (_runtime.ChosenAction == UnitPlanKind.Utility)
            MarkUtilityCoverage(selected);
        else if (_runtime.ChosenAction == UnitPlanKind.Move)
            MarkMoveTiles(selected);

        BoardCell stand = _board.GetCell(selected.behaviour.Position);
        if (stand != null)
            stand.SetMark(CellMark.Selected);
    }

    void MarkPlannedAttacks()
    {
        for (int i = 0; i < _runtime.Units.Count; i++)
        {
            Unit unit = _runtime.Units[i];
            PlannedAction plan = unit.behaviour.Plan;
            if (plan == null || plan.Type != PlannedActionType.Attack || plan.TargetPositions == null)
                continue;
            if (_runtime.IsPickingAction(unit))
                continue;
            int damage = unit.behaviour != null
                ? unit.behaviour.CurrentStats.damage
                : 0;
            foreach (Position target in plan.TargetPositions)
            {
                BoardCell cell = _board.GetCell(target);
                if (cell != null)
                    cell.AddAttackDamage(damage);
            }
        }
    }

    void RefreshPlanArrows()
    {
        for (int i = 0; i < _runtime.Units.Count; i++)
        {
            Unit unit = _runtime.Units[i];
            if (_runtime.IsPickingAction(unit))
                unit.HideArrow();
            else
                ShowPlanArrow(unit);
        }
    }

    void MarkAttackCoverage(Unit unit)
    {
        var cells = unit.behaviour.GetReachablePositions(PlannedActionType.Attack);
        int damage = unit.behaviour.CurrentStats.damage;
        for (int i = 0; i < cells.Count; i++)
        {
            BoardCell cell = _board.GetCell(cells[i]);
            if (cell == null)
                continue;
            cell.AddAttackDamage(damage);
            if (!unit.behaviour.TryGetSoleAbilityIdentifier(PlannedActionType.Attack, cells[i], out _))
                cell.SetBlockedAttack(true);
        }
    }

    void MarkPlannedUtilities()
    {
        for (int i = 0; i < _runtime.Units.Count; i++)
        {
            Unit unit = _runtime.Units[i];
            PlannedAction plan = unit.behaviour.Plan;
            if (plan == null || plan.Type != PlannedActionType.Utility || plan.TargetPositions == null)
                continue;
            if (_runtime.IsPickingAction(unit))
                continue;
            foreach (Position target in plan.TargetPositions)
            {
                BoardCell cell = _board.GetCell(target);
                if (cell != null)
                    cell.SetUtilityCovered(true);
            }
        }
    }

    void MarkUtilityCoverage(Unit unit)
    {
        var cells = unit.behaviour.GetReachablePositions(PlannedActionType.Utility);
        for (int i = 0; i < cells.Count; i++)
        {
            BoardCell cell = _board.GetCell(cells[i]);
            if (cell == null)
                continue;
            cell.SetUtilityCovered(true);
            if (!unit.behaviour.TryGetSoleAbilityIdentifier(PlannedActionType.Utility, cells[i], out _))
                cell.SetBlockedAttack(true);
        }
    }

    void MarkSpawnRow()
    {
        for (int column = 0; column < _board.Columns; column++)
        {
            BoardCell cell = _board.GetCell(column, _runtime.HomeRow);
            if (cell != null && cell.Occupant == null && !cell.HasFlag)
                cell.SetMark(CellMark.Spawn);
        }
    }

    void MarkMoveTiles(Unit unit)
    {
        var cells = unit.behaviour.GetReachablePositions(PlannedActionType.Move);
        for (int i = 0; i < cells.Count; i++)
        {
            BoardCell cell = _board.GetCell(cells[i]);
            if (cell == null)
                continue;
            cell.SetMark(CellMark.Move);
        }
    }

    void ShowPlanArrow(Unit unit)
    {
        PlannedAction plan = unit.behaviour.Plan;
        if (plan == null || plan.Type != PlannedActionType.Move
            || plan.TargetPositions == null || plan.TargetPositions.Count != 1)
        {
            unit.HideArrow();
            return;
        }

        Position destination = plan.GetSoleTarget();
        BoardCell from = _board.GetCell(unit.behaviour.Position);
        BoardCell to = _board.GetCell(destination);
        if (from == null || to == null)
        {
            unit.HideArrow();
            return;
        }

        unit.ShowArrow(from.transform.position, to.transform.position, MoveArrowColor);
    }

    /// <summary>
    /// Battle-phase marks for one occupied ring. Highlights the ring; attack signs come later.
    /// </summary>
    public void ShowSpawns(IReadOnlyList<Unit> units)
    {
        _board.ClearMarks();
        if (units == null)
            return;
        for (int i = 0; i < units.Count; i++)
        {
            Unit unit = units[i];
            if (unit == null || unit.behaviour == null)
                continue;
            BoardCell cell = _board.GetCell(unit.behaviour.Position);
            if (cell != null)
                cell.SetMark(CellMark.Spawn);
        }
    }

    public void ShowBattleTick(BattleTick tick)
    {
        _board.ClearMarks();
        if (tick == null)
            return;

        MarkRingCells(tick);
        for (int i = 0; i < tick.AllUnits.Count; i++)
        {
            Unit unit = tick.AllUnits[i];
            if (unit != null)
                unit.HideArrow();
        }
    }

    /// <summary>
    /// Overlays for one kind after this ring's moves. Utilities show before attacks.
    /// Does not clear the ring.
    /// </summary>
    public void ShowBattleAbilities(BattleTick tick, UnitPlanKind kind)
    {
        if (tick == null)
            return;

        if (kind == UnitPlanKind.Attack)
            ClearUtilityMarks(tick);

        for (int i = 0; i < tick.Abilities.Count; i++)
        {
            BattleAbility ability = tick.Abilities[i];
            if (ability.Kind != kind)
                continue;
            if (kind == UnitPlanKind.Attack)
                MarkAbilityTiles(ability.TargetPositions, ability.Damage, utility: false);
            else if (kind == UnitPlanKind.Utility)
                MarkAbilityTiles(ability.TargetPositions, 0, utility: true);
        }
    }

    /// <summary>Highlights a ring with no acting units so the player can see that step pass.</summary>
    public void ShowBattleRing(BattleTick tick)
    {
        _board.ClearMarks();
        if (tick == null)
            return;

        MarkRingCells(tick);
    }

    void MarkRingCells(BattleTick tick)
    {
        for (int i = 0; i < tick.RingCells.Count; i++)
        {
            BoardCell cell = _board.GetCell(tick.RingCells[i]);
            if (cell != null)
                cell.SetMark(CellMark.Move);
        }
    }

    void ClearUtilityMarks(BattleTick tick)
    {
        for (int i = 0; i < tick.Abilities.Count; i++)
        {
            BattleAbility ability = tick.Abilities[i];
            if (ability.Kind != UnitPlanKind.Utility || ability.TargetPositions == null)
                continue;
            for (int t = 0; t < ability.TargetPositions.Count; t++)
            {
                BoardCell cell = _board.GetCell(ability.TargetPositions[t]);
                if (cell != null)
                    cell.SetUtilityCovered(false);
            }
        }
    }

    void MarkAbilityTiles(List<Position> targets, int damage, bool utility)
    {
        if (targets == null)
            return;
        for (int i = 0; i < targets.Count; i++)
        {
            BoardCell cell = _board.GetCell(targets[i]);
            if (cell == null)
                continue;
            if (utility)
                cell.SetUtilityCovered(true);
            else
                cell.AddAttackDamage(damage);
        }
    }
}
