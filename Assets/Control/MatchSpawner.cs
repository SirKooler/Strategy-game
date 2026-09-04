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
    Sprite _unitSprite;

    public MatchSpawner(MatchRuntime runtime, PlaceholderBoard board, Transform unitParent, Player player)
    {
        _runtime = runtime;
        _board = board;
        _unitParent = unitParent;
        _player = player;
    }

    public void SetUnitSprite(Sprite sprite)
    {
        _unitSprite = sprite;
    }

    public bool CanSpawnNow()
    {
        return _runtime.CanAffordSpawn
            && HasUnspawnedDeckUnit()
            && HasEmptyBottomCell();
    }

    public bool TrySpawnFromDeck(bool asOpening = false)
    {
        int cost = _runtime.NextSpawnCost;
        if (!_runtime.CanAffordSpawn)
            return false;

        UnitStats definition = PickRandomUnspawnedFromDeck();
        if (definition == null)
        {
            Debug.LogWarning("MatchSpawner needs a player with a deck.");
            return false;
        }

        if (!TryPickRandomEmptyBottomCell(out Position start))
        {
            Debug.LogWarning("No empty bottom-row tile to spawn on.");
            return false;
        }

        if (!TrySpawn(definition, true, start, asOpening))
            return false;

        if (cost > 0)
        {
            _runtime.PaidPlayerSpawnCount += 1;
            _runtime.SpawnEnergySpent += cost;
        }

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
        float size = _board.CellSize;
        go.transform.localScale = new Vector3(size, size, 1f);

        var unit = go.AddComponent<Unit>();
        unit.behaviour = go.AddComponent<UnitBehaviour>();
        unit.behaviour.Setup(definition, start);
        if (asOpening)
            unit.behaviour.MarkOpeningSpawn();
        unit.Setup(unit.behaviour, _unitSprite);
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

    bool TryPickRandomEmptyBottomCell(out Position start)
    {
        start = null;
        int empty = 0;
        for (int column = 0; column < _board.Columns; column++)
        {
            BoardCell cell = _board.GetCell(column, 0);
            if (cell != null && cell.Occupant == null)
                empty++;
        }

        if (empty == 0)
            return false;

        int pick = Random.Range(0, empty);
        for (int column = 0; column < _board.Columns; column++)
        {
            BoardCell cell = _board.GetCell(column, 0);
            if (cell == null || cell.Occupant != null)
                continue;
            if (pick == 0)
            {
                start = cell.Position;
                return true;
            }

            pick--;
        }

        return false;
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
            BoardCell cell = _board.GetCell(column, 0);
            if (cell != null && cell.Occupant == null)
                return true;
        }

        return false;
    }
}
