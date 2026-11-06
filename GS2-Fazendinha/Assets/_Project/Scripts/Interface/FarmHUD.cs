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

    [Header("HUD Text References")]

    // Texto que mostra o saldo de moedas.
    [SerializeField] private TMP_Text coinCounterText;

    // Texto que mostra a agua atual e a capacidade do reservatorio.
    [SerializeField] private TMP_Text waterAmountText;

    // Texto que mostra produtos colhidos e sementes da cultura selecionada.
    [SerializeField] private TMP_Text cropInventoryText;

    // Texto que mostra a cultura, o estado e a hidratacao do terreno.
    [SerializeField] private TMP_Text selectedPlotText;

    // Texto que lista as tecnologias desbloqueadas.
    [SerializeField] private TMP_Text unlockedTechnologiesText;

    // Textos que mostram as descricoes das duas primeiras missoes.
    [SerializeField] private TMP_Text firstMissionDescriptionText;
    [SerializeField] private TMP_Text secondMissionDescriptionText;

    // Texto que mostra o tempo para regenerar agua.
    [SerializeField] private TMP_Text waterRegenerationTimerText;

    // Texto que mostra o resultado das acoes e mensagens de erro.
    [SerializeField] private TMP_Text actionMessageText;

    [Header("HUD Progress Bars")]

    // Barra que mostra a quantidade de agua no reservatorio.
    [SerializeField] private Slider waterAmountSlider;

    // Barra que mostra a hidratacao da planta selecionada.
    [SerializeField] private Slider selectedPlotHydrationSlider;

    [Header("UI Settings")]
    [SerializeField, Min(0.05f)]
    private float refreshInterval = 0.15f;

    private float nextRefreshTime;
    private float messageVisibleUntil;

    private void Start()
    {
        RefreshHUD();
        nextRefreshTime = Time.unscaledTime + refreshInterval;
    }

    private void Update()
    {
        RefreshIfNeeded();
        UpdateMessageVisibility();
    }

    // Esconde mensagens antigas para deixar a tela de jogo limpa.
    private void UpdateMessageVisibility()
    {
        if (actionMessageText != null)
            actionMessageText.gameObject.SetActive(Time.unscaledTime < messageVisibleUntil);
    }

    // Atualiza a interface em intervalos definidos.
    private void RefreshIfNeeded()
    {
        if (Time.unscaledTime < nextRefreshTime)
            return;

        nextRefreshTime = Time.unscaledTime + refreshInterval;
        RefreshHUD();
    }

    // Atualiza todas as informacoes exibidas pelo HUD.
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
    }

    // Mostra os precos e recursos da cultura escolhida na loja.
    private void UpdateShopCropUI()
    {
        if (crop == null || shopCropDetailsText == null) return;
        int seeds = inventory != null ? inventory.SeedCount(crop) : 0;
        int produce = inventory != null ? inventory.ProduceCount(crop) : 0;
        shopCropDetailsText.text = crop.CropName + "\n\n" +
            "Seeds owned: " + seeds + "\nProduce owned: " + produce + "\n\n" +
            "Buy " + seedsPerPurchase + " seeds: " + crop.seedPrice * seedsPerPurchase + " coins\n" +
            "Sell " + producePerSale + " produce: " + crop.sellPrice * producePerSale + " coins\n\n" +
            "Growth time: " + (crop.plantedSeconds + crop.growingSeconds).ToString("0") + "s\n" +
            "Water per use: " + crop.waterCost.ToString("0.#");
    }

    // Seleciona a cultura da loja pelo indice do dropdown.
    public void SelectShopCrop(int index)
    {
        if (availableCrops == null || index < 0 || index >= availableCrops.Length) return;
        SetCrop(availableCrops[index]);
    }

    // Mostra o saldo atual de moedas.
    private void UpdateCoinUI()
    {
        if (coinWallet == null || coinCounterText == null)
            return;

        coinCounterText.text = $"Coins: {coinWallet.Coins}";
    }

    // Mostra a quantidade de agua e atualiza sua barra.
    private void UpdateWaterUI()
    {
        if (waterTank == null)
            return;

        if (waterAmountText != null)
        {
            waterAmountText.text =
                $"Water: {waterTank.CurrentWater:0.0}/{waterTank.Capacity:0.0}";
        }

        if (waterAmountSlider != null)
        {
            waterAmountSlider.maxValue = waterTank.Capacity;
            waterAmountSlider.value = waterTank.CurrentWater;
        }
    }

    // Mostra o tempo restante para regenerar agua.
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
            $"Next Regeneration: {waterTank.RegenerationTimeRemaining:0.0}s";
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
            $"{crop.CropName}\nProduce: {inventory.ProduceCount(crop)} " +
            $"| Seeds: {inventory.SeedCount(crop)}";
    }

    // Troca a cultura utilizada pelo HUD.
    public void SetCrop(CropData newCrop)
    {
        if (newCrop == null)
            return;

        crop = newCrop;
        if (availableCrops != null && shopCropDropdown != null)
        {
            for (int index = 0; index < availableCrops.Length; index++)
                if (availableCrops[index] == crop)
                    shopCropDropdown.SetValueWithoutNotify(index);
        }
        RefreshHUD();
    }

    // Mostra o estado e a hidratacao do terreno selecionado.
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

    // Lista as tecnologias que ja foram desbloqueadas.
    private void UpdateTechnologiesUI()
    {
        if (technologies == null || unlockedTechnologiesText == null)
            return;

        unlockedTechnologiesText.text =
            $"Technologies: {technologies.GetUnlockedNames()}";
    }

    // Mostra as descricoes das duas primeiras missoes.
    private void UpdateMissionsUI()
    {
        if (missions == null)
            return;

        if (firstMissionDescriptionText != null)
            firstMissionDescriptionText.text = missions.Describe(0);

        if (secondMissionDescriptionText != null)
            secondMissionDescriptionText.text = missions.Describe(1);
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

    // Vende produtos usando a cultura selecionada.
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

    // Encaminha a compra da tecnologia para a loja.
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

    // Encaminha a entrega para o quadro de missoes.
    private void DeliverMission(int index)
    {
        if (missions == null)
            return;

        missions.Deliver(index, out string message);
        ShowMessage(message);
    }

    // Mostra o resultado da acao e atualiza o HUD.
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
