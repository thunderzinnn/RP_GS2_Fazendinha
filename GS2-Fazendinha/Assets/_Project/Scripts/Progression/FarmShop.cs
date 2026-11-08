using System.Collections.Generic;
using UnityEngine;

public class FarmShop : MonoBehaviour
{
    [Header("System References")]
    [SerializeField] private FarmInventory inventory;
    [SerializeField] private FarmWallet wallet;
    [SerializeField] private FarmTechnologyManager technologies;

    [Header("Seasonal Catalogs")]
    [SerializeField] private CropData[] cropCatalog;
    [SerializeField] private TechnologyData[] exclusiveTechnologyCatalog;

    [SerializeField, Min(1)]
    private int seedOffersPerRotation = 3;

    [SerializeField, Min(1)]
    private int technologyOffersPerRotation = 1;

    [Header("Market Rotation")]
    [SerializeField, Min(1f)]
    private float refreshInterval = 210f;

    [SerializeField, Min(1f)]
    private float demandPriceMultiplier = 1.5f;

    private readonly List<CropData> seedOffers =
        new List<CropData>();

    private readonly List<TechnologyData> technologyOffers =
        new List<TechnologyData>();

    private int rotationIndex = -1;
    private float refreshTimer;

    public int MarketRevision { get; private set; }

    public CropData HighDemandCrop { get; private set; }

    public float RefreshTimeRemaining =>
        Mathf.Max(0f, refreshTimer);

    public int TechnologyOfferCount => technologyOffers.Count;

    public bool HasExclusiveCatalog =>
        exclusiveTechnologyCatalog != null &&
        exclusiveTechnologyCatalog.Length > 0;

    private void Awake()
    {
        RefreshMarketOffers();
    }

    private void Update()
    {
        TickMarket(Time.deltaTime);
    }

    // Aproveita as culturas do HUD quando não existe um catálogo próprio.
    public void ConfigureCropCatalogIfEmpty(CropData[] crops)
    {
        if (cropCatalog != null && cropCatalog.Length > 0)
            return;

        if (crops == null || crops.Length == 0)
            return;

        cropCatalog = crops;
        rotationIndex = -1;

        RefreshMarketOffers();
    }

    // Conta o tempo de jogo até a próxima renovação das ofertas.
    public void TickMarket(float seconds)
    {
        if (seconds <= 0f)
            return;

        refreshTimer -= seconds;

        if (refreshTimer > 0f)
            return;

        float interval = Mathf.Max(1f, refreshInterval);
        float overdue = -refreshTimer;

        int rotations =
            1 + Mathf.FloorToInt(overdue / interval);

        rotationIndex += rotations - 1;

        RefreshMarketOffers();

        refreshTimer = interval - overdue % interval;
    }

    // Renova as sementes, as tecnologias e a cultura com maior demanda.
    private void RefreshMarketOffers()
    {
        rotationIndex++;

        seedOffers.Clear();
        technologyOffers.Clear();

        HighDemandCrop = null;

        PopulateSeedOffers();
        PopulateTechnologyOffers();

        refreshTimer = Mathf.Max(1f, refreshInterval);
        MarketRevision++;
    }

    // Percorre as culturas em ordem circular, sem repetir uma oferta.
    private void PopulateSeedOffers()
    {
        if (cropCatalog == null || cropCatalog.Length == 0)
            return;

        int offerLimit = Mathf.Max(1, seedOffersPerRotation);

        for (int offset = 0;
             offset < cropCatalog.Length && seedOffers.Count < offerLimit;
             offset++)
        {
            int index =
                (rotationIndex + offset) % cropCatalog.Length;

            CropData crop = cropCatalog[index];

            if (crop != null && !seedOffers.Contains(crop))
                seedOffers.Add(crop);
        }

        if (seedOffers.Count > 0)
            HighDemandCrop = seedOffers[0];
    }

    // Oferece somente tecnologias exclusivas que ainda não foram desbloqueadas.
    private void PopulateTechnologyOffers()
    {
        if (!HasExclusiveCatalog)
            return;

        int offerLimit =
            Mathf.Max(1, technologyOffersPerRotation);

        for (int offset = 0;
             offset < exclusiveTechnologyCatalog.Length &&
             technologyOffers.Count < offerLimit;
             offset++)
        {
            int index =
                (rotationIndex + offset) %
                exclusiveTechnologyCatalog.Length;

            TechnologyData technology =
                exclusiveTechnologyCatalog[index];

            if (technology == null ||
                !technology.marketExclusive ||
                technologyOffers.Contains(technology))
            {
                continue;
            }

            if (technologies != null && technologies.Has(technology))
                continue;

            technologyOffers.Add(technology);
        }
    }

