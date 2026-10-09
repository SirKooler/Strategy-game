using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public partial class MatchController
{
    IEnumerator SendCurrentPlans(System.Action<CloudSubmitPlansResult> done)
    {
        BuildCloudSubmit(out List<CloudOrder> orders, out List<CloudSpawn> spawns, out List<string> unspawns);
        var submit = _cloud.SubmitPlans(_snapshot.matchId, _submittedTurn, orders, spawns, unspawns);
        yield return AwaitTask(submit);
        _cloudReadyBusy = false;
        if (submit.IsFaulted)
        {
            RestoreReady();
            yield break;
        }

        done(submit.Result);
    }

    IEnumerator StartCloudMatch()
    {
        CloudMatchSnapshot snapshot = MatchSession.Consume();
        if (snapshot == null)
        {
            bool hosting = string.IsNullOrWhiteSpace(joinMatchId);
            var signIn = _cloud.SignIn(hosting ? "default" : "guest");
            yield return AwaitTask(signIn);
            if (signIn.IsFaulted)
                yield break;

            List<string> deck = CloudDeckIds();
            var open = hosting ? _cloud.CreateMatch(deck) : _cloud.JoinMatch(joinMatchId.Trim(), deck);
            yield return AwaitTask(open);
            if (open.IsFaulted)
                yield break;

            snapshot = open.Result;
            if (hosting)
                Debug.Log("Created match " + snapshot.matchId);
        }

        OrientBoard(snapshot);
        ApplySnapshot(snapshot);
        _runtime.Phase = MatchPhase.Plan;
        AfterPlanChange(() => _planner.Select(null));

        string opponentAtStart = snapshot.opponentPlayerId;
        while (snapshot.waitingForOpponent)
        {
            yield return new WaitForSeconds(1.5f);
            var poll = _cloud.GetMatch(snapshot.matchId);
            yield return AwaitTask(poll);
            if (poll.IsFaulted)
                yield break;
            snapshot = poll.Result;
        }

        if (!_runtime.IsResolving && snapshot.opponentPlayerId != opponentAtStart)
        {
            bool held = _holdReadyUntilOpponent;
            AddOpponentUnits(snapshot);
            _runtime.Phase = MatchPhase.Plan;
            if (held)
            {
                _runtime.IsWaitingForOpponent = true;
                _marks.Refresh();
                RefreshHud();
                StartCoroutine(ConfirmCloudOrders());
            }
            else
                AfterPlanChange(() => _planner.Select(null));
        }
    }

    void OrientBoard(CloudMatchSnapshot snapshot)
    {
        _runtime.HomeRow = snapshot.homeRow;
        board.ShowFarRowAtBottom(snapshot.homeRow != 0);
    }

    IEnumerator ConfirmCloudOrders()
    {
        bool held = _holdReadyUntilOpponent && _snapshot != null && !_snapshot.waitingForOpponent;
        if (_snapshot == null || _cloudReadyBusy)
            yield break;
        if (_runtime.IsWaitingForOpponent && !held)
            yield break;

        if (!held && _snapshot.waitingForOpponent)
        {
            _holdReadyUntilOpponent = true;
            _submittedTurn = _runtime.Turn;
            BeginReadyWait();
            yield break;
        }

        _holdReadyUntilOpponent = false;
        _cloudReadyBusy = true;
        _cancelReadyRequested = false;
        _submittedTurn = _runtime.Turn;
        BeginReadyWait();

        CloudSubmitPlansResult result = null;
        yield return SendCurrentPlans(value => result = value);
        if (result == null)
            yield break;

        if (_cancelReadyRequested)
        {
            yield return WithdrawCloudReady();
            yield break;
        }

        CloudMatchSnapshot snapshot = result.match;
        List<CloudBattleTick> ticks = result.ticks;
        if (!result.waitingForOpponent)
        {
            yield return PlayCloudBattle(snapshot, ticks);
            yield break;
        }

        while (_runtime.IsWaitingForOpponent)
        {
            yield return new WaitForSeconds(1.5f);
            if (!_runtime.IsWaitingForOpponent)
                yield break;

            var poll = _cloud.GetMatch(_snapshot.matchId);
            yield return AwaitTask(poll);
            if (poll.IsFaulted)
            {
                RestoreReady();
                yield break;
            }

            snapshot = poll.Result;
            if (CloudTurnResolved(snapshot, _submittedTurn))
            {
                ticks = snapshot.lastTicks ?? ticks;
                break;
            }

            if (snapshot.local != null && !snapshot.local.plansSubmitted && !_cloudReadyBusy)
            {
                RestoreReady();
                yield break;
            }
        }

        if (!_runtime.IsWaitingForOpponent)
            yield break;

        yield return PlayCloudBattle(snapshot, ticks);
    }

    void BeginReadyWait()
    {
        _runtime.IsWaitingForOpponent = true;
        _marks.Refresh();
        RefreshHud();
    }

    IEnumerator ReplaceSubmittedPlans()
    {
        if (_snapshot == null || _holdReadyUntilOpponent || _cloudReadyBusy || !_runtime.IsWaitingForOpponent)
            yield break;

        _cloudReadyBusy = true;
        var withdraw = _cloud.WithdrawPlans(_snapshot.matchId);
        yield return AwaitTask(withdraw);
        if (withdraw.IsFaulted || !_runtime.IsWaitingForOpponent)
        {
            _cloudReadyBusy = false;
            yield break;
        }

        _snapshot = withdraw.Result;
        CloudSubmitPlansResult result = null;
        yield return SendCurrentPlans(value => result = value);
        if (result == null)
            yield break;
        if (result.waitingForOpponent)
        {
            _snapshot = result.match;
            _runtime.IsWaitingForOpponent = true;
            RefreshHud();
            yield break;
        }

        yield return PlayCloudBattle(result.match, result.ticks);
    }

    string WaitingPlanFingerprint()
    {
        if (!useCloudMatch || !_runtime.IsWaitingForOpponent || _holdReadyUntilOpponent || _cloudReadyBusy)
            return null;

        BuildCloudSubmit(out List<CloudOrder> orders, out List<CloudSpawn> spawns, out List<string> unspawns);
        var text = new System.Text.StringBuilder();
        for (int i = 0; i < orders.Count; i++)
        {
            CloudOrder order = orders[i];
            text.Append(order.unitId).Append(order.characterId).Append(order.type);
            text.Append(order.column).Append(order.row).Append(order.abilityId).Append(';');
        }

        for (int i = 0; i < spawns.Count; i++)
            text.Append(spawns[i].characterId).Append(spawns[i].column).Append(spawns[i].row).Append('|');
        for (int i = 0; i < unspawns.Count; i++)
            text.Append(unspawns[i]).Append(',');
        return text.ToString();
    }

    IEnumerator WithdrawCloudReady()
    {
        if (_snapshot == null || !_runtime.IsWaitingForOpponent)
            yield break;

        if (_holdReadyUntilOpponent)
        {
            _holdReadyUntilOpponent = false;
            RestoreReady();
            yield break;
        }

        _cancelReadyRequested = true;
        if (_cloudReadyBusy)
            yield break;

        _cloudReadyBusy = true;
        var withdraw = _cloud.WithdrawPlans(_snapshot.matchId);
        yield return AwaitTask(withdraw);
        _cloudReadyBusy = false;
        if (!withdraw.IsFaulted)
        {
            _snapshot = withdraw.Result;
            RestoreReady();
            yield break;
        }

        var poll = _cloud.GetMatch(_snapshot.matchId);
        yield return AwaitTask(poll);
        if (poll.IsFaulted)
        {
            RestoreReady();
            yield break;
        }

        CloudMatchSnapshot snapshot = poll.Result;
        if (CloudTurnResolved(snapshot, _submittedTurn))
        {
            yield return PlayCloudBattle(snapshot, snapshot.lastTicks);
            yield break;
        }

        if (snapshot.local != null && snapshot.local.plansSubmitted)
        {
            _cancelReadyRequested = false;
            _runtime.IsWaitingForOpponent = true;
            RefreshHud();
            yield break;
        }

        RestoreReady();
    }

    IEnumerator PlayCloudBattle(CloudMatchSnapshot snapshot, List<CloudBattleTick> ticks)
    {
        if (_runtime.IsResolving)
            yield break;

        _cancelReadyRequested = false;
        _runtime.IsWaitingForOpponent = false;
        _runtime.IsResolving = true;
        _runtime.Phase = MatchPhase.Battle;
        RefreshHud();
        PlaceBattleSpawns(snapshot, ticks);
        yield return _playback.PlayPreparedTicks(ToBattleTicks(ticks));
        ApplySnapshot(snapshot);
        _runtime.Phase = MatchPhase.Battle;
        _runtime.IsResolving = false;
        RefreshHud();
        yield return new WaitForSeconds(1f);
        NextTurn();
    }

    void RestoreReady()
    {
        _cancelReadyRequested = false;
        _holdReadyUntilOpponent = false;
        _runtime.IsWaitingForOpponent = false;
        _runtime.IsResolving = false;
        _marks.Refresh();
        RefreshHud();
    }

    static bool CloudTurnResolved(CloudMatchSnapshot snapshot, int submittedTurn)
    {
        return snapshot != null && snapshot.turn != submittedTurn;
    }

    void ApplySnapshot(CloudMatchSnapshot snapshot)
    {
        _snapshot = snapshot;
        _runtime.LocalPlayerId = snapshot.localPlayerId;
        _runtime.OpponentPlayerId = snapshot.opponentPlayerId;
        ClearBoardUnits();
        _runtime.Turn = snapshot.turn;
        _runtime.Phase = snapshot.phase == "Plan" || snapshot.phase == "WaitingForOpponent"
            ? MatchPhase.Plan
            : MatchPhase.Battle;
        if (!_cloudReadyBusy)
            _runtime.IsWaitingForOpponent = snapshot.local != null && snapshot.local.plansSubmitted;
        _runtime.SpawnEnergySpent = 0;
        _runtime.PaidPlayerSpawnCount = snapshot.local != null ? snapshot.local.paidSpawnCount : 0;

        if (snapshot.local != null && snapshot.local.dead != null)
        {
            for (int i = 0; i < snapshot.local.dead.Count; i++)
            {
                UnitStats def = DefinitionFor(snapshot.local.dead[i]);
                if (def != null)
                    _runtime.DeadDefinitions.Add(def);
            }
        }

        ApplyFlags(snapshot);
        if (snapshot.units == null)
            return;

        for (int i = 0; i < snapshot.units.Count; i++)
            SpawnCloudUnit(snapshot, snapshot.units[i]);
        TintFlagsFromBoard();
    }

    /// <summary>A unit still standing on a corner owns that flag and paints it.</summary>
    void TintFlagsFromBoard()
    {
        var flags = new List<Flag>();
        board.CollectFlags(flags);
        for (int i = 0; i < flags.Count; i++)
        {
            Flag flag = flags[i];
            BoardCell cell = board.GetCell(flag.Column, flag.Row);
            Unit occupant = cell != null ? cell.Occupant : null;
            if (occupant == null || !occupant.IsAlive)
                continue;
            string owner = _runtime.OwnerIdOf(occupant);
            flag.SetOwner(owner, Flag.ForPlayer(owner, _runtime.LocalPlayerId));
        }
    }

    void ApplyFlags(CloudMatchSnapshot snapshot)
    {
        if (snapshot.flags != null && snapshot.flags.Count > 0)
        {
            for (int i = 0; i < snapshot.flags.Count; i++)
            {
                CloudFlag cloudFlag = snapshot.flags[i];
                if (cloudFlag == null)
                    continue;
                Flag flag = board.GetFlag(cloudFlag.column, cloudFlag.row);
                if (flag == null)
                    continue;
                flag.SetOwner(cloudFlag.ownerPlayerId, Flag.ForPlayer(cloudFlag.ownerPlayerId, _runtime.LocalPlayerId));
            }
        }
        else
            board.AssignFlagOwners(_runtime.LocalPlayerId, _runtime.OpponentPlayerId, _runtime.HomeRow, _runtime.LocalPlayerId);

        if (string.IsNullOrEmpty(snapshot.winnerPlayerId))
            _runtime.Winner = MatchWinner.None;
        else
            _runtime.Winner = snapshot.winnerPlayerId == snapshot.localPlayerId
                ? MatchWinner.Local
                : MatchWinner.Opponent;
    }

    /// <summary>
    /// Puts this turn's ally and enemy spawns on the board before playback.
    /// Units that were not already visible stay hidden until the shared spawn beat.
    /// </summary>
    void PlaceBattleSpawns(CloudMatchSnapshot snapshot, List<CloudBattleTick> ticks)
    {
        var seen = new HashSet<string>();
        if (ticks != null)
        {
            for (int t = 0; t < ticks.Count; t++)
            {
                CloudBattleTick tick = ticks[t];
                if (tick == null || !tick.isSpawn || tick.spawns == null)
                    continue;
                for (int i = 0; i < tick.spawns.Count; i++)
                {
                    CloudBattleSpawn spawn = tick.spawns[i];
                    if (spawn == null || string.IsNullOrEmpty(spawn.unitId) || !seen.Add(spawn.unitId))
                        continue;
                    if (FindByServerId(spawn.unitId) != null)
                        continue;

                    var cloudUnit = new CloudUnit
                    {
                        unitId = spawn.unitId,
                        ownerPlayerId = spawn.ownerPlayerId,
                        characterId = spawn.characterId,
                        column = spawn.column,
                        row = spawn.row,
                        health = spawn.health,
                        maxHealth = spawn.maxHealth,
                        damage = spawn.damage
                    };
                    if (TryClaimVisibleSpawn(snapshot, cloudUnit))
                        continue;

                    SpawnCloudUnit(snapshot, cloudUnit);
                    HideUntilSpawnBeat(spawn.unitId);
                }
            }
        }

        if (snapshot == null || snapshot.units == null)
            return;

        for (int i = 0; i < snapshot.units.Count; i++)
        {
            CloudUnit cloudUnit = snapshot.units[i];
            if (cloudUnit == null || string.IsNullOrEmpty(cloudUnit.unitId) || !seen.Add(cloudUnit.unitId))
                continue;
            if (FindByServerId(cloudUnit.unitId) != null)
                continue;
            SpawnCloudUnit(snapshot, cloudUnit);
            Unit unit = FindByServerId(cloudUnit.unitId);
            if (unit == null || unit.behaviour == null || unit.behaviour.IsOpeningSpawn)
                continue;
            unit.behaviour.SetEnteredBattle(false);
            unit.transform.localScale = Vector3.zero;
        }
    }

    /// <summary>
    /// A character placed this plan is already on the board, but the server id arrives with the battle.
    /// Bind that id so its move can play. Leave it visible; only brand-new bodies wait for the spawn beat.
    /// </summary>
    bool TryClaimVisibleSpawn(CloudMatchSnapshot snapshot, CloudUnit cloudUnit)
    {
        if (cloudUnit == null || string.IsNullOrEmpty(cloudUnit.unitId))
            return false;

        BoardCell cell = board.GetCell(cloudUnit.column, cloudUnit.row);
        Unit unit = cell != null ? cell.Occupant : null;
        if (unit == null || unit.behaviour == null || !string.IsNullOrEmpty(unit.behaviour.ServerUnitId))
            return false;
        if (!SameCharacter(unit, cloudUnit.characterId))
            return false;

        bool owned = snapshot != null
            && snapshot.localPlayerId != null
            && cloudUnit.ownerPlayerId == snapshot.localPlayerId;
        if (owned != _runtime.PlayerUnits.Contains(unit))
            return false;

        unit.behaviour.ServerUnitId = cloudUnit.unitId;
        unit.behaviour.OwnerId = cloudUnit.ownerPlayerId;
        unit.behaviour.ApplyServerStats(cloudUnit.health, cloudUnit.maxHealth, cloudUnit.damage);
        return true;
    }

    static bool SameCharacter(Unit unit, string characterId)
    {
        return unit != null
            && unit.behaviour != null
            && UnitConstants.TryGetPlayableId(unit.behaviour.SourceDefinition, out string id)
            && id == characterId;
    }

    void HideUntilSpawnBeat(string unitId)
    {
        Unit unit = FindByServerId(unitId);
        if (unit == null || unit.behaviour == null || unit.behaviour.IsOpeningSpawn)
            return;
        unit.behaviour.SetEnteredBattle(false);
        unit.transform.localScale = Vector3.zero;
    }

    void AddOpponentUnits(CloudMatchSnapshot snapshot)
    {
        _snapshot = snapshot;
        if (snapshot.units == null)
            return;

        for (int i = 0; i < snapshot.units.Count; i++)
        {
            CloudUnit cloudUnit = snapshot.units[i];
            if (snapshot.localPlayerId != null && cloudUnit.ownerPlayerId == snapshot.localPlayerId)
                continue;
            if (FindByServerId(cloudUnit.unitId) != null)
                continue;
            SpawnCloudUnit(snapshot, cloudUnit);
        }
    }

    void SpawnCloudUnit(CloudMatchSnapshot snapshot, CloudUnit cloudUnit)
    {
        UnitStats def = DefinitionFor(cloudUnit.characterId);
        bool owned = snapshot.localPlayerId != null && cloudUnit.ownerPlayerId == snapshot.localPlayerId;
        var start = new Position(cloudUnit.column, cloudUnit.row);
        if (!_spawner.TrySpawn(def, owned, start))
            return;
        BoardCell cell = board.GetCell(start);
        Unit unit = cell != null ? cell.Occupant : null;
        if (unit == null || unit.behaviour == null)
            return;
        unit.behaviour.ServerUnitId = cloudUnit.unitId;
        unit.behaviour.OwnerId = cloudUnit.ownerPlayerId;
        unit.behaviour.ApplyServerStats(cloudUnit.health, cloudUnit.maxHealth, cloudUnit.damage);
        if (cloudUnit.isOpeningSpawn)
            unit.behaviour.MarkOpeningSpawn();
        unit.behaviour.SetEnteredBattle(cloudUnit.hasEnteredBattle);
    }

    void ClearBoardUnits()
    {
        for (int i = _runtime.Units.Count - 1; i >= 0; i--)
        {
            Unit unit = _runtime.Units[i];
            if (unit == null)
                continue;
            if (unit.behaviour != null)
            {
                BoardCell cell = board.GetCell(unit.behaviour.Position);
                if (cell != null && cell.Occupant == unit)
                    cell.Occupant = null;
            }

            Destroy(unit.gameObject);
        }

        _runtime.Units.Clear();
        _runtime.PlayerUnits.Clear();
        _runtime.SpawnedDefinitions.Clear();
        _runtime.DeadDefinitions.Clear();
        _runtime.Selected = null;
        _runtime.ClearPendingSpawn();
    }

    void BuildCloudSubmit(out List<CloudOrder> orders, out List<CloudSpawn> spawns, out List<string> unspawns)
    {
        orders = new List<CloudOrder>();
        spawns = new List<CloudSpawn>();
        unspawns = new List<string>();

        var presentIds = new HashSet<string>();
        for (int i = 0; i < _runtime.Units.Count; i++)
        {
            Unit unit = _runtime.Units[i];
            if (unit == null || !_runtime.PlayerUnits.Contains(unit) || unit.behaviour == null)
                continue;
            string characterId;
            UnitConstants.TryGetPlayableId(unit.behaviour.SourceDefinition, out characterId);
            if (string.IsNullOrEmpty(unit.behaviour.ServerUnitId))
            {
                spawns.Add(new CloudSpawn
                {
                    characterId = characterId,
                    column = unit.behaviour.Position.Column,
                    row = unit.behaviour.Position.Row
                });
            }
            else
                presentIds.Add(unit.behaviour.ServerUnitId);

            PlannedAction plan = unit.behaviour.Plan;
            if (plan == null)
                continue;
            var order = new CloudOrder
            {
                unitId = unit.behaviour.ServerUnitId,
                characterId = characterId,
                type = plan.Type.ToString(),
                abilityId = plan.AbilityIdentifier
            };
            if (plan.Type == PlannedActionType.Move && plan.TargetPositions != null)
            {
                Position dest = plan.GetSoleTarget();
                order.column = dest.Column;
                order.row = dest.Row;
            }

            orders.Add(order);
        }

        if (_snapshot.units == null)
            return;
        for (int i = 0; i < _snapshot.units.Count; i++)
        {
            CloudUnit cloudUnit = _snapshot.units[i];
            if (cloudUnit.ownerPlayerId != _snapshot.localPlayerId)
                continue;
            if (presentIds.Contains(cloudUnit.unitId))
                continue;
            if (cloudUnit.isOpeningSpawn || cloudUnit.hasEnteredBattle)
                continue;
            unspawns.Add(cloudUnit.unitId);
        }
    }

    List<BattleTick> ToBattleTicks(List<CloudBattleTick> source)
    {
        var ticks = new List<BattleTick>();
        if (source == null)
            return ticks;
        for (int i = 0; i < source.Count; i++)
            ticks.Add(ToBattleTick(source[i]));
        return ticks;
    }

    BattleTick ToBattleTick(CloudBattleTick cloud)
    {
        if (cloud.isSpawn && cloud.spawns != null && cloud.spawns.Count > 0)
            return ToSpawnTick(cloud);

        var tick = new BattleTick { IsEmpty = cloud.isEmpty };
        CopyCells(cloud.ringCells, tick.RingCells);
        if (cloud.moves != null)
        {
            for (int i = 0; i < cloud.moves.Count; i++)
            {
                CloudBattleMove move = cloud.moves[i];
                Unit unit = FindByServerId(move.unitId);
                Vector3 from = unit != null ? unit.transform.position : Vector3.zero;
                tick.Moves.Add(new BattleMove
                {
                    Unit = unit,
                    Destination = new Position(move.column, move.row),
                    FromWorld = from,
                    ToWorld = CellWorld(move.column, move.row, from),
                    Blocked = move.blocked
                });
            }
        }

        if (cloud.abilities != null)
        {
            for (int i = 0; i < cloud.abilities.Count; i++)
            {
                CloudBattleAbility ability = cloud.abilities[i];
                var local = new BattleAbility
                {
                    Unit = FindByServerId(ability.unitId),
                    Kind = ability.kind == "Utility" ? UnitPlanKind.Utility : UnitPlanKind.Attack,
                    Damage = ability.damage
                };
                CopyCells(ability.targets, local.TargetPositions);
                tick.Abilities.Add(local);
            }
        }

        return tick;
    }

    BattleTick ToSpawnTick(CloudBattleTick cloud)
    {
        var tick = new BattleTick { IsSpawn = true };
        for (int i = 0; i < cloud.spawns.Count; i++)
        {
            CloudBattleSpawn spawn = cloud.spawns[i];
            Unit unit = FindByServerId(spawn.unitId);
            if (unit == null && _snapshot != null)
            {
                SpawnCloudUnit(_snapshot, new CloudUnit
                {
                    unitId = spawn.unitId,
                    ownerPlayerId = spawn.ownerPlayerId,
                    characterId = spawn.characterId,
                    column = spawn.column,
                    row = spawn.row,
                    health = spawn.health,
                    maxHealth = spawn.maxHealth,
                    damage = spawn.damage
                });
                unit = FindByServerId(spawn.unitId);
            }

            if (unit != null)
                tick.Spawns.Add(unit);
        }

        return tick;
    }

    static void CopyCells(List<CloudCell> from, List<Position> to)
    {
        if (from == null)
            return;
        for (int i = 0; i < from.Count; i++)
            to.Add(new Position(from[i].column, from[i].row));
    }

    Vector3 CellWorld(int column, int row, Vector3 fallback)
    {
        BoardCell cell = board.GetCell(column, row);
        return cell != null ? cell.transform.position : fallback;
    }

    Unit FindByServerId(string unitId)
    {
        if (string.IsNullOrEmpty(unitId))
            return null;
        for (int i = 0; i < _runtime.Units.Count; i++)
        {
            Unit unit = _runtime.Units[i];
            if (unit != null && unit.behaviour != null && unit.behaviour.ServerUnitId == unitId)
                return unit;
        }

        return null;
    }

    List<string> CloudDeckIds()
    {
        var ids = new List<string>();
        if (player == null || player.deck == null)
            return ids;
        for (int i = 0; i < player.deck.Count; i++)
        {
            if (UnitConstants.TryGetPlayableId(player.deck[i], out string id))
                ids.Add(id);
        }

        return ids;
    }

    UnitStats DefinitionFor(string characterId)
    {
        if (player != null && player.deck != null)
        {
            for (int i = 0; i < player.deck.Count; i++)
            {
                UnitStats definition = player.deck[i];
                if (UnitConstants.TryGetPlayableId(definition, out string id) && id == characterId)
                    return definition;
            }
        }

        if (UnitConstants.TryCreate(characterId, out UnitStats created))
            return created;
        return null;
    }

    static IEnumerator AwaitTask(System.Threading.Tasks.Task task)
    {
        while (!task.IsCompleted)
            yield return null;
        if (task.IsFaulted && task.Exception != null)
            Debug.LogError(task.Exception.GetBaseException().Message);
    }
}
