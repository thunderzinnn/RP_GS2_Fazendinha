using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class FarmSaveSetup
{
    private const string Root = "Assets/_Project/";

    public static void Apply()
    {
        FarmSaveChecks.CleanupPreviousTestProfile();
        Connect();
        FarmSystemChecks.RunPlayerPrefs();
    }

    public static void Connect()
    {
        Directory.CreateDirectory("../../Verification/PlayerPrefs");
        File.WriteAllText("../../Verification/PlayerPrefs/PlotIds.txt", "");
        AssignAssetIds<CropData>();
        AssignAssetIds<TechnologyData>();
        AssignAssetIds<MissionData>();
        AssignAssetIds<FarmCommunityData>();
        AssetDatabase.SaveAssets();
        foreach (string name in new[] { "MainLevel", "Tutorial", "MainMenu" })
        {
            var scene = EditorSceneManager.OpenScene(Root + "Scenes/" + name + ".unity");
            var manager = Object.FindFirstObjectByType<FarmSaveManager>();
            if (manager == null) manager = new GameObject("FarmSaveSystem").AddComponent<FarmSaveManager>();
            var data = new SerializedObject(manager);
            data.FindProperty("tutorialProfile").boolValue = name == "Tutorial";
            if (name != "MainMenu")
            {
                Set(data, "wallet", Object.FindFirstObjectByType<FarmWallet>());
                Set(data, "inventory", Object.FindFirstObjectByType<FarmInventory>());
                Set(data, "technologies", Object.FindFirstObjectByType<FarmTechnologyManager>());
                Set(data, "waterTank", Object.FindFirstObjectByType<FarmWaterTank>());
                Set(data, "fertilizerStorage", Object.FindFirstObjectByType<FarmFertilizerStorage>());
                Set(data, "wormComposter", Object.FindFirstObjectByType<FarmWormComposter>());
                Set(data, "missions", Object.FindFirstObjectByType<FarmMissionBoard>());
                Set(data, "shop", Object.FindFirstObjectByType<FarmShop>());
                Set(data, "expansion", Object.FindFirstObjectByType<FarmPlotExpansion>());
                Set(data, "player", Object.FindFirstObjectByType<PlayerFarmController>());
                Set(data, "tutorial", Object.FindFirstObjectByType<FarmTutorialController>());
                var hud = Object.FindFirstObjectByType<FarmHUD>();
                Set(data, "hud", hud);
                var crops = new SerializedObject(hud).FindProperty("availableCrops");
                var catalog = new Object[crops.arraySize];
                for (int index = 0; index < catalog.Length; index++) catalog[index] = crops.GetArrayElementAtIndex(index).objectReferenceValue;
                SetArray(data, "crops", catalog);
                var plots = Object.FindObjectsByType<FarmPlot>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                foreach (var plot in plots)
                {
                    var plotData = new SerializedObject(plot);
                    var id = plotData.FindProperty("saveId");
                    if (string.IsNullOrEmpty(id.stringValue))
                        id.stringValue = GlobalObjectId.GetGlobalObjectIdSlow(plot).ToString();
                    plotData.ApplyModifiedPropertiesWithoutUndo();
                    PrefabUtility.RecordPrefabInstancePropertyModifications(plot);
                    File.AppendAllText("../../Verification/PlayerPrefs/PlotIds.txt", plot.SaveId + "\n");
                }
                SetArray(data, "plots", plots.OrderBy(item => item.SaveId).Cast<Object>().ToArray());
                AddSaveButton(hud.transform);
                FarmMenuSetup.Find(hud.transform, "SaveGameButton").gameObject.SetActive(name != "Tutorial");
            }
            var technologies = AssetDatabase.FindAssets("t:TechnologyData", new[] { Root + "Tech" })
                .Select(guid => AssetDatabase.LoadAssetAtPath<TechnologyData>(AssetDatabase.GUIDToAssetPath(guid)))
                .OrderBy(item => item.SaveId).Cast<Object>().ToArray();
            SetArray(data, "technologyCatalog", technologies);
            data.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        AssetDatabase.SaveAssets();
    }

    private static void AddSaveButton(Transform canvas)
    {
        var controller = canvas.GetComponent<FarmUIController>();
        var panel = FarmMenuSetup.Find(canvas, "PausePanel");
        var existing = panel.Find("SaveGameButton");
        if (existing != null) Object.DestroyImmediate(existing.gameObject);
        FarmMenuSetup.Place((RectTransform)panel.Find("ResumeButton"), 60f, 96f, 340f, 54f);
        FarmMenuSetup.Button(panel, "SaveGameButton", "SAVE", 60f, 166f, 340f, 54f, controller.SaveGame);
        FarmMenuSetup.Place((RectTransform)panel.Find("PauseOptionsButton"), 60f, 236f, 340f, 54f);
        FarmMenuSetup.Place((RectTransform)panel.Find("PauseQuitButton"), 60f, 306f, 340f, 54f);
        var hint = panel.Find("PauseHint");
        FarmMenuSetup.Place((RectTransform)hint, 30f, 398f, 400f, 30f);
        var data = new SerializedObject(controller);
        Set(data, "pauseStatusText", hint.GetComponent<TMP_Text>());
        data.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void AssignAssetIds<T>() where T : ScriptableObject
    {
        foreach (string guid in AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { Root }))
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
            var data = new SerializedObject(asset);
            data.FindProperty("saveId").stringValue = guid;
            data.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
        }
    }

    private static void Set(SerializedObject data, string name, Object value) => data.FindProperty(name).objectReferenceValue = value;
    private static void SetArray(SerializedObject data, string name, Object[] values)
    {
        var array = data.FindProperty(name);
        array.arraySize = values.Length;
        for (int index = 0; index < values.Length; index++) array.GetArrayElementAtIndex(index).objectReferenceValue = values[index];
    }
}
