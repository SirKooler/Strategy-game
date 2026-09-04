using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The order a unit will carry out when plan phase ends.
/// </summary>
public enum UnitPlanKind
{
    None,
    Move,
    Attack,
    Utility
}

/// <summary>
/// Runtime rules for one unit: stats, board cell, and the current turn order.
/// Art and the plan arrow live on <see cref="Unit"/>.
/// </summary>
public class UnitBehaviour : MonoBehaviour
{
    UnitStats _stats;
    UnitStats _sourceDefinition;
    Position _position;
    PlannedAction _plan;
    bool _isOpeningSpawn;
    bool _hasEnteredBattle;
    bool _hasResolved;

    public UnitStats CurrentStats => _stats;
    public UnitStats SourceDefinition => _sourceDefinition;
    public Position Position => _position;
    public PlannedAction Plan => _plan;
    public UnitPlanKind PlanKind =>
        _plan == null ? UnitPlanKind.None : PlannedAction.ToPlanKind(_plan.Type);
    public bool IsOpeningSpawn => _isOpeningSpawn;
    public bool HasEnteredBattle => _hasEnteredBattle;
    public bool HasResolved => _hasResolved;

    public bool IsAlive => _stats != null && _stats.health > 0;

    public bool HasUtility => _stats != null && _stats.HasUtility;

    /// <summary>
    /// Board cells this unit can target with <paramref name="action"/>.
    /// Move uses <see cref="MovementPattern"/>. Attack and Utility use their patterns.
    /// </summary>
    public List<Position> GetReachablePositions(PlannedActionType action)
    {
        var result = new List<Position>();
        if (_position == null || _stats == null)
            return result;

        AttackPattern ability = PatternFor(action);
        if (ability != null)
        {
            ability.CollectCoveredBoardCells(_position, result);
            return result;
        }

        if (action == PlannedActionType.Move && _stats.movePattern != null)
            _stats.movePattern.CollectBoardCells(_position, result);
        return result;
    }

    public bool CanReach(PlannedActionType action, Position cell)
    {
        List<Position> cells = GetReachablePositions(action);
        for (int i = 0; i < cells.Count; i++)
        {
            if (cells[i].SameCell(cell))
                return true;
        }

        return false;
    }

    public int AttackIdentifierCountAt(Position cell)
    {
        return IdentifierCountAt(PlannedActionType.Attack, cell);
    }

    public int IdentifierCountAt(PlannedActionType action, Position cell)
    {
        AttackPattern pattern = PatternFor(action);
        if (pattern == null || !pattern.TryMapBoardToLocal(_position, cell, out int row, out int col))
            return 0;
        return pattern.IdentifierCount(row, col);
    }

    /// <summary>True when the cell has exactly one identifier for <paramref name="action"/>.</summary>
    public bool TryGetSoleAbilityIdentifier(PlannedActionType action, Position cell, out byte identifier)
    {
        identifier = 0;
        AttackPattern pattern = PatternFor(action);
        if (pattern == null || !pattern.TryMapBoardToLocal(_position, cell, out int row, out int col))
            return false;
        return pattern.TryGetSoleIdentifier(row, col, out identifier);
    }

    /// <summary>True when this unit has only one identifier that can be chosen by clicking.</summary>
    public bool TryGetSingleAbilityIdentifier(PlannedActionType action, out byte identifier)
    {
        identifier = 0;
        AttackPattern pattern = PatternFor(action);
        return pattern != null && pattern.TryGetSingleIdentifier(out identifier);
    }

    public List<Position> GetPositionsWithAbilityIdentifier(PlannedActionType action, byte identifier)
    {
        var result = new List<Position>();
        AttackPattern pattern = PatternFor(action);
        if (_position == null || pattern == null)
            return result;
        pattern.CollectBoardCellsWithIdentifier(_position, identifier, result);
        return result;
    }

    AttackPattern PatternFor(PlannedActionType action)
    {
        if (_stats == null)
            return null;
        if (action == PlannedActionType.Attack)
            return _stats.attackPattern;
        if (action == PlannedActionType.Utility)
            return HasUtility ? _stats.utility.pattern : null;
        return null;
    }

    /// <summary>
    /// Copies <paramref name="definition"/> onto this behaviour, then applies
    /// <see cref="UnitConstants"/> when the unit is a known placeholder.
    /// Call once after the component is added. Art is applied by <see cref="Unit"/>.
    /// </summary>
    public void Setup(UnitStats definition, Position position)
    {
        _sourceDefinition = definition;
        _stats = Instantiate(definition);
        UnitConstants.ApplyIfKnown(_stats);
        _position = position.Copy();
        _isOpeningSpawn = false;
        _hasEnteredBattle = false;
        _hasResolved = false;
        ClearPlan();
        name = _stats.displayName;
    }

    public void MarkOpeningSpawn()
    {
        _isOpeningSpawn = true;
    }

    public void ClearResolved()
    {
        _hasResolved = false;
    }   

    public void MarkResolved()
    {
        _hasEnteredBattle = true;
        _hasResolved = true;
    }

    public void SetPosition(Position position)
    {
        _position.Set(position);
    }

    /// <summary>
    /// Vacates the current board cell, occupies the planned move destination, and updates world position.
    /// </summary>
    public void MoveTo(PlaceholderBoard board)
    {
        Unit occupant = GetComponent<Unit>();
        BoardCell to = board.GetCell(_plan.GetSoleTarget());
        if (to == null || (to.Occupant != null && to.Occupant != occupant))
            return;

        BoardCell from = board.GetCell(_position);
        if (from != null && from.Occupant == occupant)
            from.Occupant = null;

        to.Occupant = occupant;
        if (occupant != null)
            occupant.PlaceAt(to.Position, to.transform.position);
        else
            SetPosition(to.Position);
    }

    public void SetMovePlan(Position destination)
    {
        _plan = new PlannedAction
        {
            Type = PlannedActionType.Move,
            TargetPositions = new HashSet<Position>()
        };
        if (destination != null)
            _plan.TargetPositions.Add(destination.Copy());
    }

    public void SetAttackPlan(IReadOnlyList<Position> cells)
    {
        SetAbilityPlan(PlannedActionType.Attack, cells);
    }

    public void SetUtilityPlan(IReadOnlyList<Position> cells)
    {
        SetAbilityPlan(PlannedActionType.Utility, cells);
    }

    void SetAbilityPlan(PlannedActionType type, IReadOnlyList<Position> cells)
    {
        _plan = new PlannedAction
        {
            Type = type,
            TargetPositions = new HashSet<Position>()
        };
        if (cells == null)
            return;
        for (int i = 0; i < cells.Count; i++)
        {
            if (cells[i] != null)
                _plan.TargetPositions.Add(cells[i].Copy());
        }
    }

    public void ClearPlan()
    {
        _plan = null;
    }

    public void ApplyDamage(int amount)
    {
        _stats.health -= amount;
        if (_stats.health < 0)
            _stats.health = 0;
    }

    public void ApplyHeal(int amount)
    {
        if (_stats == null || amount <= 0)
            return;
        _stats.health += amount;
        if (_stats.health > _stats.maxHealth)
            _stats.health = _stats.maxHealth;
    }
}
