using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Scene HUD chip next to energy: shows the next spawn cost and requests a spawn on click.
/// Place and edit this object in the Scene view. Play Mode only updates the label.
/// </summary>
public class SpawnHud : MonoBehaviour
{
    public const string ObjectName = "SpawnHud";

    public Action OnSpawn;

    [SerializeField] Button button;
    [SerializeField] Text label;

    public RectTransform Rect => transform as RectTransform;

    void Awake()
    {
        if (button != null)
            button.onClick.AddListener(() => OnSpawn?.Invoke());
    }

    public void Refresh(MatchStatus status)
    {
        if (label != null)
        {
            if (status.SpawnCostIsInfinite)
                label.text = "Spawn ∞";
            else if (status.NextSpawnCost == 0)
                label.text = "Spawn free";
            else
                label.text = $"Spawn {status.NextSpawnCost}";
        }
        if (button != null)
            button.interactable = status.CanSpawn;
    }

    public bool ContainsScreenPoint(Vector2 screenPosition)
    {
        if (!isActiveAndEnabled)
            return false;
        return HudHit.Contains(Rect, screenPosition);
    }

    public static SpawnHud CreateInCanvas(Transform parent)
    {
        var go = new GameObject(ObjectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(LayoutElement), typeof(SpawnHud));
        go.layer = 5;
        go.transform.SetParent(parent, false);

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(160f, 72f);
        rect.localScale = Vector3.one;

        LayoutElement layout = go.GetComponent<LayoutElement>();
        layout.preferredWidth = 160f;
        layout.preferredHeight = 72f;
        layout.minWidth = 160f;
        layout.minHeight = 72f;

        Image background = go.GetComponent<Image>();
        background.color = new Color(0.16f, 0.38f, 0.44f, 0.95f);

        Button button = go.GetComponent<Button>();
        button.targetGraphic = background;
        ColorBlock colors = button.colors;
        colors.disabledColor = new Color(0.35f, 0.35f, 0.38f, 0.85f);
        button.colors = colors;

        var labelGo = new GameObject("SpawnCost", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        labelGo.layer = 5;
        labelGo.transform.SetParent(go.transform, false);
        RectTransform labelRect = labelGo.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        Text label = labelGo.GetComponent<Text>();
        label.font = UiFont();
        label.fontSize = 28;
        label.fontStyle = FontStyle.Bold;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = Color.white;
        label.text = "Spawn 1";

        SpawnHud hud = go.GetComponent<SpawnHud>();
        hud.button = button;
        hud.label = label;
        return hud;
    }

    static Font UiFont()
    {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null)
            font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        return font;
    }
}
