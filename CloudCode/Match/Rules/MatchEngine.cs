using Match.Api;

namespace Match.Rules;

public static class MatchEngine
{
    public static MatchState Create(string playerId, IReadOnlyList<string> deck)
    {
        playerId = InputGuard.PlayerId(playerId);
        List<string> cleanDeck = DeckValidator.Validate(deck);
        string matchId = Guid.NewGuid().ToString("D");

        var match = new MatchState
        {
            MatchId = matchId,
            Phase = MatchPhases.WaitingForOpponent,
            Turn = 1,
            Host = new SeatState
            {
                PlayerId = playerId,
                Deck = cleanDeck
            }
        };

        FlagRules.Place(match);
        SpawnOpening(match, match.Host);
        return match;
    }

    public static bool Join(MatchState match, string playerId, IReadOnlyList<string> deck)
    {
        playerId = InputGuard.PlayerId(playerId);
        if (match.SeatOf(playerId) != null)
            return false;
        if (match.Phase != MatchPhases.WaitingForOpponent)
            throw new MatchException("Match already has two players.");
        if (!string.IsNullOrEmpty(match.Guest.PlayerId))
            throw new MatchException("Match already has two players.");

        match.Guest = new SeatState
        {
            PlayerId = playerId,
            Deck = DeckValidator.Validate(deck)
        };
        FlagRules.AssignGuest(match);
        SpawnOpening(match, match.Guest);
        match.Phase = MatchPhases.Plan;
        match.LastTicks.Clear();
        return true;
    }

    public static SubmitPlansResult Submit(MatchState match, string playerId, SubmitPlansRequest request)
    {
        playerId = InputGuard.PlayerId(playerId);
        if (request == null)
            throw new MatchException("Plans are required.");

        PlanValidator.ValidateTurn(match, playerId, request);
        SeatState seat = match.SeatOf(playerId);
        seat.Pending = new PendingTurn
        {
            Orders = InputGuard.CappedList(request.Orders, MatchLimits.MaxOrders, "Too many orders."),
            Spawns = InputGuard.CappedList(request.Spawns, MatchLimits.MaxSpawns, "Too many spawns."),
            UnspawnUnitIds = InputGuard.CappedList(request.UnspawnUnitIds, MatchLimits.MaxUnspawns, "Too many unspawns.")
        };
        seat.PlansSubmitted = true;

        SeatState opponent = match.OpponentOf(playerId);
        if (opponent == null || string.IsNullOrEmpty(opponent.PlayerId) || !opponent.PlansSubmitted)
        {
            return new SubmitPlansResult
            {
                Accepted = true,
                WaitingForOpponent = true,
                Match = Snapshot.For(match, playerId)
            };
        }

        match.Phase = MatchPhases.Resolving;
        ApplyPending(match, match.Host);
        ApplyPending(match, match.Guest);
        List<BattleTickDto> ticks = BattleSimulator.Resolve(match);
        match.LastTicks = ticks;
        match.Host.PlansSubmitted = false;
        match.Guest.PlansSubmitted = false;
        match.Host.Pending = null;
        match.Guest.Pending = null;
        match.Host.SpawnEnergySpent = 0;
        match.Guest.SpawnEnergySpent = 0;
        match.Turn += 1;
        match.Phase = string.IsNullOrEmpty(match.WinnerPlayerId) ? MatchPhases.Plan : MatchPhases.Won;

        return new SubmitPlansResult
        {
            Accepted = true,
            WaitingForOpponent = false,
            Ticks = ticks,
            Match = Snapshot.For(match, playerId)
        };
    }

