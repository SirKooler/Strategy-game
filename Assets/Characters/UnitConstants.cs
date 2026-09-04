using UnityEngine;

/// <summary>
/// Authoring numbers for one placeholder unit. Not locked combat math.
/// </summary>
public readonly struct UnitIdentity
{
    public readonly string AssetName;
    public readonly string DisplayName;
    public readonly int MaxHealth;
    public readonly int Damage;
    public readonly Color Color;
    public readonly MovementPattern Move;
    public readonly AttackPattern Attack;
    public readonly UtilityAbility Utility;

    public UnitIdentity(
        string assetName,
        string displayName,
        int maxHealth,
        int damage,
        Color color,
        MovementPattern move,
        AttackPattern attack,
        UtilityAbility utility = null)
    {
        AssetName = assetName;
        DisplayName = displayName;
        MaxHealth = maxHealth;
        Damage = damage;
        Color = color;
        Move = move;
        Attack = attack;
        Utility = utility;
    }

    public bool Matches(UnitStats definition)
    {
        return definition.name == AssetName || definition.displayName == DisplayName;
    }

    public void ApplyTo(UnitStats stats)
    {
        stats.displayName = DisplayName;
        stats.maxHealth = MaxHealth;
        stats.health = MaxHealth;
        stats.damage = Damage;
        stats.placeholderColor = Color;
        stats.movePattern = Move;
        stats.attackPattern = Attack;
        stats.utility = Utility;
    }
}

/// <summary>
/// All placeholder unit identities.
/// </summary>
public static class UnitConstants
{
    public static readonly UnitIdentity Tank = new UnitIdentity(
        "Tank",
        "Tank",
        34,
        3,
        new Color(0.35f, 0.48f, 0.72f, 1f),
        MovementPattern.Plus(1),
        AttackPattern.Square(1),
        UtilityAbility.HealPlus(10));

    public static readonly UnitIdentity Ranger = new UnitIdentity(
        "Ranger",
        "Ranger",
        8,
        8,
        new Color(0.36f, 0.62f, 0.32f, 1f),
        MovementPattern.Plus(1),
        AttackPattern.X(3),
        UtilityAbility.HealPlus(10));

    public static readonly UnitIdentity Octopus = new UnitIdentity(
        "Octopus",
        "Octopus",
        16,
        5,
        new Color(0.56f, 0.62f, 0.32f, 1f),
        MovementPattern.X(1),
        AttackPattern.Octopus(2, true));
    
    public static readonly UnitIdentity Sniper = new UnitIdentity(
        "Sniper",
        "Sniper",
        4,
        5,
        new Color(0.200f, 0.12f, 0.112f, 1f),
        MovementPattern.Square(1),
        AttackPattern.Plus(4));
    
    public static readonly UnitIdentity SwordMan = new UnitIdentity(
        "SwordMan",
        "SwordMan",
        20,
        10,
        new Color(0.56f, 0.112f, 0.12f, 1f),
        MovementPattern.Square(1),
        AttackPattern.FromGrid(new[]
        {
            new[] { new byte[] { 0, 1 }, new byte[] { 0 }, new byte[] { 0, 2 } },
            new[] { new byte[] { 1 }, new byte[] { }, new byte[] { 2 } },
            new[] { new byte[] { 1, 3 }, new byte[] { 3 }, new byte[] { 2, 3 } },
        }),
        new UtilityAbility(AttackPattern.Square(1, false), target => target.behaviour.CurrentStats.damage += 100));

    public static readonly UnitIdentity Dummy = new UnitIdentity(
        "DummyTarget",
        "Dummy target (placeholder)",
        16,
        0,
        new Color(0.72f, 0.32f, 0.28f, 1f),
        MovementPattern.Empty(),
        AttackPattern.Empty());

    public static readonly UnitIdentity[] All = { Tank, Ranger, Octopus, Sniper, SwordMan, Dummy };

    public static void ApplyIfKnown(UnitStats stats)
    {
        for (int i = 0; i < All.Length; i++)
        {
            if (All[i].Matches(stats))
            {
                All[i].ApplyTo(stats);
                return;
            }
        }

        stats.health = stats.maxHealth;
    }
}
