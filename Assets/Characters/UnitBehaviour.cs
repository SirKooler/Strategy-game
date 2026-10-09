using System;
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
    /// <summary>
    /// Sheet text for one character. Unspawn and Clear have no text of their own.
    /// An empty move uses the action menu's default. An empty attack or utility uses <see cref="description"/>.
    /// <see cref="ApplyStats"/> replaces &lt;health&gt;, &lt;maxHealth&gt;, and &lt;damage&gt;.
    /// </summary>
    [Serializable]
    public class Descriptions
    {
        /// <summary>Fills stat tags in <paramref name="text"/> from <paramref name="stats"/>.</summary>
        public static string ApplyStats(string text, UnitStats stats)
        {
            if (string.IsNullOrEmpty(text) || stats == null)
                return text;
            return text
                .Replace("<maxHealth>", stats.maxHealth.ToString())
                .Replace("<health>", stats.health.ToString())
                .Replace("<damage>", stats.damage.ToString());
        }

        [TextArea]
        public string description;

        [TextArea]
        public string move;

        [TextArea]
        public string attack;

        [TextArea]
        public string utility;
    }

    UnitStats _stats;
    UnitStats _sourceDefinition;
    Position _position;
    PlannedAction _plan;
    bool _isOpeningSpawn;
    bool _hasEnteredBattle;
    bool _hasResolved;

    public Descriptions Text { get; private set; }
    public UnitStats CurrentStats => _stats;
    public UnitStats SourceDefinition => _sourceDefinition;
    public Position Position => _position;
    public PlannedAction Plan => _plan;
    public UnitPlanKind PlanKind =>
        _plan == null ? UnitPlanKind.None : PlannedAction.ToPlanKind(_plan.Type);
    public bool IsOpeningSpawn => _isOpeningSpawn;
    public bool HasEnteredBattle => _hasEnteredBattle;
    public bool HasResolved => _hasResolved;
    public string ServerUnitId;

    /// <summary>Player id that owns this unit. Flags copy it when this unit captures them.</summary>
    public string OwnerId;

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
    /// Keeps <paramref name="definition"/> as the deck source.
    /// A playable type gets its own stats from <see cref="UnitConstants"/>, not from the asset numbers.
    /// Call once after the component is added. Art is applied by <see cref="Unit"/>.
    /// </summary>
    public void Setup(UnitStats definition, Position position)
    {
        _sourceDefinition = definition;
        if (UnitConstants.TryCreate(definition, out UnitStats typed))
            _stats = typed;
        else
        {
            _stats = Instantiate(definition);
            _stats.health = _stats.maxHealth;
        }

        CopyDescriptions(definition);
        _position = position.Copy();
        _isOpeningSpawn = false;
        _hasEnteredBattle = false;
        _hasResolved = false;
        ClearPlan();
        name = _stats.displayName;
    }

    /// <summary>
    /// Playable numbers come from <see cref="UnitConstants"/>. Description text stays on the deck asset.
    /// </summary>
    void CopyDescriptions(UnitStats from)
    {
        Descriptions source = from != null ? from.descriptions : null;
        Text = new Descriptions
        {
            description = source != null ? source.description : null,
            move = source != null ? source.move : null,
            attack = source != null ? source.attack : null,
            utility = source != null ? source.utility : null
        };
    }

    /// <summary>Health and damage from the match server. Patterns stay on the character type.</summary>
    public void ApplyServerStats(int health, int maxHealth, int damage)
    {
        if (_stats == null)
            return;
        _stats.health = health;
        _stats.maxHealth = maxHealth;
        _stats.damage = damage;
        NotifyHealthChanged();
    }

    public void MarkOpeningSpawn()
    {
        _isOpeningSpawn = true;
    }

    public void SetEnteredBattle(bool entered)
    {
        _hasEnteredBattle = entered;
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

    public void SetAttackPlan(IReadOnlyList<Position> cells, byte abilityId = 0)
    {
        SetAbilityPlan(PlannedActionType.Attack, cells, abilityId);
    }

    public void SetUtilityPlan(IReadOnlyList<Position> cells, byte abilityId = 0)
    {
        SetAbilityPlan(PlannedActionType.Utility, cells, abilityId);
    }

    void SetAbilityPlan(PlannedActionType type, IReadOnlyList<Position> cells, byte abilityId)
    {
        _plan = new PlannedAction
        {
            Type = type,
            TargetPositions = new HashSet<Position>(),
            AbilityIdentifier = abilityId
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

    /// <summary>Puts back a plan captured before the current action view.</summary>
    public void RestorePlan(PlannedAction plan)
    {
        _plan = plan;
    }

    public void ApplyDamage(int amount)
    {
        _stats.health -= amount;
        if (_stats.health < 0)
            _stats.health = 0;
        NotifyHealthChanged();
    }

    /// <summary>Removes health, but never below <paramref name="minimum"/>.</summary>
    public void ApplyDamageDownTo(int amount, int minimum)
    {
        if (_stats == null || amount <= 0)
            return;
        _stats.health -= amount;
        if (_stats.health < minimum)
            _stats.health = minimum;
        NotifyHealthChanged();
    }

    public void ApplyHeal(int amount)
    {
        if (_stats == null || amount <= 0)
            return;
        _stats.health += amount;
        if (_stats.health > _stats.maxHealth)
            _stats.health = _stats.maxHealth;
        NotifyHealthChanged();
    }

    void NotifyHealthChanged()
    {
        Unit unit = GetComponent<Unit>();
        if (unit != null)
            unit.RefreshHealth();
    }
}