    public static MatchSnapshot Withdraw(MatchState match, string playerId)
    {
        playerId = InputGuard.PlayerId(playerId);
        if (match.Phase != MatchPhases.Plan && match.Phase != MatchPhases.WaitingForOpponent)
            throw new MatchException("Too late to unready.");

        SeatState seat = match.SeatOf(playerId);
        if (seat == null)
            throw new MatchException("Not in this match.");
        if (!seat.PlansSubmitted)
            throw new MatchException("Not ready.");

        SeatState opponent = match.OpponentOf(playerId);
        if (opponent != null && opponent.PlansSubmitted)
            throw new MatchException("Too late to unready.");

        seat.PlansSubmitted = false;
        seat.Pending = null;
        return Snapshot.For(match, playerId);
    }

    static void ApplyPending(MatchState match, SeatState seat)
    {
        if (seat?.Pending == null)
            return;

        PendingTurn pending = seat.Pending;
        for (int i = 0; i < pending.UnspawnUnitIds.Count; i++)
        {
            string unitId = InputGuard.UnitId(pending.UnspawnUnitIds[i]);
            UnitState unit = FindOwned(match, unitId, seat.PlayerId);
            if (unit == null)
                continue;
            match.Units.Remove(unit);
            seat.Spawned.Remove(unit.CharacterId);
            int refund = EnergyRules.RefundForUnspawn(seat.PaidSpawnCount);
            seat.SpawnEnergySpent -= refund;
            if (seat.SpawnEnergySpent < 0)
                seat.SpawnEnergySpent = 0;
            seat.PaidSpawnCount -= 1;
            if (seat.PaidSpawnCount < 0)
                seat.PaidSpawnCount = 0;
        }

        for (int i = 0; i < pending.Spawns.Count; i++)
        {
            SpawnDto spawn = pending.Spawns[i];
            CharacterDefinition def = CharacterRoster.Require(InputGuard.CharacterId(spawn.CharacterId));
            int living = CountOwned(match, seat.PlayerId);
            int cost = living == 0 ? 0 : EnergyRules.SpawnCost(seat.PaidSpawnCount);
            var unit = new UnitState
            {
                UnitId = Guid.NewGuid().ToString("D"),
                OwnerPlayerId = seat.PlayerId,
                CharacterId = def.Id,
                Column = spawn.Column,
                Row = spawn.Row,
                Health = def.MaxHealth,
                MaxHealth = def.MaxHealth,
                Damage = def.Damage
            };
            match.Units.Add(unit);
            if (!seat.Spawned.Contains(def.Id))
                seat.Spawned.Add(def.Id);
            if (cost > 0)
            {
                seat.PaidSpawnCount += 1;
                seat.SpawnEnergySpent += cost;
            }
        }

        for (int i = 0; i < pending.Orders.Count; i++)
        {
            OrderDto order = pending.Orders[i];
            UnitState unit = PlanValidator.ResolveOrderedUnit(match.Units, order, seat.PlayerId);
            if (unit == null)
                continue;
            unit.Plan = PlanValidator.BuildPlan(unit, order, match.Columns, match.Rows);
        }
    }

    static void SpawnOpening(MatchState match, SeatState seat)
    {
        string characterId = PickOpeningCharacter(match.MatchId, seat);
        CharacterDefinition def = CharacterRoster.Require(characterId);
        Cell cell = PickOpeningCell(match, seat);
        match.Units.Add(new UnitState
        {
            UnitId = Guid.NewGuid().ToString("D"),
            OwnerPlayerId = seat.PlayerId,
            CharacterId = def.Id,
            Column = cell.Column,
            Row = cell.Row,
            Health = def.MaxHealth,
            MaxHealth = def.MaxHealth,
            Damage = def.Damage,
            IsOpeningSpawn = true
        });
        seat.Spawned.Add(def.Id);
    }

    static string PickOpeningCharacter(string matchId, SeatState seat)
    {
        int index = StableHash(matchId + ":" + seat.PlayerId) % seat.Deck.Count;
        return seat.Deck[index];
    }

