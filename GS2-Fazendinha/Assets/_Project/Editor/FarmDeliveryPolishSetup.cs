using System.IO;
using System.Linq;
using TMPro;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

public static class FarmDeliveryPolishSetup
{
    private const string Root = "Assets/_Project/";
    public const string PreviewPrefix = "GoldenRootPreviewV2";
    private static readonly Color Ink = new Color32(61, 36, 20, 255);
    private static readonly Color Cream = new Color32(255, 244, 219, 255);

    public static void Apply()
    {
        Directory.CreateDirectory("../../Verification/DeliveryPolish");
        ImportLogo();
        BlockBarnAndRebuildNavigation();
        MenuFarmBackdropSetup.Apply();
        foreach (string name in new[] { "MainLevel", "Tutorial", "MainMenu" })
        {
            var scene = EditorSceneManager.OpenScene(Root + "Scenes/" + name + ".unity");
            var menus = Object.FindFirstObjectByType<FarmUIController>();
            var canvas = menus.transform;
            StyleTextAndButtons(canvas);
            ConfigureOptions(menus, name == "MainMenu");
            if (name == "MainMenu") ConfigureMainMenu(canvas);
            else
            {
                ConfigurePause(menus);
                ConfigureHud(canvas.GetComponent<FarmHUD>());
            }
            ConfigureResponsivePanels(canvas);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        ConfigureResetCatalogs();
        EditorSceneManager.OpenScene(Root + "Scenes/MainMenu.unity");
        Object.FindFirstObjectByType<FarmSaveManager>().ResetProgress();
        File.WriteAllText("../../Verification/DeliveryPolish/ProfileReset.txt", "PASS: isolated GoldenRootPreviewV2 progress reset; legacy profile and audio settings preserved.\n");
        AssetDatabase.SaveAssets();
        FarmToonPreviewSetup.CreateMaterials();
        File.WriteAllText("../../Verification/DeliveryPolish/Setup.txt", "PASS: menu, HUD, reset, barn collision and navigation configured. Toon materials prepared separately.\n");
    }

    private static void ImportLogo()
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(Root + "UI/Logo.png");
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.SaveAndReimport();
    }

    private static void BlockBarnAndRebuildNavigation()
    {
        var scene = EditorSceneManager.OpenScene(Root + "Scenes/MainLevel.unity");
        var barn = GameObject.Find("FarmBarn");
        var bounds = FarmCropPrefabRefresh.BoundsOf(barn);
        var old = barn.transform.Find("BarnSolidCollider");
        if (old != null) Object.DestroyImmediate(old.gameObject);
        var solid = new GameObject("BarnSolidCollider").transform;
        solid.position = bounds.center;
        solid.SetParent(barn.transform, true);
        solid.gameObject.AddComponent<BoxCollider>().size = bounds.size;
        var volume = solid.gameObject.AddComponent<NavMeshModifierVolume>();
        volume.center = Vector3.zero;
        volume.size = bounds.size + new Vector3(0.1f, 0.8f, 0.1f);
        volume.area = NavMesh.GetAreaFromName("Not Walkable");

        var composter = Object.FindFirstObjectByType<FarmWormComposter>();
        var model = (GameObject)new SerializedObject(composter).FindProperty("composterModel").objectReferenceValue;
        if (model != null)
        {
            var modelBounds = FarmCropPrefabRefresh.BoundsOf(model);
            var obstacle = model.GetComponent<NavMeshObstacle>();
            if (obstacle == null) obstacle = model.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.center = model.transform.InverseTransformPoint(modelBounds.center);
            obstacle.size = Divide(modelBounds.size, model.transform.lossyScale);
            obstacle.carving = true;
            var collider = model.GetComponent<BoxCollider>();
            if (collider == null) collider = model.AddComponent<BoxCollider>();
            collider.center = obstacle.center;
            collider.size = obstacle.size;
        }
        var surface = Object.FindFirstObjectByType<NavMeshSurface>();
        surface.BuildNavMesh();
        string path = Root + "Scenes/MainLevel/NavMesh-Navigation.asset";
        var data = AssetDatabase.LoadAssetAtPath<NavMeshData>(path);
        if (data == null) AssetDatabase.CreateAsset(surface.navMeshData, path);
        else
        {
            var generated = surface.navMeshData;
            surface.RemoveData();
            EditorUtility.CopySerialized(generated, data);
            surface.navMeshData = data;
            surface.AddData();
            EditorUtility.SetDirty(data);
        }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
    }

