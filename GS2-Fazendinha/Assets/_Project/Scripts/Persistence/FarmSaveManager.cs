using UnityEngine;

public class FarmSaveManager : MonoBehaviour
{
    public static FarmSaveManager Instance { get; private set; }
    public static string KeyPrefix { get; set; } = "GoldenRoot";
    public static bool AutomaticSavingEnabled { get; set; } = true;

    [Header("Save Profile")]
    [SerializeField] private bool tutorialProfile;
    [SerializeField] private string saveKeyPrefix = "GoldenRoot";

    [Header("Farm References")]
    [SerializeField] private FarmWallet wallet;
    [SerializeField] private FarmInventory inventory;
    [SerializeField] private FarmTechnologyManager technologies;
    [SerializeField] private FarmWaterTank waterTank;
    [SerializeField] private FarmFertilizerStorage fertilizerStorage;
    [SerializeField] private FarmWormComposter wormComposter;
    [SerializeField] private FarmMissionBoard missions;
    [SerializeField] private FarmShop shop;
    [SerializeField] private FarmPlotExpansion expansion;
    [SerializeField] private PlayerFarmController player;
    [SerializeField] private FarmTutorialController tutorial;
    [SerializeField] private FarmHUD hud;

    [Header("Save Catalogs")]
    [SerializeField] private CropData[] crops = new CropData[0];
    [SerializeField] private TechnologyData[] technologyCatalog = new TechnologyData[0];
    [SerializeField] private FarmPlot[] plots = new FarmPlot[0];

    [Header("Reset Catalogs")]
    [SerializeField] private CropData[] resetCrops = new CropData[0];
    [SerializeField] private MissionData[] resetMissions = new MissionData[0];
    [SerializeField] private FarmCommunityData[] resetCommunities = new FarmCommunityData[0];
    [SerializeField] private string[] resetPlotIds = new string[0];

    public string ProfilePrefix => KeyPrefix + (tutorialProfile ? ".Tutorial" : ".Farm");
    public bool IsReady { get; private set; }
    public bool CanSaveProgress => !tutorialProfile && wallet != null;
    public bool HasSavedProgress => CanSaveProgress && PlayerPrefs.HasKey(ProfilePrefix + ".HasSave") && PlayerPrefs.GetInt(ProfilePrefix + ".HasSave", 0) == 1;

    private void Awake()
    {
        Instance = this;
        ConfigureSaveProfile();
    }

    // A cópia pode usar suas próprias chaves, mantendo os dados antigos intactos.
    private void ConfigureSaveProfile()
    {
        if (KeyPrefix.StartsWith("GoldenRootTest_")) return;
        KeyPrefix = string.IsNullOrWhiteSpace(saveKeyPrefix) ? "GoldenRoot" : saveKeyPrefix;
        bool copied = false;
        foreach (string setting in new[] { ".MusicVolume", ".EffectsVolume" })
        {
            if (KeyPrefix == "GoldenRoot" || PlayerPrefs.HasKey(KeyPrefix + setting) || !PlayerPrefs.HasKey("GoldenRoot" + setting)) continue;
            PlayerPrefs.SetFloat(KeyPrefix + setting, PlayerPrefs.GetFloat("GoldenRoot" + setting));
            copied = true;
        }
        if (copied) PlayerPrefs.Save();
        if (AudioManager.Instance != null) LoadAudioSettings(AudioManager.Instance);
    }

    public void ResetProgress()
    {
        ConfigureSaveProfile();
        IsReady = false;
        DeleteProfileProgress(KeyPrefix + ".Farm");
        DeleteProfileProgress(KeyPrefix + ".Tutorial");
        PlayerPrefs.DeleteKey(KeyPrefix + ".TutorialCompleted");
        FarmUIController.TutorialCompleted = false;
        PlayerPrefs.Save();
    }

