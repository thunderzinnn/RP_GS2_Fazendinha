using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

using UnityEngine.UI;
using UnityEngine.SceneManagement;

public static class FarmMenuSetup
{
    public const string Root = "Assets/_Project/";
    public static readonly Color Ink = new Color32(76, 43, 24, 255);

    public static void Apply()
    {
        Directory.CreateDirectory("../../Verification/Presentation");
        Directory.CreateDirectory(Root + "Prefabs/Systems");
        AssetDatabase.Refresh();
        FarmUIRefresh.NormalizeButtonSprites();
        EditorSceneManager.OpenScene(Root + "Scenes/MainMenu.unity");
        var audioTemplate = Object.Instantiate(Object.FindFirstObjectByType<AudioManager>().gameObject);
        var preview = EditorSceneManager.NewPreviewScene();
        SceneManager.MoveGameObjectToScene(audioTemplate, preview);
        var scene = EditorSceneManager.OpenScene(Root + "Scenes/MainLevel.unity");
        FarmPresentationPolish.Apply(scene);
        var canvas = GameObject.Find("Main Canvas").transform;
        BuildController(canvas, false);
        var eventPrefab = PrefabUtility.SaveAsPrefabAsset(Object.FindFirstObjectByType<EventSystem>().gameObject,
            Root + "Prefabs/Systems/FarmEventSystem.prefab");
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        FarmTutorialSetup.Build();
        BuildMainMenu(audioTemplate, eventPrefab);
        EditorSceneManager.ClosePreviewScene(preview);
        var current = EditorBuildSettings.scenes.Where(item => item.path != Root + "Scenes/MainMenu.unity" &&
            item.path != Root + "Scenes/Tutorial.unity" && item.path != Root + "Scenes/MainLevel.unity");
        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene(Root + "Scenes/MainMenu.unity", true),
            new EditorBuildSettingsScene(Root + "Scenes/Tutorial.unity", true),
            new EditorBuildSettingsScene(Root + "Scenes/MainLevel.unity", true)
        }.Concat(current).ToArray();
        FarmSaveSetup.Connect();
        AssetDatabase.SaveAssets();
        FarmSystemChecks.RunPresentation();
    }

    public static FarmUIController BuildController(Transform canvas, bool mainMenu)
    {
        var controller = canvas.GetComponent<FarmUIController>();
        if (controller == null) controller = canvas.gameObject.AddComponent<FarmUIController>();
        foreach (string name in new[] { "PauseOverlay", "OptionsOverlay" })
        {
            var old = canvas.Find(name);
            if (old != null) Object.DestroyImmediate(old.gameObject);
        }
        var pause = Overlay(canvas, "PauseOverlay");
        var pausePanel = Panel(pause, "PausePanel", 570f, 205f, 460f, 460f);
        Text(pausePanel, "PauseTitle", "PAUSED", 30f, 28f, 400f, 48f, 32f, TextAlignmentOptions.Center);
        Button(pausePanel, "ResumeButton", "RESUME", 60f, 110f, 340f, 56f, controller.ResumeGame, "ButtonWoodGreen");
        Button(pausePanel, "PauseOptionsButton", "OPTIONS", 60f, 190f, 340f, 56f, controller.OpenOptions);
        Button(pausePanel, "PauseQuitButton", "QUIT", 60f, 270f, 340f, 56f, controller.QuitGame);
        Text(pausePanel, "PauseHint", "Esc to resume", 30f, 365f, 400f, 30f, 18f, TextAlignmentOptions.Center);

        var options = Overlay(canvas, "OptionsOverlay");
        var optionsPanel = Panel(options, "OptionsPanel", 480f, 210f, 640f, 460f);
        Text(optionsPanel, "OptionsTitle", "OPTIONS", 32f, 30f, 576f, 48f, 32f, TextAlignmentOptions.Center);
        Text(optionsPanel, "MusicLabel", "Music", 44f, 110f, 370f, 30f, 23f);
        var musicValue = Text(optionsPanel, "MusicVolumeValue", "70%", 480f, 110f, 115f, 30f, 23f, TextAlignmentOptions.Right);
        var music = VolumeSlider(optionsPanel, "MusicVolumeSlider", 44f, 155f, 550f, controller.SetMusicVolume);
        Text(optionsPanel, "EffectsLabel", "Sound effects", 44f, 220f, 370f, 30f, 23f);
        var effectsValue = Text(optionsPanel, "EffectsVolumeValue", "100%", 480f, 220f, 115f, 30f, 23f, TextAlignmentOptions.Right);
        var effects = VolumeSlider(optionsPanel, "EffectsVolumeSlider", 44f, 265f, 550f, controller.SetEffectsVolume);
        Button(optionsPanel, "CloseOptionsButton", "BACK", 170f, 355f, 300f, 56f, controller.CloseOptions);
        var data = new SerializedObject(controller);
        data.FindProperty("mainMenu").boolValue = mainMenu;
        Ref(data, "pausePanel", pause.gameObject);
        Ref(data, "optionsPanel", options.gameObject);
        Ref(data, "musicSlider", music);
        Ref(data, "effectsSlider", effects);
        Ref(data, "musicValueText", musicValue);
        Ref(data, "effectsValueText", effectsValue);
        data.ApplyModifiedPropertiesWithoutUndo();
        pause.gameObject.SetActive(false);
        options.gameObject.SetActive(false);
        if (!mainMenu)
        {
            var previous = canvas.Find("PauseButton");
            if (previous != null) Object.DestroyImmediate(previous.gameObject);
            var pauseButton = IconButton(canvas, "PauseButton", "Button", "PauseButton", 1178f, 821f, 68f, controller.PauseGame);
            pauseButton.transform.SetAsLastSibling();
            pause.SetAsLastSibling();
            options.SetAsLastSibling();
            FarmRefinementSetup.ApplyHUD(canvas);
        }
        return controller;
    }

    private static void BuildMainMenu(GameObject audioTemplate, GameObject eventPrefab)
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var camera = new GameObject("MenuCamera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
        camera.tag = "MainCamera";
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color32(246, 226, 184, 255);
        var canvas = new GameObject("MainMenuCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1600f, 900f);
        scaler.matchWidthOrHeight = 0.5f;
        var menu = FullRect(canvas.transform, "MainMenuPanel");
        Text(menu, "GameTitle", "GOLDEN ROOT\nQUICK FARMER", 300f, 140f, 1000f, 150f, 52f, TextAlignmentOptions.Center).fontStyle = FontStyles.Bold;
        var controller = BuildController(canvas.transform, true);
        IconButton(menu, "PlayButton", "Button02", "PlayButton", 745f, 340f, 110f, controller.PlayGame);
        Text(menu, "PlayLabel", "PLAY", 650f, 465f, 300f, 40f, 26f, TextAlignmentOptions.Center);
        Button(menu, "MainOptionsButton", "OPTIONS", 650f, 550f, 300f, 56f, controller.OpenOptions);
        Button(menu, "MainQuitButton", "QUIT", 650f, 630f, 300f, 56f, controller.QuitGame);
        var data = new SerializedObject(controller);
        Ref(data, "mainMenuPanel", menu.gameObject);
        data.ApplyModifiedPropertiesWithoutUndo();
        var audio = Object.Instantiate(audioTemplate);
        audio.name = "AudioManager";
        SceneManager.MoveGameObjectToScene(audio, scene);
        PrefabUtility.InstantiatePrefab(eventPrefab, scene);
        EditorSceneManager.SaveScene(scene, Root + "Scenes/MainMenu.unity");
    }

    public static Transform Find(Transform root, string name) => root.GetComponentsInChildren<Transform>(true).First(item => item.name == name);
    public static Sprite Sprite(string name) => AssetDatabase.LoadAllAssetsAtPath(Root + "UI/" + name + ".png").OfType<Sprite>().First();

    public static void Style(Image image, Sprite sprite, float multiplier)
    {
        image.sprite = sprite;
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = multiplier;
        image.color = Color.white;
    }

    public static RectTransform FullRect(Transform parent, string name)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.gameObject.layer = 5;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        return rect;
    }

    public static RectTransform Overlay(Transform parent, string name)
    {
        var rect = FullRect(parent, name);
        rect.gameObject.AddComponent<Image>().color = new Color(0.12f, 0.08f, 0.04f, 0.65f);
        return rect;
    }

    public static RectTransform Panel(Transform parent, string name, float x, float y, float width, float height)
    {
        var rect = FullRect(parent, name);
        Place(rect, x, y, width, height);
        Style(rect.gameObject.AddComponent<Image>(), Sprite("PanelWoodRounded"), 2f);
        return rect;
    }

    public static void Place(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
    }

    public static TMP_Text Text(Transform parent, string name, string value, float x, float y, float width, float height,
        float size, TextAlignmentOptions alignment = TextAlignmentOptions.Left)
    {
        var rect = FullRect(parent, name);
        Place(rect, x, y, width, height);
        var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.font = TMP_Settings.defaultFontAsset;
        text.text = value;
        text.fontSize = size;
        text.color = Ink;
        text.alignment = alignment;
        text.raycastTarget = false;
        return text;
    }

    public static Button Button(Transform parent, string name, string label, float x, float y, float width, float height,
        UnityAction action, string sprite = "ButtonWood")
    {
        var rect = FullRect(parent, name);
        Place(rect, x, y, width, height);
        var image = rect.gameObject.AddComponent<Image>();
        Style(image, Sprite(sprite), 4f);
        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        var text = Text(rect, name + "Label", label, 12f, 4f, width - 24f, height - 8f, 22f, TextAlignmentOptions.Center);
        text.enableAutoSizing = true;
        text.fontSizeMin = 16f;
        text.fontSizeMax = 22f;
        UnityEventTools.AddPersistentListener(button.onClick, action);
        return button;
    }

    public static Button IconButton(Transform parent, string name, string background, string icon, float x, float y,
        float size, UnityAction action)
    {
        var button = Button(parent, name, "", x, y, size, size, action);
        var image = button.GetComponent<Image>();
        image.sprite = Sprite(background);
        image.type = Image.Type.Simple;
        image.preserveAspect = true;
        var rect = FullRect(button.transform, "Icon");
        rect.offsetMin = Vector2.one * size * 0.24f;
        rect.offsetMax = -rect.offsetMin;
        var iconImage = rect.gameObject.AddComponent<Image>();
        iconImage.sprite = Sprite(icon);
        iconImage.preserveAspect = true;
        iconImage.raycastTarget = false;
        return button;
    }

    private static Slider VolumeSlider(Transform parent, string name, float x, float y, float width, UnityAction<float> action)
    {
        var rect = FullRect(parent, name);
        Place(rect, x, y, width, 34f);
        var background = rect.gameObject.AddComponent<Image>();
        Style(background, Sprite("ButtonWood"), 5f);
        var fill = FullRect(rect, "Fill");
        fill.offsetMin = new Vector2(8f, 8f);
        fill.offsetMax = new Vector2(-8f, -8f);
        Style(fill.gameObject.AddComponent<Image>(), Sprite("ButtonWoodGreen"), 5f);
        var handleArea = FullRect(rect, "HandleArea");
        handleArea.offsetMin = new Vector2(16f, 0f);
        handleArea.offsetMax = new Vector2(-16f, 0f);
        var handle = FullRect(handleArea, "Handle");
        handle.sizeDelta = new Vector2(38f, 0f);
        var handleImage = handle.gameObject.AddComponent<Image>();
        handleImage.sprite = Sprite("Button");
        handleImage.preserveAspect = true;
        var slider = rect.gameObject.AddComponent<Slider>();
        slider.fillRect = fill;
        slider.handleRect = handle;
        slider.targetGraphic = handleImage;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        UnityEventTools.AddPersistentListener(slider.onValueChanged, action);
        return slider;
    }

    public static void Ref(SerializedObject data, string name, Object value) => data.FindProperty(name).objectReferenceValue = value;
}
