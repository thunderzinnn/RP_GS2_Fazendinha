using System;
using System.IO;

using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class FarmFeedbackChecks
{
    private const string Session = "FarmFeedbackChecks";
    private const string Report = "../../Verification/Feedback/";
    private static readonly StringBuilder results = new StringBuilder();
    private static int phase;
    private static double started;

    public static void Run()
    {
        SessionState.SetBool(Session, true);
        SessionState.SetString("FarmFeedbackPrefix", "GoldenRootTest_Feedback_" + Guid.NewGuid().ToString("N"));
        SessionState.SetString("FarmSystemAuditReport", Report);
        ConfigureProfile();
        string tutorial = FarmSaveManager.KeyPrefix + ".Tutorial";
        PlayerPrefs.SetInt(tutorial + ".HasSave", 1);
        PlayerPrefs.SetInt(tutorial + ".Coins", 9999);
        PlayerPrefs.SetInt(tutorial + ".TutorialStep", 9);
        PlayerPrefs.Save();
        EditorSceneManager.OpenScene("Assets/_Project/Scenes/Tutorial.unity");
        EditorApplication.isPlaying = true;
    }

    [InitializeOnLoadMethod]
    private static void Restore()
    {
        if (!SessionState.GetBool(Session, false)) return;
        ConfigureProfile();
        phase = 0;
        started = EditorApplication.timeSinceStartup;
        EditorApplication.update += Tick;
    }

    private static void ConfigureProfile()
    {
        FarmSaveManager.KeyPrefix = SessionState.GetString("FarmFeedbackPrefix", "GoldenRootTest_Feedback");
        FarmSaveManager.AutomaticSavingEnabled = false;
    }

    private static void Tick()
    {
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.timeSinceStartup - started < 1d) return;
        try
        {
            if (phase == 0)
            {
                var save = FarmSaveManager.Instance;
                var tutorial = UnityEngine.Object.FindFirstObjectByType<FarmTutorialController>();
                Require(!save.CanSaveProgress && !save.HasSavedProgress, "Tutorial ignores legacy saves");
                Require(tutorial.CurrentStep == FarmTutorialStep.SelectPlot, "Tutorial starts at its first instruction");
                Require(UnityEngine.Object.FindFirstObjectByType<FarmWallet>().Coins != 9999, "Tutorial uses initial resources");
                save.SaveGame();
                Require(PlayerPrefs.GetInt(save.ProfilePrefix + ".Coins") == 9999, "Tutorial Save does not write gameplay values");
                var canvas = UnityEngine.Object.FindFirstObjectByType<FarmHUD>().transform;
                Require(!FarmMenuSetup.Find(canvas, "SaveGameButton").gameObject.activeSelf, "Tutorial Save button hidden");
                FarmMenuSetup.Find(canvas, "MapTab").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
                Canvas.ForceUpdateCanvases();
                var label = FarmMenuSetup.Find(canvas, "FirstCommunityButton").GetComponentInChildren<TMP_Text>();
                label.ForceMeshUpdate();
                Require(!label.isTextOverflowing, "Tutorial community label fits its button");
                FarmSystemChecks.Capture("TutorialMap");
                PlayerPrefs.SetInt(FarmSaveManager.KeyPrefix + ".Farm.Coins", 777);
                typeof(FarmTutorialController).GetProperty("CurrentStep").SetValue(tutorial, FarmTutorialStep.Completed);
                save.CompleteTutorial();
                Require(PlayerPrefs.GetInt(FarmSaveManager.KeyPrefix + ".Farm.Coins") == 777, "Tutorial completion does not transfer money");
                Require(!PlayerPrefs.HasKey(FarmSaveManager.KeyPrefix + ".Farm.HasSave"), "Tutorial does not create a farm save");
                SceneManager.LoadScene("MainLevel");
                phase = 1;
            }
            else if (phase == 1)
            {
                Time.timeScale = 0f;
                var save = FarmSaveManager.Instance;
                var wallet = UnityEngine.Object.FindFirstObjectByType<FarmWallet>();
                var shop = UnityEngine.Object.FindFirstObjectByType<FarmShop>();
                var technologies = UnityEngine.Object.FindFirstObjectByType<FarmTechnologyManager>();
                Require(wallet.Coins == wallet.StartingCoins, "Farm starts with its own resources");
                Require(GameObject.Find("FarmBarn").transform.localScale == Vector3.one * 2.2f, "Barn scale is 2.2");
                var market = new SerializedObject(shop).FindProperty("exclusiveTechnologyCatalog");
                Require(market.arraySize == 10, "All ten market technologies connected");
                wallet.AddCoins(10000);
                for (int index = 0; index < market.arraySize; index++)
                {
                    var technology = (TechnologyData)market.GetArrayElementAtIndex(index).objectReferenceValue;
                    for (int rotation = 0; rotation < 8 && !shop.IsTechnologyAvailable(technology); rotation++) shop.TickMarket(211f);
                    Require(shop.BuyTechnology(technology, out string message), "Purchase " + technology.DisplayName + ": " + message);
                    Require(technologies.Has(technology), "Technology ownership applied: " + technology.DisplayName);
                }
                CheckTechnologyEffects(technologies);
                save.SaveGame();
                int coins = wallet.Coins;
                wallet.AddCoins(1);
                save.LoadGame();
                Require(wallet.Coins == coins, "Farm money still saves and loads");
                Require(Mathf.Abs(technologies.GetBonus(TechnologyEffect.GrowthSpeedBonus) - 0.08f) < 0.0001f,
                    "New technology ownership survives save/load");
                var board = UnityEngine.Object.FindFirstObjectByType<FarmMissionBoard>();
                Require(board.DemoMissionLimit == 12 && board.CompletedMissionCount == 0, "Farm has twelve independent missions");
                for (int delivery = 0; delivery < 12; delivery++)
                {
                    int community = delivery % board.CommunityCount;
                    var mission = board.GetActiveMission(community);
                    if (mission == null) { board.TickMissionTimers(board.GetTimeRemaining(community) + 0.01f); mission = board.GetActiveMission(community); }
                    Require(mission != null, "Delivery mission available " + delivery);
                    UnityEngine.Object.FindFirstObjectByType<FarmInventory>().AddProduce(mission.requiredCrop, mission.requiredAmount);
                    Require(board.Deliver(community, out string message), "Mission delivery " + (delivery + 1) + ": " + message);
                }
                phase = 2;
            }
            else if (phase == 2)
            {
                var controller = UnityEngine.Object.FindFirstObjectByType<FarmDemoEndController>();
                var overlay = FarmMenuSetup.Find(controller.transform, "DemoEndOverlay");
                Require(overlay.gameObject.activeSelf && Time.timeScale == 0f, "Demo-end panel opens after twelve deliveries and pauses");
                FarmSystemChecks.Capture("EndOfDemo");
                controller.ContinueFarming();
                Require(!overlay.gameObject.activeSelf && Time.timeScale == 1f, "Continue resumes the farm");
                FarmSaveManager.Instance.SaveGame();
                controller.ReturnToMainMenu();
                phase = 3;
            }
            else if (phase == 3)
            {
                Require(SceneManager.GetActiveScene().name == "MainMenu", "Demo-end Main Menu action loads MainMenu");
                FarmUIController.TutorialCompleted = true;
                UnityEngine.Object.FindFirstObjectByType<FarmUIController>().PlayGame();
                phase = 4;
            }
            else if (phase == 4)
            {
                Require(SceneManager.GetActiveScene().name == "Tutorial", "Play always opens the tutorial even after completion");
                var tutorial = UnityEngine.Object.FindFirstObjectByType<FarmTutorialController>();
                Require(tutorial.CurrentStep == FarmTutorialStep.SelectPlot, "Re-entering the tutorial starts from zero");
                var wallet = UnityEngine.Object.FindFirstObjectByType<FarmWallet>();
                Require(wallet.Coins == wallet.StartingCoins, "Re-entering the tutorial resets training resources");
                typeof(FarmTutorialController).GetProperty("CurrentStep").SetValue(tutorial, FarmTutorialStep.Completed);
                tutorial.ContinueToGame();
                phase = 5;
            }
            else if (phase == 5)
            {
                Require(SceneManager.GetActiveScene().name == "MainLevel", "Tutorial finish returns to the farm");
                Require(UnityEngine.Object.FindFirstObjectByType<FarmMissionBoard>().CompletedMissionCount == 12,
                    "Farm retains all twelve completed missions after repeating the tutorial");
                var ending = UnityEngine.Object.FindFirstObjectByType<FarmDemoEndController>();
                Require(ending.IsOpen, "Saved demo completion opens the end panel on re-entry");
                Finish(0);
                return;
            }
            started = EditorApplication.timeSinceStartup;
        }
        catch (Exception error)
        {
            results.AppendLine("FAIL: " + error);
            Finish(1);
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        results.AppendLine("PASS: " + message);
    }

    private static void CheckTechnologyEffects(FarmTechnologyManager technologies)
    {
        Require(Mathf.Abs(technologies.GetBonus(TechnologyEffect.GrowthSpeedBonus) - 0.08f) < 0.0001f, "Growth technologies total +8% speed");
        var fertilizer = UnityEngine.Object.FindFirstObjectByType<FarmFertilizerStorage>();
        Require(Mathf.Abs(fertilizer.AddHarvestReward(false) - 23f) < 0.0001f, "Normal harvest gives 23 fertilizer with both bonuses");
        Require(Mathf.Abs(fertilizer.AddHarvestReward(true) - 5.75f) < 0.0001f, "Fertilized harvest gives 5.75 fertilizer with both bonuses");
        var worm = UnityEngine.Object.FindFirstObjectByType<FarmWormComposter>();
        float before = fertilizer.CurrentFertilizer;
        worm.TickGeneration(5f);
        Require(fertilizer.CurrentFertilizer == before, "Regeneration upgrades do not bypass the locked composter");
        technologies.Unlock(AssetDatabase.LoadAssetAtPath<TechnologyData>("Assets/_Project/Tech/FarmUpgrades/Worm Composter.asset"));
        worm.TickGeneration(5f);
        Require(Mathf.Abs(fertilizer.CurrentFertilizer - before - 1.1f) < 0.0001f, "Upgraded composter generates 1.10 per five seconds");
        var plot = UnityEngine.Object.FindFirstObjectByType<FarmPlot>();
        var crop = AssetDatabase.LoadAssetAtPath<CropData>("Assets/_Project/Crops/Carrot.asset");
        var inventory = UnityEngine.Object.FindFirstObjectByType<FarmInventory>();
        inventory.AddSeeds(crop, 1);
        Require(plot.TryPlant(crop, inventory, out _), "Plant crop for growth bonus test");
        plot.TickGrowth(2f, crop.plantedSeconds, plot.Growing);
        Require(Mathf.Abs(plot.StageTimer - 2.16f) < 0.0001f, "Growth speed bonus applies without fertilizer");
        Require(plot.TryFertilize(out _), "Apply fertilizer alongside growth technologies");
        float elapsed = plot.StageTimer;
        plot.TickGrowth(1f, crop.plantedSeconds, plot.Growing);
        Require(Mathf.Abs(plot.StageTimer - elapsed - 2.16f) < 0.0001f, "Growth technologies combine with worm fertilizer");
        Require(plot.GetHarvestAmount() == 6, "Worm harvest bonus remains +35%, rounded up");
        typeof(FarmPlot).GetProperty("Hydration").SetValue(plot, 0f);
        elapsed = plot.StageTimer;
        plot.TickGrowth(1f, crop.plantedSeconds, plot.Growing);
        Require(plot.StageTimer == elapsed, "Growth technology does not grow dry plants");
        plot.ClearCrop();
    }

    private static void Finish(int code)
    {
        FarmSaveChecks.CleanupTestKeys();
        Directory.CreateDirectory(Report);
        File.WriteAllText(Report + "PlayModeChecks.txt", results + "Failures: " + code + "\n");
        SessionState.SetBool(Session, false);
        SessionState.SetInt("FarmFeedbackExit", code);
        EditorApplication.update -= Tick;
        EditorApplication.update += Exit;
        EditorApplication.isPlaying = false;
    }

    private static void Exit()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
        EditorApplication.Exit(SessionState.GetInt("FarmFeedbackExit", 1));
    }
}
