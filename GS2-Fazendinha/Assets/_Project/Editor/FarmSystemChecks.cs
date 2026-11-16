using System;
using System.IO;
using System.Reflection;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class FarmSystemChecks
{
    private const string SessionKey = "FarmSystemAuditRunning";
    private const string ExitKey = "FarmSystemAuditExitCode";
    private static string ReportDirectory => SessionState.GetString("FarmSystemAuditReport", "../../Verification/SystemAudit");
    private static StringBuilder report;
    private static int failures;
    private static int phase;
    private static int plotIndex;
    private static double phaseStarted;
    private static FarmPlot[] plots;
    private static CropData navigationCrop;
    private static AudioManager persistentAudio;
    private static FarmPlot fertilizerPlot;

    public static void Run()
    {
        StartChecks("../../Verification/SystemAudit");
    }

    public static void RunAssetRefresh()
    {
        StartChecks("../../Verification/AssetRefresh");
    }

    public static void RunPresentation()
    {
        SessionState.SetBool("FarmMenuAuditEnabled", true);
        FarmUIController.TutorialCompleted = false;
        StartChecks("../../Verification/Presentation");
    }

    public static void RunRefinement()
    {
        SessionState.SetBool("FarmMenuAuditEnabled", true);
        FarmUIController.TutorialCompleted = false;
        StartChecks("../../Verification/Refinement");
    }

    public static void RunPlayerPrefs()
    {
        FarmSaveChecks.CleanupPreviousTestProfile();
        SessionState.SetBool("FarmMenuAuditEnabled", true);
        SessionState.SetBool("FarmPlayerPrefsAudit", true);
        FarmUIController.TutorialCompleted = false;
        StartChecks("../../Verification/PlayerPrefs");
    }

    public static void RunFertilizer()
    {
        FarmFertilizerSetup.Apply();
        StartChecks("../../Verification/Fertilizer");
    }

    public static void RunPermanentUpgrades()
    {
        FarmPermanentUpgradeSetup.Apply();
        StartChecks("../../Verification/PermanentUpgrades");
    }

    public static void RunUIIcons()
    {
        FarmUIIconSetup.Apply();
        StartChecks("../../Verification/UIIcons");
    }

    public static void RunCropGroups()
    {
        FarmCropGroupSetup.Apply();
        StartChecks("../../Verification/CropGroups");
    }

    private static void StartChecks(string directory)
    {
        Directory.CreateDirectory(directory);
        string savePrefix = "GoldenRootTest_" + Guid.NewGuid().ToString("N");
        SessionState.SetString("FarmAuditSavePrefix", savePrefix);
        File.WriteAllText(directory + "/TestPrefix.txt", savePrefix);
        FarmSaveManager.KeyPrefix = savePrefix;
        FarmSaveManager.AutomaticSavingEnabled = false;
        EditorSceneManager.OpenScene("Assets/_Project/Scenes/MainMenu.unity");
        SessionState.SetString("FarmSystemAuditReport", directory);
        SessionState.SetInt(ExitKey, -1);
        SessionState.SetBool(SessionKey, true);
        EditorApplication.isPlaying = true;
    }

    [InitializeOnLoadMethod]
    private static void Restore()
    {
        if (SessionState.GetInt(ExitKey, -1) >= 0)
        {
            EditorApplication.update += ExitAfterPlayMode;
            return;
        }
        if (!SessionState.GetBool(SessionKey, false)) return;
        FarmSaveManager.KeyPrefix = SessionState.GetString("FarmAuditSavePrefix", "GoldenRootTest");
        FarmSaveManager.AutomaticSavingEnabled = false;
        report = new StringBuilder();
        phase = -1;
        failures = 0;
        phaseStarted = EditorApplication.timeSinceStartup;
        EditorApplication.update += Tick;
        Application.logMessageReceived += ObserveLog;
    }

    private static void Tick()
    {
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        if (EditorApplication.timeSinceStartup - phaseStarted < 1d) return;
        try
        {
            if (phase == -1)
            {
                Require(AudioManager.Instance != null, "MainMenu initializes persistent audio");
                SceneManager.LoadScene("MainLevel");
                phase = 0;
                phaseStarted = EditorApplication.timeSinceStartup;
                return;
            }
            if (phase == 0)
            {
                Test("Scene and navigation setup", CheckScene);
                Test("Permanent upgrade UI and initial water", CheckPermanentUpgrades);
                if (Read<CropData[]>(UnityEngine.Object.FindFirstObjectByType<FarmPlotContextMenu>(), "availableCrops")[2].CropName == "Lettuce")
                    Test("New crop groups, approved values and model stages", FarmCropGroupChecks.Run);
                Test("Plant, hydrate, grow, harvest and die", CheckCrops);
                Test("Seasonal market and technology purchases", CheckMarket);
                if (Read<Image>(UnityEngine.Object.FindFirstObjectByType<FarmHUD>(), "shopCropIcon") != null)
                    Test("Crop and technology icon references and selection", FarmUIIconChecks.Run);
                if (UnityEngine.Object.FindFirstObjectByType<FarmFertilizerStorage>() != null)
                    Test("Fertilizer and worm composter rules", FarmFertilizerChecks.Run);
                Test("Mission rotation and all configured deliveries", CheckMissions);
                Test("Plot expansion purchases", CheckExpansion);
                Test("Persistent audio singleton", CheckAudio);
                plots = UnityEngine.Object.FindObjectsByType<FarmPlot>(FindObjectsSortMode.None);
                plotIndex = 0;
                phase = 1;
                phaseStarted = EditorApplication.timeSinceStartup;
            }
            else if (phase == 1)
            {
                if (plotIndex >= plots.Length)
                {
                    if (UnityEngine.Object.FindFirstObjectByType<FarmFertilizerStorage>() != null)
                        BeginFertilizerNavigation();
                    else
                        CompleteNavigation();
                    return;
                }
                var player = UnityEngine.Object.FindFirstObjectByType<PlayerFarmController>();
                var menu = UnityEngine.Object.FindFirstObjectByType<FarmPlotContextMenu>();
                navigationCrop = Read<CropData[]>(menu, "availableCrops")[0];
                plots[plotIndex].ClearCrop();
                player.Inventory.AddSeeds(navigationCrop, 1);
                if (!player.RequestAction(plots[plotIndex], FarmTool.Plant, navigationCrop))
                {
                    Record(false, "Navigation request for " + plots[plotIndex].name);
                    plotIndex++;
                    return;
                }
                phase = 2;
                phaseStarted = EditorApplication.timeSinceStartup;
            }
            else if (phase == 2)
            {
                var plot = plots[plotIndex];
                if (plot.CurrentCrop == navigationCrop)
                {
                    Record(true, "Walk to and plant: " + plot.name);
                    plot.ClearCrop();
                    plotIndex++;
                    phase = 1;
                    phaseStarted = EditorApplication.timeSinceStartup;
                }
                else if (EditorApplication.timeSinceStartup - phaseStarted > 20d)
                {
                    Record(false, "Walk to and plant timed out: " + plot.name);
                    plotIndex++;
                    phase = 1;
                    phaseStarted = EditorApplication.timeSinceStartup;
                }
            }
            else if (phase == 3)
            {
                Test("Audio and UI survive scene reload", CheckReloadedAudio);
                Test("Timed water regeneration", CheckRegeneration);
                Test("Fertilizer test data is not saved", CheckFreshFertilizer);
                if (SessionState.GetBool("FarmMenuAuditEnabled", false))
                {
                    Test("Pause menu and polished presentation", FarmMenuChecks.CheckPause);
                    SceneManager.LoadScene("MainMenu");
                    phase = 7;
                    phaseStarted = EditorApplication.timeSinceStartup;
                }
                else Finish();
            }
            else if (phase == 7)
            {
                Test("Main menu and audio options", FarmMenuChecks.CheckMainMenu);
                UnityEngine.Object.FindFirstObjectByType<FarmUIController>().PlayGame();
                phase = 8;
                phaseStarted = EditorApplication.timeSinceStartup;
            }
            else if (phase == 8)
            {
                Test("Single-plot tutorial setup and navigation", FarmMenuChecks.BeginTutorial);
                Test("Tutorial recovery after out-of-order actions", FarmMenuChecks.CheckTutorialRecovery);
                FarmMenuChecks.BeginTutorialPlant();
                phase = 9;
                phaseStarted = EditorApplication.timeSinceStartup;
            }
            else if (phase == 9)
            {
                if (FarmMenuChecks.TutorialPlantReady)
                {
                    Test("Tutorial steps, fertilizer and twelve-carrot delivery", FarmMenuChecks.CompleteTutorial);
                    FarmMenuChecks.ContinueTutorial();
                    phase = 10;
                    phaseStarted = EditorApplication.timeSinceStartup;
                }
                else if (EditorApplication.timeSinceStartup - phaseStarted > 20d)
                {
                    Record(false, "Tutorial walking interaction timed out");
                    Finish();
                }
            }
            else if (phase == 10)
            {
                Test("Tutorial unlocks full game and preserves audio", FarmMenuChecks.CheckFullGame);
                if (SessionState.GetBool("FarmPlayerPrefsAudit", false))
                    Test("PlayerPrefs save, zero values, restoration and cross-session snapshot", FarmSaveChecks.PrepareSnapshot);
                Finish();
            }
            else if (phase == 4)
            {
                if (fertilizerPlot.IsFertilized)
                {
                    Test("Walk to crop and apply fertilizer through contextual button", CheckFertilizerNavigation);
                    CompleteNavigation();
                }
                else if (EditorApplication.timeSinceStartup - phaseStarted > 20d)
                {
                    Record(false, "Fertilizer navigation timed out");
                    CompleteNavigation();
                }
            }
            else if (phase == 5)
            {
                Test("Market dropdown icons and upgrade navigation", CheckDropdownPanels);
                var menu = UnityEngine.Object.FindFirstObjectByType<FarmPlotContextMenu>();
                menu.Show(plots[0], new Vector2(800f, 450f));
                phase = 6;
                phaseStarted = EditorApplication.timeSinceStartup;
            }
            else if (phase == 6)
            {
                Test("Planting dropdown icon rows", CheckPlantDropdown);
                persistentAudio = AudioManager.Instance;
                SceneManager.LoadScene("MainLevel");
                phase = 3;
                phaseStarted = EditorApplication.timeSinceStartup;
            }
        }
        catch (Exception exception)
        {
            Record(false, exception.ToString());
            Finish();
        }
    }

    private static void CheckScene()
    {
        var player = UnityEngine.Object.FindFirstObjectByType<PlayerFarmController>();
        Require(player != null && player.Agent.isOnNavMesh, "Player is on NavMesh");
        Require(UnityEngine.Object.FindObjectsByType<FarmPlot>(FindObjectsSortMode.None).Length == 1, "One plot starts unlocked");
        Require(Time.timeScale == 1f, "Game time runs normally");
        foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject) == 0, "No missing script: " + transform.name);
        var map = Find("MapTab").GetComponent<Button>();
        map.onClick.Invoke();
        Require(Find("MapPanel").activeInHierarchy, "Map button opens map");
        Require(Time.timeScale == 1f, "Map does not pause time");
        Find("CloseMapButton").GetComponent<Button>().onClick.Invoke();
        var fertilizer = UnityEngine.Object.FindFirstObjectByType<FarmFertilizerStorage>();
        if (fertilizer != null)
        {
            Require(fertilizer.CurrentFertilizer == 0f, "Fertilizer starts at zero");
            Capture("InitialGameplay");
        }
    }

    private static void CheckPermanentUpgrades()
    {
        var hud = UnityEngine.Object.FindFirstObjectByType<FarmHUD>();
        var tank = UnityEngine.Object.FindFirstObjectByType<FarmWaterTank>();
        var shop = UnityEngine.Object.FindFirstObjectByType<FarmShop>();
        Require(Read<float>(tank, "startingWater") == 50f && tank.CurrentWater < 52f,
            "Tank starts at fifty water");
        var growing = Read<TechnologyData>(hud, "shopTechnologyA");
        var worm = Read<TechnologyData>(hud, "shopTechnologyB");
        Require(growing.effect == TechnologyEffect.ShowGrowingTime && !growing.marketExclusive && growing.price == 100,
            "Growing Time is a permanent upgrade for one hundred coins");
        Require(worm.effect == TechnologyEffect.WormComposter && !worm.marketExclusive && worm.price == 150,
            "Worm Composter is a permanent upgrade for one hundred fifty coins");
        foreach (var item in Read<TechnologyData[]>(shop, "exclusiveTechnologyCatalog"))
            Require(item != growing && item != worm, "Permanent upgrades are absent from seasonal catalog");
        Find("TechnologiesTab").GetComponent<Button>().onClick.Invoke();
        hud.RefreshHUD();
        foreach (string field in new[] { "growingTimeUpgradeButton", "wormComposterUpgradeButton" })
        {
            var button = Read<Button>(hud, field);
            Require(button.gameObject.activeInHierarchy, "Permanent upgrade button is visible");
            Require(!button.interactable, "Upgrade button blocks insufficient coins");
        }
        Capture("PermanentUpgradesAvailable");
        Find("CloseUpgradesButton").GetComponent<Button>().onClick.Invoke();
    }

    private static void BeginFertilizerNavigation()
    {
        var menu = UnityEngine.Object.FindFirstObjectByType<FarmPlotContextMenu>();
        var storage = UnityEngine.Object.FindFirstObjectByType<FarmFertilizerStorage>();
        UnityEngine.Object.FindFirstObjectByType<FarmWormComposter>().enabled = false;
        typeof(FarmFertilizerStorage).GetProperty("CurrentFertilizer").SetValue(storage, 20f);
        fertilizerPlot = plots[0];
        fertilizerPlot.ClearCrop();
        var crop = Read<CropData[]>(menu, "availableCrops")[0];
        var inventory = UnityEngine.Object.FindFirstObjectByType<FarmInventory>();
        inventory.AddSeeds(crop, 1);
        Require(fertilizerPlot.TryPlant(crop, inventory, out _), "Prepare crop for navigation test");
        menu.Show(fertilizerPlot, new Vector2(800f, 450f));
        var button = Read<Button>(menu, "fertilizeButton");
        Require(button.gameObject.activeInHierarchy && button.interactable, "Fertilizer button is available");
        button.onClick.Invoke();
        Require(!fertilizerPlot.IsFertilized && storage.CurrentFertilizer == 20f, "Fertilizer is not spent before reaching the plot");
        phase = 4;
        phaseStarted = EditorApplication.timeSinceStartup;
    }

    private static void CheckFertilizerNavigation()
    {
        var storage = UnityEngine.Object.FindFirstObjectByType<FarmFertilizerStorage>();
        Require(storage.CurrentFertilizer == 0f, "Exactly twenty fertilizer is spent on arrival");
        var menu = UnityEngine.Object.FindFirstObjectByType<FarmPlotContextMenu>();
        menu.Show(fertilizerPlot, new Vector2(800f, 450f));
        Require(!Read<Button>(menu, "fertilizeButton").interactable, "Repeat application button is disabled");
        UnityEngine.Object.FindFirstObjectByType<FarmHUD>().RefreshHUD();
        Capture("FertilizedCrop");
        menu.Close();
        fertilizerPlot.ClearCrop();
    }

    private static void CompleteNavigation()
    {
        Test("Interface navigation and screenshots", CheckPanels);
        // Aguarda os dropdowns iniciarem no painel antes de abrir suas listas.
        phase = 5;
        phaseStarted = EditorApplication.timeSinceStartup;
    }

    private static void CheckFreshFertilizer()
    {
        var storage = UnityEngine.Object.FindFirstObjectByType<FarmFertilizerStorage>();
        if (storage == null) return;
        var composter = UnityEngine.Object.FindFirstObjectByType<FarmWormComposter>();
        Require(storage.CurrentFertilizer == 0f && !composter.IsUnlocked, "Reload starts with empty reserve and locked composter");
        Require(!Read<GameObject>(composter, "composterModel").activeSelf, "Reloaded composter model is hidden");
    }

    private static void CheckCrops()
    {
        var menu = UnityEngine.Object.FindFirstObjectByType<FarmPlotContextMenu>();
        var inventory = UnityEngine.Object.FindFirstObjectByType<FarmInventory>();
        var tank = UnityEngine.Object.FindFirstObjectByType<FarmWaterTank>();
        var plot = UnityEngine.Object.FindFirstObjectByType<FarmPlot>();
        foreach (var source in Read<CropData[]>(menu, "availableCrops"))
        {
            tank.SendMessage("InitializeWater");
            Require(source.plantedPrefab != null && source.growingPrefab != null && source.grownPrefab != null && source.deadPrefab != null, "All crop visuals: " + source.CropName);
            var crop = UnityEngine.Object.Instantiate(source);
            crop.plantedSeconds = 2f;
            crop.growingSeconds = 3f;
            crop.initialHydration = 100f;
            crop.hydrationLossPerSecond = 0f;
            plot.ClearCrop();
            inventory.AddSeeds(crop, 2);
            Require(plot.TryPlant(crop, inventory, out _), "Plant succeeds");
            Require(plot.GrowthPercentage == 0f, "Growth starts at zero");
            var indicator = plot.GetComponentInChildren<CropWorldIndicator>(true);
            indicator.SendMessage("LateUpdate");
            Require(Mathf.Approximately(Read<Image>(indicator, "hydrationRing").fillAmount, 1f), "Hydration ring fills at full water");
            plot.TickGrowth(1f, 2f, plot.Growing);
            Require(Mathf.Abs(plot.GrowthPercentage - 20f) < 0.01f, "Total growth includes both stages");
            plot.TickGrowth(1f, 2f, plot.Growing);
            Require(plot.Machine.CurrentState == plot.Growing && Mathf.Abs(plot.GrowthPercentage - 40f) < 0.01f, "Growth stays continuous across states");
            plot.TickGrowth(3f, 3f, plot.Grown);
            Require(plot.Machine.CurrentState == plot.Grown && plot.GrowthPercentage == 100f, "Crop reaches 100 percent");
            indicator.SendMessage("LateUpdate");
            Require(Read<Image>(indicator, "readyMarker").gameObject.activeSelf, "Ready marker appears");
            int produce = inventory.ProduceCount(crop);
            Require(plot.TryHarvest(inventory, out _), "Harvest succeeds");
            Require(inventory.ProduceCount(crop) == produce + crop.harvestAmount && plot.CurrentCrop == null, "Harvest adds produce and clears plot");
            crop.initialHydration = 0f;
            crop.plantedSeconds = 100f;
            Require(plot.TryPlant(crop, inventory, out _), "Replant succeeds");
            plot.TickGrowth(1f, 100f, plot.Growing);
            Require(plot.StageTimer == 0f, "Dry crop pauses growth");
            indicator.SendMessage("LateUpdate");
            Require(Read<Image>(indicator, "dryMarker").gameObject.activeSelf, "Dry warning appears");
            Require(plot.TryWater(tank, out _), "Watering succeeds");
            Require(plot.Hydration > 0f && plot.DryTimeRemaining > 9f, "Watering resets dry countdown");
            crop.hydrationLossPerSecond = 1000f;
            plot.TickGrowth(11f, 100f, plot.Growing);
            Require(plot.Machine.CurrentState == plot.Dead, "Dry crop enters dead state");
            plot.StageTimer = plot.DeadVisualSeconds;
            plot.Dead.Tick();
            Require(plot.Machine.CurrentState == plot.Empty, "Dead visual expires and releases plot");
            UnityEngine.Object.Destroy(crop);
        }
        tank.SendMessage("InitializeWater");
    }

    private static void CheckMarket()
    {
        var shop = UnityEngine.Object.FindFirstObjectByType<FarmShop>();
        var wallet = UnityEngine.Object.FindFirstObjectByType<FarmWallet>();
        var inventory = UnityEngine.Object.FindFirstObjectByType<FarmInventory>();
        var technologies = UnityEngine.Object.FindFirstObjectByType<FarmTechnologyManager>();
        wallet.AddCoins(10000);
        var crops = Read<CropData[]>(shop, "cropCatalog");
        bool foundUnavailable = false;
        foreach (var crop in crops)
        {
            int beforeCoins = wallet.Coins;
            int beforeSeeds = inventory.SeedCount(crop);
            bool bought = shop.BuySeeds(crop, 1, out _);
            Require(bought == shop.IsSeedAvailable(crop), "Seasonal availability is enforced");
            Require(wallet.Coins == beforeCoins - (bought ? crop.seedPrice : 0), "Seed payment matches purchase");
            Require(inventory.SeedCount(crop) == beforeSeeds + (bought ? 1 : 0), "Seed inventory matches purchase");
            foundUnavailable |= !bought;
        }
        Require(foundUnavailable, "Some seeds rotate out of season");
        var demand = shop.HighDemandCrop;
        inventory.AddProduce(demand, 1);
        int coins = wallet.Coins;
        Require(shop.SellProduce(demand, 1, out _), "Market sale succeeds");
        Require(wallet.Coins == coins + shop.GetSellPrice(demand), "Demand price is paid");
        int revision = shop.MarketRevision;
        shop.TickMarket(shop.RefreshTimeRemaining + 0.1f);
        Require(shop.MarketRevision > revision && shop.HighDemandCrop != demand, "Market refresh rotates demand");
        var menu = UnityEngine.Object.FindFirstObjectByType<FarmPlotContextMenu>();
        var growthTechnology = Read<TechnologyData>(menu, "growthTimeTechnology");
        Require(!(bool)Call(menu, "CanShowGrowth"), "Growth information starts locked");
        var hud = UnityEngine.Object.FindFirstObjectByType<FarmHUD>();
        hud.RefreshHUD();
        var bulkSeedButton = Read<Button>(hud, "buyFiveSeedsButton");
        if (bulkSeedButton != null)
        {
            var purchaseCrop = Array.Find(crops, item => shop.IsSeedAvailable(item));
            hud.SetCrop(purchaseCrop);
            int seedCoins = wallet.Coins;
            int seedCount = inventory.SeedCount(purchaseCrop);
            Read<Button>(hud, "buySeedsButton").onClick.Invoke();
            Require(wallet.Coins == seedCoins - purchaseCrop.seedPrice &&
                inventory.SeedCount(purchaseCrop) == seedCount + 1,
                "Primary seed button purchases one at its unit price");
            bulkSeedButton.onClick.Invoke();
            Require(wallet.Coins == seedCoins - purchaseCrop.seedPrice * 6 &&
                inventory.SeedCount(purchaseCrop) == seedCount + 6,
                "Bulk seed button purchases five at five times the unit price");
        }
        Require(!shop.IsTechnologyAvailable(growthTechnology), "Growth technology is not seasonal");
        int upgradeCoins = wallet.Coins;
        Read<Button>(hud, "growingTimeUpgradeButton").onClick.Invoke();
        Require(technologies.Has(growthTechnology) && wallet.Coins == upgradeCoins - growthTechnology.price,
            "Permanent growth upgrade charges its configured price");
        Require((bool)Call(menu, "CanShowGrowth"), "Growth information unlocks through Upgrades");
        Require(!Read<Button>(hud, "growingTimeUpgradeButton").interactable,
            "Purchased growth upgrade cannot be bought again");
        var tankTechnology = Array.Find(Read<TechnologyData[]>(shop, "exclusiveTechnologyCatalog"), item => item != null && item.effect == TechnologyEffect.TankCapacityBonus);
        var tank = UnityEngine.Object.FindFirstObjectByType<FarmWaterTank>();
        float capacity = tank.Capacity;
        Require(tankTechnology != null, "Tank capacity technology configured");
        technologies.Unlock(tankTechnology);
        Require(Mathf.Abs(tank.Capacity - capacity - Read<float>(tank, "baseCapacity") * tankTechnology.value) < 0.01f, "Capacity bonus uses percentage of base");
    }

    private static void CheckMissions()
    {
        var board = UnityEngine.Object.FindFirstObjectByType<FarmMissionBoard>();
        var inventory = UnityEngine.Object.FindFirstObjectByType<FarmInventory>();
        Require(board.CommunityCount == 3, "All three communities are configured");
        for (int index = 0; index < board.CommunityCount; index++)
        {
            Require(board.GetActiveMission(index) != null, "Each community starts with a mission");
        }
        board.TickMissionTimers(board.GetTimeRemaining(0) + 0.1f);
        Require(board.IsCoolingDown(0), "Expired mission enters cooldown");
        board.TickMissionTimers(board.GetTimeRemaining(0) + 0.1f);
        Require(board.GetActiveMission(0) != null, "Cooldown offers another mission");
        int safety = 0;
        while (!board.IsDemoComplete && safety++ < 30)
        {
            for (int index = 0; index < board.CommunityCount; index++)
            {
                var mission = board.GetActiveMission(index);
                if (mission == null) continue;
                inventory.AddProduce(mission.requiredCrop, mission.requiredAmount);
                Require(board.CanDeliver(index), "Configured mission can be delivered: " + mission.name);
                Require(board.Deliver(index, out _), "Delivery succeeds");
                Require(!board.Deliver(index, out _), "Delivery cannot be repeated during cooldown");
            }
            board.TickMissionTimers(400f);
        }
        Require(board.CompletedMissionCount == board.DemoMissionLimit, "All twelve missions can be completed once");
        Require(board.CommunitiesServed == 3, "All three communities receive deliveries");
    }

    private static void CheckExpansion()
    {
        var expansion = UnityEngine.Object.FindFirstObjectByType<FarmPlotExpansion>();
        var wallet = UnityEngine.Object.FindFirstObjectByType<FarmWallet>();
        var button = Read<Button>(expansion, "buyPlotButton");
        wallet.AddCoins(10000);
        for (int i = 0; i < 3; i++)
        {
            int price = expansion.NextPlotPrice;
            int coins = wallet.Coins;
            button.onClick.Invoke();
            Require(expansion.PurchasedPlots == i + 1 && wallet.Coins == coins - price, "Plot button unlocks and charges configured price");
            Require(Read<GameObject[]>(expansion, "extraPlots")[i].activeSelf, "Purchased plot becomes active");
        }
        int finalCoins = wallet.Coins;
        expansion.BuyNextPlot();
        Require(expansion.AllPlotsUnlocked && expansion.TotalAvailablePlots == 4 && wallet.Coins == finalCoins, "Four-plot limit prevents further charge");
    }

    private static void CheckAudio()
    {
        var audio = AudioManager.Instance;
        Require(audio != null, "Audio manager is present");
        Require(audio.gameObject.scene.name == "DontDestroyOnLoad", "Audio persists between scenes");
        Require(Read<AudioSource>(audio, "musicSource") != Read<AudioSource>(audio, "effectsSource"), "Music and effects use separate sources");
        audio.SetMusicVolume(0.3f);
        audio.SetEffectsVolume(0.4f);
        var mixer = Read<UnityEngine.Audio.AudioMixer>(audio, "mixer");
        Require(mixer.GetFloat("MusicVolume", out float music) && Mathf.Abs(music - Mathf.Log10(0.3f) * 20f) < 0.001f, "Music mixer volume is independent");
        Require(mixer.GetFloat("SFXVolume", out float effects) && Mathf.Abs(effects - Mathf.Log10(0.4f) * 20f) < 0.001f, "Effects mixer volume is independent");
        var duplicate = new GameObject("Duplicate Audio Test").AddComponent<AudioManager>();
        Require(AudioManager.Instance == audio, "Duplicate does not replace singleton");
        UnityEngine.Object.Destroy(duplicate.gameObject);
    }

    private static void CheckPanels()
    {
        Capture("Gameplay");
        Find("MapTab").GetComponent<Button>().onClick.Invoke();
        Capture("Map");
        foreach (string prefix in new[] { "First", "Second", "Third" })
        {
            Find(prefix + "CommunityButton").GetComponent<Button>().onClick.Invoke();
            Require(Find(prefix + "CommunityPanel").activeInHierarchy, prefix + " community opens");
            if (prefix == "Third") Capture("ThirdCommunity");
            Find("BackFrom" + prefix + "CommunityButton").GetComponent<Button>().onClick.Invoke();
            Require(Find("MapPanel").activeInHierarchy && !Find(prefix + "CommunityPanel").activeInHierarchy, "Back closes selected community");
        }
        Find("MarketMapButton").GetComponent<Button>().onClick.Invoke();
        Require(Find("ShopPanel").activeInHierarchy && !Find("MapPanel").activeInHierarchy, "Market opens alone");
        Capture("Market");
    }

    private static void CheckDropdownPanels()
    {
        var cropDropdown = Find("ShopCropDropdown").GetComponent<TMP_Dropdown>();
        if (cropDropdown.itemImage != null)
        {
            ShowDropdown(cropDropdown, "CropDropdownIcons");
            ShowDropdown(Find("MarketTechnologyDropdown").GetComponent<TMP_Dropdown>(), "TechnologyDropdownIcons");
        }
        Find("BackFromMarketButton").GetComponent<Button>().onClick.Invoke();
        Find("CloseMapButton").GetComponent<Button>().onClick.Invoke();
        Find("TechnologiesTab").GetComponent<Button>().onClick.Invoke();
        Capture("Upgrades");
        Find("CloseUpgradesButton").GetComponent<Button>().onClick.Invoke();
        Require(Time.timeScale == 1f, "Panels keep game time running");
    }

    private static void CheckPlantDropdown()
    {
        var menu = UnityEngine.Object.FindFirstObjectByType<FarmPlotContextMenu>();
        var dropdown = Read<TMP_Dropdown>(menu, "cropDropdown");
        if (dropdown.itemImage != null) ShowDropdown(dropdown, "PlantDropdownIcons");
        menu.Close();
    }

    private static void ShowDropdown(TMP_Dropdown dropdown, string screenshot)
    {
        float fade = dropdown.alphaFadeSpeed;
        dropdown.alphaFadeSpeed = 0f;
        try
        {
            dropdown.Show();
            var list = dropdown.transform.Find("Dropdown List");
            Require(list != null, "Dropdown opens its option list: " + dropdown.name);
            var rows = list.GetComponentsInChildren<Toggle>(false);
            Require(rows.Length == dropdown.options.Count, "Dropdown displays all configured options");
            foreach (var row in rows)
                Require(row.transform.Find("OptionIconFrame/OptionIcon") != null,
                    "Every option has its reserved icon space");
            Capture(screenshot);
        }
        finally
        {
            dropdown.Hide();
            dropdown.alphaFadeSpeed = fade;
        }
    }

    private static void CheckReloadedAudio()
    {
        Require(AudioManager.Instance == persistentAudio, "Same audio instance survives scene reload");
        Require(UnityEngine.Object.FindObjectsByType<AudioManager>(FindObjectsSortMode.None).Length == 1, "Duplicate audio is removed after reload");
        var hud = UnityEngine.Object.FindFirstObjectByType<FarmHUD>();
        var button = Find("MapTab").GetComponent<Button>();
        bool connected = false;
        for (int index = 0; index < button.onClick.GetPersistentEventCount(); index++)
            connected |= button.onClick.GetPersistentTarget(index) == hud && button.onClick.GetPersistentMethodName(index) == "PlayUIClick";
        Require(connected, "Reloaded UI resolves the persistent audio through its HUD");
        button.onClick.Invoke();
        Require(Find("MapPanel").activeInHierarchy, "Reloaded map still opens");
    }

    private static void CheckRegeneration()
    {
        var tank = UnityEngine.Object.FindFirstObjectByType<FarmWaterTank>();
        tank.SendMessage("InitializeWater");
        float before = tank.CurrentWater;
        float amount = Read<float>(tank, "regenerationAmount");
        typeof(FarmWaterTank).GetField("regenerationTimer", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(tank, Read<float>(tank, "regenerationInterval"));
        Call(tank, "UpdateRegeneration");
        Require(Mathf.Approximately(tank.CurrentWater, before + amount), "Completed interval restores the configured water amount");
        Require(tank.RegenerationTimeRemaining > 0f, "Next regeneration countdown is available");
    }

    public static void Capture(string name)
    {
        if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
        var camera = Camera.main;
        var canvas = (GameObject.Find("Main Canvas") ?? GameObject.Find("MainMenuCanvas")).GetComponent<Canvas>();
        var mode = canvas.renderMode;
        var previousCamera = canvas.worldCamera;
        float distance = canvas.planeDistance;
        var previousTarget = camera.targetTexture;
        var previousActive = RenderTexture.active;
        var target = new RenderTexture(1600, 900, 24);
        var image = new Texture2D(1600, 900, TextureFormat.RGB24, false);
        try
        {
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1f;
            camera.targetTexture = target;
            Canvas.ForceUpdateCanvases();
            foreach (var text in canvas.GetComponentsInChildren<TMP_Text>(false)) text.ForceMeshUpdate();
            camera.Render();
            camera.Render();
            RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
            image.Apply();
            Directory.CreateDirectory(ReportDirectory);
            File.WriteAllBytes(ReportDirectory + "/" + name + ".png", image.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = previousTarget;
            RenderTexture.active = previousActive;
            canvas.renderMode = mode;
            canvas.worldCamera = previousCamera;
            canvas.planeDistance = distance;
            UnityEngine.Object.Destroy(image);
            UnityEngine.Object.Destroy(target);
        }
    }

    private static GameObject Find(string name)
    {
        foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                if (transform.name == name) return transform.gameObject;
        throw new Exception("Object not found: " + name);
    }

    private static T Read<T>(object target, string field) =>
        (T)target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);

    private static object Call(object target, string method) =>
        target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    private static void Test(string name, Action action)
    {
        try { action(); Record(true, name); }
        catch (Exception exception) { Record(false, name + ": " + exception); }
    }

    private static void Record(bool success, string message)
    {
        if (!success) failures++;
        report.AppendLine((success ? "PASS " : "FAIL ") + message);
    }

    private static void ObserveLog(string message, string trace, LogType type)
    {
        if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert)
            Record(false, "Runtime log: " + message);
    }

    private static void Finish()
    {
        SessionState.SetBool(SessionKey, false);
        EditorApplication.update -= Tick;
        Application.logMessageReceived -= ObserveLog;
        report.AppendLine("Failures: " + failures);
        Directory.CreateDirectory(ReportDirectory);
        File.WriteAllText(ReportDirectory + "/PlayModeChecks.txt", report.ToString());
        SessionState.SetInt(ExitKey, failures == 0 ? 0 : 1);
        EditorApplication.update += ExitAfterPlayMode;
        EditorApplication.isPlaying = false;
    }

    private static void ExitAfterPlayMode()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        int code = SessionState.GetInt(ExitKey, -1);
        if (code < 0) return;
        SessionState.SetInt(ExitKey, -1);
        EditorApplication.update -= ExitAfterPlayMode;
        EditorApplication.Exit(code);
    }
}
