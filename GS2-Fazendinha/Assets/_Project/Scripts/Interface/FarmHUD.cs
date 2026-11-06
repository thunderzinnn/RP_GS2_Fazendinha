
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Serialization;

public class FarmHUD : MonoBehaviour
{
    [Header("System References")]
    [SerializeField] private PlayerFarmController player;
    [SerializeField] private FarmInventory inventory;
    [SerializeField] private FarmWallet wallet;
    [SerializeField] private FarmWaterTank tank;
    [SerializeField] private FarmTechnologyManager technologies;
    [SerializeField] private FarmMissionBoard missions;
    [SerializeField] private FarmShop shop;

    [Header("Crop Settings")]
    [SerializeField] private CropData crop;
    [SerializeField, Min(1)] private int seedsPerPurchase = 5;
    [SerializeField, Min(1)] private int producePerSale = 1;

    [Header("Shop Technologies")]
    [SerializeField] private TechnologyData shopTechnologyA;
    [SerializeField] private TechnologyData shopTechnologyB;

    [Header("Text References")]
    [SerializeField] private TMP_Text walletText;
    [SerializeField] private TMP_Text tankText;
    [SerializeField] private TMP_Text resourcesText;
    [SerializeField] private TMP_Text selectedPlotText;
    [SerializeField] private TMP_Text technologiesText;
    [SerializeField] private TMP_Text missionOneText;
    [SerializeField] private TMP_Text missionTwoText;

    [FormerlySerializedAs("collectWaterText")]
    [SerializeField] private TMP_Text waterRegenerationText;

    [SerializeField] private TMP_Text feedbackText;

    [Header("Slider References")]
    [SerializeField] private Slider tankSlider;
    [SerializeField] private Slider plotHydrationSlider;

    [Header("UI Settings")]
    [SerializeField, Min(0.05f)]
    private float refreshInterval = 0.15f;

    private float nextRefreshTime;


    // Initialization

    private void Start()
    {
        RefreshHUD();
        nextRefreshTime = Time.unscaledTime + refreshInterval;
    }

    private void Update()
    {
        RefreshIfNeeded();
    }


    // UI Refresh

    // Refreshes the HUD at a fixed interval.
    private void RefreshIfNeeded()
    {
        if (Time.unscaledTime < nextRefreshTime)
            return;

        nextRefreshTime = Time.unscaledTime + refreshInterval;
        RefreshHUD();
    }

    // Updates all HUD sections.
    public void RefreshHUD()
    {
        UpdateWalletUI();
        UpdateWaterUI();
        UpdateCropUI();
        UpdateSelectedPlotUI();
        UpdateTechnologiesUI();
        UpdateMissionsUI();
        UpdateWaterRegenerationUI();
    }


    // Wallet UI

    // Displays the player's current coins.
    private void UpdateWalletUI()
    {
        if (wallet == null || walletText == null)
            return;

        walletText.text = $"Coins: {wallet.Coins}";
    }


    // Water UI

    // Updates the water counter and tank slider.
    private void UpdateWaterUI()
    {
        if (tank == null)
            return;

        if (tankText != null)
        {
            tankText.text =
                $"Water: {tank.CurrentWater:0.0}/{tank.Capacity:0.0}";
        }

        if (tankSlider != null)
        {
            tankSlider.maxValue = tank.Capacity;
            tankSlider.value = tank.CurrentWater;
        }
    }

    // Displays the automatic regeneration timer.
    private void UpdateWaterRegenerationUI()
    {
        if (tank == null || waterRegenerationText == null)
            return;

        if (tank.CurrentWater >= tank.Capacity)
        {
            waterRegenerationText.text = "Water Tank Full";
            return;
        }

        waterRegenerationText.text =
            $"Next Regeneration: {tank.RegenerationTimeRemaining:0.0}s";
    }


    // Crop UI

    // Displays information about the selected crop.
    private void UpdateCropUI()
    {
        if (resourcesText == null || inventory == null)
            return;

        if (crop == null)
        {
            resourcesText.text = "Crop: None Selected";
            return;
        }

        resourcesText.text =
            $"{crop.CropName}: {inventory.ProduceCount(crop)} " +
            $"| Seeds: {inventory.SeedCount(crop)}";
    }

    // Changes the crop displayed in the HUD.
    public void SetCrop(CropData newCrop)
    {
        if (newCrop == null)
            return;

        crop = newCrop;
        RefreshHUD();
    }


    // Selected Plot UI

    // Updates the selected plot information and hydration bar.
    private void UpdateSelectedPlotUI()
    {
        if (player == null)
            return;

        FarmPlot plot = player.LastSelectedPlot;

        if (selectedPlotText != null)
        {
            if (plot == null)
            {
                selectedPlotText.text = "Plot: None";
            }
            else
            {
                string cropName = plot.CurrentCrop != null
                    ? plot.CurrentCrop.CropName
                    : "Empty";

                selectedPlotText.text =
                    $"Plot: {cropName} | {plot.StateName} " +
                    $"| Hydration: {plot.Hydration:0}%";
            }
        }

        if (plotHydrationSlider != null)
        {
            plotHydrationSlider.maxValue = 100f;
            plotHydrationSlider.value =
                plot != null ? plot.Hydration : 0f;
        }
    }


    // Technologies UI

    // Displays unlocked technologies.
    private void UpdateTechnologiesUI()
    {
        if (technologies == null || technologiesText == null)
            return;

        technologiesText.text =
            $"Technologies: {technologies.GetUnlockedNames()}";
    }


    // Missions UI

    // Updates the available mission descriptions.
    private void UpdateMissionsUI()
    {
        if (missions == null)
            return;

        if (missionOneText != null)
            missionOneText.text = missions.Describe(0);

        if (missionTwoText != null)
            missionTwoText.text = missions.Describe(1);
    }


    // Shop Actions

    // Buys seeds for the selected crop.
    public void BuySeeds()
    {
        if (shop == null || crop == null)
        {
            ShowMessage("Crop or shop not configured.");
            return;
        }

        shop.BuySeeds(crop, seedsPerPurchase, out string message);
        ShowMessage(message);
    }

    // Sells produce from the selected crop.
    public void SellProduce()
    {
        if (shop == null || crop == null)
        {
            ShowMessage("Crop or shop not configured.");
            return;
        }

        shop.SellProduce(crop, producePerSale, out string message);
        ShowMessage(message);
    }


    // Technology Purchase

    public void BuyTechnologyA()
    {
        BuyTechnology(shopTechnologyA);
    }

    public void BuyTechnologyB()
    {
        BuyTechnology(shopTechnologyB);
    }

    // Purchases a technology from the shop.
    private void BuyTechnology(TechnologyData technology)
    {
        if (shop == null || technology == null)
            return;

        shop.BuyTechnology(technology, out string message);
        ShowMessage(message);
    }


    // Mission Delivery

    public void DeliverMissionOne()
    {
        DeliverMission(0);
    }

    public void DeliverMissionTwo()
    {
        DeliverMission(1);
    }

    // Submits the required products for a mission.
    private void DeliverMission(int index)
    {
        if (missions == null)
            return;

        missions.Deliver(index, out string message);
        ShowMessage(message);
    }


    // Feedback

    // Displays an action result and refreshes the HUD.
    public void ShowMessage(string message)
    {
        if (feedbackText != null)
            feedbackText.text = message;

        Debug.Log(message);
        RefreshHUD();
    }
}
