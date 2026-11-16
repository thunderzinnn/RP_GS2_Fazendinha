using System;
using System.IO;
using System.Reflection;
using System.Text;

using UnityEditor;
using UnityEngine;

public static class FarmCropGroupChecks
{
    public static void Run()
    {
        var report = new StringBuilder();
        string[] names = { "Carrot", "Corn", "Lettuce", "Wheat", "Sunflower" };
        int[] plants = { 4, 3, 9, 10, 5 };
        int[] harvest = { 4, 9, 9, 10, 5 };
        int[] seeds = { 20, 36, 36, 50, 40 };
        int[] sales = { 10, 10, 12, 20, 25 };
        float[] planted = { 30f, 40f, 50f, 60f, 60f };
        float[] growing = { 40f, 80f, 40f, 85f, 90f };
        float[] initial = { 12f, 10f, 40f, 2f, 6.6f };
        float[] loss = { 1.5f, 1.8f, 2f, 2f, 3.2f };
        float[] cost = { 12f, 18f, 16f, 20f, 10f };
        float[] recovery = { 37f, 45f, 34f, 50f, 35f };
        var menu = UnityEngine.Object.FindFirstObjectByType<FarmPlotContextMenu>();
        var crops = Read<CropData[]>(menu, "availableCrops");
        var inventory = UnityEngine.Object.FindFirstObjectByType<FarmInventory>();
        var plot = UnityEngine.Object.FindFirstObjectByType<FarmPlot>();
        var soil = plot.transform.Find("SoilMesh").GetComponent<Renderer>();
        var camera = Camera.main;
        camera.aspect = 1600f / 900f;
        try
        {
            Check(crops.Length == 5, "Five active cultures", report);
            for (int index = 0; index < names.Length; index++)
            {
                var crop = crops[index];
                Check(crop.CropName == names[index], "Active culture: " + names[index], report);
                Check(crop.harvestAmount == harvest[index] && crop.seedPrice == seeds[index] && crop.sellPrice == sales[index],
                    "Approved yield and prices: " + crop.CropName, report);
                Check(Mathf.Approximately(crop.plantedSeconds, planted[index]) && Mathf.Approximately(crop.growingSeconds, growing[index]),
                    "Approved growth duration: " + crop.CropName, report);
                Check(Mathf.Approximately(crop.initialHydration, initial[index]) && Mathf.Approximately(crop.hydrationLossPerSecond, loss[index]) &&
                    Mathf.Approximately(crop.waterCost, cost[index]) && Mathf.Approximately(crop.hydrationPerWatering, recovery[index]),
                    "Approved hydration and watering values: " + crop.CropName, report);

                foreach (var prefab in new[] { crop.plantedPrefab, crop.growingPrefab, crop.grownPrefab })
                {
                    Check(prefab != null && prefab.transform.childCount == plants[index], "Grouped prefab plant count: " + crop.CropName, report);
                    Check(prefab.transform.localPosition == Vector3.zero, "Prefab origin is plot-local: " + prefab.name, report);
                }
                Check(crop.deadPrefab != null && (AssetDatabase.GetAssetPath(crop.deadPrefab).Contains("PlaceHolder") ||
                    crop.CropName == "Carrot"), "Provisional dead model retained: " + crop.CropName, report);

                var temporary = UnityEngine.Object.Instantiate(crop);
                temporary.initialHydration = 100f;
                temporary.hydrationLossPerSecond = 0f;
                temporary.plantedSeconds = 0.2f;
                temporary.growingSeconds = 0.2f;
                try
                {
                    plot.ClearCrop();
                    inventory.AddSeeds(temporary, 2);
                    Check(plot.TryPlant(temporary, inventory, out _), "Plant grouped crop: " + crop.CropName, report);
                    CheckStage(plot, soil, plants[index], crop.CropName + " Seed", report);
                    FarmSystemChecks.Capture(crop.CropName + "_Seed");
                    plot.TickGrowth(0.2f, 0.2f, plot.Growing);
                    Check(plot.Machine.CurrentState == plot.Growing, "Enter Growing: " + crop.CropName, report);
                    CheckStage(plot, soil, plants[index], crop.CropName + " Growing", report);
                    FarmSystemChecks.Capture(crop.CropName + "_Growing");
                    plot.TickGrowth(0.2f, 0.2f, plot.Grown);
                    Check(plot.Machine.CurrentState == plot.Grown, "Enter Grown: " + crop.CropName, report);
                    CheckStage(plot, soil, plants[index], crop.CropName + " Grown", report);
                    FarmSystemChecks.Capture(crop.CropName + "_Grown");
                    int before = inventory.ProduceCount(temporary);
                    Check(plot.TryHarvest(inventory, out _) && inventory.ProduceCount(temporary) == before + harvest[index],
                        "Harvest gives approved amount: " + crop.CropName, report);
                    temporary.initialHydration = 0f;
                    Check(plot.TryPlant(temporary, inventory, out _), "Replant for death check: " + crop.CropName, report);
                    plot.TickGrowth(11f, 0.2f, plot.Growing);
                    Check(plot.Machine.CurrentState == plot.Dead && Read<GameObject>(plot, "currentModelInstance").name.StartsWith(crop.deadPrefab.name),
                        "Dry crop uses retained dead model: " + crop.CropName, report);
                    plot.StageTimer = plot.DeadVisualSeconds;
                    plot.Dead.Tick();
                    Check(plot.Machine.CurrentState == plot.Empty, "Dead crop releases plot: " + crop.CropName, report);
                }
                finally { plot.ClearCrop(); UnityEngine.Object.Destroy(temporary); }
            }

            var hud = UnityEngine.Object.FindFirstObjectByType<FarmHUD>();
            Check(Read<CropData[]>(hud, "availableCrops")[2].CropName == "Lettuce" && Read<CropData[]>(hud, "availableCrops")[3].CropName == "Wheat",
                "HUD uses Lettuce and Wheat", report);
            foreach (var crop in Read<CropData[]>(UnityEngine.Object.FindFirstObjectByType<FarmShop>(), "cropCatalog"))
                Check(Array.IndexOf(names, crop.CropName) >= 0, "Market references active cultures only", report);
            foreach (var entry in Read<InitialSeeds[]>(inventory, "startingSeeds"))
                Check(Array.IndexOf(names, entry.crop.CropName) >= 0, "Starting inventory references active cultures only", report);
            foreach (string guid in AssetDatabase.FindAssets("t:MissionData", new[] { "Assets/_Project/Missions" }))
            {
                var mission = AssetDatabase.LoadAssetAtPath<MissionData>(AssetDatabase.GUIDToAssetPath(guid));
                Check(Array.IndexOf(names, mission.requiredCrop.CropName) >= 0, "Mission references active culture: " + mission.name, report);
            }
        }
        finally
        {
            camera.ResetAspect();
            plot.ClearCrop();
            string directory = SessionState.GetString("FarmSystemAuditReport", "../../Verification/CropGroups");
            Directory.CreateDirectory(directory);
            File.WriteAllText(directory + "/CropChecks.txt", report.ToString());
        }
    }

