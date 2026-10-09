using System;
using UnityEngine;

/// <summary>
/// Data for one unit type. Create assets from Create &gt; Strategy &gt; Unit Stats.
/// Values here are starting numbers, not locked combat math.
/// Lives under Assets/Characters so roster data stays with character assets.
/// </summary>
[CreateAssetMenu(menuName = "Strategy/Unit Stats", fileName = "UnitStats")]
public class UnitStats : ScriptableObject
{
    /// <summary>Name shown on the HUD and spawned GameObject.</summary>
    public string displayName;

    /// <summary>Sheet text. Copied onto <see cref="UnitBehaviour"/> when the unit spawns.</summary>
    public UnitBehaviour.Descriptions descriptions = new UnitBehaviour.Descriptions();

    /// <summary>Runtime only. Starting value comes from <see cref="UnitConstants"/>.</summary>
    [NonSerialized] public int maxHealth = 10;

    /// <summary>Runtime only. Starts at <see cref="maxHealth"/> and changes during a match.</summary>
    [NonSerialized] public int health = 10;

    /// <summary>Runtime only. Starting value comes from <see cref="UnitConstants"/>.</summary>
    [NonSerialized] public int damage = 1;

    public MovementPattern movePattern = MovementPattern.Plus(1);
    public AttackPattern attackPattern = AttackPattern.Plus(1);
    public AttackAbility attack;

    /// <summary>Ally ability, or null when this unit has none.</summary>
    public UtilityAbility utility;

    public bool HasUtility => utility != null && utility.HasEffect;
}
