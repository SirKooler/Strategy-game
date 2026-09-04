using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Runtime placeholder grid. Size is not locked; change columns and rows in the Inspector.
/// Cells are rebuilt on enable and are not saved in the scene.
/// </summary>
[ExecuteAlways]
public class PlaceholderBoard : MonoBehaviour
{
    [SerializeField] int columns = 6;
    [SerializeField] int rows = 6;
    [SerializeField] float cellSize = 1f;
    [SerializeField] float gap = 0.06f;

    [Header("Attack overlay (Min + (Max - Min) * 2 * arctan(Factor * damage) / π)")]
    [FormerlySerializedAs("A")]
    [SerializeField] float maxOpacity = 0.6f;
    [FormerlySerializedAs("B")]
    [SerializeField] float minOpacity = 0.4f;
    [FormerlySerializedAs("C")]
    [SerializeField] float factor = 1f;

    Sprite _cellSprite;
    BoardCell[,] _cells;

    public float CellSize => cellSize;
    public int Columns => columns;
    public int Rows => rows;
    public int Size => Mathf.Max(Columns, Rows);

    /// <summary>World width and height of the built grid, including gaps.</summary>
    public Vector2 WorldSize
    {
        get
        {
            float width = columns * cellSize + Mathf.Max(0, columns - 1) * gap;
            float height = rows * cellSize + Mathf.Max(0, rows - 1) * gap;
            return new Vector2(width, height);
        }
    }

    void OnEnable()
    {
        Rebuild();
    }

    void OnDisable()
    {
        ClearCells();
    }

    void OnValidate()
    {
        columns = Mathf.Clamp(columns, 1, 32);
        rows = Mathf.Clamp(rows, 1, 32);
        cellSize = Mathf.Max(0.1f, cellSize);
        gap = Mathf.Max(0f, gap);
        maxOpacity = Mathf.Clamp01(maxOpacity);
        minOpacity = Mathf.Clamp01(minOpacity);
        RefreshAttackOverlays();
    }

    /// <summary>Destroys existing cells and builds a centered grid of one placeholder color.</summary>
    [ContextMenu("Rebuild Board")]
    public void Rebuild()
    {
        ClearCells();
        EnsureSprite();
        _cells = new BoardCell[columns, rows];

        float step = cellSize + gap;
        float originX = -(columns - 1) * step * 0.5f;
        float originY = -(rows - 1) * step * 0.5f;
        Color tile = new Color(0.72f, 0.74f, 0.78f, 1f);

        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < columns; x++)
            {
                var go = new GameObject($"Cell {x},{y}");
                go.hideFlags = HideFlags.DontSave;
                go.transform.SetParent(transform, false);
                go.transform.localPosition = new Vector3(originX + x * step, originY + y * step, 0f);
                go.transform.localScale = new Vector3(cellSize, cellSize, 1f);

                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = _cellSprite;

                var collider = go.AddComponent<BoxCollider2D>();
                collider.size = Vector2.one;

                var cell = go.AddComponent<BoardCell>();
                cell.Setup(new Position(x, y), tile, this);
                _cells[x, y] = cell;
            }
        }
    }

    public bool InBounds(Position position)
    {
        return position != null && InBounds(position.Column, position.Row);
    }

    bool InBounds(int column, int row)
    {
        return column >= 0 && row >= 0 && column < columns && row < rows;
    }

    /// <summary>Returns the cell at the grid index, or null if the index is off the board.</summary>
    public BoardCell GetCell(Position position)
    {
        if (position == null)
            return null;
        return GetCell(position.Column, position.Row);
    }

    public BoardCell GetCell(int column, int row)
    {
        if (_cells == null || !InBounds(column, row))
            return null;
        return _cells[column, row];
    }

    /// <summary>Finds a cell under a world point using 2D physics.</summary>
    public bool TryGetCellAtWorld(Vector3 world, out BoardCell cell)
    {
        cell = null;
        Collider2D[] hits = Physics2D.OverlapPointAll(world);
        for (int i = 0; i < hits.Length; i++)
        {
            cell = hits[i].GetComponent<BoardCell>();
            if (cell != null)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Overlay alpha = Min + (Max - Min) * 2 * arctan(Factor * damage) / π, then clamped to [MinOpacity, MaxOpacity].
    /// </summary>
    public float AttackOverlayAlpha(int damage)
    {
        float lo = Mathf.Min(minOpacity, maxOpacity);
        float hi = Mathf.Max(minOpacity, maxOpacity);

        if (damage < 1)
            return 0f;

        float t = 2f * Mathf.Atan(factor * damage) / Mathf.PI;
        float opacity = minOpacity + (maxOpacity - minOpacity) * t;
        return Mathf.Clamp(opacity, lo, hi);
    }

    void RefreshAttackOverlays()
    {
        if (_cells == null)
            return;
        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < columns; x++)
            {
                if (_cells[x, y] != null)
                    _cells[x, y].RefreshAttackOverlay();
            }
        }
    }

    public void ClearMarks()
    {
        if (_cells == null)
            return;

        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < columns; x++)
            {
                if (_cells[x, y] != null)
                    _cells[x, y].SetMark(CellMark.None);
            }
        }
    }

    void ClearCells()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            GameObject child = transform.GetChild(i).gameObject;
            if (Application.isPlaying)
                Destroy(child);
            else
                DestroyImmediate(child);
        }

        _cells = null;
    }

    void EnsureSprite()
    {
        _cellSprite = PlaceholderSprite.WhiteSquare();
    }
}