    static Cell PickOpeningCell(MatchState match, SeatState seat)
    {
        int row = match.HomeRow(seat.PlayerId);
        int columns = match.Columns;
        int hash = StableHash(match.MatchId + ":cell:" + seat.PlayerId);
        int column;
        if ((columns & 1) == 1)
            column = columns / 2;
        else
        {
            int left = columns / 2 - 1;
            int right = columns / 2;
            column = (hash & 1) == 0 ? left : right;
        }

        if (CanOpenOn(match, column, row))
            return new Cell(column, row);
        for (int c = 0; c < columns; c++)
        {
            if (CanOpenOn(match, c, row))
                return new Cell(c, row);
        }

        throw new MatchException("No opening cell.");
    }

    static bool CanOpenOn(MatchState match, int column, int row)
    {
        return IsEmpty(match, column, row) && !FlagRules.HasFlag(match, column, row);
    }

    static bool IsEmpty(MatchState match, int column, int row)
    {
        for (int i = 0; i < match.Units.Count; i++)
        {
            UnitState unit = match.Units[i];
            if (unit.IsAlive && unit.Column == column && unit.Row == row)
                return false;
        }

        return true;
    }

    static UnitState FindOwned(MatchState match, string unitId, string playerId)
    {
        for (int i = 0; i < match.Units.Count; i++)
        {
            UnitState unit = match.Units[i];
            if (unit.UnitId == unitId && unit.OwnerPlayerId == playerId)
                return unit;
        }

        return null;
    }

    static int CountOwned(MatchState match, string playerId)
    {
        int count = 0;
        for (int i = 0; i < match.Units.Count; i++)
        {
            if (match.Units[i].OwnerPlayerId == playerId && match.Units[i].IsAlive)
                count++;
        }

        return count;
    }

    public static int StableHash(string value)
    {
        unchecked
        {
            int hash = 23;
            for (int i = 0; i < value.Length; i++)
                hash = hash * 31 + value[i];
            return hash & 0x7fffffff;
        }
    }
}

public static class Snapshot
{
    public static MatchSnapshot For(MatchState match, string playerId)
    {
        SeatState local = match.SeatOf(playerId);
        SeatState opponent = match.OpponentOf(playerId);
        bool hideTicks = local != null && local.PlansSubmitted && (opponent == null || !opponent.PlansSubmitted);
        return new MatchSnapshot
        {
            MatchId = match.MatchId,
            Turn = match.Turn,
            Phase = match.Phase,
            Columns = match.Columns,
            Rows = match.Rows,
            HomeRow = match.HomeRow(playerId),
            LocalPlayerId = playerId,
            OpponentPlayerId = opponent?.PlayerId,
            Local = PublicSeat(match, local),
            Opponent = PublicSeat(match, opponent),
            Units = match.Units,
            Flags = match.Flags,
            WinnerPlayerId = match.WinnerPlayerId,
            LastTicks = hideTicks ? new List<BattleTickDto>() : match.LastTicks,
            WaitingForOpponent = match.Phase == MatchPhases.WaitingForOpponent,
            WaitingForOpponentPlans = local != null && local.PlansSubmitted
                && opponent != null && !string.IsNullOrEmpty(opponent.PlayerId) && !opponent.PlansSubmitted
        };
    }

    static SeatPublic PublicSeat(MatchState match, SeatState seat)
    {
        if (seat == null || string.IsNullOrEmpty(seat.PlayerId))
            return null;

        int spentPlans = 0;
        if (seat.PlansSubmitted && seat.Pending != null)
        {
            for (int i = 0; i < seat.Pending.Orders.Count; i++)
                spentPlans += EnergyRules.Cost(seat.Pending.Orders[i].Type);
        }

        return new SeatPublic
        {
            PlayerId = seat.PlayerId,
            Deck = seat.Deck,
            Spawned = seat.Spawned,
            Dead = seat.Dead,
            PaidSpawnCount = seat.PaidSpawnCount,
            EnergyRemaining = EnergyRules.PoolForTurn(match.Turn) - spentPlans - seat.SpawnEnergySpent,
            PlansSubmitted = seat.PlansSubmitted
        };
    }
}
