using UnityEngine;

/// <summary>
/// Deck and in-game sprites for one character.
/// Each lives in its own PNG under Assets/Resources/Characters.
/// </summary>
public static class CharacterAppearance
{
    public static Sprite Deck(UnitStats stats)
    {
        return Load(stats, "Characters/Deck/");
    }

    public static Sprite InGame(UnitStats stats)
    {
        return Load(stats, "Characters/InGame/");
    }

    static Sprite Load(UnitStats stats, string folder)
    {
        string assetName = stats != null ? stats.name : null;
        if (string.IsNullOrEmpty(assetName) && stats != null)
            assetName = stats.displayName;
        if (string.IsNullOrEmpty(assetName))
        {
            Debug.LogWarning("Character appearance is missing a name.");
            return PlaceholderSprite.WhiteSquare();
        }

        Sprite sprite = Resources.Load<Sprite>(folder + assetName);
        if (sprite != null)
            return sprite;

        Debug.LogWarning($"Character appearance PNG is missing at {folder}{assetName}.");
        return PlaceholderSprite.WhiteSquare();
    }
}
