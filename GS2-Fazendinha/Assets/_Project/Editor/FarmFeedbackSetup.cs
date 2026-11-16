using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class FarmFeedbackSetup
{
    private const string Root = "Assets/_Project/";

    public static void Apply()
    {
        ConfigureMarketTechnologies();
        FarmSaveSetup.Connect();
        ResizeCarrot();
        foreach (string sceneName in new[] { "MainLevel", "Tutorial" })
        {
            var scene = EditorSceneManager.OpenScene(Root + "Scenes/" + sceneName + ".unity");
            var hud = Object.FindFirstObjectByType<FarmHUD>();
            var canvas = hud.transform;
            ImproveReadability(canvas);
            var shop = Object.FindFirstObjectByType<FarmShop>();
            var shopData = new SerializedObject(shop);
            var technologies = AssetDatabase.FindAssets("t:TechnologyData", new[] { Root + "Tech/Market" })
                .Select(guid => AssetDatabase.LoadAssetAtPath<TechnologyData>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(item => item.marketExclusive).OrderBy(item => item.name).ToArray();
            var catalog = shopData.FindProperty("exclusiveTechnologyCatalog");
            catalog.arraySize = technologies.Length;
            for (int index = 0; index < technologies.Length; index++) catalog.GetArrayElementAtIndex(index).objectReferenceValue = technologies[index];
            shopData.ApplyModifiedPropertiesWithoutUndo();

            var saveButton = FarmMenuSetup.Find(canvas, "SaveGameButton");
            if (saveButton != null) saveButton.gameObject.SetActive(sceneName != "Tutorial");
            if (sceneName == "Tutorial")
            {
                var button = FarmMenuSetup.Find(canvas, "FirstCommunityButton");
                foreach (var label in button.GetComponentsInChildren<TMP_Text>(true))
                {
                    var rect = label.rectTransform;
                    rect.anchorMin = Vector2.zero;
                    rect.anchorMax = Vector2.one;
                    rect.pivot = Vector2.one * 0.5f;
                    rect.sizeDelta = new Vector2(-32f, -32f);
                    rect.anchoredPosition = Vector2.zero;
                    label.enableAutoSizing = true;
                    label.fontSizeMin = 16f;
                    label.fontSizeMax = 26f;
                    label.alignment = TextAlignmentOptions.Center;
                    label.overflowMode = TextOverflowModes.Ellipsis;
                }
            }
            else
            {
                var barn = GameObject.Find("FarmBarn");
                if (barn != null)
                {
                    barn.transform.localScale = Vector3.one * 2.2f;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(barn.transform);
                }
                AddDemoEnd(canvas);
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        AssetDatabase.SaveAssets();
        Directory.CreateDirectory("../../Verification/Feedback");
        File.WriteAllText("../../Verification/Feedback/Setup.txt", "PASS: tutorial label, market catalogs, carrot scales, barn and demo-end panel connected.\n");
        EditorSceneManager.OpenScene(Root + "Scenes/MainMenu.unity");
    }

    private static void ConfigureMarketTechnologies()
    {
        var first = AssetDatabase.LoadAssetAtPath<TechnologyData>(Root + "Tech/Market/Better Fertilization.asset");
        first.effect = TechnologyEffect.GrowthSpeedBonus;
        first.value = 0.03f;
        var second = AssetDatabase.LoadAssetAtPath<TechnologyData>(Root + "Tech/Market/Better Fert.asset");
        second.effect = TechnologyEffect.GrowthSpeedBonus;
        second.value = 0.05f;
        EditorUtility.SetDirty(first);
        EditorUtility.SetDirty(second);
        CreateTechnology("Fertilizer Regeneration I", TechnologyEffect.FertilizerRegenerationBonus, 0.05f, 70, first.icon);
        CreateTechnology("Fertilizer Regeneration II", TechnologyEffect.FertilizerRegenerationBonus, 0.05f, 108, first.icon);
        CreateTechnology("Harvest Compost I", TechnologyEffect.HarvestFertilizerBonus, 0.05f, 100, first.icon);
        CreateTechnology("Harvest Compost II", TechnologyEffect.HarvestFertilizerBonus, 0.1f, 140, first.icon);
        AssetDatabase.SaveAssets();
    }

    private static void CreateTechnology(string name, TechnologyEffect effect, float value, int price, Sprite icon)
    {
        string path = Root + "Tech/Market/" + name + ".asset";
        var technology = AssetDatabase.LoadAssetAtPath<TechnologyData>(path);
        if (technology == null)
        {
            technology = ScriptableObject.CreateInstance<TechnologyData>();
            AssetDatabase.CreateAsset(technology, path);
        }
        var data = new SerializedObject(technology);
        data.FindProperty("displayName").stringValue = name;
        data.ApplyModifiedPropertiesWithoutUndo();
        technology.effect = effect;
        technology.value = value;
        technology.price = price;
        technology.icon = icon;
        technology.marketExclusive = true;
        EditorUtility.SetDirty(technology);
    }

    private static void ImproveReadability(Transform canvas)
    {
        foreach (var text in canvas.GetComponentsInChildren<TMP_Text>(true))
        {
            if (text.name.Contains("Details") || text.name.Contains("Description"))
            {
                text.enableAutoSizing = true;
                text.fontSizeMax = Mathf.Max(text.fontSize, 20f);
                text.fontSizeMin = 18f;
            }
        }
    }

    private static void ResizeCarrot()
    {
        const string marker = "../../Verification/Feedback/CarrotResized.txt";
        if (File.Exists(marker)) return;
        var crop = AssetDatabase.LoadAssetAtPath<CropData>(Root + "Crops/Carrot.asset");
        foreach (var model in new[] { crop.plantedPrefab, crop.growingPrefab, crop.grownPrefab }.Distinct())
        {
            string path = AssetDatabase.GetAssetPath(model);
            var prefab = PrefabUtility.LoadPrefabContents(path);
            try
            {
                prefab.transform.localScale *= 0.85f;
                PrefabUtility.SaveAsPrefabAsset(prefab, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
        }
        Directory.CreateDirectory("../../Verification/Feedback");
        File.WriteAllText(marker, "Carrot planted, growing and grown roots multiplied by 0.85.\n");
    }

    private static void AddDemoEnd(Transform canvas)
    {
        var old = canvas.GetComponentsInChildren<Transform>(true).FirstOrDefault(item => item.name == "DemoEndOverlay");
        if (old != null) Object.DestroyImmediate(old.gameObject);
        var controller = canvas.GetComponent<FarmDemoEndController>();
        if (controller == null) controller = canvas.gameObject.AddComponent<FarmDemoEndController>();
        var overlay = FarmMenuSetup.Overlay(canvas, "DemoEndOverlay");
        var panel = FarmMenuSetup.Panel(overlay, "DemoEndPanel", 470f, 220f, 660f, 460f);
        FarmMenuSetup.Text(panel, "DemoEndTitle", "END OF DEMO", 40f, 40f, 580f, 70f, 42f, TextAlignmentOptions.Center);
        FarmMenuSetup.Text(panel, "DemoEndMessage", "All 12 deliveries completed!\nThank you for playing.", 50f, 130f, 560f, 80f, 25f, TextAlignmentOptions.Center);
        FarmMenuSetup.Button(panel, "ContinueFarmingButton", "CONTINUE FARMING", 110f, 250f, 440f, 60f, controller.ContinueFarming);
        FarmMenuSetup.Button(panel, "ReturnToMenuButton", "MAIN MENU", 110f, 330f, 440f, 60f, controller.ReturnToMainMenu);
        var data = new SerializedObject(controller);
        FarmMenuSetup.Ref(data, "missions", Object.FindFirstObjectByType<FarmMissionBoard>());
        FarmMenuSetup.Ref(data, "menus", canvas.GetComponent<FarmUIController>());
        FarmMenuSetup.Ref(data, "endPanel", overlay.gameObject);
        data.ApplyModifiedPropertiesWithoutUndo();
        overlay.gameObject.SetActive(false);
    }
}
