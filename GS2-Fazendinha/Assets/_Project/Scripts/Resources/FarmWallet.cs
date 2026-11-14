using UnityEngine;

public class FarmWallet : MonoBehaviour
{
    [SerializeField, Min(0)] private int startingCoins = 50;

    public int Coins { get; private set; }
    public int StartingCoins => startingCoins;

    public void SaveProgress(string prefix)
    {
        PlayerPrefs.SetInt(prefix + ".Coins", Coins);
    }

    public void LoadProgress(string prefix)
    {
        Coins = Mathf.Max(0, PlayerPrefs.GetInt(prefix + ".Coins", startingCoins));
    }

    private void Awake() => Coins = startingCoins;

    public void AddCoins(int amount)
    {
        if (amount > 0)
            Coins += amount;
    }

    public bool TrySpend(int amount)
    {
        if (amount < 0 || Coins < amount)
            return false;

        Coins -= amount;
        return true;
    }
}
