using System;

/// <summary>
/// Runs on one ally standing on a covered tile.
/// Assigned in code when a unit is given a utility; null means no effect.
/// </summary>
public delegate void UtilityEffect(Unit target);

/// <summary>
/// Optional ally ability. Null on a unit, or a null <see cref="Effect"/>, means no utility.
/// <see cref="Effect"/> is set in code (not serialized).
/// </summary>
[Serializable]
public class UtilityAbility
{
    public AttackPattern pattern;

    /// <summary>Called once per ally on a covered tile. Null if this unit has no utility effect.</summary>
    public UtilityEffect Effect;

    public bool HasEffect => Effect != null && pattern != null;

    public UtilityAbility(AttackPattern pattern, UtilityEffect effect)
    {
        this.pattern = pattern;
        this.Effect = effect;
    }

    /// <summary>Heal <paramref name="healAmount"/> on a plus of adjacent tiles (stand cell empty).</summary>
    public static UtilityAbility HealPlus(int healAmount)
    {
        return new UtilityAbility(
            AttackPattern.Plus(1, true), 
            target => target.behaviour.ApplyHeal(healAmount));
    }
}
