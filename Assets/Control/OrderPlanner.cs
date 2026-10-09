using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Plan-phase rules: select any unit to inspect, and store a move, attack, or utility
/// order for the player's units. Does not move pieces or draw marks.
/// </summary>
public class OrderPlanner
{
    readonly MatchRuntime _runtime;
    readonly PlaceholderBoard _board;
    readonly MatchSpawner _spawner;
    Unit _planOwner;
    PlannedAction _planBeforeView;
    bool _hasPlanBeforeView;

    /// <summary>True when this change stored or cleared a plan, not when it only opened targeting.</summary>
    public bool TookAction { get; private set; }

    public void BeginChange()
    {
        TookAction = false;
    }

    public OrderPlanner(MatchRuntime runtime, PlaceholderBoard board, MatchSpawner spawner)
    {
        _runtime = runtime;
        _board = board;
        _spawner = spawner;
    }

    public void HandleClick(Vector3 world)
    {
        _board.TryGetCellAtWorld(world, out BoardCell clickedCell);
        Unit clickedUnit = clickedCell != null ? clickedCell.Occupant : null;

        if (_runtime.PendingSpawn != null)
        {
            HandlePendingSpawnClick(clickedCell, clickedUnit);
            return;
        }

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
                    PlannedActionType.Attack, attackId), attackId);
                return;
            }

            if (_runtime.ChosenAction == UnitPlanKind.Utility
                && _runtime.Selected.behaviour.TryGetSoleAbilityIdentifier(
                    PlannedActionType.Utility, clickedCell.Position, out byte utilityId))
            {
                AssignUtility(_runtime.Selected.behaviour.GetPositionsWithAbilityIdentifier(
                    PlannedActionType.Utility, utilityId), utilityId);
                return;
            }
        }

        if (clickedUnit != null && clickedUnit.IsAlive)
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
        _runtime.ClearPendingSpawn();
    }

    public void ChoosePendingSpawn(UnitStats definition)
    {
        if (definition == null)
            return;
        if (_runtime.PendingSpawn == definition)
        {
            _runtime.ClearPendingSpawn();
            return;
        }

        if (_spawner == null || !_spawner.CanSpawnDefinition(definition))
            return;

        _runtime.Selected = null;
        _runtime.ChosenAction = UnitPlanKind.None;
        _runtime.PendingSpawn = definition;
    }

    /// <summary>Captures the selected unit's plan before an action view changes it.</summary>
    public void RememberPlan()
    {
        Unit unit = _runtime.Selected;
        _planOwner = unit;
        _hasPlanBeforeView = unit != null && unit.behaviour != null;
        _planBeforeView = _hasPlanBeforeView ? CopyPlan(unit.behaviour.Plan) : null;
    }

    /// <summary>
    /// Hides the board targeting pattern and puts the selected unit's plan back
    /// to the one captured by <see cref="RememberPlan"/>.
    /// </summary>
    public void RevertViewedAction()
    {
        _runtime.ChosenAction = UnitPlanKind.None;
        if (!_hasPlanBeforeView || _planOwner == null || _planOwner.behaviour == null)
        {
            _hasPlanBeforeView = false;
            return;
        }

        if (_runtime.Selected == _planOwner)
            _planOwner.behaviour.RestorePlan(CopyPlan(_planBeforeView));
        _hasPlanBeforeView = false;
    }

    public void ChooseAction(UnitPlanKind action)
    {
        _runtime.ClearPendingSpawn();
        if (!_runtime.CanIssueOrders(_runtime.Selected) || !_runtime.CanAfford(_runtime.Selected, action))
            return;

        _runtime.ChosenAction = action;
    }

    public void ClearSelectedPlan()
    {
        _runtime.ClearPendingSpawn();
        if (_runtime.Selected == null)
            return;
        _runtime.Selected.behaviour.ClearPlan();
        _runtime.ChosenAction = UnitPlanKind.None;
        TookAction = true;
    }

    void HandlePendingSpawnClick(BoardCell clickedCell, Unit clickedUnit)
    {
        if (clickedCell != null
            && clickedCell.Occupant == null
            && !clickedCell.HasFlag
            && clickedCell.Position != null
            && clickedCell.Position.Row == _runtime.HomeRow)
        {
            _spawner.TrySpawnFromDeck(_runtime.PendingSpawn, clickedCell.Position);
            _runtime.ClearPendingSpawn();
            return;
        }

        if (clickedUnit != null && clickedUnit.IsAlive)
        {
            Select(clickedUnit);
            return;
        }

        _runtime.ClearPendingSpawn();
    }

    void AssignMove(BoardCell cell)
    {
        if (!_runtime.CanAfford(_runtime.Selected, UnitPlanKind.Move))
            return;
        _runtime.Selected.behaviour.SetMovePlan(cell.Position);
        _runtime.ChosenAction = UnitPlanKind.None;
        TookAction = true;
    }

    void AssignAttack(List<Position> cells, byte abilityId)
    {
        if (!_runtime.CanAfford(_runtime.Selected, UnitPlanKind.Attack))
            return;
        _runtime.Selected.behaviour.SetAttackPlan(cells, abilityId);
        _runtime.ChosenAction = UnitPlanKind.None;
        TookAction = true;
    }

    void AssignUtility(List<Position> cells, byte abilityId)
    {
        if (!_runtime.CanAfford(_runtime.Selected, UnitPlanKind.Utility))
            return;
        _runtime.Selected.behaviour.SetUtilityPlan(cells, abilityId);
        _runtime.ChosenAction = UnitPlanKind.None;
        TookAction = true;
    }

    static PlannedAction CopyPlan(PlannedAction plan)
    {
        if (plan == null)
            return null;

        var copy = new PlannedAction
        {
            Type = plan.Type,
            Character = plan.Character,
            AbilityIdentifier = plan.AbilityIdentifier,
            TargetPositions = new HashSet<Position>()
        };
        if (plan.TargetPositions == null)
            return copy;

        foreach (Position position in plan.TargetPositions)
        {
            if (position != null)
                copy.TargetPositions.Add(position.Copy());
        }

        return copy;
    }
}
