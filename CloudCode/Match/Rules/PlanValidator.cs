using Match.Api;

namespace Match.Rules;

public static class PlanValidator
{
    public static void ValidateTurn(MatchState match, string playerId, SubmitPlansRequest request)
    {
        if (match.Phase != MatchPhases.Plan && match.Phase != MatchPhases.WaitingForOpponent)
            throw new MatchException("Not in plan phase.");
        if (request.Turn != match.Turn)
            throw new MatchException("Turn does not match.");

        SeatState seat = match.SeatOf(playerId);
        if (seat == null)
            throw new MatchException("Not in this match.");
        if (seat.PlansSubmitted)
            throw new MatchException("Plans already submitted.");

        List<OrderDto> orders = InputGuard.CappedList(request.Orders, MatchLimits.MaxOrders, "Too many orders.");
        List<SpawnDto> spawns = InputGuard.CappedList(request.Spawns, MatchLimits.MaxSpawns, "Too many spawns.");
        List<string> unspawns = InputGuard.CappedList(request.UnspawnUnitIds, MatchLimits.MaxUnspawns, "Too many unspawns.");

        var working = CloneUnits(match.Units);
        var spawned = new HashSet<string>(seat.Spawned ?? new List<string>(), StringComparer.Ordinal);
        var dead = new HashSet<string>(seat.Dead ?? new List<string>(), StringComparer.Ordinal);
        int paid = seat.PaidSpawnCount;
        int spent = 0;
        int pool = EnergyRules.PoolForTurn(match.Turn);
        int homeRow = match.HomeRow(playerId);

        for (int i = 0; i < unspawns.Count; i++)
        {
            string unitId = InputGuard.UnitId(unspawns[i]);
            UnitState unit = FindOwned(working, unitId, playerId);
            if (unit == null || !unit.IsAlive)
                throw new MatchException("Cannot unspawn that unit.");
            if (unit.IsOpeningSpawn || unit.HasEnteredBattle)
                throw new MatchException("Cannot unspawn that unit.");

            working.Remove(unit);
            spawned.Remove(unit.CharacterId);
            int refund = EnergyRules.RefundForUnspawn(paid);
            spent -= refund;
            if (spent < 0)
                spent = 0;
            paid -= 1;
            if (paid < 0)
                paid = 0;
        }

        var occupied = Occupied(working);
        for (int i = 0; i < spawns.Count; i++)
        {
            SpawnDto spawn = spawns[i];
            string characterId = InputGuard.CharacterId(spawn.CharacterId);
            InputGuard.EnsureInBounds(spawn.Column, spawn.Row, match.Columns, match.Rows);
            if (spawn.Row != homeRow)
                throw new MatchException("Spawn must be on your home row.");
            if (!seat.Deck.Contains(characterId))
                throw new MatchException("Character is not in the deck.");
            if (spawned.Contains(characterId) || dead.Contains(characterId))
                throw new MatchException("Character is not available.");
            if (occupied.Contains(new Cell(spawn.Column, spawn.Row)))
                throw new MatchException("Spawn cell is occupied.");
            if (FlagRules.HasFlag(match, spawn.Column, spawn.Row))
                throw new MatchException("Cannot spawn on a flag.");

            int livingOwned = CountOwned(working, playerId);
            int cost = livingOwned == 0 ? 0 : EnergyRules.SpawnCost(paid);
            if (cost == EnergyRules.InfiniteSpawnCost || spent + cost > pool)
                throw new MatchException("Not enough energy to spawn.");

            CharacterDefinition def = CharacterRoster.Require(characterId);
            var unit = new UnitState
            {
                UnitId = Guid.NewGuid().ToString("D"),
                OwnerPlayerId = playerId,
                CharacterId = characterId,
                Column = spawn.Column,
                Row = spawn.Row,
                Health = def.MaxHealth,
                MaxHealth = def.MaxHealth,
                Damage = def.Damage
            };
            working.Add(unit);
            occupied.Add(new Cell(spawn.Column, spawn.Row));
            spawned.Add(characterId);
            if (cost > 0)
            {
                paid += 1;
                spent += cost;
            }
        }

        var seenUnits = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < orders.Count; i++)
        {
            OrderDto order = orders[i];
            if (!PlanTypes.IsKnown(order.Type))
                throw new MatchException("Unknown order type.");
            UnitState unit = ResolveOrderedUnit(working, order, playerId);
            if (unit == null || !unit.IsAlive)
                throw new MatchException("Cannot order that unit.");
            if (!seenUnits.Add(unit.UnitId))
                throw new MatchException("A unit can only have one order.");

            CharacterDefinition def = CharacterRoster.Require(unit.CharacterId);
            int cost = EnergyRules.Cost(order.Type);
            if (spent + cost > pool)
                throw new MatchException("Not enough energy.");
            spent += cost;

            var origin = new Cell(unit.Column, unit.Row);
            if (order.Type == PlanTypes.Move)
            {
                InputGuard.EnsureInBounds(order.Column, order.Row, match.Columns, match.Rows);
                if (!CanReachMove(unit, new Cell(order.Column, order.Row)))
                    throw new MatchException("Move is out of reach.");
                continue;
            }

            if (order.Type == PlanTypes.Utility && !def.HasUtility)
                throw new MatchException("Unit has no utility.");
            if (order.AbilityId < 0 || order.AbilityId > 255)
                throw new MatchException("Invalid ability.");

            AttackPattern pattern = order.Type == PlanTypes.Attack ? def.Attack : def.UtilityPattern;
            var covered = new List<Cell>();
            pattern.CollectBoardCellsWithIdentifier(origin, (byte)order.AbilityId, covered);
            if (covered.Count == 0)
                throw new MatchException("Ability does not cover any cells.");
        }
    }

    public static PlannedActionState BuildPlan(UnitState unit, OrderDto order, int columns, int rows)
    {
        var origin = new Cell(unit.Column, unit.Row);
        if (order.Type == PlanTypes.Move)
        {
            return new PlannedActionState
            {
                Type = PlanTypes.Move,
                Targets = new List<CellDto> { new() { Column = order.Column, Row = order.Row } }
            };
        }

        CharacterDefinition def = CharacterRoster.Require(unit.CharacterId);
        AttackPattern pattern = order.Type == PlanTypes.Attack ? def.Attack : def.UtilityPattern;
        var cells = new List<Cell>();
        pattern.CollectBoardCellsWithIdentifier(origin, (byte)order.AbilityId, cells);
        var targets = new List<CellDto>();
        for (int i = 0; i < cells.Count; i++)
        {
            Cell cell = cells[i];
            if (cell.Column < 0 || cell.Row < 0 || cell.Column >= columns || cell.Row >= rows)
                continue;
            targets.Add(new CellDto { Column = cell.Column, Row = cell.Row });
        }

        return new PlannedActionState { Type = order.Type, Targets = targets };
    }

    public static bool CanReachMove(UnitState unit, Cell destination)
    {
        var reach = new List<Cell>();
        CharacterRoster.MoveFor(unit.CharacterId, unit.UseSpeedsterMove)
            .CollectBoardCells(new Cell(unit.Column, unit.Row), reach);
        for (int i = 0; i < reach.Count; i++)
        {
            if (reach[i].Equals(destination))
                return true;
        }

        return false;
    }

    public static UnitState ResolveOrderedUnit(List<UnitState> units, OrderDto order, string playerId)
    {
        if (!string.IsNullOrEmpty(order.UnitId) && Guid.TryParse(order.UnitId, out _))
        {
            UnitState byId = FindOwned(units, InputGuard.UnitId(order.UnitId), playerId);
            if (byId != null)
                return byId;
        }

        if (string.IsNullOrEmpty(order.CharacterId))
            return null;
        string characterId = InputGuard.CharacterId(order.CharacterId);
        return FindOwnedByCharacter(units, characterId, playerId);
    }

    static UnitState FindOwnedByCharacter(List<UnitState> units, string characterId, string playerId)
    {
        for (int i = 0; i < units.Count; i++)
        {
            UnitState unit = units[i];
            if (unit.CharacterId == characterId && unit.OwnerPlayerId == playerId && unit.IsAlive)
                return unit;
        }

        return null;
    }

    static UnitState FindOwned(List<UnitState> units, string unitId, string playerId)
    {
        for (int i = 0; i < units.Count; i++)
        {
            UnitState unit = units[i];
            if (unit.UnitId == unitId && unit.OwnerPlayerId == playerId)
                return unit;
        }

        return null;
    }

    static int CountOwned(List<UnitState> units, string playerId)
    {
        int count = 0;
        for (int i = 0; i < units.Count; i++)
        {
            if (units[i].OwnerPlayerId == playerId && units[i].IsAlive)
                count++;
        }

        return count;
    }

    static HashSet<Cell> Occupied(List<UnitState> units)
    {
        var cells = new HashSet<Cell>();
        for (int i = 0; i < units.Count; i++)
        {
            if (units[i].IsAlive)
                cells.Add(new Cell(units[i].Column, units[i].Row));
        }

        return cells;
    }

    static List<UnitState> CloneUnits(List<UnitState> units)
    {
        var clone = new List<UnitState>(units.Count);
        for (int i = 0; i < units.Count; i++)
        {
            UnitState u = units[i];
            clone.Add(new UnitState
            {
                UnitId = u.UnitId,
                OwnerPlayerId = u.OwnerPlayerId,
                CharacterId = u.CharacterId,
                Column = u.Column,
                Row = u.Row,
                Health = u.Health,
                MaxHealth = u.MaxHealth,
                Damage = u.Damage,
                IsOpeningSpawn = u.IsOpeningSpawn,
                HasEnteredBattle = u.HasEnteredBattle,
                UseSpeedsterMove = u.UseSpeedsterMove
            });
        }

        return clone;
    }
}
