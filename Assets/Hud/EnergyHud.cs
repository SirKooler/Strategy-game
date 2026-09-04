using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Scene HUD for remaining energy: lightning icon plus a number.
/// Place and edit this object in the Scene view. Play Mode only updates the number.
/// </summary>
[ExecuteAlways]
public class EnergyHud : MonoBehaviour
{
    public const string ObjectName = "EnergyHud";

    [SerializeField] Image icon;
    [SerializeField] Text value;

    public RectTransform Rect => transform as RectTransform;

    public void SetEnergy(int remaining)
    {
        if (value != null)
            value.text = remaining.ToString();
    }

    public bool ContainsScreenPoint(Vector2 screenPosition)
    {
        if (!isActiveAndEnabled)
            return false;
        return HudHit.Contains(Rect, screenPosition);
    }

    void OnEnable()
    {
        EnsureIcon();
    }

    void OnValidate()
    {
        EnsureIcon();
    }

    public static EnergyHud CreateInCanvas(Transform canvas)
    {
        Sprite lightning = null;
#if UNITY_EDITOR
        lightning = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Hud/EnergyIcon.png");
#endif

        var go = new GameObject(ObjectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(HorizontalLayoutGroup), typeof(LayoutElement), typeof(EnergyHud));
        go.layer = 5;
        go.transform.SetParent(canvas, false);

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(180f, 72f);
        rect.localScale = Vector3.one;

        Image background = go.GetComponent<Image>();
        background.color = new Color(0.1f, 0.1f, 0.12f, 0.9f);

        LayoutElement rootLayout = go.GetComponent<LayoutElement>();
        rootLayout.preferredWidth = 180f;
        rootLayout.preferredHeight = 72f;
        rootLayout.minWidth = 180f;
        rootLayout.minHeight = 72f;

        HorizontalLayoutGroup row = go.GetComponent<HorizontalLayoutGroup>();
        row.padding = new RectOffset(12, 16, 8, 8);
        row.spacing = 8;
        row.childAlignment = TextAnchor.MiddleCenter;
        row.childControlWidth = false;
        row.childControlHeight = true;
        row.childForceExpandWidth = false;
        row.childForceExpandHeight = true;

        var iconGo = new GameObject("EnergyIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement));
        iconGo.layer = 5;
        iconGo.transform.SetParent(go.transform, false);
        iconGo.GetComponent<RectTransform>().sizeDelta = new Vector2(56f, 56f);
        LayoutElement iconLayout = iconGo.GetComponent<LayoutElement>();
        iconLayout.preferredWidth = 56f;
        iconLayout.preferredHeight = 56f;
        iconLayout.minWidth = 56f;
        Image iconImage = iconGo.GetComponent<Image>();
        iconImage.sprite = lightning;
        iconImage.color = Color.white;
        iconImage.preserveAspect = true;

        var valueGo = new GameObject("EnergyValue", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text), typeof(LayoutElement));
        valueGo.layer = 5;
        valueGo.transform.SetParent(go.transform, false);
        LayoutElement valueLayout = valueGo.GetComponent<LayoutElement>();
        valueLayout.preferredWidth = 80f;
        valueLayout.flexibleWidth = 1f;
        Text valueText = valueGo.GetComponent<Text>();
        valueText.font = UiFont();
        valueText.fontSize = 42;
        valueText.fontStyle = FontStyle.Bold;
        valueText.alignment = TextAnchor.MiddleLeft;
        valueText.color = new Color(1f, 0.9f, 0.2f, 1f);
        valueText.text = "2";

        EnergyHud hud = go.GetComponent<EnergyHud>();
        hud.icon = iconImage;
        hud.value = valueText;
        return hud;
    }

    void EnsureIcon()
    {
        if (value != null)
            value.fontStyle = FontStyle.Bold;
    }

    static Font UiFont()
    {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null)
            font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        return font;
    }
}
