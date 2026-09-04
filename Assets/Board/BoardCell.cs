using UnityEngine;

/// <summary>
/// Highlight drawn on a board cell during plan phase.
/// Move is blue. Attack uses a transparent overlay whose opacity follows combined damage.
/// Utility uses a placeholder sparkle overlay. Tiles do not scale.
/// </summary>
public enum CellMark
{
    None,
    Selected,
    Move
}

/// <summary>
/// One tile on <see cref="PlaceholderBoard"/>.
/// Stores its <see cref="Position"/>, who stands here, and the current highlight.
/// </summary>
public class BoardCell : MonoBehaviour
{
    const int OverlaySortingOrder = 1;
    const int BlockedMarkSortingOrder = 3;

    static readonly Color AttackOverlayColor = new Color(0.88f, 0.22f, 0.22f, 1f);
    static readonly Color UtilityOverlayColor = new Color(1f, 1f, 1f, 1f);
    static readonly Color BlockedMarkColor = new Color(0.12f, 0.04f, 0.04f, 0.95f);
    static readonly int SparkleSeedId = Shader.PropertyToID("_Seed");
    static Material _overlayMaterial;
    static Material _sparkleMaterial;

    public Position Position;

    /// <summary>Unit standing on this cell, or null if empty.</summary>
    public Unit Occupant;

    SpriteRenderer _renderer;
    SpriteRenderer _attackOverlay;
    SpriteRenderer _utilityOverlay;
    SpriteRenderer _blockedMark;
    MaterialPropertyBlock _sparkleBlock;
    Color _baseColor;
    CellMark _mark;
    int _attackDamage;
    bool _utilityCovered;
    bool _blockedAttack;
    PlaceholderBoard _board;

    /// <summary>Called by the board when this cell is created or rebuilt.</summary>
    public void Setup(Position position, Color color, PlaceholderBoard board)
    {
        Position = position.Copy();
        _baseColor = color;
        _board = board;
        Occupant = null;
        _mark = CellMark.None;
        _attackDamage = 0;
        _utilityCovered = false;
        _blockedAttack = false;
        name = $"Cell {Position}";
        _renderer = GetComponent<SpriteRenderer>();
        EnsureOverlay();
        EnsureUtilityOverlay();
        EnsureBlockedMark();
        ApplyColor();
        ApplyAttackOverlay();
        ApplyUtilityOverlay();
        ApplyBlockedMark();
    }

    public void SetMark(CellMark mark)
    {
        _mark = mark;
        if (mark == CellMark.None)
        {
            _attackDamage = 0;
            _utilityCovered = false;
            _blockedAttack = false;
        }

        ApplyColor();
        ApplyAttackOverlay();
        ApplyUtilityOverlay();
        ApplyBlockedMark();
    }

    /// <summary>
    /// Same attack overlay as a clickable tile, plus an X when the cell cannot be chosen.
    /// </summary>
    public void SetBlockedAttack(bool blocked)
    {
        _blockedAttack = blocked;
        ApplyBlockedMark();
    }

    /// <summary>Adds planned or preview attack damage on this tile, then refreshes the overlay.</summary>
    public void AddAttackDamage(int damage)
    {
        if (damage <= 0)
            return;
        _attackDamage += damage;
        ApplyAttackOverlay();
    }

    public void RefreshAttackOverlay()
    {
        ApplyAttackOverlay();
    }

    /// <summary>Tiny sparkles on a utility-covered tile. The tile does not scale.</summary>
    public void SetUtilityCovered(bool covered)
    {
        _utilityCovered = covered;
        ApplyUtilityOverlay();
    }

    void ApplyColor()
    {
        switch (_mark)
        {
            case CellMark.Selected:
                _renderer.color = new Color(0.95f, 0.85f, 0.25f, 1f);
                break;
            case CellMark.Move:
                _renderer.color = new Color(0.35f, 0.55f, 0.95f, 1f);
                break;
            default:
                _renderer.color = _baseColor;
                break;
        }
    }

