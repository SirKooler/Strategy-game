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

    public int maxHealth = 10;
    public int health = 10;
    public int damage = 1;

    public MovementPattern movePattern = MovementPattern.Plus(1);
    public AttackPattern attackPattern = AttackPattern.Plus(1);

    /// <summary>Ally ability, or null when this unit has none.</summary>
    public UtilityAbility utility;

    /// <summary>Placeholder body color until a visual theme is chosen.</summary>
    public Color placeholderColor = Color.white;

    public bool HasUtility => utility != null && utility.HasEffect;
}
