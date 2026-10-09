#if UNITY_5_3_OR_NEWER
using UnityEngine;
#endif

/// <summary>
/// Authoring numbers for one placeholder unit. Not locked combat math.
/// </summary>
public readonly struct UnitIdentity
{
    public readonly string AssetName;
    public readonly string DisplayName;
    public readonly int MaxHealth;
    public readonly MovementPattern Move;
    public readonly AttackAbility Attack;
    public readonly UtilityAbility Utility;

    public int Damage => Attack != null ? Attack.Damage : 0;

    public UnitIdentity(
        string assetName,
        string displayName,
        int maxHealth,
        MovementPattern move,
        AttackAbility attack,
        UtilityAbility utility = null)
    {
        AssetName = assetName;
        DisplayName = displayName;
        MaxHealth = maxHealth;
        Move = move;
        Attack = attack;
        Utility = utility;
    }

#if UNITY_5_3_OR_NEWER
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
        stats.movePattern = Move;
        stats.attackPattern = Attack != null ? Attack.Pattern : AttackPattern.Empty();
        stats.attack = Attack;
        stats.utility = Utility;
    }
#endif
}

/// <summary>
/// All placeholder unit identities.
/// </summary>
public static class UnitConstants
{
    public static readonly UnitIdentity Tank = new UnitIdentity(
        "Tank",
        "Tank",
        2000,
        MovementPattern.Plus(1),
        Ability.BuildAttack(AttackPattern.X(2), 100));

    public static readonly UnitIdentity Ranger = new UnitIdentity(
        "Ranger",
        "Ranger",
        800,
        MovementPattern.Plus(1),
        Ability.BuildAttack(AttackPattern.FromGrid(new[]
        {
            new[] { new byte[] { 0 }, new byte[] { 0 }, new byte[] { 0 }, new byte[] { }, new byte[] { }, new byte[] { }, new byte[] { 1 }, new byte[] { 1 }, new byte[] { 1 } },
            new[] { new byte[] { 0 }, new byte[] { 0 }, new byte[] { 0 }, new byte[] { }, new byte[] { }, new byte[] { }, new byte[] { 1 }, new byte[] { 1 }, new byte[] { 1 } },
            new[] { new byte[] { 0 }, new byte[] { 0 }, new byte[] { 0 }, new byte[] { }, new byte[] { }, new byte[] { }, new byte[] { 1 }, new byte[] { 1 }, new byte[] { 1 } },
            new[] { new byte[] { }, new byte[] { }, new byte[] { }, new byte[] { 0 }, new byte[] { }, new byte[] { 1 }, new byte[] { }, new byte[] { }, new byte[] { } },
            new[] { new byte[] { }, new byte[] { }, new byte[] { }, new byte[] { }, new byte[] { }, new byte[] { }, new byte[] { }, new byte[] { }, new byte[] { } },
            new[] { new byte[] { }, new byte[] { }, new byte[] { }, new byte[] { 2 }, new byte[] { }, new byte[] { 3 }, new byte[] { }, new byte[] { }, new byte[] { } },
            new[] { new byte[] { 2 }, new byte[] { 2 }, new byte[] { 2 }, new byte[] { }, new byte[] { }, new byte[] { }, new byte[] { 3 }, new byte[] { 3 }, new byte[] { 3 } },
            new[] { new byte[] { 2 }, new byte[] { 2 }, new byte[] { 2 }, new byte[] { }, new byte[] { }, new byte[] { }, new byte[] { 3 }, new byte[] { 3 }, new byte[] { 3 } },
            new[] { new byte[] { 2 }, new byte[] { 2 }, new byte[] { 2 }, new byte[] { }, new byte[] { }, new byte[] { }, new byte[] { 3 }, new byte[] { 3 }, new byte[] { 3 } },
        }), 200));

    public static readonly UnitIdentity Octopus = new UnitIdentity(
        "Octopus",
        "Octopus",
        1300,
        MovementPattern.Plus(1),
        Ability.BuildAttack(AttackPattern.Octopus(2, true), 300));
    
    public static readonly UnitIdentity Sniper = new UnitIdentity(
        "Sniper",
        "Sniper",
        900,
        MovementPattern.Plus(1),
        Ability.BuildAttack(AttackPattern.Plus(4), 200));
    
    public static readonly UnitIdentity SwordMan = new UnitIdentity(
        "SwordMan",
        "SwordMan",
        1800,
        MovementPattern.FromGrid(new[]
        {
            new[] { false, true, false },
            new[] { false, true, false },
            new[] { true, false, true },
            new[] { false, true, false },
            new[] { false, false, false },
        }),
        Ability.BuildAttack(AttackPattern.FromGrid(new[]
        {
            new[] { new byte[] { 0, 1 }, new byte[] { 0 }, new byte[] { 0, 2 } },
            new[] { new byte[] { 1 }, new byte[] { }, new byte[] { 2 } },
            new[] { new byte[] { 1, 3 }, new byte[] { 3 }, new byte[] { 2, 3 } },
        }), 350),
        Ability.BuildUtility(AttackPattern.Square(1, false), (activator, target) =>
        {
            target.Damage += 100;
            target.LoseHealthDownTo(200, 1);
        }));

