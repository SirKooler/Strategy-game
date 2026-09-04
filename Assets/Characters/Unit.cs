using UnityEngine;

/// <summary>
/// Board piece. Placeholder body and plan arrow live here.
/// Game rules and the current order live on <see cref="behaviour"/>.
/// </summary>
public class Unit : MonoBehaviour
{
    public UnitBehaviour behaviour;

    OrderArrow _arrow;

    public bool IsAlive => behaviour != null && behaviour.IsAlive;

    /// <summary>
    /// Wires <paramref name="behaviour"/>, then applies placeholder art and a click collider.
    /// Call after the behaviour has been set up.
    /// </summary>
    public void Setup(UnitBehaviour behaviour, Sprite sprite)
    {
        this.behaviour = behaviour;
        name = behaviour.CurrentStats.displayName;

        var body = gameObject.AddComponent<SpriteRenderer>();
        body.sprite = sprite;
        body.sortingOrder = 2;
        body.color = behaviour.CurrentStats.placeholderColor;

        var arrowGo = new GameObject($"{name} Arrow");
        arrowGo.transform.SetParent(transform, false);
        _arrow = arrowGo.AddComponent<OrderArrow>();

        var collider = gameObject.AddComponent<BoxCollider2D>();
        collider.size = Vector2.one;
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
