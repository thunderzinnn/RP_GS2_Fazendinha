using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class FarmUIIconChecks
{
    public static void Run()
    {
        var report = new StringBuilder();
        var hud = UnityEngine.Object.FindFirstObjectByType<FarmHUD>();
        var menu = UnityEngine.Object.FindFirstObjectByType<FarmPlotContextMenu>();
        var shop = UnityEngine.Object.FindFirstObjectByType<FarmShop>();
        var plot = UnityEngine.Object.FindFirstObjectByType<FarmPlot>();
        var inventory = UnityEngine.Object.FindFirstObjectByType<FarmInventory>();
        var crops = Read<CropData[]>(hud, "availableCrops");
        var initialCrop = Read<CropData>(hud, "crop");
        var firstTechnology = Read<TechnologyData>(hud, "shopTechnologyA");
        var secondTechnology = Read<TechnologyData>(hud, "shopTechnologyB");
        var seasonalTechnology = shop.GetTechnologyOffer(0);
        var cropIcons = new Dictionary<CropData, Sprite>();
        var technologyIcons = new Dictionary<TechnologyData, Sprite>();
        foreach (var crop in new[] { crops[0], crops[1] }) cropIcons[crop] = crop.icon;
        foreach (var technology in new[] { firstTechnology, secondTechnology, seasonalTechnology })
            if (technology != null) technologyIcons[technology] = technology.icon;
        var spriteA = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f));
        var spriteB = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f));
        spriteA.name = "Temporary Icon A";
        spriteB.name = "Temporary Icon B";

        try
        {
            foreach (string field in new[] { "shopCropIcon", "marketTechnologyIcon", "growingTimeUpgradeIcon", "wormComposterUpgradeIcon" })
            {
                var icon = Read<Image>(hud, field);
                Check(icon != null && icon.preserveAspect && !icon.raycastTarget, "Icon frame is configured: " + field, report);
                Check(icon.transform.parent.GetComponent<Image>() != null, "Placeholder frame exists: " + field, report);
            }

            crops[0].icon = spriteA;
            crops[1].icon = spriteB;
            firstTechnology.icon = spriteA;
            secondTechnology.icon = spriteB;
            if (seasonalTechnology != null) seasonalTechnology.icon = spriteA;
            Call(hud, "PopulateShopCropOptions");
            Call(hud, "PopulateMarketTechnologies");
            Call(menu, "PopulateDropdown");
            hud.SetCrop(crops[0]);
            Check(Read<Image>(hud, "shopCropIcon").sprite == spriteA && Read<Image>(hud, "shopCropIcon").enabled,
                "Crop detail displays the asset sprite", report);
            Read<TMP_Dropdown>(hud, "shopCropDropdown").value = 1;
            Check(Read<Image>(hud, "shopCropIcon").sprite == spriteB,
                "Changing crop selection updates its detail image", report);
            Check(Read<Image>(hud, "growingTimeUpgradeIcon").sprite == spriteA &&
                Read<Image>(hud, "wormComposterUpgradeIcon").sprite == spriteB,
                "Permanent upgrades display their own asset icons", report);
            hud.SelectMarketTechnology(0);
            hud.RefreshHUD();
            Check(seasonalTechnology == null || Read<Image>(hud, "marketTechnologyIcon").sprite == spriteA,
                "Seasonal technology detail follows its selected asset", report);

            foreach (var dropdown in new[] { Read<TMP_Dropdown>(hud, "shopCropDropdown"),
                Read<TMP_Dropdown>(hud, "marketTechnologyDropdown"), Read<TMP_Dropdown>(menu, "cropDropdown") })
            {
                Check(dropdown.captionImage != null && dropdown.itemImage != null,
                    "Dropdown has selected and option image references: " + dropdown.name, report);
                Check(dropdown.options.Count > 0 && dropdown.options[0].image == spriteA,
                    "Dropdown option uses its data asset image: " + dropdown.name, report);
                dropdown.SetValueWithoutNotify(0);
                dropdown.RefreshShownValue();
                Check(dropdown.captionImage.sprite == spriteA && dropdown.captionImage.enabled,
                    "Dropdown selected icon refreshes: " + dropdown.name, report);
                var row = dropdown.itemText.GetComponentInParent<Toggle>(true).GetComponent<RectTransform>();
                Check(row.rect.height >= 40f && dropdown.itemText.rectTransform.offsetMin.x >= 48f,
                    "Dropdown rows reserve space for the image: " + dropdown.name, report);
            }

            plot.ClearCrop();
            inventory.AddSeeds(crops[0], 1);
            Check(plot.TryPlant(crops[0], inventory, out _), "Prepare crop for contextual icon test", report);
            menu.Show(plot, new Vector2(800f, 450f));
            Check(Read<Image>(menu, "cropIcon").sprite == spriteA && Read<Image>(menu, "cropIcon").enabled,
                "Contextual menu shows the planted crop icon", report);
            menu.Close();
            plot.ClearCrop();

            crops[0].icon = null;
            firstTechnology.icon = null;
            hud.SetCrop(crops[0]);
            Check(!Read<Image>(hud, "shopCropIcon").enabled && Read<Image>(hud, "shopCropIcon").transform.parent.gameObject.activeSelf,
                "Missing crop sprite leaves the reserved placeholder visible", report);
            Check(!Read<Image>(hud, "growingTimeUpgradeIcon").enabled,
                "Missing technology sprite does not display an unrelated image", report);
        }
        finally
        {
            foreach (var entry in cropIcons) entry.Key.icon = entry.Value;
            foreach (var entry in technologyIcons) entry.Key.icon = entry.Value;
            plot.ClearCrop();
            menu.Close();
            Call(hud, "PopulateShopCropOptions");
            Call(hud, "PopulateMarketTechnologies");
            Call(menu, "PopulateDropdown");
            hud.SetCrop(initialCrop);
            UnityEngine.Object.Destroy(spriteA);
            UnityEngine.Object.Destroy(spriteB);
            string directory = SessionState.GetString("FarmSystemAuditReport", "../../Verification/UIIcons");
            Directory.CreateDirectory(directory);
            File.WriteAllText(directory + "/IconChecks.txt", report.ToString());
        }
    }

    private static T Read<T>(object target, string field) =>
        (T)target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);

    private static void Call(object target, string method) =>
        target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);

    private static void Check(bool condition, string message, StringBuilder report)
    {
        report.AppendLine((condition ? "PASS " : "FAIL ") + message);
        if (!condition) throw new Exception(message);
    }
}
