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

    public void SaveProgress(string prefix)
    {
        PlayerPrefs.SetInt(prefix + ".PurchasedPlots", purchasedPlots);
    }

    public void LoadProgress(string prefix)
    {
        int limit = extraPlots != null && plotPrices != null ? Mathf.Min(extraPlots.Length, plotPrices.Length) : 0;
        purchasedPlots = Mathf.Clamp(PlayerPrefs.GetInt(prefix + ".PurchasedPlots", 0), 0, limit);
        for (int index = 0; index < limit; index++)
            if (extraPlots[index] != null) extraPlots[index].SetActive(index < purchasedPlots);
        RefreshUpgradeUI();
    }
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

    public void BuyNextPlot()
    {
        if (AllPlotsUnlocked)
        {
            ShowMessage("All four planting spaces are already unlocked.");
            return;
        }
        if (extraPlots[purchasedPlots] == null)
        {
            ShowMessage("Next planting space is not configured.");
            return;
        }
        if (coinWallet == null || NextPlotPrice <= 0 || !coinWallet.TrySpend(NextPlotPrice))
        {
            ShowMessage("Not enough coins for the next planting space.");
            return;
        }
        extraPlots[purchasedPlots].SetActive(true);
        purchasedPlots++;
        if (AudioManager.Instance != null) AudioManager.Instance.PlayPurchase();
        ShowMessage("Planting space unlocked! Available plots: " + TotalAvailablePlots + "/4");
        RefreshUpgradeUI();
    }

    private void ShowMessage(string message)
    {
        if (hud != null) hud.ShowMessage(message);
        else Debug.Log(message);
    }

    private void RefreshUpgradeUI()
    {
        if (plotExpansionText != null)
            plotExpansionText.text = "PLANTING SPACES: " + TotalAvailablePlots + "/4\n" +
                (AllPlotsUnlocked ? "All spaces unlocked." : "Next space costs " + NextPlotPrice + " coins.");
        if (buyPlotButtonText != null)
            buyPlotButtonText.text = AllPlotsUnlocked ? "ALL SPACES UNLOCKED" : "UNLOCK PLOT / " + NextPlotPrice + " COINS";
        if (buyPlotButton != null)
            buyPlotButton.interactable = !AllPlotsUnlocked &&
                extraPlots[purchasedPlots] != null && NextPlotPrice > 0 &&
                coinWallet != null && coinWallet.Coins >= NextPlotPrice;
    }
}
