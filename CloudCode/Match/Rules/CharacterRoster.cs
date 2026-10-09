namespace Match.Rules;

public sealed class CharacterDefinition
{
    public string Id { get; }
    public int MaxHealth { get; }
    public int Damage { get; }
    public MovementPattern Move { get; }
    public AttackPattern Attack { get; }
    public AttackPattern UtilityPattern { get; }
    public global::AttackAbility SourceAttack { get; }
    public global::UtilityAbility SourceUtility { get; }

    public bool HasUtility => SourceUtility != null && SourceUtility.HasEffect;

    public CharacterDefinition(
        string id,
        int maxHealth,
        int damage,
        MovementPattern move,
        AttackPattern attack,
        AttackPattern utilityPattern,
        global::AttackAbility sourceAttack,
        global::UtilityAbility sourceUtility)
    {
        Id = id;
        MaxHealth = maxHealth;
        Damage = damage;
        Move = move;
        Attack = attack;
        UtilityPattern = utilityPattern;
        SourceAttack = sourceAttack;
        SourceUtility = sourceUtility;
    }
}

/// <summary>
/// Server view of <see cref="UnitConstants"/>. Each entry is one of those instances.
/// </summary>
public static class CharacterRoster
{
    public const string DummyId = "DummyTarget";

    public static readonly IReadOnlyList<string> PlayableIds = UnitConstants.PlayableAssetNames;

    static readonly Dictionary<string, CharacterDefinition> ById = Build();

    static Dictionary<string, CharacterDefinition> Build()
    {
        var map = new Dictionary<string, CharacterDefinition>(StringComparer.Ordinal);
        string[] ids = UnitConstants.PlayableAssetNames;
        for (int i = 0; i < ids.Length; i++)
        {
            if (!TryFind(ids[i], out UnitIdentity identity))
                continue;
            map[identity.AssetName] = From(identity);
        }

        return map;
    }

    static bool TryFind(string assetName, out UnitIdentity identity)
    {
        UnitIdentity[] all = UnitConstants.All;
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i].AssetName != assetName)
                continue;
            identity = all[i];
            return true;
        }

        identity = default;
        return false;
    }

    static CharacterDefinition From(UnitIdentity identity)
    {
        return new CharacterDefinition(
            identity.AssetName,
            identity.MaxHealth,
            identity.Damage,
            CopyMove(identity.Move),
            CopyAttack(identity.Attack != null ? identity.Attack.Pattern : null),
            CopyAttack(identity.Utility != null ? identity.Utility.pattern : null),
            identity.Attack,
            identity.Utility);
    }

    static MovementPattern CopyMove(global::MovementPattern source)
    {
        if (source == null || source.cells == null)
            return null;
        var cells = new bool[source.cells.Length];
        Array.Copy(source.cells, cells, cells.Length);
        return new MovementPattern(cells);
    }

    static AttackPattern CopyAttack(global::AttackPattern source)
    {
        if (source == null || source.cells == null || source.width <= 0 || source.height <= 0)
            return null;

        var grid = new byte[source.height][][];
        for (int row = 0; row < source.height; row++)
        {
            grid[row] = new byte[source.width][];
            for (int col = 0; col < source.width; col++)
            {
                global::AttackPatternCell cell = source.cells[row * source.width + col];
                if (cell == null || cell.identifiers == null || cell.identifiers.Count == 0)
                    grid[row][col] = Array.Empty<byte>();
                else
                    grid[row][col] = cell.identifiers.ToArray();
            }
        }

        return AttackPattern.FromGrid(grid);
    }

    public static bool IsPlayable(string id)
    {
        return !string.IsNullOrEmpty(id) && ById.ContainsKey(id);
    }

    public static bool TryGet(string id, out CharacterDefinition definition)
    {
        definition = null;
        if (string.IsNullOrEmpty(id))
            return false;
        return ById.TryGetValue(id, out definition);
    }

    public static MovementPattern MoveFor(string characterId, bool useSpeedsterMove)
    {
        if (!TryGet(characterId, out CharacterDefinition def))
            return MovementPattern.Empty();
        if (useSpeedsterMove && def.SourceUtility != null)
        {
            var granted = new AbilitySubject();
            def.SourceUtility.Apply(granted, granted);
            if (granted.Move != null)
                return CopyMove(granted.Move);
        }

        return def.Move ?? MovementPattern.Empty();
    }

    public static AttackPattern AttackFor(string characterId)
    {
        return TryGet(characterId, out CharacterDefinition def) ? def.Attack : AttackPattern.Empty();
    }

    public static CharacterDefinition Require(string id)
    {
        if (!TryGet(id, out CharacterDefinition def))
            throw new MatchException("Unknown character.");
        return def;
    }
}