    private static Vector3 Divide(Vector3 value, Vector3 scale) => new Vector3(value.x / Mathf.Abs(scale.x), value.y / Mathf.Abs(scale.y), value.z / Mathf.Abs(scale.z));

    private static void ConfigureResetCatalogs()
    {
        var ids = new System.Collections.Generic.List<string>();
        foreach (string name in new[] { "MainLevel", "Tutorial" })
        {
            EditorSceneManager.OpenScene(Root + "Scenes/" + name + ".unity");
            ids.AddRange(Object.FindObjectsByType<FarmPlot>(FindObjectsInactive.Include, FindObjectsSortMode.None).Select(plot => plot.SaveId));
        }
        foreach (string name in new[] { "MainLevel", "Tutorial", "MainMenu" })
        {
            var scene = EditorSceneManager.OpenScene(Root + "Scenes/" + name + ".unity");
            var manager = new SerializedObject(Object.FindFirstObjectByType<FarmSaveManager>());
            manager.FindProperty("saveKeyPrefix").stringValue = PreviewPrefix;
            Array(manager, "resetCrops", Assets<CropData>());
            Array(manager, "resetMissions", Assets<MissionData>());
            Array(manager, "resetCommunities", Assets<FarmCommunityData>());
            Array(manager, "technologyCatalog", Assets<TechnologyData>());
            var plots = manager.FindProperty("resetPlotIds");
            plots.arraySize = ids.Count;
            for (int index = 0; index < ids.Count; index++) plots.GetArrayElementAtIndex(index).stringValue = ids[index];
            manager.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }

    private static void StyleTextAndButtons(Transform canvas)
    {
        foreach (var button in canvas.GetComponentsInChildren<Button>(true))
        {
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color32(255, 244, 200, 255);
            colors.pressedColor = new Color32(218, 183, 126, 255);
            colors.disabledColor = new Color(0.7f, 0.65f, 0.55f, 0.65f);
            colors.fadeDuration = 0.1f;
            button.colors = colors;
            foreach (var text in button.GetComponentsInChildren<TMP_Text>(true))
            {
                text.enableAutoSizing = true;
                text.fontSizeMin = 18f;
                text.fontSizeMax = 24f;
                text.color = Ink;
                text.raycastTarget = false;
                text.margin = new Vector4(5f, 2f, 5f, 2f);
            }
        }
        foreach (var dropdown in canvas.GetComponentsInChildren<TMP_Dropdown>(true))
        {
            foreach (var text in new[] { dropdown.captionText, dropdown.itemText })
            {
                if (text == null) continue;
                text.color = Ink;
                text.enableAutoSizing = true;
                text.fontSizeMin = 18f;
                text.fontSizeMax = 23f;
            }
        }
        foreach (var text in canvas.GetComponentsInChildren<TMP_Text>(true))
        {
            if (text.name.Contains("Title"))
            {
                text.fontStyle = FontStyles.Bold;
                text.color = Ink;
            }
            if (text.name.Contains("Details") || text.name.Contains("Description") || text.name.Contains("Instruction"))
            {
                text.color = Ink;
                text.enableAutoSizing = true;
                text.fontSizeMin = 18f;
                text.fontSizeMax = Mathf.Max(21f, text.fontSize);
                text.margin = new Vector4(4f, 4f, 4f, 4f);
            }
            if (text.text.Contains("VIEW REQUEST")) text.text = text.text.Replace("VIEW REQUEST", "VIEW MISSION");
        }
    }

    private static void ConfigureMainMenu(Transform canvas)
    {
        var panel = Find(canvas, "MainMenuPanel");
        foreach (string name in new[] { "GameTitle", "PlayButton", "PlayLabel", "MainOptionsButton", "MainQuitButton" })
        {
            var item = Find(panel, name);
            if (item != null) item.SetParent(panel, false);
        }
        Delete(panel, "MainMenuBoard");
        var board = FarmMenuSetup.Panel(panel, "MainMenuBoard", 52f, 48f, 472f, 804f);
        var logo = FarmMenuSetup.FullRect(board, "GameLogo");
        FarmMenuSetup.Place(logo, 119f, 20f, 234f, 160f);
        var image = logo.gameObject.AddComponent<Image>();
        image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Root + "UI/Logo.png");
        image.preserveAspect = true;
        image.raycastTarget = false;
        Reparent(Find(panel, "GameTitle"), board, 25f, 190f, 422f, 118f);
        var title = Find(board, "GameTitle").GetComponent<TMP_Text>();
        title.fontSize = 39f;
        title.color = Ink;
        title.alignment = TextAlignmentOptions.Center;
        title.enableAutoSizing = true;
        title.fontSizeMin = 34f;
        title.fontSizeMax = 39f;
        Reparent(Find(panel, "PlayButton"), board, 181f, 335f, 110f, 110f);
        Reparent(Find(panel, "PlayLabel"), board, 40f, 459f, 392f, 34f);
        Find(board, "PlayLabel").GetComponent<TMP_Text>().color = Ink;
        Reparent(Find(panel, "MainOptionsButton"), board, 61f, 530f, 350f, 58f);
        Reparent(Find(panel, "MainQuitButton"), board, 61f, 608f, 350f, 58f);
        FarmMenuSetup.Text(board, "MainMenuCaption", "Grow crops. Help communities.\nA farming demo", 35f, 718f, 402f, 58f, 19f, TextAlignmentOptions.Center).color = Ink;
        var shade = Find(canvas, "MenuBackdropShade");
        if (shade != null) shade.GetComponent<Image>().color = new Color(0.05f, 0.025f, 0.01f, 0.18f);
    }

