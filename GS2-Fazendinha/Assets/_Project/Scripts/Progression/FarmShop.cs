using UnityEngine;

public class FarmShop : MonoBehaviour
{
    [SerializeField] private FarmInventory inventory;
    [SerializeField] private FarmWallet wallet;
    [SerializeField] private FarmTechnologyManager technologies;

    public bool BuySeeds(CropData crop, int amount, out string message)
    {
        if (crop == null || amount <= 0 || inventory == null || wallet == null)
        {
            message = "Loja nao configurada.";
            return false;
        }
        int cost = crop.seedPrice * amount;
        if (!wallet.TrySpend(cost))
        {
            message = "Moedas insuficientes para sementes.";
            return false;
        }
        inventory.AddSeeds(crop, amount);
        message = "Comprou " + amount + " sementes de " + crop.CropName + " por " + cost + " moedas.";
        return true;
    }

    public bool SellProduce(CropData crop, int amount, out string message)
    {
        if (crop == null || amount <= 0 || inventory == null || wallet == null)
        {
            message = "Mercado nao configurado.";
            return false;
        }
        if (!inventory.TryRemoveProduce(crop, amount))
        {
            message = "Produtos insuficientes para vender.";
            return false;
        }
        int earned = crop.sellPrice * amount;
        wallet.AddCoins(earned);
        message = "Vendeu " + amount + " " + crop.CropName + ": +" + earned + " moedas.";
        return true;
    }

    public bool BuyTechnology(TechnologyData technology, out string message)
    {
        if (technology == null || wallet == null || technologies == null)
        {
            message = "Tecnologia indisponivel.";
            return false;
        }
        if (technologies.Has(technology))
        {
            message = "Tecnologia ja desbloqueada.";
            return false;
        }
        if (!wallet.TrySpend(technology.price))
        {
            message = "Moedas insuficientes para a tecnologia.";
            return false;
        }
        technologies.Unlock(technology);
        message = "Tecnologia adquirida: " + technology.DisplayName;
        return true;
    }
}
