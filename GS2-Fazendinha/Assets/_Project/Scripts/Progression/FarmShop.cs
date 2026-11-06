using UnityEngine;

public class FarmShop : MonoBehaviour
{
    [SerializeField] private FarmInventory inventory;
    [SerializeField] private FarmWallet wallet;
    [SerializeField] private FarmTechnologyManager technologies;

    public bool BuySeeds(
        CropData crop,
        int amount,
        out string message)
    {
        // Compra sementes da cultura recebida, sem nomes fixos.
        if (crop == null ||
            amount <= 0 ||
            inventory == null ||
            wallet == null)
        {
            message = "Shop not configured.";
            return false;
        }

        int cost = crop.seedPrice * amount;

        if (!wallet.TrySpend(cost))
        {
            message = "Not enough coins for seeds.";
            return false;
        }

        inventory.AddSeeds(crop, amount);

        message = "Bought " + amount +
            " seeds for " + crop.CropName +
            " for " + cost + " coins.";

        return true;
    }

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

        int earned = crop.sellPrice * amount;

        wallet.AddCoins(earned);

        message = "Sold " + amount +
            " " + crop.CropName +
            ": +" + earned + " coins.";

        return true;
    }

    public bool BuyTechnology(
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

        message = "Technology purchased: " +
            technology.DisplayName;

        return true;
    }
}