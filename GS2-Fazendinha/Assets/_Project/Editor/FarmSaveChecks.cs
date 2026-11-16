using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

using UnityEngine.UI;

public static class FarmSaveChecks
{
    private const string Report = "../../Verification/PlayerPrefs/";
    private const string ReloadKey = "FarmSaveReloadCheck";
    private static double started;

    public static void PrepareSnapshot()
    {
        var save = FarmSaveManager.Instance;
        Require(save != null && save.IsReady, "Save manager initialized");
        Require(FarmSaveManager.KeyPrefix.StartsWith("GoldenRootTest_"), "Test profile is isolated");
        Time.timeScale = 0f;
        var wallet = UnityEngine.Object.FindFirstObjectByType<FarmWallet>();
        var inventory = UnityEngine.Object.FindFirstObjectByType<FarmInventory>();
        var tech = UnityEngine.Object.FindFirstObjectByType<FarmTechnologyManager>();
        var fertilizer = UnityEngine.Object.FindFirstObjectByType<FarmFertilizerStorage>();
        var crops = Read<CropData[]>(save, "crops");
        var catalog = Read<TechnologyData[]>(save, "technologyCatalog");
        wallet.AddCoins(10000);
        foreach (var crop in crops) { inventory.AddSeeds(crop, 20); inventory.AddProduce(crop, 7); }
        tech.Unlock(catalog.First(item => item.effect == TechnologyEffect.WormComposter));
        tech.Unlock(catalog.First(item => item.effect == TechnologyEffect.TankCapacityBonus));
        var expansion = UnityEngine.Object.FindFirstObjectByType<FarmPlotExpansion>();
        while (!expansion.AllPlotsUnlocked) expansion.BuyNextPlot();
        fertilizer.AddFertilizer(50f);
        var plots = Read<FarmPlot[]>(save, "plots");
        for (int index = 0; index < plots.Length; index++)
        {
            var plot = plots[index];
            plot.ClearCrop();
            Require(plot.TryPlant(crops[index], inventory, out _), "Prepare saved crop " + index);
        }
        Require(plots[0].TryWater(UnityEngine.Object.FindFirstObjectByType<FarmWaterTank>(), out _), "Water saved planted crop");
        Require(plots[0].TryFertilize(out _), "Fertilize saved planted crop");
        plots[0].TickGrowth(2f, crops[0].plantedSeconds, plots[0].Growing);
        plots[1].Machine.ChangeState(plots[1].Growing);
        plots[1].TickGrowth(crops[1].initialHydration / crops[1].hydrationLossPerSecond + 3f,
            crops[1].growingSeconds, plots[1].Grown);
        plots[2].Machine.ChangeState(plots[2].Grown);
        plots[3].TickGrowth(crops[3].initialHydration / crops[3].hydrationLossPerSecond + plots[3].DryTimeRemaining,
            crops[3].plantedSeconds, plots[3].Growing);
        Require(plots[3].Machine.CurrentState == plots[3].Dead, "Prepare saved dead state");
        plots[3].StageTimer = 1.25f;
        var board = UnityEngine.Object.FindFirstObjectByType<FarmMissionBoard>();
        var mission = board.GetActiveMission(0);
        inventory.AddProduce(mission.requiredCrop, mission.requiredAmount);
        Require(board.Deliver(0, out _), "Save a completed mission without replaying its rewards");
        board.TickMissionTimers(12f);
        var shop = UnityEngine.Object.FindFirstObjectByType<FarmShop>();
        shop.TickMarket(shop.RefreshTimeRemaining + 12.5f);
        UnityEngine.Object.FindFirstObjectByType<FarmWormComposter>().TickGeneration(2f);
        var zeroCrop = crops[crops.Length - 1];
        int count = inventory.ProduceCount(zeroCrop);
        if (count > 0) inventory.TryRemoveProduce(zeroCrop, count);
        while (inventory.TryUseSeed(zeroCrop)) { }
        wallet.TrySpend(wallet.Coins);
        save.SaveGame();
        Require(PlayerPrefs.GetInt(save.ProfilePrefix + ".Coins", -1) == 0, "Zero coins are saved");
        wallet.AddCoins(999);
        inventory.AddSeeds(zeroCrop, 99);
        save.LoadGame();
        Require(wallet.Coins == 0 && inventory.SeedCount(zeroCrop) == 0, "Saved zero values do not fall back to starting values");
        var controller = UnityEngine.Object.FindFirstObjectByType<FarmUIController>();
        controller.PauseGame();
        var button = FarmMenuSetup.Find(controller.transform, "SaveGameButton").GetComponent<Button>();
        wallet.AddCoins(1);
        button.onClick.Invoke();
        Require(PlayerPrefs.GetInt(save.ProfilePrefix + ".Coins", -1) == 1, "Save button writes the current wallet");
        Require(Read<TMP_Text>(controller, "pauseStatusText").text == "Progress saved.", "Save confirmation is visible");
        FarmSystemChecks.Capture("SaveButton");
        controller.ResumeGame();
        FarmSaveManager.AutomaticSavingEnabled = true;
        wallet.AddCoins(1);
        controller.PauseGame();
        Require(PlayerPrefs.GetInt(save.ProfilePrefix + ".Coins", -1) == 2, "Pausing saves automatically");
        FarmSaveManager.AutomaticSavingEnabled = false;
        controller.ResumeGame();
        Time.timeScale = 0f;
        WriteExpected();
        foreach (var plot in plots) plot.ClearCrop();
        wallet.AddCoins(777);
        tech.Unlock(catalog.First(item => !tech.Has(item)));
        shop.TickMarket(1000f);
        board.TickMissionTimers(1000f);
        save.LoadGame();
        save.SaveGame();
        VerifyExpected();
        Require(plots[0].IsFertilized && plots[1].Hydration == 0f && plots[2].Machine.CurrentState == plots[2].Grown &&
            plots[3].Machine.CurrentState == plots[3].Dead, "All four saved crop states restored");
        Require(board.CompletedMissionCount == 1 && board.IsCoolingDown(0), "Mission completion and cooldown restored");
        FarmSystemChecks.Capture("SavedFarm");
        File.WriteAllText(Report + "SnapshotReady.txt", "PASS: values saved for a second Unity process.\n");
    }

