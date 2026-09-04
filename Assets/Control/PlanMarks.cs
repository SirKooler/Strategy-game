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
}
