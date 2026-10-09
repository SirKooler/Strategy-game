using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 3x3 deck palette. Play Mode only updates slot state; the scene object is authored in the Editor.
/// </summary>
public class CharacterMenu : MonoBehaviour
{
    public const string ObjectName = "CharacterMenu";
    public const string ToggleButtonName = "DeckButton";

    public Action<UnitStats> OnSpawn;

    [SerializeField] RectTransform panel;
    [SerializeField] RectTransform grid;
    [SerializeField] Button toggleButton;

    readonly List<SlotView> _slots = new List<SlotView>();

    public RectTransform Rect => panel != null ? panel : transform as RectTransform;

    public bool IsOpen => gameObject.activeSelf;

    void Awake()
    {
        BindToggle(toggleButton);
    }

    public void BindToggle(Button button)
    {
        if (button != null)
            toggleButton = button;
        if (toggleButton == null)
            return;
        toggleButton.onClick.RemoveListener(Toggle);
        toggleButton.onClick.AddListener(Toggle);
    }

    public void Toggle()
    {
        SetOpen(!IsOpen);
    }

    public void SetOpen(bool open)
    {
        gameObject.SetActive(open);
    }

    public void HideIfClickedOutside(Vector2 screenPosition)
    {
        if (!IsOpen)
            return;
        if (ContainsMenu(screenPosition) || ContainsToggle(screenPosition))
            return;
        SetOpen(false);
    }

    public bool ContainsMenu(Vector2 screenPosition)
    {
        return HudHit.Contains(Rect, screenPosition);
    }

    public bool ContainsToggle(Vector2 screenPosition)
    {
        if (toggleButton == null || !toggleButton.isActiveAndEnabled)
            return false;
        return HudHit.Contains(toggleButton.transform as RectTransform, screenPosition);
    }

    public bool ContainsScreenPoint(Vector2 screenPosition)
    {
        return ContainsToggle(screenPosition) || ContainsMenu(screenPosition);
    }

    public void Refresh(MatchStatus status)
    {
        if (status == null || grid == null)
            return;

        EnsureSlotCount(status.DeckSlots.Count);
        for (int i = 0; i < _slots.Count; i++)
        {
            if (i < status.DeckSlots.Count)
                BindSlot(_slots[i], status, status.DeckSlots[i]);
            else
                _slots[i].Root.gameObject.SetActive(false);
        }
    }

    void EnsureSlotCount(int count)
    {
        while (_slots.Count < count)
            _slots.Add(CreateSlot(grid, _slots.Count));
    }

    void BindSlot(SlotView slot, MatchStatus status, DeckSlotStatus data)
    {
        slot.Root.gameObject.SetActive(true);
        slot.Definition = data.Definition;
        slot.Swatch.sprite = data.DeckSprite;
        slot.Swatch.color = Color.white;
        slot.Swatch.preserveAspect = true;
        slot.Name.text = data.DisplayName;

        bool onBoard = data.State == DeckSlotState.OnBoard;
        bool dead = data.State == DeckSlotState.Dead;
        slot.Name.color = onBoard ? Color.white : new Color(0.72f, 0.72f, 0.74f, 1f);
        slot.Gray.enabled = !onBoard;
        slot.DeadX.enabled = dead;

        if (dead || onBoard)
            slot.Cost.text = "";
        else if (status.SpawnCostIsInfinite)
            slot.Cost.text = "∞";
        else if (status.NextSpawnCost == 0)
            slot.Cost.text = "free";
        else
            slot.Cost.text = status.NextSpawnCost.ToString();

        slot.Pending.enabled = data.IsPending;
        slot.Button.interactable = data.CanSpawnThis || data.IsPending;
    }

    public static CharacterMenu CreateInCanvas(Transform parent)
    {
        var go = new GameObject(ObjectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CharacterMenu));
        go.layer = 5;
        go.transform.SetParent(parent, false);

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(300f, 340f);
        rect.localScale = new Vector3(0.01f, 0.01f, 1f);

        Image background = go.GetComponent<Image>();
        background.color = new Color(0.1f, 0.1f, 0.12f, 0.92f);