    public static void VerifyReload()
    {
        string prefix = File.ReadAllText(Report + "TestPrefix.txt").Trim();
        Require(prefix.StartsWith("GoldenRootTest_"), "Valid test prefix");
        SessionState.SetString("FarmReloadSavePrefix", prefix);
        SessionState.SetBool(ReloadKey, true);
        FarmSaveManager.KeyPrefix = prefix;
        FarmSaveManager.AutomaticSavingEnabled = false;
        EditorSceneManager.OpenScene("Assets/_Project/Scenes/MainLevel.unity");
        EditorApplication.isPlaying = true;
    }

    [InitializeOnLoadMethod]
    private static void RestoreReload()
    {
        if (!SessionState.GetBool(ReloadKey, false)) return;
        FarmSaveManager.KeyPrefix = SessionState.GetString("FarmReloadSavePrefix", "GoldenRootTest");
        FarmSaveManager.AutomaticSavingEnabled = false;
        Time.timeScale = 0f;
        started = EditorApplication.timeSinceStartup;
        EditorApplication.update += CheckReload;
    }

    private static void CheckReload()
    {
        if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup - started < 1d) return;
        int exitCode = 0;
        try
        {
            var save = FarmSaveManager.Instance;
            Require(save != null && save.IsReady && save.HasSavedProgress, "Second process loaded the saved profile");
            VerifyExpected();
            Require(Time.timeScale == 0f && Time.timeSinceLevelLoad < 0.1f, "Reload verification freezes the startup clock");
            Require(UnityEngine.Object.FindFirstObjectByType<FarmWallet>().Coins == 2, "Saved wallet restored after closing Unity");
            var plots = Read<FarmPlot[]>(save, "plots");
            Require(plots[0].IsFertilized && plots[1].Hydration == 0f && plots[3].Machine.CurrentState == plots[3].Dead,
                "Plant state, fertilizer and dryness survive closing Unity");
            Require(UnityEngine.Object.FindFirstObjectByType<FarmMissionBoard>().CompletedMissionCount == 1, "Mission completion survives closing Unity");
            Require(save.CanSaveProgress, "Farm profile remains independent from the unsaved tutorial");
            Require(Mathf.Abs(AudioManager.Instance.MusicVolume - 0.35f) < 0.001f, "Volume survives closing Unity");
            // Recarrega com o relógio já parado para comparar os temporizadores exatos.
            save.LoadGame();
            save.SaveGame();
            VerifyExpected();
            SessionState.SetString("FarmSystemAuditReport", Report);
            FarmSystemChecks.Capture("RestoredFarm");
            File.WriteAllText(Report + "ReloadChecks.txt", "PASS: PlayerPrefs persisted across two separate Unity processes.\nPASS: reloaded runtime systems match every saved value.\nFailures: 0\n");
        }
        catch (Exception exception)
        {
            exitCode = 1;
            File.WriteAllText(Report + "ReloadChecks.txt", "FAIL: " + exception + "\nFailures: 1\n");
        }
        finally
        {
            if (exitCode == 0) CleanupTestKeys();
            SessionState.SetBool(ReloadKey, false);
            EditorApplication.update -= CheckReload;
            SessionState.SetInt("FarmSystemAuditExitCode", exitCode);
            EditorApplication.isPlaying = false;
            EditorApplication.update += ExitReload;
        }
    }

    private static void ExitReload()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        EditorApplication.update -= ExitReload;
        int code = SessionState.GetInt("FarmSystemAuditExitCode", 1);
        SessionState.SetInt("FarmSystemAuditExitCode", -1);
        EditorApplication.Exit(code);
    }

    private static void WriteExpected()
    {
        var lines = new List<string>();
        foreach (var entry in PreferenceKeys())
        {
            if (!PlayerPrefs.HasKey(entry.Key)) continue;
            string value = entry.Value == "I" ? PlayerPrefs.GetInt(entry.Key).ToString(CultureInfo.InvariantCulture) :
                entry.Value == "F" ? PlayerPrefs.GetFloat(entry.Key).ToString("R", CultureInfo.InvariantCulture) : PlayerPrefs.GetString(entry.Key);
            lines.Add(entry.Value + "\t" + entry.Key + "\t" + value);
        }
        File.WriteAllLines(Report + "ExpectedValues.txt", lines);
    }

    private static void VerifyExpected()
    {
        foreach (string line in File.ReadAllLines(Report + "ExpectedValues.txt"))
        {
            string[] entry = line.Split('\t');
            Require(PlayerPrefs.HasKey(entry[1]), "Missing saved key: " + entry[1]);
            if (entry[0] == "I") Require(PlayerPrefs.GetInt(entry[1]) == int.Parse(entry[2], CultureInfo.InvariantCulture), "Integer differs: " + entry[1]);
            else if (entry[0] == "F") Require(Mathf.Abs(PlayerPrefs.GetFloat(entry[1]) - float.Parse(entry[2], CultureInfo.InvariantCulture)) < 0.002f,
                "Float differs: " + entry[1] + " expected=" + entry[2] + " actual=" + PlayerPrefs.GetFloat(entry[1]));
            else Require(PlayerPrefs.GetString(entry[1]) == entry[2], "Text differs: " + entry[1]);
        }
    }

    public static void CleanupTestKeys()
    {
        Require(FarmSaveManager.KeyPrefix.StartsWith("GoldenRootTest_"), "Never erase a real profile during tests");
        foreach (var key in PreferenceKeys().Keys) PlayerPrefs.DeleteKey(key);
        PlayerPrefs.Save();
    }

    public static void CleanupPreviousTestProfile()
    {
        if (!File.Exists(Report + "TestPrefix.txt") || !File.Exists(Report + "PlotIds.txt")) return;
        string prefix = File.ReadAllText(Report + "TestPrefix.txt").Trim();
        if (!prefix.StartsWith("GoldenRootTest_")) return;
        string previous = FarmSaveManager.KeyPrefix;
        FarmSaveManager.KeyPrefix = prefix;
        CleanupTestKeys();
        FarmSaveManager.KeyPrefix = previous;
    }

    private static Dictionary<string, string> PreferenceKeys()
    {
        var keys = new Dictionary<string, string>();
        string root = FarmSaveManager.KeyPrefix;
        keys[root + ".TutorialCompleted"] = "I";
        keys[root + ".MusicVolume"] = keys[root + ".EffectsVolume"] = "F";
        foreach (string profile in new[] { ".Farm", ".Tutorial" })
        {
            string prefix = root + profile;
            foreach (string suffix in new[] { ".HasSave", ".Coins", ".PurchasedPlots", ".DeliveredProduce", ".TutorialStep", ".TutorialWatered", ".TutorialFertilized", ".InitialCoins", ".Market.Rotation", ".Market.SeedCount", ".Market.TechnologyCount" }) keys[prefix + suffix] = "I";
            foreach (string suffix in new[] { ".Water", ".WaterTimer", ".Fertilizer", ".ComposterTimer", ".PlayerX", ".PlayerY", ".PlayerZ", ".PlayerRotation", ".InitialWater", ".InitialFertilizer", ".Market.Timer" }) keys[prefix + suffix] = "F";
            keys[prefix + ".Market.Demand"] = "S";
            foreach (var crop in Assets<CropData>())
                foreach (string suffix in new[] { ".Seeds.", ".Produce.", ".InitialSeeds." }) keys[prefix + suffix + crop.SaveId] = "I";
            foreach (var tech in Assets<TechnologyData>()) keys[prefix + ".Technology." + tech.SaveId] = "I";
            foreach (var mission in Assets<MissionData>()) keys[prefix + ".Mission." + mission.SaveId] = "I";
            foreach (var community in Assets<FarmCommunityData>())
            {
                string key = prefix + ".Community." + community.SaveId;
                keys[key + ".Active"] = "S";
                keys[key + ".Time"] = "F";
                foreach (string suffix in new[] { ".Cooldown", ".Next", ".Completed" }) keys[key + suffix] = "I";
            }
            foreach (string id in File.ReadAllLines(Report + "PlotIds.txt"))
            {
                string key = prefix + ".Plot." + id.Trim();
                keys[key + ".Crop"] = keys[key + ".State"] = "S";
                keys[key + ".Fertilized"] = "I";
                foreach (string suffix in new[] { ".StageTimer", ".Hydration", ".DryTimer" }) keys[key + suffix] = "F";
            }
            for (int index = 0; index < 32; index++)
                keys[prefix + ".Market.Seed." + index] = keys[prefix + ".Market.Technology." + index] = "S";
        }
        return keys;
    }

    private static T[] Assets<T>() where T : UnityEngine.Object => AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { "Assets/_Project" })
        .Select(guid => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid))).ToArray();
    private static T Read<T>(object target, string name) => (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
}
