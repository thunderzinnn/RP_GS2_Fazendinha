using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class FarmMenuChecks
{
    private static FarmPlot plot;
    private static CropData crop;
    private static FarmTutorialController tutorial;
    private static int tutorialCoins;
    private static int tutorialSeeds;
    private static float tutorialWater;
    private static float tutorialFertilizer;
    private static string tutorialRewardId;

    public static void CheckPause()
    {
        var controller = UnityEngine.Object.FindFirstObjectByType<FarmUIController>();
        var canvas = controller.transform;
        var pauseButton = FarmMenuSetup.Find(canvas, "PauseButton").GetComponent<Button>();
        Canvas.ForceUpdateCanvases();
        var pauseRect = (RectTransform)pauseButton.transform;
        var pointer = new PointerEventData(EventSystem.current)
        {
            position = RectTransformUtility.WorldToScreenPoint(null, pauseRect.TransformPoint(pauseRect.rect.center)),
            button = PointerEventData.InputButton.Left
        };
        var hits = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointer, hits);
        Require(hits.Count > 0 && hits[0].gameObject.GetComponentInParent<Button>() == pauseButton,
            "Pause is visible and receives pointer input");
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, pointer, ExecuteEvents.pointerClickHandler);
        Require(controller.IsPaused && Time.timeScale == 0f, "Pause stops game time");
        FarmSystemChecks.Capture("PauseMenu");
        controller.OpenOptions();
        Require(Read<GameObject>(controller, "optionsPanel").activeSelf && Time.timeScale == 0f, "Options keep the game paused");
        FarmSystemChecks.Capture("PauseOptions");
        controller.CloseOptions();
        controller.ResumeGame();
        Require(!controller.IsPaused && Time.timeScale == 1f, "Resume restores game time");
        controller.OpenOptions();
        Require(controller.IsPaused && Time.timeScale == 0f, "Direct options access also pauses gameplay");
        controller.CloseOptions();
        controller.ResumeGame();
        var map = (RectTransform)FarmMenuSetup.Find(canvas, "MapTab");
        Require(map.sizeDelta.x == map.sizeDelta.y && map.sizeDelta.x <= 72f, "Map uses a compact square button");
        Require(map.anchoredPosition.y > -50f && map.parent.name == "Footer", "Map stays inside the visible footer area");
        var fertilizer = FarmMenuSetup.Find(canvas, "IconFertilizer").GetComponent<Image>();
        Require(fertilizer.rectTransform.sizeDelta.x == 100f && fertilizer.transform.parent.name == "FertilizerSlider",
            "Fertilizer icon is enlarged and anchored to its slider");
        var soil = UnityEngine.Object.FindFirstObjectByType<FarmPlot>().transform.Find("SoilMesh");
        Require(FarmPresentationSetup.MinimumSurfaceHeight(soil) >= GameObject.Find("Ground").GetComponent<Renderer>().bounds.max.y + 0.01f,
            "Ground does not intersect the upward-facing plot surface");
        Require(FarmCropPrefabRefresh.BoundsOf(GameObject.Find("FarmBarn")).size.z > 5.9f, "Barn enlarged to six units");
    }

    public static void CheckMainMenu()
    {
        Require(SceneManager.GetActiveScene().name == "MainMenu", "Separate main menu scene loaded");
        var controller = UnityEngine.Object.FindFirstObjectByType<FarmUIController>();
        Require(UnityEngine.Object.FindObjectsByType<AudioManager>(FindObjectsSortMode.None).Length == 1, "Only one audio singleton after scene change");
        FarmSystemChecks.Capture("MainMenu");
        controller.OpenOptions();
        Read<Slider>(controller, "musicSlider").value = 0.35f;
        Read<Slider>(controller, "effectsSlider").value = 0.45f;
        Require(Mathf.Approximately(AudioManager.Instance.MusicVolume, 0.35f) && Mathf.Approximately(AudioManager.Instance.EffectsVolume, 0.45f),
            "Both audio sliders control the singleton");
        Require(Read<TMP_Text>(controller, "musicValueText").text == "35%", "Volume label follows its slider");
        FarmSystemChecks.Capture("MainMenuOptions");
        controller.CloseOptions();
        Require(Read<GameObject>(controller, "mainMenuPanel").activeSelf, "Back returns to the main menu");
        Require(FarmMenuSetup.Find(controller.transform, "MainQuitButton").GetComponent<Button>().onClick.GetPersistentMethodName(0) == "QuitGame",
            "Quit button is connected to application exit");
    }

    public static void BeginTutorial()
    {
        Require(SceneManager.GetActiveScene().name == "Tutorial", "Play opens Tutorial");
        var allPlots = UnityEngine.Object.FindObjectsByType<FarmPlot>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        Require(allPlots.Length == 1, "Tutorial contains exactly one plot");
        plot = allPlots[0];
        tutorial = UnityEngine.Object.FindFirstObjectByType<FarmTutorialController>();
        crop = Read<CropData>(tutorial, "tutorialCrop");
        Require(crop.plantedSeconds + crop.growingSeconds == 15f && crop.harvestAmount == 4, "Tutorial crop grows in fifteen seconds and yields four");
        Require(UnityEngine.Object.FindFirstObjectByType<FarmMissionBoard>().CommunityCount == 1, "Tutorial has one community");
        Require(UnityEngine.Object.FindFirstObjectByType<FarmMissionBoard>().GetActiveMission(0).requiredAmount == 12, "Tutorial delivery needs twelve carrots");
        Require(GameObject.Find("FarmBarn") == null, "Tutorial excludes the decorative barn");
        Require(!Read<GameObject>(tutorial, "continueButton").activeSelf, "Cannot skip the delivery gate");
        Require(UnityEngine.Object.FindFirstObjectByType<PlayerFarmController>().Agent.isOnNavMesh, "Tutorial navigation is available");
        FarmSystemChecks.Capture("TutorialStart");
    }

    public static void BeginTutorialPlant()
    {
        var player = UnityEngine.Object.FindFirstObjectByType<PlayerFarmController>();
        typeof(PlayerFarmController).GetField("<LastSelectedPlot>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(player, plot);
        Tick();
        Require(tutorial.CurrentStep == FarmTutorialStep.PlantCrop, "Selecting the plot advances the guide");
        Require(player.RequestAction(plot, FarmTool.Plant, crop), "Tutorial planting uses the walking interaction");
    }

    public static void CheckTutorialRecovery()
    {
        var player = UnityEngine.Object.FindFirstObjectByType<PlayerFarmController>();
        var inventory = player.Inventory;
        var wallet = UnityEngine.Object.FindFirstObjectByType<FarmWallet>();
        int seeds = inventory.SeedCount(crop);
        int coins = wallet.Coins;
        try
        {
            SetStep(FarmTutorialStep.WaterCrop);
            Tick();
            Require(tutorial.CurrentStep == FarmTutorialStep.PlantCrop, "Empty plot does not leave watering instruction stuck");
            Require(plot.TryPlant(crop, inventory, out _) && plot.TryWater(player.Tank, out _), "Prepare mature crop before fertilizing");
            Grow();
            SetStep(FarmTutorialStep.FertilizeCrop);
            Tick();
            Require(Read<TMP_Text>(tutorial, "instructionText").text.Contains("already ready"), "Guide explains missed fertilizer timing");
            plot.ClearCrop();
            Tick();
            Require(tutorial.CurrentStep == FarmTutorialStep.PlantAgain, "Guide returns to planting after the crop is cleared");
            inventory.AddProduce(crop, 12);
            SetStep(FarmTutorialStep.DeliverMission);
            Require(UnityEngine.Object.FindFirstObjectByType<FarmShop>().SellProduce(crop, 1, out _), "Can sell part of the planned delivery");
            Tick();
            Require(tutorial.CurrentStep == FarmTutorialStep.CollectProduce, "Guide asks to collect again after selling delivery items");
        }
        finally
        {
            plot.ClearCrop();
            inventory.AddSeeds(crop, seeds - inventory.SeedCount(crop));
            int produce = inventory.ProduceCount(crop);
            if (produce > 0) inventory.TryRemoveProduce(crop, produce);
            if (wallet.Coins > coins) wallet.TrySpend(wallet.Coins - coins);
            player.Tank.SendMessage("InitializeWater");
            SetStep(FarmTutorialStep.SelectPlot);
            typeof(FarmTutorialController).GetField("wateredOnce", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(tutorial, false);
            typeof(FarmTutorialController).GetField("fertilizedOnce", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(tutorial, false);
            Tick();
        }
    }

    public static bool TutorialPlantReady => plot != null && plot.CurrentCrop == crop;

    public static void CompleteTutorial()
    {
        var player = UnityEngine.Object.FindFirstObjectByType<PlayerFarmController>();
        var inventory = player.Inventory;
        Tick();
        Require(tutorial.CurrentStep == FarmTutorialStep.WaterCrop, "Planting advances to watering");
        Require(plot.TryWater(player.Tank, out _), "Tutorial crop can be watered");
        Tick();
        Require(tutorial.CurrentStep == FarmTutorialStep.HarvestCrop, "Watering advances to harvesting");
        Grow();
        UnityEngine.Object.FindFirstObjectByType<FarmHUD>().RefreshHUD();
        FarmSystemChecks.Capture("TutorialHarvest");
        Require(plot.TryHarvest(inventory, out _), "First tutorial harvest succeeds");
        Tick();
        Require(tutorial.CurrentStep == FarmTutorialStep.PlantAgain, "Harvest introduces fertilizer");
        Require(plot.TryPlant(crop, inventory, out _), "Second tutorial planting succeeds");
        Tick();
        Require(plot.TryFertilize(out _), "Harvested fertilizer can be applied");
        Tick();
        Require(tutorial.CurrentStep == FarmTutorialStep.CollectProduce, "Fertilizing advances the guide");
        Require(plot.TryWater(player.Tank, out _), "Second crop can be watered");
        Grow();
        Require(plot.TryHarvest(inventory, out _), "Second tutorial harvest succeeds");
        Require(plot.TryPlant(crop, inventory, out _), "Third tutorial planting succeeds");
        Require(plot.TryWater(player.Tank, out _), "Third crop can be watered");
        Grow();
        Require(plot.TryHarvest(inventory, out _) && inventory.ProduceCount(crop) == 12, "Three harvests provide twelve carrots");
        Tick();
        Require(tutorial.CurrentStep == FarmTutorialStep.OpenMap, "Twelve carrots unlock the map instruction");
        var canvas = tutorial.transform;
        FarmMenuSetup.Find(canvas, "MapTab").GetComponent<Button>().onClick.Invoke();
        Tick();
        Require(tutorial.CurrentStep == FarmTutorialStep.DeliverMission, "Map leads to delivery");
        FarmMenuSetup.Find(canvas, "FirstCommunityButton").GetComponent<Button>().onClick.Invoke();
        FarmSystemChecks.Capture("TutorialMission");
        FarmMenuSetup.Find(canvas, "DeliverFirstMissionButton").GetComponent<Button>().onClick.Invoke();
        Tick();
        Require(tutorial.CurrentStep == FarmTutorialStep.Completed && Read<GameObject>(tutorial, "continueButton").activeSelf,
            "Delivery unlocks Start Farming");
        Require(inventory.ProduceCount(crop) == 0, "Mission consumes the twelve carrots");
        tutorialCoins = UnityEngine.Object.FindFirstObjectByType<FarmWallet>().Coins;
        tutorialSeeds = inventory.SeedCount(crop);
        tutorialWater = player.Tank.CurrentWater;
        tutorialFertilizer = UnityEngine.Object.FindFirstObjectByType<FarmFertilizerStorage>().CurrentFertilizer;
        tutorialRewardId = AssetDatabase.LoadAssetAtPath<MissionData>("Assets/_Project/Tutorial/TutorialMission.asset").rewardTechnology.SaveId;
        if (FarmSaveManager.Instance != null)
        {
            FarmSaveManager.Instance.CompleteTutorial();
            string prefix = FarmSaveManager.KeyPrefix + ".Farm";
            int savedCoins = PlayerPrefs.GetInt(prefix + ".Coins", -1);
            FarmSaveManager.Instance.CompleteTutorial();
            Require(PlayerPrefs.GetInt(prefix + ".Coins", -1) == savedCoins,
                "Tutorial completion does not transfer resources");
        }
        FarmSystemChecks.Capture("TutorialCompleted");
    }

    public static void ContinueTutorial() => tutorial.ContinueToGame();

    public static void CheckFullGame()
    {
        Require(SceneManager.GetActiveScene().name == "MainLevel" && FarmUIController.TutorialCompleted, "Completed tutorial opens MainLevel");
        var board = UnityEngine.Object.FindFirstObjectByType<FarmMissionBoard>();
        Require(board.CommunityCount == 3 && board.DemoMissionLimit == 12 && board.CompletedMissionCount == 0, "Full game retains its twelve separate missions");
        var carrot = AssetDatabase.LoadAssetAtPath<CropData>(FarmMenuSetup.Root + "Crops/Carrot.asset");
        Require(carrot.plantedSeconds + carrot.growingSeconds == 70f, "Full-game carrot duration remains seventy seconds");
        if (FarmSaveManager.Instance != null)
        {
            var wallet = UnityEngine.Object.FindFirstObjectByType<FarmWallet>();
            var inventory = UnityEngine.Object.FindFirstObjectByType<FarmInventory>();
            Require(wallet.Coins == wallet.StartingCoins, "Farm uses its own starting coins");
            Require(inventory.SeedCount(carrot) == inventory.StartingSeedCount(carrot), "Farm uses its own starting seeds");
            var reward = AssetDatabase.FindAssets("t:TechnologyData", new[] { "Assets/_Project/Tech" })
                .Select(guid => AssetDatabase.LoadAssetAtPath<TechnologyData>(AssetDatabase.GUIDToAssetPath(guid)))
                .First(item => item.SaveId == tutorialRewardId);
            Require(!UnityEngine.Object.FindFirstObjectByType<FarmTechnologyManager>().Has(reward), "Tutorial technology reward stays in the tutorial");
            var tank = UnityEngine.Object.FindFirstObjectByType<FarmWaterTank>();
            var fertilizer = UnityEngine.Object.FindFirstObjectByType<FarmFertilizerStorage>();
            Require(Mathf.Abs(tank.CurrentWater - tank.StartingWater) < 2f &&
                Mathf.Abs(fertilizer.CurrentFertilizer - fertilizer.StartingFertilizer) < 1f,
                "Farm starts with its own water and fertilizer");
        }
        Require(UnityEngine.Object.FindObjectsByType<AudioManager>(FindObjectsSortMode.None).Length == 1 &&
            Mathf.Approximately(AudioManager.Instance.MusicVolume, 0.35f), "Audio and volume survive all three scenes");
        FarmSystemChecks.Capture("GameAfterTutorial");
    }

    private static void Grow()
    {
        plot.TickGrowth(crop.plantedSeconds, crop.plantedSeconds, plot.Growing);
        plot.TickGrowth(crop.growingSeconds, crop.growingSeconds, plot.Grown);
        Require(plot.Machine.CurrentState == plot.Grown, "Tutorial crop reaches harvest state");
    }

    private static void Tick() => typeof(FarmTutorialController).GetMethod("UpdateTutorialProgress", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(tutorial, null);
    private static void SetStep(FarmTutorialStep step) => typeof(FarmTutorialController).GetField("<CurrentStep>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(tutorial, step);
    private static T Read<T>(object target, string name) => (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
}
