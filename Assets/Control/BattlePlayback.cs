using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Plays one battle: spawns first, then each ring slides movers, plays utilities, then plays attacks.
/// Moves last <see cref="MoveSeconds"/>; utilities and attacks each last <see cref="AbilitySeconds"/>.
/// Rings with no acting units only flash for <see cref="EmptyRingSeconds"/>.
/// </summary>
public class BattlePlayback
{
    public const float MoveSeconds = 1f;
    public const float AbilitySeconds = 1.5f;
    public const float SpawnSeconds = 0.8f;
    public const float EmptyRingSeconds = 0.5f;
    public const float BlockedTravelPortion = 0.3f;

    readonly BattleResolver _resolver;
    readonly PlanMarks _marks;
    readonly PlaceholderBoard _board;
    readonly Action _onChanged;
    readonly Action _onMovesSettled;
    readonly Action<BattleTick> _onMovesPresented;
    readonly Func<bool> _matchWon;

    public BattlePlayback(
        BattleResolver resolver,
        PlanMarks marks,
        PlaceholderBoard board,
        Action onChanged,
        Action onMovesSettled,
        Action<BattleTick> onMovesPresented,
        Func<bool> matchWon)
    {
        _resolver = resolver;
        _marks = marks;
        _board = board;
        _onChanged = onChanged;
        _onMovesSettled = onMovesSettled;
        _onMovesPresented = onMovesPresented;
        _matchWon = matchWon;
    }

    public IEnumerator Play()
    {
        _resolver.Begin();
        yield return PlayNewSpawns();
        for (int iteration = 0; iteration <= _resolver.MaxIteration; iteration++)
        {
            if (!_resolver.TryBuildTick(iteration, out BattleTick tick))
                continue;
            yield return PlayResolvedTick(tick, applyResolver: true);
            if (MatchWon())
                break;
        }
    }

    /// <summary>
    /// Plays server-authored ticks for animation only. The caller applies the snapshot afterward.
    /// Spawn ticks run before move and attack rings.
    /// </summary>
    public IEnumerator PlayPreparedTicks(IReadOnlyList<BattleTick> ticks)
    {
        if (ticks == null)
            yield break;

        yield return PlayNewSpawns();

        for (int i = 0; i < ticks.Count; i++)
        {
            BattleTick tick = ticks[i];
            if (tick != null && tick.IsSpawn)
                continue;
            yield return PlayResolvedTick(tick, applyResolver: false);
        }
    }

    IEnumerator PlayNewSpawns()
    {
        var tick = new BattleTick { IsSpawn = true };
        _resolver.CollectNewSpawns(tick.Spawns);
        yield return PlaySpawnTick(tick);
    }

    IEnumerator PlaySpawnTick(BattleTick tick)
    {
        if (tick == null || tick.Spawns.Count == 0)
            yield break;

        _marks.ShowSpawns(tick.Spawns);
        yield return RevealSpawns(tick.Spawns, SpawnSeconds);
        _board.ClearMarks();
    }

    IEnumerator PlayResolvedTick(BattleTick tick, bool applyResolver)
    {
        if (tick.IsEmpty)
        {
            _marks.ShowBattleRing(tick);
            yield return new WaitForSeconds(EmptyRingSeconds);
            if (applyResolver)
            {
                _resolver.FinishTick(tick);
                _onMovesSettled?.Invoke();
            }
            else
                _onMovesPresented?.Invoke(tick);
            _onChanged?.Invoke();
            _board.ClearMarks();
            yield break;
        }

        _marks.ShowBattleTick(tick);

        yield return PlayMoves(tick);
        if (applyResolver)
        {
            _resolver.ApplyMoves(tick);
            _onMovesSettled?.Invoke();
        }
        else
            _onMovesPresented?.Invoke(tick);
        _onChanged?.Invoke();

        if (MatchWon())
        {
            if (applyResolver)
                _resolver.FinishTick(tick);
            _board.ClearMarks();
            yield break;
        }

        _marks.ShowBattleAbilities(tick, UnitPlanKind.Utility);
        yield return PlayAbilities(tick, UnitPlanKind.Utility);
        if (applyResolver)
            _resolver.ApplyAbilities(tick, UnitPlanKind.Utility);
        _onChanged?.Invoke();

        _marks.ShowBattleAbilities(tick, UnitPlanKind.Attack);
        yield return PlayAbilities(tick, UnitPlanKind.Attack);
        if (applyResolver)
        {
            _resolver.ApplyAbilities(tick, UnitPlanKind.Attack);
            _resolver.FinishTick(tick);
        }

        _onChanged?.Invoke();
        _board.ClearMarks();
    }

