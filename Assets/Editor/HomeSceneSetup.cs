using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Builds the placeholder home scene so Find match can be edited in the Scene view.
/// </summary>
public static class HomeSceneSetup
{
    const string ScenePath = "Assets/Scenes/Home.unity";

    [MenuItem("Strategy/Create Home Scene")]
    public static void CreateFromMenu()
    {
        Create();
    }

    public static void Create()
    {
        Scene previous = EditorSceneManager.GetActiveScene();
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        EditorSceneManager.SetActiveScene(scene);

        var cameraGo = new GameObject("Main Camera");
        cameraGo.tag = "MainCamera";
        Camera camera = cameraGo.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.12f, 0.14f, 0.18f, 1f);
        cameraGo.AddComponent<AudioListener>();
        cameraGo.transform.position = new Vector3(0f, 0f, -10f);

        EnsureEventSystem();
        GameObject canvasGo = CreateCanvas();
        DefaultControls.Resources resources = new DefaultControls.Resources
        {
            standard = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd"),
            background = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd")
        };

        GameObject panel = CreatePanel(canvasGo.transform);
        Text trophies = CreateText(panel.transform, "TrophyLabel", "Trophies: 0", 48f);
        Text status = CreateText(panel.transform, "StatusLabel", "", 36f);
        Button find = CreateButton(panel.transform, "FindMatchButton", "Find match", new Color(0.22f, 0.62f, 0.38f, 1f), resources);
        Button cancel = CreateButton(panel.transform, "CancelButton", "Cancel", new Color(0.45f, 0.38f, 0.32f, 1f), resources);
        cancel.gameObject.SetActive(false);

        HomeScreen screen = canvasGo.AddComponent<HomeScreen>();
        SerializedObject so = new SerializedObject(screen);
        so.FindProperty("player").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<Player>("Assets/Player/PlaceholderPlayer.asset");
        so.FindProperty("authProfile").stringValue = "default";
        so.FindProperty("trophyLabel").objectReferenceValue = trophies;
        so.FindProperty("statusLabel").objectReferenceValue = status;
        so.FindProperty("findButton").objectReferenceValue = find;
        so.FindProperty("cancelButton").objectReferenceValue = cancel;
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.SaveScene(scene, ScenePath);
        AddToBuildSettings();
        AssetDatabase.SaveAssets();
        if (previous.IsValid())
            EditorSceneManager.SetActiveScene(previous);
        EditorSceneManager.CloseScene(scene, true);
    }

    static void AddToBuildSettings()
    {
        var scenes = new List<EditorBuildSettingsScene>();
        EditorBuildSettingsScene home = null;
        EditorBuildSettingsScene[] current = EditorBuildSettings.scenes;
        for (int i = 0; i < current.Length; i++)
        {
            if (current[i].path == ScenePath)
                home = new EditorBuildSettingsScene(ScenePath, true);
            else
                scenes.Add(current[i]);
        }

        if (home == null)
            home = new EditorBuildSettingsScene(ScenePath, true);
        scenes.Insert(0, home);
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    static void EnsureEventSystem()
    {
        if (Object.FindAnyObjectByType<EventSystem>() != null)
            return;

        var go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        Undo.RegisterCreatedObjectUndo(go, "Create EventSystem");
    }

    static GameObject CreateCanvas()
    {
        var go = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        go.layer = 5;
        Canvas canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        return go;
    }

    static GameObject CreatePanel(Transform parent)
    {
        var go = new GameObject("HomePanel", typeof(RectTransform), typeof(VerticalLayoutGroup));
        go.layer = 5;
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(420f, 280f);

        VerticalLayoutGroup layout = go.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(24, 24, 24, 24);
        layout.spacing = 16;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        return go;
    }

    static Text CreateText(Transform parent, string objectName, string value, float preferredHeight)
    {
        GameObject go = DefaultControls.CreateText(new DefaultControls.Resources());
        go.name = objectName;
        go.layer = 5;
        go.transform.SetParent(parent, false);
        LayoutElement layout = go.GetComponent<LayoutElement>();
        if (layout == null)
            layout = go.AddComponent<LayoutElement>();
        layout.preferredHeight = preferredHeight;
        Text label = go.GetComponent<Text>();
        label.text = value;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = Color.white;
        label.fontSize = 28;
        return label;
    }

    static Button CreateButton(Transform parent, string objectName, string label, Color color, DefaultControls.Resources resources)
    {
        var go = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        go.layer = 5;
        go.transform.SetParent(parent, false);
        go.GetComponent<LayoutElement>().preferredHeight = 64f;

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
        text.fontSize = 28;

        Button button = go.GetComponent<Button>();
        button.targetGraphic = image;
        return button;
    }
}
