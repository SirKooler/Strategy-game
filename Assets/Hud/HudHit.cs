using UnityEngine;

/// <summary>
/// Shared pointer-vs-HUD tests for world-space canvas panels.
/// </summary>
public static class HudHit
{
    public static bool Contains(RectTransform rect, Vector2 screenPosition)
    {
        if (rect == null || !rect.gameObject.activeInHierarchy)
            return false;
        return RectTransformUtility.RectangleContainsScreenPoint(rect, screenPosition, EventCamera(rect));
    }

    public static Camera EventCamera(Transform from)
    {
        Canvas canvas = from != null ? from.GetComponentInParent<Canvas>() : null;
        if (canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            return null;
        return canvas.worldCamera != null ? canvas.worldCamera : Camera.main;
    }
}
