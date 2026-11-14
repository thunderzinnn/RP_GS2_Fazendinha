using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class FarmHUD : MonoBehaviour
{
    [Header("System References")]
    [SerializeField] private PlayerFarmController player;
    [SerializeField] private FarmInventory inventory;
    [SerializeField] private FarmWallet coinWallet;
    [SerializeField] private FarmWaterTank waterTank;
    [SerializeField] private FarmTechnologyManager technologies;
    [SerializeField] private FarmMissionBoard missions;
    [SerializeField] private FarmShop shop;
    [SerializeField] private FarmFertilizerStorage fertilizerStorage;

    [Header("Fertilizer HUD")]
    [SerializeField] private Slider fertilizerAmountSlider;
    [SerializeField] private TMP_Text fertilizerAmountText;
    [SerializeField] private FarmWormComposter wormComposter;
    [SerializeField] private TMP_Text fertilizerGenerationTimerText;

    [Header("Crop Settings")]
    [SerializeField] private CropData crop;

    [SerializeField] private CropData[] availableCrops;
    [SerializeField] private TMP_Dropdown shopCropDropdown;
    [SerializeField] private TMP_Text shopCropDetailsText;
    [SerializeField, Min(1)] private int seedsPerPurchase = 1;
    [SerializeField, Min(1)] private int producePerSale = 1;

    [Header("Shop Technologies")]
    [SerializeField] private TechnologyData shopTechnologyA;
    [SerializeField] private TechnologyData shopTechnologyB;

    [Header("Permanent Upgrade UI")]
    [SerializeField] private TMP_Text growingTimeUpgradeDescriptionText;
    [SerializeField] private Button growingTimeUpgradeButton;
    [SerializeField] private TMP_Text growingTimeUpgradeButtonText;
    [SerializeField] private TMP_Text wormComposterUpgradeDescriptionText;
    [SerializeField] private Button wormComposterUpgradeButton;
    [SerializeField] private TMP_Text wormComposterUpgradeButtonText;

    [Header("Crop and Technology Icons")]
    [SerializeField] private Image shopCropIcon;
    [SerializeField] private Image marketTechnologyIcon;
    [SerializeField] private Image growingTimeUpgradeIcon;
    [SerializeField] private Image wormComposterUpgradeIcon;

    [Header("Seasonal Market UI")]
    [SerializeField] private TMP_Dropdown marketTechnologyDropdown;
    [SerializeField] private TMP_Text marketTechnologyDetailsText;
    [SerializeField] private TMP_Text marketRefreshText;
    [SerializeField] private Button buySeedsButton;
    [SerializeField] private Button buyFiveSeedsButton;
    [SerializeField] private Button buyMarketTechnologyButton;

    [Header("Mission Progress UI")]
    [SerializeField] private TMP_Text missionProgressText;
    [SerializeField] private Button firstMissionDeliverButton;
    [SerializeField] private Button secondMissionDeliverButton;
    [SerializeField] private TMP_Text thirdMissionDescriptionText;
    [SerializeField] private Button thirdMissionDeliverButton;

    [Header("HUD Text References")]

    [SerializeField] private TMP_Text coinCounterText;

    [SerializeField] private TMP_Text waterAmountText;

    [SerializeField] private TMP_Text cropInventoryText;

    [SerializeField] private TMP_Text selectedPlotText;

    [SerializeField] private TMP_Text unlockedTechnologiesText;

    [SerializeField] private TMP_Text firstMissionDescriptionText;
    [SerializeField] private TMP_Text secondMissionDescriptionText;

    [SerializeField] private TMP_Text waterRegenerationTimerText;

    [SerializeField] private TMP_Text actionMessageText;

    [Header("HUD Progress Bars")]
    [SerializeField] private Slider waterAmountSlider;
    [SerializeField] private Slider selectedPlotHydrationSlider;

    [Header("UI Settings")]
    [SerializeField, Min(0.05f)]
    private float refreshInterval = 0.15f;

    private float nextRefreshTime;
    private float messageVisibleUntil;

    private int lastMarketRevision = -1;
    private TechnologyData selectedMarketTechnology;

    private void Start()
    {
        InitializeMarketUI();

        RefreshHUD();
        nextRefreshTime = Time.unscaledTime + refreshInterval;
    }

    private void OnDestroy()
    {
        if (marketTechnologyDropdown != null)
        {
            marketTechnologyDropdown.onValueChanged.RemoveListener(
                SelectMarketTechnology);
        }
    }

    private void Update()
    {
        RefreshIfNeeded();
        UpdateMessageVisibility();
    }

    private void InitializeMarketUI()
    {
        if (shop != null)
            shop.ConfigureCropCatalogIfEmpty(availableCrops);

        PopulateShopCropOptions();

        if (marketTechnologyDropdown != null)
        {
            marketTechnologyDropdown.onValueChanged.AddListener(
                SelectMarketTechnology);
        }
    }

    private void PopulateShopCropOptions()
    {
        if (shopCropDropdown == null || availableCrops == null)
            return;

        var options = new List<TMP_Dropdown.OptionData>();
        int selectedIndex = 0;

        for (int index = 0; index < availableCrops.Length; index++)
        {
            CropData item = availableCrops[index];
            options.Add(new TMP_Dropdown.OptionData(
                item != null ? item.CropName : "Crop not configured",
                item != null ? item.icon : null, Color.white));

            if (item == crop)
                selectedIndex = index;
        }

        shopCropDropdown.ClearOptions();
        shopCropDropdown.AddOptions(options);
        shopCropDropdown.SetValueWithoutNotify(selectedIndex);
        shopCropDropdown.RefreshShownValue();
    }

    private void UpdateIcon(Image image, Sprite sprite)
    {
        if (image == null)
            return;

        image.sprite = sprite;
        image.enabled = sprite != null;
    }

    private void UpdateMessageVisibility()
    {
        if (actionMessageText == null)
            return;

        actionMessageText.gameObject.SetActive(
            Time.unscaledTime < messageVisibleUntil);
    }

    private void RefreshIfNeeded()
    {
        if (Time.unscaledTime < nextRefreshTime)
            return;

        nextRefreshTime = Time.unscaledTime + refreshInterval;
        RefreshHUD();
    }

    public void RefreshHUD()
    {
        UpdateCoinUI();
        UpdateWaterUI();
        UpdateFertilizerUI();
        UpdateCropUI();
        UpdateSelectedPlotUI();
        UpdateTechnologiesUI();
        UpdateUpgradesUI();
        UpdateMissionsUI();
        UpdateWaterTimer();
        UpdateShopCropUI();
        UpdateSeasonalMarketUI();
    }

    private void UpdateFertilizerUI()
    {
        if (fertilizerStorage == null)
            return;

        if (fertilizerAmountSlider != null)
        {
            fertilizerAmountSlider.maxValue = fertilizerStorage.Capacity;
            fertilizerAmountSlider.value = fertilizerStorage.CurrentFertilizer;
        }

        if (fertilizerAmountText != null)
        {
            fertilizerAmountText.text = fertilizerStorage.CurrentFertilizer.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + " / " +
                fertilizerStorage.Capacity.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
        }

        if (fertilizerGenerationTimerText != null)
        {
            bool generating = wormComposter != null && wormComposter.IsUnlocked;
            fertilizerGenerationTimerText.gameObject.SetActive(true);

            if (generating)
            {
                fertilizerGenerationTimerText.text =
                    fertilizerStorage.CurrentFertilizer >= fertilizerStorage.Capacity
                        ? "Reserve full"
                        : "Next compost in " +
                            wormComposter.GenerationTimeRemaining.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "s";
            }
            else fertilizerGenerationTimerText.text = "Compost from harvests";
        }
    }

    private void UpdateCoinUI()
    {
        if (coinWallet == null || coinCounterText == null)
            return;

        coinCounterText.text = "COINS " + coinWallet.Coins.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);
    }

    private void UpdateWaterUI()
    {
        if (waterTank == null)
            return;

        if (waterAmountText != null)
        {
            waterAmountText.text = waterTank.CurrentWater.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) +
                " / " + waterTank.Capacity.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
        }

        if (waterAmountSlider != null)
        {
            waterAmountSlider.maxValue = waterTank.Capacity;
            waterAmountSlider.value = waterTank.CurrentWater;
        }
    }

    private void UpdateWaterTimer()
    {
        if (waterTank == null || waterRegenerationTimerText == null)
            return;

        if (waterTank.CurrentWater >= waterTank.Capacity)
        {
            waterRegenerationTimerText.text = "Tank full";
            return;
        }

        waterRegenerationTimerText.text =
            "Next refill in " + waterTank.RegenerationTimeRemaining.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "s";
    }

    private void UpdateCropUI()
    {
        if (cropInventoryText == null || inventory == null)
            return;

        if (crop == null)
        {
            cropInventoryText.text = "Crop: None Selected";
            return;
        }

        cropInventoryText.text =
            $"{crop.CropName}\n" +
            $"Produce: {inventory.ProduceCount(crop)} " +
            $"| Seeds: {inventory.SeedCount(crop)}";
    }

    public void SetCrop(CropData newCrop)
    {
        if (newCrop == null)
            return;

        crop = newCrop;

        if (availableCrops != null && shopCropDropdown != null)
        {
            for (int index = 0; index < availableCrops.Length; index++)
            {
                if (availableCrops[index] != crop)
                    continue;

                shopCropDropdown.SetValueWithoutNotify(index);
                break;
            }
        }

        RefreshHUD();
    }

    public void SelectShopCrop(int index)
    {
        if (availableCrops == null ||
            index < 0 ||
            index >= availableCrops.Length)
        {
            return;
        }

        SetCrop(availableCrops[index]);
    }

    private void UpdateShopCropUI()
    {
        UpdateIcon(shopCropIcon, crop != null ? crop.icon : null);

        if (crop == null || shopCropDetailsText == null)
            return;

        int seeds = inventory != null
            ? inventory.SeedCount(crop)
            : 0;

        int produce = inventory != null
            ? inventory.ProduceCount(crop)
            : 0;

        bool inSeason = shop == null || shop.IsSeedAvailable(crop);

        int salePrice = shop != null
            ? shop.GetSellPrice(crop)
            : crop.sellPrice;

        string demand = shop != null && shop.HighDemandCrop == crop
            ? " (HIGH DEMAND)"
            : "";

        string availability = inSeason
            ? "Available this season"
            : "Out of season";

        shopCropDetailsText.text =
            $"{crop.CropName}\n" +
            $"Seeds: {seeds} | Produce: {produce}\n\n" +
            $"Buy {seedsPerPurchase} {(seedsPerPurchase == 1 ? "seed" : "seeds")}: " +
            $"{crop.seedPrice * seedsPerPurchase} coins\n" +
            $"Buy 5 seeds: {crop.seedPrice * 5} coins\n" +
            $"Seeds: {availability}\n" +
            $"Sell {producePerSale}: " +
            $"{salePrice * producePerSale} coins{demand}\n\n" +
            $"Growth: {crop.plantedSeconds + crop.growingSeconds:0}s " +
            $"| Water: {crop.waterCost:0.#}";

        if (shop != null)
        {
            shopCropDetailsText.text +=
                "\nMarket refresh: " +
                FormatTime(shop.RefreshTimeRemaining);
        }
    }

    private void UpdateSeasonalMarketUI()
    {
        if (shop == null)
            return;

        if (lastMarketRevision != shop.MarketRevision)
        {
            lastMarketRevision = shop.MarketRevision;

            PopulateMarketTechnologies();
            UpdateSeedOptionLabels();
        }

        if (marketRefreshText != null)
        {
            marketRefreshText.text =
                "Market refresh: " +
                FormatTime(shop.RefreshTimeRemaining);
        }

        UpdateMarketButtons();
        UpdateTechDetails();
    }

    private void UpdateMarketButtons()
    {
        if (buySeedsButton != null)
        {
            buySeedsButton.interactable =
                crop != null &&
                shop.IsSeedAvailable(crop) &&
                coinWallet != null &&
                coinWallet.Coins >= crop.seedPrice * seedsPerPurchase;
        }

        if (buyFiveSeedsButton != null)
        {
            buyFiveSeedsButton.interactable =
                crop != null &&
                shop.IsSeedAvailable(crop) &&
                coinWallet != null &&
                coinWallet.Coins >= crop.seedPrice * 5;
        }

        if (buyMarketTechnologyButton != null)
        {
            buyMarketTechnologyButton.interactable =
                shop.IsTechnologyAvailable(selectedMarketTechnology) &&
                coinWallet != null &&
                coinWallet.Coins >= selectedMarketTechnology.price;
        }
    }

    // Sementes fora de época continuam selecionáveis para vender a colheita.
    private void UpdateSeedOptionLabels()
    {
        if (shopCropDropdown == null || availableCrops == null)
            return;

        for (int index = 0;
             index < availableCrops.Length &&
             index < shopCropDropdown.options.Count;
             index++)
        {
            CropData item = availableCrops[index];

            if (item == null)
            {
                shopCropDropdown.options[index].text =
                    "Crop not configured";
                shopCropDropdown.options[index].image = null;

                continue;
            }

            string availability = shop.IsSeedAvailable(item)
                ? ""
                : " (out of season)";

            shopCropDropdown.options[index].text =
                item.CropName + availability;
            shopCropDropdown.options[index].image = item.icon;
        }

        shopCropDropdown.RefreshShownValue();
    }

    private void PopulateMarketTechnologies()
    {
        var options = new List<TMP_Dropdown.OptionData>();
        int selectedIndex = 0;

        for (int index = 0; index < shop.TechnologyOfferCount; index++)
        {
            TechnologyData technology = shop.GetTechnologyOffer(index);

            options.Add(new TMP_Dropdown.OptionData(technology.DisplayName, technology.icon, Color.white));

            if (technology == selectedMarketTechnology)
                selectedIndex = index;
        }

        selectedMarketTechnology =
            shop.GetTechnologyOffer(selectedIndex);

        if (marketTechnologyDropdown == null)
            return;

        marketTechnologyDropdown.ClearOptions();

        if (options.Count == 0)
            options.Add(new TMP_Dropdown.OptionData("No seasonal technology available"));

        marketTechnologyDropdown.AddOptions(options);
        marketTechnologyDropdown.SetValueWithoutNotify(selectedIndex);

        marketTechnologyDropdown.interactable =
            shop.TechnologyOfferCount > 0;

        marketTechnologyDropdown.RefreshShownValue();
    }

    public void SelectMarketTechnology(int index)
    {
        if (shop == null)
            return;

        selectedMarketTechnology = shop.GetTechnologyOffer(index);
        UpdateTechDetails();
    }

    private void UpdateTechDetails()
    {
        UpdateIcon(marketTechnologyIcon,
            selectedMarketTechnology != null ? selectedMarketTechnology.icon : null);

        if (marketTechnologyDetailsText == null || shop == null)
            return;

        if (selectedMarketTechnology != null)
        {
            marketTechnologyDetailsText.text =
                selectedMarketTechnology.DisplayName +
                "\nPrice: " +
                selectedMarketTechnology.price +
                " coins";

            if (!string.IsNullOrEmpty(selectedMarketTechnology.EffectDescription))
                marketTechnologyDetailsText.text += "\n\n" + selectedMarketTechnology.EffectDescription;

            if (selectedMarketTechnology.effect == TechnologyEffect.WormComposter &&
                wormComposter != null)
            {
                marketTechnologyDetailsText.text +=
                    "\n\nGenerates " + wormComposter.GenerationAmount.ToString("0.#") +
                    " fertilizer every " + wormComposter.GenerationInterval.ToString("0.#") +
                    "s.\nFertilized crops yield +" +
                    (selectedMarketTechnology.value * 100f).ToString("0") + "% produce.";
            }

            return;
        }

        marketTechnologyDetailsText.text = shop.HasExclusiveCatalog
            ? "No seasonal technology available. Check the next refresh."
            : "Configure the exclusive technology catalog in FarmShop.";
    }

    public void BuySelectedMarketTechnology()
    {
        if (shop == null || selectedMarketTechnology == null)
        {
            ShowMessage("No seasonal technology selected.");
            return;
        }

        shop.BuyMarketTechnology(
            selectedMarketTechnology,
            out string message);

        ShowMessage(message);
    }

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

        if (selectedPlotHydrationSlider != null)
        {
            selectedPlotHydrationSlider.maxValue = 100f;
            selectedPlotHydrationSlider.value =
                plot != null ? plot.Hydration : 0f;
        }
    }

    private void UpdateTechnologiesUI()
    {
        if (technologies == null || unlockedTechnologiesText == null)
            return;

        unlockedTechnologiesText.text =
            $"Technologies: {technologies.GetUnlockedNames()}";
    }

    private void UpdateUpgradesUI()
    {
        UpdateIcon(growingTimeUpgradeIcon, shopTechnologyA != null ? shopTechnologyA.icon : null);
        UpdateIcon(wormComposterUpgradeIcon, shopTechnologyB != null ? shopTechnologyB.icon : null);

        UpdatePermanentUpgrade(shopTechnologyA, growingTimeUpgradeDescriptionText,
            growingTimeUpgradeButton, growingTimeUpgradeButtonText);
        UpdatePermanentUpgrade(shopTechnologyB, wormComposterUpgradeDescriptionText,
            wormComposterUpgradeButton, wormComposterUpgradeButtonText);
    }

    private void UpdatePermanentUpgrade(TechnologyData technology, TMP_Text description,
        Button button, TMP_Text buttonText)
    {
        bool unlocked = technologies != null && technologies.Has(technology);

        if (description != null)
            description.text = DescribeUpgrade(technology);

        if (button != null)
        {
            button.interactable = technology != null && !technology.marketExclusive &&
                !unlocked && shop != null && technologies != null &&
                coinWallet != null && coinWallet.Coins >= technology.price;
        }

        if (buttonText != null)
        {
            buttonText.text = technology == null ? "NOT CONFIGURED" :
                unlocked ? "PURCHASED" : "BUY / " + technology.price + " COINS";
        }
    }

    private string DescribeUpgrade(TechnologyData technology)
    {
        if (technology == null)
            return "Upgrade not configured";

        if (technology.effect == TechnologyEffect.ShowGrowingTime)
            return technology.DisplayName + "\nShows crop growth percentage in its contextual menu.";

        if (technology.effect == TechnologyEffect.WormComposter && wormComposter != null)
        {
            float totalReduction = fertilizerStorage != null
                ? Mathf.Clamp(fertilizerStorage.BaseGrowthTimeReduction +
                    wormComposter.GrowthTimeReductionBonus, 0f, 0.9f)
                : wormComposter.GrowthTimeReductionBonus;

            return technology.DisplayName + "\nGenerates " +
                wormComposter.GenerationAmount.ToString("0.#") + " fertilizer every " +
                wormComposter.GenerationInterval.ToString("0.#") + "s.\nFertilized crops: " +
                (totalReduction * 100f).ToString("0") + "% less remaining growth time; +" +
                (technology.value * 100f).ToString("0") + "% produce.";
        }

        return technology.DisplayName;
    }

    private void UpdateMissionsUI()
    {
        if (missions == null)
            return;

        if (firstMissionDescriptionText != null)
            firstMissionDescriptionText.text = missions.Describe(0);

        if (secondMissionDescriptionText != null)
            secondMissionDescriptionText.text = missions.Describe(1);

        if (thirdMissionDescriptionText != null)
            thirdMissionDescriptionText.text = missions.Describe(2);

        if (missionProgressText != null)
        {
            missionProgressText.text =
                $"Requests: {missions.CompletedMissionCount}/" +
                $"{missions.DemoMissionLimit} " +
                $"| Food delivered: {missions.TotalProduceDelivered} " +
                $"| Communities served: {missions.CommunitiesServed}";
        }

        if (firstMissionDeliverButton != null)
            firstMissionDeliverButton.interactable = missions.CanDeliver(0);

        if (secondMissionDeliverButton != null)
            secondMissionDeliverButton.interactable = missions.CanDeliver(1);

        if (thirdMissionDeliverButton != null)
            thirdMissionDeliverButton.interactable = missions.CanDeliver(2);
    }

    public void BuySeeds()
    {
        BuySeedAmount(seedsPerPurchase);
    }

    public void BuyFiveSeeds()
    {
        BuySeedAmount(5);
    }

    private void BuySeedAmount(int amount)
    {
        if (shop == null || crop == null)
        {
            ShowMessage("Crop or shop not configured.");
            return;
        }

        shop.BuySeeds(crop, amount, out string message);
        ShowMessage(message);
    }

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

    public void BuyTechnologyA()
    {
        BuyTechnology(shopTechnologyA);
    }

    public void BuyTechnologyB()
    {
        BuyTechnology(shopTechnologyB);
    }

    private void BuyTechnology(TechnologyData technology)
    {
        if (shop == null || technology == null)
            return;

        shop.BuyTechnology(technology, out string message);
        ShowMessage(message);
    }

    public void DeliverMissionOne()
    {
        DeliverMission(0);
    }

    public void DeliverMissionTwo()
    {
        DeliverMission(1);
    }

    public void DeliverMissionThree()
    {
        DeliverMission(2);
    }

    public void DeliverCommunity(int index)
    {
        DeliverMission(index);
    }

    private void DeliverMission(int index)
    {
        if (missions == null)
            return;

        missions.Deliver(index, out string message);
        ShowMessage(message);
    }

    private string FormatTime(float seconds)
    {
        int total = Mathf.CeilToInt(Mathf.Max(0f, seconds));

        return (total / 60).ToString("00") +
            ":" +
            (total % 60).ToString("00");
    }

    public void PlayUIClick()
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayFarmClick();
    }

    public void PlayCropClick()
    {
        if (AudioManager.Instance != null) AudioManager.Instance.PlayCropClick();
    }

    public void PlayFarmClose()
    {
        if (AudioManager.Instance != null) AudioManager.Instance.PlayFarmClose();
    }

    public void ShowMessage(string message)
    {
        messageVisibleUntil = Time.unscaledTime + 5f;

        if (actionMessageText != null)
        {
            actionMessageText.text = message;
            actionMessageText.gameObject.SetActive(true);
        }

        Debug.Log(message);
        RefreshHUD();
    }
}