    private static void ConfigureOptions(FarmUIController menus, bool mainMenu)
    {
        var canvas = menus.transform;
        var panel = (RectTransform)Find(canvas, "OptionsPanel");
        FarmMenuSetup.Place(panel, 475f, 165f, 650f, 570f);
        Place(canvas, "OptionsTitle", 34f, 30f, 582f, 48f);
        Find(canvas, "OptionsTitle").GetComponent<TMP_Text>().text = "SETTINGS";
        Place(canvas, "MusicLabel", 45f, 112f, 370f, 32f);
        Place(canvas, "MusicVolumeValue", 490f, 112f, 115f, 32f);
        Place(canvas, "MusicVolumeSlider", 45f, 158f, 560f, 34f);
        Place(canvas, "EffectsLabel", 45f, 230f, 370f, 32f);
        Place(canvas, "EffectsVolumeValue", 490f, 230f, 115f, 32f);
        Place(canvas, "EffectsVolumeSlider", 45f, 276f, 560f, 34f);
        Place(canvas, "CloseOptionsButton", 165f, 470f, 320f, 58f);
        Delete(panel, "AudioSaveHint");
        FarmMenuSetup.Text(panel, "AudioSaveHint", "Audio settings are saved automatically.", 35f, 332f, 580f, 30f, 18f, TextAlignmentOptions.Center);
        Delete(panel, "ResetProgressButton");
        if (!mainMenu) return;
        FarmMenuSetup.Button(panel, "ResetProgressButton", "RESET FARM PROGRESS", 75f, 387f, 500f, 52f, menus.ShowResetProgress);
        Delete(canvas, "ResetProgressOverlay");
        var overlay = FarmMenuSetup.Overlay(canvas, "ResetProgressOverlay");
        var card = FarmMenuSetup.Panel(overlay, "ResetProgressPanel", 470f, 230f, 660f, 440f);
        FarmMenuSetup.Text(card, "ResetProgressTitle", "START A NEW FARM?", 40f, 38f, 580f, 55f, 32f, TextAlignmentOptions.Center).fontStyle = FontStyles.Bold;
        FarmMenuSetup.Text(card, "ResetProgressDescription", "Coins, crops, deliveries and upgrades\nwill be reset.\nYour audio settings will be kept.", 40f, 117f, 580f, 128f, 23f, TextAlignmentOptions.Center);
        FarmMenuSetup.Button(card, "CancelResetProgressButton", "KEEP MY FARM", 45f, 310f, 270f, 60f, menus.CancelResetProgress);
        var reset = FarmMenuSetup.Button(card, "ConfirmResetProgressButton", "RESET FARM", 345f, 310f, 270f, 60f, menus.ConfirmResetProgress);
        reset.GetComponent<Image>().color = new Color32(185, 94, 68, 255);
        reset.GetComponentInChildren<TMP_Text>().color = Cream;
        var data = new SerializedObject(menus);
        data.FindProperty("resetConfirmationPanel").objectReferenceValue = overlay.gameObject;
        data.ApplyModifiedPropertiesWithoutUndo();
        overlay.gameObject.SetActive(false);
    }

