using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum FarmTutorialStep
{
    SelectPlot, PlantCrop, WaterCrop, HarvestCrop, PlantAgain,
    FertilizeCrop, CollectProduce, OpenMap, DeliverMission, Completed
}

public class FarmTutorialController : MonoBehaviour
{
    [Header("Tutorial References")]
    [SerializeField] private PlayerFarmController player;
    [SerializeField] private FarmPlot plot;
    [SerializeField] private FarmInventory inventory;
    [SerializeField] private FarmMissionBoard missions;
    [SerializeField] private CropData tutorialCrop;
    [SerializeField] private GameObject mapPanel;

    [Header("Tutorial UI")]
    [SerializeField] private TMP_Text stepText;
    [SerializeField] private TMP_Text instructionText;
    [SerializeField] private GameObject continueButton;
    [SerializeField] private string gameplayScene = "MainLevel";

    private float previousHydration;
    private bool wateredOnce;
    private bool fertilizedOnce;
    private bool progressLoaded;
    public FarmTutorialStep CurrentStep { get; private set; }

    private void Start()
    {
        if (!progressLoaded) CurrentStep = FarmTutorialStep.SelectPlot;
        RefreshInstructions();
    }

    public void SaveProgress(string prefix)
    {
        PlayerPrefs.SetInt(prefix + ".TutorialStep", (int)CurrentStep);
        PlayerPrefs.SetInt(prefix + ".TutorialWatered", wateredOnce ? 1 : 0);
        PlayerPrefs.SetInt(prefix + ".TutorialFertilized", fertilizedOnce ? 1 : 0);
    }

    public void LoadProgress(string prefix)
    {
        CurrentStep = (FarmTutorialStep)Mathf.Clamp(PlayerPrefs.GetInt(prefix + ".TutorialStep", 0), 0, (int)FarmTutorialStep.Completed);
        wateredOnce = PlayerPrefs.GetInt(prefix + ".TutorialWatered", 0) == 1;
        fertilizedOnce = PlayerPrefs.GetInt(prefix + ".TutorialFertilized", 0) == 1;
        previousHydration = plot != null ? plot.Hydration : 0f;
        progressLoaded = true;
        RefreshInstructions();
    }

    private void Update()
    {
        UpdateTutorialProgress();
    }

    private void UpdateTutorialProgress()
    {
        if (plot == null || plot.Machine == null || inventory == null || missions == null) return;
        bool planted = plot.CurrentCrop != null;
        bool ready = plot.Machine.CurrentState == plot.Grown;
        bool dead = plot.Machine.CurrentState == plot.Dead;
        int produce = inventory.ProduceCount(tutorialCrop);
        if (planted && plot.Hydration > previousHydration + 1f) wateredOnce = true;
        if (planted && plot.IsFertilized) fertilizedOnce = true;

        if (missions.CompletedMissionCount > 0)
        {
            CurrentStep = FarmTutorialStep.Completed;
        }
        else
        {
            if ((CurrentStep == FarmTutorialStep.OpenMap || CurrentStep == FarmTutorialStep.DeliverMission) && produce < 12)
                CurrentStep = FarmTutorialStep.CollectProduce;
            switch (CurrentStep)
            {
                case FarmTutorialStep.SelectPlot:
                    if (player.LastSelectedPlot == plot) CurrentStep = FarmTutorialStep.PlantCrop;
                    break;
                case FarmTutorialStep.PlantCrop:
                    if (planted && !dead) { wateredOnce = false; CurrentStep = FarmTutorialStep.WaterCrop; }
                    break;
                case FarmTutorialStep.WaterCrop:
                    if (dead || !planted) CurrentStep = FarmTutorialStep.PlantCrop;
                    else if (wateredOnce) CurrentStep = FarmTutorialStep.HarvestCrop;
                    break;
                case FarmTutorialStep.HarvestCrop:
                    if (produce >= 4) CurrentStep = FarmTutorialStep.PlantAgain;
                    else if (dead) CurrentStep = FarmTutorialStep.PlantCrop;
                    break;
                case FarmTutorialStep.PlantAgain:
                    if (planted && !ready && !dead) CurrentStep = FarmTutorialStep.FertilizeCrop;
                    break;
                case FarmTutorialStep.FertilizeCrop:
                    if (fertilizedOnce) CurrentStep = FarmTutorialStep.CollectProduce;
                    else if (!planted || dead) CurrentStep = FarmTutorialStep.PlantAgain;
                    break;
                case FarmTutorialStep.CollectProduce:
                    if (produce >= 12) CurrentStep = FarmTutorialStep.OpenMap;
                    break;
                case FarmTutorialStep.OpenMap:
                    if (mapPanel != null && mapPanel.activeInHierarchy) CurrentStep = FarmTutorialStep.DeliverMission;
                    break;
            }
        }
        previousHydration = planted ? plot.Hydration : 0f;
        RefreshInstructions();
    }

