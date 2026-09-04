using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// Builds match HUD objects in the open scene so they can be edited in the Scene view.
/// Play Mode does not create these objects.
/// </summary>
public static class MatchHudSceneSetup
{
    const string CanvasName = "Canvas";
    const string PhasePanelName = "PhasePanel";
    const string ActionMenuName = "ActionMenu";
    const string TopHudName = "TopHud";

    [MenuItem("Strategy/Create Match HUD In Scene")]
    public static void CreateFromMenu()
    {
        CreateHud(true);
    }

    [InitializeOnLoadMethod]
    static void CreateIfMissing()
    {
        EditorApplication.delayCall += () =>
        {
            if (Application.isPlaying)
                return;
            if (Object.FindAnyObjectByType<MatchController>() == null)
                return;
            if (Object.FindAnyObjectByType<Canvas>() is Canvas canvas && canvas.transform.Find(PhasePanelName) != null)
            {
                EnsureTopHud(canvas);
                EnsureEnergyHud(canvas);
                EnsureUtilityButton();
                EnsureUnspawnButton();
                EnsureLayoutOn(canvas.gameObject);
                ColorHudButtons();
                return;
            }
            CreateHud(false);
        };
    }

    static void CreateHud(bool force)
    {
        if (!force)
        {
            Canvas existing = Object.FindAnyObjectByType<Canvas>();
            if (existing != null && existing.transform.Find(PhasePanelName) != null)
                return;
        }

        DefaultControls.Resources resources = new DefaultControls.Resources
        {
            standard = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd"),
            background = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd")
        };

        EnsureEventSystem();
        GameObject canvasGo = EnsureCanvas();

        MatchPhaseHud oldPhase = Object.FindAnyObjectByType<MatchPhaseHud>();
        UnitActionMenu oldAction = Object.FindAnyObjectByType<UnitActionMenu>();

        Transform oldPhasePanel = canvasGo.transform.Find(PhasePanelName);
        Transform oldActionPanel = canvasGo.transform.Find(ActionMenuName);
        if (oldPhasePanel != null)
            Object.DestroyImmediate(oldPhasePanel.gameObject);
        if (oldActionPanel != null)
            Object.DestroyImmediate(oldActionPanel.gameObject);

        GameObject phasePanel = CreatePhasePanel(canvasGo.transform, resources);
        GameObject actionPanel = CreateActionPanel(canvasGo.transform, resources);
        RectTransform topHud = EnsureTopHud(canvasGo.GetComponent<Canvas>());

        MatchPhaseHud phaseHud = phasePanel.GetComponent<MatchPhaseHud>();
        UnitActionMenu actionMenu = actionPanel.GetComponent<UnitActionMenu>();
        SpawnHud spawnHud = Object.FindAnyObjectByType<SpawnHud>();
        WirePhase(phaseHud, phasePanel);
        WireAction(actionMenu, actionPanel);
        WireLayout(
            canvasGo,
            phasePanel.GetComponent<RectTransform>(),
            actionPanel.GetComponent<RectTransform>(),
            topHud);

        if (oldPhase != null && oldPhase.gameObject != phasePanel)
            Object.DestroyImmediate(oldPhase.gameObject);
        if (oldAction != null && oldAction.gameObject != actionPanel)
            Object.DestroyImmediate(oldAction.gameObject);

        MatchController match = Object.FindAnyObjectByType<MatchController>();
        if (match != null)
        {
            SerializedObject so = new SerializedObject(match);
            so.FindProperty("phaseHud").objectReferenceValue = phaseHud;
            so.FindProperty("actionMenu").objectReferenceValue = actionMenu;
            so.FindProperty("spawnHud").objectReferenceValue = spawnHud;
            so.ApplyModifiedProperties();
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("Match HUD is in the scene. Edit Canvas / PhasePanel / ActionMenu, then save.");
    }

    static void EnsureEventSystem()
    {
        if (Object.FindAnyObjectByType<EventSystem>() != null)
            return;

        GameObject esGo = new GameObject("EventSystem");
        Undo.RegisterCreatedObjectUndo(esGo, "Create EventSystem");
        esGo.AddComponent<EventSystem>();
        esGo.AddComponent<InputSystemUIInputModule>();
    }

    static GameObject EnsureCanvas()
    {
        Canvas canvas = Object.FindAnyObjectByType<Canvas>();
        if (canvas != null)
            return canvas.gameObject;

        GameObject canvasGo = new GameObject(CanvasName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Undo.RegisterCreatedObjectUndo(canvasGo, "Create Canvas");
        canvasGo.layer = 5;
        canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = Camera.main;
        canvas.sortingOrder = 10;
        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        return canvasGo;
    }

    static GameObject CreatePhasePanel(Transform parent, DefaultControls.Resources resources)
    {
        GameObject panel = DefaultControls.CreatePanel(resources);
        panel.name = PhasePanelName;
        panel.layer = 5;
        panel.transform.SetParent(parent, false);
        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(420f, 520f);

        VerticalLayoutGroup layout = panel.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(12, 12, 12, 12);
        layout.spacing = 8;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        GameObject banner = DefaultControls.CreateImage(resources);
        banner.name = "PhaseBanner";
        banner.transform.SetParent(panel.transform, false);
        banner.AddComponent<LayoutElement>().preferredHeight = 72f;
        VerticalLayoutGroup bannerLayout = banner.AddComponent<VerticalLayoutGroup>();
        bannerLayout.padding = new RectOffset(8, 8, 8, 8);
        bannerLayout.spacing = 2;
        bannerLayout.childControlWidth = true;
        bannerLayout.childControlHeight = true;
        bannerLayout.childForceExpandWidth = true;
        bannerLayout.childForceExpandHeight = false;

        CreateText(banner.transform, "PhaseTitle", "PLAN PHASE", true, 24f, resources);
        CreateText(banner.transform, "PhaseSubtitle", "Set orders. Units do not move yet.", false, 20f, resources);
        CreateText(panel.transform, "MatchInfo", "MATCH", false, 28f, resources);
        CreateText(panel.transform, "TurnHelp", "Turn 1", false, 80f, resources);
        CreateText(panel.transform, "UnitList", "Units", false, 80f, resources);
        CreateButton(panel.transform, "ReadyButton", "Ready", new Color(0.22f, 0.62f, 0.38f, 1f), resources);
        CreateButton(panel.transform, "NextTurnButton", "Next Turn", new Color(0.85f, 0.42f, 0.22f, 1f), resources);

        panel.AddComponent<MatchPhaseHud>();
        Undo.RegisterCreatedObjectUndo(panel, "Create PhasePanel");
        return panel;
    }

    static GameObject CreateActionPanel(Transform parent, DefaultControls.Resources resources)
    {
        GameObject panel = DefaultControls.CreatePanel(resources);
        panel.name = ActionMenuName;
        panel.layer = 5;
        panel.transform.SetParent(parent, false);
        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(480f, 140f);

        VerticalLayoutGroup layout = panel.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(12, 12, 12, 12);
        layout.spacing = 8;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        CreateText(panel.transform, "SelectedInfo", "Select a character", false, 28f, resources);

        GameObject row = new GameObject("PlanButtons", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        row.layer = 5;
        row.transform.SetParent(panel.transform, false);
        HorizontalLayoutGroup horizontal = row.GetComponent<HorizontalLayoutGroup>();
        horizontal.spacing = 8;
        horizontal.childAlignment = TextAnchor.MiddleCenter;
        horizontal.childControlWidth = true;
        horizontal.childControlHeight = true;
        horizontal.childForceExpandWidth = true;
        horizontal.childForceExpandHeight = true;
        row.GetComponent<LayoutElement>().preferredHeight = 36f;

        CreateButton(row.transform, "MoveButton", "Move", new Color(0.25f, 0.5f, 0.9f, 1f), resources);
        CreateButton(row.transform, "AttackButton", "Attack", new Color(0.85f, 0.25f, 0.25f, 1f), resources);
        CreateButton(row.transform, "UtilityButton", "Utility", new Color(0.28f, 0.72f, 0.48f, 1f), resources);
        CreateButton(row.transform, "ClearButton", "Clear", new Color(0.4f, 0.4f, 0.45f, 1f), resources);
        CreateButton(row.transform, "UnspawnButton", "Unspawn", new Color(0.55f, 0.32f, 0.22f, 1f), resources);

        panel.AddComponent<UnitActionMenu>();
        Undo.RegisterCreatedObjectUndo(panel, "Create ActionMenu");
        return panel;
    }

    static Text CreateText(
        Transform parent,
        string objectName,
        string value,
        bool bold,
        float preferredHeight,
        DefaultControls.Resources resources)
    {
        GameObject go = DefaultControls.CreateText(resources);
        go.name = objectName;
        go.layer = 5;
        go.transform.SetParent(parent, false);
        LayoutElement layout = go.GetComponent<LayoutElement>();
        if (layout == null)
            layout = go.AddComponent<LayoutElement>();
        layout.preferredHeight = preferredHeight;
        Text label = go.GetComponent<Text>();
        label.text = value;
        label.alignment = TextAnchor.UpperLeft;
        if (bold)
            label.fontStyle = FontStyle.Bold;
        return label;
    }

    static Button CreateButton(
        Transform parent,
        string objectName,
        string label,
        Color color,
        DefaultControls.Resources resources)
    {
        GameObject go = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        go.layer = 5;
        go.transform.SetParent(parent, false);
        go.GetComponent<LayoutElement>().preferredHeight = 36f;

        Image image = go.GetComponent<Image>();
        image.sprite = resources.background;
        image.type = Image.Type.Sliced;
        image.color = color;

        GameObject textGo = DefaultControls.CreateText(resources);
        textGo.name = "Text";
        textGo.layer = 5;
        textGo.transform.SetParent(go.transform, false);
        LayoutElement textLayout = textGo.GetComponent<LayoutElement>();
        if (textLayout != null)
            Object.DestroyImmediate(textLayout);

        RectTransform textRect = textGo.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        Text text = textGo.GetComponent<Text>();
        text.text = label;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;

        Button button = go.GetComponent<Button>();
        button.targetGraphic = image;
        return button;
    }

    static void ColorHudButtons()
    {
        ColorHudButton("ReadyButton", new Color(0.22f, 0.62f, 0.38f, 1f));
        ColorHudButton("NextTurnButton", new Color(0.85f, 0.42f, 0.22f, 1f));
        ColorHudButton("MoveButton", new Color(0.25f, 0.5f, 0.9f, 1f));
        ColorHudButton("AttackButton", new Color(0.85f, 0.25f, 0.25f, 1f));
        ColorHudButton("UtilityButton", new Color(0.28f, 0.72f, 0.48f, 1f));
        ColorHudButton("ClearButton", new Color(0.4f, 0.4f, 0.45f, 1f));
        ColorHudButton("UnspawnButton", new Color(0.55f, 0.32f, 0.22f, 1f));
    }

    static void ColorHudButton(string objectName, Color color)
    {
        Button[] buttons = Object.FindObjectsByType<Button>(FindObjectsInactive.Include);
        for (int i = 0; i < buttons.Length; i++)
        {
            Button button = buttons[i];
            if (button.name != objectName)
                continue;

            Image image = button.GetComponent<Image>();
            if (image == null)
                image = Undo.AddComponent<Image>(button.gameObject);
            Undo.RecordObject(image, "Color HUD button");
            image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");
            image.type = Image.Type.Sliced;
            image.color = color;
            Undo.RecordObject(button, "Color HUD button");
            button.targetGraphic = image;

            Text text = button.GetComponentInChildren<Text>();
            Undo.RecordObject(text, "Color HUD button");
            text.color = Color.white;
        }
    }

    static void WirePhase(MatchPhaseHud hud, GameObject panel)
    {
        SerializedObject so = new SerializedObject(hud);
        so.FindProperty("panel").objectReferenceValue = panel.GetComponent<RectTransform>();
        so.FindProperty("banner").objectReferenceValue = panel.transform.Find("PhaseBanner").GetComponent<Image>();
        so.FindProperty("phaseTitle").objectReferenceValue = panel.transform.Find("PhaseBanner/PhaseTitle").GetComponent<Text>();
        so.FindProperty("phaseSubtitle").objectReferenceValue = panel.transform.Find("PhaseBanner/PhaseSubtitle").GetComponent<Text>();
        so.FindProperty("matchInfo").objectReferenceValue = panel.transform.Find("MatchInfo").GetComponent<Text>();
        so.FindProperty("turnHelp").objectReferenceValue = panel.transform.Find("TurnHelp").GetComponent<Text>();
        so.FindProperty("unitList").objectReferenceValue = panel.transform.Find("UnitList").GetComponent<Text>();
        so.FindProperty("readyButton").objectReferenceValue = panel.transform.Find("ReadyButton").GetComponent<Button>();
        so.FindProperty("nextTurnButton").objectReferenceValue = panel.transform.Find("NextTurnButton").GetComponent<Button>();
        so.FindProperty("energyHud").objectReferenceValue = Object.FindAnyObjectByType<EnergyHud>();
        so.ApplyModifiedProperties();
    }

    static void WireAction(UnitActionMenu menu, GameObject panel)
    {
        SerializedObject so = new SerializedObject(menu);
        so.FindProperty("panel").objectReferenceValue = panel.GetComponent<RectTransform>();
        so.FindProperty("selectedInfo").objectReferenceValue = panel.transform.Find("SelectedInfo").GetComponent<Text>();
        so.FindProperty("moveButton").objectReferenceValue = panel.transform.Find("PlanButtons/MoveButton").GetComponent<Button>();
        so.FindProperty("attackButton").objectReferenceValue = panel.transform.Find("PlanButtons/AttackButton").GetComponent<Button>();
        Transform utility = panel.transform.Find("PlanButtons/UtilityButton");
        if (utility != null)
            so.FindProperty("utilityButton").objectReferenceValue = utility.GetComponent<Button>();
        so.FindProperty("clearButton").objectReferenceValue = panel.transform.Find("PlanButtons/ClearButton").GetComponent<Button>();
        Transform unspawn = panel.transform.Find("PlanButtons/UnspawnButton");
        if (unspawn != null)
            so.FindProperty("unspawnButton").objectReferenceValue = unspawn.GetComponent<Button>();
        so.ApplyModifiedProperties();
    }

    static void EnsureUtilityButton()
    {
        Canvas canvas = Object.FindAnyObjectByType<Canvas>();
        UnitActionMenu menu = Object.FindAnyObjectByType<UnitActionMenu>();
        if (canvas == null || menu == null)
            return;

        Transform row = canvas.transform.Find($"{ActionMenuName}/PlanButtons");
        if (row == null)
            return;

        Transform utility = row.Find("UtilityButton");
        if (utility == null)
        {
            Transform attack = row.Find("AttackButton");
            if (attack == null)
                return;
            GameObject go = Object.Instantiate(attack.gameObject, row);
            go.name = "UtilityButton";
            go.transform.SetSiblingIndex(attack.GetSiblingIndex() + 1);
            Undo.RegisterCreatedObjectUndo(go, "Create UtilityButton");
            Text label = go.GetComponentInChildren<Text>();
            if (label != null)
                label.text = "Utility";
            Image image = go.GetComponent<Image>();
            if (image != null)
                image.color = new Color(0.28f, 0.72f, 0.48f, 1f);
            utility = go.transform;
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }

        SerializedObject so = new SerializedObject(menu);
        so.FindProperty("utilityButton").objectReferenceValue = utility.GetComponent<Button>();
        so.ApplyModifiedProperties();
    }

    static void EnsureUnspawnButton()
    {
        Canvas canvas = Object.FindAnyObjectByType<Canvas>();
        UnitActionMenu menu = Object.FindAnyObjectByType<UnitActionMenu>();
        if (canvas == null || menu == null)
            return;

        Transform row = canvas.transform.Find($"{ActionMenuName}/PlanButtons");
        if (row == null)
            return;

        Transform unspawn = row.Find("UnspawnButton");
        if (unspawn == null)
        {
            Transform clear = row.Find("ClearButton");
            if (clear == null)
                return;
            GameObject go = Object.Instantiate(clear.gameObject, row);
            go.name = "UnspawnButton";
            Undo.RegisterCreatedObjectUndo(go, "Create UnspawnButton");
            Text label = go.GetComponentInChildren<Text>();
            if (label != null)
                label.text = "Unspawn";
            Image image = go.GetComponent<Image>();
            if (image != null)
                image.color = new Color(0.55f, 0.32f, 0.22f, 1f);
            unspawn = go.transform;
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }

        SerializedObject so = new SerializedObject(menu);
        so.FindProperty("unspawnButton").objectReferenceValue = unspawn.GetComponent<Button>();
        so.ApplyModifiedProperties();
    }

    static void EnsureEnergyHud(Canvas canvas)
    {
        EnergyHud energy = Object.FindAnyObjectByType<EnergyHud>();
        if (energy == null)
        {
            Transform leftover = canvas.transform.Find("EnergyPanel");
            if (leftover != null)
                Object.DestroyImmediate(leftover.gameObject);
            return;
        }

        MatchPhaseHud hud = Object.FindAnyObjectByType<MatchPhaseHud>();
        if (hud == null)
            return;

        SerializedObject so = new SerializedObject(hud);
        so.FindProperty("energyHud").objectReferenceValue = energy;
        so.ApplyModifiedProperties();
    }

    static RectTransform EnsureTopHud(Canvas canvas)
    {
        Transform existing = canvas.transform.Find(TopHudName);
        RectTransform topHud = existing != null ? existing.GetComponent<RectTransform>() : null;
        if (topHud == null)
        {
            var go = new GameObject(TopHudName, typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
            go.layer = 5;
            go.transform.SetParent(canvas.transform, false);
            Undo.RegisterCreatedObjectUndo(go, "Create TopHud");
            topHud = go.GetComponent<RectTransform>();
            topHud.anchorMin = new Vector2(0.5f, 0.5f);
            topHud.anchorMax = new Vector2(0.5f, 0.5f);
            topHud.pivot = new Vector2(0.5f, 0.5f);
            topHud.localScale = new Vector3(0.01f, 0.01f, 1f);

            HorizontalLayoutGroup row = go.GetComponent<HorizontalLayoutGroup>();
            row.spacing = 8f;
            row.childAlignment = TextAnchor.MiddleCenter;
            row.childControlWidth = false;
            row.childControlHeight = false;
            row.childForceExpandWidth = false;
            row.childForceExpandHeight = false;

            ContentSizeFitter fitter = go.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }

        EnergyHud energy = Object.FindAnyObjectByType<EnergyHud>();
        if (energy == null)
            energy = EnergyHud.CreateInCanvas(topHud);
        if (energy.transform.parent != topHud)
        {
            Undo.SetTransformParent(energy.transform, topHud, "Parent EnergyHud");
        }

        energy.transform.localScale = Vector3.one;
        if (energy.GetComponent<LayoutElement>() == null)
        {
            LayoutElement layout = Undo.AddComponent<LayoutElement>(energy.gameObject);
            layout.preferredWidth = 180f;
            layout.preferredHeight = 72f;
            layout.minWidth = 180f;
            layout.minHeight = 72f;
        }

        SpawnHud spawn = Object.FindAnyObjectByType<SpawnHud>();
        if (spawn == null)
        {
            spawn = SpawnHud.CreateInCanvas(topHud);
            Undo.RegisterCreatedObjectUndo(spawn.gameObject, "Create SpawnHud");
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }
        else if (spawn.transform.parent != topHud)
        {
            Undo.SetTransformParent(spawn.transform, topHud, "Parent SpawnHud");
            spawn.transform.localScale = Vector3.one;
        }

        spawn.transform.SetSiblingIndex(1);

        MatchController match = Object.FindAnyObjectByType<MatchController>();
        if (match != null)
        {
            SerializedObject so = new SerializedObject(match);
            so.FindProperty("spawnHud").objectReferenceValue = spawn;
            so.ApplyModifiedProperties();
        }

        PlaceTopHudAboveBoard(topHud);
        return topHud;
    }

    static void PlaceTopHudAboveBoard(RectTransform topHud)
    {
        PlaceholderBoard board = Object.FindAnyObjectByType<PlaceholderBoard>();
        if (board == null || topHud == null)
            return;
        Vector2 boardSize = board.WorldSize;
        Vector3 center = board.transform.position;
        Vector2 topSize = new Vector2(3.48f, 0.72f);
        topHud.position = center + new Vector3(0f, boardSize.y * 0.5f + topSize.y * 0.5f + 0.08f, 0f);
    }

    static void EnsureLayoutOn(GameObject canvasGo)
    {
        Transform phase = canvasGo.transform.Find(PhasePanelName);
        Transform action = canvasGo.transform.Find(ActionMenuName);
        Transform top = canvasGo.transform.Find(TopHudName);
        WireLayout(
            canvasGo,
            phase != null ? phase.GetComponent<RectTransform>() : null,
            action != null ? action.GetComponent<RectTransform>() : null,
            top != null ? top.GetComponent<RectTransform>() : null);
    }

    static void WireLayout(
        GameObject canvasGo,
        RectTransform phasePanel,
        RectTransform actionPanel,
        RectTransform topHud)
    {
        MatchHudLayout layout = canvasGo.GetComponent<MatchHudLayout>();
        bool created = layout == null;
        if (created)
            layout = canvasGo.AddComponent<MatchHudLayout>();

        SerializedObject so = new SerializedObject(layout);
        so.FindProperty("board").objectReferenceValue = Object.FindAnyObjectByType<PlaceholderBoard>();
        so.FindProperty("phasePanel").objectReferenceValue = phasePanel;
        so.FindProperty("actionPanel").objectReferenceValue = actionPanel;
        so.FindProperty("topHud").objectReferenceValue = topHud;
        so.ApplyModifiedProperties();
        layout.Apply();
        if (created)
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }
}