    private static void ConfigurePause(FarmUIController menus)
    {
        var canvas = menus.transform;
        FarmMenuSetup.Place((RectTransform)Find(canvas, "PausePanel"), 550f, 165f, 500f, 570f);
        Place(canvas, "PauseTitle", 40f, 30f, 420f, 50f);
        string[] buttons = { "ResumeButton", "SaveGameButton", "PauseOptionsButton", "PauseQuitButton" };
        float[] tops = { 110f, 178f, 246f, 382f };
        for (int index = 0; index < buttons.Length; index++) Place(canvas, buttons[index], 70f, tops[index], 360f, 54f);
        var panel = Find(canvas, "PausePanel");
        Delete(panel, "PauseMainMenuButton");
        FarmMenuSetup.Button(panel, "PauseMainMenuButton", "MAIN MENU", 70f, 314f, 360f, 54f, menus.ReturnToMainMenu);
        Place(canvas, "PauseHint", 35f, 468f, 430f, 40f);
    }

    private static void ConfigureHud(FarmHUD hud)
    {
        var data = new SerializedObject(hud);
        ResourceCard(hud.transform, "WaterHudCard", "WATER", "IconWater", data, "waterAmountSlider", "waterAmountText", "waterRegenerationTimerText", 80f);
        ResourceCard(hud.transform, "FertilizerHudCard", "FERTILIZER", "IconFertilizer", data, "fertilizerAmountSlider", "fertilizerAmountText", "fertilizerGenerationTimerText", 202f);
        var coins = (TMP_Text)data.FindProperty("coinCounterText").objectReferenceValue;
        var previous = coins.transform.parent;
        bool hidePrevious = previous != hud.transform && previous.GetComponent<Canvas>() == null && previous.GetComponentsInChildren<TMP_Text>(true).Length == 1;
        coins.transform.SetParent(hud.transform, false);
        if (hidePrevious) previous.gameObject.SetActive(false);
        Delete(hud.transform, "CoinsHudCard");
        var card = FarmMenuSetup.Panel(hud.transform, "CoinsHudCard", 24f, 24f, 245f, 60f);
        var icon = FarmMenuSetup.FullRect(card, "MoneyIcon");
        FarmMenuSetup.Place(icon, 12f, 10f, 44f, 40f);
        var image = icon.gameObject.AddComponent<Image>();
        image.sprite = FarmMenuSetup.Sprite("IconMoney"); image.preserveAspect = true; image.raycastTarget = false;
        Reparent(coins.transform, card, 65f, 13f, 165f, 34f);
        coins.fontSize = 23f; coins.enableAutoSizing = true; coins.fontSizeMin = 18f; coins.fontSizeMax = 23f; coins.color = Ink;
        var message = (TMP_Text)data.FindProperty("actionMessageText").objectReferenceValue;
        if (message != null) { message.color = Cream; message.fontSize = 20f; }
    }

