using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Small grid of one move or attack pattern. The center dot is the stand tile.
/// </summary>
public class ActionPatternView : MonoBehaviour
{
    const float Box = 140f;
    const float Gap = 2f;

    static readonly Color EmptyColor = new Color(0.22f, 0.24f, 0.28f, 1f);
    static readonly Color StandColor = new Color(0.82f, 0.84f, 0.88f, 1f);
    static readonly Color MoveColor = new Color(0.3f, 0.55f, 0.95f, 1f);
    static readonly Color StandDot = new Color(0.08f, 0.08f, 0.1f, 1f);
    public static readonly Color AttackColor = new Color(0.86f, 0.22f, 0.18f, 1f);
    public static readonly Color UtilityColor = new Color(0.28f, 0.72f, 0.38f, 1f);

    readonly List<Image> _cells = new List<Image>();
    GridLayoutGroup _grid;

    public void ShowMove(MovementPattern pattern)
    {
        if (pattern == null || !pattern.HasCells)
        {
            Clear();
            return;
        }

        int side = pattern.size;
        int center = side / 2;
        Show(side, side, center, center, (row, column) =>
        {
            if (pattern.Includes(row, column))
                return MoveColor;
            return row == center && column == center ? StandColor : EmptyColor;
        });
    }

    public void ShowAttack(AttackPattern pattern, Color covered)
    {
        if (pattern == null || pattern.cells == null || pattern.width <= 0 || pattern.height <= 0)
        {
            Clear();
            return;
        }

        int centerRow = pattern.height / 2;
        int centerColumn = pattern.width / 2;
        Show(pattern.width, pattern.height, centerRow, centerColumn, (row, column) =>
        {
            if (!pattern.Covers(row, column))
                return row == centerRow && column == centerColumn ? StandColor : EmptyColor;
            return covered;
        });
    }

    public void Clear()
    {
        for (int i = 0; i < _cells.Count; i++)
            _cells[i].gameObject.SetActive(false);
    }

    void Show(int columns, int rows, int centerRow, int centerColumn, System.Func<int, int, Color> colorOf)
    {
        EnsureGrid();
        int count = columns * rows;
        float span = Mathf.Max(columns, rows);
        float cell = (Box - Gap * (span - 1f)) / span;
        cell = Mathf.Max(2f, cell);
        _grid.cellSize = new Vector2(cell, cell);
        _grid.spacing = new Vector2(Gap, Gap);
        _grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        _grid.constraintCount = columns;

        RectTransform gridRect = _grid.transform as RectTransform;
        float width = columns * cell + Gap * (columns - 1);
        float height = rows * cell + Gap * (rows - 1);
        gridRect.sizeDelta = new Vector2(width, height);

        while (_cells.Count < count)
            _cells.Add(CreateCell());

        for (int i = 0; i < _cells.Count; i++)
        {
            bool on = i < count;
            _cells[i].gameObject.SetActive(on);
            if (!on)
                continue;
            int row = i / columns;
            int column = i % columns;
            _cells[i].color = colorOf(row, column);
            SetDot(_cells[i], row == centerRow && column == centerColumn, cell);
        }
    }

    void EnsureGrid()
    {
        if (_grid != null)
            return;
        _grid = GetComponent<GridLayoutGroup>();
        if (_grid == null)
            _grid = gameObject.AddComponent<GridLayoutGroup>();
        _grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
        _grid.startAxis = GridLayoutGroup.Axis.Horizontal;
        _grid.childAlignment = TextAnchor.UpperLeft;
    }

    Image CreateCell()
    {
        var go = new GameObject("Cell", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.layer = 5;
        go.transform.SetParent(transform, false);
        Image image = go.GetComponent<Image>();
        image.sprite = PlaceholderSprite.WhiteSquare();
        image.raycastTarget = false;
        return image;
    }

    static void SetDot(Image cell, bool center, float cellSize)
    {
        Transform dot = cell.transform.Find("Stand");
        if (!center)
        {
            if (dot != null)
                dot.gameObject.SetActive(false);
            return;
        }

        if (dot == null)
        {
            var go = new GameObject("Stand", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.layer = 5;
            go.transform.SetParent(cell.transform, false);
            Image image = go.GetComponent<Image>();
            image.sprite = PlaceholderSprite.WhiteSquare();
            image.color = StandDot;
            image.raycastTarget = false;
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            dot = go.transform;
        }

        dot.gameObject.SetActive(true);
        ((RectTransform)dot).sizeDelta = Vector2.one * cellSize * 0.28f;
    }
}