    private void DeleteProfileProgress(string prefix)
    {
        foreach (string suffix in new[] { ".HasSave", ".Coins", ".PurchasedPlots", ".DeliveredProduce", ".Water", ".WaterTimer",
            ".Fertilizer", ".ComposterTimer", ".PlayerX", ".PlayerY", ".PlayerZ", ".PlayerRotation", ".TutorialStep",
            ".TutorialWatered", ".TutorialFertilized", ".InitialCoins", ".InitialWater", ".InitialFertilizer",
            ".Market.Rotation", ".Market.Timer", ".Market.Demand", ".Market.SeedCount", ".Market.TechnologyCount" })
            PlayerPrefs.DeleteKey(prefix + suffix);
        foreach (var crop in resetCrops)
            if (crop != null)
                foreach (string suffix in new[] { ".Seeds.", ".Produce.", ".InitialSeeds." }) PlayerPrefs.DeleteKey(prefix + suffix + crop.SaveId);
        foreach (var technology in technologyCatalog)
            if (technology != null) PlayerPrefs.DeleteKey(prefix + ".Technology." + technology.SaveId);
        foreach (var mission in resetMissions)
            if (mission != null) PlayerPrefs.DeleteKey(prefix + ".Mission." + mission.SaveId);
        foreach (var community in resetCommunities)
            if (community != null)
                foreach (string suffix in new[] { ".Active", ".Time", ".Cooldown", ".Next", ".Completed" })
                    PlayerPrefs.DeleteKey(prefix + ".Community." + community.SaveId + suffix);
        foreach (string id in resetPlotIds)
            foreach (string suffix in new[] { ".Crop", ".State", ".Fertilized", ".StageTimer", ".Hydration", ".DryTimer" })
                PlayerPrefs.DeleteKey(prefix + ".Plot." + id + suffix);
        for (int index = 0; index < 64; index++)
        {
            PlayerPrefs.DeleteKey(prefix + ".Market.Seed." + index);
            PlayerPrefs.DeleteKey(prefix + ".Market.Technology." + index);
        }
    }

    private void Start()
    {
        LoadGame();
    }

    public void LoadGame()
    {
        if (wallet != null && HasSavedProgress)
        {
            technologies.LoadProgress(ProfilePrefix, technologyCatalog);
            if (expansion != null) expansion.LoadProgress(ProfilePrefix);
            wallet.LoadProgress(ProfilePrefix);
            inventory.LoadProgress(ProfilePrefix, crops);
            waterTank.LoadProgress(ProfilePrefix);
            fertilizerStorage.LoadProgress(ProfilePrefix);
            if (wormComposter != null) wormComposter.LoadProgress(ProfilePrefix);
            missions.LoadProgress(ProfilePrefix);
            shop.LoadProgress(ProfilePrefix);
            foreach (var plot in plots) if (plot != null) plot.LoadProgress(ProfilePrefix, crops);
            if (player != null) player.LoadProgress(ProfilePrefix);
            if (tutorial != null) tutorial.LoadProgress(ProfilePrefix);
        }
        IsReady = true;
        if (hud != null) hud.RefreshHUD();
    }

    public void SaveGame()
    {
        if (!IsReady) return;
        SaveAudioSettings();
        if (CanSaveProgress)
        {
            wallet.SaveProgress(ProfilePrefix);
            inventory.SaveProgress(ProfilePrefix, crops);
            technologies.SaveProgress(ProfilePrefix, technologyCatalog);
            waterTank.SaveProgress(ProfilePrefix);
            fertilizerStorage.SaveProgress(ProfilePrefix);
            if (wormComposter != null) wormComposter.SaveProgress(ProfilePrefix);
            if (expansion != null) expansion.SaveProgress(ProfilePrefix);
            missions.SaveProgress(ProfilePrefix);
            shop.SaveProgress(ProfilePrefix);
            foreach (var plot in plots) if (plot != null) plot.SaveProgress(ProfilePrefix);
            if (player != null) player.SaveProgress(ProfilePrefix);
            if (tutorial != null) tutorial.SaveProgress(ProfilePrefix);
            PlayerPrefs.SetInt(ProfilePrefix + ".HasSave", 1);
        }
        PlayerPrefs.Save();
    }

    public void SaveAutomatically()
    {
        if (AutomaticSavingEnabled && IsReady) SaveGame();
    }

    public static void LoadAudioSettings(AudioManager audio)
    {
        audio.SetMusicVolume(PlayerPrefs.GetFloat(KeyPrefix + ".MusicVolume", audio.MusicVolume));
        audio.SetEffectsVolume(PlayerPrefs.GetFloat(KeyPrefix + ".EffectsVolume", audio.EffectsVolume));
    }

    public static void SaveAudioSettings()
    {
        var audio = AudioManager.Instance;
        if (audio == null) return;
        PlayerPrefs.SetFloat(KeyPrefix + ".MusicVolume", audio.MusicVolume);
        PlayerPrefs.SetFloat(KeyPrefix + ".EffectsVolume", audio.EffectsVolume);
    }

    public void CompleteTutorial()
    {
        if (!tutorialProfile || tutorial == null || tutorial.CurrentStep != FarmTutorialStep.Completed) return;
        FarmUIController.TutorialCompleted = true;
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused) SaveAutomatically();
    }

    private void OnApplicationFocus(bool focused)
    {
        if (!focused) SaveAutomatically();
    }

    private void OnApplicationQuit()
    {
        SaveAutomatically();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
