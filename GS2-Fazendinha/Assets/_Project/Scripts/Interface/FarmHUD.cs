
using TMPro;
using UnityEngine.UI;
using UnityEngine;

public class FarmHUD : MonoBehaviour
{
    [Header("Sistemas")]
    [SerializeField] private PlayerFarmController player;
    [SerializeField] private FarmInventory inventory;
    [SerializeField] private FarmWallet wallet;
    [SerializeField] private FarmWaterTank tank;
    [SerializeField] private FarmTechnologyManager technologies;
    [SerializeField] private FarmMissionBoard missions;
    [SerializeField] private FarmShop shop;

    [Header("Itens do prototipo")]
    [SerializeField] private CropData wheat;
    [SerializeField] private CropData corn;
    [SerializeField] private TechnologyData shopTechnologyA;
    [SerializeField] private TechnologyData shopTechnologyB;

    [Header("Textos TMP da interface")]
    [SerializeField] private TMP_Text walletText;
    [SerializeField] private TMP_Text tankText;
    [SerializeField] private TMP_Text resourcesText;
    [SerializeField] private TMP_Text selectedPlotText;
    [SerializeField] private TMP_Text technologiesText;
    [SerializeField] private TMP_Text missionOneText;
    [SerializeField] private TMP_Text missionTwoText;
    [SerializeField] private TMP_Text collectWaterText;
    [SerializeField] private TMP_Text feedbackText;
    [SerializeField] private Slider tankSlider;
    [SerializeField] private Slider plotHydrationSlider;

    private void Update()
    {
        if (tankSlider != null && tank != null)
        {
            tankSlider.maxValue = tank.Capacity;
            tankSlider.value = tank.CurrentWater;
        }

        if (plotHydrationSlider != null && player != null)
        {
            plotHydrationSlider.maxValue = 100f;
            plotHydrationSlider.value = player.LastSelectedPlot != null
                ? player.LastSelectedPlot.Hydration : 0f;
        }

        if (walletText != null && wallet != null)
            walletText.text = "Moedas: " + wallet.Coins;

        if (tankText != null && tank != null)
            tankText.text = "Agua: " + tank.CurrentWater.ToString("0.0") +
                "/" + tank.Capacity.ToString("0.0");

        if (resourcesText != null && inventory != null)
            resourcesText.text = "Trigo: " + CropText(wheat) +
                " | Milho: " + CropText(corn);

        if (selectedPlotText != null && player != null)
        {
            FarmPlot plot = player.LastSelectedPlot;
            selectedPlotText.text = plot == null ? "Terreno: nenhum" :
                "Terreno: " + plot.StateName + " | Hidratacao: " +
                plot.Hydration.ToString("0") + "%";
        }

        if (technologiesText != null && technologies != null)
            technologiesText.text = "Tecnologias: " + technologies.GetUnlockedNames();

        if (missionOneText != null && missions != null)
            missionOneText.text = missions.Describe(0);

        if (missionTwoText != null && missions != null)
            missionTwoText.text = missions.Describe(1);

        if (collectWaterText != null && tank != null)
            collectWaterText.text = tank.CollectionCooldownRemaining > 0f
                ? "Captacao em " + tank.CollectionCooldownRemaining.ToString("0") + "s"
                : "Captacao disponivel";
    }

    private string CropText(CropData crop)
    {
        if (crop == null) return "nao configurado";
        return inventory.ProduceCount(crop) + " (sementes: " + inventory.SeedCount(crop) + ")";
    }

    public void ShowMessage(string message)
    {
        if (feedbackText != null) feedbackText.text = message;
        Debug.Log(message);
    }

    public void BuyWheatSeeds() => BuySeeds(wheat);
    public void BuyCornSeeds() => BuySeeds(corn);

    private void BuySeeds(CropData crop)
    {
        if (shop != null) { shop.BuySeeds(crop, 5, out string message); ShowMessage(message); }
    }

    public void SellWheat() => Sell(wheat);
    public void SellCorn() => Sell(corn);

    private void Sell(CropData crop)
    {
        if (shop != null) { shop.SellProduce(crop, 1, out string message); ShowMessage(message); }
    }

    public void BuyTechnologyA() => BuyTechnology(shopTechnologyA);
    public void BuyTechnologyB() => BuyTechnology(shopTechnologyB);

    private void BuyTechnology(TechnologyData technology)
    {
        if (shop != null) { shop.BuyTechnology(technology, out string message); ShowMessage(message); }
    }

    public void DeliverMissionOne() => Deliver(0);
    public void DeliverMissionTwo() => Deliver(1);

    private void Deliver(int index)
    {
        if (missions != null) { missions.Deliver(index, out string message); ShowMessage(message); }
    }

    public void CollectWater()
    {
        if (tank == null) return;
        float collected = tank.CollectWater();
        ShowMessage(collected > 0f
            ? "Agua coletada: +" + collected.ToString("0.0")
            : "Captacao em recarga ou tanque cheio.");
    }
}
