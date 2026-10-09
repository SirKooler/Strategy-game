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
    const string TurnBarName = "TurnBar";
    const string LegacyPhasePanelName = "PhasePanel";
    const string ActionMenuName = "ActionMenu";
    const string TopHudName = "TopHud";
    const string CharacterMenuName = "CharacterMenu";
    const string DeckButtonName = "DeckButton";
    const string MatchViewName = "MatchView";

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
            if (Object.FindAnyObjectByType<Canvas>() is Canvas canvas && FindTurnBar(canvas.transform) != null)
            {
                EnsureSlimTurnBar(canvas);
                EnsureTopHud(canvas);
                EnsureEnergyHud(canvas);
                EnsureCharacterMenu(canvas);
                EnsureUtilityButton();
                EnsureUnspawnButton();
                EnsureCharacterInspect();
                EnsureLayoutOn(canvas.gameObject);
                EnsureMatchView();
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
            if (existing != null && FindTurnBar(existing.transform) != null)
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

        Transform oldTurnBar = canvasGo.transform.Find(TurnBarName);
        Transform oldPhasePanel = canvasGo.transform.Find(LegacyPhasePanelName);
        Transform oldActionPanel = canvasGo.transform.Find(ActionMenuName);
        if (oldTurnBar != null)
            Object.DestroyImmediate(oldTurnBar.gameObject);
        if (oldPhasePanel != null)
            Object.DestroyImmediate(oldPhasePanel.gameObject);
        if (oldActionPanel != null)
            Object.DestroyImmediate(oldActionPanel.gameObject);

        GameObject phasePanel = CreateTurnBar(canvasGo.transform, resources);
        GameObject actionPanel = CreateActionPanel(canvasGo.transform, resources);
        RectTransform topHud = EnsureTopHud(canvasGo.GetComponent<Canvas>());
        CharacterMenu characterMenu = EnsureCharacterMenu(canvasGo.GetComponent<Canvas>());

        MatchPhaseHud phaseHud = phasePanel.GetComponent<MatchPhaseHud>();
        UnitActionMenu actionMenu = actionPanel.GetComponent<UnitActionMenu>();
        WirePhase(phaseHud, phasePanel);
        WireAction(actionMenu, actionPanel);
        EnsureCharacterInspect();
        WireLayout(
            canvasGo,
            phasePanel.GetComponent<RectTransform>(),
            actionPanel.GetComponent<RectTransform>(),
            topHud,
            characterMenu != null ? characterMenu.Rect : null);
        EnsureMatchView();

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
            so.FindProperty("characterMenu").objectReferenceValue = characterMenu;
            so.ApplyModifiedProperties();
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("Match HUD is in the scene. Edit Canvas / TurnBar / ActionMenu, then save.");
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

    static GameObject CreateTurnBar(Transform parent, DefaultControls.Resources resources)
    {
        GameObject panel = DefaultControls.CreatePanel(resources);
        panel.name = TurnBarName;
        panel.layer = 5;
        panel.transform.SetParent(parent, false);
        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(280f, 48f);
        rect.localScale = new Vector3(0.01f, 0.01f, 1f);

        HorizontalLayoutGroup layout = panel.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(8, 8, 8, 8);
        layout.spacing = 8;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = true;

        CreateResultLabel(panel.transform, resources);

        panel.AddComponent<MatchPhaseHud>();
        Undo.RegisterCreatedObjectUndo(panel, "Create TurnBar");
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

    static void CreateResultLabel(Transform parent, DefaultControls.Resources resources)
    {
        GameObject textGo = DefaultControls.CreateText(resources);
        textGo.name = "ResultLabel";
        textGo.layer = 5;
        textGo.transform.SetParent(parent, false);
        textGo.SetActive(false);

        Text text = textGo.GetComponent<Text>();
        text.text = "You win";
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;

        LayoutElement layout = textGo.GetComponent<LayoutElement>();
        if (layout == null)
            layout = textGo.AddComponent<LayoutElement>();
        layout.preferredHeight = 36f;
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
        ColorHudButton("MoveButton", new Color(0.25f, 0.5f, 0.9f, 1f));
        ColorHudButton("AttackButton", new Color(0.85f, 0.25f, 0.25f, 1f));
        ColorHudButton("UtilityButton", new Color(0.28f, 0.72f, 0.48f, 1f));
        ColorHudButton("ClearButton", new Color(0.4f, 0.4f, 0.45f, 1f));
        ColorHudButton("UnspawnButton", new Color(0.55f, 0.32f, 0.22f, 1f));
        ColorHudButton(DeckButtonName, new Color(0.16f, 0.38f, 0.44f, 0.95f), "Deck");
    }

    static void ColorHudButton(string objectName, Color color, string label = null)
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
            if (label != null)
                text.text = label;
        }
    }

    static void WirePhase(MatchPhaseHud hud, GameObject panel)
    {
        SerializedObject so = new SerializedObject(hud);
        so.FindProperty("panel").objectReferenceValue = panel.GetComponent<RectTransform>();
        Transform ready = FindReadyButton(panel.transform);
        if (ready != null)
            so.FindProperty("readyButton").objectReferenceValue = ready.GetComponent<Button>();
        Transform result = panel.transform.Find("ResultLabel");
        if (result != null)
            so.FindProperty("resultLabel").objectReferenceValue = result.GetComponent<Text>();
        so.FindProperty("energyHud").objectReferenceValue = Object.FindAnyObjectByType<EnergyHud>();
        so.ApplyModifiedProperties();
    }

    static void WireAction(UnitActionMenu menu, GameObject panel)
    {
        SerializedObject so = new SerializedObject(menu);
        so.FindProperty("panel").objectReferenceValue = panel.GetComponent<RectTransform>();
        so.ApplyModifiedProperties();
        menu.EnsureLayout();
    }

    static void EnsureCharacterInspect()
    {
        UnitActionMenu menu = Object.FindAnyObjectByType<UnitActionMenu>();
        if (menu == null || !menu.EnsureLayout())
            return;
        EditorUtility.SetDirty(menu);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
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
            row.childControlWidth = true;
            row.childControlHeight = true;
            row.childForceExpandWidth = false;
            row.childForceExpandHeight = false;

            ContentSizeFitter fitter = go.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            DefaultControls.Resources resources = new DefaultControls.Resources
            {
                background = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd")
            };
            Button ready = CreateButton(go.transform, "ReadyButton", "Ready", new Color(0.22f, 0.62f, 0.38f, 1f), resources);
            RectTransform readyRect = ready.GetComponent<RectTransform>();
            readyRect.sizeDelta = new Vector2(120f, 72f);
            LayoutElement readyLayout = ready.GetComponent<LayoutElement>();
            readyLayout.preferredWidth = 120f;
            readyLayout.preferredHeight = 72f;
            readyLayout.minWidth = 120f;
            readyLayout.minHeight = 72f;
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

        EnsureDeckButton(topHud);
        RemoveSpawnHud();
        PlaceTopHudAboveBoard(topHud);
        return topHud;
    }

    static Transform FindReadyButton(Transform panel)
    {
        Transform ready = panel.Find("ReadyButton");
        if (ready != null)
            return ready;
        Transform canvas = panel.parent;
        if (canvas == null)
            return null;
        return canvas.Find($"{TopHudName}/ReadyButton");
    }

    static Button EnsureDeckButton(RectTransform topHud)
    {
        Transform existing = topHud.Find(DeckButtonName);
        if (existing == null)
        {
            Transform leftover = topHud.Find(SpawnHud.ObjectName);
            if (leftover != null)
            {
                Undo.RecordObject(leftover.gameObject, "Rename DeckButton");
                leftover.name = DeckButtonName;
                existing = leftover;
            }
        }

        if (existing == null)
        {
            DefaultControls.Resources resources = new DefaultControls.Resources
            {
                background = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd")
            };
            Button created = CreateButton(topHud, DeckButtonName, "Deck", new Color(0.16f, 0.38f, 0.44f, 0.95f), resources);
            existing = created.transform;
            Undo.RegisterCreatedObjectUndo(created.gameObject, "Create DeckButton");
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }

        SpawnHud spawn = existing.GetComponent<SpawnHud>();
        if (spawn != null)
            Undo.DestroyObjectImmediate(spawn);

        RectTransform rect = existing.GetComponent<RectTransform>();
        Undo.RecordObject(rect, "Size DeckButton");
        rect.sizeDelta = new Vector2(120f, 72f);
        existing.localScale = Vector3.one;

        LayoutElement layout = existing.GetComponent<LayoutElement>();
        if (layout == null)
            layout = Undo.AddComponent<LayoutElement>(existing.gameObject);
        Undo.RecordObject(layout, "Size DeckButton");
        layout.preferredWidth = 120f;
        layout.preferredHeight = 72f;
        layout.minWidth = 120f;
        layout.minHeight = 72f;

        Text label = existing.GetComponentInChildren<Text>();
        if (label != null)
        {
            Undo.RecordObject(label, "Label DeckButton");
            label.text = "Deck";
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
        }

        Button button = existing.GetComponent<Button>();
        if (button == null)
            button = Undo.AddComponent<Button>(existing.gameObject);
        Image image = existing.GetComponent<Image>();
        if (image != null)
        {
            Undo.RecordObject(image, "Color DeckButton");
            image.color = new Color(0.16f, 0.38f, 0.44f, 0.95f);
            button.targetGraphic = image;
        }

        return button;
    }

    static CharacterMenu EnsureCharacterMenu(Canvas canvas)
    {
        CharacterMenu menu = Object.FindAnyObjectByType<CharacterMenu>(FindObjectsInactive.Include);
        if (menu == null)
        {
            menu = CharacterMenu.CreateInCanvas(canvas.transform);
            Undo.RegisterCreatedObjectUndo(menu.gameObject, "Create CharacterMenu");
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }
        else if (menu.transform.parent != canvas.transform)
        {
            Undo.SetTransformParent(menu.transform, canvas.transform, "Parent CharacterMenu");
        }

        Transform topHud = canvas.transform.Find(TopHudName);
        Button deck = topHud != null ? EnsureDeckButton(topHud.GetComponent<RectTransform>()) : null;
        if (deck != null)
        {
            SerializedObject menuSo = new SerializedObject(menu);
            menuSo.FindProperty("toggleButton").objectReferenceValue = deck;
            menuSo.ApplyModifiedProperties();
            menu.BindToggle(deck);
        }

        menu.SetOpen(false);
        RemoveSpawnHud();

        MatchController match = Object.FindAnyObjectByType<MatchController>();
        if (match != null)
        {
            SerializedObject so = new SerializedObject(match);
            so.FindProperty("characterMenu").objectReferenceValue = menu;
            so.ApplyModifiedProperties();
        }

        return menu;
    }

    static void RemoveSpawnHud()
    {
        SpawnHud spawn = Object.FindAnyObjectByType<SpawnHud>();
        if (spawn == null)
            return;
        Undo.DestroyObjectImmediate(spawn.gameObject);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }

    static void PlaceTopHudAboveBoard(RectTransform topHud)
    {
        PlaceholderBoard board = Object.FindAnyObjectByType<PlaceholderBoard>();
        if (board == null || topHud == null)
            return;
        Vector2 boardSize = board.WorldSize;
        Vector2 topSize = new Vector2(3.48f, 0.72f);
        topHud.localPosition = new Vector3(0f, boardSize.y * 0.5f + topSize.y * 0.5f + 0.08f, 0f);
    }

    static Transform FindTurnBar(Transform canvas)
    {
        Transform turn = canvas.Find(TurnBarName);
        if (turn != null)
            return turn;
        return canvas.Find(LegacyPhasePanelName);
    }

    static void EnsureSlimTurnBar(Canvas canvas)
    {
        Transform bar = FindTurnBar(canvas.transform);
        if (bar == null)
            return;

        if (bar.name != TurnBarName)
        {
            Undo.RecordObject(bar.gameObject, "Rename TurnBar");
            bar.name = TurnBarName;
        }

        string[] leftover = { "PhaseBanner", "MatchInfo", "TurnHelp", "UnitList", "NextTurnButton" };
        for (int i = 0; i < leftover.Length; i++)
        {
            Transform child = bar.Find(leftover[i]);
            if (child != null)
                Undo.DestroyObjectImmediate(child.gameObject);
        }

        VerticalLayoutGroup vertical = bar.GetComponent<VerticalLayoutGroup>();
        if (vertical != null)
            Undo.DestroyObjectImmediate(vertical);

        HorizontalLayoutGroup row = bar.GetComponent<HorizontalLayoutGroup>();
        if (row == null)
            row = Undo.AddComponent<HorizontalLayoutGroup>(bar.gameObject);
        Undo.RecordObject(row, "Size TurnBar");
        row.padding = new RectOffset(8, 8, 8, 8);
        row.spacing = 8;
        row.childAlignment = TextAnchor.MiddleCenter;
        row.childControlWidth = true;
        row.childControlHeight = true;
        row.childForceExpandWidth = true;
        row.childForceExpandHeight = true;

        RectTransform rect = bar.GetComponent<RectTransform>();
        Undo.RecordObject(rect, "Size TurnBar");
        rect.sizeDelta = new Vector2(280f, 48f);
        rect.localScale = new Vector3(0.01f, 0.01f, 1f);

        DefaultControls.Resources resources = new DefaultControls.Resources
        {
            standard = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd"),
            background = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd")
        };
        if (bar.Find("ResultLabel") == null)
            CreateResultLabel(bar, resources);

        MatchPhaseHud hud = bar.GetComponent<MatchPhaseHud>();
        if (hud != null)
            WirePhase(hud, bar.gameObject);

        DestroyLeftoverInfoWidgets();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }

    static void DestroyLeftoverInfoWidgets()
    {
        string[] names = { "PhaseBanner", "MatchInfo", "TurnHelp", "UnitList", "PhaseTitle", "PhaseSubtitle" };
        GameObject[] objects = Resources.FindObjectsOfTypeAll<GameObject>();
        for (int i = 0; i < objects.Length; i++)
        {
            GameObject go = objects[i];
            if (go == null || EditorUtility.IsPersistent(go))
                continue;
            bool match = false;
            for (int n = 0; n < names.Length; n++)
            {
                if (go.name == names[n])
                {
                    match = true;
                    break;
                }
            }
            if (!match)
                continue;
            Undo.DestroyObjectImmediate(go);
        }
    }

    static void EnsureLayoutOn(GameObject canvasGo)
    {
        Transform phase = FindTurnBar(canvasGo.transform);
        Transform action = canvasGo.transform.Find(ActionMenuName);
        Transform top = canvasGo.transform.Find(TopHudName);
        CharacterMenu menu = Object.FindAnyObjectByType<CharacterMenu>(FindObjectsInactive.Include);
        WireLayout(
            canvasGo,
            phase != null ? phase.GetComponent<RectTransform>() : null,
            action != null ? action.GetComponent<RectTransform>() : null,
            top != null ? top.GetComponent<RectTransform>() : null,
            menu != null ? menu.Rect : null);
    }

    static void WireLayout(
        GameObject canvasGo,
        RectTransform phasePanel,
        RectTransform actionPanel,
        RectTransform topHud,
        RectTransform characterMenu)
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
        so.FindProperty("characterMenu").objectReferenceValue = characterMenu;
        so.ApplyModifiedProperties();
        layout.Apply();
        if (created)
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }

    static void EnsureMatchView()
    {
        PlaceholderBoard board = Object.FindAnyObjectByType<PlaceholderBoard>();
        MatchController match = Object.FindAnyObjectByType<MatchController>();
        Canvas canvas = Object.FindAnyObjectByType<Canvas>();
        if (board == null || match == null || canvas == null)
            return;

        Transform root = FindMatchView();
        if (root == null)
        {
            var go = new GameObject(MatchViewName);
            Undo.RegisterCreatedObjectUndo(go, "Create MatchView");
            root = go.transform;
            go.AddComponent<MatchViewFit>();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }

        MatchViewFit fit = root.GetComponent<MatchViewFit>();
        if (fit == null)
            fit = Undo.AddComponent<MatchViewFit>(root.gameObject);

        SerializedObject so = new SerializedObject(fit);
        SerializedProperty cameraProp = so.FindProperty("targetCamera");
        if (cameraProp != null && cameraProp.objectReferenceValue == null)
        {
            cameraProp.objectReferenceValue = Camera.main;
            so.ApplyModifiedProperties();
        }

        ParentUnderMatchView(board.transform, root);
        ParentUnderMatchView(match.transform, root);
        ParentUnderMatchView(canvas.transform, root);
        fit.Apply();
    }

    static Transform FindMatchView()
    {
        MatchViewFit existing = Object.FindAnyObjectByType<MatchViewFit>();
        if (existing != null)
            return existing.transform;

        Canvas canvas = Object.FindAnyObjectByType<Canvas>();
        if (canvas != null && canvas.transform.parent != null && canvas.transform.parent.name == MatchViewName)
            return canvas.transform.parent;

        return null;
    }

    static void ParentUnderMatchView(Transform child, Transform root)
    {
        if (child == null || child.parent == root)
            return;

        Undo.SetTransformParent(child, root, "Parent MatchView");
        child.localPosition = Vector3.zero;
        child.localRotation = Quaternion.identity;
        if (Mathf.Abs(child.localScale.x - 1f) < 0.0001f
            && Mathf.Abs(child.localScale.y - 1f) < 0.0001f)
            child.localScale = Vector3.one;
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }
}
