namespace Match.Rules;

/// <summary>
/// Mirrors Assets/Control/Energy.cs.
/// </summary>
public static class EnergyRules
{
    public const int MoveCost = 1;
    public const int AttackCost = 3;
    public const int UtilityCost = 3;
    public const int InfiniteSpawnCost = -1;

    static readonly int[] SpawnCosts = { 1, 2, 4, 7, 8, 9, 10, 10 };

    public static int PoolForTurn(int turn)
    {
        if (turn < 1)
            turn = 1;
        return turn + 1;
    }

    public static int Cost(string type)
    {
        if (type == PlanTypes.Attack)
            return AttackCost;
        if (type == PlanTypes.Utility)
            return UtilityCost;
        return MoveCost;
    }

    public static int SpawnCost(int paidSpawnCount)
    {
        if (paidSpawnCount < 0)
            paidSpawnCount = 0;
        if (paidSpawnCount >= SpawnCosts.Length)
            return InfiniteSpawnCost;
        return SpawnCosts[paidSpawnCount];
    }

    public static int RefundForUnspawn(int paidSpawnCount)
    {
        if (paidSpawnCount <= 0)
            return 0;
        int cost = SpawnCost(paidSpawnCount - 1);
        return cost < 0 ? 0 : cost;
    }
}

public static class PlanTypes
{
    public const string Move = "Move";
    public const string Attack = "Attack";
    public const string Utility = "Utility";

    public static bool IsKnown(string type)
    {
        return type == Move || type == Attack || type == Utility;
    }
}