    // Sem catálogo configurado, preserva o comportamento anterior da loja.
    public bool IsSeedAvailable(CropData crop)
    {
        if (crop == null)
            return false;

        return cropCatalog == null ||
            cropCatalog.Length == 0 ||
            seedOffers.Contains(crop);
    }

    // Aplica a demanda ao preço e arredonda para uma quantidade inteira de moedas.
    public int GetSellPrice(CropData crop)
    {
        if (crop == null)
            return 0;

        float multiplier = crop == HighDemandCrop
            ? Mathf.Max(1f, demandPriceMultiplier)
            : 1f;

        return Mathf.Max(
            1,
            Mathf.RoundToInt(crop.sellPrice * multiplier));
    }

    public TechnologyData GetTechnologyOffer(int index)
    {
        if (index < 0 || index >= technologyOffers.Count)
            return null;

        return technologyOffers[index];
    }

    // Confere a oferta atual e impede a compra de uma tecnologia já adquirida.
    public bool IsTechnologyAvailable(TechnologyData technology)
    {
        return technology != null &&
            technology.marketExclusive &&
            technologyOffers.Contains(technology) &&
            technologies != null &&
            !technologies.Has(technology);
    }

    // Compra sementes da cultura recebida, respeitando a época atual.
    public bool BuySeeds(
        CropData crop,
        int amount,
        out string message)
    {
        if (crop == null ||
            amount <= 0 ||
            inventory == null ||
            wallet == null)
        {
            message = "Shop not configured.";
            return false;
        }

        if (!IsSeedAvailable(crop))
        {
            message =
                "These seeds are out of season. " +
                "Wait for the next market refresh.";

            return false;
        }

        int cost = crop.seedPrice * amount;

        if (!wallet.TrySpend(cost))
        {
            message = "Not enough coins for seeds.";
            return false;
        }

        inventory.AddSeeds(crop, amount);

        message =
            "Bought " + amount +
            " seeds for " + crop.CropName +
            " for " + cost + " coins.";

        return true;
    }

    // Permite vender qualquer cultura, usando o preço da demanda atual.
    public bool SellProduce(
        CropData crop,
        int amount,
        out string message)
    {
        if (crop == null ||
            amount <= 0 ||
            inventory == null ||
            wallet == null)
        {
            message = "Market not configured.";
            return false;
        }

        if (!inventory.TryRemoveProduce(crop, amount))
        {
            message = "Not enough produce to sell.";
            return false;
        }

        int earned = GetSellPrice(crop) * amount;

        wallet.AddCoins(earned);

        message =
            "Sold " + amount +
            " " + crop.CropName +
            ": +" + earned + " coins.";

        return true;
    }

    // Preserva as melhorias permanentes e valida as exclusivas pela oferta atual.
    public bool BuyTechnology(
        TechnologyData technology,
        out string message)
    {
        if (technology != null && technology.marketExclusive)
            return BuyMarketTechnology(technology, out message);

        return TryPurchaseTechnology(technology, out message);
    }

    // Retira a tecnologia comprada das ofertas até a próxima renovação.
    public bool BuyMarketTechnology(
        TechnologyData technology,
        out string message)
    {
        if (!IsTechnologyAvailable(technology))
        {
            message =
                "This technology is not currently offered by the market, " +
                "or is already unlocked.";

            return false;
        }

        if (!TryPurchaseTechnology(technology, out message))
            return false;

        technologyOffers.Remove(technology);
        MarketRevision++;

        return true;
    }

    // Centraliza o pagamento e o desbloqueio para os dois tipos de compra.
    private bool TryPurchaseTechnology(
        TechnologyData technology,
        out string message)
    {
        if (technology == null ||
            wallet == null ||
            technologies == null)
        {
            message = "Technology unavailable.";
            return false;
        }

        if (technologies.Has(technology))
        {
            message = "Technology already unlocked.";
            return false;
        }

        if (!wallet.TrySpend(technology.price))
        {
            message = "Not enough coins for this technology.";
            return false;
        }

        technologies.Unlock(technology);

        message =
            "Technology purchased: " +
            technology.DisplayName;

        return true;
    }
}