using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class FarmPlotExpansion : MonoBehaviour
{
    [Header("System References")]
    [SerializeField] private FarmWallet coinWallet;
    [SerializeField] private FarmHUD hud;

    [Header("Extra Planting Spaces")]
    [SerializeField] private GameObject[] extraPlots;
    [SerializeField] private int[] plotPrices = { 100, 200, 800 };

    [Header("Upgrade Panel References")]
    [SerializeField] private Button buyPlotButton;
    [SerializeField] private TMP_Text buyPlotButtonText;
    [SerializeField] private TMP_Text plotExpansionText;

    private int purchasedPlots;
    public int PurchasedPlots => purchasedPlots;
    public int TotalAvailablePlots => 1 + purchasedPlots;
    public bool AllPlotsUnlocked => extraPlots == null || plotPrices == null ||
        purchasedPlots >= Mathf.Min(extraPlots.Length, plotPrices.Length);
    public int NextPlotPrice => !AllPlotsUnlocked && purchasedPlots < plotPrices.Length
        ? plotPrices[purchasedPlots] : 0;

    private void Start()
    {
        RefreshUpgradeUI();
    }

    private void Update()
    {
        RefreshUpgradeUI();
    }

    // Compra um terreno por vez, seguindo os valores 100, 200 e 800.
    public void BuyNextPlot()
    {
        if (AllPlotsUnlocked)
        {
            ShowMessage("All four planting spaces are already unlocked.");
            return;
        }
        if (coinWallet == null || NextPlotPrice <= 0 || !coinWallet.TrySpend(NextPlotPrice))
        {
            ShowMessage("Not enough coins for the next planting space.");
            return;
        }
        extraPlots[purchasedPlots].SetActive(true);
        purchasedPlots++;
        ShowMessage("Planting space unlocked! Available plots: " + TotalAvailablePlots + "/4");
        RefreshUpgradeUI();
    }

    private void ShowMessage(string message)
    {
        if (hud != null) hud.ShowMessage(message);
        else Debug.Log(message);
    }

    // Atualiza o preco, o limite de terrenos e a disponibilidade do botao.
    private void RefreshUpgradeUI()
    {
        if (plotExpansionText != null)
            plotExpansionText.text = "PLANTING SPACES: " + TotalAvailablePlots + "/4\n" +
                (AllPlotsUnlocked ? "All spaces unlocked." : "Next space costs " + NextPlotPrice + " coins.");
        if (buyPlotButtonText != null)
            buyPlotButtonText.text = AllPlotsUnlocked ? "ALL SPACES UNLOCKED" : "UNLOCK PLOT / " + NextPlotPrice + " COINS";
        if (buyPlotButton != null)
            buyPlotButton.interactable = !AllPlotsUnlocked && coinWallet != null && coinWallet.Coins >= NextPlotPrice;
    }
}