    bool MatchWon()
    {
        return _matchWon != null && _matchWon();
    }

    IEnumerator PlayMoves(BattleTick tick)
    {
        if (tick.Moves.Count == 0)
            yield break;

        for (int i = 0; i < tick.Moves.Count; i++)
        {
            BattleMove move = tick.Moves[i];
            if (move.Unit != null)
                move.FromWorld = move.Unit.transform.position;
        }

        float elapsed = 0f;
        while (elapsed < MoveSeconds)
        {
            elapsed += Time.deltaTime;
            float u = Mathf.Clamp01(elapsed / MoveSeconds);
            for (int i = 0; i < tick.Moves.Count; i++)
            {
                BattleMove move = tick.Moves[i];
                if (move.Unit == null)
                    continue;
                move.Unit.transform.position = MovePosition(move, u);
            }

            yield return null;
        }

        for (int i = 0; i < tick.Moves.Count; i++)
        {
            BattleMove move = tick.Moves[i];
            if (move.Unit == null)
                continue;
            move.Unit.transform.position = move.Blocked ? move.FromWorld : move.ToWorld;
        }
    }

    static Vector3 MovePosition(BattleMove move, float u)
    {
        if (!move.Blocked)
            return Vector3.Lerp(move.FromWorld, move.ToWorld, u);

        if (u <= BlockedTravelPortion)
            return Vector3.Lerp(move.FromWorld, move.ToWorld, u);

        float back = (u - BlockedTravelPortion) / BlockedTravelPortion;
        Vector3 mid = Vector3.Lerp(move.FromWorld, move.ToWorld, BlockedTravelPortion);
        return back >= 1f ? move.FromWorld : Vector3.Lerp(mid, move.FromWorld, back);
    }

    IEnumerator PlayAbilities(BattleTick tick, UnitPlanKind kind)
    {
        var units = new List<Unit>();
        for (int i = 0; i < tick.Abilities.Count; i++)
        {
            BattleAbility ability = tick.Abilities[i];
            if (ability.Kind == kind && ability.Unit != null)
                units.Add(ability.Unit);
        }

        yield return Pulse(units, AbilitySeconds);
    }

    IEnumerator RevealSpawns(List<Unit> units, float seconds)
    {
        var shown = new List<(Transform Transform, Vector3 Scale, bool Hidden)>();
        for (int i = 0; i < units.Count; i++)
        {
            Unit unit = units[i];
            if (unit == null)
                continue;
            bool hidden = unit.transform.localScale.sqrMagnitude < 0.0001f;
            Vector3 rest = hidden ? unit.BoardScale : unit.transform.localScale;
            shown.Add((unit.transform, rest, hidden));
        }

        if (shown.Count == 0)
            yield break;

        float elapsed = 0f;
        while (elapsed < seconds)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / seconds);
            float wave = 1f + 0.15f * Mathf.Sin(Mathf.PI * t);
            for (int i = 0; i < shown.Count; i++)
            {
                if (shown[i].Transform == null)
                    continue;
                Vector3 scale = shown[i].Hidden
                    ? Vector3.Lerp(Vector3.zero, shown[i].Scale, t)
                    : shown[i].Scale * wave;
                shown[i].Transform.localScale = scale;
            }

            yield return null;
        }

        for (int i = 0; i < shown.Count; i++)
        {
            if (shown[i].Transform != null)
                shown[i].Transform.localScale = shown[i].Scale;
        }
    }

    IEnumerator Pulse(List<Unit> units, float seconds)
    {
        var origins = new List<(Transform Transform, Vector3 Scale)>();
        for (int i = 0; i < units.Count; i++)
        {
            Unit unit = units[i];
            if (unit == null)
                continue;
            origins.Add((unit.transform, unit.transform.localScale));
        }

        if (origins.Count == 0)
            yield break;

        float elapsed = 0f;
        while (elapsed < seconds)
        {
            elapsed += Time.deltaTime;
            float wave = 1f + 0.15f * Mathf.Sin(Mathf.PI * Mathf.Clamp01(elapsed / seconds));
            for (int i = 0; i < origins.Count; i++)
            {
                if (origins[i].Transform != null)
                    origins[i].Transform.localScale = origins[i].Scale * wave;
            }

            yield return null;
        }

        for (int i = 0; i < origins.Count; i++)
        {
            if (origins[i].Transform != null)
                origins[i].Transform.localScale = origins[i].Scale;
        }
    }
}
