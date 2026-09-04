using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Plan-phase rules: select a player unit and store a move, attack, or utility order.
/// Does not move pieces or draw marks.
/// </summary>
public class OrderPlanner
{
    readonly MatchRuntime _runtime;
    readonly PlaceholderBoard _board;

    public OrderPlanner(MatchRuntime runtime, PlaceholderBoard board)
    {
        _runtime = runtime;
        _board = board;
    }

    public void HandleClick(Vector3 world)
    {
        _board.TryGetCellAtWorld(world, out BoardCell clickedCell);
        Unit clickedUnit = clickedCell != null ? clickedCell.Occupant : null;

        if (_runtime.Selected != null && clickedCell != null)
        {
            if (_runtime.ChosenAction == UnitPlanKind.Move
                && _runtime.Selected.behaviour.CanReach(PlannedActionType.Move, clickedCell.Position))
            {
                AssignMove(clickedCell);
                return;
            }

            if (_runtime.ChosenAction == UnitPlanKind.Attack
                && _runtime.Selected.behaviour.TryGetSoleAbilityIdentifier(
                    PlannedActionType.Attack, clickedCell.Position, out byte attackId))
            {
                AssignAttack(_runtime.Selected.behaviour.GetPositionsWithAbilityIdentifier(
                    PlannedActionType.Attack, attackId));
                return;
            }

            if (_runtime.ChosenAction == UnitPlanKind.Utility
                && _runtime.Selected.behaviour.TryGetSoleAbilityIdentifier(
                    PlannedActionType.Utility, clickedCell.Position, out byte utilityId))
            {
                AssignUtility(_runtime.Selected.behaviour.GetPositionsWithAbilityIdentifier(
                    PlannedActionType.Utility, utilityId));
                return;
            }
        }

        if (_runtime.CanIssueOrders(clickedUnit))
        {
            Select(clickedUnit);
            return;
        }

        if (_runtime.Selected == null || clickedCell == null)
            return;

        if (_runtime.ChosenAction == UnitPlanKind.None && clickedCell.Occupant == null)
            Select(null);
    }

    public void Select(Unit unit)
    {
        _runtime.Selected = unit;
        _runtime.ChosenAction = UnitPlanKind.None;
    }

    public void ChooseAction(UnitPlanKind action)
    {
        if (!_runtime.CanIssueOrders(_runtime.Selected) || !_runtime.CanAfford(_runtime.Selected, action))
            return;
        if (action == UnitPlanKind.Attack
            && _runtime.Selected.behaviour.TryGetSingleAbilityIdentifier(PlannedActionType.Attack, out byte attackId))
        {
            AssignAttack(_runtime.Selected.behaviour.GetPositionsWithAbilityIdentifier(
                PlannedActionType.Attack, attackId));
            return;
        }

        if (action == UnitPlanKind.Utility
            && _runtime.Selected.behaviour.TryGetSingleAbilityIdentifier(PlannedActionType.Utility, out byte utilityId))
        {
            AssignUtility(_runtime.Selected.behaviour.GetPositionsWithAbilityIdentifier(
                PlannedActionType.Utility, utilityId));
            return;
        }

        _runtime.ChosenAction = action;
    }

    public void ClearSelectedPlan()
    {
        if (_runtime.Selected == null)
            return;
        _runtime.Selected.behaviour.ClearPlan();
        _runtime.ChosenAction = UnitPlanKind.None;
    }

    void AssignMove(BoardCell cell)
    {
        if (!_runtime.CanAfford(_runtime.Selected, UnitPlanKind.Move))
            return;
        _runtime.Selected.behaviour.SetMovePlan(cell.Position);
        _runtime.ChosenAction = UnitPlanKind.None;
    }

    void AssignAttack(List<Position> cells)
    {
        if (!_runtime.CanAfford(_runtime.Selected, UnitPlanKind.Attack))
            return;
        _runtime.Selected.behaviour.SetAttackPlan(cells);
        _runtime.ChosenAction = UnitPlanKind.None;
    }

    void AssignUtility(List<Position> cells)
    {
        if (!_runtime.CanAfford(_runtime.Selected, UnitPlanKind.Utility))
            return;
        _runtime.Selected.behaviour.SetUtilityPlan(cells);
        _runtime.ChosenAction = UnitPlanKind.None;
    }
}
