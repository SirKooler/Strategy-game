using UnityEngine;

/// <summary>
/// One-pixel white sprite used by placeholder board cells and units.
/// </summary>
public static class PlaceholderSprite
{
    static Sprite _square;
    static Sprite _cross;

    public static Sprite WhiteSquare()
    {
        if (_square != null)
            return _square;

        var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        tex.filterMode = FilterMode.Point;
        _square = Sprite.Create(tex, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        return _square;
    }

    /// <summary>Placeholder X for tiles that are in a pattern but cannot be clicked.</summary>
    public static Sprite Cross()
    {
        if (_cross != null)
            return _cross;

        const int size = 32;
        const int thickness = 3;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        var clear = new Color(1f, 1f, 1f, 0f);
        var ink = Color.white;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                bool main = Mathf.Abs(x - y) <= thickness;
                bool anti = Mathf.Abs(x + y - (size - 1)) <= thickness;
                tex.SetPixel(x, y, main || anti ? ink : clear);
            }
        }

        tex.Apply();
        _cross = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        return _cross;
    }
}
