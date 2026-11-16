using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class FarmPermanentUpgradeSetup
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

        if (!AssetDatabase.IsValidFolder(Root + "Tech/FarmUpgrades"))
            AssetDatabase.CreateFolder(Root + "Tech", "FarmUpgrades");
        var growingTime = MoveTechnology("Show Growing Time.asset");
        var worm = MoveTechnology("Worm Composter.asset");
        growingTime.marketExclusive = false;
        worm.marketExclusive = false;
        worm.value = 0.35f;
        EditorUtility.SetDirty(growingTime);
        EditorUtility.SetDirty(worm);

        var systems = GameObject.Find("GameSystems");
        var tankData = new SerializedObject(systems.GetComponent<FarmWaterTank>());
        tankData.FindProperty("startingWater").floatValue = 50f;
        tankData.ApplyModifiedPropertiesWithoutUndo();
        var composterData = new SerializedObject(systems.GetComponent<FarmWormComposter>());
        composterData.FindProperty("additionalGrowthTimeReduction").floatValue = 0.25f;
        composterData.ApplyModifiedPropertiesWithoutUndo();

        var shopData = new SerializedObject(systems.GetComponent<FarmShop>());
        var catalog = shopData.FindProperty("exclusiveTechnologyCatalog");
        for (int index = catalog.arraySize - 1; index >= 0; index--)
        {
            var item = catalog.GetArrayElementAtIndex(index);
            if (item.objectReferenceValue != growingTime && item.objectReferenceValue != worm)
                continue;
            item.objectReferenceValue = null;
            catalog.DeleteArrayElementAtIndex(index);
        }
        shopData.ApplyModifiedPropertiesWithoutUndo();

        var canvas = GameObject.Find("Main Canvas").transform;
        var panel = canvas.Find("ScreenOverlay/TechnologiesPanel");
        var growingDescription = Rename(panel, "UpgradeOneDescription", "GrowingTimeUpgradeDescription").GetComponent<TMP_Text>();
        var wormDescription = Rename(panel, "UpgradeTwoDescription", "WormComposterUpgradeDescription").GetComponent<TMP_Text>();
        var growingButton = Rename(panel, "BuyTechnologyAButton", "GrowingTimeUpgradeButton").GetComponent<Button>();
        var wormButton = Rename(panel, "BuyTechnologyBButton", "WormComposterUpgradeButton").GetComponent<Button>();
        var growingLabel = growingButton.GetComponentInChildren<TMP_Text>(true);
        var wormLabel = wormButton.GetComponentInChildren<TMP_Text>(true);
        growingLabel.name = "GrowingTimeUpgradeButtonLabel";
        wormLabel.name = "WormComposterUpgradeButtonLabel";

        SetRect(growingDescription.rectTransform, 365f, 65f);
        SetRect((RectTransform)growingButton.transform, 435f, 48f);
        SetRect(wormDescription.rectTransform, 505f, 105f);
        SetRect((RectTransform)wormButton.transform, 625f, 48f);
        growingDescription.enableAutoSizing = false;
        growingDescription.fontSize = 18f;
        wormDescription.enableAutoSizing = false;
        wormDescription.fontSize = 18f;

        foreach (var transform in new[] { growingDescription.transform, wormDescription.transform, growingButton.transform, wormButton.transform })
            transform.gameObject.SetActive(true);

        var hud = canvas.GetComponent<FarmHUD>();
        var hudData = new SerializedObject(hud);
        Connect(hudData, "shopTechnologyA", growingTime);
        Connect(hudData, "shopTechnologyB", worm);
        Connect(hudData, "growingTimeUpgradeDescriptionText", growingDescription);
        Connect(hudData, "growingTimeUpgradeButton", growingButton);
        Connect(hudData, "growingTimeUpgradeButtonText", growingLabel);
        Connect(hudData, "wormComposterUpgradeDescriptionText", wormDescription);
        Connect(hudData, "wormComposterUpgradeButton", wormButton);
        Connect(hudData, "wormComposterUpgradeButtonText", wormLabel);
        hudData.ApplyModifiedPropertiesWithoutUndo();
        growingDescription.text = growingTime.DisplayName + "\nShows crop growth percentage in its contextual menu.";
        wormDescription.text = worm.DisplayName + "\nGenerates 1 fertilizer every 5s.\nFertilized crops: 50% less remaining growth time; +35% produce.";
        growingLabel.text = "BUY / " + growingTime.price + " COINS";
        wormLabel.text = "BUY / " + worm.price + " COINS";
        growingButton.interactable = false;
        wormButton.interactable = false;

        foreach (var entry in originalTransforms)
            if (EditorJsonUtility.ToJson(entry.Key) != entry.Value)
                throw new Exception("Original scene transform changed: " + entry.Key.name);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Directory.CreateDirectory("../../Verification/PermanentUpgrades");
        File.WriteAllText("../../Verification/PermanentUpgrades/Setup.txt",
            "Initial water: 50/100.\n" +
            "Growing Time and Worm Composter available permanently in Upgrades; removed from seasonal catalog.\n" +
            "Composter adds 25 percentage points to fertilizer growth reduction and grants 35% produce bonus.\n" +
            "Technology GUIDs, prices and all non-UI transforms preserved.\n");
        Debug.Log("Permanent farm upgrades configured.");
    }

    private static TechnologyData MoveTechnology(string name)
    {
        string target = Root + "Tech/FarmUpgrades/" + name;
        var technology = AssetDatabase.LoadAssetAtPath<TechnologyData>(target);
        if (technology != null) return technology;

        string source = Root + "Tech/Market/" + name;
        technology = AssetDatabase.LoadAssetAtPath<TechnologyData>(source);
        if (technology == null) throw new Exception("Technology not found: " + name);
        string error = AssetDatabase.MoveAsset(source, target);
        if (!string.IsNullOrEmpty(error)) throw new Exception(error);
        return technology;
    }

    private static Transform Rename(Transform parent, string oldName, string name)
    {
        var child = parent.Find(name);
        if (child == null) child = parent.Find(oldName);
        if (child == null) throw new Exception("Upgrade UI not found: " + oldName);
        child.name = name;
        return child;
    }

    private static void SetRect(RectTransform rect, float y, float height)
    {
        bool hasIcon = rect.name.EndsWith("UpgradeDescription") &&
            rect.parent.Find(rect.name.Replace("Description", "IconFrame")) != null;
        rect.anchoredPosition = new Vector2(hasIcon ? 92f : 28f, -y);
        rect.sizeDelta = new Vector2(hasIcon ? 580f : 644f, height);
    }

    private static void Connect(SerializedObject target, string field, UnityEngine.Object value) =>
        target.FindProperty(field).objectReferenceValue = value;
}
