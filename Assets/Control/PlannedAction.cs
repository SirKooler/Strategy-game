using System;
using System.Collections.Generic;

/// <summary>
/// Kind of order a unit can plan.
/// </summary>
public enum PlannedActionType
{
    Attack,
    Move,
    Utility
}

/// <summary>
/// One planned order: who does it, what kind, and which cells it targets.
/// Move stores one destination in <see cref="TargetPositions"/>. Attack and Utility store every covered cell.
/// Runtime-only; Unity cannot serialize <see cref="HashSet{T}"/>.
/// </summary>
public class PlannedAction
{
    public PlannedActionType Type;
    public HashSet<Position> TargetPositions;
    public Unit Character;

    /// <summary>
    /// Returns the sole target position if the plan targets exactly one cell (a move destination).
    /// Throws an InvalidOperationException if there is not exactly one target.
    /// </summary>
    public Position GetSoleTarget()
    {
        if (TargetPositions == null || TargetPositions.Count != 1)
            throw new InvalidOperationException("PlannedAction does not have exactly one target.");
        foreach (Position position in TargetPositions)
            return position;
        throw new InvalidOperationException("PlannedAction TargetPositions is empty.");
    }

    public static UnitPlanKind ToPlanKind(PlannedActionType type)
    {
        switch (type)
        {
            case PlannedActionType.Move:
                return UnitPlanKind.Move;
            case PlannedActionType.Attack:
                return UnitPlanKind.Attack;
            case PlannedActionType.Utility:
                return UnitPlanKind.Utility;
            default:
                return UnitPlanKind.None;
        }
    }
}
