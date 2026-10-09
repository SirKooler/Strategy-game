using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Selected character sheet: name, health, deck image, actions, description, and pattern.
/// The back arrow closes the action view, hides board targeting, and restores the plan from before that view.
/// </summary>
public class UnitActionMenu : MonoBehaviour
{
    const string PlaceholderDescription = "Placeholder description.";

    [SerializeField] [TextArea] string defaultMoveDescription = "Move to a targeted square";

    static readonly Color FriendlyHealth = new Color(0.3f, 0.55f, 1f, 1f);
    static readonly Color EnemyHealth = new Color(0.89f, 0.23f, 0.2f, 1f);
    static readonly Color PanelColor = new Color(0.1f, 0.12f, 0.16f, 0.94f);
    static readonly Color ColumnColor = new Color(0.16f, 0.18f, 0.22f, 1f);

    public Action OnMove;
    public Action OnAttack;
    public Action OnUtility;
    public Action OnClear;
    public Action OnUnspawn;
    public Action OnRememberPlan;
    public Action OnRevertPlan;
    public Action OnClose;

    [SerializeField] RectTransform panel;
    [SerializeField] Text nameLabel;
    [SerializeField] Text healthLabel;
    [SerializeField] Image healthFill;
    [SerializeField] Image portrait;
    [SerializeField] Text descriptionLabel;
    [SerializeField] Button unviewButton;
    [SerializeField] ActionPatternView patternView;
    [SerializeField] Button moveButton;
    [SerializeField] Button attackButton;
    [SerializeField] Button utilityButton;
    [SerializeField] Button clearButton;
    [SerializeField] Button unspawnButton;

    readonly Dictionary<Button, Color> _baseColors = new Dictionary<Button, Color>();

    Unit _shown;
    ViewedAction _viewed;
    MatchStatus _status;

    enum ViewedAction
    {
        None,
        Move,
        Attack,
        Utility,
        Clear,
        Unspawn
    }

    void Awake()
    {
        EnsureLayout();
        BindClicks();
    }

    public bool ContainsScreenPoint(Vector2 screenPosition)
    {
        if (panel == null || !panel.gameObject.activeInHierarchy)
            return false;
        return HudHit.Contains(panel, screenPosition);
    }

    public void Refresh(MatchStatus status)
    {
        _status = status;
        bool show = status != null
            && status.Phase == MatchPhase.Plan
            && status.Selected != null
            && status.Selected.IsAlive
            && status.Selected.behaviour != null
            && status.Selected.behaviour.CurrentStats != null;

        if (panel != null)
            panel.gameObject.SetActive(show);
        if (!show)
        {
            _shown = null;
            _viewed = ViewedAction.None;
            return;
        }

        if (status.Selected != _shown)
        {
            _shown = status.Selected;
            _viewed = ViewedAction.None;
        }

        Paint(status);
    }

    /// <summary>
    /// Builds the sheet if the scene still has the old action row.
    /// Returns true when it changed the hierarchy.
    /// </summary>
    public bool EnsureLayout()
    {
        if (panel == null)
            panel = transform as RectTransform;
        if (panel == null)
            return false;
        if (panel.Find("Identity") != null)
            return BindExisting();

        Build();
        return true;
    }

    void BindClicks()
    {
        Bind(moveButton, () => View(ViewedAction.Move, OnMove));
        Bind(attackButton, () => View(ViewedAction.Attack, OnAttack));
        Bind(utilityButton, () => View(ViewedAction.Utility, OnUtility));
        Bind(clearButton, () => View(ViewedAction.Clear, OnClear));
        Bind(unspawnButton, () => View(ViewedAction.Unspawn, OnUnspawn));
        Bind(unviewButton, OnBack);
    }