        var gridGo = new GameObject("Grid", typeof(RectTransform), typeof(GridLayoutGroup));
        gridGo.layer = 5;
        gridGo.transform.SetParent(go.transform, false);
        RectTransform gridRect = gridGo.GetComponent<RectTransform>();
        gridRect.anchorMin = Vector2.zero;
        gridRect.anchorMax = Vector2.one;
        gridRect.offsetMin = new Vector2(10f, 10f);
        gridRect.offsetMax = new Vector2(-10f, -10f);

        GridLayoutGroup grid = gridGo.GetComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(88f, 96f);
        grid.spacing = new Vector2(6f, 6f);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 3;
        grid.childAlignment = TextAnchor.MiddleCenter;

        CharacterMenu menu = go.GetComponent<CharacterMenu>();
        menu.panel = rect;
        menu.grid = gridRect;
        go.SetActive(false);
        return menu;
    }

    SlotView CreateSlot(Transform parent, int index)
    {
        var go = new GameObject($"Slot{index}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.layer = 5;
        go.transform.SetParent(parent, false);

        Image background = go.GetComponent<Image>();
        background.color = new Color(0.18f, 0.18f, 0.2f, 1f);

        Button button = go.GetComponent<Button>();
        button.targetGraphic = background;
        ColorBlock colors = button.colors;
        colors.disabledColor = new Color(0.18f, 0.18f, 0.2f, 1f);
        button.colors = colors;

        Image swatch = CreateImage(go.transform, "Swatch", Color.white);
        swatch.preserveAspect = true;
        Stretch(swatch.rectTransform, new Vector2(0.15f, 0.42f), new Vector2(0.85f, 0.88f));

        Image gray = CreateImage(go.transform, "Gray", new Color(0.12f, 0.12f, 0.14f, 0.55f));
        Stretch(gray.rectTransform, new Vector2(0.15f, 0.42f), new Vector2(0.85f, 0.88f));
        gray.raycastTarget = false;

        Text name = CreateLabel(go.transform, "Name", 12, TextAnchor.LowerCenter);
        Stretch(name.rectTransform, new Vector2(0.05f, 0.2f), new Vector2(0.95f, 0.42f));

        Text cost = CreateLabel(go.transform, "Cost", 14, TextAnchor.LowerCenter);
        cost.fontStyle = FontStyle.Bold;
        cost.color = new Color(1f, 0.9f, 0.2f, 1f);
        Stretch(cost.rectTransform, new Vector2(0.05f, 0.02f), new Vector2(0.95f, 0.22f));

        Text deadX = CreateLabel(go.transform, "DeadX", 42, TextAnchor.MiddleCenter);
        deadX.fontStyle = FontStyle.Bold;
        deadX.color = new Color(0.9f, 0.15f, 0.12f, 1f);
        deadX.text = "X";
        Stretch(deadX.rectTransform, Vector2.zero, Vector2.one);
        deadX.raycastTarget = false;
        deadX.enabled = false;

        Image pending = CreateImage(go.transform, "Pending", new Color(0.95f, 0.85f, 0.25f, 0.28f));
        Stretch(pending.rectTransform, Vector2.zero, Vector2.one);
        pending.raycastTarget = false;
        pending.enabled = false;

        var view = new SlotView
        {
            Root = go.transform as RectTransform,
            Definition = null,
            Button = button,
            Swatch = swatch,
            Name = name,
            Cost = cost,
            Gray = gray,
            DeadX = deadX,
            Pending = pending
        };

        button.onClick.AddListener(() => OnSpawn?.Invoke(view.Definition));
        return view;
    }

    static Image CreateImage(Transform parent, string objectName, Color color)
    {
        var go = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.layer = 5;
        go.transform.SetParent(parent, false);
        Image image = go.GetComponent<Image>();
        image.color = color;
        return image;
    }

    static Text CreateLabel(Transform parent, string objectName, int fontSize, TextAnchor alignment)
    {
        var go = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        go.layer = 5;
        go.transform.SetParent(parent, false);
        Text text = go.GetComponent<Text>();
        text.font = UiFont();
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = Color.white;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        return text;
    }

    static void Stretch(RectTransform rect, Vector2 min, Vector2 max)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    static Font UiFont()
    {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null)
            font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        return font;
    }

    class SlotView
    {
        public RectTransform Root;
        public UnitStats Definition;
        public Button Button;
        public Image Swatch;
        public Text Name;
        public Text Cost;
        public Image Gray;
        public Text DeadX;
        public Image Pending;
    }
}
