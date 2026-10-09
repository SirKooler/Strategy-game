using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Places the HUD around the board in unscaled design space. Panel sizes stay on the
/// RectTransforms unless <see cref="matchBoardSize"/> is on.
/// </summary>
[ExecuteAlways]
[DefaultExecutionOrder(-50)]
public class MatchHudLayout : MonoBehaviour
{
    const float PixelsPerUnit = 100f;

    [SerializeField] PlaceholderBoard board;
    [SerializeField] RectTransform phasePanel;
    [SerializeField] RectTransform actionPanel;
    [SerializeField] RectTransform topHud;
    [SerializeField] RectTransform characterMenu;
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

        if (matchBoardSize)
            SizeToBoard(actionPanel, boardSize);

        if (characterMenu == null)
        {
            CharacterMenu menu = GetComponentInChildren<CharacterMenu>(true);
            if (menu != null)
                characterMenu = menu.Rect;
        }

        if (topHud != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(topHud);

        if (placeBesideBoard)
        {
            Vector2 actionSize = PanelDesignSize(actionPanel);
            Vector2 turnSize = PanelDesignSize(phasePanel);
            float belowBoard = -boardSize.y * 0.5f - gap;
            actionPanel.localPosition = new Vector3(0f, belowBoard - actionSize.y * 0.5f, 0f);
            phasePanel.localPosition = new Vector3(
                0f,
                belowBoard - actionSize.y - gap - turnSize.y * 0.5f,
                0f);
            if (topHud != null)
            {
                Vector2 topSize = PanelDesignSize(topHud);
                topHud.localPosition = new Vector3(0f, boardSize.y * 0.5f + topSize.y * 0.5f + gap, 0f);
            }

            if (characterMenu != null && characterMenu.gameObject.activeInHierarchy && topHud != null)
            {
                Vector2 topSize = PanelDesignSize(topHud);
                Vector2 menuSize = PanelDesignSize(characterMenu);
                characterMenu.SetAsLastSibling();
                characterMenu.localPosition = new Vector3(
                    topHud.localPosition.x,
                    topHud.localPosition.y - topSize.y * 0.5f - menuSize.y * 0.5f - gap,
                    0f);
            }
        }
    }

    /// <summary>
    /// Design-space AABB of the board plus chrome HUD. The open deck menu is omitted
    /// so opening it does not change the fit scale.
    /// </summary>
    public bool TryGetFitBounds(out Rect bounds)
    {
        bounds = default;
        if (board == null || phasePanel == null || actionPanel == null)
            return false;

        Vector2 boardSize = board.WorldSize;
        float minX = -boardSize.x * 0.5f;
        float maxX = boardSize.x * 0.5f;
        float minY = -boardSize.y * 0.5f;
        float maxY = boardSize.y * 0.5f;

        Encapsulate(ref minX, ref minY, ref maxX, ref maxY, topHud);
        Encapsulate(ref minX, ref minY, ref maxX, ref maxY, actionPanel);
        Encapsulate(ref minX, ref minY, ref maxX, ref maxY, phasePanel);

        bounds = Rect.MinMaxRect(minX, minY, maxX, maxY);
        return bounds.width > 0.0001f && bounds.height > 0.0001f;
    }

    static void Encapsulate(ref float minX, ref float minY, ref float maxX, ref float maxY, RectTransform panel)
    {
        if (panel == null)
            return;

        Vector2 size = PanelDesignSize(panel);
        Vector3 p = panel.localPosition;
        minX = Mathf.Min(minX, p.x - size.x * 0.5f);
        maxX = Mathf.Max(maxX, p.x + size.x * 0.5f);
        minY = Mathf.Min(minY, p.y - size.y * 0.5f);
        maxY = Mathf.Max(maxY, p.y + size.y * 0.5f);
    }

    static Vector2 PanelDesignSize(RectTransform panel)
    {
        Rect rect = panel.rect;
        Vector3 scale = panel.localScale;
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
