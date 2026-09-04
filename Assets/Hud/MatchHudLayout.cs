using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Optionally places the HUD next to the board. Panel sizes stay on the RectTransforms
/// unless <see cref="matchBoardSize"/> is on.
/// </summary>
[ExecuteAlways]
public class MatchHudLayout : MonoBehaviour
{
    const float PixelsPerUnit = 100f;

    [SerializeField] PlaceholderBoard board;
    [SerializeField] RectTransform phasePanel;
    [SerializeField] RectTransform actionPanel;
    [SerializeField] RectTransform topHud;
    [SerializeField] float gap = 0.08f;

    [SerializeField] bool placeBesideBoard = true;
    [SerializeField] bool matchBoardSize;

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
        if (board == null || phasePanel == null || actionPanel == null)
            return;

        Canvas canvas = GetComponent<Canvas>();
        if (canvas == null)
            return;
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = Camera.main;

        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
        if (scaler != null)
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;

        Vector2 boardSize = board.WorldSize;
        Vector3 center = board.transform.position;

        if (matchBoardSize)
        {
            SizeToBoard(phasePanel, boardSize);
            SizeToBoard(actionPanel, boardSize);
        }

        if (placeBesideBoard)
        {
            Vector2 phaseSize = PanelWorldSize(phasePanel);
            Vector2 actionSize = PanelWorldSize(actionPanel);
            phasePanel.position = center + new Vector3(-(boardSize.x * 0.5f + phaseSize.x * 0.5f + gap), 0f, 0f);
            actionPanel.position = center + new Vector3(0f, -(boardSize.y * 0.5f + actionSize.y * 0.5f + gap), 0f);
            if (topHud != null)
            {
                Vector2 topSize = PanelWorldSize(topHud);
                topHud.position = center + new Vector3(0f, boardSize.y * 0.5f + topSize.y * 0.5f + gap, 0f);
            }
        }
    }

    static Vector2 PanelWorldSize(RectTransform panel)
    {
        Rect rect = panel.rect;
        Vector3 scale = panel.lossyScale;
        return new Vector2(Mathf.Abs(rect.width * scale.x), Mathf.Abs(rect.height * scale.y));
    }

    static void SizeToBoard(RectTransform panel, Vector2 worldSize)
    {
        panel.anchorMin = new Vector2(0.5f, 0.5f);
        panel.anchorMax = new Vector2(0.5f, 0.5f);
        panel.pivot = new Vector2(0.5f, 0.5f);
        panel.sizeDelta = worldSize * PixelsPerUnit;
        panel.localScale = new Vector3(1f / PixelsPerUnit, 1f / PixelsPerUnit, 1f);
    }
}
