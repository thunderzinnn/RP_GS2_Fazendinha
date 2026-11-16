using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class FarmUIIconSetup
{
    public static void Apply()
    {
        var scene = EditorSceneManager.OpenScene("Assets/_Project/Scenes/MainLevel.unity");
        var originalTransforms = new Dictionary<Transform, string>();
        foreach (var root in scene.GetRootGameObjects())
            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                if (!(transform is RectTransform))
                    originalTransforms[transform] = EditorJsonUtility.ToJson(transform);

        var canvas = GameObject.Find("Main Canvas").transform;
        var upgrades = canvas.Find("ScreenOverlay/TechnologiesPanel");
        var market = canvas.Find("ScreenOverlay/ShopPanel");
        var cropPanel = canvas.Find("CropActionPanel");
        var growingIcon = CreateIcon(upgrades, "GrowingTimeUpgradeIcon", 28f, 365f, 48f);
        var wormIcon = CreateIcon(upgrades, "WormComposterUpgradeIcon", 28f, 505f, 48f);
        var cropIcon = CreateIcon(market, "ShopCropIcon", 28f, 170f, 64f);
        var technologyIcon = CreateIcon(market, "MarketTechnologyIcon", 572f, 170f, 64f);
        var contextIcon = CreateIcon(cropPanel, "CropIcon", 20f, 16f, 40f);
        SetRect(upgrades.Find("GrowingTimeUpgradeDescription"), 92f, 365f, 580f, 65f);
        SetRect(upgrades.Find("WormComposterUpgradeDescription"), 92f, 505f, 580f, 105f);
        SetRect(market.Find("ShopCropDetailsText"), 108f, 170f, 420f, 310f);
        SetRect(market.Find("MarketTechnologyDetailsText"), 652f, 170f, 420f, 220f);
        SetRect(cropPanel.Find("CropTitleText"), 76f, 20f, 244f, 32f);

        var hud = new SerializedObject(canvas.GetComponent<FarmHUD>());
        Connect(hud, "growingTimeUpgradeIcon", growingIcon);
        Connect(hud, "wormComposterUpgradeIcon", wormIcon);
        Connect(hud, "shopCropIcon", cropIcon);
        Connect(hud, "marketTechnologyIcon", technologyIcon);
        hud.ApplyModifiedPropertiesWithoutUndo();
        var menu = new SerializedObject(canvas.GetComponent<FarmPlotContextMenu>());
        Connect(menu, "cropIcon", contextIcon);
        menu.ApplyModifiedPropertiesWithoutUndo();

        ConfigureDropdown(market.Find("ShopCropDropdown").GetComponent<TMP_Dropdown>());
        ConfigureDropdown(market.Find("MarketTechnologyDropdown").GetComponent<TMP_Dropdown>());
        ConfigureDropdown(cropPanel.Find("PlantRow/CropDropdown").GetComponent<TMP_Dropdown>());

        foreach (var entry in originalTransforms)
            if (EditorJsonUtility.ToJson(entry.Key) != entry.Value)
                throw new Exception("Original scene transform changed: " + entry.Key.name);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Directory.CreateDirectory("../../Verification/UIIcons");
        File.WriteAllText("../../Verification/UIIcons/Setup.txt",
            "Five detail icon frames and three dropdown templates configured.\n" +
            "CropData.icon and TechnologyData.icon supply the shared sprites.\n" +
            "Empty fields keep warm-colored placeholder frames.\n" +
            "Original non-UI transforms and gameplay data preserved.\n");
        Debug.Log("Crop and technology icon spaces configured.");
    }

    private static Image CreateIcon(Transform parent, string name, float x, float y, float size)
    {
        var existing = parent.Find(name + "Frame");
        if (existing != null) return existing.Find(name).GetComponent<Image>();

        var frame = new GameObject(name + "Frame", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
        frame.SetParent(parent, false);
        frame.gameObject.layer = 5;
        frame.anchorMin = frame.anchorMax = frame.pivot = new Vector2(0f, 1f);
        frame.anchoredPosition = new Vector2(x, -y);
        frame.sizeDelta = new Vector2(size, size);
        frame.GetComponent<Image>().color = new Color32(156, 105, 54, 255);
        frame.GetComponent<Image>().raycastTarget = false;

        var icon = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        icon.transform.SetParent(frame, false);
        icon.gameObject.layer = 5;
        icon.rectTransform.anchorMin = Vector2.zero;
        icon.rectTransform.anchorMax = Vector2.one;
        icon.rectTransform.offsetMin = new Vector2(3f, 3f);
        icon.rectTransform.offsetMax = new Vector2(-3f, -3f);
        icon.color = Color.white;
        icon.preserveAspect = true;
        icon.raycastTarget = false;
        icon.enabled = false;
        return icon;
    }

    private static void ConfigureDropdown(TMP_Dropdown dropdown)
    {
        var caption = CreateIcon(dropdown.transform, "SelectedItemIcon", 8f, 6f, 32f);
        CenterLeft(caption.transform.parent as RectTransform);
        dropdown.captionImage = caption;
        ConfigureOptionText(dropdown.captionText, 26f);

        var item = dropdown.itemText.GetComponentInParent<Toggle>(true);
        var itemRect = (RectTransform)item.transform;
        itemRect.anchorMin = new Vector2(0f, 1f);
        itemRect.anchorMax = new Vector2(1f, 1f);
        itemRect.pivot = new Vector2(0.5f, 1f);
        itemRect.anchoredPosition = Vector2.zero;
        itemRect.sizeDelta = new Vector2(0f, 40f);
        ((RectTransform)itemRect.parent).SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 40f);
        var option = CreateIcon(item.transform, "OptionIcon", 8f, 4f, 32f);
        CenterLeft(option.transform.parent as RectTransform);
        dropdown.itemImage = option;
        ConfigureOptionText(dropdown.itemText, 20f);

        if (item.graphic != null)
        {
            var checkmark = item.graphic.rectTransform;
            checkmark.anchorMin = checkmark.anchorMax = new Vector2(1f, 0.5f);
            checkmark.pivot = new Vector2(0.5f, 0.5f);
            checkmark.anchoredPosition = new Vector2(-14f, 0f);
            checkmark.sizeDelta = new Vector2(14f, 14f);
        }

        dropdown.template.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 200f);
        dropdown.template.gameObject.SetActive(false);
        EditorUtility.SetDirty(dropdown);
    }

    private static void ConfigureOptionText(TMP_Text text, float maximumSize)
    {
        text.rectTransform.anchorMin = Vector2.zero;
        text.rectTransform.anchorMax = Vector2.one;
        text.rectTransform.offsetMin = new Vector2(48f, 3f);
        text.rectTransform.offsetMax = new Vector2(-32f, -3f);
        text.alignment = TextAlignmentOptions.Left;
        text.enableAutoSizing = true;
        text.fontSizeMin = 12f;
        text.fontSizeMax = maximumSize;
        text.fontSize = maximumSize;
        text.overflowMode = TextOverflowModes.Ellipsis;
    }

    private static void CenterLeft(RectTransform rect)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 0.5f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = new Vector2(8f, 0f);
    }

    private static void SetRect(Transform transform, float x, float y, float width, float height)
    {
        var rect = (RectTransform)transform;
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
    }

    private static void Connect(SerializedObject target, string field, UnityEngine.Object value) =>
        target.FindProperty(field).objectReferenceValue = value;
}
