using System.IO;
using System.Linq;
using TMPro;
using Unity.AI.Navigation;
using UnityEditor;

using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class FarmTutorialSetup
{
    private const string Root = "Assets/_Project/";

    public static void Build()
    {
        var original = SceneManager.GetActiveScene();
        EditorSceneManager.SaveScene(original, Root + "Scenes/Tutorial.unity", true);
        var scene = EditorSceneManager.OpenScene(Root + "Scenes/Tutorial.unity");
        var plots = Object.FindObjectsByType<FarmPlot>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        var plot = plots.First(item => item.gameObject.activeSelf);
        foreach (var extra in plots) if (extra != plot) Object.DestroyImmediate(extra.gameObject);
        plot.name = "TutorialPlot";
        plot.transform.position = new Vector3(0f, 0.25f, 1.5f);
        foreach (var root in scene.GetRootGameObjects())
        foreach (var item in root.GetComponentsInChildren<Transform>(true))
            if (item != null && (item.name == "FarmBarn" || item.name == "WormComposter")) Object.DestroyImmediate(item.gameObject);

        var floor = GameObject.Find("Ground");
        var oldSize = floor.GetComponent<Renderer>().bounds.size;
        floor.transform.localScale = Vector3.Scale(floor.transform.localScale, new Vector3(12f / oldSize.x, 1f, 12f / oldSize.z));
        var camera = Camera.main;
        camera.transform.position = new Vector3(8f, 14f, -16f);
        camera.transform.LookAt(new Vector3(0f, 0f, 0.5f));
        camera.orthographic = true;
        camera.orthographicSize = 8.7f;
        var player = Object.FindFirstObjectByType<PlayerFarmController>();
        player.transform.position = new Vector3(0f, 0.25f, -2.5f);

        var crop = CopyAsset(AssetDatabase.LoadAssetAtPath<CropData>(Root + "Crops/Carrot.asset"), Root + "Crops/TutorialCarrot.asset");
        crop.plantedSeconds = 5f;
        crop.growingSeconds = 10f;
        EditorUtility.SetDirty(crop);
        Directory.CreateDirectory(Root + "Tutorial");
        AssetDatabase.Refresh();
        var sourceMission = AssetDatabase.FindAssets("t:MissionData", new[] { Root + "Missions" })
            .Select(guid => AssetDatabase.LoadAssetAtPath<MissionData>(AssetDatabase.GUIDToAssetPath(guid)))
            .First(item => item.requiredCrop.CropName == "Carrot" && item.requiredAmount == 12);
        var mission = CopyAsset(sourceMission, Root + "Tutorial/TutorialMission.asset");
        mission.requiredCrop = crop;
        EditorUtility.SetDirty(mission);
        var community = AssetDatabase.LoadAssetAtPath<FarmCommunityData>(Root + "Tutorial/TutorialCommunity.asset");
        if (community == null)
        {
            community = ScriptableObject.CreateInstance<FarmCommunityData>();
            AssetDatabase.CreateAsset(community, Root + "Tutorial/TutorialCommunity.asset");
        }
        community.communityName = sourceMission.communityName;
        community.technologyFocus = "Learn to grow food and deliver it to a community";
        community.missions = new[] { mission };
        community.missionDurationSeconds = 3600f;
        community.missionCooldownSeconds = 1f;
        EditorUtility.SetDirty(community);

        var canvas = GameObject.Find("Main Canvas").transform;
        var hud = canvas.GetComponent<FarmHUD>();
        ConfigureCrop(hud, crop);
        ConfigureCrop(canvas.GetComponent<FarmPlotContextMenu>(), crop);
        ConfigureCrop(Object.FindFirstObjectByType<FarmShop>(), crop);
        var inventory = Object.FindFirstObjectByType<FarmInventory>();
        var inventoryData = new SerializedObject(inventory);
        var seeds = inventoryData.FindProperty("startingSeeds");
        seeds.arraySize = 1;
        seeds.GetArrayElementAtIndex(0).FindPropertyRelative("crop").objectReferenceValue = crop;
        seeds.GetArrayElementAtIndex(0).FindPropertyRelative("amount").intValue = 5;
        inventoryData.ApplyModifiedPropertiesWithoutUndo();
        var board = Object.FindFirstObjectByType<FarmMissionBoard>();
        var boardData = new SerializedObject(board);
        boardData.FindProperty("communities").arraySize = 1;
        boardData.FindProperty("communities").GetArrayElementAtIndex(0).objectReferenceValue = community;
        boardData.FindProperty("missions").arraySize = 0;
        boardData.FindProperty("demoMissionLimit").intValue = 1;
        boardData.ApplyModifiedPropertiesWithoutUndo();
        foreach (string name in new[] { "SecondCommunityButton", "ThirdCommunityButton", "TechnologiesTab" })
            FarmMenuSetup.Find(canvas, name).gameObject.SetActive(false);
        FarmMenuSetup.Find(canvas, "MapTitle").GetComponent<TMP_Text>().text = "TUTORIAL MAP";
        FarmMenuSetup.Place((RectTransform)FarmMenuSetup.Find(canvas, "FirstCommunityButton"), 340f, 230f, 300f, 180f);
        FarmMenuSetup.Place((RectTransform)FarmMenuSetup.Find(canvas, "MarketMapButton"), 700f, 230f, 300f, 180f);
        var communityPanel = (RectTransform)FarmMenuSetup.Find(canvas, "FirstCommunityPanel");
        communityPanel.anchoredPosition = new Vector2(540f, communityPanel.anchoredPosition.y);
        foreach (string name in new[] { "MapFooter", "MissionProgressText" }) FarmMenuSetup.Find(canvas, name).gameObject.SetActive(false);
        foreach (var expansion in Object.FindObjectsByType<FarmPlotExpansion>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var data = new SerializedObject(expansion);
            data.FindProperty("extraPlots").arraySize = 0;
            data.FindProperty("plotPrices").arraySize = 0;
            data.ApplyModifiedPropertiesWithoutUndo();
            expansion.enabled = false;
        }
        var guide = FarmMenuSetup.Panel(canvas, "TutorialGuidePanel", 24f, 600f, 480f, 250f);
        var stepText = FarmMenuSetup.Text(guide, "TutorialStepText", "TUTORIAL 1/9", 24f, 18f, 432f, 42f, 27f);
        var instruction = FarmMenuSetup.Text(guide, "TutorialInstructionText", "Click the planting plot.", 24f, 70f, 432f, 100f, 21f);
        var controller = canvas.gameObject.AddComponent<FarmTutorialController>();
        var next = FarmMenuSetup.Button(guide, "StartFarmingButton", "START FARMING", 24f, 184f, 432f, 48f, controller.ContinueToGame);
        var tutorialData = new SerializedObject(controller);
        FarmMenuSetup.Ref(tutorialData, "player", player);
        FarmMenuSetup.Ref(tutorialData, "plot", plot);
        FarmMenuSetup.Ref(tutorialData, "inventory", inventory);
        FarmMenuSetup.Ref(tutorialData, "missions", board);
        FarmMenuSetup.Ref(tutorialData, "tutorialCrop", crop);
        FarmMenuSetup.Ref(tutorialData, "mapPanel", FarmMenuSetup.Find(canvas, "MapPanel").gameObject);
        FarmMenuSetup.Ref(tutorialData, "stepText", stepText);
        FarmMenuSetup.Ref(tutorialData, "instructionText", instruction);
        FarmMenuSetup.Ref(tutorialData, "continueButton", next.gameObject);
        tutorialData.ApplyModifiedPropertiesWithoutUndo();
        next.gameObject.SetActive(false);
        FarmMenuSetup.Find(canvas, "PauseOverlay").SetAsLastSibling();
        FarmMenuSetup.Find(canvas, "OptionsOverlay").SetAsLastSibling();

        var surface = Object.FindFirstObjectByType<NavMeshSurface>();
        surface.BuildNavMesh();
        string navigationPath = Root + "Tutorial/TutorialNavigation.asset";
        var navigation = AssetDatabase.LoadAssetAtPath<UnityEngine.AI.NavMeshData>(navigationPath);
        if (navigation == null) AssetDatabase.CreateAsset(surface.navMeshData, navigationPath);
        else
        {
            EditorUtility.CopySerialized(surface.navMeshData, navigation);
            surface.navMeshData = navigation;
            EditorUtility.SetDirty(navigation);
        }
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static void ConfigureCrop(MonoBehaviour target, CropData crop)
    {
        var data = new SerializedObject(target);
        foreach (string name in new[] { "availableCrops", "cropCatalog" })
        {
            var list = data.FindProperty(name);
            if (list == null) continue;
            list.arraySize = 1;
            list.GetArrayElementAtIndex(0).objectReferenceValue = crop;
        }
        var selected = data.FindProperty("crop");
        if (selected != null) selected.objectReferenceValue = crop;
        data.ApplyModifiedPropertiesWithoutUndo();
    }

    private static T CopyAsset<T>(T source, string path) where T : Object
    {
        var target = AssetDatabase.LoadAssetAtPath<T>(path);
        if (target == null)
        {
            target = Object.Instantiate(source);
            target.name = Path.GetFileNameWithoutExtension(path);
            AssetDatabase.CreateAsset(target, path);
        }
        else EditorUtility.CopySerialized(source, target);
        return target;
    }
}
