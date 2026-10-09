using Match.Api;

namespace Match.Rules;

/// <summary>
/// Center-out ring resolution matching Assets/Control/BattleResolver.cs, for two seats.
/// Each ring moves, then utilities, then attacks. Utility hits allies. Attack hits enemies.
/// </summary>
public static class BattleSimulator
{
    public static List<BattleTickDto> Resolve(MatchState match)
    {
        var ticks = new List<BattleTickDto>();
        for (int i = 0; i < match.Units.Count; i++)
        {
            match.Units[i].HasResolved = false;
        }

        BattleTickDto spawns = BuildSpawnTick(match);
        if (spawns != null)
            ticks.Add(spawns);

        int maxIteration = (match.BoardSize - 1) / 2;
        for (int ring = 0; ring <= maxIteration; ring++)
        {
            ticks.Add(ResolveRing(match, ring));
            if (!string.IsNullOrEmpty(match.WinnerPlayerId))
                break;
        }

        return ticks;
    }

    static BattleTickDto BuildSpawnTick(MatchState match)
    {
        var tick = new BattleTickDto { IsSpawn = true };
        for (int i = 0; i < match.Units.Count; i++)
        {
            UnitState unit = match.Units[i];
            if (unit == null || !unit.IsAlive || unit.IsOpeningSpawn || unit.HasEnteredBattle)
                continue;
            tick.Spawns.Add(new BattleSpawnDto
            {
                UnitId = unit.UnitId,
                OwnerPlayerId = unit.OwnerPlayerId,
                CharacterId = unit.CharacterId,
                Column = unit.Column,
                Row = unit.Row,
                Health = unit.Health,
                MaxHealth = unit.MaxHealth,
                Damage = unit.Damage
            });
        }

        return tick.Spawns.Count == 0 ? null : tick;
    }

    static BattleTickDto ResolveRing(MatchState match, int ring)
    {
        var tick = new BattleTickDto { Ring = ring };
        var onRing = new List<UnitState>();
        var movers = new List<UnitState>();
        var casters = new List<UnitState>();
        ForEachRingCell(match, ring, (column, row) =>
        {
            tick.RingCells.Add(new CellDto { Column = column, Row = row });
            UnitState unit = Occupant(match, column, row);
            if (unit == null || !unit.IsAlive || unit.HasResolved)
                return;
            onRing.Add(unit);
            if (unit.Plan != null && unit.Plan.Type == PlanTypes.Move)
                movers.Add(unit);
            else if (unit.Plan != null
                && (unit.Plan.Type == PlanTypes.Attack || unit.Plan.Type == PlanTypes.Utility))
                casters.Add(unit);
        });

        tick.IsEmpty = movers.Count == 0 && casters.Count == 0;
        if (!tick.IsEmpty)
        {
            BuildMoves(match, movers, tick);
            ApplyMoves(match, tick);
        }

        FlagRules.Capture(match);
        if (!tick.IsEmpty && string.IsNullOrEmpty(match.WinnerPlayerId))
        {
            BuildAbilities(casters, tick);
            ApplyAbilities(match, casters);
        }

        for (int i = 0; i < onRing.Count; i++)
        {
            onRing[i].HasResolved = true;
            onRing[i].HasEnteredBattle = true;
            onRing[i].Plan = null;
        }

        return tick;
    }

    static void BuildMoves(MatchState match, List<UnitState> movers, BattleTickDto tick)
    {
        var destTiles = new Dictionary<Cell, HashSet<string>>();
        for (int i = 0; i < movers.Count; i++)
        {
            UnitState unit = movers[i];
            Cell from = new(unit.Column, unit.Row);
            Cell to = SoleTarget(unit);
            AddClaim(destTiles, from, unit.UnitId);
            AddClaim(destTiles, to, unit.UnitId);
        }

        for (int i = 0; i < movers.Count; i++)
        {
            UnitState unit = movers[i];
            Cell to = SoleTarget(unit);
            UnitState occupant = Occupant(match, to.Column, to.Row);
            bool contested = destTiles.TryGetValue(to, out HashSet<string> claimants) && claimants.Count > 1;
            bool occupied = occupant != null && occupant.UnitId != unit.UnitId;
            bool offBoard = !InBounds(match, to.Column, to.Row);
            tick.Moves.Add(new BattleMoveDto
            {
                UnitId = unit.UnitId,
                Column = to.Column,
                Row = to.Row,
                Blocked = contested || occupied || offBoard
            });
        }
    }

    static void ApplyMoves(MatchState match, BattleTickDto tick)
    {
        for (int i = 0; i < tick.Moves.Count; i++)
        {
            BattleMoveDto move = tick.Moves[i];
            if (move.Blocked)
                continue;
            UnitState unit = Find(match, move.UnitId);
            if (unit == null || !unit.IsAlive)
                continue;
            unit.Column = move.Column;
            unit.Row = move.Row;
        }
    }

    static void BuildAbilities(List<UnitState> casters, BattleTickDto tick)
    {
        AddAbilities(casters, tick, PlanTypes.Utility);
        AddAbilities(casters, tick, PlanTypes.Attack);
    }

    static void AddAbilities(List<UnitState> casters, BattleTickDto tick, string kind)
    {
        for (int i = 0; i < casters.Count; i++)
        {
            UnitState unit = casters[i];
            if (unit.Plan == null || unit.Plan.Type != kind)
                continue;
            var ability = new BattleAbilityDto
            {
                UnitId = unit.UnitId,
                Kind = unit.Plan.Type,
                Damage = unit.Damage
            };
            if (unit.Plan.Targets != null)
                ability.Targets.AddRange(unit.Plan.Targets);
            tick.Abilities.Add(ability);
        }
    }