    private static void ResourceCard(Transform canvas, string name, string label, string iconName, SerializedObject hud,
        string sliderField, string amountField, string timerField, float top)
    {
        var slider = (Slider)hud.FindProperty(sliderField).objectReferenceValue;
        var number = (TMP_Text)hud.FindProperty(amountField).objectReferenceValue;
        var timer = (TMP_Text)hud.FindProperty(timerField).objectReferenceValue;
        var oldIcon = Find(slider.transform, iconName);
        if (oldIcon != null) oldIcon.gameObject.SetActive(false);
        if (label == "WATER")
        {
            var legacyIcon = Find(canvas, "WaterIcon");
            if (legacyIcon != null) legacyIcon.gameObject.SetActive(false);
        }
        slider.transform.SetParent(canvas, false);
        timer.transform.SetParent(canvas, false);
        Delete(canvas, name);
        Delete(slider.transform, "ResourceNumberPlate");
        var card = FarmMenuSetup.Panel(canvas, name, 0f, 0f, 280f, 110f);
        card.anchorMin = card.anchorMax = card.pivot = Vector2.one;
        card.anchoredPosition = new Vector2(-24f, -top);
        Reparent(slider.transform, card, 70f, 39f, 192f, 30f);
        slider.transform.localScale = Vector3.one;
        slider.interactable = false;
        slider.transition = Selectable.Transition.None;
        var fill = slider.fillRect;
        slider.fillRect = null;
        if (fill != null)
        {
            fill.SetParent(slider.transform, false);
            fill.anchorMin = Vector2.zero; fill.anchorMax = Vector2.one;
            fill.offsetMin = new Vector2(4f, 4f); fill.offsetMax = new Vector2(-4f, -4f);
        }
        slider.fillRect = fill;
        foreach (var graphic in slider.GetComponentsInChildren<Image>(true))
            if (graphic.transform != slider.transform && graphic.transform != fill) graphic.enabled = false;
        Reparent(number.transform, slider.transform, 0f, 0f, 192f, 30f);
        number.transform.localScale = Vector3.one;
        number.transform.SetAsLastSibling();
        number.enableAutoSizing = false; number.fontSize = 19f; number.fontSizeMin = number.fontSizeMax = 19f;
        number.color = Cream; number.alignment = TextAlignmentOptions.Center;
        var plate = FarmMenuSetup.FullRect(slider.transform, "ResourceNumberPlate");
        FarmMenuSetup.Place(plate, 25f, 2f, 142f, 26f);
        var plateImage = plate.gameObject.AddComponent<Image>();
        plateImage.color = new Color(0.16f, 0.09f, 0.045f, 0.84f); plateImage.raycastTarget = false;
        plate.SetSiblingIndex(number.transform.GetSiblingIndex());
        number.transform.SetAsLastSibling();
        var icon = FarmMenuSetup.FullRect(card, "ResourceIcon");
        FarmMenuSetup.Place(icon, 12f, 26f, 47f, 55f);
        var image = icon.gameObject.AddComponent<Image>();
        image.sprite = FarmMenuSetup.Sprite(iconName); image.preserveAspect = true; image.raycastTarget = false;
        FarmMenuSetup.Text(card, "ResourceTitle", label, 72f, 10f, 190f, 25f, 18f).fontStyle = FontStyles.Bold;
        Reparent(timer.transform, card, 12f, 79f, 256f, 25f);
        timer.transform.localScale = Vector3.one;
        timer.color = Ink; timer.fontSize = 17f; timer.alignment = TextAlignmentOptions.Center;
    }