    private void RefreshInstructions()
    {
        if (continueButton != null) continueButton.SetActive(CurrentStep == FarmTutorialStep.Completed);
        if (stepText != null) stepText.text = CurrentStep == FarmTutorialStep.Completed ? "TUTORIAL COMPLETE" :
            "TUTORIAL " + ((int)CurrentStep + 1) + "/9";
        if (instructionText == null) return;
        switch (CurrentStep)
        {
            case FarmTutorialStep.SelectPlot: instructionText.text = "Click the planting plot to open its crop menu."; break;
            case FarmTutorialStep.PlantCrop: instructionText.text = "Click the plot, choose Carrot and press Plant. Your farmer will walk to the plot."; break;
            case FarmTutorialStep.WaterCrop: instructionText.text = "Click the planted crop and press Water. Keep its hydration ring filled so it can grow."; break;
            case FarmTutorialStep.HarvestCrop: instructionText.text = "Wait for the ! marker, then click the crop and press Harvest. Each harvest gives 4 carrots and fertilizer."; break;
            case FarmTutorialStep.PlantAgain: instructionText.text = "Plant another carrot crop. Your first harvest gave you 20 fertilizer."; break;
            case FarmTutorialStep.FertilizeCrop: instructionText.text = "Use Fertilize on a growing crop to speed it up. You can fertilize once per planting. Keep watering too."; break;
            case FarmTutorialStep.CollectProduce: instructionText.text = "Grow and harvest until you have 12 carrots. Keep them for the community mission. Carrots: " + inventory.ProduceCount(tutorialCrop) + "/12"; break;
            case FarmTutorialStep.OpenMap: instructionText.text = "You have 12 carrots! Open the map using the map icon at the bottom."; break;
            case FarmTutorialStep.DeliverMission: instructionText.text = "Choose Le Fezende Ghoon'd and press Deliver. The mission needs 12 carrots."; break;
            case FarmTutorialStep.Completed: instructionText.text = "Delivery complete! You learned to grow, water, fertilize and help a community. Start farming to explore all three regions."; break;
        }
        RefreshRecoveryHint();
    }

    private void RefreshRecoveryHint()
    {
        if (CurrentStep == FarmTutorialStep.Completed || plot == null || plot.Machine == null) return;
        if (plot.Machine.CurrentState == plot.Dead)
            instructionText.text = "This crop dried out. Wait for the plot to clear, plant again and keep it watered.";
        else if (plot.CurrentCrop == null && inventory.SeedCount(tutorialCrop) == 0)
            instructionText.text = "Out of seeds? Open the map, visit the market and buy more carrot seeds to continue.";
        else if (CurrentStep == FarmTutorialStep.FertilizeCrop && plot.Machine.CurrentState == plot.Grown)
            instructionText.text = "This crop is already ready. Harvest it, plant again and use Fertilize while the new crop is growing.";
    }

    public void ContinueToGame()
    {
        if (CurrentStep != FarmTutorialStep.Completed) return;
        if (AudioManager.Instance != null) AudioManager.Instance.PlayTutorialComplete();
        if (FarmSaveManager.Instance != null) FarmSaveManager.Instance.CompleteTutorial();
        FarmUIController.TutorialCompleted = true;
        Time.timeScale = 1f;
        SceneManager.LoadScene(gameplayScene);
    }
}