    public static readonly UnitIdentity Healer = new UnitIdentity(
        "Healer",
        "Healer",
        1000,
        MovementPattern.Plus(1),
        Ability.BuildAttack(AttackPattern.Square(1), 300),
        Ability.BuildUtility(AttackPattern.Square(1, true, true), (activator, target) => target.Heal(100)));

    public static readonly UnitIdentity Speedster = new UnitIdentity(
        "Speedster",
        "Speedster",
        800,
        MovementPattern.X(2),
        Ability.BuildAttack(AttackPattern.Plus(1), 350),
        Ability.BuildUtility(AttackPattern.Plus(1, false), (activator, target) =>
        {
            target.UseAlternateMove = true;
            target.Move = MovementPattern.FromGrid(new[]
            {
                new[] { false, true, false },
                new[] { false, true, false },
                new[] { true, false, true },
                new[] { false, true, false },
                new[] { false, true, false },
            });
        }));

    public static readonly UnitIdentity Laser = new UnitIdentity(
        "Laser",
        "Laser",
        600,
        MovementPattern.FromGrid(new[]
        {
            new[] { true, false, true },
        }),
        Ability.BuildAttack(AttackPattern.Plus(10), 500));

    public static readonly UnitIdentity Controller = new UnitIdentity(
        "Controller",
        "Controller",
        500,
        MovementPattern.X(1),
        Ability.BuildAttack(AttackPattern.FromGrid(new[]
        {
            new[] { new byte[] { }, new byte[] { }, new byte[] { 0 }, new byte[] { 0 }, new byte[] { 0 }, new byte[] { }, new byte[] { }},
            new[] { new byte[] { }, new byte[] { }, new byte[] { 0 }, new byte[] { 0 }, new byte[] { 0 }, new byte[] { }, new byte[] { }},
            new[] { new byte[] { 2 }, new byte[] { 2 }, new byte[] { 0, 2 }, new byte[] { 0 }, new byte[] { 0, 3 }, new byte[] { 3 	}, new byte[] {3  }},
            new[] { new byte[] { 2 }, new byte[] { 2 }, new byte[] { 2 }, new byte[] {  }, new byte[] { 3 }, new byte[] { 3 }, new byte[] { 3 }},
            new[] { new byte[] { 2 }, new byte[] { 2 }, new byte[] { 1, 2 }, new byte[] { 1 }, new byte[] { 1, 3 }, new byte[] { 3 }, new byte[] { 3 }},
            new[] { new byte[] { }, new byte[] { }, new byte[] { 1 }, new byte[] { 1 }, new byte[] { 1 }, new byte[] { }, new byte[] { }},
            new[] { new byte[] { }, new byte[] { }, new byte[] { 1 }, new byte[] { 1 }, new byte[] { 1 }, new byte[] { }, new byte[] { }},
        }), 100));
    
    public static readonly UnitIdentity Dummy = new UnitIdentity(
        "DummyTarget",
        "Dummy target (placeholder)",
        10000,
        MovementPattern.Empty(),
        Ability.BuildAttack(AttackPattern.Empty(), 0));

    public static readonly UnitIdentity[] All = { Tank, Ranger, Octopus, Sniper, SwordMan, Healer, Speedster, Laser, Controller, Dummy };

    public static readonly string[] PlayableAssetNames =
    {
        "Tank", "Ranger", "Octopus", "Sniper", "SwordMan", "Healer", "Speedster", "Laser", "Controller"
    };

#if UNITY_5_3_OR_NEWER
    /// <summary>
    /// Runtime stats for a character type. Health and damage come from the type, not from a deck asset.
    /// </summary>
    public static bool TryCreate(UnitStats definition, out UnitStats stats)
    {
        stats = null;
        if (definition == null)
            return false;

        for (int i = 0; i < All.Length; i++)
        {
            if (!All[i].Matches(definition))
                continue;
            stats = ScriptableObject.CreateInstance<UnitStats>();
            stats.name = All[i].AssetName;
            All[i].ApplyTo(stats);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Runtime stats for a character type. Health and damage come from the type, not from a deck asset.
    /// </summary>
    public static bool TryCreate(string characterId, out UnitStats stats)
    {
        stats = null;
        if (string.IsNullOrEmpty(characterId))
            return false;

        for (int i = 0; i < All.Length; i++)
        {
            if (All[i].AssetName != characterId)
                continue;
            stats = ScriptableObject.CreateInstance<UnitStats>();
            stats.name = All[i].AssetName;
            All[i].ApplyTo(stats);
            return true;
        }

        return false;
    }

    public static bool TryGetPlayableId(UnitStats definition, out string assetName)
    {
        assetName = null;
        if (definition == null)
            return false;
        for (int i = 0; i < PlayableAssetNames.Length; i++)
        {
            if (!All[i].Matches(definition))
                continue;
            assetName = All[i].AssetName;
            return true;
        }

        return false;
    }

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
#endif
}
