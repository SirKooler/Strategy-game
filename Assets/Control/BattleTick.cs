using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One unit's planned move for a battle ring, including whether it will bounce back.
/// </summary>
public class BattleMove
{
    public Unit Unit;
    public Position Destination;
    public Vector3 FromWorld;
    public Vector3 ToWorld;
    public bool Blocked;
}

/// <summary>
/// One unit's planned attack or utility for a battle ring.
/// Occupants are read when the ability applies: utilities after this ring's moves, attacks after utilities.
/// </summary>
public class BattleAbility
{
    public Unit Unit;
    public UnitPlanKind Kind;
    public readonly List<Position> TargetPositions = new List<Position>();
    public int Damage;
}

/// <summary>
/// Snapshot of one ring from the center outward.
/// Empty means no unit on the ring has a move, attack, or utility.
/// </summary>
public class BattleTick
{
    public bool IsEmpty;
    public bool IsSpawn;
    public readonly List<Unit> Spawns = new List<Unit>();
    public readonly List<Position> RingCells = new List<Position>();
    public readonly List<BattleMove> Moves = new List<BattleMove>();
    public readonly List<BattleAbility> Abilities = new List<BattleAbility>();
    public readonly List<Unit> AllUnits = new List<Unit>();
}