    private static void CheckStage(FarmPlot plot, Renderer soil, int count, string label, StringBuilder report)
    {
        var model = Read<GameObject>(plot, "currentModelInstance");
        Check(model != null && model.activeInHierarchy && model.transform.childCount == count, "Visible grouped stage: " + label, report);
        var renderers = model.GetComponentsInChildren<Renderer>();
        foreach (var renderer in renderers)
        {
            Check(Mathf.Abs(renderer.bounds.min.y - soil.bounds.max.y) < 0.02f, "Plant base meets soil: " + label, report);
            foreach (var material in renderer.sharedMaterials)
                Check(material != null && material.shader.name == "Universal Render Pipeline/Lit" && material.mainTexture != null &&
                    material.mainTexture.name == "textura-universal", "Original textured material: " + label, report);
        }
        var bounds = renderers[0].bounds;
        foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
        Check(bounds.min.x >= soil.bounds.min.x - 0.25f && bounds.max.x <= soil.bounds.max.x + 0.25f &&
            bounds.min.z >= soil.bounds.min.z - 0.25f && bounds.max.z <= soil.bounds.max.z + 0.25f,
            "Group stays within plot footprint tolerance: " + label, report);
        var camera = Camera.main;
        for (int x = -1; x <= 1; x += 2)
            for (int y = -1; y <= 1; y += 2)
                for (int z = -1; z <= 1; z += 2)
                {
                    Vector3 point = bounds.center + Vector3.Scale(bounds.extents, new Vector3(x, y, z));
                    Vector3 view = camera.WorldToViewportPoint(point);
                    Check(view.z > 0f && view.x >= 0.02f && view.x <= 0.98f && view.y >= 0.08f && view.y <= 0.98f,
                        "Crop is visible within camera framing: " + label, report);
                }
        plot.GetComponentInChildren<CropWorldIndicator>(true).SendMessage("LateUpdate");
    }

    private static T Read<T>(object target, string field) =>
        (T)target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);

    private static void Check(bool condition, string message, StringBuilder report)
    {
        report.AppendLine((condition ? "PASS " : "FAIL ") + message);
        if (!condition) throw new Exception(message);
    }
}
