/// <summary>
/// Player energy for the current plan phase. Pool is fresh each turn and does not carry over.
/// Turn 1 grants 2; each later turn grants 1 more.
/// Extra characters cost 1, then 2, 4, 6; after that spawning is locked.
/// </summary>
public static class Energy
{
    public const int MoveCost = 1;
    public const int AttackCost = 3;
    public const int UtilityCost = 3;

    /// <summary>Sentinel for a locked spawn. Not a payable amount.</summary>
    public const int InfiniteSpawnCost = -1;

    static readonly int[] SpawnCosts = { 1, 2, 4, 6 };

    public static int PoolForTurn(int turn)
    {
        if (turn < 1)
            turn = 1;
        return turn + 1;
    }

    public static int Cost(PlannedActionType type)
    {
        switch (type)
        {
            case PlannedActionType.Attack:
                return AttackCost;
            case PlannedActionType.Utility:
                return UtilityCost;
            default:
                return MoveCost;
        }
    }

    public static int Cost(UnitPlanKind kind)
    {
        switch (kind)
        {
            case UnitPlanKind.Attack:
                return AttackCost;
            case UnitPlanKind.Utility:
                return UtilityCost;
            default:
                return MoveCost;
        }
    }

    /// <summary>
    /// Cost of the next paid spawn. The opening unit is free and does not use this table.
    /// </summary>
    public static int SpawnCost(int paidSpawnCount)
    {
        if (paidSpawnCount < 0)
            paidSpawnCount = 0;
        if (paidSpawnCount >= SpawnCosts.Length)
            return InfiniteSpawnCost;
        return SpawnCosts[paidSpawnCount];
    }

    public static bool SpawnCostIsInfinite(int paidSpawnCount)
    {
        return SpawnCost(paidSpawnCount) == InfiniteSpawnCost;
    }

    /// <summary>
    /// Energy returned when a paid spawn is cancelled. Same as the cost to summon one unit back.
    /// </summary>
    public static int RefundForUnspawn(int paidSpawnCount)
    {
        if (paidSpawnCount <= 0)
            return 0;
        int cost = SpawnCost(paidSpawnCount - 1);
        return cost < 0 ? 0 : cost;
    }
}
