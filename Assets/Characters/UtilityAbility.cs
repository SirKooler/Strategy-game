using System;

/// <summary>
/// One attack: where it reaches, how much damage it deals, and an optional extra effect.
/// </summary>
[Serializable]
public class AttackAbility
{
    public AttackPattern Pattern;
    public int Damage;
    public AbilityEffect Effect;

    /// <summary>
    /// Deals <see cref="Damage"/> to <paramref name="target"/>, then runs <see cref="Effect"/> if there is one.
    /// </summary>
    public void Apply(AbilitySubject activator, AbilitySubject target)
    {
        target.Health -= activator.Damage;
        if (target.Health < 0)
            target.Health = 0;
        Effect?.Invoke(activator, target);
    }
}

/// <summary>
/// One utility: where it reaches, and an optional effect. Utilities do not deal attack damage.
/// </summary>
[Serializable]
public class UtilityAbility
{
    public AttackPattern pattern;
    public AbilityEffect Effect;

    public bool HasEffect => Effect != null && pattern != null;

    public UtilityAbility(AttackPattern pattern, AbilityEffect effect)
    {
        this.pattern = pattern;
        Effect = effect;
    }

    public void Apply(AbilitySubject activator, AbilitySubject target)
    {
        Effect?.Invoke(activator, target);
    }

    /// <summary>Heal <paramref name="healAmount"/> on a plus of adjacent tiles (stand cell empty).</summary>
    public static UtilityAbility HealPlus(int healAmount)
    {
        return Ability.BuildUtility(
            AttackPattern.Plus(1, true),
            (activator, target) => target.Heal(healAmount));
    }
}

public static class Ability
{
    public static AttackAbility BuildAttack(AttackPattern pattern, int damage, AbilityEffect effect = null)
    {
        return new AttackAbility
        {
            Pattern = pattern,
            Damage = damage,
            Effect = effect
        };
    }

    public static UtilityAbility BuildUtility(AttackPattern pattern, AbilityEffect effect = null)
    {
        return new UtilityAbility(pattern, effect);
    }
}
