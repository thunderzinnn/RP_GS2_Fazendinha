using System;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;

public static class FarmFertilizerChecks
{
    private static readonly StringBuilder report = new StringBuilder();

    public static void Run()
    {
        report.Clear();
        var storage = UnityEngine.Object.FindFirstObjectByType<FarmFertilizerStorage>();
        var composter = UnityEngine.Object.FindFirstObjectByType<FarmWormComposter>();
        var plot = UnityEngine.Object.FindFirstObjectByType<FarmPlot>();
        var inventory = UnityEngine.Object.FindFirstObjectByType<FarmInventory>();
        var wallet = UnityEngine.Object.FindFirstObjectByType<FarmWallet>();
        var shop = UnityEngine.Object.FindFirstObjectByType<FarmShop>();
        var tank = UnityEngine.Object.FindFirstObjectByType<FarmWaterTank>();
        var source = Read<CropData[]>(UnityEngine.Object.FindFirstObjectByType<FarmPlotContextMenu>(), "availableCrops")[0];
        var crop = UnityEngine.Object.Instantiate(source);
        crop.plantedSeconds = 40f;
        crop.growingSeconds = 60f;
        crop.initialHydration = 100f;
        crop.hydrationLossPerSecond = 0f;
        crop.harvestAmount = 3;
        composter.enabled = false;

        try
        {
            SetAmount(storage, 0f);
            Check(!composter.IsUnlocked, "Worm composter starts locked");
            composter.TickGeneration(500f);
            Check(storage.CurrentFertilizer == 0f, "Locked composter generates nothing");
            Check(!Read<GameObject>(composter, "composterModel").activeSelf, "Composter model starts hidden");

            Plant(plot, inventory, crop);
            Check(!plot.TryFertilize(out _), "Application fails without fertilizer");
            Check(storage.CurrentFertilizer == 0f && !plot.IsFertilized, "Failed application changes neither balance nor crop");
            Mature(plot, crop);
            int beforeProduce = inventory.ProduceCount(crop);
            Check(plot.TryHarvest(inventory, out _), "Normal crop can be harvested");
            Check(storage.CurrentFertilizer == 20f, "Normal harvest adds 20 percent of capacity");
            Check(inventory.ProduceCount(crop) == beforeProduce + 3, "Normal harvest keeps base produce count");
            Check(!plot.TryHarvest(inventory, out _) && storage.CurrentFertilizer == 20f, "Repeated harvest gives no extra fertilizer");

            Plant(plot, inventory, crop);
            plot.TickGrowth(20f, crop.plantedSeconds, plot.Growing);
            float percentage = plot.GrowthPercentage;
            Check(plot.TryFertilize(out _), "Fertilizer can be applied during planted state");
            Check(storage.CurrentFertilizer == 0f, "Application spends exactly 20");
            Check(plot.GrowthPercentage == percentage, "Applying fertilizer does not jump growth percentage");
            Check(!plot.TryFertilize(out _) && storage.CurrentFertilizer == 0f, "Second application is rejected without another charge");
            plot.TickGrowth(15f, crop.plantedSeconds, plot.Growing);
            Check(plot.Machine.CurrentState == plot.Growing && Mathf.Abs(plot.GrowthPercentage - 40f) < 0.001f,
                "Remaining planted time falls from 20 to 15 seconds");
            plot.TickGrowth(44.9f, crop.growingSeconds, plot.Grown);
            Check(plot.Machine.CurrentState == plot.Growing, "Fertilized crop does not finish before reduced remaining time");
            plot.TickGrowth(0.11f, crop.growingSeconds, plot.Grown);
            Check(plot.Machine.CurrentState == plot.Grown, "80 seconds remaining becomes 60 hydrated seconds");
            beforeProduce = inventory.ProduceCount(crop);
            Check(plot.TryHarvest(inventory, out _), "Fertilized crop can be harvested");
            Check(storage.CurrentFertilizer == 5f, "Fertilized harvest gives only 5 percent fertilizer");
            Check(inventory.ProduceCount(crop) == beforeProduce + 3, "Fertilizer alone does not add produce before composter unlock");

            SetAmount(storage, 20f);
            Plant(plot, inventory, crop);
            plot.TickGrowth(40f, crop.plantedSeconds, plot.Growing);
            Check(plot.TryFertilize(out _), "Application also works during growing state");
            plot.TickGrowth(45.01f, crop.growingSeconds, plot.Grown);
            Check(plot.Machine.CurrentState == plot.Grown, "Growing-stage remaining time is reduced by 25 percent");
            plot.ClearCrop();
            Check(!plot.IsFertilized, "Clearing the crop resets its fertilizer flag");

            crop.initialHydration = 0f;
            SetAmount(storage, 20f);
            Plant(plot, inventory, crop);
            Check(plot.TryFertilize(out _), "Dry crop can receive fertilizer without receiving water");
            plot.TickGrowth(3f, crop.plantedSeconds, plot.Growing);
            Check(plot.StageTimer == 0f && plot.Hydration == 0f, "Fertilizer never grows or hydrates a dry crop");
            tank.SendMessage("InitializeWater");
            Check(plot.TryWater(tank, out _), "Fertilized crop can still be watered");
            plot.TickGrowth(3f, crop.plantedSeconds, plot.Growing);
            Check(Mathf.Abs(plot.StageTimer - 4f) < 0.001f, "Growth resumes at improved speed after watering");
            crop.hydrationLossPerSecond = 1000f;
            plot.TickGrowth(11f, crop.plantedSeconds, plot.Growing);
            Check(plot.Machine.CurrentState == plot.Dead && storage.CurrentFertilizer == 0f, "Fertilized crop still dies when dry and gives no harvest reward");
            plot.StageTimer = plot.DeadVisualSeconds;
            plot.Dead.Tick();
            Check(!plot.IsFertilized && plot.Machine.CurrentState == plot.Empty, "Death cleanup resets fertilization");

            crop.initialHydration = 100f;
            crop.hydrationLossPerSecond = 0f;
            SetAmount(storage, 20f);
            Plant(plot, inventory, crop);
            Check(plot.TryFertilize(out _), "Crop can be fertilized before buying composter");
            Check(plot.GetHarvestAmount() == 3, "Pre-unlock fertilized yield stays at three");
            var technology = Read<TechnologyData>(composter, "unlockTechnology");
            wallet.AddCoins(1000);
            int coins = wallet.Coins;
            Check(!shop.IsTechnologyAvailable(technology), "Composter is not a seasonal offer");
            Check(!shop.BuyMarketTechnology(technology, out _) && wallet.Coins == coins,
                "Seasonal purchase path rejects permanent composter without charging");
            var permanentHud = UnityEngine.Object.FindFirstObjectByType<FarmHUD>();
            permanentHud.RefreshHUD();
            Read<Button>(permanentHud, "wormComposterUpgradeButton").onClick.Invoke();
            Check(wallet.Coins == coins - 150 && composter.IsUnlocked, "Technology costs 150 and unlocks the composter");
            Check(!Read<Button>(permanentHud, "wormComposterUpgradeButton").interactable,
                "Purchased composter button is disabled");
            int afterPurchaseCoins = wallet.Coins;
            Check(!shop.BuyTechnology(technology, out _) && wallet.Coins == afterPurchaseCoins,
                "Repeated permanent purchase is rejected without payment");
            composter.SendMessage("UpdateUnlockState");
            Check(Read<GameObject>(composter, "composterModel").activeSelf, "Composter model appears after purchase");
            Check(Mathf.Abs(storage.GrowthTimeReduction - 0.5f) < 0.001f,
                "Composter raises fertilizer time reduction from 25 to 50 percent");
            float progressBeforePurchaseEffect = plot.GrowthPercentage;
            plot.TickGrowth(2f, crop.plantedSeconds, plot.Growing);
            Check(Mathf.Abs(plot.StageTimer - 4f) < 0.001f && plot.GrowthPercentage > progressBeforePurchaseEffect,
                "Existing fertilized crop immediately grows twice as fast after purchase");
            Check(plot.GetHarvestAmount() == 5, "Three produce becomes five with 35 percent bonus rounded up");
            crop.harvestAmount = 5;
            Check(plot.GetHarvestAmount() == 7, "Five produce becomes seven with upward rounding");
            crop.harvestAmount = 3;
            Mature(plot, crop);
            beforeProduce = inventory.ProduceCount(crop);
            Check(plot.TryHarvest(inventory, out _), "Improved fertilized crop can be harvested");
            Check(inventory.ProduceCount(crop) == beforeProduce + 5 && storage.CurrentFertilizer == 5f,
                "Composter improves produce but fertilized crop still returns only five fertilizer");

            SetAmount(storage, 0f);
            Plant(plot, inventory, crop);
            plot.TickGrowth(5f, crop.plantedSeconds, plot.Growing);
            Check(plot.StageTimer == 5f, "Composter never accelerates an unfertilized crop");
            Mature(plot, crop);
            beforeProduce = inventory.ProduceCount(crop);
            plot.TryHarvest(inventory, out _);
            Check(inventory.ProduceCount(crop) == beforeProduce + 3 && storage.CurrentFertilizer == 20f,
                "Unfertilized crops keep base yield and 20 fertilizer after composter unlock");

            SetAmount(storage, 20f);
            Plant(plot, inventory, crop);
            Check(plot.TryFertilize(out _), "New crop receives improved fertilizer after purchase");
            plot.TickGrowth(20f, crop.plantedSeconds, plot.Growing);
            Check(plot.Machine.CurrentState == plot.Growing, "Improved fertilizer halves planted stage duration");
            plot.TickGrowth(29.99f, crop.growingSeconds, plot.Grown);
            Check(plot.Machine.CurrentState == plot.Growing, "Improved crop does not finish before fifty seconds");
            plot.TickGrowth(0.02f, crop.growingSeconds, plot.Grown);
            Check(plot.Machine.CurrentState == plot.Grown, "Improved fertilizer halves total hydrated growth time");
            plot.ClearCrop();

            for (int index = 0; index < 8; index++)
            {
                shop.TickMarket(shop.RefreshTimeRemaining + 0.1f);
                for (int offer = 0; offer < shop.TechnologyOfferCount; offer++)
                    Check(shop.GetTechnologyOffer(offer).effect != TechnologyEffect.ShowGrowingTime &&
                        shop.GetTechnologyOffer(offer).effect != TechnologyEffect.WormComposter,
                        "Market rotation never offers permanent farm upgrades");
            }

            SetAmount(storage, 0f);
            composter.TickGeneration(4f);
            Check(storage.CurrentFertilizer == 0f && Mathf.Abs(composter.GenerationTimeRemaining - 1f) < 0.001f,
                "Composter waits for a full five-second interval");
            composter.TickGeneration(1f);
            Check(storage.CurrentFertilizer == 1f, "Composter generates one fertilizer every five seconds");
            composter.TickGeneration(12f);
            Check(storage.CurrentFertilizer == 3f && Mathf.Abs(composter.GenerationTimeRemaining - 3f) < 0.001f,
                "Composter keeps partial interval time");
            storage.AddFertilizer(1000f);
            composter.TickGeneration(1000f);
            Check(storage.CurrentFertilizer == 100f, "Reserve never exceeds 100");
            storage.TrySpendForApplication();
            composter.TickGeneration(4f);
            Check(storage.CurrentFertilizer == 80f, "Full reserve does not bank generation time");
            composter.TickGeneration(1f);
            Check(storage.CurrentFertilizer == 81f, "Generation resumes after spending fertilizer");
            SetAmount(storage, 98f);
            Check(storage.AddHarvestReward(false) == 2f && storage.CurrentFertilizer == 100f,
                "Harvest reward is capped at remaining storage space");

            var hud = UnityEngine.Object.FindFirstObjectByType<FarmHUD>();
            hud.RefreshHUD();
            Check(Read<Slider>(hud, "fertilizerAmountSlider").value == 100f, "Fertilizer HUD follows shared reserve");
            Check(!Read<TMPro.TMP_Text>(hud, "fertilizerAmountText").enableAutoSizing,
                "Fertilizer text keeps the configured readable size");
            Check(!Read<Slider>(hud, "fertilizerAmountSlider").interactable,
                "Fertilizer HUD is read-only");
            Check(Read<TMPro.TMP_Text>(hud, "fertilizerGenerationTimerText").gameObject.activeSelf,
                "Generation timer is shown after technology unlock");
        }
        finally
        {
            plot.ClearCrop();
            tank.SendMessage("InitializeWater");
            composter.enabled = true;
            UnityEngine.Object.Destroy(crop);
            string directory = SessionState.GetString("FarmSystemAuditReport", "../../Verification/Fertilizer");
            Directory.CreateDirectory(directory);
            File.WriteAllText(directory + "/Rules.txt", report.ToString());
        }
    }

    private static void Plant(FarmPlot plot, FarmInventory inventory, CropData crop)
    {
        plot.ClearCrop();
        inventory.AddSeeds(crop, 1);
        if (!plot.TryPlant(crop, inventory, out string message)) throw new Exception(message);
    }

    private static void Mature(FarmPlot plot, CropData crop)
    {
        if (plot.Machine.CurrentState == plot.Planted)
            plot.TickGrowth(crop.plantedSeconds, crop.plantedSeconds, plot.Growing);
        if (plot.Machine.CurrentState == plot.Growing)
            plot.TickGrowth(crop.growingSeconds, crop.growingSeconds, plot.Grown);
    }

    private static void SetAmount(FarmFertilizerStorage storage, float amount) =>
        typeof(FarmFertilizerStorage).GetProperty("CurrentFertilizer").SetValue(storage, amount);

    private static T Read<T>(object target, string field) =>
        (T)target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);

    private static void Check(bool condition, string message)
    {
        report.AppendLine((condition ? "PASS " : "FAIL ") + message);
        if (!condition) throw new Exception(message);
    }
}
