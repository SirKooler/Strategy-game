using UnityEngine;

/// <summary>
/// One corner flag. Yours matches the friendly health bar. The opponent's matches the enemy bar.
/// Sorting order 1 draws it in front of the tile and behind unit bodies.
/// </summary>
public class Flag : MonoBehaviour
{
    public const int SortingOrder = 1;
    public const string LocalSeat = "local";
    public const string OpponentSeat = "opponent";
    public static readonly Color Yours = new Color(64f / 255f, 140f / 255f, 242f / 255f, 1f);
    public static readonly Color Theirs = new Color(217f / 255f, 56f / 255f, 51f / 255f, 1f);

    [SerializeField] SpriteRenderer body;

    public string OwnerId;
    public int Column;
    public int Row;
    public Color Color { get; private set; } = Color.white;

    void Awake()
    {
        if (body != null)
            body.sortingOrder = SortingOrder;
    }

    public void Bind(int column, int row)
    {
        Column = column;
        Row = row;
        SetOwner(null, Color.white);
    }

    public void SetOwner(string ownerId, Color color)
    {
        OwnerId = ownerId;
        Color = color.a <= 0f ? Color.white : color;
        if (body == null)
            return;
        body.sortingOrder = SortingOrder;
        body.color = Color;
    }

    public static Color ForPlayer(string ownerId, string localPlayerId)
    {
        return !string.IsNullOrEmpty(localPlayerId) && ownerId == localPlayerId ? Yours : Theirs;
    }
}
