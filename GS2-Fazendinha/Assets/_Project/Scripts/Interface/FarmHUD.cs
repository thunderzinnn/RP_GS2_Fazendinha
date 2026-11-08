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

    [Header("Crop Settings")]
    [SerializeField] private CropData crop;

    // Culturas e textos usados pelo painel da loja.
    [SerializeField] private CropData[] availableCrops;
    [SerializeField] private TMP_Dropdown shopCropDropdown;
    [SerializeField] private TMP_Text shopCropDetailsText;
    [SerializeField, Min(1)] private int seedsPerPurchase = 5;
    [SerializeField, Min(1)] private int producePerSale = 1;

    [Header("Shop Technologies")]
    [SerializeField] private TechnologyData shopTechnologyA;
    [SerializeField] private TechnologyData shopTechnologyB;

    [Header("Seasonal Market UI")]
    [SerializeField] private TMP_Dropdown marketTechnologyDropdown;
    [SerializeField] private TMP_Text marketTechnologyDetailsText;
    [SerializeField] private TMP_Text marketRefreshText;
    [SerializeField] private Button buySeedsButton;
    [SerializeField] private Button buyMarketTechnologyButton;

    [Header("Mission Progress UI")]
    [SerializeField] private TMP_Text missionProgressText;
    [SerializeField] private Button firstMissionDeliverButton;
    [SerializeField] private Button secondMissionDeliverButton;

    [Header("HUD Text References")]

    // Mostra o saldo de moedas.
    [SerializeField] private TMP_Text coinCounterText;

    // Mostra a água atual e a capacidade do reservatório.
    [SerializeField] private TMP_Text waterAmountText;

    // Mostra produtos colhidos e sementes da cultura selecionada.
    [SerializeField] private TMP_Text cropInventoryText;

    // Mostra a cultura, o estado e a hidratação do terreno.
    [SerializeField] private TMP_Text selectedPlotText;

    // Lista as tecnologias desbloqueadas.
    [SerializeField] private TMP_Text unlockedTechnologiesText;

    // Mostram os requisitos e os tempos das duas primeiras comunidades.
    [SerializeField] private TMP_Text firstMissionDescriptionText;
    [SerializeField] private TMP_Text secondMissionDescriptionText;

    // Mostra o tempo para regenerar água.
    [SerializeField] private TMP_Text waterRegenerationTimerText;

    // Mostra resultados das ações e mensagens de erro.
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

    // Aproveita as culturas existentes e conecta a seleção de tecnologias.
    private void InitializeMarketUI()
    {
        if (shop != null)
            shop.ConfigureCropCatalogIfEmpty(availableCrops);

        if (marketTechnologyDropdown != null)
        {
            marketTechnologyDropdown.onValueChanged.AddListener(
                SelectMarketTechnology);
        }
    }

    // Esconde mensagens antigas para deixar a tela de jogo limpa.
    private void UpdateMessageVisibility()
    {
        if (actionMessageText == null)
            return;

        actionMessageText.gameObject.SetActive(
            Time.unscaledTime < messageVisibleUntil);
    }

    // Atualiza a interface em intervalos definidos.
    private void RefreshIfNeeded()
    {
        if (Time.unscaledTime < nextRefreshTime)
            return;

        nextRefreshTime = Time.unscaledTime + refreshInterval;
        RefreshHUD();
    }

    // Atualiza todas as informações exibidas pelo HUD.
    public void RefreshHUD()
    {
        UpdateCoinUI();
        UpdateWaterUI();
        UpdateCropUI();
        UpdateSelectedPlotUI();
        UpdateTechnologiesUI();
        UpdateMissionsUI();
        UpdateWaterRegenerationUI();
        UpdateShopCropUI();
        UpdateSeasonalMarketUI();
    }

    // Mostra o saldo atual de moedas.
    private void UpdateCoinUI()
    {
        if (coinWallet == null || coinCounterText == null)
            return;

        coinCounterText.text = $"Coins: {coinWallet.Coins}";
    }

    // Mostra a quantidade de água e atualiza sua barra.
    private void UpdateWaterUI()
    {
        if (waterTank == null)
            return;

        if (waterAmountText != null)
        {
            waterAmountText.text =
                $"Water: {waterTank.CurrentWater:0.0}/" +
                $"{waterTank.Capacity:0.0}";
        }

        if (waterAmountSlider != null)
        {
            waterAmountSlider.maxValue = waterTank.Capacity;
            waterAmountSlider.value = waterTank.CurrentWater;
        }
    }

    // Mostra o tempo restante para regenerar água.
    private void UpdateWaterRegenerationUI()
    {
        if (waterTank == null || waterRegenerationTimerText == null)
            return;

        if (waterTank.CurrentWater >= waterTank.Capacity)
        {
            waterRegenerationTimerText.text = "Water Tank Full";
            return;
        }

        waterRegenerationTimerText.text =
            $"Next Regeneration: " +
            $"{waterTank.RegenerationTimeRemaining:0.0}s";
    }

    // Mostra os produtos e sementes da cultura selecionada.
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

    // Troca a cultura e sincroniza o dropdown sem disparar outro evento.
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

    // Seleciona a cultura da loja pelo índice do dropdown.
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

    // Mostra os preços reais e a disponibilidade da cultura na época atual.
    private void UpdateShopCropUI()
    {
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
            $"Buy {seedsPerPurchase} seeds: " +
            $"{crop.seedPrice * seedsPerPurchase} coins\n" +
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

    // Reconstrói as opções somente quando as ofertas mudam.
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
        UpdateMarketTechnologyDetails();
    }

    // Bloqueia compras sem saldo ou sem oferta disponível.
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

                continue;
            }

            string availability = shop.IsSeedAvailable(item)
                ? ""
                : " (out of season)";

            shopCropDropdown.options[index].text =
                item.CropName + availability;
        }

        shopCropDropdown.RefreshShownValue();
    }

    // Preenche o dropdown com as tecnologias oferecidas neste ciclo.
    private void PopulateMarketTechnologies()
    {
        List<string> options = new List<string>();
        int selectedIndex = 0;

        for (int index = 0; index < shop.TechnologyOfferCount; index++)
        {
            TechnologyData technology = shop.GetTechnologyOffer(index);

            options.Add(technology.DisplayName);

            if (technology == selectedMarketTechnology)
                selectedIndex = index;
        }

        selectedMarketTechnology =
            shop.GetTechnologyOffer(selectedIndex);

        if (marketTechnologyDropdown == null)
            return;

        marketTechnologyDropdown.ClearOptions();

        if (options.Count == 0)
            options.Add("No seasonal technology available");

        marketTechnologyDropdown.AddOptions(options);
        marketTechnologyDropdown.SetValueWithoutNotify(selectedIndex);

        marketTechnologyDropdown.interactable =
            shop.TechnologyOfferCount > 0;

        marketTechnologyDropdown.RefreshShownValue();
    }

    // Troca a tecnologia selecionada sem executar uma compra.
    public void SelectMarketTechnology(int index)
    {
        if (shop == null)
            return;

        selectedMarketTechnology = shop.GetTechnologyOffer(index);
        UpdateMarketTechnologyDetails();
    }

    // Mostra a oferta selecionada ou informa a ausência de tecnologias.
    private void UpdateMarketTechnologyDetails()
    {
        if (marketTechnologyDetailsText == null || shop == null)
            return;

        if (selectedMarketTechnology != null)
        {
            marketTechnologyDetailsText.text =
                selectedMarketTechnology.DisplayName +
                "\nPrice: " +
                selectedMarketTechnology.price +
                " coins";

            return;
        }

        marketTechnologyDetailsText.text = shop.HasExclusiveCatalog
            ? "No seasonal technology available. Check the next refresh."
            : "Configure the exclusive technology catalog in FarmShop.";
    }

    // Encaminha a compra da tecnologia sazonal para o mercado.
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

    // Mostra o estado e a hidratação do terreno selecionado.
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

    // Lista as tecnologias que já foram desbloqueadas.
    private void UpdateTechnologiesUI()
    {
        if (technologies == null || unlockedTechnologiesText == null)
            return;

        unlockedTechnologiesText.text =
            $"Technologies: {technologies.GetUnlockedNames()}";
    }

    // Mostra os requisitos, os temporizadores e o progresso das entregas.
    private void UpdateMissionsUI()
    {
        if (missions == null)
            return;

        if (firstMissionDescriptionText != null)
            firstMissionDescriptionText.text = missions.Describe(0);

        if (secondMissionDescriptionText != null)
            secondMissionDescriptionText.text = missions.Describe(1);

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
    }

    // Compra sementes usando a cultura selecionada.
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

    // Vende produtos pelo preço atual do mercado.
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

    // Preserva os botões de compra das melhorias já existentes.
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

    // Permite conectar outras comunidades pelo índice no Inspector.
    public void DeliverCommunity(int index)
    {
        DeliverMission(index);
    }

    // Encaminha a entrega para o quadro de missões.
    private void DeliverMission(int index)
    {
        if (missions == null)
            return;

        missions.Deliver(index, out string message);
        ShowMessage(message);
    }

    // Converte os segundos restantes para o formato minutos:segundos.
    private string FormatTime(float seconds)
    {
        int total = Mathf.CeilToInt(Mathf.Max(0f, seconds));

        return (total / 60).ToString("00") +
            ":" +
            (total % 60).ToString("00");
    }

    // Mostra o resultado da ação e mantém a mensagem visível por cinco segundos.
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