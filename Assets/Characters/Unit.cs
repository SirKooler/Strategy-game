using UnityEngine;

/// <summary>
/// Board piece. In-game sprite and plan arrow live here.
/// Game rules and the current order live on <see cref="behaviour"/>.
/// </summary>
public class Unit : MonoBehaviour
{
    public UnitBehaviour behaviour;

    OrderArrow _arrow;
    HealthBar _healthBar;
    bool _friendly;
    Vector3 _boardScale = Vector3.one;

    /// <summary>Scale that fills one cell. Survives hiding the body until the spawn beat.</summary>
    public Vector3 BoardScale => _boardScale;

    public bool IsAlive => behaviour != null && behaviour.IsAlive;

    /// <summary>
    /// Wires <paramref name="behaviour"/>, then applies the in-game sprite and a click collider.
    /// Call after the behaviour has been set up.
    /// </summary>
    public void Setup(UnitBehaviour behaviour, float cellSize, bool friendly, HealthBar healthBarPrefab)
    {
        this.behaviour = behaviour;
        _friendly = friendly;
        name = behaviour.CurrentStats.displayName;

        Sprite sprite = CharacterAppearance.InGame(behaviour.CurrentStats);
        float longest = SpriteLongestSide(sprite);

        var body = gameObject.AddComponent<SpriteRenderer>();
        body.sprite = sprite;
        body.sortingOrder = 2;
        body.color = Color.white;
        _boardScale = new Vector3(cellSize / longest, cellSize / longest, 1f);
        transform.localScale = _boardScale;

        var arrowGo = new GameObject($"{name} Arrow");
        arrowGo.transform.SetParent(transform, false);
        arrowGo.transform.localScale = new Vector3(longest, longest, 1f);
        _arrow = arrowGo.AddComponent<OrderArrow>();

        if (healthBarPrefab != null)
        {
            _healthBar = Instantiate(healthBarPrefab, transform, false);
            KeepInCellSpace(_healthBar.transform, longest);
        }
        RefreshHealth();

        var collider = gameObject.AddComponent<BoxCollider2D>();
        collider.size = sprite != null ? (Vector2)sprite.bounds.size : Vector2.one;
    }

    /// <summary>
    /// Longer side of the sprite in local units. Pixel count changes this, not the cell size.
    /// </summary>
    static float SpriteLongestSide(Sprite sprite)
    {
        if (sprite == null)
            return 1f;
        float longest = Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y);
        return longest > 0f ? longest : 1f;
    }

    /// <summary>
    /// The health bar is authored as if one local unit is one cell.
    /// Root scale absorbs sprite resolution, so scale the bar back into that cell space.
    /// </summary>
    static void KeepInCellSpace(Transform bar, float longest)
    {
        bar.localScale = Vector3.Scale(bar.localScale, new Vector3(longest, longest, 1f));
        bar.localPosition = Vector3.Scale(bar.localPosition, new Vector3(longest, longest, 1f));
    }

    /// <summary>Paints the bar from the current health.</summary>
    public void RefreshHealth()
    {
        if (_healthBar == null || behaviour == null || behaviour.CurrentStats == null)
            return;
        UnitStats stats = behaviour.CurrentStats;
        _healthBar.Show(_friendly, stats.health, stats.maxHealth);
    }

    /// <summary>Moves the piece onto a cell without changing its plan.</summary>
    public void PlaceAt(Position position, Vector3 worldPosition)
    {
        behaviour.SetPosition(position);
        transform.position = worldPosition;
    }

    public void ShowArrow(Vector3 from, Vector3 to, Color color)
    {
        _arrow.Show(from, to, color);
    }

    public void HideArrow()
    {
        _arrow.Hide();
    }
}