    private static void ConfigureResponsivePanels(Transform canvas)
    {
        // Os painéis precisam ficar acima dos cartões de recursos criados no HUD.
        foreach (string name in new[] { "MapPanel", "ShopPanel", "TechnologiesPanel", "FirstCommunityPanel", "SecondCommunityPanel", "ThirdCommunityPanel", "DemoEndPanel", "PausePanel", "OptionsPanel", "ResetProgressOverlay" })
        {
            var overlay = Find(canvas, name);
            if (overlay == null) continue;
            overlay.SetAsLastSibling();
            var layer = overlay;
            while (layer.parent != null && layer.parent != canvas) layer = layer.parent;
            layer.SetAsLastSibling();
        }
        canvas.GetComponent<CanvasScaler>().matchWidthOrHeight = 1f;
        var panels = new System.Collections.Generic.List<RectTransform>();
        foreach (string name in new[] { "OptionsPanel", "PausePanel", "ResetProgressPanel", "MapPanel", "ShopPanel", "TechnologiesPanel",
            "FirstCommunityPanel", "SecondCommunityPanel", "ThirdCommunityPanel", "DemoEndPanel" })
        {
            var panel = Find(canvas, name) as RectTransform;
            if (panel == null) continue;
            panel.anchorMin = panel.anchorMax = panel.pivot = Vector2.one * 0.5f;
            panel.anchoredPosition = Vector2.zero;
            panels.Add(panel);
        }
        var board = Find(canvas, "MainMenuBoard") as RectTransform;
        if (board != null)
        {
            board.anchorMin = board.anchorMax = board.pivot = new Vector2(0f, 0.5f);
            board.anchoredPosition = new Vector2(52f, 0f);
            panels.Add(board);
        }
        var component = canvas.GetComponent<FarmPanelLayout>();
        if (component == null) component = canvas.gameObject.AddComponent<FarmPanelLayout>();
        var data = new SerializedObject(component);
        data.FindProperty("canvasRect").objectReferenceValue = canvas;
        var list = data.FindProperty("panels");
        list.arraySize = panels.Count;
        for (int index = 0; index < panels.Count; index++) list.GetArrayElementAtIndex(index).objectReferenceValue = panels[index];
        data.ApplyModifiedPropertiesWithoutUndo();
        var footer = Find(canvas, "Footer") as RectTransform;
        if (footer == null) return;
        footer.anchorMin = new Vector2(0f, 0f); footer.anchorMax = new Vector2(1f, 0f); footer.pivot = new Vector2(0.5f, 0f);
        footer.anchoredPosition = Vector2.zero; footer.sizeDelta = new Vector2(-40f, 76f);
        var map = Find(footer, "MapTab") as RectTransform;
        if (map != null) { map.anchorMin = map.anchorMax = map.pivot = Vector2.one; map.anchoredPosition = new Vector2(-240f, -4f); map.sizeDelta = Vector2.one * 68f; }
        var upgrades = Find(footer, "TechnologiesTab") as RectTransform;
        if (upgrades != null) { upgrades.anchorMin = upgrades.anchorMax = upgrades.pivot = Vector2.one; upgrades.anchoredPosition = new Vector2(-14f, -9f); upgrades.sizeDelta = new Vector2(210f, 58f); }
        var hud = new SerializedObject(canvas.GetComponent<FarmHUD>());
        var message = (TMP_Text)hud.FindProperty("actionMessageText").objectReferenceValue;
        if (message != null)
        {
            message.transform.SetParent(footer, false);
            var rect = message.rectTransform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(18f, 10f); rect.offsetMax = new Vector2(-330f, -10f);
            message.enableAutoSizing = true; message.fontSizeMin = 18f; message.fontSizeMax = 20f;
        }
    }

    private static Transform Find(Transform root, string name) => root.GetComponentsInChildren<Transform>(true).FirstOrDefault(item => item.name == name);
    private static void Delete(Transform root, string name) { var item = Find(root, name); if (item != null) Object.DestroyImmediate(item.gameObject); }
    private static void Place(Transform root, string name, float x, float y, float w, float h) => FarmMenuSetup.Place((RectTransform)Find(root, name), x, y, w, h);
    private static void Reparent(Transform item, Transform parent, float x, float y, float w, float h) { item.SetParent(parent, false); FarmMenuSetup.Place((RectTransform)item, x, y, w, h); }
    private static Object[] Assets<T>() where T : Object => AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { Root }).Select(guid => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid))).Cast<Object>().ToArray();
    private static void Array(SerializedObject data, string field, Object[] values) { var array = data.FindProperty(field); array.arraySize = values.Length; for (int index = 0; index < values.Length; index++) array.GetArrayElementAtIndex(index).objectReferenceValue = values[index]; }
}
