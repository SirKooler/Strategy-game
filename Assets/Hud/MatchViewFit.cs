using UnityEngine;

/// <summary>
/// Uniformly scales this root so the board and HUD fit the camera view.
/// Camera and background stay put.
/// </summary>
[ExecuteAlways]
[DefaultExecutionOrder(50)]
public class MatchViewFit : MonoBehaviour
{
    public const string ObjectName = "MatchView";

    [SerializeField] Camera targetCamera;
    [SerializeField] [Range(0f, 0.25f)] float margin = 0.04f;

    void OnEnable()
    {
        Apply();
    }

    void OnValidate()
    {
        Apply();
    }

    void LateUpdate()
    {
        Apply();
    }

    public void Apply()
    {
        Camera cam = targetCamera != null ? targetCamera : Camera.main;
        if (cam == null || !cam.orthographic)
            return;

        MatchHudLayout layout = GetComponentInChildren<MatchHudLayout>();
        if (layout == null)
            return;

        layout.Apply();
        if (!layout.TryGetFitBounds(out Rect bounds))
            return;

        int pixelHeight = Mathf.Max(1, cam.pixelHeight);
        float aspect = (float)Mathf.Max(1, cam.pixelWidth) / pixelHeight;
        float viewH = cam.orthographicSize * 2f;
        float viewW = viewH * aspect;

        float halfW = Mathf.Max(Mathf.Abs(bounds.xMin), Mathf.Abs(bounds.xMax));
        float halfH = Mathf.Max(Mathf.Abs(bounds.yMin), Mathf.Abs(bounds.yMax));
        if (halfW < 0.0001f || halfH < 0.0001f)
            return;

        float pad = 1f - Mathf.Clamp01(margin);
        float scaleX = (viewW * 0.5f * pad) / halfW;
        float scaleY = (viewH * 0.5f * pad) / halfH;
        float scale = Mathf.Min(scaleX, scaleY);
        if (scale < 0.0001f)
            return;

        transform.localScale = new Vector3(scale, scale, 1f);
    }
}