    void EnsureOverlay()
    {
        if (_attackOverlay != null)
            return;

        var go = new GameObject("AttackOverlay");
        go.hideFlags = HideFlags.DontSave;
        go.transform.SetParent(transform, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localScale = Vector3.one;

        _attackOverlay = go.AddComponent<SpriteRenderer>();
        _attackOverlay.sprite = _renderer != null ? _renderer.sprite : PlaceholderSprite.WhiteSquare();
        _attackOverlay.sharedMaterial = OverlayMaterial();
        _attackOverlay.sortingOrder = OverlaySortingOrder;
        _attackOverlay.enabled = false;
    }

    void EnsureUtilityOverlay()
    {
        if (_utilityOverlay != null)
            return;

        var go = new GameObject("UtilityOverlay");
        go.hideFlags = HideFlags.DontSave;
        go.transform.SetParent(transform, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localScale = Vector3.one;

        _utilityOverlay = go.AddComponent<SpriteRenderer>();
        _utilityOverlay.sprite = _renderer != null ? _renderer.sprite : PlaceholderSprite.WhiteSquare();
        _utilityOverlay.sharedMaterial = SparkleMaterial();
        _utilityOverlay.sortingOrder = OverlaySortingOrder;
        _utilityOverlay.enabled = false;
    }

    void EnsureBlockedMark()
    {
        if (_blockedMark != null)
            return;

        var go = new GameObject("BlockedAttackMark");
        go.hideFlags = HideFlags.DontSave;
        go.transform.SetParent(transform, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localScale = new Vector3(0.72f, 0.72f, 1f);

        _blockedMark = go.AddComponent<SpriteRenderer>();
        _blockedMark.sprite = PlaceholderSprite.Cross();
        _blockedMark.sharedMaterial = OverlayMaterial();
        _blockedMark.sortingOrder = BlockedMarkSortingOrder;
        _blockedMark.color = BlockedMarkColor;
        _blockedMark.enabled = false;
    }

    void ApplyBlockedMark()
    {
        if (_blockedMark == null)
            return;
        _blockedMark.enabled = _blockedAttack;
    }

    void ApplyAttackOverlay()
    {
        if (_attackOverlay == null)
            return;

        float alpha = _board != null ? _board.AttackOverlayAlpha(_attackDamage) : 0f;
        if (alpha <= 0f)
        {
            _attackOverlay.enabled = false;
            return;
        }

        Color color = AttackOverlayColor;
        color.a = alpha;
        _attackOverlay.color = color;
        _attackOverlay.enabled = true;
    }

    void ApplyUtilityOverlay()
    {
        if (_utilityOverlay == null)
            return;

        if (!_utilityCovered)
        {
            _utilityOverlay.enabled = false;
            return;
        }

        if (_sparkleBlock == null)
            _sparkleBlock = new MaterialPropertyBlock();
        _utilityOverlay.GetPropertyBlock(_sparkleBlock);
        _sparkleBlock.SetFloat(SparkleSeedId, Position.Column * 13.37f + Position.Row * 7.91f);
        _utilityOverlay.SetPropertyBlock(_sparkleBlock);
        _utilityOverlay.color = UtilityOverlayColor;
        _utilityOverlay.enabled = true;
    }

    static Material OverlayMaterial()
    {
        if (_overlayMaterial != null)
            return _overlayMaterial;

        Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");
        _overlayMaterial = shader != null ? new Material(shader) : null;
        if (_overlayMaterial != null)
            _overlayMaterial.name = "AttackOverlay-Unlit";
        return _overlayMaterial;
    }

    static Material SparkleMaterial()
    {
        if (_sparkleMaterial != null)
            return _sparkleMaterial;

        Shader shader = Shader.Find("Strategy/UtilitySparkle");
        if (shader == null)
            return OverlayMaterial();

        _sparkleMaterial = new Material(shader);
        _sparkleMaterial.name = "UtilitySparkle-Unlit";
        return _sparkleMaterial;
    }
}