    static void Bind(Button button, Action action)
    {
        if (button == null)
            return;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => action());
    }

    void View(ViewedAction action, Action plan)
    {
        if (_viewed != action)
            OnRememberPlan?.Invoke();
        _viewed = action;
        if (plan != null)
            plan.Invoke();
        else if (_status != null)
            Paint(_status);
    }

    /// <summary>Character description, with no action pattern. Used after a plan is stored or cleared.</summary>
    public void ReturnToDefault()
    {
        _viewed = ViewedAction.None;
        if (_status == null || _status.Selected == null || panel == null || !panel.gameObject.activeInHierarchy)
            return;
        Paint(_status);
    }

    /// <summary>While an action is open, steps back to the character. On the default sheet, closes the menu.</summary>
    void OnBack()
    {
        if (_viewed != ViewedAction.None)
        {
            Unview();
            return;
        }

        if (OnClose != null)
            OnClose.Invoke();
    }

    /// <summary>Closes the view, hides board targeting, and restores the plan from before the view.</summary>
    void Unview()
    {
        _viewed = ViewedAction.None;
        if (OnRevertPlan != null)
            OnRevertPlan.Invoke();
        else if (_status != null)
            Paint(_status);
    }

    void Paint(MatchStatus status)
    {
        UnitStats stats = status.Selected.behaviour.CurrentStats;
        bool friendly = status.PlayerUnits != null && status.PlayerUnits.Contains(status.Selected);
        Color healthColor = friendly ? FriendlyHealth : EnemyHealth;

        if (nameLabel != null)
            nameLabel.text = string.IsNullOrEmpty(stats.displayName) ? stats.name : stats.displayName;

        if (healthLabel != null)
        {
            healthLabel.supportRichText = true;
            string hex = ColorUtility.ToHtmlStringRGB(healthColor);
            healthLabel.text = $"<color=#{hex}>{stats.health}</color> / {stats.maxHealth}";
        }

        if (healthFill != null)
        {
            healthFill.color = healthColor;
            healthFill.type = Image.Type.Filled;
            healthFill.fillMethod = Image.FillMethod.Horizontal;
            healthFill.fillOrigin = (int)Image.OriginHorizontal.Left;
            float ratio = stats.maxHealth <= 0 ? 0f : Mathf.Clamp01(stats.health / (float)stats.maxHealth);
            healthFill.fillAmount = ratio;
        }

        if (portrait != null)
        {
            portrait.sprite = CharacterAppearance.Deck(stats);
            portrait.color = Color.white;
            portrait.preserveAspect = true;
        }

        if (descriptionLabel != null)
            descriptionLabel.text = UnitBehaviour.Descriptions.ApplyStats(
                DescriptionOf(status.Selected.behaviour.Text, _viewed),
                stats);

        if (unviewButton != null)
            unviewButton.gameObject.SetActive(true);

        if (patternView != null)
        {
            if (_viewed == ViewedAction.Move)
                patternView.ShowMove(stats.movePattern);
            else if (_viewed == ViewedAction.Attack)
                patternView.ShowAttack(stats.attackPattern, ActionPatternView.AttackColor);
            else if (_viewed == ViewedAction.Utility)
                patternView.ShowAttack(stats.utility != null ? stats.utility.pattern : null, ActionPatternView.UtilityColor);
            else
                patternView.Clear();
        }

        bool own = status.CanPlanSelected;
        if (utilityButton != null)
            utilityButton.gameObject.SetActive(status.HasUtility);
        if (clearButton != null)
            clearButton.gameObject.SetActive(own);
        if (unspawnButton != null)
            unspawnButton.gameObject.SetActive(status.CanUnspawnSelected);

        PaintButton(moveButton, _viewed == ViewedAction.Move, own && !status.CanAffordMove);
        PaintButton(attackButton, _viewed == ViewedAction.Attack, own && !status.CanAffordAttack);
        PaintButton(utilityButton, _viewed == ViewedAction.Utility, own && !status.CanAffordUtility);
        PaintButton(clearButton, _viewed == ViewedAction.Clear, false);
        PaintButton(unspawnButton, _viewed == ViewedAction.Unspawn, false);
    }

    string DescriptionOf(UnitBehaviour.Descriptions text, ViewedAction viewed)
    {
        string character = text == null || string.IsNullOrWhiteSpace(text.description)
            ? PlaceholderDescription
            : text.description.Trim();
        if (viewed == ViewedAction.None || viewed == ViewedAction.Clear || viewed == ViewedAction.Unspawn)
            return character;

        string action = null;
        if (viewed == ViewedAction.Move)
            action = text != null && !string.IsNullOrWhiteSpace(text.move)
                ? text.move
                : defaultMoveDescription;
        else if (viewed == ViewedAction.Attack)
            action = text.attack;
        else if (viewed == ViewedAction.Utility)
            action = text.utility;

        return string.IsNullOrWhiteSpace(action) ? character : action.Trim();
    }

    void PaintButton(Button button, bool viewed, bool dim)
    {
        if (button == null || !button.gameObject.activeInHierarchy)
            return;
        Image image = button.GetComponent<Image>();
        if (image == null)
            return;
        if (!_baseColors.TryGetValue(button, out Color baseColor))
        {
            baseColor = image.color;
            _baseColors[button] = baseColor;
        }

        Color color = baseColor;
        if (dim && !viewed)
            color = new Color(baseColor.r * 0.55f, baseColor.g * 0.55f, baseColor.b * 0.55f, baseColor.a);
        if (viewed)
            color = Color.Lerp(baseColor, Color.white, 0.42f);
        image.color = color;
        button.interactable = true;
    }

    bool BindExisting()
    {
        bool changed = false;
        changed |= Assign(ref nameLabel, FindText("Identity/Name"));
        changed |= Assign(ref healthLabel, FindText("Identity/HealthText"));
        changed |= Assign(ref healthFill, FindImage("Identity/HealthBar/Fill"));
        changed |= Assign(ref portrait, FindImage("Identity/Portrait/Sprite"));
        changed |= Assign(ref descriptionLabel, FindText("Detail/DescriptionRow/Description"));
        changed |= Assign(ref unviewButton, FindButton("UnviewButton"));
        changed |= Assign(ref patternView, FindPattern());
        changed |= Assign(ref moveButton, FindButton("MoveButton"));
        changed |= Assign(ref attackButton, FindButton("AttackButton"));
        changed |= Assign(ref utilityButton, FindButton("UtilityButton"));
        changed |= Assign(ref clearButton, FindButton("ClearButton"));
        changed |= Assign(ref unspawnButton, FindButton("UnspawnButton"));
        return changed;
    }

    static bool Assign<T>(ref T field, T value) where T : UnityEngine.Object
    {
        if (field != null || value == null)
            return false;
        field = value;
        return true;
    }

    void Build()
    {
        Image background = panel.GetComponent<Image>();
        if (background != null)
        {
            background.color = PanelColor;
            background.raycastTarget = true;
        }

        panel.anchorMin = new Vector2(0.5f, 0.5f);
        panel.anchorMax = new Vector2(0.5f, 0.5f);
        panel.pivot = new Vector2(0.5f, 0.5f);
        panel.sizeDelta = new Vector2(640f, 300f);
        if (panel.localScale.x > 0.5f)
            panel.localScale = new Vector3(0.01f, 0.01f, 1f);

        VerticalLayoutGroup oldColumn = panel.GetComponent<VerticalLayoutGroup>();
        if (oldColumn != null)
            DestroyImmediate(oldColumn);

        HorizontalLayoutGroup row = panel.GetComponent<HorizontalLayoutGroup>();
        if (row == null)
            row = panel.gameObject.AddComponent<HorizontalLayoutGroup>();
        row.padding = new RectOffset(8, 8, 8, 8);
        row.spacing = 8f;
        row.childAlignment = TextAnchor.MiddleCenter;
        row.childControlWidth = true;
        row.childControlHeight = true;
        row.childForceExpandWidth = false;
        row.childForceExpandHeight = true;

        moveButton = moveButton != null ? moveButton : FindButton("MoveButton");
        attackButton = attackButton != null ? attackButton : FindButton("AttackButton");
        utilityButton = utilityButton != null ? utilityButton : FindButton("UtilityButton");
        clearButton = clearButton != null ? clearButton : FindButton("ClearButton");
        unspawnButton = unspawnButton != null ? unspawnButton : FindButton("UnspawnButton");

        RectTransform identity = CreateColumn("Identity", 200f, 0f);
        RectTransform actions = CreateColumn("Actions", 156f, 0f);
        RectTransform detail = CreateColumn("Detail", 250f, 1f);

        nameLabel = CreateLabel(identity, "Name", 20, TextAnchor.MiddleLeft, FontStyle.Bold);
        nameLabel.gameObject.GetComponent<LayoutElement>().preferredHeight = 28f;
        healthLabel = CreateLabel(identity, "HealthText", 18, TextAnchor.MiddleLeft, FontStyle.Bold);
        healthLabel.gameObject.GetComponent<LayoutElement>().preferredHeight = 24f;
        healthFill = CreateHealthBar(identity);
        portrait = CreatePortrait(identity);

        PlaceAction(actions, ref moveButton, "MoveButton", "Move", new Color(0.25f, 0.5f, 0.9f, 1f));
        PlaceAction(actions, ref attackButton, "AttackButton", "Attack", new Color(0.85f, 0.25f, 0.25f, 1f));
        PlaceAction(actions, ref utilityButton, "UtilityButton", "Utility", new Color(0.28f, 0.72f, 0.48f, 1f));
        PlaceAction(actions, ref clearButton, "ClearButton", "Clear", new Color(0.4f, 0.4f, 0.45f, 1f));
        PlaceAction(actions, ref unspawnButton, "UnspawnButton", "Unspawn", new Color(0.55f, 0.32f, 0.22f, 1f));

        RectTransform descriptionRow = CreateRow(detail, "DescriptionRow");
        descriptionLabel = CreateLabel(descriptionRow, "Description", 16, TextAnchor.UpperLeft, FontStyle.Normal);
        LayoutElement descriptionLayout = descriptionLabel.GetComponent<LayoutElement>();
        descriptionLayout.flexibleWidth = 1f;
        descriptionLayout.flexibleHeight = 1f;
        unviewButton = CreateUnviewButton(descriptionRow);

        patternView = CreatePattern(detail);

        HideLeftover("SelectedInfo");
        HideLeftover("PlanButtons");

        LayoutRebuilder.ForceRebuildLayoutImmediate(panel);
    }

    RectTransform CreateColumn(string objectName, float preferredWidth, float flexibleWidth)
    {
        Image image = CreateImage(panel, objectName, ColumnColor);
        VerticalLayoutGroup layout = image.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(8, 8, 8, 8);
        layout.spacing = 6f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        LayoutElement element = image.gameObject.AddComponent<LayoutElement>();
        element.preferredWidth = preferredWidth;
        element.flexibleWidth = flexibleWidth;
        element.minWidth = flexibleWidth > 0f ? 160f : preferredWidth;
        return image.rectTransform;
    }

    static RectTransform CreateRow(Transform parent, string objectName)
    {
        var go = new GameObject(objectName, typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        go.layer = 5;
        go.transform.SetParent(parent, false);
        HorizontalLayoutGroup layout = go.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = 6f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        LayoutElement element = go.GetComponent<LayoutElement>();
        element.flexibleWidth = 1f;
        element.flexibleHeight = 1f;
        element.minHeight = 48f;
        return go.GetComponent<RectTransform>();
    }

    Image CreateHealthBar(Transform parent)
    {
        Image track = CreateImage(parent, "HealthBar", new Color(0.08f, 0.08f, 0.1f, 1f));
        LayoutElement element = track.gameObject.AddComponent<LayoutElement>();
        element.preferredHeight = 16f;
        element.minHeight = 16f;
        element.flexibleWidth = 1f;
        Image fill = CreateImage(track.transform, "Fill", FriendlyHealth);
        Stretch(fill.rectTransform, Vector2.zero, Vector2.one);
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillOrigin = (int)Image.OriginHorizontal.Left;
        fill.fillAmount = 1f;
        return fill;
    }

    Image CreatePortrait(Transform parent)
    {
        Image frame = CreateImage(parent, "Portrait", new Color(0.08f, 0.08f, 0.1f, 1f));
        LayoutElement element = frame.gameObject.AddComponent<LayoutElement>();
        element.flexibleHeight = 1f;
        element.minHeight = 72f;
        element.flexibleWidth = 1f;
        Image sprite = CreateImage(frame.transform, "Sprite", Color.white);
        Stretch(sprite.rectTransform, new Vector2(0.08f, 0.08f), new Vector2(0.92f, 0.92f));
        sprite.preserveAspect = true;
        return sprite;
    }

    Button CreateUnviewButton(Transform parent)
    {
        Button button = CreateActionButton(parent, "UnviewButton", "\u2190", new Color(0.22f, 0.24f, 0.28f, 1f));
        LayoutElement element = button.GetComponent<LayoutElement>();
        element.preferredWidth = 36f;
        element.minWidth = 36f;
        element.flexibleWidth = 0f;
        element.preferredHeight = 36f;
        element.minHeight = 36f;
        element.flexibleHeight = 0f;
        Text label = button.GetComponentInChildren<Text>();
        if (label != null)
            label.fontSize = 22;
        button.gameObject.SetActive(false);
        return button;
    }

    ActionPatternView CreatePattern(Transform parent)
    {
        Image frame = CreateImage(parent, "Pattern", new Color(0.1f, 0.11f, 0.13f, 1f));
        LayoutElement element = frame.gameObject.AddComponent<LayoutElement>();
        element.preferredHeight = 150f;
        element.minHeight = 120f;
        element.flexibleWidth = 1f;

        var gridGo = new GameObject("Grid", typeof(RectTransform), typeof(GridLayoutGroup), typeof(ActionPatternView));
        gridGo.layer = 5;
        gridGo.transform.SetParent(frame.transform, false);
        RectTransform gridRect = gridGo.GetComponent<RectTransform>();
        gridRect.anchorMin = new Vector2(0.5f, 0.5f);
        gridRect.anchorMax = new Vector2(0.5f, 0.5f);
        gridRect.pivot = new Vector2(0.5f, 0.5f);
        gridRect.sizeDelta = new Vector2(140f, 140f);
        return gridGo.GetComponent<ActionPatternView>();
    }

    void PlaceAction(Transform parent, ref Button button, string objectName, string label, Color color)
    {
        if (button == null)
            button = CreateActionButton(parent, objectName, label, color);
        else
            button.transform.SetParent(parent, false);

        LayoutElement element = button.GetComponent<LayoutElement>();
        if (element == null)
            element = button.gameObject.AddComponent<LayoutElement>();
        element.flexibleWidth = 1f;
        element.flexibleHeight = 1f;
        element.minHeight = 28f;
    }

    Button CreateActionButton(Transform parent, string objectName, string label, Color color)
    {
        var go = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(LayoutElement));
        go.layer = 5;
        go.transform.SetParent(parent, false);
        Image image = go.GetComponent<Image>();
        image.sprite = PlaceholderSprite.WhiteSquare();
        image.color = color;
        Button button = go.GetComponent<Button>();
        button.targetGraphic = image;

        Text text = CreateLabel(go.transform, "Text", 16, TextAnchor.MiddleCenter, FontStyle.Bold);
        Stretch(text.rectTransform, Vector2.zero, Vector2.one);
        text.text = label;
        text.color = Color.white;
        LayoutElement textLayout = text.GetComponent<LayoutElement>();
        if (textLayout != null)
            DestroyUi(textLayout);
        return button;
    }

    void HideLeftover(string objectName)
    {
        Transform leftover = panel.Find(objectName);
        if (leftover == null)
            return;
        leftover.gameObject.SetActive(false);
        DestroyUi(leftover.gameObject);
    }

    Text FindText(string path)
    {
        Transform child = panel.Find(path);
        return child != null ? child.GetComponent<Text>() : null;
    }

    Image FindImage(string path)
    {
        Transform child = panel.Find(path);
        return child != null ? child.GetComponent<Image>() : null;
    }

    ActionPatternView FindPattern()
    {
        Transform child = panel.Find("Detail/Pattern/Grid");
        return child != null ? child.GetComponent<ActionPatternView>() : null;
    }

    Button FindButton(string objectName)
    {
        Button[] buttons = panel.GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i].name == objectName)
                return buttons[i];
        }

        return null;
    }

    static Image CreateImage(Transform parent, string objectName, Color color)
    {
        var go = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.layer = 5;
        go.transform.SetParent(parent, false);
        Image image = go.GetComponent<Image>();
        image.sprite = PlaceholderSprite.WhiteSquare();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    static Text CreateLabel(Transform parent, string objectName, int fontSize, TextAnchor alignment, FontStyle style)
    {
        var go = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text), typeof(LayoutElement));
        go.layer = 5;
        go.transform.SetParent(parent, false);
        Text text = go.GetComponent<Text>();
        text.font = UiFont();
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.alignment = alignment;
        text.color = Color.white;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        text.raycastTarget = false;
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

    static void DestroyUi(UnityEngine.Object target)
    {
        if (target == null)
            return;
        if (Application.isPlaying)
            Destroy(target);
        else
            DestroyImmediate(target);
    }
}
