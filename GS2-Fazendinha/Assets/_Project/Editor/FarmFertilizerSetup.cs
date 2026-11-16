using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class FarmFertilizerSetup
{
    private const string Root = "Assets/_Project/";

    public static void Apply()
    {
        var scene = EditorSceneManager.OpenScene(Root + "Scenes/MainLevel.unity");
        var originalTransforms = new Dictionary<Transform, string>();
        foreach (var root in scene.GetRootGameObjects())
            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                if (!(transform is RectTransform))
                    originalTransforms[transform] = EditorJsonUtility.ToJson(transform);

        var systems = GameObject.Find("GameSystems");
        var storage = systems.GetComponent<FarmFertilizerStorage>();
        if (storage == null) storage = systems.AddComponent<FarmFertilizerStorage>();
        var composter = systems.GetComponent<FarmWormComposter>();
        if (composter == null) composter = systems.AddComponent<FarmWormComposter>();
        var technology = ConfigureTechnology();
        var composterModel = ConfigureModel(scene.GetRootGameObjects());

        var storageData = new SerializedObject(storage);
        Connect(storageData, "wormComposter", composter);
        storageData.ApplyModifiedPropertiesWithoutUndo();
        var composterData = new SerializedObject(composter);
        Connect(composterData, "technologies", systems.GetComponent<FarmTechnologyManager>());
        Connect(composterData, "fertilizerStorage", storage);
        Connect(composterData, "unlockTechnology", technology);
        Connect(composterData, "composterModel", composterModel);
        composterData.ApplyModifiedPropertiesWithoutUndo();

        foreach (var plot in UnityEngine.Object.FindObjectsByType<FarmPlot>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var plotData = new SerializedObject(plot);
            Connect(plotData, "fertilizerStorage", storage);
            plotData.ApplyModifiedPropertiesWithoutUndo();
        }

        var shopData = new SerializedObject(systems.GetComponent<FarmShop>());
        var catalog = shopData.FindProperty("exclusiveTechnologyCatalog");
        bool listed = false;
        for (int index = 0; index < catalog.arraySize; index++)
            listed |= catalog.GetArrayElementAtIndex(index).objectReferenceValue == technology;
        if (!listed && technology.marketExclusive)
        {
            catalog.arraySize++;
            catalog.GetArrayElementAtIndex(catalog.arraySize - 1).objectReferenceValue = technology;
            shopData.ApplyModifiedPropertiesWithoutUndo();
        }

        var canvas = GameObject.Find("Main Canvas").transform;
        var hud = canvas.GetComponent<FarmHUD>();
        var slider = CreateFertilizerSlider(canvas);
        var fertilizerText = slider.GetComponentInChildren<TMP_Text>(true);
        fertilizerText.enableAutoSizing = false;
        fertilizerText.fontSize = 22f;
        slider.transition = Selectable.Transition.None;
        slider.transform.SetSiblingIndex(canvas.Find("WaterRegenerationTimerText").GetSiblingIndex() + 1);
        var timer = canvas.Find("FertilizerGenerationTimerText");
        if (timer == null)
        {
            timer = UnityEngine.Object.Instantiate(canvas.Find("WaterRegenerationTimerText").gameObject, canvas, false).transform;
            timer.name = "FertilizerGenerationTimerText";
            var rect = (RectTransform)timer;
            rect.anchoredPosition = new Vector2(-24f, -250f);
            rect.sizeDelta = new Vector2(300f, 28f);
            timer.GetComponent<TMP_Text>().fontSize = 16f;
        }
        timer.GetComponent<TMP_Text>().enableAutoSizing = false;
        timer.GetComponent<TMP_Text>().fontSize = 16f;
        timer.SetSiblingIndex(slider.transform.GetSiblingIndex() + 1);
        timer.gameObject.SetActive(false);
        var hudData = new SerializedObject(hud);
        Connect(hudData, "fertilizerStorage", storage);
        Connect(hudData, "wormComposter", composter);
        Connect(hudData, "fertilizerAmountSlider", slider);
        Connect(hudData, "fertilizerAmountText", slider.GetComponentInChildren<TMP_Text>(true));
        Connect(hudData, "fertilizerGenerationTimerText", timer.GetComponent<TMP_Text>());
        hudData.ApplyModifiedPropertiesWithoutUndo();

        var menu = canvas.GetComponent<FarmPlotContextMenu>();
        var panel = canvas.Find("CropActionPanel");
        var buttonTransform = panel.Find("FertilizeButton");
        if (buttonTransform == null)
        {
            buttonTransform = UnityEngine.Object.Instantiate(panel.Find("WaterButton").gameObject, panel, false).transform;
            buttonTransform.name = "FertilizeButton";
        }
        var button = buttonTransform.GetComponent<Button>();
        button.onClick = new Button.ButtonClickedEvent();
        UnityEventTools.AddPersistentListener(button.onClick, menu.Fertilize);
        UnityEventTools.AddPersistentListener(button.onClick, hud.PlayUIClick);
        var buttonLabel = buttonTransform.GetComponentInChildren<TMP_Text>(true);
        buttonLabel.name = "FertilizeButtonLabel";
        buttonLabel.text = "Fertilize (20)";
        ((RectTransform)buttonTransform).anchoredPosition = new Vector2(20f, -242f);
        ((RectTransform)panel.Find("WaterButton")).anchoredPosition = new Vector2(20f, -184f);
        ((RectTransform)panel.Find("HarvestButton")).anchoredPosition = new Vector2(20f, -184f);
        var status = panel.Find("CropStatusText").GetComponent<TMP_Text>();
        status.rectTransform.sizeDelta = new Vector2(300f, 110f);
        status.fontSize = 18f;
        var menuData = new SerializedObject(menu);
        Connect(menuData, "fertilizerStorage", storage);
        Connect(menuData, "fertilizeButton", button);
        Connect(menuData, "fertilizeButtonText", buttonLabel);
        menuData.ApplyModifiedPropertiesWithoutUndo();
        panel.gameObject.SetActive(false);

        foreach (var entry in originalTransforms)
            if (EditorJsonUtility.ToJson(entry.Key) != entry.Value)
                throw new Exception("Original scene transform changed: " + entry.Key.name);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Directory.CreateDirectory("../../Verification/Fertilizer");
        File.WriteAllText("../../Verification/Fertilizer/Setup.txt",
            "Fertilizer storage, HUD, four plots, contextual action and seasonal technology connected.\n" +
            "All existing non-UI transforms preserved.\n" +
            "Composter position: " + composterModel.transform.position + "\n" +
            "Composter starts hidden; fertilizer starts at zero.\n");
        Debug.Log("Fertilizer setup completed.");
    }

    private static TechnologyData ConfigureTechnology()
    {
        const string path = Root + "Tech/FarmUpgrades/Worm Composter.asset";
        var technology = AssetDatabase.LoadAssetAtPath<TechnologyData>(path);
        if (technology == null)
            technology = AssetDatabase.LoadAssetAtPath<TechnologyData>(Root + "Tech/Market/Worm Composter.asset");
        if (technology != null) return technology;

        if (!AssetDatabase.IsValidFolder(Root + "Tech/FarmUpgrades"))
            AssetDatabase.CreateFolder(Root + "Tech", "FarmUpgrades");

        technology = ScriptableObject.CreateInstance<TechnologyData>();
        technology.effect = TechnologyEffect.WormComposter;
        technology.value = 0.35f;
        technology.price = 150;
        technology.marketExclusive = false;
        var data = new SerializedObject(technology);
        data.FindProperty("displayName").stringValue = "Worm Composter";
        data.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.CreateAsset(technology, path);
        return technology;
    }

    private static GameObject ConfigureModel(GameObject[] roots)
    {
        foreach (var root in roots)
            if (root.name == "WormComposter") return root;

        const string materialPath = Root + "Materials/WormComposter.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.name = "WormComposter";
            material.color = new Color32(111, 74, 45, 255);
            material.enableInstancing = true;
            AssetDatabase.CreateAsset(material, materialPath);
        }
        var model = GameObject.CreatePrimitive(PrimitiveType.Cube);
        model.name = "WormComposter";
        var ground = GameObject.Find("Ground").GetComponent<Renderer>().bounds;
        var player = GameObject.Find("Player").transform.position;
        model.transform.position = new Vector3(ground.max.x - 1.5f, ground.max.y + 0.5f, player.z - 2f);
        model.GetComponent<Renderer>().sharedMaterial = material;
        UnityEngine.Object.DestroyImmediate(model.GetComponent<Collider>());
        model.SetActive(false);
        return model;
    }

    private static Slider CreateFertilizerSlider(Transform canvas)
    {
        var existing = canvas.Find("FertilizerSlider");
        if (existing != null) return existing.GetComponent<Slider>();

        var rect = new GameObject("FertilizerSlider", typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(canvas, false);
        rect.gameObject.layer = 5;
        rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one;
        rect.anchoredPosition = new Vector2(-24f, -190f);
        rect.sizeDelta = new Vector2(300f, 54f);
        var background = rect.gameObject.AddComponent<Image>();
        background.color = new Color32(49, 30, 22, 255);
        background.raycastTarget = false;
        var fillArea = new GameObject("FertilizerFillArea", typeof(RectTransform)).GetComponent<RectTransform>();
        fillArea.SetParent(rect, false);
        fillArea.gameObject.layer = 5;
        fillArea.anchorMin = Vector2.zero;
        fillArea.anchorMax = Vector2.one;
        fillArea.offsetMin = new Vector2(3f, 3f);
        fillArea.offsetMax = new Vector2(-3f, -3f);
        var fill = new GameObject("FertilizerFill", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
        fill.SetParent(fillArea, false);
        fill.gameObject.layer = 5;
        fill.anchorMin = Vector2.zero;
        fill.anchorMax = Vector2.one;
        fill.offsetMin = Vector2.zero;
        fill.offsetMax = Vector2.zero;
        fill.GetComponent<Image>().color = new Color32(155, 99, 49, 255);
        fill.GetComponent<Image>().raycastTarget = false;
        var slider = rect.gameObject.AddComponent<Slider>();
        slider.fillRect = fill;
        slider.minValue = 0f;
        slider.maxValue = 100f;
        slider.value = 0f;
        slider.interactable = false;
        slider.navigation = new Navigation { mode = Navigation.Mode.None };

        var text = UnityEngine.Object.Instantiate(canvas.Find("WaterSlider/WaterSliderText").gameObject, rect, false).GetComponent<TMP_Text>();
        text.name = "FertilizerAmountText";
        text.rectTransform.anchorMin = Vector2.zero;
        text.rectTransform.anchorMax = Vector2.one;
        text.rectTransform.offsetMin = new Vector2(8f, 4f);
        text.rectTransform.offsetMax = new Vector2(-8f, -4f);
        text.fontSize = 22f;
        text.alignment = TextAlignmentOptions.Center;
        text.color = new Color32(255, 235, 197, 255);
        text.raycastTarget = false;
        text.text = "Fertilizer: 0/100";
        return slider;
    }

    private static void Connect(SerializedObject target, string field, UnityEngine.Object value) =>
        target.FindProperty(field).objectReferenceValue = value;
}