    static void ApplyAbilities(MatchState match, List<UnitState> casters)
    {
        var dead = new List<UnitState>();
        for (int i = 0; i < casters.Count; i++)
        {
            UnitState unit = casters[i];
            if (!unit.IsAlive || unit.Plan == null)
                continue;

            if (unit.Plan.Type != PlanTypes.Utility)
                continue;
            CharacterRoster.TryGet(unit.CharacterId, out CharacterDefinition def);
            if (def == null || !def.HasUtility)
                continue;

            List<UnitState> allies = FindOccupants(match, unit, allies: true);
            AbilitySubject caster = ReadSubject(unit);
            for (int a = 0; a < allies.Count; a++)
            {
                AbilitySubject ally = ReadSubject(allies[a]);
                def.SourceUtility.Apply(caster, ally);
                WriteSubject(allies[a], ally);
            }

            WriteSubject(unit, caster);
        }

        for (int i = 0; i < casters.Count; i++)
        {
            UnitState unit = casters[i];
            if (!unit.IsAlive || unit.Plan == null || unit.Plan.Type != PlanTypes.Attack)
                continue;

            CharacterRoster.TryGet(unit.CharacterId, out CharacterDefinition def);
            List<UnitState> enemies = FindOccupants(match, unit, allies: false);
            AbilitySubject activator = ReadSubject(unit);
            for (int e = 0; e < enemies.Count; e++)
            {
                UnitState defender = enemies[e];
                AbilitySubject hit = ReadSubject(defender);
                if (def != null && def.SourceAttack != null)
                    def.SourceAttack.Apply(activator, hit);
                else
                {
                    hit.Health -= unit.Damage;
                    if (hit.Health < 0)
                        hit.Health = 0;
                }

                WriteSubject(defender, hit);
                if (!defender.IsAlive && !dead.Contains(defender))
                    dead.Add(defender);
            }

            WriteSubject(unit, activator);
        }

        for (int i = 0; i < dead.Count; i++)
            RemoveDead(match, dead[i]);
    }

    static AbilitySubject ReadSubject(UnitState unit)
    {
        return new AbilitySubject
        {
            Health = unit.Health,
            MaxHealth = unit.MaxHealth,
            Damage = unit.Damage,
            UseAlternateMove = unit.UseSpeedsterMove
        };
    }

    static void WriteSubject(UnitState unit, AbilitySubject subject)
    {
        unit.Health = subject.Health;
        unit.Damage = subject.Damage;
        if (subject.UseAlternateMove || subject.Move != null)
            unit.UseSpeedsterMove = true;
    }

    static List<UnitState> FindOccupants(MatchState match, UnitState source, bool allies)
    {
        var found = new List<UnitState>();
        if (source.Plan?.Targets == null)
            return found;

        for (int i = 0; i < source.Plan.Targets.Count; i++)
        {
            CellDto cell = source.Plan.Targets[i];
            UnitState occupant = Occupant(match, cell.Column, cell.Row);
            if (occupant == null || !occupant.IsAlive)
                continue;
            bool isAlly = occupant.OwnerPlayerId == source.OwnerPlayerId;
            if (isAlly != allies)
                continue;
            if (!found.Contains(occupant))
                found.Add(occupant);
        }

        return found;
    }

    static void RemoveDead(MatchState match, UnitState unit)
    {
        SeatState seat = match.SeatOf(unit.OwnerPlayerId);
        if (seat != null && !seat.Dead.Contains(unit.CharacterId))
            seat.Dead.Add(unit.CharacterId);
        match.Units.Remove(unit);
    }

    static void ForEachRingCell(MatchState match, int iteration, Action<int, int> visit)
    {
        int lowFrame = (match.BoardSize - 1) / 2 - iteration;
        int highFrame = match.BoardSize / 2 + iteration;
        var seen = new HashSet<Cell>();
        void TryVisit(int column, int row)
        {
            if (!InBounds(match, column, row))
                return;
            if (!seen.Add(new Cell(column, row)))
                return;
            visit(column, row);
        }

        for (int i = 0; i <= highFrame - lowFrame; i++)
        {
            TryVisit(lowFrame, lowFrame + i);
            TryVisit(lowFrame + i, lowFrame);
            TryVisit(highFrame - i, highFrame);
            TryVisit(highFrame, highFrame - i);
        }
    }

    static UnitState Occupant(MatchState match, int column, int row)
    {
        for (int i = 0; i < match.Units.Count; i++)
        {
            UnitState unit = match.Units[i];
            if (unit.IsAlive && unit.Column == column && unit.Row == row)
                return unit;
        }

        return null;
    }

    static UnitState Find(MatchState match, string unitId)
    {
        for (int i = 0; i < match.Units.Count; i++)
        {
            if (match.Units[i].UnitId == unitId)
                return match.Units[i];
        }

        return null;
    }

    static Cell SoleTarget(UnitState unit)
    {
        CellDto target = unit.Plan.Targets[0];
        return new Cell(target.Column, target.Row);
    }

    static bool InBounds(MatchState match, int column, int row)
    {
        return column >= 0 && row >= 0 && column < match.Columns && row < match.Rows;
    }

    static void AddClaim(Dictionary<Cell, HashSet<string>> destTiles, Cell cell, string unitId)
    {
        if (!destTiles.TryGetValue(cell, out HashSet<string> units))
        {
            units = new HashSet<string>(StringComparer.Ordinal);
            destTiles[cell] = units;
        }

        units.Add(unitId);
    }
}
