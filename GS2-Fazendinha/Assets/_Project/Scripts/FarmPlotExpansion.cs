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
        ApplyPlotState();
        RefreshUpgradeUI();
    }
    public int PurchasedPlots => purchasedPlots;
    public int TotalAvailablePlots => 1 + purchasedPlots;
    public bool AllPlotsUnlocked => extraPlots == null || plotPrices == null ||
        purchasedPlots >= Mathf.Min(extraPlots.Length, plotPrices.Length);
    public int NextPlotPrice => !AllPlotsUnlocked && purchasedPlots < plotPrices.Length
        ? plotPrices[purchasedPlots] : 0;

    private void Awake()
    {
        ApplyPlotState();
    }

    private void Start()
    {
        ApplyPlotState();
        RefreshUpgradeUI();
    }

    private void ApplyPlotState()
    {
        int limit = extraPlots != null && plotPrices != null
            ? Mathf.Min(extraPlots.Length, plotPrices.Length)
            : 0;
        purchasedPlots = Mathf.Clamp(purchasedPlots, 0, limit);

        if (extraPlots == null)
            return;

        for (int index = 0; index < extraPlots.Length; index++)
        {
            GameObject plot = extraPlots[index];
            if (plot == null || transform.IsChildOf(plot.transform))
                continue;

            plot.SetActive(index < purchasedPlots);
        }
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
        bool canBuy = CanBuyPlot(out string reason);

        if (plotExpansionText != null)
        {
            string details = AllPlotsUnlocked
                ? "All spaces unlocked."
                : "Next space: " + NextPlotPrice + " coins.";

            plotExpansionText.text = "PLANTING SPACES: " + TotalAvailablePlots + "/4\n" +
                (canBuy || AllPlotsUnlocked ? details : reason);
        }

        if (buyPlotButtonText != null)
        {
            buyPlotButtonText.text = AllPlotsUnlocked
                ? "ALL SPACES UNLOCKED"
                : canBuy
                    ? "UNLOCK PLOT / " + NextPlotPrice + " COINS"
                    : reason.ToUpperInvariant();
        }

        if (buyPlotButton != null)
            buyPlotButton.interactable = canBuy;
    }

    private bool CanBuyPlot(out string reason)
    {
        if (AllPlotsUnlocked)
        {
            reason = "All spaces unlocked.";
            return false;
        }

        if (extraPlots[purchasedPlots] == null || NextPlotPrice <= 0 || coinWallet == null)
        {
            reason = "Planting space unavailable.";
            return false;
        }

        int missing = NextPlotPrice - coinWallet.Coins;
        if (missing > 0)
        {
            reason = "Need " + missing + " more coins (" + coinWallet.Coins + "/" + NextPlotPrice + ").";
            return false;
        }

        reason = "";
        return true;
    }
}
