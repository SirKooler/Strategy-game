using UnityEngine;

/// <summary>
/// Places units on the board from a player deck or a given definition.
/// MatchController decides when to spawn; this class owns how.
/// </summary>
public class MatchSpawner
{
    readonly MatchRuntime _runtime;
    readonly PlaceholderBoard _board;
    readonly Transform _unitParent;
    readonly Player _player;
    HealthBar _healthBarPrefab;

    public MatchSpawner(MatchRuntime runtime, PlaceholderBoard board, Transform unitParent, Player player)
    {
        _runtime = runtime;
        _board = board;
        _unitParent = unitParent;
        _player = player;
    }

    public void SetHealthBar(HealthBar prefab)
    {
        _healthBarPrefab = prefab;
    }

    public bool CanSpawnNow()
    {
        return _runtime.CanAffordSpawn
            && HasUnspawnedDeckUnit()
            && HasEmptyBottomCell();
    }

    public bool CanSpawnDefinition(UnitStats definition)
    {
        return CanSpawnNow() && _runtime.IsDefinitionAvailable(definition);
    }

    public bool TrySpawnFromDeck(bool asOpening = false)
    {
        UnitStats definition = PickRandomUnspawnedFromDeck();
        if (definition == null)
        {
            Debug.LogWarning("MatchSpawner needs a player with a deck.");
            return false;
        }

        if (!TryPickOpeningBottomCell(out Position start))
        {
            Debug.LogWarning("No empty middle bottom-row tile to spawn on.");
            return false;
        }

        return FinishSpawnFromDeck(definition, start, asOpening);
    }

    public bool TrySpawnFromDeck(UnitStats definition, Position cell)
    {
        if (!CanSpawnDefinition(definition))
            return false;
        if (cell == null || cell.Row != _runtime.HomeRow || !CanSpawnOn(_board.GetCell(cell)))
            return false;

        return FinishSpawnFromDeck(definition, cell, false);
    }

    bool FinishSpawnFromDeck(UnitStats definition, Position start, bool asOpening)
    {
        int cost = _runtime.NextSpawnCost;
        if (!_runtime.CanAffordSpawn)
            return false;

        if (!TrySpawn(definition, true, start, asOpening))
            return false;

        if (cost > 0)
        {
            _runtime.PaidPlayerSpawnCount += 1;
            _runtime.SpawnEnergySpent += cost;
        }

        _runtime.ClearPendingSpawn();
        return true;
    }

    /// <summary>
    /// Removes a paid player unit that has not entered battle.
    /// Refunds the energy it would cost to spawn one unit again.
    /// </summary>
    public bool TryUnspawn(Unit unit)
    {
        if (!_runtime.CanUnspawn(unit))
            return false;

        unit.behaviour.ClearPlan();

        int refund = Energy.RefundForUnspawn(_runtime.PaidPlayerSpawnCount);
        if (refund > 0)
        {
            _runtime.SpawnEnergySpent -= refund;
            if (_runtime.SpawnEnergySpent < 0)
                _runtime.SpawnEnergySpent = 0;
        }

        _runtime.PaidPlayerSpawnCount -= 1;
        if (_runtime.PaidPlayerSpawnCount < 0)
            _runtime.PaidPlayerSpawnCount = 0;

        UnitStats source = unit.behaviour.SourceDefinition;
        if (source != null)
            _runtime.SpawnedDefinitions.Remove(source);

        _runtime.ClearPendingSpawn();

        BoardCell cell = _board.GetCell(unit.behaviour.Position);
        if (cell != null && cell.Occupant == unit)
            cell.Occupant = null;

        if (_runtime.Selected == unit)
        {
            _runtime.Selected = null;
            _runtime.ChosenAction = UnitPlanKind.None;
        }

        _runtime.Units.Remove(unit);
        _runtime.PlayerUnits.Remove(unit);
        Object.Destroy(unit.gameObject);
        return true;
    }

    public bool TrySpawn(UnitStats definition, bool playerControlled, Position start)
    {
        return TrySpawn(definition, playerControlled, start, false);
    }

