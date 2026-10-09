/// <summary>
/// The unit an attack or utility is acting on. Effects read and change this.
/// </summary>
public sealed class AbilitySubject
{
    public int Health;
    public int MaxHealth;
    public int Damage;
    public MovementPattern Move;
    public bool UseAlternateMove;

    public void Heal(int amount)
    {
        if (amount <= 0)
            return;
        Health += amount;
        if (Health > MaxHealth)
            Health = MaxHealth;
    }

    public void LoseHealthDownTo(int amount, int minimum)
    {
        if (amount <= 0)
            return;
        Health -= amount;
        if (Health < minimum)
            Health = minimum;
    }

#if UNITY_5_3_OR_NEWER
    public static AbilitySubject Read(Unit unit)
    {
        UnitStats stats = unit.behaviour.CurrentStats;
        return new AbilitySubject
        {
            Health = stats.health,
            MaxHealth = stats.maxHealth,
            Damage = stats.damage,
            Move = stats.movePattern
        };
    }

    public void WriteTo(Unit unit)
    {
        UnitStats stats = unit.behaviour.CurrentStats;
        stats.health = Health;
        stats.damage = Damage;
        if (Move != null)
            stats.movePattern = Move;
        unit.RefreshHealth();
    }
#endif
}

/// <summary>
/// Optional extra behaviour. <paramref name="activator"/> uses the ability. <paramref name="target"/> receives it.
/// </summary>
public delegate void AbilityEffect(AbilitySubject activator, AbilitySubject target);
