using UnityEngine;

public class FarmWallet : MonoBehaviour
{
    [SerializeField, Min(0)] private int startingCoins = 50;

    public int Coins { get; private set; }

    private void Awake() => Coins = startingCoins;

    public void AddCoins(int amount)
    {
        if (amount > 0)
            Coins += amount;
    }

    public bool TrySpend(int amount)
    {
        // Desconta moedas apenas quando o saldo e suficiente.
        if (amount < 0 || Coins < amount)
            return false;

        Coins -= amount;
        return true;
    }
}