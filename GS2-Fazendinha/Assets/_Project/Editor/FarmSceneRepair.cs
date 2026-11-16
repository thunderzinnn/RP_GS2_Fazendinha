using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class FarmSceneRepair
{
    private const string Root = "Assets/_Project/";
    private static readonly StringBuilder report = new StringBuilder();

    public static void Apply()
    {
        var scene = EditorSceneManager.OpenScene(Root + "Scenes/MainLevel.unity");
        var protectedTransforms = new Dictionary<Transform, string>();
        foreach (var root in scene.GetRootGameObjects())
            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                if (!(transform is RectTransform))
                    protectedTransforms[transform] = DescribeTransform(transform);

        ConfigureRewards();
        var canvas = GameObject.Find("Main Canvas").transform;
        var hud = canvas.GetComponent<FarmHUD>();
        var overlay = canvas.Find("ScreenOverlay");
        var map = overlay.Find("MapPanel");
        var shop = overlay.Find("ShopPanel");
        var first = overlay.Find("FirstCommunityPanel");
        var second = overlay.Find("SecondCommunityPanel");
        var upgrades = overlay.Find("TechnologiesPanel");
        var cropMenu = canvas.Find("CropActionPanel");
        var third = overlay.Find("ThirdCommunityPanel");
        if (third == null)
        {
            third = UnityEngine.Object.Instantiate(first.gameObject, overlay, false).transform;
            third.name = "ThirdCommunityPanel";
            Rename(third, "FirstCommunityTitle", "ThirdCommunityTitle");
            Rename(third, "FirstMissionDescriptionText", "ThirdMissionDescriptionText");
            Rename(third, "BackFromFirstCommunityButton", "BackFromThirdCommunityButton");
            Rename(third, "DeliverFirstMissionButton", "DeliverThirdMissionButton");
        }

        var thirdButton = map.Find("ThirdCommunityButton");
        if (thirdButton == null)
        {
            thirdButton = UnityEngine.Object.Instantiate(map.Find("FirstCommunityButton").gameObject, map, false).transform;
            thirdButton.name = "ThirdCommunityButton";
            SetRect(thirdButton, 450f, -450f, 300f, 120f);
            var label = thirdButton.GetComponentInChildren<TMP_Text>(true);
            label.name = "ThirdCommunityButtonLabel";
            label.rectTransform.sizeDelta = new Vector2(284f, 110f);
        }

        var communityData = new[]
        {
            Load<FarmCommunityData>("Comunitiies/Le Fezende Ghoon'd.asset"),
            Load<FarmCommunityData>("Comunitiies/Mataradar LTDA.asset"),
            Load<FarmCommunityData>("Comunitiies/Community 3.asset")
        };
        var board = UnityEngine.Object.FindFirstObjectByType<FarmMissionBoard>();
        var boardData = new SerializedObject(board);
        var communityArray = boardData.FindProperty("communities");
        communityArray.arraySize = communityData.Length;
        for (int index = 0; index < communityData.Length; index++)
            communityArray.GetArrayElementAtIndex(index).objectReferenceValue = communityData[index];
        boardData.ApplyModifiedPropertiesWithoutUndo();

        first.Find("FirstCommunityTitle").GetComponent<TMP_Text>().text = communityData[0].communityName;
        second.Find("SecondCommunityTitle").GetComponent<TMP_Text>().text = communityData[1].communityName;
        third.Find("ThirdCommunityTitle").GetComponent<TMP_Text>().text = communityData[2].communityName;
        map.Find("FirstCommunityButton").GetComponentInChildren<TMP_Text>(true).text = communityData[0].communityName + "\n\nVIEW REQUEST";
        map.Find("SecondCommunityButton").GetComponentInChildren<TMP_Text>(true).text = communityData[1].communityName + "\n\nVIEW REQUEST";
        thirdButton.GetComponentInChildren<TMP_Text>(true).text = communityData[2].communityName + "\nVIEW REQUEST";

        var panels = new[] { map, shop, first, second, third, upgrades };
        SetRect(thirdButton, 450f, -450f, 300f, 120f);
        var navigationButtons = new[]
        {
            canvas.Find("Footer/MapTab").GetComponent<Button>(),
            canvas.Find("Footer/TechnologiesTab").GetComponent<Button>(),
            map.Find("FirstCommunityButton").GetComponent<Button>(),
            map.Find("SecondCommunityButton").GetComponent<Button>(),
            map.Find("MarketMapButton").GetComponent<Button>(),
            first.Find("BackFromFirstCommunityButton").GetComponent<Button>(),
            second.Find("BackFromSecondCommunityButton").GetComponent<Button>(),
            shop.Find("BackFromMarketButton").GetComponent<Button>()
        };
        foreach (var button in navigationButtons)
            AddVisibilityCall(button, third.gameObject, false);
        ConfigureNavigation(thirdButton.GetComponent<Button>(), third, overlay, panels, cropMenu);
        ConfigureNavigation(third.Find("BackFromThirdCommunityButton").GetComponent<Button>(), map, overlay, panels, cropMenu);
        var deliverThird = third.Find("DeliverThirdMissionButton").GetComponent<Button>();
        RenameLabel(third.Find("BackFromThirdCommunityButton"), "BackFromThirdCommunityButtonLabel");
        RenameLabel(deliverThird.transform, "DeliverThirdMissionButtonLabel");
        deliverThird.onClick = new Button.ButtonClickedEvent();
        UnityEventTools.AddPersistentListener(deliverThird.onClick, hud.DeliverMissionThree);

        var progressText = map.Find("MissionProgressText");
        if (progressText == null)
        {
            progressText = UnityEngine.Object.Instantiate(map.Find("MapFooter").gameObject, map, false).transform;
            progressText.name = "MissionProgressText";
            SetRect(progressText, 36f, -580f, 1100f, 32f);
        }
        SetRect(progressText, 36f, -580f, 1100f, 32f);
        SetRect(map.Find("MapFooter"), 36f, -635f, 1100f, 32f);
        foreach (var panel in new[] { first, second, third })
        {
            foreach (var text in panel.GetComponentsInChildren<TMP_Text>(true))
            {
                if (!text.name.EndsWith("MissionDescriptionText")) continue;
                SetRect(text.transform, 28f, -115f, 744f, 310f);
                text.enableAutoSizing = true;
                text.fontSizeMin = 16f;
                text.fontSizeMax = 22f;
            }
        }

        var hudData = new SerializedObject(hud);
        Connect(hudData, "thirdMissionDescriptionText", third.Find("ThirdMissionDescriptionText").GetComponent<TMP_Text>());
        Connect(hudData, "thirdMissionDeliverButton", deliverThird);
        Connect(hudData, "firstMissionDeliverButton", first.Find("DeliverFirstMissionButton").GetComponent<Button>());
        Connect(hudData, "secondMissionDeliverButton", second.Find("DeliverSecondMissionButton").GetComponent<Button>());
        Connect(hudData, "missionProgressText", progressText.GetComponent<TMP_Text>());
        Connect(hudData, "buySeedsButton", shop.Find("BuySeedsButton").GetComponent<Button>());

        SetRect(shop, 250f, -120f, 1100f, 640f);
        foreach (string name in new[] { "ShopCropDropdown", "ShopCropDetailsText", "BuySeedsButton", "SellProduceButton" })
        {
            var rect = (RectTransform)shop.Find(name);
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 500f);
            ResizeControlText(rect, 500f);
        }
        var dropdown = shop.Find("MarketTechnologyDropdown");
        if (dropdown == null)
        {
            dropdown = UnityEngine.Object.Instantiate(shop.Find("ShopCropDropdown").gameObject, shop, false).transform;
            dropdown.name = "MarketTechnologyDropdown";
            dropdown.GetComponent<TMP_Dropdown>().onValueChanged = new TMP_Dropdown.DropdownEvent();
        }
        SetRect(dropdown, 572f, -95f, 500f, 48f);
        var details = shop.Find("MarketTechnologyDetailsText");
        if (details == null)
        {
            details = UnityEngine.Object.Instantiate(shop.Find("ShopCropDetailsText").gameObject, shop, false).transform;
            details.name = "MarketTechnologyDetailsText";
        }
        SetRect(details, 572f, -170f, 500f, 220f);
        var buyTechnology = shop.Find("BuyMarketTechnologyButton");
        if (buyTechnology == null)
        {
            buyTechnology = UnityEngine.Object.Instantiate(shop.Find("BuySeedsButton").gameObject, shop, false).transform;
            buyTechnology.name = "BuyMarketTechnologyButton";
        }
        SetRect(buyTechnology, 572f, -508f, 500f, 48f);
        buyTechnology.GetComponentInChildren<TMP_Text>(true).text = "BUY TECHNOLOGY";
        RenameLabel(buyTechnology, "BuyMarketTechnologyButtonLabel");
        var purchaseButton = buyTechnology.GetComponent<Button>();
        purchaseButton.onClick = new Button.ButtonClickedEvent();
        UnityEventTools.AddPersistentListener(purchaseButton.onClick, hud.BuySelectedMarketTechnology);
        var refresh = shop.Find("MarketRefreshText");
        if (refresh == null)
        {
            refresh = UnityEngine.Object.Instantiate(details.gameObject, shop, false).transform;
            refresh.name = "MarketRefreshText";
        }
        SetRect(refresh, 572f, -422f, 500f, 64f);
        Connect(hudData, "marketTechnologyDropdown", dropdown.GetComponent<TMP_Dropdown>());
        Connect(hudData, "marketTechnologyDetailsText", details.GetComponent<TMP_Text>());
        Connect(hudData, "marketRefreshText", refresh.GetComponent<TMP_Text>());
        Connect(hudData, "buyMarketTechnologyButton", purchaseButton);
        hudData.ApplyModifiedPropertiesWithoutUndo();

        foreach (string name in new[] { "UpgradeOneDescription", "UpgradeTwoDescription", "BuyTechnologyAButton", "BuyTechnologyBButton" })
        {
            var oldUpgrade = upgrades.Find(name);
            if (oldUpgrade != null) oldUpgrade.gameObject.SetActive(false);
        }

        foreach (var button in canvas.GetComponentsInChildren<Button>(true))
        {
            for (int index = button.onClick.GetPersistentEventCount() - 1; index >= 0; index--)
                if (button.onClick.GetPersistentTarget(index) is AudioManager &&
                    button.onClick.GetPersistentMethodName(index) == "PlayUIClick")
                    UnityEventTools.RemovePersistentListener(button.onClick, index);
            bool connected = false;
            for (int index = 0; index < button.onClick.GetPersistentEventCount(); index++)
                connected |= button.onClick.GetPersistentTarget(index) == hud &&
                    button.onClick.GetPersistentMethodName(index) == "PlayUIClick";
            if (!connected) UnityEventTools.AddPersistentListener(button.onClick, hud.PlayUIClick);
        }

        third.gameObject.SetActive(false);
        overlay.gameObject.SetActive(false);
        cropMenu.gameObject.SetActive(false);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(scene.path, true) };

        foreach (var entry in protectedTransforms)
            if (DescribeTransform(entry.Key) != entry.Value)
                throw new Exception("Protected scene transform changed: " + entry.Key.name);
        report.AppendLine("Preserved all " + protectedTransforms.Count + " existing non-UI transforms, including camera and plots.");
        report.AppendLine("Preserved crop data, water balance, 5% tank bonus, plot prices and graphics settings.");
        report.AppendLine("Connected seasonal market, three communities, progress, delivery buttons and audio manager.");
        report.AppendLine("No audio clips were provided; music/click fields remain empty.");
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Directory.CreateDirectory("../../Verification/SystemAudit");
        File.WriteAllText("../../Verification/SystemAudit/Repairs.txt", report.ToString());
        FarmProjectAudit.Inspect();
        Debug.Log("Farm scene repairs completed.");
    }

    private static void ConfigureRewards()
    {
        ConfigureCommunityRewards("Comunitiies/Mataradar LTDA.asset", new[]
        {
            "Tech/Mataradar/Better Soil I.asset", "Tech/Mataradar/Better Soil II.asset",
            "Tech/Mataradar/Better Soil III.asset", "Tech/Mataradar/Better Soil IV.asset"
        });
        ConfigureCommunityRewards("Comunitiies/Le Fezende Ghoon'd.asset", new[]
        {
            "Tech/Le Fezend/Reinforced Pipes I.asset", "Tech/Le Fezend/Reinforced Pipes II.asset",
            "Tech/Le Fezend/Better Water Collection.asset", "Tech/Le Fezend/Better Water Collection II.asset"
        });
        ConfigureCommunityRewards("Comunitiies/Community 3.asset", new[]
        {
            "Tech/Community 3/Better Irrigation.asset", "Tech/Community 3/Better Irrigation II.asset",
            "Tech/Community 3/Better Irrigation III.asset", "Tech/Community 3/Better Irrigation IV.asset"
        });
    }

    private static void ConfigureCommunityRewards(string communityPath, string[] technologyPaths)
    {
        var community = Load<FarmCommunityData>(communityPath);
        for (int index = 0; index < technologyPaths.Length; index++)
        {
            var mission = community.missions[index];
            mission.rewardTechnology = Load<TechnologyData>(technologyPaths[index]);
            EditorUtility.SetDirty(mission);
            report.AppendLine(mission.name + " -> " + mission.rewardTechnology.name);
        }
    }

    private static T Load<T>(string path) where T : UnityEngine.Object
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(Root + path);
        if (asset == null) throw new Exception("Missing asset: " + path);
        return asset;
    }

    private static void Rename(Transform parent, string oldName, string newName) => parent.Find(oldName).name = newName;

    private static void RenameLabel(Transform parent, string name) => parent.GetComponentInChildren<TMP_Text>(true).name = name;

    private static void Connect(SerializedObject target, string field, UnityEngine.Object value) =>
        target.FindProperty(field).objectReferenceValue = value;

    private static void SetRect(Transform transform, float x, float y, float width, float height)
    {
        var rect = (RectTransform)transform;
        rect.anchoredPosition = new Vector2(x, y);
        rect.sizeDelta = new Vector2(width, height);
    }

    private static void ResizeControlText(Transform control, float width)
    {
        var dropdown = control.GetComponent<TMP_Dropdown>();
        if (dropdown != null && dropdown.captionText != null)
            dropdown.captionText.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width - 40f);
        var button = control.GetComponent<Button>();
        if (button != null)
            foreach (var label in control.GetComponentsInChildren<TMP_Text>(true))
                label.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width - 16f);
    }

    private static void AddVisibilityCall(Button button, GameObject target, bool active)
    {
        for (int index = 0; index < button.onClick.GetPersistentEventCount(); index++)
            if (button.onClick.GetPersistentTarget(index) == target && button.onClick.GetPersistentMethodName(index) == "SetActive") return;
        UnityEventTools.AddBoolPersistentListener(button.onClick, target.SetActive, active);
    }

    private static void ConfigureNavigation(Button button, Transform target, Transform overlay, Transform[] panels, Transform cropMenu)
    {
        button.onClick = new Button.ButtonClickedEvent();
        AddVisibilityCall(button, overlay.gameObject, true);
        foreach (var panel in panels) AddVisibilityCall(button, panel.gameObject, panel == target);
        AddVisibilityCall(button, cropMenu.gameObject, false);
    }

    private static string DescribeTransform(Transform transform) =>
        EditorJsonUtility.ToJson(transform);
}
