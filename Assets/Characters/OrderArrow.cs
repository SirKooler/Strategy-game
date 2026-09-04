using UnityEngine;

/// <summary>
/// Placeholder arrow drawn during plan phase for a move order.
/// Built from two LineRenderers so no art asset is required.
/// </summary>
public class OrderArrow : MonoBehaviour
{
    LineRenderer _shaft;
    LineRenderer _head;

    void Awake()
    {
        _shaft = CreateLine("Shaft", 0.07f);
        _head = CreateLine("Head", 0.07f);
        Hide();
    }

    /// <summary>
    /// Draws an arrow from <paramref name="from"/> to <paramref name="to"/>.
    /// Hidden if the two points are almost the same.
    /// </summary>
    public void Show(Vector3 from, Vector3 to, Color color)
    {
        from.z = -0.15f;
        to.z = -0.15f;
        Vector3 delta = to - from;
        if (delta.sqrMagnitude < 0.01f)
        {
            Hide();
            return;
        }

        _shaft.enabled = true;
        _head.enabled = true;

        _shaft.startColor = color;
        _shaft.endColor = color;
        _head.startColor = color;
        _head.endColor = color;
        _shaft.material.color = color;
        _head.material.color = color;

        _shaft.positionCount = 2;
        _shaft.SetPosition(0, from);
        _shaft.SetPosition(1, to);

        Vector3 dir = delta.normalized;
        Vector3 side = new Vector3(-dir.y, dir.x, 0f);
        float headSize = 0.28f;
        Vector3 tip = to;
        Vector3 left = to - dir * headSize + side * (headSize * 0.55f);
        Vector3 right = to - dir * headSize - side * (headSize * 0.55f);

        _head.positionCount = 3;
        _head.SetPosition(0, left);
        _head.SetPosition(1, tip);
        _head.SetPosition(2, right);
    }

    public void Hide()
    {
        _shaft.enabled = false;
        _head.enabled = false;
    }

    LineRenderer CreateLine(string childName, float width)
    {
        var go = new GameObject(childName);
        go.transform.SetParent(transform, false);

        var line = go.AddComponent<LineRenderer>();
        line.material = new Material(Shader.Find("Sprites/Default"));
        line.useWorldSpace = true;
        line.sortingOrder = 12;
        line.textureMode = LineTextureMode.Stretch;
        line.numCapVertices = 2;
        line.widthMultiplier = 1f;
        line.startWidth = width;
        line.endWidth = width;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        return line;
    }
}