    bool TrySpawn(UnitStats definition, bool playerControlled, Position start, bool asOpening)
    {
        if (definition == null)
        {
            Debug.LogWarning("MatchSpawner is missing a unit definition.");
            return false;
        }

        if (!_board.InBounds(start))
        {
            Debug.LogWarning($"{definition.displayName} start cell {start} is off the board.");
            return false;
        }

        BoardCell cell = _board.GetCell(start);
        if (cell == null || cell.Occupant != null)
        {
            Debug.LogWarning($"Cannot spawn {definition.displayName} at {start}.");
            return false;
        }

        string unitName = string.IsNullOrEmpty(definition.displayName) ? "Unit" : definition.displayName;
        var go = new GameObject(unitName);
        go.transform.SetParent(_unitParent, false);

        var unit = go.AddComponent<Unit>();
        unit.behaviour = go.AddComponent<UnitBehaviour>();
        unit.behaviour.Setup(definition, start);
        unit.behaviour.OwnerId = playerControlled ? _runtime.LocalPlayerId : _runtime.OpponentPlayerId;
        if (asOpening)
            unit.behaviour.MarkOpeningSpawn();
        unit.Setup(unit.behaviour, _board.CellSize, playerControlled, _healthBarPrefab);
        unit.PlaceAt(start, cell.transform.position);
        cell.Occupant = unit;
        if (playerControlled)
        {
            _runtime.PlayerUnits.Add(unit);
            _runtime.SpawnedDefinitions.Add(definition);
        }

        _runtime.Units.Add(unit);
        return true;
    }

    UnitStats PickRandomUnspawnedFromDeck()
    {
        if (_player == null || _player.deck == null || _player.deck.Count == 0)
            return null;

        int count = 0;
        for (int i = 0; i < _player.deck.Count; i++)
        {
            UnitStats definition = _player.deck[i];
            if (definition != null && !_runtime.SpawnedDefinitions.Contains(definition))
                count++;
        }

        if (count == 0)
            return null;

        int pick = Random.Range(0, count);
        for (int i = 0; i < _player.deck.Count; i++)
        {
            UnitStats definition = _player.deck[i];
            if (definition == null || _runtime.SpawnedDefinitions.Contains(definition))
                continue;
            if (pick == 0)
                return definition;
            pick--;
        }

        return null;
    }

    bool TryPickOpeningBottomCell(out Position start)
    {
        start = null;
        int columns = _board.Columns;
        if (columns <= 0)
            return false;

        if ((columns & 1) == 1)
            return TryUseEmptyBottomCell(columns / 2, out start);

        int left = columns / 2 - 1;
        int right = columns / 2;
        bool leftOpen = IsEmptyBottomCell(left);
        bool rightOpen = IsEmptyBottomCell(right);
        if (leftOpen && rightOpen)
            return TryUseEmptyBottomCell(Random.Range(0, 2) == 0 ? left : right, out start);
        if (leftOpen)
            return TryUseEmptyBottomCell(left, out start);
        if (rightOpen)
            return TryUseEmptyBottomCell(right, out start);
        return false;
    }

    bool TryUseEmptyBottomCell(int column, out Position start)
    {
        start = null;
        BoardCell cell = _board.GetCell(column, _runtime.HomeRow);
        if (!CanSpawnOn(cell))
            return false;
        start = cell.Position;
        return true;
    }

    bool IsEmptyBottomCell(int column)
    {
        return CanSpawnOn(_board.GetCell(column, _runtime.HomeRow));
    }

    static bool CanSpawnOn(BoardCell cell)
    {
        return cell != null && cell.Occupant == null && !cell.HasFlag;
    }

    bool HasUnspawnedDeckUnit()
    {
        if (_player == null || _player.deck == null)
            return false;
        for (int i = 0; i < _player.deck.Count; i++)
        {
            UnitStats definition = _player.deck[i];
            if (definition != null && !_runtime.SpawnedDefinitions.Contains(definition))
                return true;
        }

        return false;
    }

    bool HasEmptyBottomCell()
    {
        for (int column = 0; column < _board.Columns; column++)
        {
            if (CanSpawnOn(_board.GetCell(column, _runtime.HomeRow)))
                return true;
        }

        return false;
    }
}
